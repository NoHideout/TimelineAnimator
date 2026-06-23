using Dalamud.Bindings.ImGui;
using TimelineAnimator.Data;
using TimelineAnimator.ImSequencer;

namespace TimelineAnimator.Sequencers
{
    public abstract class SequencerBase : ISequencer
    {
        public abstract string Name { get; }
        public TimelineSequence Sequence { get; } = new();
        public ImSequencerState State { get; } = new();
        public Dictionary<string, TransformState> DefaultPose { get; set; } = new();
        public bool IsVisible { get; set; } = true;
        public abstract void ApplyPose(int frame);
        public abstract void DrawInspector(int currentFrame);
        public abstract void RebuildHierarchy();

        public virtual void Draw(ImSequencerCore uiCore, ref int currentFrame, ref int selectedEntry, bool modifierHeld)
        {
            uiCore.Draw(Name, State, Sequence, ref currentFrame, ref selectedEntry, modifierHeld);
            this.HandleContextMenu(modifierHeld, ref selectedEntry);
        }

        public List<TrackKeyframe> GetSelectedKeyframes()
        {
            return State.SelectedKeyframes
                .Select(sk => Sequence.GetTrack(sk.trackIndex)?.Keyframes.FirstOrDefault(k => k.Id == sk.keyframeId))
                .Where(kf => kf != null)
                .ToList()!;
        }

        public int GetSelectedKeyframeCount() => State.SelectedKeyframes.Count;

        public TrackKeyframe? GetFirstSelectedKeyframe() => GetSelectedKeyframes().FirstOrDefault();

        public void DeleteSelectedKeyframes()
        {
            var selectedIds = State.SelectedKeyframes.Select(sk => sk.keyframeId).ToHashSet();
            foreach (var trackIndex in State.SelectedKeyframes.Select(x => x.trackIndex).Distinct())
            {
                var track = Sequence.GetTrack(trackIndex);
                if (track != null) track.Keyframes.RemoveAll(k => selectedIds.Contains(k.Id));
            }

            State.SelectedKeyframes.Clear();
        }

        public virtual void RemoveTrackSafely(int index)
        {
            if (index < 0 || index >= Sequence.Tracks.Count) return;

            var trackToDelete = Sequence.Tracks[index];
            var parentOfDeleted = trackToDelete.ParentName;

            foreach (var t in Sequence.Tracks)
            {
                if (t.ParentName == trackToDelete.Name) t.ParentName = parentOfDeleted;
            }

            Sequence.RemoveTrack(index);

            var updatedSelection = new HashSet<SelectedKeyframe>();
            foreach (var sk in State.SelectedKeyframes)
            {
                if (sk.trackIndex < index) updatedSelection.Add(sk);
                else if (sk.trackIndex > index)
                    updatedSelection.Add(new SelectedKeyframe(sk.trackIndex - 1, sk.keyframeId));
            }

            State.SelectedKeyframes = updatedSelection;
            RebuildHierarchy();
        }

        public virtual void HandleContextMenu(bool modifierHeld, ref int sharedSelectedEntry)
        {
            using var popup = Dalamud.Interface.Utility.Raii.ImRaii.Popup("SequencerContextMenu");
            if (!popup) return;

            var hasSelection = State.SelectedKeyframes.Any();
            var contextKf = State.contextKeyframe;
            var canUseKeyframe = contextKf != null || hasSelection;

            if (ImGui.MenuItem("Edit Easing", string.Empty, false, canUseKeyframe))
            {
                var keyframesToEdit = new List<TrackKeyframe>();
                if (hasSelection)
                {
                    foreach (var sk in State.SelectedKeyframes)
                    {
                        var track = Sequence.GetTrack(sk.trackIndex);
                        var kf = track?.Keyframes.FirstOrDefault(k => k.Id == sk.keyframeId);
                        if (kf != null) keyframesToEdit.Add(kf);
                    }
                }
                else if (contextKf != null)
                {
                    keyframesToEdit.Add(contextKf);
                }

                if (keyframesToEdit.Any())
                {
                    Services.WorkspaceService.RequestEditEasing(keyframesToEdit);
                }

                ImGui.CloseCurrentPopup();
            }

            if (ImGui.MenuItem("Copy", string.Empty, false, canUseKeyframe))
            {
                var keyframesToCopy = new List<TrackKeyframe>();
                var keyframeToTrackMap = new Dictionary<Guid, int>();

                if (hasSelection)
                {
                    foreach (var sk in State.SelectedKeyframes)
                    {
                        var track = Sequence.GetTrack(sk.trackIndex);
                        var kf = track?.Keyframes.FirstOrDefault(k => k.Id == sk.keyframeId);
                        if (kf != null)
                        {
                            keyframesToCopy.Add(kf);
                            keyframeToTrackMap[kf.Id] = sk.trackIndex;
                        }
                    }
                }
                else if (contextKf != null)
                {
                    keyframesToCopy.Add(contextKf);
                    keyframeToTrackMap[contextKf.Id] = State.contextTrackIndex;
                }

                Clipboard.Copy(keyframesToCopy, keyframeToTrackMap);
                ImGui.CloseCurrentPopup();
            }

            if (ImGui.MenuItem("Paste", string.Empty, false, Clipboard.HasData))
            {
                var pasteFrame = State.contextMouseFrame;
                var clipboardData = Clipboard.GetKeyframesForPasting();

                if (clipboardData.Any())
                {
                    var allFrames = clipboardData.Select(ckf => ckf.Keyframe.Frame).ToList();
                    var minFrame = allFrames.Min();
                    var maxFrame = allFrames.Max();
                    var blockCenterFrame = (minFrame + maxFrame) / 2;

                    var keyframesByTrack = clipboardData.GroupBy(ckf => ckf.TrackIndex);

                    foreach (var group in keyframesByTrack)
                    {
                        var targetTrackIndex = group.Key;
                        var targetTrack = Sequence.GetTrack(targetTrackIndex);

                        if (targetTrack != null)
                        {
                            foreach (var copiedKeyframe in group)
                            {
                                var frameOffset = copiedKeyframe.Keyframe.Frame - blockCenterFrame;
                                var newFrame = pasteFrame + frameOffset;

                                targetTrack.Keyframes.RemoveAll(k => k.Frame == newFrame);

                                var newKf = new TrackKeyframe(newFrame, copiedKeyframe.Keyframe.Transform)
                                {
                                    P1 = copiedKeyframe.Keyframe.P1,
                                    P2 = copiedKeyframe.Keyframe.P2,
                                    Shape = copiedKeyframe.Keyframe.Shape,
                                    CustomColor = copiedKeyframe.Keyframe.CustomColor,
                                };

                                targetTrack.Keyframes.Add(newKf);
                            }

                            targetTrack.Keyframes = targetTrack.Keyframes.OrderBy(k => k.Frame).ToList();
                        }
                    }
                }

                ImGui.CloseCurrentPopup();
            }

            ImGui.Separator();

            if (!modifierHeld) ImGui.BeginDisabled();
            if (ImGui.MenuItem("Delete Keyframe", string.Empty, false, canUseKeyframe))
            {
                if (hasSelection)
                {
                    var toDelete = State.SelectedKeyframes.ToList();
                    foreach (var sk in toDelete)
                    {
                        var track = Sequence.GetTrack(sk.trackIndex);
                        track?.DeleteKeyframe(sk.keyframeId);
                    }

                    State.SelectedKeyframes.Clear();
                }
                else if (contextKf != null)
                {
                    var track = Sequence.GetTrack(State.contextTrackIndex);
                    track?.DeleteKeyframe(contextKf.Id);
                }

                ImGui.CloseCurrentPopup();
            }

            if (!modifierHeld) ImGui.EndDisabled();

            DrawAdditionalContextMenus(modifierHeld, ref sharedSelectedEntry);
        }

        protected virtual void DrawAdditionalContextMenus(bool modifierHeld, ref int sharedSelectedEntry)
        {
        }
    }
}