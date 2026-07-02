using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using TimelineAnimator.Windows;

namespace TimelineAnimator
{
    public sealed class Plugin : IDalamudPlugin
    {
        private readonly IDalamudPluginInterface pluginInterface;
        private bool wasInGpose = false;
        private bool isInitialized = false;

        private const string CommandName = "/animator";
        private const string PosingPluginIpcName = "Ktisis"; // later need to handle properly for multiple

        public string PluginConfigDirectory => pluginInterface.GetPluginConfigDirectory();
        private readonly WindowSystem windowSystem = new("TimelineAnimator");

        private ConfigWindow ConfigWindow { get; set; }
        private MainWindow MainWindow { get; set; }
        private TutorialWindow TutorialWindow { get; set; }
        private GraphEditorWindow GraphEditorWindow { get; set; }

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
            this.pluginInterface.Create<Services>();

            if (this.pluginInterface.InstalledPlugins.Any(p => p is { InternalName: PosingPluginIpcName, IsLoaded: true }))
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
            if (args.Kind == PluginListInvalidationKind.Loaded && args.AffectedInternalNames.Contains(PosingPluginIpcName))
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
                MainWindow = new MainWindow()
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
                GraphEditorWindow = new GraphEditorWindow();

                windowSystem.AddWindow(ConfigWindow);
                windowSystem.AddWindow(MainWindow);
                windowSystem.AddWindow(TutorialWindow);
                windowSystem.AddWindow(GraphEditorWindow);
                pluginInterface.UiBuilder.DisableGposeUiHide = true;

                Services.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
                {
                    HelpMessage = "Toggles the Main Window."
                });

                pluginInterface.UiBuilder.Draw += DrawUi;
                pluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
                pluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

                Services.Framework.Update += OnFrameworkUpdate;
                Services.WorkspaceService.EditGraphRequested += _ => OpenGraphEditor();

                Services.Log.Information($"Found {PosingPluginIpcName}");
                isInitialized = true;
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Failed to initialize");
            }
        }

        private void DrawUi()
        {
            windowSystem.Draw();
            Services.FileDialogManager.Draw();
        }

        public void Dispose()
        {
            pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;

            if (!isInitialized) return;

            Services.Framework.Update -= OnFrameworkUpdate;
            pluginInterface.UiBuilder.Draw -= DrawUi;
            pluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
            pluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

            windowSystem.RemoveAllWindows();

            Services.WorkspaceService.EditGraphRequested -= _ => OpenGraphEditor();
            
            ConfigWindow?.Dispose();
            MainWindow?.Dispose();

            GraphEditorWindow?.Dispose();
            TutorialWindow?.Dispose();

            Services.CommandManager.RemoveHandler(CommandName);
            Services.Dispose();
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
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
            Services.AutosaveService.Update((float)framework.UpdateDelta.TotalSeconds);

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
            GraphEditorWindow.IsOpen = false;
        }

        private void OnCommand(string command, string args)
        {
            MainWindow.Toggle();
        }

        private void ToggleConfigUi() => ConfigWindow?.Toggle();
        private void ToggleMainUi() => MainWindow?.Toggle();
        public void ToggleTutorialWindow() => TutorialWindow?.Toggle();

        private void OpenGraphEditor()
        {
            if (GraphEditorWindow != null) GraphEditorWindow.IsOpen = true;
        }
    }
}