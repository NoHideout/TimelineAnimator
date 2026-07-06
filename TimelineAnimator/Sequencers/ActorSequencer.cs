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
            
            foreach (var obj in Clip.Objects)
            {
                if (obj.Type != ObjectType.Bone) continue;
                if (!Clip.BasePose.BonePoses.TryGetValue(obj.Name, out var defaultState)) continue;

                float posX = EvaluateProperty(obj, PropertyType.PositionX, frame, defaultState.Position.X);
                float posY = EvaluateProperty(obj, PropertyType.PositionY, frame, defaultState.Position.Y);
                float posZ = EvaluateProperty(obj, PropertyType.PositionZ, frame, defaultState.Position.Z);
                Vector3 pos = new Vector3(posX, posY, posZ);

                var defaultEuler = AnimationHelpers.ToEulerAngles(defaultState.Rotation);
                Quaternion rot = AnimationHelpers.EvaluateRotation(obj, frame, defaultEuler);

                float scaleX = EvaluateProperty(obj, PropertyType.ScaleX, frame, defaultState.Scale.X);
                float scaleY = EvaluateProperty(obj, PropertyType.ScaleY, frame, defaultState.Scale.Y);
                float scaleZ = EvaluateProperty(obj, PropertyType.ScaleZ, frame, defaultState.Scale.Z);
                Vector3 scale = new Vector3(scaleX, scaleY, scaleZ);

                matrices[obj.Name] = Matrix4x4.CreateScale(scale) * 
                                     Matrix4x4.CreateFromQuaternion(rot) * 
                                     Matrix4x4.CreateTranslation(pos);
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

            if (!modifierHeld && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && Services.Configuration.ShowTooltips)
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
                
                while (FullSkeletonHierarchy.TryGetValue(currentSearchName, out var parentName) && !string.IsNullOrEmpty(parentName))
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