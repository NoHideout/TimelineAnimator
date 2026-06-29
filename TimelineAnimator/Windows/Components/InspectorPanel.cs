using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using TimelineAnimator.Data;
using TimelineAnimator.ImSequencer;
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

            if (workspace.ActiveSequencerIndex < 0 || workspace.ActiveSequencerIndex >= project.Sequencers.Count)
            {
                ImGui.TextDisabled("No Sequence Active");
                return;
            }

            var activeSeq = project.Sequencers[workspace.ActiveSequencerIndex] as SequencerBase;
            if (activeSeq == null) return;

            activeSeq.DrawInspector(Services.PlaybackService.CurrentFrame);

            DrawGlobalSettings(activeSeq);

            if (workspace.SharedSelectedEntry >= 0 && workspace.SharedSelectedEntry < activeSeq.Sequence.Tracks.Count)
            {
                var selectedTrack = activeSeq.Sequence.Tracks[workspace.SharedSelectedEntry];
                string folderName = selectedTrack is FolderTrack ? selectedTrack.Name : selectedTrack.ParentName;
                var folderTrack = activeSeq.Sequence.GetTrackByName(folderName) as FolderTrack;

                if (folderTrack != null)
                {
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    ImGui.Text($"Track Properties: {folderTrack.DisplayName}");
                    DrawTrackControls(activeSeq, folderName);

                    ImGui.Spacing();
                    if (!modifier) ImGui.BeginDisabled();
                    if (ImGui.Button("Delete Track"))
                    {
                        activeSeq.RemoveTrackSafely(workspace.SharedSelectedEntry);
                        workspace.SharedSelectedEntry = -1;
                    }
                    if (!modifier)
                    {
                        ImGui.EndDisabled();
                        ShowTooltip($"Hold {Services.Configuration.ModifierKey} to delete track", disabled: true);
                    }
                }
            }

            int selectedCount = activeSeq.GetSelectedKeyframeCount();
            if (selectedCount > 0)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                if (selectedCount > 1)
                {
                    ImGui.Text($"{selectedCount} Keyframes Selected");
                }
                else
                {
                    DrawSingleKeyframeSettings(activeSeq);
                }

                ImGui.Spacing();
                if (!modifier) ImGui.BeginDisabled();
                if (ImGui.Button("Delete Selection"))
                    activeSeq.DeleteSelectedKeyframes();
                if (!modifier)
                {
                    ImGui.EndDisabled();
                    ShowTooltip($"Hold {Services.Configuration.ModifierKey} to delete", disabled: true);
                }

                DrawKeyframeStyleSettings(activeSeq);
            }
        }

        private static void DrawGlobalSettings(SequencerBase activeSequencer)
        {
            ImGui.Text("Global Settings");
            int frameMax = Services.ProjectService.GetGlobalMaxFrame();
            int minFrames = Services.ProjectService.GetGlobalMinFrame() + 1;

            if (ImGui.DragInt("Max Frames", ref frameMax, 1.0f, minFrames, 10000))
                Services.ProjectService.SetMaxFrameForAll(frameMax);

            ImGui.Spacing();
            if (ImGui.Button("Save"))
            {
                string configDir = Services.PluginInterface.GetPluginConfigDirectory();
                
                Services.FileDialogManager.SaveFileDialog("Save Animation", ".xivanim", "animation.xivanim", "xivanim",
                    (success, path) =>
                    {
                        if (success) Services.ProjectService.SaveAnimation(path, activeSequencer);
                    }, configDir);
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

        private static void DrawSingleKeyframeSettings(SequencerBase activeSequencer)
        {
            var keyframe = activeSequencer.GetFirstSelectedKeyframe();
            var track = activeSequencer.Sequence.GetTrack(Services.WorkspaceService.SharedSelectedEntry);

            ImGui.Text($"Track {Services.WorkspaceService.SharedSelectedEntry + 1} Selected");
            if (track != null)
            {
                string label = activeSequencer is ActorSequencer ? "Bone" : "Track";
                ImGui.Text($"{label}: {track.DisplayName}");
                ShowTooltip($"Internal Name: {track.Name}");
            }

            if (keyframe != null) ImGui.Text($"Frame: {keyframe.Frame}");
            else ImGui.TextDisabled("Keyframe not found.");
        }

        private static void DrawKeyframeStyleSettings(SequencerBase activeSequencer)
        {
            ImGui.Separator();
            ImGui.Spacing();

            var representativeKeyframe = activeSequencer.GetFirstSelectedKeyframe();
            if (representativeKeyframe != null)
            {
                ImGui.Text("Keyframe Style");
                int currentShape = (int)representativeKeyframe.Shape;
                if (ImGui.Combo("Shape", ref currentShape, KeyframeShapeNames, KeyframeShapeNames.Length))
                {
                    var newShape = (KeyframeShape)currentShape;
                    foreach (var kf in activeSequencer.GetSelectedKeyframes()) kf.Shape = newShape;
                }

                bool customColor = representativeKeyframe.CustomColor.HasValue;
                if (ImGui.Checkbox("Custom Color", ref customColor))
                {
                    uint? newColor = customColor ? 0xFFFFFFFF : null;
                    foreach (var kf in activeSequencer.GetSelectedKeyframes()) kf.CustomColor = newColor;
                }

                ImGui.SameLine();
                if (representativeKeyframe.CustomColor.HasValue)
                {
                    uint abgr = representativeKeyframe.CustomColor.Value;
                    Vector4 rgba = new(
                        ((abgr >> 0) & 0xFF) / 255.0f, ((abgr >> 8) & 0xFF) / 255.0f,
                        ((abgr >> 16) & 0xFF) / 255.0f, ((abgr >> 24) & 0xFF) / 255.0f
                    );

                    if (ImGui.ColorEdit4("##KeyframeColor", ref rgba, ImGuiColorEditFlags.NoInputs))
                    {
                        uint finalColor =
                            (((uint)(rgba.X * 255) & 0xFF) << 0) | (((uint)(rgba.Y * 255) & 0xFF) << 8) |
                            (((uint)(rgba.Z * 255) & 0xFF) << 16) | (((uint)(rgba.W * 255) & 0xFF) << 24);
                        foreach (var kf in activeSequencer.GetSelectedKeyframes()) kf.CustomColor = finalColor;
                    }
                }
            }
        }

        private static void DrawTrackControls(ISequencer activeSeq, string folderName)
        {
            int currentFrame = Services.PlaybackService.CurrentFrame;
            var defaultPose = activeSeq.DefaultPose.TryGetValue(folderName, out var dp) ? dp : TransformState.Identity;
            
            bool poseChanged = false;
            bool hierarchyChanged = false;

            var posTrack = GetOrAddTrack<Vector3>(activeSeq, folderName, "Position", "Position", TrackType.Vector3, ref hierarchyChanged);
            DrawVector3Control("Position", posTrack, currentFrame, defaultPose.Position, ref poseChanged);

            var rotTrack = GetOrAddTrack<Quaternion>(activeSeq, folderName, "Rotation", "Rotation", TrackType.Quaternion, ref hierarchyChanged);
            DrawRotationControl("Rotation", rotTrack, currentFrame, defaultPose.Rotation, ref poseChanged);

            if (activeSeq is CameraSequencer)
            {
                var fovTrack = GetOrAddTrack<float>(activeSeq, folderName, "FOV", "Field of View", TrackType.Float, ref hierarchyChanged);
                DrawFloatControl("Field of View", fovTrack, currentFrame, defaultPose.FieldOfView, ref poseChanged);
            }
            else
            {
                var scaleTrack = GetOrAddTrack<Vector3>(activeSeq, folderName, "Scale", "Scale", TrackType.Vector3, ref hierarchyChanged);
                DrawVector3Control("Scale", scaleTrack, currentFrame, defaultPose.Scale, ref poseChanged);
            }

            if (hierarchyChanged) activeSeq.RebuildHierarchy();
            if (poseChanged) activeSeq.ApplyPose(currentFrame);
        }

        private static TimelineTrack<T> GetOrAddTrack<T>(ISequencer activeSeq, string folderName, string suffix, string displayName, TrackType type, ref bool hierarchyChanged)
        {
            string trackName = $"{folderName}_{suffix}";
            var track = activeSeq.Sequence.GetTrackByName(trackName) as TimelineTrack<T>;
            if (track == null)
            {
                track = activeSeq.Sequence.AddTrack<T>(trackName, type);
                track.ParentName = folderName;
                track.DisplayName = displayName;
                hierarchyChanged = true;
            }
            return track;
        }

        private static void DrawVector3Control(string label, TimelineTrack<Vector3> track, int currentFrame, Vector3 fallback, ref bool changed)
        {
            ImGui.PushID(label);
            
            ImGui.Text(label);
            
            var existingKf = track.Keyframes.FirstOrDefault(k => k.Frame == currentFrame);
            bool hasKeyframe = existingKf != null;

            uint color = hasKeyframe ? ImGui.GetColorU32(ImGuiCol.ButtonActive) : ImGui.GetColorU32(ImGuiCol.TextDisabled);
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Circle))
            {
                if (hasKeyframe) track.Keyframes.Remove(existingKf!);
                else track.AddKeyframe(currentFrame, AnimationHelpers.GetInterpolatedVector3(track, currentFrame, fallback) ?? fallback);
                changed = true;
            }
            ImGui.PopStyleColor();

            ImGui.SameLine();
            var val = hasKeyframe ? existingKf!.Value : (AnimationHelpers.GetInterpolatedVector3(track, currentFrame, fallback) ?? fallback);
            
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.DragFloat3($"##{label}_input", ref val, 0.01f))
            {
                if (hasKeyframe) existingKf!.Value = val;
                else track.AddKeyframe(currentFrame, val);
                changed = true;
            }
            
            ImGui.Spacing();
            ImGui.PopID();
        }

        private static void DrawRotationControl(string label, TimelineTrack<Quaternion> track, int currentFrame, Quaternion fallback, ref bool changed)
        {
            ImGui.PushID(label);
            
            ImGui.Text(label);
            
            var existingKf = track.Keyframes.FirstOrDefault(k => k.Frame == currentFrame);
            bool hasKeyframe = existingKf != null;

            uint color = hasKeyframe ? ImGui.GetColorU32(ImGuiCol.ButtonActive) : ImGui.GetColorU32(ImGuiCol.TextDisabled);
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Circle))
            {
                if (hasKeyframe) track.Keyframes.Remove(existingKf!);
                else track.AddKeyframe(currentFrame, AnimationHelpers.GetInterpolatedQuaternion(track, currentFrame, fallback) ?? fallback);
                changed = true;
            }
            ImGui.PopStyleColor();

            ImGui.SameLine();
            var qVal = hasKeyframe ? existingKf!.Value : (AnimationHelpers.GetInterpolatedQuaternion(track, currentFrame, fallback) ?? fallback);
            var euler = ToEulerAngles(qVal);
            
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.DragFloat3($"##{label}_input", ref euler, 0.01f))
            {
                var newVal = Quaternion.CreateFromYawPitchRoll(euler.Y, euler.X, euler.Z);
                if (hasKeyframe) existingKf!.Value = newVal;
                else track.AddKeyframe(currentFrame, newVal);
                changed = true;
            }
            
            ImGui.Spacing();
            ImGui.PopID();
        }

        private static void DrawFloatControl(string label, TimelineTrack<float> track, int currentFrame, float fallback, ref bool changed)
        {
            ImGui.PushID(label);
            
            ImGui.Text(label);
            
            var existingKf = track.Keyframes.FirstOrDefault(k => k.Frame == currentFrame);
            bool hasKeyframe = existingKf != null;

            uint color = hasKeyframe ? ImGui.GetColorU32(ImGuiCol.ButtonActive) : ImGui.GetColorU32(ImGuiCol.TextDisabled);
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Circle))
            {
                if (hasKeyframe) track.Keyframes.Remove(existingKf!);
                else track.AddKeyframe(currentFrame, AnimationHelpers.GetInterpolatedFloat(track, currentFrame, fallback));
                changed = true;
            }
            ImGui.PopStyleColor();

            ImGui.SameLine();
            var val = hasKeyframe ? existingKf!.Value : AnimationHelpers.GetInterpolatedFloat(track, currentFrame, fallback);
            
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.DragFloat($"##{label}_input", ref val, 0.01f))
            {
                if (hasKeyframe) existingKf!.Value = val;
                else track.AddKeyframe(currentFrame, val);
                changed = true;
            }
            
            ImGui.Spacing();
            ImGui.PopID();
        }

        private static Vector3 ToEulerAngles(Quaternion q)
        {
            Vector3 angles = new();
            float sinp = 2 * (q.W * q.X - q.Y * q.Z);
            if (Math.Abs(sinp) >= 1) angles.X = (float)Math.CopySign(Math.PI / 2, sinp);
            else angles.X = (float)Math.Asin(sinp);

            float sinyCosp = 2 * (q.W * q.Y + q.X * q.Z);
            float cosyCosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            angles.Y = (float)Math.Atan2(sinyCosp, cosyCosp);

            float sinrCosp = 2 * (q.W * q.Z + q.X * q.Y);
            float cosrCosp = 1 - 2 * (q.X * q.X + q.Z * q.Z);
            angles.Z = (float)Math.Atan2(sinrCosp, cosrCosp);
            return angles;
        }

        private static void ShowTooltip(string text, bool disabled = false)
        {
            if (!Services.Configuration.ShowTooltips) return;
            if (ImGui.IsItemHovered(disabled ? ImGuiHoveredFlags.AllowWhenDisabled : ImGuiHoveredFlags.None))
                ImGui.SetTooltip(text);
        }
    }
}