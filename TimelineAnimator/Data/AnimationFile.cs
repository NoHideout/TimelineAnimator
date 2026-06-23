using System.Numerics;

namespace TimelineAnimator.Data;

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
    public List<AnimationKeyframe> Keyframes { get; set; } = new();
}

public class AnimationKeyframe
{
    public int Frame { get; set; }
    public TransformState Transform { get; set; }
    public KeyframeShape Shape { get; set; }
    public uint? CustomColor { get; set; }

    public Vector2 P1 { get; set; }
    public Vector2 P2 { get; set; }

    public AnimationKeyframe()
    {
    }

    public AnimationKeyframe(TrackKeyframe kf)
    {
        Frame = kf.Frame;
        Transform = kf.Transform;
        Shape = kf.Shape;
        CustomColor = kf.CustomColor;
        P1 = kf.P1;
        P2 = kf.P2;
    }

    public TrackKeyframe ToKeyframe()
    {
        return new TrackKeyframe(Frame, Transform)
        {
            Shape = Shape,
            CustomColor = CustomColor,
            P1 = P1,
            P2 = P2
        };
    }
}