using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TimelineAnimator.Data;
using TimelineAnimator.ImSequencer;
using TimelineAnimator.Sequencers;

namespace TimelineAnimator.Core
{
    public class ProjectService
    {
        public List<ISequencer> Sequencers { get; private set; } = new();

        public int GetGlobalMinFrame() => Sequencers.Count == 0 ? 0 : Sequencers.Min(s => s.Sequence.FrameMin);
        public int GetGlobalMaxFrame() => Sequencers.Count == 0 ? 100 : Sequencers.Max(s => s.Sequence.FrameMax);

        public void SetMaxFrameForAll(int max)
        {
            foreach (var s in Sequencers) s.Sequence.FrameMax = max;
        }

        public void ClearProject()
        {
            Sequencers.Clear();
        }

        public CameraSequencer AddCameraSequencer()
        {
            var existing = Sequencers.OfType<CameraSequencer>().FirstOrDefault();
            if (existing != null)
            {
                existing.AddKeyframeAt(Services.PlaybackService.CurrentFrame);
                return existing;
            }

            var camSequencer = new CameraSequencer();
            camSequencer.Sequence.FrameMax = GetGlobalMaxFrame();

            Sequencers.Add(camSequencer);
            return camSequencer;
        }

        public void SaveAnimation(string path, ISequencer sequencerToSave)
        {
            try
            {
                var animationFile = GetAnimationData(sequencerToSave);
                if (animationFile == null) return;

                AnimationSerializer.Save(path, animationFile);
                Services.Log.Information($"Animation saved to {path}");
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Could not save animation");
            }
        }

        public void LoadAnimation(string path, ISequencer targetSequencer)
        {
            try
            {
                var animationFile = AnimationSerializer.Load(path);
                {
                    bool isCameraFile = animationFile.AnimationType == "Camera";
                    bool isCameraSequencer = targetSequencer is CameraSequencer;

                    if (isCameraFile != isCameraSequencer)
                    {
                        Services.Log.Error($"Invalid File Type: Cannot load {animationFile.AnimationType} animation into {targetSequencer.Name}.");
                        return;
                    }

                    ApplyAnimationData(animationFile, targetSequencer);
                    Services.Log.Information($"Animation loaded from {path}");
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Could not load animation");
            }
        }

        private AnimationFile? GetAnimationData(ISequencer activeSequencer)
        {
            var animationFile = new AnimationFile
            {
                StartFrame = activeSequencer.Sequence.FrameMin,
                EndFrame = activeSequencer.Sequence.FrameMax,
                AnimationType = activeSequencer is CameraSequencer ? "Camera" : "Actor"
            };

            foreach (var (TrackName, transform) in activeSequencer.DefaultPose)
                animationFile.BaseState[TrackName] = transform;

            foreach (var abstractTrack in activeSequencer.Sequence.Tracks)
            {
                var animationTrack = new AnimationTrack 
                { 
                    TrackName = abstractTrack.Name,
                    Type = abstractTrack.Type,
                    ParentName = abstractTrack.ParentName ?? string.Empty
                };

                foreach (var kf in abstractTrack.GetUntypedKeyframes())
                {
                    var skf = new SerializedKeyframe
                    {
                        Frame = kf.Frame, Shape = kf.Shape, CustomColor = kf.CustomColor, P1 = kf.P1, P2 = kf.P2
                    };

                    if (abstractTrack is TimelineTrack<Vector3> v3t) skf.VectorValue = ((TrackKeyframe<Vector3>)kf).Value;
                    else if (abstractTrack is TimelineTrack<Quaternion> qt) skf.QuatValue = ((TrackKeyframe<Quaternion>)kf).Value;
                    else if (abstractTrack is TimelineTrack<float> ft) skf.FloatValue = ((TrackKeyframe<float>)kf).Value;

                    animationTrack.Keyframes.Add(skf);
                }
                animationFile.Tracks.Add(animationTrack);
            }

            return animationFile;
        }

        private void ApplyAnimationData(AnimationFile animationFile, ISequencer activeSequencer)
        {
            activeSequencer.DefaultPose = new Dictionary<string, TransformState>(animationFile.BaseState);
            activeSequencer.Sequence.Tracks.Clear();
            activeSequencer.Sequence.FrameMax = animationFile.EndFrame;

            foreach (var trackData in animationFile.Tracks)
            {
                TimelineTrack newTrack = null;

                if (trackData.Type == TrackType.Folder)
                {
                    newTrack = new FolderTrack(trackData.TrackName) { ParentName = trackData.ParentName };
                    activeSequencer.Sequence.Tracks.Add(newTrack);
                }
                else if (trackData.Type == TrackType.Vector3)
                {
                    var t = activeSequencer.Sequence.AddTrack<Vector3>(trackData.TrackName, TrackType.Vector3);
                    t.ParentName = trackData.ParentName;
                    foreach (var kf in trackData.Keyframes)
                    {
                        var newKf = t.AddKeyframe(kf.Frame, kf.VectorValue);
                        newKf.P1 = kf.P1; newKf.P2 = kf.P2; newKf.Shape = kf.Shape; newKf.CustomColor = kf.CustomColor;
                    }
                    newTrack = t;
                }
                else if (trackData.Type == TrackType.Quaternion)
                {
                    var t = activeSequencer.Sequence.AddTrack<Quaternion>(trackData.TrackName, TrackType.Quaternion);
                    t.ParentName = trackData.ParentName;
                    foreach (var kf in trackData.Keyframes)
                    {
                        var newKf = t.AddKeyframe(kf.Frame, kf.QuatValue);
                        newKf.P1 = kf.P1; newKf.P2 = kf.P2; newKf.Shape = kf.Shape; newKf.CustomColor = kf.CustomColor;
                    }
                    newTrack = t;
                }
                else if (trackData.Type == TrackType.Float)
                {
                    var t = activeSequencer.Sequence.AddTrack<float>(trackData.TrackName, TrackType.Float);
                    t.ParentName = trackData.ParentName;
                    foreach (var kf in trackData.Keyframes)
                    {
                        var newKf = t.AddKeyframe(kf.Frame, kf.FloatValue);
                        newKf.P1 = kf.P1; newKf.P2 = kf.P2; newKf.Shape = kf.Shape; newKf.CustomColor = kf.CustomColor;
                    }
                    newTrack = t;
                }

                if (newTrack != null)
                {
                    if (trackData.TrackName.EndsWith("_Position")) newTrack.DisplayName = "Position";
                    else if (trackData.TrackName.EndsWith("_Rotation")) newTrack.DisplayName = "Rotation";
                    else if (trackData.TrackName.EndsWith("_Scale")) newTrack.DisplayName = "Scale";
                    else if (trackData.TrackName.EndsWith("_FOV")) newTrack.DisplayName = "Field of View";
                }
            }

            activeSequencer.RebuildHierarchy();
            Services.PlaybackService.Stop();
        }
    }
}