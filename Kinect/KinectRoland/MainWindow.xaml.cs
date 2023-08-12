using System;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Kinect;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Threading.Tasks.Dataflow;

namespace KinectRoland
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ActionBlock<string> workerBlock;

        public MainWindow()
        {
            InitializeComponent();
            workerBlock = new ActionBlock<string>(async msg =>
            {
                if (socket != null && socket.State == WebSocketState.Open)
                    await socket.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{msgid++} {msg}")), WebSocketMessageType.Text, true, default);
            });
        }

        private KinectSensor sensor;
        private Display display;
        private Gestures gestures;
        private ClientWebSocket socket;
        private int msgid = 1;
        private bool enabled = false;

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            sensor?.Stop();
            Stop();
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "quit", default);
        }

        private async void Stop()
        {
            workerBlock.Post("s");
            
            await Dispatcher.BeginInvoke(new Action(() => {
                PositionText.Text = "STOPPED (jump to start)";
            }));
        }

        private void Move(float left, float right) => workerBlock.Post($"m {left:0.000} {right:0.000}");
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

            gestures = new Gestures(sensor);
            gestures.All.Subscribe(async a =>
            {
                if (!enabled) return;
                if(double.IsNaN(a.lha))
                {
                    Stop();
                    return;
                }

                // normalize speed & snap to 0
                float speed = (float)a.lha / 90;
                speed = Math.Abs(speed) < 0.2 ? 0 : speed;

                // steering
                float handlength = 0.4F;
                float x = (a.rhp.x / handlength + 1) / 2;

                float left = x * speed;
                float right = (1 - x) * speed;

                Move(left, right);

                // calculate left/right
                await Dispatcher.BeginInvoke(new Action(() => {
                    PositionText.Text = $"x: {a.rhp.x} => {x}\nleft: {left:0.00}\nright: {right:0.00}\nsend: '{msgid++} m {left:0.000} {right:0.000}'";
                }));
            });

            gestures.Tracked.Subscribe(tracked => {
                if(!tracked) Stop();
            });

            gestures.Jumped.Subscribe(jumped => {
                if (jumped) enabled = !enabled;
                if (!enabled) Stop();
            });

            // start image stream
            display = new Display(sensor);
            DisplayImage.Source = display.ImageSource;
    
            sensor.SkeletonStream.Enable();
            sensor.SkeletonStream.EnableTrackingInNearRange = true;

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
    }
}
