using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using System.Numerics;
using System.Threading.Tasks;

namespace TimelineAnimator.Interop;

public class KtisisIpc
{
    private readonly IPluginLog log = Services.Log;
    public bool IsAvailable { get; private set; } = true;

    private readonly ICallGateSubscriber<(int, int)>? _getVersion;
    private readonly ICallGateSubscriber<bool>? _isPosing;

    private readonly ICallGateSubscriber<Task<Dictionary<int, HashSet<string>>>>? _getSelectedBones;

    private ICallGateSubscriber<uint, Dictionary<string, Matrix4x4>, Task<bool>>? _applyAbsolutePoses;

    public KtisisIpc()
    {
        try
        {
            IDalamudPluginInterface pluginInterface = Services.PluginInterface;

            _getVersion = pluginInterface.GetIpcSubscriber<(int, int)>("Ktisis.ApiVersion");
            _isPosing = pluginInterface.GetIpcSubscriber<bool>("Ktisis.IsPosing");

            _getSelectedBones = pluginInterface.GetIpcSubscriber<Task<Dictionary<int, HashSet<string>>>>("Ktisis.SelectedBones");
            _applyAbsolutePoses = pluginInterface.GetIpcSubscriber<uint, Dictionary<string, Matrix4x4>, Task<bool>>("Ktisis.ApplyAbsolutePoses");
        }
        catch (Exception e)
        {
            log.Error(e, "Ktisis IPC init error.");
            IsAvailable = false;
        }
    }

    public (int, int) GetVersion()
    {
        if (!IsAvailable || _getVersion == null) return (0, 0);
        try
        {
            return _getVersion.InvokeFunc();
        }
        catch (Exception e)
        {
            log.Error(e, "Error calling Ktisis.GetVersion");
            return (0, 0);
        }
    }

    //Todo could be removed
    public bool IsPosing()
    {
        if (!IsAvailable || _isPosing == null) return false;
        try
        {
            return _isPosing.InvokeFunc();
        }
        catch (Exception e)
        {
            log.Error(e, "Error calling Ktisis.IsPosing");
            return false;
        }
    }

    public async Task<Dictionary<int, HashSet<string>>> GetSelectedBonesAsync()
    {
        if (!IsAvailable || _getSelectedBones == null) return new Dictionary<int, HashSet<string>>();
        try
        {
            return await _getSelectedBones.InvokeFunc();
        }
        catch (Exception e)
        {
            log.Error(e, "Error calling Ktisis.GetSelectedBonesAsync");
            return new Dictionary<int, HashSet<string>>();
        }
    }

    public async Task SendAnimationFrame(uint gameObjectIndex, Dictionary<string, Matrix4x4> localAnimationPoses)
    {
        if (_applyAbsolutePoses != null)
        {
            try
            {
                await _applyAbsolutePoses.InvokeFunc(gameObjectIndex, localAnimationPoses);
            }
            catch (Exception ex)
            {
                log.Error($"Failed to send frame to Ktisis");
            }
        }
    }
}