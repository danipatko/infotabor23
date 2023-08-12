using System;
using System.Linq;
using Microsoft.Kinect;
using System.Reactive.Linq;

namespace KinectRoland
{
    internal class Gestures
    {
        public enum Side
        {
            Left,
            Right 
        };

        // max distance between right hand and right shoulder
        private const float IdleHandOffset = 0.05f;

        public IObservable<bool> Tracked;
        public IObservable<bool> Jumped;
        public IObservable<double> LeftHandAngle;
        public IObservable<double> RightHandAngle;
        public IObservable<(float x, float y)> LeftHandPosition;
        public IObservable<(float x, float y)> RightHandPosition;

        public IObservable<(bool tracked, bool jumped, double lha, double rha, (float x, float y) lhp, (float x, float y) rhp)> All;


        private KinectSensor Sensor;

        public Gestures(KinectSensor sensor)
        {
            Sensor = sensor;
            Update();
        }

        private void Update()
        {
            var skeletonFrames = Observable.FromEventPattern<SkeletonFrameReadyEventArgs>
                (e => Sensor.SkeletonFrameReady += e, e => Sensor.SkeletonFrameReady -= e);

            var skeletons = skeletonFrames.Select(sf =>
            {
                using (var frame = sf.EventArgs.OpenSkeletonFrame())
                {
                    if (frame == null) return new Skeleton[0];
                    var sd = new Skeleton[frame.SkeletonArrayLength];
                    frame.CopySkeletonDataTo(sd);
                    return sd;
                }
            });

            Tracked = (from sd in skeletons
                      let tracked = sd.FirstOrDefault(s => s.TrackingState == SkeletonTrackingState.Tracked)
                      select tracked != null).DistinctUntilChanged();

            var joints = from sd in skeletons
                         let tracked = sd.FirstOrDefault(s => s.TrackingState == SkeletonTrackingState.Tracked)
                         where tracked != null
                         select tracked.Joints;

            Jumped = DetectJump(joints);
            LeftHandAngle = GetHandAngle(joints, Side.Left);
            RightHandAngle = GetHandAngle(joints, Side.Right);
            LeftHandPosition = GetHandElbowRelative(joints, Side.Left);
            RightHandPosition = GetHandElbowRelative(joints, Side.Right);

            // combine all
            All = Observable.CombineLatest(Tracked, Jumped, LeftHandAngle, RightHandAngle, LeftHandPosition, RightHandPosition,
                (bool tracked, bool jumped, double lha, double rha, (float x, float y) lhp, (float x, float y) rhp) => {
                    return (tracked, jumped, lha, rha, lhp, rhp);
                });
        }

        private IObservable<bool> DetectJump(IObservable<JointCollection> joints)
        {
            var feetpos = (from joint in joints
                        select (joint[JointType.FootLeft].Position.Y + joint[JointType.FootRight].Position.Y) / 2.0)
                            .Buffer(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(200));
            
            return (from p in feetpos
                    where p.Count() > 2
                    let lowThreshold = p.Max() - 0.2
                    select p.First() < lowThreshold && p.Last() < lowThreshold).DistinctUntilChanged();
        }

        private IObservable<(float x, float y)> GetHandElbowRelative(IObservable<JointCollection> joints, Side side = Side.Right) =>
            from joint in joints
            let hand = joint[side == Side.Right ? JointType.HandRight : JointType.HandLeft]
            let elbow = joint[side == Side.Right ? JointType.ElbowRight : JointType.ElbowLeft]
            select (hand.Position.X - elbow.Position.X, hand.Position.Y - elbow.Position.Y);

        private IObservable<double> GetHandAngle(IObservable<JointCollection> joints, Side side = Side.Right) =>
            from joint in joints
            let hand = joint[side == Side.Right ? JointType.HandRight : JointType.HandLeft]
            let shoulder = joint[side == Side.Right ? JointType.ShoulderRight : JointType.ShoulderLeft]
            // ignore hand on other side
            select (side == Side.Right && hand.Position.X < shoulder.Position.X) || (side == Side.Left && hand.Position.X > shoulder.Position.X)
                ? double.NaN
                : Math.Atan((hand.Position.Y - shoulder.Position.Y) / Math.Abs(shoulder.Position.X - hand.Position.X)) * 180 / Math.PI;
            
        private float Distance(Joint a, Joint b)
        {
            return Math.Abs(a.Position.X - b.Position.X) + Math.Abs(a.Position.Y - b.Position.Y);
        }

    }
}
