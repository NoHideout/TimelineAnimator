using System;
using System.Linq;
using System.Threading.Tasks;
using TimelineAnimator.Data;
using TimelineAnimator.Interop;
using TimelineAnimator.Sequencers;

namespace TimelineAnimator;

public class IntegrationService
{
    public async Task FetchSelectedEntitiesAsync()
    {
        if (!Services.KtisisIpc.IsAvailable)
        {
            Services.Log.Error("Cannot get bones IPC not available");
            return;
        }

        try
        {
            var selectedBonesByActor = await Services.KtisisIpc.GetSelectedBonesAsync();
            if (selectedBonesByActor == null || selectedBonesByActor.Count == 0) return;

            var project = Services.ProjectService;
            var playback = Services.PlaybackService;

            foreach (var (actorIndex, TrackNames) in selectedBonesByActor)
            {
                if (TrackNames == null || TrackNames.Count == 0) continue;

                var existingSequencer = project.Sequencers.OfType<ActorSequencer>().FirstOrDefault(s => s.ActorIndex == actorIndex);
                var gameObject = Services.ObjectTable[(int)actorIndex];
                if (gameObject == null) continue;

                var nativeBones = HavokInterop.GetNativeBones(gameObject.Address);
                var defaultPose = nativeBones.ToDictionary(k => k.Key, v => v.Value.Transform);
                var fullHierarchy = nativeBones.ToDictionary(k => k.Key, v => v.Value.ParentName);

                if (existingSequencer == null)
                {
                    int currentGlobalMax = project.GetGlobalMaxFrame();
                    string actorName = $"Actor {actorIndex}";

                    var newSequencer = new ActorSequencer(actorName, (uint)actorIndex, defaultPose);
                    newSequencer.Sequence.FrameMax = currentGlobalMax;
                    newSequencer.FullSkeletonHierarchy = fullHierarchy;

                    foreach (var TrackName in TrackNames)
                    {
                        string parentName = nativeBones.TryGetValue(TrackName, out var info) ? info.ParentName : "";
                        
                        newSequencer.Sequence.AddTrack<TransformState>(TrackName, TrackType.Transform);
                        var track = newSequencer.Sequence.GetTrackByName(TrackName) as TimelineTrack<TransformState>;
                        if (track != null) track.ParentName = parentName;
                    }

                    newSequencer.RebuildHierarchy();
                    project.Sequencers.Add(newSequencer);
                }
                else
                {
                    existingSequencer.FullSkeletonHierarchy = fullHierarchy;

                    foreach (var TrackName in TrackNames)
                    {
                        string parentName = nativeBones.TryGetValue(TrackName, out var info) ? info.ParentName : "";
                        
                        var track = existingSequencer.Sequence.GetTrackByName(TrackName) as TimelineTrack<TransformState>;
                        if (track == null)
                        {
                            existingSequencer.Sequence.AddTrack<TransformState>(TrackName, TrackType.Transform);
                            track = existingSequencer.Sequence.GetTrackByName(TrackName) as TimelineTrack<TransformState>;
                            if (track != null) track.ParentName = parentName;
                        }
                        else
                        {
                            track.ParentName = parentName;
                            var existingKeyframe = track.Keyframes.FirstOrDefault(k => k.Frame == playback.CurrentFrame);
                            
                            TransformState? boneTransform = defaultPose.TryGetValue(TrackName, out var bone) ? bone : (TransformState?)null;

                            if (existingKeyframe == null) track.AddKeyframe(playback.CurrentFrame, boneTransform ?? TransformState.Identity);
                            else if (boneTransform.HasValue) existingKeyframe.Value = boneTransform.Value;
                        }
                    }

                    existingSequencer.RebuildHierarchy();
                }
            }

            if (project.Sequencers.Count > 0 && Services.WorkspaceService.ActiveSequencerIndex < 0) 
                Services.WorkspaceService.ActiveSequencerIndex = project.Sequencers.Count - 1;
            else if (project.Sequencers.Count > 0 && Services.WorkspaceService.ActiveSequencerIndex >= project.Sequencers.Count) 
                Services.WorkspaceService.ActiveSequencerIndex = 0;
            else if (project.Sequencers.Count == 0) 
                Services.WorkspaceService.ActiveSequencerIndex = -1;
        }
        catch (Exception e)
        {
            Services.Log.Error(e, "FetchSelectedEntitiesAsync");
        }
    }
}