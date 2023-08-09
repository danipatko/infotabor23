using System;
using System.IO;
using System.Windows;
using Microsoft.Kinect;
using System.Net.WebSockets;
using System.Threading;
using System.Text;

namespace KinectRoland
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private KinectSensor sensor;
        private Display display;
        private Gestures gestures;
        private ClientWebSocket socket;
        private int msgid = 1;

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sensor != null) sensor.Stop();
            if(socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{msgid++} s")), WebSocketMessageType.Text, false, default);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "quit", default);
            }
        }
        
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // try to connect ws
            socket = new ClientWebSocket();
            Uri url = new Uri("ws://roland:1111/ws");

            try
            {
                await socket.ConnectAsync(url, default);
            }
            catch (WebSocketException)
            {
                MessageBox.Show($"Failed to connect to '{url}'", "cope", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            // find a sensor
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    sensor = potentialSensor;
                    break;
                }
            }
            if (sensor == null) throw new Exception("Unable to find kinect sensor!");

            gestures = new Gestures();
            gestures.OnMoved += HandleMovement;

            // start image stream
            display = new Display(sensor);
            DisplayImage.Source = display.ImageSource;
    
            sensor.SkeletonStream.Enable();
            sensor.SkeletonStream.EnableTrackingInNearRange = true;

            sensor.SkeletonFrameReady += gestures.OnFrameReady;
            sensor.SkeletonFrameReady += display.OnFrameReady;

            try
            {
                sensor.Start();
            }
            catch (IOException)
            {
                sensor = null;
            }
        }

        private async void HandleMovement(object sender, Gestures.MovementEventArgs e)
        {
            // stop
            if(!e.Tracked || double.IsNaN(e.LeftHandAngle))
            { 
                await socket.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{msgid++} s")), WebSocketMessageType.Text, true, default);
                PositionText.Text = "STOPPED";
                return;
            }

            // normalize speed & snap to 0
            float speed = (float)e.LeftHandAngle / 90;
            speed = Math.Abs(speed) < 0.2 ? 0 : speed;

            // steering
            float handlength = 0.4F;
            float x = (e.RightHandPosition.x / handlength + 1) / 2;

            float left = x * speed;
            float right = (1 - x) * speed;

            // PositionText.Text = $"left: {(int)e.LeftHandAngle}\nright: {(int)e.RightHandAngle}\nposition\nleft:{ToString(e.LeftHandPosition)}\nright:{ToString(e.RightHandPosition)}";
            PositionText.Text = $"x: {e.RightHandPosition.x} => {x}\nleft: {left:0.00}\nright: {right:0.00}\nsend: '{msgid++} m {left:0.000} {right:0.000}'";

            var message = new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{msgid++} m {left:0.000} {right:0.000}"));
            await socket.SendAsync(message, WebSocketMessageType.Text, true, default);
        }
    }
}
