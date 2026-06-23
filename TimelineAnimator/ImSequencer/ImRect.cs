using System.Numerics;

namespace TimelineAnimator.ImSequencer
{
    public class ImRect
    {
        public Vector2 Min { get; }
        public Vector2 Max { get; }

        public ImRect(Vector2 a, Vector2 b)
        {
            Min = a;
            Max = b;
        }

        public bool Contains(Vector2 p) =>
            p.X >= Min.X && p.Y >= Min.Y && p.X < Max.X && p.Y < Max.Y;

        public bool Overlaps(ImRect r) =>
            r.Min.X < Max.X && r.Max.X > Min.X && r.Min.Y < Max.Y && r.Max.Y > Min.Y;
    }
}