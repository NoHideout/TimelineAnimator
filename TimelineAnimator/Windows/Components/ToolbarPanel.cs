using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using TimelineAnimator.Sequencers;

namespace TimelineAnimator.Windows.Components
{
    public static class ToolbarPanel
    {
        public static void Draw()
        {
            float width = ImGui.GetContentRegionAvail().X;
            float itemHeight = ImGui.GetFrameHeight();
            float spacing = ImGui.GetStyle().ItemSpacing.X;

            if (ImGuiComponents.IconButton(FontAwesomeIcon.PlusCircle))
                _ = Services.IntegrationService.FetchSelectedEntitiesAsync();
            ShowTooltip("Select bones in Ktisis first to add tracks/keyframe.");

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Video))
            {
                Services.ProjectService.AddCameraSequencer();
                Services.WorkspaceService.ActiveSequencerIndex = Services.ProjectService.Sequencers.Count - 1;
            }
            ShowTooltip("Add Camera track.");

            ImGui.SameLine();

            var cameraService = Services.CameraService;
            bool canToggle = cameraService.CanEnableCamera;
            bool isActive = cameraService.IsOverridden;

            using (ImRaii.Disabled(!canToggle))
            {
                using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.PlotLinesHovered), isActive))
                {
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Camera))
                    {
                        cameraService.IsOverridden = !isActive;
                        if (cameraService.IsOverridden)
                        {
                            var camSeq = Services.ProjectService.Sequencers.OfType<Sequencers.CameraSequencer>().FirstOrDefault();
                            camSeq?.ApplyPose(Services.PlaybackService.CurrentFrame);
                        }
                    }
                }
            }

            string tooltip = 
                !Services.ClientState.IsGPosing ? "Enter GPose to use Free Camera." : 
                !Services.ProjectService.Sequencers.OfType<Sequencers.CameraSequencer>().Any() ? "Add a Camera track first to use Free Camera." :
                "Toggle Free Camera.";
            
            ShowTooltip(tooltip, !canToggle);

            float midWidth = (itemHeight * 2) + spacing;
            float midX = (width - midWidth) * 0.5f;

            ImGui.SameLine();
            ImGui.SetCursorPosX(midX);

            var icon = Services.PlaybackService.IsPlaying ? FontAwesomeIcon.PauseCircle : FontAwesomeIcon.PlayCircle;
            if (ImGuiComponents.IconButton(icon))
                Services.PlaybackService.TogglePlay();
            ShowTooltip("Start / Pause Playback.");

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.StopCircle))
                Services.PlaybackService.Stop();
            ShowTooltip("Stop Playback.");
        }

        private static void ShowTooltip(string text, bool disabled = false)
        {
            if (!Services.Configuration.ShowTooltips) return;
            if (ImGui.IsItemHovered(disabled ? ImGuiHoveredFlags.AllowWhenDisabled : ImGuiHoveredFlags.None))
                ImGui.SetTooltip(text);
        }
    }
}