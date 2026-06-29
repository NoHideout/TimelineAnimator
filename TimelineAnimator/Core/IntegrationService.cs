using System;
using System.Linq;
using System.Threading.Tasks;
using System.Numerics;
using TimelineAnimator.Data;
using TimelineAnimator.Interop;
using TimelineAnimator.Sequencers;

namespace TimelineAnimator.Core
{
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

                    ActorSequencer targetSequencer;
                    
                    if (existingSequencer == null)
                    {
                        int currentGlobalMax = project.GetGlobalMaxFrame();
                        string actorName = $"Actor {actorIndex}";

                        targetSequencer = new ActorSequencer(actorName, (uint)actorIndex, defaultPose);
                        targetSequencer.Sequence.FrameMax = currentGlobalMax;
                        targetSequencer.FullSkeletonHierarchy = fullHierarchy;
                        project.Sequencers.Add(targetSequencer);
                    }
                    else
                    {
                        targetSequencer = existingSequencer;
                        targetSequencer.FullSkeletonHierarchy = fullHierarchy;
                    }

                    foreach (var TrackName in TrackNames)
                    {
                        string parentName = nativeBones.TryGetValue(TrackName, out var info) ? info.ParentName : "";
                        TransformState boneTransform = defaultPose.TryGetValue(TrackName, out var bone) ? bone : TransformState.Identity;
                        
                        var folder = targetSequencer.Sequence.GetTrackByName(TrackName);
                        if (folder == null)
                        {
                            folder = new FolderTrack(TrackName) { ParentName = parentName };
                            targetSequencer.Sequence.Tracks.Add(folder);

                            var posTrack = targetSequencer.Sequence.AddTrack<Vector3>($"{TrackName}_Position", TrackType.Vector3);
                            posTrack.ParentName = TrackName; posTrack.DisplayName = "Position";
                            
                            var rotTrack = targetSequencer.Sequence.AddTrack<Quaternion>($"{TrackName}_Rotation", TrackType.Quaternion);
                            rotTrack.ParentName = TrackName; rotTrack.DisplayName = "Rotation";
                            
                            var scaleTrack = targetSequencer.Sequence.AddTrack<Vector3>($"{TrackName}_Scale", TrackType.Vector3);
                            scaleTrack.ParentName = TrackName; scaleTrack.DisplayName = "Scale";
                            
                            posTrack.AddKeyframe(playback.CurrentFrame, boneTransform.Position);
                            rotTrack.AddKeyframe(playback.CurrentFrame, boneTransform.Rotation);
                            scaleTrack.AddKeyframe(playback.CurrentFrame, boneTransform.Scale);
                        }
                        else
                        {
                            folder.ParentName = parentName;
                            
                            var pT = targetSequencer.Sequence.GetTrackByName($"{TrackName}_Position") as TimelineTrack<Vector3>;
                            if (pT == null) { pT = targetSequencer.Sequence.AddTrack<Vector3>($"{TrackName}_Position", TrackType.Vector3); pT.ParentName = TrackName; pT.DisplayName = "Position"; }

                            var rT = targetSequencer.Sequence.GetTrackByName($"{TrackName}_Rotation") as TimelineTrack<Quaternion>;
                            if (rT == null) { rT = targetSequencer.Sequence.AddTrack<Quaternion>($"{TrackName}_Rotation", TrackType.Quaternion); rT.ParentName = TrackName; rT.DisplayName = "Rotation"; }

                            var sT = targetSequencer.Sequence.GetTrackByName($"{TrackName}_Scale") as TimelineTrack<Vector3>;
                            if (sT == null) { sT = targetSequencer.Sequence.AddTrack<Vector3>($"{TrackName}_Scale", TrackType.Vector3); sT.ParentName = TrackName; sT.DisplayName = "Scale"; }

                            var kfPos = pT.Keyframes.FirstOrDefault(k => k.Frame == playback.CurrentFrame);
                            if (kfPos == null) pT.AddKeyframe(playback.CurrentFrame, boneTransform.Position); else kfPos.Value = boneTransform.Position;

                            var kfRot = rT.Keyframes.FirstOrDefault(k => k.Frame == playback.CurrentFrame);
                            if (kfRot == null) rT.AddKeyframe(playback.CurrentFrame, boneTransform.Rotation); else kfRot.Value = boneTransform.Rotation;

                            var kfScale = sT.Keyframes.FirstOrDefault(k => k.Frame == playback.CurrentFrame);
                            if (kfScale == null) sT.AddKeyframe(playback.CurrentFrame, boneTransform.Scale); else kfScale.Value = boneTransform.Scale;
                        }
                    }
                    
                    targetSequencer.RebuildHierarchy();
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
}