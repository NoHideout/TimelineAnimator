using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace TimelineAnimator.Interop
{
    public class EorzeaCamcorderIpc
    {
        public bool IsAvailable { get; private set; } = true;

        private readonly ICallGateSubscriber<(int, int)>? _apiVersion;
        private readonly ICallGateSubscriber<string, object>? _startRecording;
        private readonly ICallGateSubscriber<object>? _stopRecording;
        private readonly ICallGateSubscriber<bool>? _isRecording;
        private readonly ICallGateSubscriber<string?, int?, object>? _saveReplay;
        private readonly ICallGateSubscriber<string, object>? _startReplay;
        private readonly ICallGateSubscriber<object>? _stopReplay;
        private readonly ICallGateSubscriber<bool>? _isReplayBufferRunning;

        public EorzeaCamcorderIpc()
        {
            try
            {
                IDalamudPluginInterface pluginInterface = Services.PluginInterface;

                _apiVersion = pluginInterface.GetIpcSubscriber<(int, int)>("EorzeaCamcorder.ApiVersion");
                _startRecording = pluginInterface.GetIpcSubscriber<string, object>("EorzeaCamcorder.StartRecording");
                _stopRecording = pluginInterface.GetIpcSubscriber<object>("EorzeaCamcorder.StopRecording");
                _isRecording = pluginInterface.GetIpcSubscriber<bool>("EorzeaCamcorder.IsRecording");
            }
            catch (Exception e)
            {
                Services.Log.Error(e, "EorzeaCamcorder IPC init error.");
                IsAvailable = false;
            }
        }

        public (int, int) GetApiVersion()
        {
            if (!IsAvailable || _apiVersion == null) return (0, 0);
            try
            {
                return _apiVersion.InvokeFunc();
            }
            catch (Exception e)
            {
                Services.Log.Error(e, "Error calling EorzeaCamcorder.ApiVersion");
                return (0, 0);
            }
        }

        public void StartRecording(string customPath)
        {
            if (!IsAvailable || _startRecording == null) return;
            try
            {
                _startRecording.InvokeAction(customPath);
            }
            catch (Exception e)
            {
                Services.Log.Error(e, "Error calling EorzeaCamcorder.StartRecording");
            }
        }

        public void StopRecording()
        {
            if (!IsAvailable || _stopRecording == null) return;
            try
            {
                _stopRecording.InvokeAction();
            }
            catch (Exception e)
            {
                Services.Log.Error(e, "Error calling EorzeaCamcorder.StopRecording");
            }
        }

        public bool IsRecording()
        {
            if (!IsAvailable || _isRecording == null) return false;
            try
            {
                return _isRecording.InvokeFunc();
            }
            catch (Exception e)
            {
                Services.Log.Error(e, "Error calling EorzeaCamcorder.IsRecording");
                return false;
            }
        }
    }
}