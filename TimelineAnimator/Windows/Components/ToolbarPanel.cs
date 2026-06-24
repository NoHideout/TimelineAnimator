using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Bindings.ImGui;

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

        private static void ShowTooltip(string text)
        {
            if (Services.Configuration.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip(text);
        }
    }
}