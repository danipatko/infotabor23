use ctrlc;
use roblib_client::{roblib::roland::*, transports::tcp::Tcp, Result, Robot};
use std::{
    sync::{
        atomic::{AtomicBool, Ordering},
        Arc,
    },
    time::Duration,
};

const WEIGHTS: [i32; 4] = [-3, -1, 1, 3];
const MAX_WEIGHT: f32 = 4f32; // sum of weights on one side
const STRAIGHT_TRESH: f32 = 0.25;
const AVG_WINDOW: i32 = 20;

// const TEST_RUN: [f32; 206] = [
//     0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
//     -0.75, -0.75, -0.75, 0.0, 0.0, 0.0, 0.75, 0.75, 0.75, 0.75, 0.0, -0.25, -0.25, -0.25, -0.25,
//     -0.25, -0.25, -0.25, -0.25, -0.25, -0.25, -0.25, -0.25, -0.25, 0.0, 0.0, 0.0, 0.75, 0.75, 0.75,
//     0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.25, 0.0, 0.25, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75,
//     0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.0, 0.0, 0.0, -0.75, -0.75, -0.75, -0.75, 0.0, 0.0, 0.75,
//     0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75,
//     0.75, 0.75, 0.75, 0.0, 0.0, 0.0, -0.75, -0.75, -0.75, -0.75, 0.0, 0.0, 0.75, 0.75, 0.75, 0.75,
//     0.75, 0.75, 0.25, 0.0, 0.0, 0.0, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75,
//     0.0, -0.75, -0.75, -0.75, -0.75, -0.75, -0.75, -0.75, -0.75, -0.75, -0.75, -0.75, -0.75, -0.75,
//     -0.75, -0.25, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75,
//     0.75, 0.75, 0.25, 0.0, 0.0, 0.0, 0.25, 0.25, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75,
//     0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.25, 0.0, 0.0, 0.0,
//     0.0, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.0, -0.75, -0.75,
//     -0.75, 0.75,
// ];

#[derive(PartialEq, Eq, Debug)]
enum Segment {
    Straight,
    Left,
    Right,
}

fn get_segment(angle: f32) -> Segment {
    if angle > STRAIGHT_TRESH {
        Segment::Right
    } else if angle < -STRAIGHT_TRESH {
        Segment::Left
    } else {
        Segment::Straight
    }
}

fn main() -> Result<()> {
    roblib_client::logger::init_log(None);
    let ip = std::env::args()
        .nth(1)
        .unwrap_or_else(|| "roland:1110".into());

    let quit = Arc::new(AtomicBool::new(false));
    let robot = Arc::new(Robot::new(Tcp::connect(ip)?));

    let rc: Arc<Robot<Tcp>> = robot.clone();
    let q = quit.clone();
    ctrlc::set_handler(move || {
        println!("Stopping");
        match rc.stop() {
            Err(e) => println!("NUH UUH\n{}", e),
            _ => (),
        }

        if q.load(Ordering::Relaxed) {
            println!("Shutting down");
            std::process::exit(0);
        }

        q.store(true, Ordering::Relaxed);
    })
    .expect("Error setting Ctrl-C handler");

    let dirs = scout_lap(&robot, quit)?;
    let segments = analyze_lap(dirs);
    dbg!(&segments);

    std::thread::sleep(Duration::from_secs(1));

    fast_lap(segments, &robot)?;

    // let segments = analyze_lap(TEST_RUN.to_vec());

    Ok(())
}

fn scout_lap(robot: &Robot<Tcp>, quit: Arc<AtomicBool>) -> Result<Vec<f32>> {
    let speed = 0.3;
    let mut normalized = 0f32;
    let mut dirs: Vec<f32> = Vec::new();
    let mut turn_rate = 0.0;

    while !quit.load(Ordering::Relaxed) {
        let track = robot.track_sensor()?;
        let n_sens = track.iter().map(|x| !*x as i32).sum::<i32>();

        // forced or end of track
        if n_sens == 4 && dirs.len() > 100 {
            robot.stop()?;
            break;
        }

        // on track
        if n_sens != 0 {
            normalized = track
                .iter()
                .enumerate()
                .map(|(i, x)| (!*x as i32) * WEIGHTS[i])
                .sum::<i32>() as f32
                / MAX_WEIGHT;
        }

        if let Some(d) = dirs.last() {
            // signs match
            if *d * normalized > 0. {
                turn_rate = lerp(turn_rate, 1., 0.1);
            } else {
                turn_rate = 0.
            }
        }

        dirs.push(normalized);

        // [-1, 1] => [0, 1]
        let n = (normalized + 1f32) / 2f32;
        let mut left = n * speed;
        let mut right = (1f32 - n) * speed;

        if normalized != 0. {
            let is_left = normalized < 0.;
            let applied_turn_rate = turn_rate.powf(2.);

            left = lerp(
                left,
                if is_left { -speed } else { speed },
                applied_turn_rate,
            );

            right = lerp(
                right,
                if is_left { speed } else { -speed },
                applied_turn_rate,
            );
        }

        println!("{:.3} {:.3}", left, right);
        robot.drive(left as f64, right as f64)?;
    }

    robot.stop()?;
    Ok(dirs)
}

fn analyze_lap(dirs: Vec<f32>) -> Vec<(Segment, i32)> {
    let mut segments: Vec<(Segment, i32)> = Vec::new();
    // suppose all dir value was added in an equal interval
    let mut scale = 1;

    dbg!(&dirs);

    for i in 0..dirs.len() {
        let avg = avg(&dirs, i);
        let segment = get_segment(avg);
        // dbg!(avg);
        // dbg!(&segment);

        if let Some((s, _)) = segments.last() {
            if segment != *s {
                segments.push((segment, scale));
                scale = 1;
            }
        } else {
            // add first one
            segments.push((segment, 1));
        }

        scale += 1;
    }

    return segments;
}

fn fast_lap(segments: Vec<(Segment, i32)>, robot: &Robot<Tcp>) -> Result<()> {
    let speed = 0.35;

    let mut normalized = 0f32;
    let mut dirs: Vec<f32> = Vec::new();
    let mut turn_rate = 0.0;
    let mut segment_idx = 0;
    let mut current_scale = 1;
    let mut current_segment: Segment;

    for i in 0.. {
        let track = robot.track_sensor()?;
        let n_sens = track.iter().map(|x| !*x as i32).sum::<i32>();

        // forced or end of track
        if n_sens == 4 && dirs.len() > 100 {
            robot.stop()?;
            break;
        }

        // on track
        if n_sens != 0 {
            normalized = track
                .iter()
                .enumerate()
                .map(|(i, x)| (!*x as i32) * WEIGHTS[i])
                .sum::<i32>() as f32
                / MAX_WEIGHT;
        }

        // get current position on track
        let avg = avg(&dirs, i);
        if !f32::is_nan(avg) {
            current_segment = get_segment(avg);
            dbg!(current_scale, current_segment);

            // let (tracked_segment, scale) = &segments[segment_idx];

            // println!(
            //     "on segment {:?} (tracked: {:?})  | {}/{} = | scale {}/{}",
            //     current_segment,
            //     tracked_segment,
            //     segment_idx,
            //     segments.len(),
            //     current_scale,
            //     scale
            // );

            // // move on to the next segment
            // if current_segment != *tracked_segment || (*scale - current_scale).abs() < 25 {
            //     segment_idx += 1;
            //     current_scale = 0;
            // }
        }

        if let Some(d) = dirs.last() {
            // signs match
            if *d * normalized > 0. {
                turn_rate = lerp(turn_rate, 1., 0.1);
            } else {
                turn_rate = 0.
            }
        }

        dirs.push(normalized);

        // [-1, 1] => [0, 1]
        let n = (normalized + 1f32) / 2f32;
        let mut left = n * speed;
        let mut right = (1f32 - n) * speed;

        if normalized != 0. {
            let is_left = normalized < 0.;
            let applied_turn_rate = turn_rate.powf(2.);

            left = lerp(
                left,
                if is_left { -speed } else { speed },
                applied_turn_rate,
            );

            right = lerp(
                right,
                if is_left { speed } else { -speed },
                applied_turn_rate,
            );
        }

        println!("{:.3} {:.3}", left, right);
        robot.drive(left as f64, right as f64)?;

        current_scale += 1;
    }

    robot.stop()?;
    Ok(())
}

fn lerp(a: f32, b: f32, t: f32) -> f32 {
    a + (b - a) * t
}

fn avg(dirs: &Vec<f32>, i: usize) -> f32 {
    let from: usize = ((i as i32) - AVG_WINDOW).max(0) as usize;
    // println!("{} to {} | n = {}", from, i, from.min(AVG_WINDOW as usize));
    &dirs[from..i].iter().sum::<f32>() / from.min(AVG_WINDOW as usize) as f32
}
