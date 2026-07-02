using System;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;

namespace TimelineAnimator
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public bool OpenInGpose { get; set; } = true;
        public bool ShowTutorial { get; set; } = true;
        public bool ShowTooltips { get; set; } = true;
        public double PlaybackFramesPerSecond { get; set; } = 30.0;
        
        public VirtualKey TogglePlaybackKey { get; set; } = VirtualKey.SPACE;
        public VirtualKey AddItemKey { get; set; } = VirtualKey.A;
        public VirtualKey ModifierKey { get; set; } = VirtualKey.CONTROL;
        
        public bool EnableAutosave { get; set; } = false;
        public int AutosaveIntervalMinutes { get; set; } = 5;
        public int MaxAutosaveBackups { get; set; } = 3;
        public string AutosaveDirectory { get; set; } = string.Empty;

        public void Save()
        {
            Services.PluginInterface.SavePluginConfig(this);
        }
    }
}