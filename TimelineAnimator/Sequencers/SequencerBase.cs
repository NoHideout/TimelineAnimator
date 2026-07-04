using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using TimelineAnimator.Data;
using TimelineAnimator.ImSequencer;

namespace TimelineAnimator.Sequencers
{
    public abstract class SequencerBase : ISequencer
    {
        public abstract string Name { get; }
        public AnimationClip Clip { get; } = new();
        public ImSequencerState State { get; } = new();
        public bool IsVisible { get; set; } = true;

        public abstract void ApplyPose(int frame);
        public abstract void DrawInspector(int currentFrame);

        public List<SequencerRow> GetFlattenedRows()
        {
            var rows = new List<SequencerRow>();
            void AddObj(AnimationObject obj, int depth)
            {
                rows.Add(new SequencerRow { Id = obj.Id, AnimObject = obj, Name = obj.Name, Depth = depth });
                
                if (obj.Tracks.Count > 0)
                {
                    byte[] guidBytes = obj.Id.ToByteArray();
                    guidBytes[15] ^= 0xFF;
                    Guid transformId = new Guid(guidBytes);

                    rows.Add(new SequencerRow { Id = transformId, AnimObject = obj, Name = "Transform", Depth = depth + 1 });

                    foreach (var t in obj.Tracks)
                        rows.Add(new SequencerRow { Id = t.Id, PropTrack = t, Name = t.Property.ToString(), Depth = depth + 2 });
                }

                foreach (var child in Clip.Objects.Where(o => o.ParentId == obj.Id))
                    AddObj(child, depth + 1);
            }
            
            foreach (var root in Clip.Objects.Where(o => !o.ParentId.HasValue)) AddObj(root, 0);
            
            return rows;
        }

        public virtual void Draw(ImSequencerCore uiCore, ref int currentFrame, ref int selectedEntry, bool modifierHeld)
        {
            uiCore.Draw(Name, State, Clip, ref currentFrame, ref selectedEntry, modifierHeld);
            HandleContextMenu(modifierHeld, ref selectedEntry);
        }

        public List<CurveKeyframe> GetSelectedKeyframes()
        {
            var selectedIds = State.SelectedKeyframes.Select(sk => sk.keyframeId).ToHashSet();
            return Clip.Objects.SelectMany(o => o.Tracks).SelectMany(t => t.Curve.Keys)
                .Where(k => selectedIds.Contains(k.Id)).ToList();
        }

        public void DeleteSelectedKeyframes()
        {
            var selectedIds = State.SelectedKeyframes.Select(sk => sk.keyframeId).ToHashSet();
            foreach (var track in Clip.Objects.SelectMany(o => o.Tracks))
            {
                track.Curve.Keys.RemoveAll(k => selectedIds.Contains(k.Id));
            }
            State.SelectedKeyframes.Clear();
        }

        public void RemoveTrackSafely(int index)
        {
            var rows = GetFlattenedRows();
            if (index < 0 || index >= rows.Count) return;
            var targetRow = rows[index];

            if (targetRow.AnimObject != null)
            {
                var toDelete = new HashSet<Guid> { targetRow.Id };
                bool added;
                do
                {
                    added = false;
                    foreach (var obj in Clip.Objects)
                    {
                        if (obj.ParentId.HasValue && toDelete.Contains(obj.ParentId.Value) && !toDelete.Contains(obj.Id))
                        {
                            toDelete.Add(obj.Id);
                            added = true;
                        }
                    }
                } while (added);
                Clip.Objects.RemoveAll(o => toDelete.Contains(o.Id));
            }
            else if (targetRow.PropTrack != null)
            {
                foreach (var obj in Clip.Objects)
                    obj.Tracks.RemoveAll(t => t.Id == targetRow.Id);
            }
            State.SelectedKeyframes.Clear();
            State.contextKeyframe = null;
            RebuildHierarchy();
        }

        public void HandleContextMenu(bool modifierHeld, ref int sharedSelectedEntry)
        {
            using var popup = Dalamud.Interface.Utility.Raii.ImRaii.Popup("SequencerContextMenu");
            if (!popup) return;
            
            var hasSelection = State.SelectedKeyframes.Any();
            var contextKf = State.contextKeyframe;
            var canUseKeyframe = contextKf != null || hasSelection;
            
            var rows = GetFlattenedRows();
            bool validRow = State.contextTrackIndex >= 0 && State.contextTrackIndex < rows.Count;
            bool isPropTrack = validRow && rows[State.contextTrackIndex].PropTrack != null;

            if (ImGui.MenuItem("Edit Easing / Graph", string.Empty, false, isPropTrack))
            {
                sharedSelectedEntry = State.contextTrackIndex;
                Services.WorkspaceService.SharedSelectedEntry = sharedSelectedEntry;
                
                Services.WorkspaceService.RequestEditGraph(null);
                
                ImGui.CloseCurrentPopup();
            }

            ImGui.Separator();

            if (ImGui.MenuItem("Copy", string.Empty, false, canUseKeyframe))
            {
                var keyframesToCopy = new List<CurveKeyframe>();
                var trackMap = new Dictionary<Guid, Guid>();
                
                var sourceKeyframes = hasSelection ? GetSelectedKeyframes() : new List<CurveKeyframe>();
                if (!hasSelection && contextKf != null) sourceKeyframes.Add(contextKf);

                foreach (var kf in sourceKeyframes)
                {
                    var track = Clip.Objects.SelectMany(o => o.Tracks).FirstOrDefault(t => t.Curve.Keys.Any(k => k.Id == kf.Id));
                    if (track != null)
                    {
                        var clone = new CurveKeyframe
                        {
                            Frame = kf.Frame,
                            Value = kf.Value,
                            Tangents = kf.Tangents,
                            Interpolation = kf.Interpolation,
                            Shape = kf.Shape,
                            CustomColor = kf.CustomColor
                        };
                        trackMap[clone.Id] = track.Id;
                        keyframesToCopy.Add(clone);
                    }
                }
                
                Clipboard.Copy(keyframesToCopy, trackMap);
                ImGui.CloseCurrentPopup();
            }

            if (ImGui.MenuItem("Paste", string.Empty, false, Clipboard.HasData))
            {
                var copiedData = Clipboard.GetKeyframesForPasting();
                if (copiedData.Count > 0)
                {
                    int minFrame = copiedData.Min(c => c.Keyframe.Frame);
                    int frameOffset = State.contextMouseFrame - minFrame;

                    State.SelectedKeyframes.Clear();

                    foreach (var copied in copiedData)
                    {
                        var track = Clip.Objects.SelectMany(o => o.Tracks).FirstOrDefault(t => t.Id == copied.TrackId);
                        if (track != null)
                        {
                            int targetFrame = copied.Keyframe.Frame + frameOffset;
                            
                            var newKf = track.Curve.AddKey(targetFrame, copied.Keyframe.Value);
                            newKf.Tangents = copied.Keyframe.Tangents;
                            newKf.Interpolation = copied.Keyframe.Interpolation;
                            newKf.Shape = copied.Keyframe.Shape;
                            newKf.CustomColor = copied.Keyframe.CustomColor;

                            int trackIndex = rows.FindIndex(r => r.Id == track.Id);
                            if (trackIndex >= 0)
                            {
                                State.SelectedKeyframes.Add(new SelectedKeyframe(trackIndex, newKf.Id));
                            }
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
                    DeleteSelectedKeyframes();
                }
                else if (contextKf != null)
                {
                    foreach (var track in Clip.Objects.SelectMany(o => o.Tracks))
                        track.Curve.Keys.RemoveAll(k => k.Id == contextKf.Id);
                }
                
                ImGui.CloseCurrentPopup();
            }
            
            if (!modifierHeld) ImGui.EndDisabled();

            DrawAdditionalContextMenus(modifierHeld, ref sharedSelectedEntry);
        }

        protected virtual void DrawAdditionalContextMenus(bool modifierHeld, ref int sharedSelectedEntry) { }

        public virtual void RebuildHierarchy()
        {
            int GetDepth(AnimationObject obj, int currentDepth = 0)
            {
                if (!obj.ParentId.HasValue || currentDepth > 50) return currentDepth;
                var parent = Clip.Objects.FirstOrDefault(o => o.Id == obj.ParentId.Value);
                return parent != null ? GetDepth(parent, currentDepth + 1) : currentDepth;
            }

            foreach (var obj in Clip.Objects) obj.Depth = GetDepth(obj);
        }

        protected float EvaluateProperty(AnimationObject obj, PropertyType type, int frame, float defaultVal)
        {
            var track = obj.GetTrack(type);
            return track == null ? defaultVal : AnimationHelpers.EvaluateCurve(track.Curve, frame, defaultVal);
        }

    }
}