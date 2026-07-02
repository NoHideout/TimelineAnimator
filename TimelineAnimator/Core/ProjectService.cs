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

        public int GetGlobalMinFrame() => Sequencers.Count == 0 ? 0 : Sequencers.Min(s => s.Clip.StartFrame);
        public int GetGlobalMaxFrame() => Sequencers.Count == 0 ? 100 : Sequencers.Max(s => s.Clip.EndFrame);

        public void SetMaxFrameForAll(int max)
        {
            foreach (var s in Sequencers) s.Clip.EndFrame = max;
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
            camSequencer.Clip.EndFrame = GetGlobalMaxFrame();
            
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
            return new AnimationFile
            {
                AnimationType = activeSequencer is CameraSequencer ? "Camera" : "Actor",
                Clip = activeSequencer.Clip
            };
        }

        private void ApplyAnimationData(AnimationFile animationFile, ISequencer activeSequencer)
        {
            activeSequencer.Clip.Name = animationFile.Clip.Name;
            activeSequencer.Clip.StartFrame = animationFile.Clip.StartFrame;
            activeSequencer.Clip.EndFrame = animationFile.Clip.EndFrame;
            activeSequencer.Clip.FrameRate = animationFile.Clip.FrameRate;
    
            activeSequencer.Clip.BasePose = animationFile.Clip.BasePose;

            activeSequencer.Clip.Objects.Clear();
            activeSequencer.Clip.Objects.AddRange(animationFile.Clip.Objects);
            activeSequencer.RebuildHierarchy();

            Services.PlaybackService.Stop();
        }
    }
}