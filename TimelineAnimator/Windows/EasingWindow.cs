using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System.Numerics;
using TimelineAnimator.Data;

namespace TimelineAnimator.Windows;

public class EasingWindow : Window, IDisposable
{
    private readonly List<ITrackKeyframe> currentKeyframes = new();

    private Vector2 p1 = new(0.25f, 0.25f);
    private Vector2 p2 = new(0.75f, 0.75f);

    private readonly string[] presetNames = { "Linear", "Ease In", "Ease Out", "Ease In Out", "Custom" };
    private const int PRESET_LINEAR = 0;
    private const int PRESET_EASE_IN = 1;
    private const int PRESET_EASE_OUT = 2;
    private const int PRESET_EASE_IN_OUT = 3;
    private const int PRESET_CUSTOM = 4;
    private int selectedPreset = PRESET_LINEAR;

    private readonly Vector2[] presetValuesP1 =
    {
        new(0.25f, 0.25f), // Linear
        new(0.42f, 0.0f), // Ease In
        new(0.0f, 0.0f), // Ease Out
        new(0.42f, 0.0f) // Ease In Out
    };

    private readonly Vector2[] presetValuesP2 =
    {
        new(0.75f, 0.75f),
        new(1.0f, 1.0f),
        new(0.58f, 1.0f),
        new(0.58f, 1.0f)
    };

    private int draggingPoint = -1; // -1 = none, 0 = P1, 1 = P2

    public EasingWindow() : base("Easing Editor###EasingWindow")
    {
        Size = new Vector2(300, 375);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose()
    {
    }

    public void SetKeyframes(List<ITrackKeyframe>? keyframes)
    {
        currentKeyframes.Clear();
        if (keyframes != null && keyframes.Any())
        {
            currentKeyframes.AddRange(keyframes);
            var firstKeyframe = currentKeyframes.First();
            p1 = firstKeyframe.P1;
            p2 = firstKeyframe.P2;
            UpdateSelectedPreset();
        }
        else
        {
            p1 = presetValuesP1[PRESET_LINEAR];
            p2 = presetValuesP2[PRESET_LINEAR];
            selectedPreset = PRESET_LINEAR;
        }
    }

    private void UpdateSelectedPreset()
    {
        for (int i = 0; i < PRESET_CUSTOM; i++)
        {
            if (Vector2.Distance(p1, presetValuesP1[i]) < 0.001f &&
                Vector2.Distance(p2, presetValuesP2[i]) < 0.001f)
            {
                selectedPreset = i;
                return;
            }
        }

        selectedPreset = PRESET_CUSTOM;
    }

    public override void Draw()
    {
        if (!currentKeyframes.Any())
        {
            ImGui.TextDisabled("No keyframe selected.");
            ImGui.TextDisabled("In the main window, right click a keyframe");
            ImGui.TextDisabled("and select 'Edit Easing' to begin.");
            p1 = presetValuesP1[PRESET_LINEAR];
            p2 = presetValuesP2[PRESET_LINEAR];
            selectedPreset = PRESET_LINEAR;
            return;
        }

        if (currentKeyframes.Count == 1)
            ImGui.Text($"Editing Keyframe: {currentKeyframes.First().Frame}");
        else
            ImGui.Text($"Editing {currentKeyframes.Count} keyframes.");

        ImGui.PushItemWidth(-1);
        if (ImGui.Combo("Preset", ref selectedPreset, presetNames, presetNames.Length))
        {
            if (selectedPreset != PRESET_CUSTOM)
            {
                p1 = presetValuesP1[selectedPreset];
                p2 = presetValuesP2[selectedPreset];

                foreach (var keyframe in currentKeyframes)
                {
                    keyframe.P1 = p1;
                    keyframe.P2 = p2;
                }
            }
        }

        ImGui.PopItemWidth();
        ImGui.Separator();

        ImGui.BeginChild("EasingCanvas", new Vector2(-1, -1), true,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        var canvasPos = ImGui.GetCursorScreenPos();
        var canvasSize = ImGui.GetContentRegionAvail();

        ImGui.SetCursorScreenPos(canvasPos);
        ImGui.InvisibleButton("EasingCanvasButton", canvasSize);

        bool isCanvasActive = ImGui.IsItemActive();
        bool isCanvasClicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);

        var drawList = ImGui.GetWindowDrawList();
        var io = ImGui.GetIO();
        var mousePos = io.MousePos;

        float margin = canvasSize.X * 0.05f;
        var plotMin = canvasPos + new Vector2(margin, margin);
        var plotMax = canvasPos + canvasSize - new Vector2(margin, margin);
        var plotSize = plotMax - plotMin;

        Vector2 MapNormalizedToScreen(Vector2 norm)
        {
            return new Vector2(
                plotMin.X + norm.X * plotSize.X,
                plotMin.Y + (1.0f - norm.Y) * plotSize.Y
            );
        }

        Vector2 MapScreenToNormalized(Vector2 screen)
        {
            var norm = (screen - plotMin) / plotSize;
            norm.Y = 1.0f - norm.Y;
            return norm;
        }

        uint bgCol = ImGui.GetColorU32(ImGuiCol.FrameBg);
        uint gridCol = ImGui.GetColorU32(ImGuiCol.TextDisabled, 0.5f);
        drawList.AddRectFilled(plotMin, plotMax, bgCol);
        drawList.AddRect(plotMin, plotMax, gridCol);

        for (int i = 1; i <= 3; i++)
        {
            float x = plotMin.X + (plotSize.X / 4 * i);
            float y = plotMin.Y + (plotSize.Y / 4 * i);
            drawList.AddLine(new Vector2(x, plotMin.Y), new Vector2(x, plotMax.Y), gridCol);
            drawList.AddLine(new Vector2(plotMin.X, y), new Vector2(plotMax.X, y), gridCol);
        }

        var p0_screen = MapNormalizedToScreen(new Vector2(0, 0));
        var p1_screen = MapNormalizedToScreen(p1);
        var p2_screen = MapNormalizedToScreen(p2);
        var p3_screen = MapNormalizedToScreen(new Vector2(1, 1));

        float grabRadius = 10.0f;
        if (isCanvasClicked)
        {
            if (Vector2.Distance(mousePos, p1_screen) < grabRadius)
            {
                draggingPoint = 0;
            }
            else if (Vector2.Distance(mousePos, p2_screen) < grabRadius)
            {
                draggingPoint = 1;
            }
            else
            {
                draggingPoint = -1;
            }
        }

        if (isCanvasActive && draggingPoint != -1 && io.MouseDown[(int)ImGuiMouseButton.Left])
        {
            var newNormPos = MapScreenToNormalized(mousePos);
            newNormPos.X = Math.Clamp(newNormPos.X, 0.0f, 1.0f);
            if (draggingPoint == 0)
            {
                p1 = newNormPos;
                foreach (var keyframe in currentKeyframes)
                    keyframe.P1 = newNormPos;
            }

            if (draggingPoint == 1)
            {
                p2 = newNormPos;
                foreach (var keyframe in currentKeyframes)
                    keyframe.P2 = newNormPos;
            }

            UpdateSelectedPreset();
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            draggingPoint = -1;
        }

        uint handleCol = ImGui.GetColorU32(ImGuiCol.Button);
        drawList.AddLine(p0_screen, p1_screen, handleCol, 2.0f);
        drawList.AddLine(p3_screen, p2_screen, handleCol, 2.0f);

        uint curveCol = ImGui.GetColorU32(ImGuiCol.PlotLines);
        drawList.AddBezierCubic(p0_screen, p1_screen, p2_screen, p3_screen, curveCol, 3.0f, 32);

        uint p03Col = ImGui.GetColorU32(ImGuiCol.PlotLines);
        uint p12Col = ImGui.GetColorU32(ImGuiCol.ButtonHovered);

        drawList.AddCircleFilled(p0_screen, 6.0f, p03Col);
        drawList.AddCircleFilled(p3_screen, 6.0f, p03Col);

        drawList.AddCircleFilled(p1_screen, 8.0f, p12Col);
        drawList.AddCircleFilled(p2_screen, 8.0f, p12Col);

        ImGui.EndChild();
    }
}