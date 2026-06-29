using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using TimelineAnimator.Data;
using TimelineAnimator.ImSequencer;

namespace TimelineAnimator.Sequencers
{
    public class CameraSequencer : SequencerBase
    {
        public override string Name => "Camera";

        private float flySpeed = 10.0f;
        private float lookSensitivity = 0.003f;
        private Vector3 position;
        private Quaternion rotation = Quaternion.Identity;
        private Vector3 eulerAngles;
        private float fov = 0.78f;

        public CameraSequencer()
        {
            var camFolder = new FolderTrack("Camera") { DisplayName = "Camera" };
            Sequence.Tracks.Add(camFolder);
            
            var posTrack = Sequence.AddTrack<Vector3>("Camera_Position", TrackType.Vector3);
            posTrack.ParentName = "Camera"; posTrack.DisplayName = "Position";
            
            var rotTrack = Sequence.AddTrack<Quaternion>("Camera_Rotation", TrackType.Quaternion);
            rotTrack.ParentName = "Camera"; rotTrack.DisplayName = "Rotation";
            
            var fovTrack = Sequence.AddTrack<float>("Camera_FOV", TrackType.Float);
            fovTrack.ParentName = "Camera"; fovTrack.DisplayName = "Field of View";

            var camState = Services.CameraService.GetCurrentCameraState();
            
            DefaultPose["Camera"] = new TransformState
            {
                Position = camState.Position,
                Rotation = camState.Rotation,
                Scale = Vector3.One,
                FieldOfView = camState.FoV
            };

            posTrack.AddKeyframe(0, camState.Position);
            rotTrack.AddKeyframe(0, camState.Rotation);
            fovTrack.AddKeyframe(0, camState.FoV);

            RebuildHierarchy();
        }

        public override void Draw(ImSequencerCore uiCore, ref int currentFrame, ref int selectedEntry,
            bool modifierHeld)
        {
            if (Services.CameraService.IsOverridden)
            {
                HandleFreeCamInput();

                var newMatrix = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
                Matrix4x4.Invert(newMatrix, out var invertedView);

                Services.CameraService.CustomMatrix = invertedView;
                Services.CameraService.CustomFov = fov;
            }

            base.Draw(uiCore, ref currentFrame, ref selectedEntry, modifierHeld);
        }

        public void AddKeyframeAt(int frame)
        {
            if (!Services.CameraService.IsOverridden) return;

            var posTrack = Sequence.GetTrackByName("Camera_Position") as TimelineTrack<Vector3>;
            if (posTrack == null) { posTrack = Sequence.AddTrack<Vector3>("Camera_Position", TrackType.Vector3); posTrack.ParentName = "Camera"; posTrack.DisplayName = "Position"; }

            var rotTrack = Sequence.GetTrackByName("Camera_Rotation") as TimelineTrack<Quaternion>;
            if (rotTrack == null) { rotTrack = Sequence.AddTrack<Quaternion>("Camera_Rotation", TrackType.Quaternion); rotTrack.ParentName = "Camera"; rotTrack.DisplayName = "Rotation"; }

            var fovTrack = Sequence.GetTrackByName("Camera_FOV") as TimelineTrack<float>;
            if (fovTrack == null) { fovTrack = Sequence.AddTrack<float>("Camera_FOV", TrackType.Float); fovTrack.ParentName = "Camera"; fovTrack.DisplayName = "Field of View"; }

            posTrack.Keyframes.RemoveAll(k => k.Frame == frame);
            posTrack.AddKeyframe(frame, position);

            rotTrack.Keyframes.RemoveAll(k => k.Frame == frame);
            rotTrack.AddKeyframe(frame, rotation);

            fovTrack.Keyframes.RemoveAll(k => k.Frame == frame);
            fovTrack.AddKeyframe(frame, fov);

            RebuildHierarchy();
        }

        public override void DrawInspector(int currentFrame)
        {
            ImGui.Text("Camera Controls");
            ImGui.Separator();
            ImGui.Spacing();

            if (Services.CameraService.IsOverridden)
            {
                ImGui.Text("Movement Settings");
                ImGui.DragFloat("Fly Speed", ref flySpeed, 0.1f, 1.0f, 100.0f);
                ImGui.DragFloat("Mouse Sensitivity", ref lookSensitivity, 0.001f, 0.001f, 0.02f);
            }
            else
            {
                ImGui.TextDisabled("Enable Free Camera in the toolbar");
                ImGui.TextDisabled("to use movement controls.");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        public override void ApplyPose(int frame)
        {
            if (!Services.CameraService.IsOverridden) return;

            foreach (var folderTrack in Sequence.Tracks.OfType<FolderTrack>())
            {
                string name = folderTrack.Name; 
                if (!DefaultPose.TryGetValue(name, out var defaultState)) continue;

                var posTrack = Sequence.GetTrackByName($"{name}_Position") as TimelineTrack<Vector3>;
                var rotTrack = Sequence.GetTrackByName($"{name}_Rotation") as TimelineTrack<Quaternion>;
                var fovTrack = Sequence.GetTrackByName($"{name}_FOV") as TimelineTrack<float>;

                position = posTrack != null ? AnimationHelpers.GetInterpolatedVector3(posTrack, frame, defaultState.Position) ?? defaultState.Position : defaultState.Position;
                rotation = rotTrack != null ? AnimationHelpers.GetInterpolatedQuaternion(rotTrack, frame, defaultState.Rotation) ?? defaultState.Rotation : defaultState.Rotation;
                fov = fovTrack != null ? AnimationHelpers.GetInterpolatedFloat(fovTrack, frame, defaultState.FieldOfView) : defaultState.FieldOfView;
            }

            eulerAngles = ToEulerAngles(rotation);

            var newMatrix = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
            Matrix4x4.Invert(newMatrix, out var invertedView);

            Services.CameraService.CustomMatrix = invertedView;
            Services.CameraService.CustomFov = fov;
        }

        private void HandleFreeCamInput()
        {
            int scrollDelta = Services.CameraService.MouseWheel;
            if (scrollDelta != 0)
            {
                flySpeed += Math.Sign(scrollDelta) * 2.0f;
                flySpeed = Math.Clamp(flySpeed, 1.0f, 100.0f);
            }

            if (Services.CameraService.IsRightClickDragging)
            {
                var delta = Services.CameraService.MouseDelta;
                if (delta.X != 0 || delta.Y != 0)
                {
                    eulerAngles.Y -= delta.X * lookSensitivity;
                    eulerAngles.X -= delta.Y * lookSensitivity;
                    eulerAngles.X = Math.Clamp(eulerAngles.X, -1.57f, 1.57f);

                    rotation = Quaternion.CreateFromYawPitchRoll(eulerAngles.Y, eulerAngles.X, eulerAngles.Z);
                }
            }

            if (ImGui.GetIO().WantCaptureKeyboard) return;

            var forward = Vector3.Transform(new Vector3(0, 0, 1), rotation);
            var right = Vector3.Transform(new Vector3(1, 0, 0), rotation);
            var up = new Vector3(0, 1, 0);

            Vector3 moveInput = Vector3.Zero;

            if (Services.KeyState[VirtualKey.W]) moveInput -= forward;
            if (Services.KeyState[VirtualKey.S]) moveInput += forward;
            if (Services.KeyState[VirtualKey.A]) moveInput -= right;
            if (Services.KeyState[VirtualKey.D]) moveInput += right;
            if (Services.KeyState[VirtualKey.E]) moveInput += up;
            if (Services.KeyState[VirtualKey.Q]) moveInput -= up;

            float currentSpeed = flySpeed;
            if (Services.KeyState[VirtualKey.SHIFT]) currentSpeed *= 3.0f;

            if (moveInput.LengthSquared() > 0)
            {
                moveInput = Vector3.Normalize(moveInput);
                position += moveInput * currentSpeed * ImGui.GetIO().DeltaTime;
            }
        }

        private Vector3 ToEulerAngles(Quaternion q)
        {
            Vector3 angles = new();
            float sinp = 2 * (q.W * q.X - q.Y * q.Z);
            if (Math.Abs(sinp) >= 1) angles.X = (float)Math.CopySign(Math.PI / 2, sinp);
            else angles.X = (float)Math.Asin(sinp);

            float sinyCosp = 2 * (q.W * q.Y + q.X * q.Z);
            float cosyCosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            angles.Y = (float)Math.Atan2(sinyCosp, cosyCosp);

            float sinrCosp = 2 * (q.W * q.Z + q.X * q.Y);
            float cosrCosp = 1 - 2 * (q.X * q.X + q.Z * q.Z);
            angles.Z = (float)Math.Atan2(sinrCosp, cosrCosp);
            return angles;
        }
    }
}