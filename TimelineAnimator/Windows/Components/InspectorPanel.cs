using Dalamud.Bindings.ImGui;
using System.Numerics;
using TimelineAnimator.Data;
using TimelineAnimator.Sequencers;

namespace TimelineAnimator.Windows.Components;

public static class InspectorPanel
{
    private static readonly string[] keyframeShapeNames = Enum.GetNames(typeof(KeyframeShape));

    public static void Draw(Plugin plugin)
    {
        bool modifier = Services.InputManager.IsModifierHeld;
        ImGui.Text("Inspector");
        ImGui.Separator();
        ImGui.Spacing();

        var activeSequencer = Services.WorkspaceService.GetActiveSequencer() as SequencerBase;

        if (activeSequencer == null)
        {
            ImGui.Text("No actor selected.");
            ImGui.TextWrapped("Please select bones in ktisis and press the plus icon to add an actor and bone track.");
            plugin.UpdateEasingUiKeyframes(null);
            return;
        }

        activeSequencer.DrawInspector(Services.PlaybackService.CurrentFrame);
        plugin.UpdateEasingUiKeyframes(activeSequencer.GetSelectedKeyframes());

        int selectedCount = activeSequencer.GetSelectedKeyframeCount();
        bool hasSelection = selectedCount > 0;

        if (!hasSelection)
        {
            DrawGlobalSettings(plugin, activeSequencer, modifier);
            return;
        }

        if (selectedCount > 1)
        {
            ImGui.Text($"{selectedCount} Keyframes Selected");
            ImGui.Separator();

            if (!modifier) ImGui.BeginDisabled();
            if (ImGui.Button("Delete Selection"))
                activeSequencer.DeleteSelectedKeyframes();
            if (!modifier)
            {
                ImGui.EndDisabled();
                ShowTooltip($"Hold {Services.Configuration.ModifierKey} to delete", disabled: true);
            }
        }
        else
        {
            DrawSingleKeyframeSettings(activeSequencer, modifier);
        }

        DrawKeyframeStyleSettings(activeSequencer);
    }

    private static void DrawGlobalSettings(Plugin plugin, SequencerBase activeSequencer, bool modifier)
    {
        ImGui.Text("Global Settings");
        int frameMax = Services.ProjectService.GetGlobalMaxFrame();
        int minFrames = Services.ProjectService.GetGlobalMinFrame() + 1;

        if (ImGui.DragInt("Max Frames", ref frameMax, 1.0f, minFrames, 10000))
            Services.ProjectService.SetMaxFrameForAll(frameMax);

        ImGui.Spacing();

        if (ImGui.Button("Save"))
        {
            Services.FileDialogManager.SaveFileDialog("Save Animation", ".xivanim", "animation.xivanim", "xivanim",
                (success, path) =>
                {
                    if (success) Services.ProjectService.SaveAnimation(path, activeSequencer);
                    ;
                }, plugin.PluginConfigDirectory);
        }

        ImGui.SameLine();
        if (ImGui.Button("Load"))
        {
            Services.FileDialogManager.OpenFileDialog("Load Animation", ".xivanim", (success, paths) =>
            {
                if (success && paths.Count > 0) Services.ProjectService.LoadAnimation(paths[0], activeSequencer);
            }, 1, plugin.PluginConfigDirectory);
        }

        int selectedTrack = Services.WorkspaceService.SharedSelectedEntry;
        if (selectedTrack >= 0 && selectedTrack < activeSequencer.Sequence.Tracks.Count)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var track = activeSequencer.Sequence.GetTrack(selectedTrack);
            if (track != null)
            {
                ImGui.Text("Track Properties");
                string label = activeSequencer is ActorSequencer ? "Bone" : "Track";
                ImGui.Text($"{label}: {track.DisplayName}");
                ImGui.Spacing();

                if (activeSequencer is ActorSequencer)
                {
                    if (!modifier) ImGui.BeginDisabled();
                    if (ImGui.Button("Delete Track"))
                    {
                        activeSequencer.RemoveTrackSafely(selectedTrack);
                        Services.WorkspaceService.SharedSelectedEntry = -1;
                    }

                    if (!modifier)
                    {
                        ImGui.EndDisabled();
                        ShowTooltip($"Hold {Services.Configuration.ModifierKey} to delete track", disabled: true);
                    }
                }
            }
        }
    }

    private static void DrawSingleKeyframeSettings(SequencerBase activeSequencer, bool modifier)
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

        ImGui.Spacing();

        if (!modifier) ImGui.BeginDisabled();
        if (ImGui.Button("Delete Keyframe"))
        {
            activeSequencer.DeleteSelectedKeyframes();
        }

        if (!modifier)
        {
            ImGui.EndDisabled();
            ShowTooltip($"Hold {Services.Configuration.ModifierKey} to delete", disabled: true);
        }
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
            if (ImGui.Combo("Shape", ref currentShape, keyframeShapeNames, keyframeShapeNames.Length))
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

    private static void ShowTooltip(string text, bool disabled = false)
    {
        if (!Services.Configuration.ShowTooltips) return;
        if (ImGui.IsItemHovered(disabled ? ImGuiHoveredFlags.AllowWhenDisabled : ImGuiHoveredFlags.None))
            ImGui.SetTooltip(text);
    }
}