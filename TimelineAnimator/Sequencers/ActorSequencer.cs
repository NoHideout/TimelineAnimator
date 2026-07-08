using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using TimelineAnimator.Data;

namespace TimelineAnimator.Sequencers
{
    public class ActorSequencer : SequencerBase
    {
        public override string Name { get; }
        public uint ActorIndex { get; }
        public Dictionary<string, string> FullSkeletonHierarchy { get; set; } = new();

        public ActorSequencer(string name, uint actorIndex, AnimationPose basePose)
        {
            Name = name;
            ActorIndex = actorIndex;
            Clip.BasePose = basePose;
        }

        public override void ApplyPose(int frame)
        {
            var matrices = new Dictionary<string, Matrix4x4>();

            foreach (var boneKvp in Clip.BasePose.BonePoses)
            {
                var pose = boneKvp.Value;
                matrices[boneKvp.Key] = Matrix4x4.CreateScale(pose.Scale) *
                                        Matrix4x4.CreateFromQuaternion(pose.Rotation) *
                                        Matrix4x4.CreateTranslation(pose.Position);
            }

            foreach (var obj in Clip.Objects)
            {
                if (obj.Type != ObjectType.Bone) continue;
                if (!Clip.BasePose.BonePoses.TryGetValue(obj.Name, out var defaultState)) continue;

                var trackX = obj.GetTrack(PropertyType.PositionX);
                var trackY = obj.GetTrack(PropertyType.PositionY);
                var trackZ = obj.GetTrack(PropertyType.PositionZ);

                if (trackX != null || trackY != null || trackZ != null)
                {
                    float posX = EvaluateProperty(obj, PropertyType.PositionX, frame, defaultState.Position.X);
                    float posY = EvaluateProperty(obj, PropertyType.PositionY, frame, defaultState.Position.Y);
                    float posZ = EvaluateProperty(obj, PropertyType.PositionZ, frame, defaultState.Position.Z);

                    var defaultEuler = AnimationHelpers.ToEulerAngles(defaultState.Rotation);
                    Quaternion rot = AnimationHelpers.EvaluateRotation(obj, frame, defaultEuler);

                    float scaleX = EvaluateProperty(obj, PropertyType.ScaleX, frame, defaultState.Scale.X);
                    float scaleY = EvaluateProperty(obj, PropertyType.ScaleY, frame, defaultState.Scale.Y);
                    float scaleZ = EvaluateProperty(obj, PropertyType.ScaleZ, frame, defaultState.Scale.Z);

                    matrices[obj.Name] = Matrix4x4.CreateScale(new Vector3(scaleX, scaleY, scaleZ)) *
                                         Matrix4x4.CreateFromQuaternion(rot) *
                                         Matrix4x4.CreateTranslation(new Vector3(posX, posY, posZ));
                }
            }

            if (matrices.Count > 0)
            {
                _ = Services.KtisisIpc.SendAnimationFrame(ActorIndex, matrices);
            }
        }

        public override void DrawInspector(int currentFrame)
        {
        }

        protected override void DrawAdditionalContextMenus(bool modifierHeld, ref int sharedSelectedEntry)
        {
            ImGui.Separator();

            var rows = GetFlattenedRows();
            bool isTrackContext = State.contextTrackIndex >= 0 && State.contextTrackIndex < rows.Count;

            if (!modifierHeld) ImGui.BeginDisabled();

            if (ImGui.MenuItem("Delete Track", string.Empty, false, isTrackContext))
            {
                RemoveTrackSafely(State.contextTrackIndex);

                if (sharedSelectedEntry == State.contextTrackIndex)
                {
                    sharedSelectedEntry = -1;
                }

                ImGui.CloseCurrentPopup();
            }

            if (!modifierHeld) ImGui.EndDisabled();

            if (!modifierHeld && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) &&
                Services.Configuration.ShowTooltips)
            {
                ImGui.SetTooltip($"Hold {Services.Configuration.ModifierKey} to delete this track.");
            }
        }

        public override void RebuildHierarchy()
        {
            foreach (var obj in Clip.Objects)
            {
                if (obj.Type != ObjectType.Bone) continue;

                string currentSearchName = obj.Name;
                AnimationObject? closestLoadedParent = null;

                while (FullSkeletonHierarchy.TryGetValue(currentSearchName, out var parentName) &&
                       !string.IsNullOrEmpty(parentName))
                {
                    closestLoadedParent = Clip.Objects.FirstOrDefault(o => o.Name == parentName);
                    if (closestLoadedParent != null)
                    {
                        break;
                    }

                    currentSearchName = parentName;
                }

                if (closestLoadedParent != null)
                {
                    obj.ParentId = closestLoadedParent.Id;
                }
                else
                {
                    obj.ParentId = null;
                }
            }

            base.RebuildHierarchy();
        }
    }
}