using System.Collections.Generic;
using System.Numerics;

namespace TimelineAnimator.Data
{
    public class AnimationFile
    {
        public int StartFrame { get; set; }
        public int EndFrame { get; set; }
        public string AnimationType { get; set; } = "Actor";
        public Dictionary<string, TransformState> BaseState { get; set; } = new();
        public List<AnimationTrack> Tracks { get; set; } = new();
    }

    public class AnimationTrack
    {
        public string TrackName { get; set; } = string.Empty;
        public TrackType Type { get; set; }
        public string ParentName { get; set; } = string.Empty;
        public List<SerializedKeyframe> Keyframes { get; set; } = new();
    }

    public class SerializedKeyframe
    {
        public int Frame { get; set; }
        public KeyframeShape Shape { get; set; }
        public uint? CustomColor { get; set; }
        public Vector2 P1 { get; set; }
        public Vector2 P2 { get; set; }
        
        public Vector3 VectorValue { get; set; }
        public Quaternion QuatValue { get; set; }
        public float FloatValue { get; set; }
    }
}