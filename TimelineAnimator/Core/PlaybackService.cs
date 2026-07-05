using System;
using System.IO;

namespace TimelineAnimator.Core
{
    public class PlaybackService
    {
        public int CurrentFrame { get; set; } = 0;
        public bool IsPlaying { get; private set; } = false;
        private float recordingDelay = 0.5f;
        private float delayTimer = 0f;

        private bool wasInGPose = false;
        private int lastAppliedFrame = -1;
        private double timeAccumulator = 0.0;

        public bool IsRecording { get; private set; } = false;

        public void TogglePlay()
        {
            IsPlaying = !IsPlaying;
            if (!IsPlaying)
            {
                timeAccumulator = 0.0;
                delayTimer = 0f;
                if (IsRecording)
                {
                    Services.EorzeaCamcorderIpc.StopRecording();
                    IsRecording = false;
                }
            }
        }

        public void StartRecording()
        {
            if (!Services.EorzeaCamcorderIpc.IsAvailable) return;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"TimelineRec_{timestamp}.mp4";
            string path = Path.Combine(Services.PluginInterface.GetPluginConfigDirectory(), fileName);
            Services.Log.Debug($"PlaybackService: Starting recording to {path}");

            IsPlaying = false;
            CurrentFrame = Services.ProjectService.GetGlobalMinFrame();
            timeAccumulator = 0.0;
            lastAppliedFrame = -1;
            ApplyCurrentPose();

            IsRecording = true;
            delayTimer = recordingDelay;
            
            Services.EorzeaCamcorderIpc.StartRecording(path);
        }

        public void Stop()
        {
            IsPlaying = false;
            delayTimer = 0f;
            
            if (IsRecording)
            {
                Services.EorzeaCamcorderIpc.StopRecording();
                IsRecording = false;
            }

            CurrentFrame = Services.ProjectService.GetGlobalMinFrame();
            timeAccumulator = 0.0;
            lastAppliedFrame = -1;
            ApplyCurrentPose();
        }

        public void ApplyCurrentPose()
        {
            if (lastAppliedFrame == CurrentFrame) return;

            var sequencers = Services.ProjectService.Sequencers;
            if (sequencers.Count == 0) return;

            foreach (var sequencer in sequencers)
            {
                sequencer.ApplyPose(CurrentFrame);
            }

            lastAppliedFrame = CurrentFrame;
        }

        public void Update(float deltaSeconds)
        {
            bool inGPose = Services.ClientState.IsGPosing;

            if (wasInGPose && !inGPose)
            {
                Services.CameraService.IsOverridden = false;
                IsPlaying = false;
                IsRecording = false;
                CurrentFrame = 0;
                timeAccumulator = 0.0;
                lastAppliedFrame = -1;
                Services.ProjectService.ClearProject();

                wasInGPose = inGPose;
                return;
            }

            if (!inGPose)
            {
                wasInGPose = false;
                if (IsPlaying) Stop();
                return;
            }

            wasInGPose = true;
            if (IsRecording && !IsPlaying)
            {
                delayTimer -= deltaSeconds;
                if (delayTimer <= 0)
                {
                    IsPlaying = true;
                }
                return;
            }
            
            if (!IsPlaying || Services.Configuration.PlaybackFramesPerSecond <= 0)
            {
                timeAccumulator = 0.0;
                return;
            }

            var sequencers = Services.ProjectService.Sequencers;
            if (sequencers.Count == 0) return;

            timeAccumulator += deltaSeconds;

            double frameDuration = 1.0 / Services.Configuration.PlaybackFramesPerSecond;
            if (frameDuration <= 0)
            {
                timeAccumulator = 0.0;
                return;
            }

            int maxFrame = Services.ProjectService.GetGlobalMaxFrame();
            int minFrame = Services.ProjectService.GetGlobalMinFrame();
            bool frameAdvanced = false;

            while (timeAccumulator >= frameDuration)
            {
                CurrentFrame++;
                timeAccumulator -= frameDuration;

                if (CurrentFrame > maxFrame)
                {
                    CurrentFrame = minFrame;

                    if (IsRecording)
                    {
                        Stop();
                        return;
                    }
                }

                frameAdvanced = true;
            }

            if (frameAdvanced) ApplyCurrentPose();
        }
    }
}