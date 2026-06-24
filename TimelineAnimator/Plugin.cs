using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using TimelineAnimator.Data;
using TimelineAnimator.Windows;

namespace TimelineAnimator;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private bool wasInGpose = false;
    private bool isInitialized = false;

    private const string CommandName = "/animator";
    private const string PosingPluginIPCName = "Ktisis"; // later need to handle properly for multiple

    public string PluginConfigDirectory => pluginInterface.GetPluginConfigDirectory();
    public readonly WindowSystem WindowSystem = new("TimelineAnimator");

    private ConfigWindow ConfigWindow { get; set; }
    private MainWindow MainWindow { get; set; }
    private TutorialWindow TutorialWindow { get; set; }
    private EasingWindow EasingWindow { get; set; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        this.pluginInterface.Create<Services>();

        if (this.pluginInterface.InstalledPlugins.Any(p => p is { InternalName: PosingPluginIPCName, IsLoaded: true }))
        {
            InitializePlugin();
        }
        else
        {
            this.pluginInterface.ActivePluginsChanged += OnActivePluginsChanged;
        }
    }

    private void OnActivePluginsChanged(IActivePluginsChangedEventArgs args)
    {
        if (args.Kind == PluginListInvalidationKind.Loaded && args.AffectedInternalNames.Contains(PosingPluginIPCName))
        {
            InitializePlugin();
            pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;
        }
    }

    private void InitializePlugin()
    {
        if (isInitialized) return;

        try
        {
            Services.Initialize(this);

            ConfigWindow = new ConfigWindow(this);
            MainWindow = new MainWindow(this)
            {
                TitleBarButtons =
                [
                    new()
                    {
                        Icon = FontAwesomeIcon.Cog,
                        ShowTooltip = () => ImGui.SetTooltip("Toggle Settings Window"),
                        Click = _ => ToggleConfigUi()
                    },
                    new()
                    {
                        Icon = FontAwesomeIcon.QuestionCircle,
                        ShowTooltip = () => ImGui.SetTooltip("Show Tutorial"),
                        Click = _ => ToggleTutorialWindow()
                    }
                ]
            };
            TutorialWindow = new TutorialWindow();
            EasingWindow = new EasingWindow();

            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(TutorialWindow);
            WindowSystem.AddWindow(EasingWindow);
            pluginInterface.UiBuilder.DisableGposeUiHide = true;

            Services.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Toggles the Main Window."
            });

            pluginInterface.UiBuilder.Draw += DrawUI;
            pluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
            pluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

            Services.Framework.Update += OnFrameworkUpdate;
            Services.WorkspaceService.EditEasingRequested += OpenEasingUiForKeyframes;

            Services.Log.Information($"Found {PosingPluginIPCName}");
            isInitialized = true;
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, "Failed to initialize");
        }
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
        Services.FileDialogManager.Draw();
    }

    public void Dispose()
    {
        pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;

        if (!isInitialized) return;

        Services.Framework.Update -= OnFrameworkUpdate;
        pluginInterface.UiBuilder.Draw -= DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        pluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        Services.WorkspaceService.EditEasingRequested -= OpenEasingUiForKeyframes;

        ConfigWindow?.Dispose();
        MainWindow?.Dispose();

        EasingWindow?.Dispose();
        TutorialWindow?.Dispose();

        Services.CommandManager.RemoveHandler(CommandName);
        Services.Dispose();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (Services.PlaybackService == null || Services.ProjectService == null) return;

        bool currentlyInGpose = Services.ClientState.IsGPosing;
        if (currentlyInGpose != wasInGpose)
        {
            if (currentlyInGpose)
                OnEnterGpose();
            else
                OnLeaveGpose();

            wasInGpose = currentlyInGpose;
        }

        Services.PlaybackService.Update((float)framework.UpdateDelta.TotalSeconds);

        if (Services.InputManager.IsTogglePlaybackPressed())
        {
            Services.PlaybackService.TogglePlay();
        }

        if (Services.InputManager.IsAddItemPressed())
        {
            _ = Services.IntegrationService.FetchSelectedEntitiesAsync();
        }
    }

    private void OnEnterGpose()
    {
        if (Services.Configuration.OpenInGpose)
        {
            MainWindow.IsOpen = true;
        }

        if (Services.Configuration.ShowTutorial)
        {
            TutorialWindow.IsOpen = true;
        }
    }

    private void OnLeaveGpose()
    {
        MainWindow.IsOpen = false;
        TutorialWindow.IsOpen = false;
        EasingWindow.IsOpen = false;
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    public void ToggleConfigUi() => ConfigWindow?.Toggle();
    public void ToggleMainUi() => MainWindow?.Toggle();
    public void ToggleTutorialWindow() => TutorialWindow?.Toggle();

    public void OpenEasingUiForKeyframes(List<ITrackKeyframe>? keyframes)
    {
        EasingWindow?.SetKeyframes(keyframes);
        if (EasingWindow != null) EasingWindow.IsOpen = true;
    }

    public void UpdateEasingUiKeyframes(List<ITrackKeyframe>? keyframes)
    {
        EasingWindow?.SetKeyframes(keyframes);
    }
}