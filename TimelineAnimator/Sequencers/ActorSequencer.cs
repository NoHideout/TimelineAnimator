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

            foreach (var track in Sequence.Tracks)
            {
                var transform = AnimationHelpers.GetInterpolatedTransform(this, track, frame);
                if (transform != null)
                {
                    var pos = transform.Value.Position;
                    var rot = transform.Value.Rotation;
                    var scale = transform.Value.Scale;

                    matrices[track.Name] = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rot) * Matrix4x4.CreateTranslation(pos);
                }
            }

            if (matrices.Count > 0)
            {
                _ = Services.KtisisIpc.SendAnimationFrame(ActorIndex, matrices);
            }
        }

        public override void RebuildHierarchy()
        {
            var sorted = new List<TimelineTrack>();
            var trackDict = Sequence.Tracks.ToDictionary(t => t.Name);
            var childrenMap = new Dictionary<string, List<TimelineTrack>>();

            string GetEffectiveParent(string boneName)
            {
                string current = boneName;
                int maxDepth = 100;
                while (maxDepth-- > 0 && FullSkeletonHierarchy.TryGetValue(current, out var parent) &&
                       !string.IsNullOrEmpty(parent))
                {
                    if (trackDict.ContainsKey(parent)) return parent;
                    current = parent;
                }

                var track = Sequence.GetTrackByName(boneName);
                if (track != null && !string.IsNullOrEmpty(track.ParentName) && trackDict.ContainsKey(track.ParentName))
                    return track.ParentName;

                return string.Empty;
            }

            foreach (var t in Sequence.Tracks)
            {
                string effectiveParent = GetEffectiveParent(t.Name);
                t.ParentName = effectiveParent;

                if (!childrenMap.ContainsKey(effectiveParent)) childrenMap[effectiveParent] = new List<TimelineTrack>();
                childrenMap[effectiveParent].Add(t);
            }

            foreach (var t in Sequence.Tracks) t.HasChildren = childrenMap.ContainsKey(t.Name);

            void AddNode(TimelineTrack node, int depth)
            {
                node.Depth = depth;
                sorted.Add(node);
                if (childrenMap.TryGetValue(node.Name, out var children))
                {
                    foreach (var child in children.OrderBy(c => c.Name)) AddNode(child, depth + 1);
                }
            }

            if (childrenMap.TryGetValue(string.Empty, out var roots))
            {
                foreach (var root in roots.OrderBy(t => t.Name)) AddNode(root, 0);
            }

            Sequence.Tracks = sorted;
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
    }
}