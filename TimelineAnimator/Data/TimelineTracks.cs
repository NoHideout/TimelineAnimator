using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TimelineAnimator.Data
{
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
            float dot = Quaternion.Dot(a.Rotation, b.Rotation);
            var bRot = dot < 0.0f ? new Quaternion(-b.Rotation.X, -b.Rotation.Y, -b.Rotation.Z, -b.Rotation.W) : b.Rotation;

            return new TransformState
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Rotation = Quaternion.Slerp(a.Rotation, bRot, t),
                Scale = Vector3.Lerp(a.Scale, b.Scale, t),
                FieldOfView = a.FieldOfView + (b.FieldOfView - a.FieldOfView) * t
            };
        }
    }

    public enum TrackType { Transform, Float, Vector3, Quaternion, Folder}

    public interface ITrackKeyframe
    {
        Guid Id { get; }
        int Frame { get; set; }
        Vector2 P1 { get; set; }
        Vector2 P2 { get; set; }
        KeyframeShape Shape { get; set; }
        uint? CustomColor { get; set; }
    }

    public class TrackKeyframe<T> : ITrackKeyframe
    {
        public Guid Id { get; } = Guid.NewGuid();
        public int Frame { get; set; }
        public Vector2 P1 { get; set; }
        public Vector2 P2 { get; set; }
        public KeyframeShape Shape { get; set; }
        public uint? CustomColor { get; set; }
    
        public T Value { get; set; }

        public TrackKeyframe(int frame, T value)
        {
            Frame = frame;
            Value = value;
        }
    }

    public abstract class TimelineTrack
    {
        public string Name { get; private set; }
        public string DisplayName { get; set; }
        public string ParentName { get; set; } = String.Empty;
        public int Depth { get; set; } = 0;
        public bool IsExpanded { get; set; } = true;
        public bool HasChildren { get; set; } = false;
    
        public TrackType Type  { get; protected set; }

        protected TimelineTrack(string name, TrackType type)
        {
            Name = name;
            Type = type;
            var display = BoneNameHelpers.GetDisplayName(name); // use helpers to find translation for bone
            DisplayName = string.IsNullOrEmpty(display) ? name : display;
        }

        public abstract void DeleteKeyframe(Guid KeyframeId);
        public abstract IEnumerable<ITrackKeyframe> GetUntypedKeyframes();

        public abstract void PasteKeyframe(int newFrame, ITrackKeyframe sourceKeyframe);
        public IReadOnlyList<ITrackKeyframe> UntypedKeyframes => GetUntypedKeyframes().ToList();
    }

    public class TimelineTrack<T> : TimelineTrack
    {
        public List<TrackKeyframe<T>> Keyframes { get; set; } = new();
    
        public TimelineTrack(string name, TrackType type) : base(name, type) { }

        public TrackKeyframe<T> AddKeyframe(int frame, T value)
        {
            var newKeyframe = new TrackKeyframe<T>(frame, value);
            Keyframes.Add(newKeyframe);
            Keyframes = Keyframes.OrderBy(k => k.Frame).ToList();
            return newKeyframe;
        }

        public override void DeleteKeyframe(Guid keyframeId) => Keyframes.RemoveAll(k => k.Id == keyframeId);
        public override IEnumerable<ITrackKeyframe> GetUntypedKeyframes() => Keyframes.Cast<ITrackKeyframe>();
        public override void PasteKeyframe(int newFrame, ITrackKeyframe sourceKeyframe)
        {
            if (sourceKeyframe is TrackKeyframe<T> typedSource)
            {
                var newKf = new TrackKeyframe<T>(newFrame, typedSource.Value)
                {
                    P1 = typedSource.P1, P2 = typedSource.P2,
                    Shape = typedSource.Shape, CustomColor = typedSource.CustomColor
                };
                Keyframes.RemoveAll(k => k.Frame == newFrame);
                Keyframes.Add(newKf);
                Keyframes = Keyframes.OrderBy(k => k.Frame).ToList();
            }
        }
    }

    public class FolderTrack : TimelineTrack
    {
        public FolderTrack(string name) : base(name, TrackType.Folder) { }

        public override void DeleteKeyframe(Guid keyframeId) { }
        public override IEnumerable<ITrackKeyframe> GetUntypedKeyframes() => Array.Empty<ITrackKeyframe>();
        public override void PasteKeyframe(int newFrame, ITrackKeyframe sourceKeyframe) { }
    }
    
    public class TimelineSequence
    {
        public List<TimelineTrack> Tracks { get; set; } = new();
        public int FrameMin { get; set; } = 0;
        public int FrameMax { get; set; } = 100;

        public TimelineTrack<T> AddTrack<T>(string trackName, TrackType type)
        {
            var track = new TimelineTrack<T>(trackName, type);
            Tracks.Add(track);
            return track;
        }
    
        public void RemoveTrack(int index) => Tracks.RemoveAt(index);
        public TimelineTrack? GetTrack(int index) => index >= 0 && index < Tracks.Count ? Tracks[index] : null;
        public TimelineTrack? GetTrackByName(string name) => Tracks.FirstOrDefault(t => t.Name == name);
    }
}
