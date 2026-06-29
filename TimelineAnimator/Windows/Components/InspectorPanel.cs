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
        public static void Draw()
        {
            var workspace = Services.WorkspaceService;
            var project = Services.ProjectService;

            if (workspace.ActiveSequencerIndex < 0 || workspace.ActiveSequencerIndex >= project.Sequencers.Count)
            {
                ImGui.TextDisabled("No Sequence Active");
                return;
            }

            var activeSeq = project.Sequencers[workspace.ActiveSequencerIndex];

            if (workspace.SharedSelectedEntry < 0 || workspace.SharedSelectedEntry >= activeSeq.Sequence.Tracks.Count)
            {
                activeSeq.DrawInspector(Services.PlaybackService.CurrentFrame);
                return;
            }

            var selectedTrack = activeSeq.Sequence.Tracks[workspace.SharedSelectedEntry];
            string folderName = selectedTrack is FolderTrack ? selectedTrack.Name : selectedTrack.ParentName;
            var folderTrack = activeSeq.Sequence.GetTrackByName(folderName) as FolderTrack;

            if (folderTrack == null) return;

            ImGui.Text($"Inspector: {folderTrack.DisplayName ?? folderTrack.Name}");
            ImGui.Separator();
            ImGui.Spacing();

            DrawTrackControls(activeSeq, folderName);
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
            
            if (ImGui.DragFloat3(label, ref val, 0.01f))
            {
                if (hasKeyframe) existingKf!.Value = val;
                else track.AddKeyframe(currentFrame, val);
                changed = true;
            }
            ImGui.PopID();
        }

        private static void DrawRotationControl(string label, TimelineTrack<Quaternion> track, int currentFrame, Quaternion fallback, ref bool changed)
        {
            ImGui.PushID(label);
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
            
            if (ImGui.DragFloat3(label, ref euler, 0.01f))
            {
                var newVal = Quaternion.CreateFromYawPitchRoll(euler.Y, euler.X, euler.Z);
                if (hasKeyframe) existingKf!.Value = newVal;
                else track.AddKeyframe(currentFrame, newVal);
                changed = true;
            }
            ImGui.PopID();
        }

        private static void DrawFloatControl(string label, TimelineTrack<float> track, int currentFrame, float fallback, ref bool changed)
        {
            ImGui.PushID(label);
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
            
            if (ImGui.DragFloat(label, ref val, 0.01f))
            {
                if (hasKeyframe) existingKf!.Value = val;
                else track.AddKeyframe(currentFrame, val);
                changed = true;
            }
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
    }
}