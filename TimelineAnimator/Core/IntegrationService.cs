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
                    var gameObject = Services.ObjectTable[actorIndex];
                    if (gameObject == null) continue;

                    var nativeBones = HavokInterop.GetNativeBones(gameObject.Address);

                    var basePose = new AnimationPose();
                    foreach (var kvp in nativeBones)
                    {
                        basePose.BonePoses[kvp.Key] = new TransformPose 
                        {
                            Position = kvp.Value.Transform.Position,
                            Rotation = kvp.Value.Transform.Rotation,
                            Scale = kvp.Value.Transform.Scale
                        };
                    }
                    
                    // store scene orig in case of migration
                    var player = Services.ObjectTable[0];
                    if (player != null)
                    {
                        basePose.SceneOrigin = new TransformPose
                        {
                            Position = player.Position,
                            Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, player.Rotation),
                            Scale = Vector3.One
                        };
                    }
                    
                    var fullHierarchy = nativeBones.ToDictionary(k => k.Key, v => v.Value.ParentName);

                    ActorSequencer targetSequencer;
                    if (existingSequencer == null)
                    {
                        int currentGlobalMax = project.GetGlobalMaxFrame();
                        string actorName = $"Actor {actorIndex}";

                        targetSequencer = new ActorSequencer(actorName, (uint)actorIndex, basePose);
                        targetSequencer.Clip.EndFrame = currentGlobalMax;
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
                        var boneData = basePose.BonePoses.TryGetValue(TrackName, out var bone) 
                            ? bone 
                            : TransformPose.Identity;

                        var obj = targetSequencer.Clip.Objects.FirstOrDefault(o => o.Name == TrackName);
    
                        if (obj == null)
                        {
                            obj = new AnimationObject 
                            { 
                                Name = TrackName, 
                                Type = ObjectType.Bone 
                            };

                            if (!string.IsNullOrEmpty(parentName))
                            {
                                var parentObj = targetSequencer.Clip.Objects.FirstOrDefault(o => o.Name == parentName);
                                if (parentObj != null) obj.ParentId = parentObj.Id;
                            }

                            targetSequencer.Clip.Objects.Add(obj);
                        }

                        void SetKey(PropertyType prop, float value)
                        {
                            var track = obj.GetOrAddTrack(prop);
                            track.Curve.AddKey(playback.CurrentFrame, value);
                        }

                        SetKey(PropertyType.PositionX, boneData.Position.X);
                        SetKey(PropertyType.PositionY, boneData.Position.Y);
                        SetKey(PropertyType.PositionZ, boneData.Position.Z);
                        
                        var eulerRotation = AnimationHelpers.ToEulerAngles(boneData.Rotation);
                        float GetUnwrappedAngle(PropertyType prop, float rawAngle)
                        {
                            var track = obj.GetTrack(prop);
                            if (track == null || track.Curve.Keys.Count == 0) return rawAngle;
                            var previousKey = track.Curve.Keys.LastOrDefault(k => k.Frame <= playback.CurrentFrame) ?? track.Curve.Keys.Last();
                            return AnimationHelpers.UnwrapAngle(rawAngle, previousKey.Value);
                        }

                        SetKey(PropertyType.RotationX, GetUnwrappedAngle(PropertyType.RotationX, eulerRotation.X));
                        SetKey(PropertyType.RotationY, GetUnwrappedAngle(PropertyType.RotationY, eulerRotation.Y));
                        SetKey(PropertyType.RotationZ, GetUnwrappedAngle(PropertyType.RotationZ, eulerRotation.Z));

                        SetKey(PropertyType.ScaleX, boneData.Scale.X);
                        SetKey(PropertyType.ScaleY, boneData.Scale.Y);
                        SetKey(PropertyType.ScaleZ, boneData.Scale.Z);
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