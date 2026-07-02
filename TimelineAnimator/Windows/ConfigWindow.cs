using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Windowing;

namespace TimelineAnimator.Windows
{
    public class ConfigWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private string? bindingActionName = null;

        public ConfigWindow(Plugin plugin) : base("Timeline Animator Settings")
        {
            Size = new Vector2(232, 160);
            SizeCondition = ImGuiCond.FirstUseEver;

            this.plugin = plugin;
        }

        public void Dispose() { }

        public override void Draw()
        {
            ImGui.BeginTabBar("ConfigTabs");
            if (ImGui.BeginTabItem("General Settings"))
            {
                var openInGpose = Services.Configuration.OpenInGpose;
                if (ImGui.Checkbox("Open automatically in GPose", ref openInGpose))
                {
                    Services.Configuration.OpenInGpose = openInGpose;
                    Services.Configuration.Save();
                }

                var showTooltips = Services.Configuration.ShowTooltips;
                if (ImGui.Checkbox("Show tooltips", ref showTooltips))
                {
                    Services.Configuration.ShowTooltips = showTooltips;
                    Services.Configuration.Save();
                }

                ImGui.Spacing();
                var showTutorial = Services.Configuration.ShowTutorial;
                if (ImGui.Checkbox("Show tutorial on entering GPose", ref showTutorial))
                {
                    Services.Configuration.ShowTutorial = showTutorial;
                    Services.Configuration.Save();
                }

                if (ImGui.Button("Show Tutorial"))
                {
                    plugin.ToggleTutorialWindow();
                }

                int currentFps = (int)Services.Configuration.PlaybackFramesPerSecond;
                if (ImGui.BeginCombo("Playback Framerate", $"{currentFps} FPS"))
                {
                    int[] fpsOptions = { 24, 30, 60, 120 };
                    foreach (int fps in fpsOptions)
                    {
                        if (ImGui.Selectable($"{fps} FPS", currentFps == fps))
                        {
                            Services.Configuration.PlaybackFramesPerSecond = fps;
                            Services.Configuration.Save();
                        }
                    }

                    ImGui.EndCombo();
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Autosave"))
            {
                var enableAutosave = Services.Configuration.EnableAutosave;
                if (ImGui.Checkbox("Enable Autosave", ref enableAutosave))
                {
                    Services.Configuration.EnableAutosave = enableAutosave;
                    Services.Configuration.Save();
                }

                if (enableAutosave)
                {
                    ImGui.Indent();
                    ImGui.Spacing();

                    var interval = Services.Configuration.AutosaveIntervalMinutes;
                    if (ImGui.SliderInt("Autosave interval", ref interval, 1, 60))
                    {
                        Services.Configuration.AutosaveIntervalMinutes = interval;
                        Services.Configuration.Save();
                    }
                    
                    var maxBackups = Services.Configuration.MaxAutosaveBackups;
                    if (ImGui.SliderInt("Max Backups", ref maxBackups, 1, 20))
                    {
                        Services.Configuration.MaxAutosaveBackups = maxBackups;
                        Services.Configuration.Save();
                    }
                    
                    ImGui.Spacing();
                    ImGui.Text("Custom Autosave Directory:");
                    
                    var dir = Services.Configuration.AutosaveDirectory;
                    string displayDir = string.IsNullOrWhiteSpace(dir) ? Services.PluginInterface.GetPluginConfigDirectory() : dir;

                    if (ImGui.InputText("##AutosaveDir", ref displayDir, 512))
                    {
                        Services.Configuration.AutosaveDirectory = displayDir == Services.PluginInterface.GetPluginConfigDirectory() ? string.Empty : displayDir;
                        Services.Configuration.Save();
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Leave blank or set to the default plugin config directory.");
                    }

                    ImGui.Unindent();
                }
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Keybindings"))
            {
                ImGui.TextDisabled("Click a button to rebind. Press ESC to clear");
                ImGui.Spacing();
                DrawKeybind("Modifier", Services.Configuration.ModifierKey, k => Services.Configuration.ModifierKey = k);
                DrawKeybind("Toggle Playback", Services.Configuration.TogglePlaybackKey, k => Services.Configuration.TogglePlaybackKey = k);
                DrawKeybind("Add Item", Services.Configuration.AddItemKey, k => Services.Configuration.AddItemKey = k);

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        private void DrawKeybind(string label, VirtualKey currentKey, Action<VirtualKey> setter)
        {
            ImGui.Text(label);
            ImGui.SameLine();

            if (bindingActionName == label)
            {
                ImGui.Button("Press any key...");

                var pressedKey = Services.KeyState.GetValidVirtualKeys().FirstOrDefault(k => Services.KeyState[k]);
                if (pressedKey != VirtualKey.NO_KEY)
                {
                    if (pressedKey == VirtualKey.ESCAPE)
                    {
                        setter(VirtualKey.NO_KEY);
                    }
                    else
                    {
                        setter(pressedKey);
                    }

                    Services.Configuration.Save();
                    bindingActionName = null;
                }
            }
            else
            {
                string keyName = currentKey == VirtualKey.NO_KEY ? "None" : currentKey.ToString();
                if (ImGui.Button($"{keyName}###{label}"))
                {
                    bindingActionName = label;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Click to rebind");
                }
            }
        }
    }
}