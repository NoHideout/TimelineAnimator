using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;

namespace TimelineAnimator.Windows
{
    public class TutorialWindow : Window, IDisposable
    {
        public TutorialWindow() : base("Welcome to Timeline Animator!")
        {
            Size = new Vector2(460, 420);
            SizeCondition = ImGuiCond.Appearing;
        }

        public void Dispose()
        {
        }

        public override void Draw()
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
            {
                ImGui.SetWindowFontScale(1.25f);
                ImGui.TextWrapped("Initial Release");
                ImGui.SetWindowFontScale(1.0f);
            }

            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudYellow))
            {
                ImGui.TextWrapped(
                    "This plugin is still in development. Expect potential bugs or crashes. Project file structures may change in future updates, which could break older save files.");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextWrapped("Quick Start Guide:");
            ImGui.Spacing();

            using var bgColor = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.1f, 0.1f, 0.1f, 0.3f));
            using var rounding = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 5f);
            using var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(10f, 10f));
            using (var child = ImRaii.Child("QuickGuideFrame", new Vector2(0, 175), true))
            {
                if (child)
                {
                    ImGui.TextWrapped("1. Select the bones you want to animate in Ktisis.");
                    ImGui.TextWrapped("2. Use the '+' icon in the toolbar to add the bones to the timeline.");
                    ImGui.TextWrapped("3. Move the play head (the vertical red line) to a frame.");
                    ImGui.TextWrapped("4. Pose the bones, reselect them and click '+' again to set a keyframe.");
                    using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudGrey)) ImGui.TextWrapped("   (clicking '+' will also update existing keyframes).");
                    ImGui.Spacing();
                    ImGui.TextWrapped("5. For Cameras: Add a camera track, enable the Free Camera in the toolbar, and keyframe your views.");
                }
            }

            ImGui.Spacing();
            ImGui.TextWrapped("Right click a property track to access the Graph Editor for custom easing.");
            ImGui.Spacing();
            
            if (ImGui.Button("Don't show this again"))
            {
                Services.Configuration.ShowTutorial = false;
                Services.Configuration.Save();
                IsOpen = false;
            }

            ImGui.SameLine();
            if (ImGui.Button("Close"))
            {
                IsOpen = false;
            }
        }
    }
}