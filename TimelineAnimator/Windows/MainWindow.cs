using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using System.Numerics;
using TimelineAnimator.Sequencers;
using TimelineAnimator.Windows.Components;

namespace TimelineAnimator.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private bool inspectorVisible = true;
    private float inspectorWidth = 200f;
    private readonly ImSequencer.ImSequencerCore timelineRenderer = new();

    public MainWindow(Plugin plugin) : base("Timeline Animator##SequencerMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        this.plugin = plugin;
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

        float remainingHeight = ImGui.GetContentRegionAvail().Y;
        const float toggleButtonWidth = 15f;
        float sequencerWidth = totalWidth - toggleButtonWidth - style.ItemSpacing.X;
        if (inspectorVisible) sequencerWidth -= inspectorWidth + style.ItemSpacing.X;

        ImGui.BeginChild("SequencerArea", new Vector2(sequencerWidth, remainingHeight), false);
        DrawSequencerTabs(sequencerWidth, remainingHeight);
        ImGui.EndChild();

        ImGui.SameLine();
        DrawInspectorToggle(remainingHeight);
        if (inspectorVisible)
        {
            ImGui.SameLine();
            ImGui.BeginChild("InspectorPanel", new Vector2(inspectorWidth, remainingHeight), true);
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);

            InspectorPanel.Draw(plugin);

            ImGui.PopItemWidth();
            ImGui.EndChild();
        }
    }

    private void DrawSequencerTabs(float width, float height)
    {
        if (!ImGui.BeginTabBar("SequencerTabs", ImGuiTabBarFlags.Reorderable)) return;

        int prevIndex = Services.WorkspaceService.ActiveSequencerIndex;

        for (int i = Services.ProjectService.Sequencers.Count - 1; i >= 0; i--)
        {
            if (!Services.ProjectService.Sequencers[i].IsVisible)
            {
                if (Services.WorkspaceService.ActiveSequencerIndex == i)
                {
                    Services.WorkspaceService.ActiveSequencerIndex = -1;
                    Services.WorkspaceService.SharedSelectedEntry = -1;
                }

                if (Services.ProjectService.Sequencers[i] is CameraSequencer)
                {
                    Services.CameraService.IsOverridden = false;
                }

                Services.ProjectService.Sequencers.RemoveAt(i);
            }
        }

        for (int i = 0; i < Services.ProjectService.Sequencers.Count; i++)
        {
            var seq = Services.ProjectService.Sequencers[i];
            bool open = seq.IsVisible;

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

            seq.IsVisible = open;
        }

        if (Services.WorkspaceService.ActiveSequencerIndex != prevIndex) Services.WorkspaceService.SharedSelectedEntry = -1;

        if (Services.WorkspaceService.ActiveSequencerIndex >= Services.ProjectService.Sequencers.Count)
            Services.WorkspaceService.ActiveSequencerIndex = Services.ProjectService.Sequencers.Count - 1;

        if (Services.WorkspaceService.ActiveSequencerIndex < 0 && Services.ProjectService.Sequencers.Count > 0)
            Services.WorkspaceService.ActiveSequencerIndex = 0;

        if (!Services.PlaybackService.IsPlaying) Services.PlaybackService.ApplyCurrentPose();

        ImGui.EndTabBar();
    }

    private void DrawInspectorToggle(float height)
    {
        var icon = inspectorVisible ? FontAwesomeIcon.AngleRight : FontAwesomeIcon.AngleLeft;
        var size = new Vector2(15f, height);

        if (ImGuiComponents.IconButton(icon, size))
            inspectorVisible = !inspectorVisible;
    }
}