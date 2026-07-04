using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TimelineAnimator.Data
{
    public class AnimationClip
    {
        public string Name { get; set; } = "New Animation";
        public int StartFrame { get; set; } = 0;
        public int EndFrame { get; set; } = 100;
        public float FrameRate { get; set; } = 30f;
        
        public AnimationPose BasePose { get; set; } = new(); //for saving/loading and always having something to animate from
        public List<AnimationObject> Objects { get; } = new();
    }

    public enum ObjectType { Bone, Camera, Folder }

    public class AnimationObject
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        
        public string DisplayName => BoneNameHelpers.GetDisplayName(Name);
        
        public Guid? ParentId { get; set; }
        public ObjectType Type { get; set; }
        
        public bool IsExpanded { get; set; } = true;
        public bool IsPropertiesExpanded { get; set; } = false;
        public int Depth { get; set; } = 0;

        public List<PropertyTrack> Tracks { get; } = new();

        public PropertyTrack? GetTrack(PropertyType property)
            => Tracks.FirstOrDefault(x => x.Property == property);
            
        public PropertyTrack GetOrAddTrack(PropertyType property)
        {
            var track = GetTrack(property);
            if (track == null)
            {
                track = new PropertyTrack { Property = property };
                Tracks.Add(track);
            }
            return track;
        }
    }

    public enum PropertyType
    {
        PositionX, PositionY, PositionZ,
        RotationX, RotationY, RotationZ, RotationW,
        ScaleX, ScaleY, ScaleZ,
        CameraFov
    }

    public class PropertyTrack
    {
        public Guid Id { get; } = Guid.NewGuid();
        public PropertyType Property { get; init; }
        public AnimationCurve Curve { get; } = new();

        public bool Muted { get; set; }
        public bool Locked { get; set; }
        public bool Visible { get; set; } = true;
    }

    public class AnimationCurve
    {
        public List<CurveKeyframe> Keys { get; } = new();

        public void Sort()
        {
            Keys.Sort((a, b) => a.Frame.CompareTo(b.Frame));
        }

        public CurveKeyframe? GetKey(int frame)
            => Keys.FirstOrDefault(k => k.Frame == frame);

        public CurveKeyframe AddKey(int frame, float value)
        {
            var key = GetKey(frame);
            if (key != null)
            {
                key.Value = value;
                return key;
            }

            var newKey = new CurveKeyframe { Frame = frame, Value = value };
            Keys.Add(newKey);
            Sort();
            return newKey;
        }
    }

    public enum InterpolationMode { Constant, Linear, Bezier }

    public struct Tangent
    {
        public Vector2 In;
        public Vector2 Out;
    }

    public class CurveKeyframe
    {
        public Guid Id { get; } = Guid.NewGuid();
        public int Frame { get; set; }
        public float Value { get; set; }

        public Tangent Tangents { get; set; } = new Tangent 
        { 
            In = new Vector2(-1f, 0f), 
            Out = new Vector2(1f, 0f) 
        };

        public InterpolationMode Interpolation { get; set; } = InterpolationMode.Bezier;
        
        public KeyframeShape Shape { get; set; } = KeyframeShape.Diamond;
        public uint? CustomColor { get; set; }
    }

    // eval
    public class AnimationPose //TOdo construct in sequencer and apply in playback
    {
        public TransformPose? SceneOrigin; //here in case i need to do smth for scene migration
        public Dictionary<string, TransformPose> BonePoses { get; } = new();
        public CameraPose? Camera { get; set; }
    }

    public struct TransformPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public static TransformPose Identity => new()
        {
            Position = Vector3.Zero,
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        };

        public Matrix4x4 ToMatrix()
        {
            return Matrix4x4.CreateScale(Scale)
                 * Matrix4x4.CreateFromQuaternion(Rotation)
                 * Matrix4x4.CreateTranslation(Position);
        }
    }

    public struct CameraPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float FieldOfView;
        public bool RelativeToPlayer;
    }
    
    public enum KeyframeShape
    {
        Diamond,
        Circle,
        Square
    }

}