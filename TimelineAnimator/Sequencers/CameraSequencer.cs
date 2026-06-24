using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using TimelineAnimator.Data;
using TimelineAnimator.ImSequencer;

namespace TimelineAnimator.Sequencers
{
    public class CameraSequencer : SequencerBase
    {
        public override string Name => "Cinematic Camera";

        private float flySpeed = 10.0f;
        private float lookSensitivity = 0.003f;
        private Vector3 position;
        private Quaternion rotation = Quaternion.Identity;
        private Vector3 eulerAngles;
        private float fov = 0.78f;

        public CameraSequencer()
        {
            Sequence.AddTrack<TransformState>(Constants.TrackNames.CameraPosition, TrackType.Transform);
            Sequence.AddTrack<TransformState>(Constants.TrackNames.CameraRotation, TrackType.Transform);
            Sequence.AddTrack<TransformState>(Constants.TrackNames.CameraFOV, TrackType.Transform);

            var camState = Services.CameraService.GetCurrentCameraState();

            var defaultTransform = new TransformState
            {
                Position = new Vector3(camState.Position.X, camState.Position.Y, camState.Position.Z),
                Rotation = new Quaternion(camState.Rotation.X, camState.Rotation.Y, camState.Rotation.Z,
                    camState.Rotation.W),
                Scale = new Vector3(1, 1, 1),
                FieldOfView = camState.FoV
            };

            DefaultPose[Constants.TrackNames.CameraPosition] = defaultTransform;
            DefaultPose[Constants.TrackNames.CameraRotation] = defaultTransform;
            DefaultPose[Constants.TrackNames.CameraFOV] = defaultTransform;
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

            string[] trackNames = { "Camera Position", "Camera Rotation", "Camera FOV" };

            foreach (var trackName in trackNames)
            {
                if (Sequence.GetTrackByName(trackName) is TimelineTrack<TransformState> track)
                {
                    var transformState = new TransformState
                    {
                        Position = position,
                        Rotation = rotation,
                        Scale = Vector3.One,
                        FieldOfView = fov
                    };

                    track.Keyframes.RemoveAll(k => k.Frame == frame);
                    track.AddKeyframe(frame, transformState);
                }
            }
        }

        public override void DrawInspector(int currentFrame)
        {
            ImGui.Text("Camera Controls");
            ImGui.Separator();
            ImGui.Spacing();

            bool isFreeCam = Services.CameraService.IsOverridden;

            if (ImGui.Checkbox("Enable Camera", ref isFreeCam))
            {
                Services.CameraService.IsOverridden = isFreeCam;
                if (isFreeCam)
                {
                    var state = Services.CameraService.GetCurrentCameraState();
                    position = state.Position;
                    rotation = state.Rotation;
                    fov = state.FoV;
                    eulerAngles = ToEulerAngles(rotation);
                }
            }

            if (isFreeCam)
            {
                ImGui.Spacing();
                ImGui.Text("Movement Settings");
                ImGui.DragFloat("Fly Speed", ref flySpeed, 0.1f, 1.0f, 100.0f);
                ImGui.DragFloat("Mouse Sensitivity", ref lookSensitivity, 0.001f, 0.001f, 0.02f);

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.Text("Camera Parameters");

                ImGui.DragFloat3("Position", ref position, 0.05f);

                if (ImGui.DragFloat3("Rotation", ref eulerAngles, 0.05f))
                {
                    rotation = Quaternion.CreateFromYawPitchRoll(eulerAngles.Y, eulerAngles.X, eulerAngles.Z);
                }

                ImGui.DragFloat("FOV", ref fov, 0.01f, 0.1f, 3.0f);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        public override void ApplyPose(int frame)
        {
            if (!Services.CameraService.IsOverridden) return;

            var posTrack = Sequence.GetTrackByName(Constants.TrackNames.CameraPosition) as TimelineTrack<TransformState>;
            var rotTrack = Sequence.GetTrackByName(Constants.TrackNames.CameraRotation) as TimelineTrack<TransformState>;
            var fovTrack = Sequence.GetTrackByName(Constants.TrackNames.CameraFOV) as TimelineTrack<TransformState>;

            var posTransform = posTrack != null ? AnimationHelpers.GetInterpolatedTransform(this, posTrack, frame) : null;
            var rotTransform = rotTrack != null ? AnimationHelpers.GetInterpolatedTransform(this, rotTrack, frame) : null;
            var fovTransform = fovTrack != null ? AnimationHelpers.GetInterpolatedTransform(this, fovTrack, frame) : null;

            position = posTransform != null ? posTransform.Value.Position : position;
            rotation = rotTransform != null ? rotTransform.Value.Rotation : rotation;
            fov = fovTransform != null ? fovTransform.Value.FieldOfView : fov;

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

            float siny_cosp = 2 * (q.W * q.Y + q.X * q.Z);
            float cosy_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            angles.Y = (float)Math.Atan2(siny_cosp, cosy_cosp);

            float sinr_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            float cosr_cosp = 1 - 2 * (q.X * q.X + q.Z * q.Z);
            angles.Z = (float)Math.Atan2(sinr_cosp, cosr_cosp);
            return angles;
        }

        public override void RebuildHierarchy()
        {
            var sorted = new List<TimelineTrack>();

            var posTrack = Sequence.GetTrackByName("Camera Position");
            if (posTrack != null) sorted.Add(posTrack);

            var rotTrack = Sequence.GetTrackByName("Camera Rotation");
            if (rotTrack != null) sorted.Add(rotTrack);

            var fovTrack = Sequence.GetTrackByName("Camera FOV");
            if (fovTrack != null) sorted.Add(fovTrack);

            Sequence.Tracks = sorted;
        }
    }
}