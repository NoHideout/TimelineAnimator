using TimelineAnimator.Data;
using TimelineAnimator.ImSequencer;
using TimelineAnimator.Sequencers;

namespace TimelineAnimator;

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
            if (animationFile != null)
            {
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

        foreach (var track in activeSequencer.Sequence.Tracks)
        {
            var animationTrack = new AnimationTrack { TrackName = track.Name };
            foreach (var keyframe in track.Keyframes)
                animationTrack.Keyframes.Add(new AnimationKeyframe(keyframe));

            animationFile.Tracks.Add(animationTrack);
        }

        return animationFile;
    }

    private void ApplyAnimationData(AnimationFile animationFile, ISequencer activeSequencer)
    {
        activeSequencer.DefaultPose = new Dictionary<string, TransformState>(animationFile.BaseState);
        
        activeSequencer.Sequence.Tracks.Clear();
        activeSequencer.Sequence.FrameMax = animationFile.EndFrame;

        foreach (var track in animationFile.Tracks)
        {
            activeSequencer.Sequence.AddTrack(track.TrackName);
            var animation = activeSequencer.Sequence.GetTrackByName(track.TrackName);
            if (animation == null) continue;

            foreach (var keyframe in track.Keyframes)
                animation.Keyframes.Add(keyframe.ToKeyframe());
        }

        activeSequencer.RebuildHierarchy();
        Services.PlaybackService.Stop();
    }
}