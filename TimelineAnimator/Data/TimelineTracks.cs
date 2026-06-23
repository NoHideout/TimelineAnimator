using System.Numerics;

namespace TimelineAnimator.Data;

public enum KeyframeShape
{
    Diamond,
    Circle,
    Square
}

public struct TransformState
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
    public float FieldOfView;

    public static TransformState Identity => new()
    {
        Position = Vector3.Zero,
        Rotation = Quaternion.Identity,
        Scale = Vector3.One,
        FieldOfView = 0.785398f
    };

    public static TransformState Lerp(TransformState a, TransformState b, float t)
    {
        return new TransformState
        {
            Position = Vector3.Lerp(a.Position, b.Position, t),
            Rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t),
            Scale = Vector3.Lerp(a.Scale, b.Scale, t),
            FieldOfView = a.FieldOfView + (b.FieldOfView - a.FieldOfView) * t
        };
    }
}

public class TrackKeyframe
{
    public Guid Id { get; } = Guid.NewGuid();
    public int Frame { get; set; }

    public TransformState Transform { get; set; }

    public Vector2 P1 { get; set; } = new(0.25f, 0.25f);
    public Vector2 P2 { get; set; } = new(0.75f, 0.75f);
    public KeyframeShape Shape { get; set; } = KeyframeShape.Diamond;
    public uint? CustomColor { get; set; }

    public TrackKeyframe(int frame, TransformState? state = null)
    {
        Frame = frame;
        Transform = state ?? TransformState.Identity;
    }
}

public class TimelineTrack
{
    public string Name { get; private set; }
    public string DisplayName { get; private set; }
    public string ParentName { get; set; } = string.Empty;
    public int Depth { get; set; } = 0;

    public bool IsExpanded { get; set; } = true;
    public bool HasChildren { get; set; } = false;
    public List<TrackKeyframe> Keyframes { get; set; } = new();

    public TimelineTrack(string name)
    {
        Name = name;
        var display = BoneNameHelpers.GetDisplayName(name);
        DisplayName = string.IsNullOrEmpty(display) ? name : display;
    }

    public TrackKeyframe AddKeyframe(int frame, TransformState? state = null)
    {
        var newKeyframe = new TrackKeyframe(frame, state);
        Keyframes.Add(newKeyframe);
        Keyframes = Keyframes.OrderBy(k => k.Frame).ToList();
        return newKeyframe;
    }

    public void DeleteKeyframe(Guid keyframeId)
    {
        Keyframes.RemoveAll(k => k.Id == keyframeId);
    }
}

public class TimelineSequence
{
    public List<TimelineTrack> Tracks { get; set; } = new();
    public int FrameMin { get; set; } = 0;
    public int FrameMax { get; set; } = 100;

    public void AddTrack(string trackName) => Tracks.Add(new TimelineTrack(trackName));
    public void RemoveTrack(int index) => Tracks.RemoveAt(index);
    public TimelineTrack? GetTrack(int index) => index >= 0 && index < Tracks.Count ? Tracks[index] : null;
    public TimelineTrack? GetTrackByName(string name) => Tracks.FirstOrDefault(t => t.Name == name);
}