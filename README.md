# infotabor23

2023 stuff

## [Kinect](Kinect/)

Control with hand gestures using an Xbox 360 kinect sensor.

### Setting up

-   [Kinect for Windows SDK v1.8](https://www.microsoft.com/en-gb/download/details.aspx?id=40278)
-   [Kinect for Windows Developer Toolkit v1.8](https://www.microsoft.com/en-us/download/details.aspx?id=40276) (not required, **very** useful)
-   .NET framework 4.8

Add the driver to C# project

-   in Visual Studio
-   under the Solution menu right click **References** and **Add reference...**
-   navigate to the directory where the xbox driver is installed and select `Microsoft.Kinect.dll`
-   now `Microsoft.Kinect` can be imported

### Controls

Jump to enable/disable the tracking. Left hand operates throttle (raised left hand = more gas, lowered left hand is reverse); wave right hand left/right to steer.

## [Cube](cube/)

Follows any ArUco marker.

CLI arguments:

-   `--nows`: do not connect to the robot (display only)
-   `--host`: the websocket connection string of the robot
-   `--ipcam`: VideoCapture source (MJPEG stream connection string)

Fine tuning:

-   `SANITYCHECK`: number of frames after which the tracker is dropped
-   `MAX_SPEED`: [0; 1] speed limit for the robot (distance to target is interpolated between max and base speed - the closer the target is, the slower the robot goes)
-   `BASE_SPEED`: [0; 1] minimal speed to commute with

## [Human](human/)

Follows any person, using opencv's builtin [HOG](https://learnopencv.com/histogram-of-oriented-gradients) people detector.

CLI arguments & fine tuning: same as [Cube](#cube)

\+ `MIN_CONFIDENCE`: lower bound of detecting a human

## [Engsim](engsim/)

Sillygoofy engine "simulator" using gamepad input, made with svelte.
