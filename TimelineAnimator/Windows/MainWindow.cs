using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using TimelineAnimator.Sequencers;
using TimelineAnimator.Windows.Components;

namespace TimelineAnimator.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private bool inspectorVisible = true;
        private readonly ImSequencer.ImSequencerCore timelineRenderer = new();

        private int deleteIndex = -1;

        public MainWindow() : base("Timeline Animator##SequencerMain")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(600, 200),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }

        public void Dispose()
        {
        }

        public override void Draw()
        {
            var style = ImGui.GetStyle();
            float totalWidth = ImGui.GetContentRegionAvail().X;

            ToolbarPanel.Draw();
            ImGui.Separator();

            float scaleFactor = ImGui.GetFrameHeight() / 20f;
            float scaledInspectorWidth = 250f * scaleFactor;
            float scaledToggleButtonWidth = 15f * scaleFactor;

            float availableHeight = MathF.Floor(ImGui.GetContentRegionAvail().Y);

            float sequencerWidth = totalWidth - scaledToggleButtonWidth - style.ItemSpacing.X;
            if (inspectorVisible) sequencerWidth -= scaledInspectorWidth + style.ItemSpacing.X;

            ImGui.BeginChild("SequencerArea", new Vector2(sequencerWidth, availableHeight), false);
            DrawSequencerTabs();
            ImGui.EndChild();

            ImGui.SameLine();
            DrawInspectorToggle(availableHeight, scaledToggleButtonWidth);

            if (inspectorVisible)
            {
                ImGui.SameLine();
                ImGui.BeginChild("InspectorPanel", new Vector2(scaledInspectorWidth, availableHeight), true);
                InspectorPanel.Draw();

                ImGui.EndChild();
            }
        }

        private void DrawSequencerTabs()
        {
            if (!ImGui.BeginTabBar("SequencerTabs", ImGuiTabBarFlags.Reorderable)) return;

            int prevIndex = Services.WorkspaceService.ActiveSequencerIndex;

            for (int i = 0; i < Services.ProjectService.Sequencers.Count; i++)
            {
                var seq = Services.ProjectService.Sequencers[i];
                bool open = true;

                if (ImGui.BeginTabItem($"{seq.Name}##{i}", ref open))
                {
                    Services.WorkspaceService.ActiveSequencerIndex = i;
                    int frame = Services.PlaybackService.CurrentFrame;
                    int selected = Services.WorkspaceService.SharedSelectedEntry;

                    seq.Draw(timelineRenderer, ref frame, ref selected, Services.InputManager.IsModifierHeld);

                    Services.PlaybackService.CurrentFrame = frame;
                    Services.WorkspaceService.SharedSelectedEntry = selected;

                    ImGui.EndTabItem();
                }

                if (!open && deleteIndex == -1) deleteIndex = i;
            }

            if (Services.WorkspaceService.ActiveSequencerIndex != prevIndex)
                Services.WorkspaceService.SharedSelectedEntry = -1;

            if (Services.WorkspaceService.ActiveSequencerIndex >= Services.ProjectService.Sequencers.Count)
                Services.WorkspaceService.ActiveSequencerIndex = Services.ProjectService.Sequencers.Count - 1;

            if (Services.WorkspaceService.ActiveSequencerIndex < 0 && Services.ProjectService.Sequencers.Count > 0)
                Services.WorkspaceService.ActiveSequencerIndex = 0;

            if (!Services.PlaybackService.IsPlaying) Services.PlaybackService.ApplyCurrentPose();

            ImGui.EndTabBar();

            if (deleteIndex >= 0)
            {
                ImGui.OpenPopup("Delete Sequence");
            }

            DrawDeleteConfirmPupUp();
        }

        private void DrawDeleteConfirmPupUp()
        {
            bool isModalOpen = true;
            if (ImGui.BeginPopupModal("Delete Sequence", ref isModalOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("Are you sure you want to delete this sequence and all its keyframes?");

                using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                {
                    ImGui.Text("This cannot be undone.");
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                if (ImGui.Button("Delete", new Vector2(120, 0)))
                {
                    if (deleteIndex >= 0 && deleteIndex < Services.ProjectService.Sequencers.Count)
                    {
                        var seqToDelete = Services.ProjectService.Sequencers[deleteIndex];

                        if (seqToDelete is CameraSequencer)
                        {
                            Services.CameraService.IsOverridden = false;
                        }

                        Services.ProjectService.Sequencers.RemoveAt(deleteIndex);

                        if (Services.WorkspaceService.ActiveSequencerIndex == deleteIndex)
                        {
                            Services.WorkspaceService.ActiveSequencerIndex = -1;
                            Services.WorkspaceService.SharedSelectedEntry = -1;
                        }
                    }

                    deleteIndex = -1;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    deleteIndex = -1;
                    ImGui.CloseCurrentPopup();
                }

                if (!isModalOpen)
                {
                    deleteIndex = -1;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        private void DrawInspectorToggle(float height, float width)
        {
            var icon = inspectorVisible ? FontAwesomeIcon.AngleRight : FontAwesomeIcon.AngleLeft;
            var size = new Vector2(width, height);

            ImGui.PushFont(Services.PluginInterface.UiBuilder.FontIcon);
            if (ImGui.Button(icon.ToIconString(), size))
                inspectorVisible = !inspectorVisible;
            ImGui.PopFont();
        }
    }
}