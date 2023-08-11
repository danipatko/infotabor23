use ctrlc;
use roblib_client::{roblib::roland::*, transports::tcp::Tcp, Result, Robot};
use std::sync::Arc;

const WEIGHTS: [i32; 4] = [-3, -1, 1, 3];
const MAX_WEIGHT: f32 = 4f32; // sum of weights on one side

fn main() -> Result<()> {
    roblib_client::logger::init_log(None);
    let ip = std::env::args()
        .nth(1)
        .unwrap_or_else(|| "roland:1110".into());

    let robot = Arc::new(Robot::new(Tcp::connect(ip)?));
    let speed = 0.35;

    let mut dirs: Vec<f32> = Vec::new();

    let rc = robot.clone();
    ctrlc::set_handler(move || {
        println!("Stopping");
        match rc.stop() {
            Err(e) => println!("NUH UUH\n{}", e),
            _ => (),
        }
        std::process::exit(0);
    })
    .expect("Error setting Ctrl-C handler");

    // for t in tests {
    let mut normalized = 0f32;
    loop {
        let track = robot.track_sensor()?;

        if track.iter().map(|x| !*x as i32).sum::<i32>() != 0 {
            normalized = track
                .iter()
                .enumerate()
                .map(|(i, x)| (!*x as i32) * WEIGHTS[i])
                .sum::<i32>() as f32
                / MAX_WEIGHT;
        }

        // save
        dirs.push(normalized);

        // [-1, 1] => [0, 1]
        let n = (normalized + 1f32) / 2f32;
        let left = n * speed;
        let right = (1f32 - n) * speed;

        println!("{:.3} {:.3}", left, right);
        robot.drive(left as f64, right as f64)?;
    }

    // loop {
    //     std::thread::park();
    // }
}
