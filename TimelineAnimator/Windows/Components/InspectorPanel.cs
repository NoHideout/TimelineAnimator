using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using TimelineAnimator.Data;
using TimelineAnimator.Sequencers;

namespace TimelineAnimator.Windows.Components
{
    public static class InspectorPanel
    {
        private static readonly string[] KeyframeShapeNames = Enum.GetNames(typeof(KeyframeShape));

        public static void Draw()
        {
            var workspace = Services.WorkspaceService;
            var project = Services.ProjectService;
            bool modifier = Services.InputManager.IsModifierHeld;

            var activeSeq = workspace.GetActiveSequencer() as SequencerBase;
            if (activeSeq == null)
            {
                ImGui.TextDisabled("No Sequence Active");
                return;
            }

            activeSeq.DrawInspector(Services.PlaybackService.CurrentFrame);
            DrawGlobalSettings(activeSeq);

            AnimationObject? targetObj = null;

            if (workspace.SharedSelectedEntry >= 0)
            {
                var rows = activeSeq.GetFlattenedRows();
                if (workspace.SharedSelectedEntry < rows.Count)
                {
                    var row = rows[workspace.SharedSelectedEntry];
                    if (row.AnimObject != null)
                    {
                        targetObj = row.AnimObject;
                    }
                    else if (row.PropTrack != null)
                    {
                        targetObj = activeSeq.Clip.Objects.FirstOrDefault(o => o.Tracks.Any(t => t.Id == row.PropTrack.Id));
                    }
                }
            }

            if (targetObj != null)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.Text($"Object: {targetObj.DisplayName}");
                
                DrawTrackControls(targetObj, activeSeq);

                ImGui.Spacing();
                if (!modifier) ImGui.BeginDisabled();
                if (ImGui.Button("Delete Object"))
                {
                    activeSeq.Clip.Objects.Remove(targetObj);
                    workspace.SharedSelectedEntry = -1;
                }
                if (!modifier)
                {
                    ImGui.EndDisabled();
                    ShowTooltip($"Hold {Services.Configuration.ModifierKey} to delete object", disabled: true);
                }
            }

            var selectedKeys = activeSeq.GetSelectedKeyframes();
            if (selectedKeys.Count > 0)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                ImGui.Text($"{selectedKeys.Count} Keyframes Selected");
                
                if (!modifier) ImGui.BeginDisabled();
                if (ImGui.Button("Delete Selection")) activeSeq.DeleteSelectedKeyframes();
                if (!modifier)
                {
                    ImGui.EndDisabled();
                    ShowTooltip($"Hold {Services.Configuration.ModifierKey} to delete", disabled: true);
                }

                DrawKeyframeStyleSettings(selectedKeys);
            }
        }

        private static void DrawGlobalSettings(SequencerBase activeSequencer)
        {
            ImGui.Text("Global Settings");
            
            int frameMax = activeSequencer.Clip.EndFrame;
            if (ImGui.DragInt("Max Frames", ref frameMax, 1.0f, 1, 10000))
            {
                Services.ProjectService.SetMaxFrameForAll(frameMax);
            }

            ImGui.Spacing();
            if (ImGui.Button("Save"))
            {
                string configDir = Services.PluginInterface.GetPluginConfigDirectory();
                Services.FileDialogManager.SaveFileDialog("Save Animation", ".xivanim", "animation.xivanim", "xivanim",
                    (success, path) => { if (success) Services.ProjectService.SaveAnimation(path, activeSequencer); }, configDir);
            }
            ImGui.SameLine();
            if (ImGui.Button("Load"))
            {
                string configDir = Services.PluginInterface.GetPluginConfigDirectory();
                Services.FileDialogManager.OpenFileDialog("Load Animation", ".xivanim", (success, paths) =>
                {
                    if (success && paths.Count > 0) Services.ProjectService.LoadAnimation(paths[0], activeSequencer);
                }, 1, configDir);
            }
        }

        private static void DrawKeyframeStyleSettings(List<CurveKeyframe> selectedKeys)
        {
            ImGui.Separator();
            ImGui.Spacing();

            var representative = selectedKeys.FirstOrDefault();
            if (representative == null) return;

            ImGui.Text("Keyframe Style");
            int currentShape = (int)representative.Shape;
            if (ImGui.Combo("Shape", ref currentShape, KeyframeShapeNames, KeyframeShapeNames.Length))
            {
                var newShape = (KeyframeShape)currentShape;
                foreach (var kf in selectedKeys) kf.Shape = newShape;
            }

            bool customColor = representative.CustomColor.HasValue;
            if (ImGui.Checkbox("Custom Color", ref customColor))
            {
                uint? newColor = customColor ? 0xFFFFFFFF : null;
                foreach (var kf in selectedKeys) kf.CustomColor = newColor;
            }

            ImGui.SameLine();
            if (representative.CustomColor.HasValue)
            {
                uint abgr = representative.CustomColor.Value;
                Vector4 rgba = new(
                    ((abgr >> 0) & 0xFF) / 255.0f, ((abgr >> 8) & 0xFF) / 255.0f,
                    ((abgr >> 16) & 0xFF) / 255.0f, ((abgr >> 24) & 0xFF) / 255.0f
                );

                if (ImGui.ColorEdit4("##KeyframeColor", ref rgba, ImGuiColorEditFlags.NoInputs))
                {
                    uint finalColor = (((uint)(rgba.X * 255) & 0xFF) << 0) | (((uint)(rgba.Y * 255) & 0xFF) << 8) |
                                      (((uint)(rgba.Z * 255) & 0xFF) << 16) | (((uint)(rgba.W * 255) & 0xFF) << 24);
                    foreach (var kf in selectedKeys) kf.CustomColor = finalColor;
                }
            }
        }

        private static void DrawTrackControls(AnimationObject obj, SequencerBase activeSeq)
        {
            int currentFrame = Services.PlaybackService.CurrentFrame;
            
            foreach (var track in obj.Tracks.Where(t => t.Property != PropertyType.RotationW).OrderBy(t => t.Property))
            {
                DrawPropertySlider(Enum.GetName(track.Property), obj, track.Property, currentFrame, activeSeq);
            }
        }
        
        private static void DrawPropertySlider(string? label, AnimationObject obj, PropertyType type, int frame, SequencerBase activeSeq)
        {
            var track = obj.GetOrAddTrack(type);
            var existingKf = track.Curve.GetKey(frame);
            bool hasKeyframe = existingKf != null;

            ImGui.PushID(label);
            ImGui.Text(label);
            ImGui.SameLine();

            ImGui.PushStyleColor(ImGuiCol.Text, hasKeyframe ? ImGui.GetColorU32(ImGuiCol.ButtonActive) : ImGui.GetColorU32(ImGuiCol.TextDisabled));
            
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Circle))
            {
                if (hasKeyframe) track.Curve.Keys.Remove(existingKf!);
                else track.Curve.AddKey(frame, AnimationHelpers.EvaluateCurve(track.Curve, frame, 0f));
                
                activeSeq.ApplyPose(frame);
            }
            ImGui.PopStyleColor();

            ImGui.SameLine();
            float val = existingKf?.Value ?? AnimationHelpers.EvaluateCurve(track.Curve, frame, 0f);
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            
            if (ImGui.DragFloat($"##{label}", ref val, 0.01f))
            {
                if (hasKeyframe) existingKf!.Value = val;
                else track.Curve.AddKey(frame, val);
                
                activeSeq.ApplyPose(frame);
            }
            ImGui.PopID();
        }

        private static void ShowTooltip(string text, bool disabled = false)
        {
            if (!Services.Configuration.ShowTooltips) return;
            if (ImGui.IsItemHovered(disabled ? ImGuiHoveredFlags.AllowWhenDisabled : ImGuiHoveredFlags.None))
                ImGui.SetTooltip(text);
        }
    }
}