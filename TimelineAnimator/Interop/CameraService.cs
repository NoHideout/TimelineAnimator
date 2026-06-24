using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using System.Numerics;
using System.Runtime.InteropServices;
using RenderCamera = FFXIVClientStructs.FFXIV.Client.Graphics.Render.Camera;
using SceneCamera = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera;
using GameCamera = FFXIVClientStructs.FFXIV.Client.Game.Camera;

namespace TimelineAnimator.Interop
{
    [Flags]
    public enum MouseButton
    {
        None = 0,
        Left = 1,
        Middle = 2,
        Right = 4,
        Mouse4 = 8,
        Mouse5 = 16
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseDeviceData
    {
        public int PosX;
        public int PosY;
        public int ScrollDelta;
        public MouseButton Pressed;
        public MouseButton Clicked;
        public ulong Unk1;
        public int DeltaX;
        public int DeltaY;
        public uint Unk2;
        public bool IsFocused;

        public bool IsButtonHeld(MouseButton button) => (this.Pressed & button) != 0;
    }

    //Todo decide if this shouldnt be cam interop and seperate service
    public unsafe class CameraService : IDisposable
    {
        [Signature("48 89 5C 24 ?? 57 48 81 EC ?? ?? ?? ?? F6 81 ?? ?? ?? ?? ?? 48 8B D9 48 89 B4 24 ?? ?? ?? ??",
            DetourName = nameof(CalcViewMatrixDetour))]
        private Hook<CalcViewMatrixDelegate>? calcViewMatrixHook;

        private delegate nint CalcViewMatrixDelegate(SceneCamera* camera);

        [Signature(
            "48 8B C4 48 89 58 ?? 48 89 70 ?? 48 89 78 ?? 55 41 56 41 57 48 8D 68 ?? 48 81 EC ?? ?? ?? ?? F3 0F 58 1D",
            DetourName = nameof(CameraCollideDetour))]
        private Hook<CameraCollideDelegate>? cameraCollideHook;

        private delegate nint CameraCollideDelegate(GameCamera* camera, Vector3* a2, Vector3* a3, float a4, nint a5,
            float a6);

        [Signature("E8 ?? ?? ?? ?? 48 83 3D ?? ?? ?? ?? ?? 74 0C", DetourName = nameof(CameraControlDetour))]
        private Hook<CameraControlDelegate>? cameraControlHook;

        private delegate nint CameraControlDelegate(nint cameraManager);

        [Signature("E8 ?? ?? ?? ?? 83 7B 58 00", DetourName = nameof(UpdateInputDetour))]
        private Hook<UpdateInputDelegate>? updateInputHook;

        private delegate void UpdateInputDelegate(nint mgr, nint a2, nint controller, MouseDeviceData* mouseData,
            nint keyData);

        [Signature("E8 ?? ?? ?? ?? 48 8B 17 48 8D 4D E0")]
        private readonly LoadMatrixDelegate? loadMatrixDelegate = null;

        private delegate Matrix4x4* LoadMatrixDelegate(RenderCamera* camera, Matrix4x4* matrix);

        private float originalMinZoom;
        private float originalMaxZoom;

        public bool IsOverridden
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    var mgr = CameraManager.Instance();
                    if (mgr != null && mgr->ActiveCameraIndex >= 0)
                    {
                        var realCamera = ((GameCamera**)mgr)[mgr->ActiveCameraIndex];
                        if (realCamera != null)
                        {
                            if (field)
                            {
                                originalMinZoom = *(float*)((byte*)realCamera + 0x118);
                                originalMaxZoom = *(float*)((byte*)realCamera + 0x11C);
                            }
                            else
                            {
                                *(float*)((byte*)realCamera + 0x118) = originalMinZoom;
                                *(float*)((byte*)realCamera + 0x11C) = originalMaxZoom;
                            }
                        }
                    }
                }
            }
        } = false;

        public Matrix4x4 CustomMatrix { get; set; } = Matrix4x4.Identity;
        public float CustomFov { get; set; } = 0.78f;

        public Vector2 MouseDelta { get; private set; } = Vector2.Zero;
        public int MouseWheel { get; private set; } = 0;
        public bool IsRightClickDragging { get; private set; } = false;

        public CameraService()
        {
            Services.GameInteropProvider.InitializeFromAttributes(this);

            calcViewMatrixHook?.Enable();
            cameraCollideHook?.Enable();
            cameraControlHook?.Enable();
            updateInputHook?.Enable();
        }


        public (Vector3 Position, Quaternion Rotation, float FoV) GetCurrentCameraState()
        {
            var cameraManager = CameraManager.Instance();
            var activeCamera = cameraManager->GetActiveCamera();

            var viewMatrix = activeCamera->SceneCamera.ViewMatrix;
            viewMatrix.M44 = 1.0f;

            Matrix4x4.Invert(viewMatrix, out var worldMatrix);

            var position = worldMatrix.Translation;
            var rotation = Quaternion.CreateFromRotationMatrix(worldMatrix);

            float fov = 0.785398f;
            if (activeCamera->SceneCamera.RenderCamera != null)
            {
                fov = activeCamera->SceneCamera.RenderCamera->FoV;
            }

            return (position, rotation, fov);
        }

        private void UpdateInputDetour(nint mgr, nint a2, nint controller, MouseDeviceData* mouseData, nint keyData)
        {
            updateInputHook!.Original(mgr, a2, controller, mouseData, keyData);

            if (IsOverridden && mouseData != null)
            {
                MouseDelta = new Vector2(mouseData->DeltaX, mouseData->DeltaY);
                MouseWheel = mouseData->ScrollDelta;
                IsRightClickDragging = mouseData->IsButtonHeld(MouseButton.Right);
                mouseData->ScrollDelta = 0;
            }
            else
            {
                MouseDelta = Vector2.Zero;
                MouseWheel = 0;
                IsRightClickDragging = false;
            }
        }

        private nint CameraControlDetour(nint cameraManager)
        {
            if (IsOverridden) return 0;
            return cameraControlHook!.Original(cameraManager);
        }

        private nint CalcViewMatrixDetour(SceneCamera* camera)
        {
            var result = calcViewMatrixHook!.Original(camera);

            if (IsOverridden && loadMatrixDelegate != null)
            {
                camera->ViewMatrix = CustomMatrix;
                var activeCamera = CameraManager.Instance()->GetActiveCamera();
                if (activeCamera != null && activeCamera->CameraBase.SceneCamera.RenderCamera != null)
                {
                    var matrixPtr = (Matrix4x4*)&camera->ViewMatrix;
                    loadMatrixDelegate(activeCamera->CameraBase.SceneCamera.RenderCamera, matrixPtr);
                    activeCamera->CameraBase.SceneCamera.RenderCamera->FoV = CustomFov;
                    activeCamera->CameraBase.SceneCamera.RenderCamera->FoV_2 = CustomFov;
                }
            }

            return result;
        }

        private nint CameraCollideDetour(GameCamera* camera, Vector3* a2, Vector3* a3, float a4, nint a5, float a6)
        {
            if (IsOverridden)
            {
                *(Vector2*)((byte*)camera + 0x218) = new Vector2(a4 + 0.001f);
                return 0;
            }

            return cameraCollideHook!.Original(camera, a2, a3, a4, a5, a6);
        }

        public void Dispose()
        {
            IsOverridden = false;
            calcViewMatrixHook?.Dispose();
            cameraCollideHook?.Dispose();
            cameraControlHook?.Dispose();
            updateInputHook?.Dispose();
        }
    }
}