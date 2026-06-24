using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.ImGuiFileDialog;
using TimelineAnimator.Core;
using TimelineAnimator.Interop;

namespace TimelineAnimator
{
    public class Services
    {
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
        [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
        [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
        public static ProjectService ProjectService { get; private set; } = null!;
        public static PlaybackService PlaybackService { get; private set; } = null!;
        public static IntegrationService IntegrationService { get; private set; } = null!;
        public static CameraService CameraService { get; private set; } = null!;
        internal static FileDialogManager FileDialogManager { get; private set; } = new();
        internal static Configuration Configuration { get; private set; } = null!;
        internal static KtisisIpc KtisisIpc { get; private set; } = null!;
        internal static InputManager InputManager { get; private set; } = null!;
        public static WorkspaceService WorkspaceService { get; private set; } = null!;

        internal static void Initialize(Plugin plugin)
        {
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            KtisisIpc = new KtisisIpc();
            InputManager = new InputManager();
            CameraService = new CameraService();
            ProjectService = new ProjectService();
            PlaybackService = new PlaybackService();
            IntegrationService = new IntegrationService();
            WorkspaceService = new WorkspaceService();
        }

        internal static void Dispose()
        {
            CameraService?.Dispose();
        }
    }
}