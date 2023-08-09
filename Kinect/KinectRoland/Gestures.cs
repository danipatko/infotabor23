using System;
using Microsoft.Kinect;

namespace KinectRoland
{
    internal class Gestures
    {
        public enum Side
        {
            Left,
            Right 
        };

        // OFFSETS
        // max distance between right hand and right shoulder
        private const float IdleHandOffset = 0.05f;
        // the depth where the body is centered
        private const float BodyCenter = .6f;
        // one step distance forward
        private const float BodyOffsetY = .1f;
        // one step distance sideways
        private const float BodyOffsetX = .2f;

        public bool Tracked = false;

        Skeleton skeleton;
        
        public void OnFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            skeleton = new Skeleton();
            foreach (Skeleton s in skeletons)
            {
                if (s.TrackingState == SkeletonTrackingState.Tracked)
                {
                    skeleton = s;
                    break;
                }
            }

            if(skeleton.TrackingState != SkeletonTrackingState.Tracked)
            {
                if(Tracked)
                {
                    Moved(new MovementEventArgs
                    {
                        LeftHandAngle = double.NaN,
                        RightHandAngle = double.NaN,
                        LeftHandPosition = (0, 0),
                        RightHandPosition = (0, 0),
                        Tracked = false
                    });
                }

                Tracked = false;
                return;
            }

            Tracked = true;

            double leftHandAngle = GetHandAngle(Side.Left),
                rightHandAngle = GetHandAngle(Side.Right);

            (float x, float y) leftHandPos = GetHandElbowRelative(Side.Left),
                rightHandPos = GetHandElbowRelative(Side.Right);

            Moved(new MovementEventArgs {  
                LeftHandAngle = leftHandAngle, 
                RightHandAngle = rightHandAngle,
                LeftHandPosition = leftHandPos,
                RightHandPosition = rightHandPos,
                Tracked = true
            });
        }
        
        private (float x, float y) GetHandElbowRelative(Side side = Side.Right)
        {
            Joint hand = skeleton.Joints[side == Side.Right ? JointType.HandRight : JointType.HandLeft], 
                elbow = skeleton.Joints[side == Side.Right ? JointType.ElbowRight : JointType.ElbowLeft];

            if (Distance(hand, elbow) < IdleHandOffset)
            {
                return (0, 0);
            }

            return (hand.Position.X - elbow.Position.X, hand.Position.Y - elbow.Position.Y);
        }

        private double GetHandAngle(Side side = Side.Right)
        {
            Joint hand = skeleton.Joints[side == Side.Right ? JointType.HandRight : JointType.HandLeft],
                shoulder = skeleton.Joints[side == Side.Right ? JointType.ShoulderRight : JointType.ShoulderLeft];

            // ignore on wrong side
            if((side == Side.Right && hand.Position.X < shoulder.Position.X)
                || (side == Side.Left && hand.Position.X > shoulder.Position.X))
            {
                    return double.NaN;
            }

            return Math.Atan((hand.Position.Y - shoulder.Position.Y) / Math.Abs(shoulder.Position.X - hand.Position.X)) * 180 / Math.PI;
        }

        private (float x, float y) BodyPosition()
        {
            float depth = -(skeleton.Position.Z / 3f - BodyCenter);
            (float x, float y) pos = (skeleton.Position.X, depth);

            // snap body to (0, 0) if within offset
            if (-BodyOffsetX < skeleton.Position.X && skeleton.Position.X < BodyOffsetX) 
                pos = (0, pos.y); // x to 0
            if (-BodyOffsetY < depth && depth < BodyOffsetY) 
                pos = (pos.x, 0); // y to 0

            return pos;
        }

        private float Distance(Joint a, Joint b)
        {
            return Math.Abs(a.Position.X - b.Position.X) + Math.Abs(a.Position.Y - b.Position.Y);
        }

        // event stuff
        public class MovementEventArgs : EventArgs
        {
            public bool Tracked { get; set; }
            public double LeftHandAngle { get; set; }
            public double RightHandAngle { get; set; }
            public (float x, float y) LeftHandPosition { get; set; }
            public (float x, float y) RightHandPosition { get; set; }
        }

        public event EventHandler<MovementEventArgs> OnMoved;
        protected virtual void Moved(MovementEventArgs e)
        {
            OnMoved?.Invoke(this, e);
        }
    }
}
