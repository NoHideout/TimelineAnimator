
namespace TimelineAnimator.Core
{
    public class PlaybackService
    {
        public int CurrentFrame { get; set; } = 0;
        public bool IsPlaying { get; private set; } = false;

        private bool wasInGPose = false;
        private int lastAppliedFrame = -1;
        private double timeAccumulator = 0.0;

        public void TogglePlay()
        {
            IsPlaying = !IsPlaying;
            if (!IsPlaying) timeAccumulator = 0.0;
        }

        public void Stop()
        {
            IsPlaying = false;
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
                if (CurrentFrame > maxFrame) CurrentFrame = minFrame;

                frameAdvanced = true;
            }

            if (frameAdvanced) ApplyCurrentPose();
        }
    }
}