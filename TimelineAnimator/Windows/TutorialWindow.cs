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
            Size = new Vector2(450, 380);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void Dispose()
        {
        }

        public override void Draw()
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
            {
                ImGui.SetWindowFontScale(1.5f);
                ImGui.TextWrapped("Warning: this Plugin is currently in an early experimental stage.");
                ImGui.SetWindowFontScale(1.0f);
            }

            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudYellow))
            {
                ImGui.TextWrapped(
                    "You may encounter Bugs, Crashes or unintended Behavior. Some features are heavily under work in progress. Save files will still change and will break through updates");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextWrapped("This is just a quick start guide!");
            ImGui.Spacing();

            using var bgColor = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.1f, 0.1f, 0.1f, 0.3f));
            using var rounding = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 5f);
            using var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(10f, 10f));
            using (var child = ImRaii.Child("TutorialStepsFrame", new Vector2(0, 175), true))
            {
                if (child)
                {
                    ImGui.TextWrapped("1. Select the bones you want to animate in Ktisis.");
                    ImGui.TextWrapped("2. Click the 'Add' button to create tracks for them, while they are selected.");

                    using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudGrey))
                        ImGui.TextWrapped("   (This will also create new timelines for actors).");

                    ImGui.TextWrapped("3. Move the play head (the vertical red line) to a frame.");
                    ImGui.TextWrapped("4. Pose the bones and reselect all of them when you are ready to finish up the animation.");
                    ImGui.TextWrapped("5. Click 'Add Selected Bones' again to create a new keyframe.");

                    using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudGrey)) ImGui.TextWrapped("   (This will update an existing keyframe if you're on one).");
                
                    ImGui.TextWrapped("6. The Camera behaves similar. Make sure you have the free-cam setting in the inspector toggled on to keyframe or view the animation.");
                }
            }

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