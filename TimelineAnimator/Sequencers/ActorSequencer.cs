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

        public ActorSequencer(string name, uint actorIndex, Dictionary<string, TransformState>? defaultPose)
        {
            Name = name;
            ActorIndex = actorIndex;
            DefaultPose = defaultPose;
        }

        public override void ApplyPose(int frame)
        {
            var matrices = new Dictionary<string, Matrix4x4>();

            foreach (var folderTrack in Sequence.Tracks.OfType<FolderTrack>())
            {
                string boneName = folderTrack.Name;
                if (!DefaultPose.TryGetValue(boneName, out var defaultState)) continue;

                var posTrack = Sequence.GetTrackByName($"{boneName}_Position") as TimelineTrack<Vector3>;
                var rotTrack = Sequence.GetTrackByName($"{boneName}_Rotation") as TimelineTrack<Quaternion>;
                var scaleTrack = Sequence.GetTrackByName($"{boneName}_Scale") as TimelineTrack<Vector3>;

                if (posTrack == null || rotTrack == null || scaleTrack == null) continue;

                var pos = AnimationHelpers.GetInterpolatedVector3(posTrack, frame, defaultState.Position) ?? defaultState.Position;
                var rot = AnimationHelpers.GetInterpolatedQuaternion(rotTrack, frame, defaultState.Rotation) ?? defaultState.Rotation;
                var scale = AnimationHelpers.GetInterpolatedVector3(scaleTrack, frame, defaultState.Scale) ?? defaultState.Scale;

                matrices[boneName] = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rot) * Matrix4x4.CreateTranslation(pos);
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

            bool isTrackContext = State.contextTrackIndex >= 0 && State.contextTrackIndex < Sequence.Tracks.Count;

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
            foreach (var track in Sequence.Tracks)
            {
                if (track is FolderTrack && FullSkeletonHierarchy.TryGetValue(track.Name, out var trueParent))
                {
                    track.ParentName = trueParent;
                }
            }

            var existingTrackNames = Sequence.Tracks.Select(t => t.Name).ToHashSet();

            foreach (var track in Sequence.Tracks)
            {
                if (track is FolderTrack)
                {
                    string p = track.ParentName;
                    while (!string.IsNullOrEmpty(p) && !existingTrackNames.Contains(p))
                    {
                        if (FullSkeletonHierarchy.TryGetValue(p, out var grandParent)) p = grandParent;
                        else p = string.Empty;
                    }
                    track.ParentName = p;
                }
            }
            base.RebuildHierarchy();
        }
    }
}