using System;
using System.Collections.Generic;
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
            var camObj = new AnimationObject { Name = "Camera", Type = ObjectType.Camera };
            Clip.Objects.Add(camObj);

            camObj.GetOrAddTrack(PropertyType.PositionX);
            camObj.GetOrAddTrack(PropertyType.PositionY);
            camObj.GetOrAddTrack(PropertyType.PositionZ);
            camObj.GetOrAddTrack(PropertyType.RotationX);
            camObj.GetOrAddTrack(PropertyType.RotationY);
            camObj.GetOrAddTrack(PropertyType.RotationZ);
            camObj.GetOrAddTrack(PropertyType.CameraFov);

            var camState = Services.CameraService.GetCurrentCameraState();
            
            Clip.BasePose.Camera = new CameraPose
            {
                Position = camState.Position,
                Rotation = camState.Rotation,
                FieldOfView = camState.FoV,
                RelativeToPlayer = false
            };
            
            // store scene orig in case of migration
            var player = Services.ObjectTable[0];
            if (player != null)
            {
                Clip.BasePose.SceneOrigin = new TransformPose
                {
                    Position = player.Position,
                    Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, player.Rotation),
                    Scale = Vector3.One
                };
            }
            
            void SetKey(PropertyType prop, float val) => camObj.GetOrAddTrack(prop).Curve.AddKey(0, val);
            
            var eulerRotation = AnimationHelpers.ToEulerAngles(camState.Rotation);

            SetKey(PropertyType.PositionX, camState.Position.X);
            SetKey(PropertyType.PositionY, camState.Position.Y);
            SetKey(PropertyType.PositionZ, camState.Position.Z);
            SetKey(PropertyType.RotationX, eulerRotation.X);
            SetKey(PropertyType.RotationY, eulerRotation.Y);
            SetKey(PropertyType.RotationZ, eulerRotation.Z);
            SetKey(PropertyType.CameraFov, camState.FoV);

            RebuildHierarchy();
        }

        public override void Draw(ImSequencerCore uiCore, ref int currentFrame, ref int selectedEntry, bool modifierHeld)
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

            var camObj = Clip.Objects.FirstOrDefault(o => o.Type == ObjectType.Camera)
                          ?? new AnimationObject { Name = "Camera", Type = ObjectType.Camera };
            
            if (!Clip.Objects.Contains(camObj)) Clip.Objects.Add(camObj);

            var camState = Services.CameraService.GetCurrentCameraState();

            if (Clip.BasePose.Camera?.RelativeToPlayer == true)
            {
                var player = Services.ObjectTable[0];
                if (player != null)
                {
                    Quaternion playerRot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, player.Rotation);
                    Vector3 playerPos = player.Position;
                    
                    Quaternion invPlayerRot = Quaternion.Inverse(playerRot);
                    
                    camState.Position = Vector3.Transform(camState.Position - playerPos, invPlayerRot);
                    camState.Rotation = invPlayerRot * camState.Rotation;
                }
            }

            void AddKey(PropertyType prop, float val) => camObj.GetOrAddTrack(prop).Curve.AddKey(frame, val);

            AddKey(PropertyType.PositionX, camState.Position.X);
            AddKey(PropertyType.PositionY, camState.Position.Y);
            AddKey(PropertyType.PositionZ, camState.Position.Z);

            var eulerRotation = AnimationHelpers.ToEulerAngles(camState.Rotation);

            float GetUnwrappedAngle(PropertyType prop, float rawAngle)
            {
                var track = camObj.GetTrack(prop);
                if (track == null || track.Curve.Keys.Count == 0) return rawAngle;
                
                var previousKey = track.Curve.Keys.LastOrDefault(k => k.Frame <= frame) ?? track.Curve.Keys.Last();
                return AnimationHelpers.UnwrapAngle(rawAngle, previousKey.Value);
            }

            AddKey(PropertyType.RotationX, GetUnwrappedAngle(PropertyType.RotationX, eulerRotation.X));
            AddKey(PropertyType.RotationY, GetUnwrappedAngle(PropertyType.RotationY, eulerRotation.Y));
            AddKey(PropertyType.RotationZ, GetUnwrappedAngle(PropertyType.RotationZ, eulerRotation.Z));
            
            AddKey(PropertyType.CameraFov, camState.FoV);

            RebuildHierarchy();
        }

        public override void DrawInspector(int currentFrame)
        {
            ImGui.Separator();
            ImGui.Text("Space Settings");
            if (Clip.BasePose.Camera.HasValue)
            {
                var camPose = Clip.BasePose.Camera.Value;
                bool isRelative = camPose.RelativeToPlayer;
                
                if (ImGui.Checkbox("Relative to Player", ref isRelative))
                {
                    camPose.RelativeToPlayer = isRelative;
                    Clip.BasePose.Camera = camPose;
                    
                    ConvertCameraSpace(isRelative);
                    
                    ApplyPose(currentFrame);
                }
                
                if (ImGui.IsItemHovered() && Services.Configuration.ShowTooltips)
                    ImGui.SetTooltip("Calculates camera position and rotation relative to your character's world position.");
                
                ImGui.Spacing();
                ImGui.Separator();
            }

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
        }

        private void ConvertCameraSpace(bool toLocal)
        {
            var player = Services.ObjectTable[0];
            if (player == null) return;

            var camObj = Clip.Objects.FirstOrDefault(o => o.Type == ObjectType.Camera);
            if (camObj == null || !Clip.BasePose.Camera.HasValue) return;

            Quaternion playerRot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, player.Rotation);
            Vector3 playerPos = player.Position;

            var defaultState = Clip.BasePose.Camera.Value;
            var oldDefaultEuler = AnimationHelpers.ToEulerAngles(defaultState.Rotation);

            var tracks = new[]
            {
                camObj.GetTrack(PropertyType.PositionX), camObj.GetTrack(PropertyType.PositionY), camObj.GetTrack(PropertyType.PositionZ),
                camObj.GetTrack(PropertyType.RotationX), camObj.GetTrack(PropertyType.RotationY), camObj.GetTrack(PropertyType.RotationZ)
            };

            var uniqueFrames = tracks.Where(t => t != null)
                                     .SelectMany(t => t.Curve.Keys)
                                     .Select(k => k.Frame)
                                     .Distinct()
                                     .OrderBy(f => f)
                                     .ToList();

            var evaluatedPoses = new Dictionary<int, (Vector3 pos, Quaternion rot)>();
            foreach (int frame in uniqueFrames)
            {
                float px = EvaluateProperty(camObj, PropertyType.PositionX, frame, defaultState.Position.X);
                float py = EvaluateProperty(camObj, PropertyType.PositionY, frame, defaultState.Position.Y);
                float pz = EvaluateProperty(camObj, PropertyType.PositionZ, frame, defaultState.Position.Z);
                
                evaluatedPoses[frame] = (new Vector3(px, py, pz), AnimationHelpers.EvaluateRotation(camObj, frame, oldDefaultEuler));
            }

            Vector3 defPos = defaultState.Position;
            Quaternion defRot = defaultState.Rotation;

            if (toLocal)
            {
                Quaternion invPlayerRot = Quaternion.Inverse(playerRot);
                defPos = Vector3.Transform(defPos - playerPos, invPlayerRot);
                defRot = invPlayerRot * defRot;
            }
            else
            {
                defPos = Vector3.Transform(defPos, playerRot) + playerPos;
                defRot = playerRot * defRot;
            }

            var newBasePose = Clip.BasePose.Camera.Value;
            newBasePose.Position = defPos;
            newBasePose.Rotation = defRot;
            Clip.BasePose.Camera = newBasePose;

            var newDefaultEuler = AnimationHelpers.ToEulerAngles(defRot);
            Vector3 lastEuler = newDefaultEuler;

            foreach (int frame in uniqueFrames)
            {
                var oldPose = evaluatedPoses[frame];
                Vector3 newPos = oldPose.pos;
                Quaternion newRot = oldPose.rot;

                if (toLocal)
                {
                    Quaternion invPlayerRot = Quaternion.Inverse(playerRot);
                    newPos = Vector3.Transform(newPos - playerPos, invPlayerRot);
                    newRot = invPlayerRot * newRot;
                }
                else
                {
                    newPos = Vector3.Transform(newPos, playerRot) + playerPos;
                    newRot = playerRot * newRot;
                }

                Vector3 euler = AnimationHelpers.ToEulerAngles(newRot);
                euler.X = AnimationHelpers.UnwrapAngle(euler.X, lastEuler.X);
                euler.Y = AnimationHelpers.UnwrapAngle(euler.Y, lastEuler.Y);
                euler.Z = AnimationHelpers.UnwrapAngle(euler.Z, lastEuler.Z);
                lastEuler = euler;

                void UpdateKey(PropertyType prop, float val)
                {
                    var t = camObj.GetOrAddTrack(prop);
                    var existingKey = t.Curve.GetKey(frame);
                    
                    if (existingKey != null)
                    {
                        existingKey.Value = val;
                    }
                    else
                    {
                        t.Curve.AddKey(frame, val);
                    }
                }

                UpdateKey(PropertyType.PositionX, newPos.X);
                UpdateKey(PropertyType.PositionY, newPos.Y);
                UpdateKey(PropertyType.PositionZ, newPos.Z);
                UpdateKey(PropertyType.RotationX, euler.X);
                UpdateKey(PropertyType.RotationY, euler.Y);
                UpdateKey(PropertyType.RotationZ, euler.Z);
            }
        }

        public override void ApplyPose(int frame)
        {
            if (!Services.CameraService.IsOverridden) return;

            var camObj = Clip.Objects.FirstOrDefault(o => o.Type == ObjectType.Camera);
            if (camObj == null || !Clip.BasePose.Camera.HasValue) return;

            var defaultState = Clip.BasePose.Camera.Value;

            float posX = EvaluateProperty(camObj, PropertyType.PositionX, frame, defaultState.Position.X);
            float posY = EvaluateProperty(camObj, PropertyType.PositionY, frame, defaultState.Position.Y);
            float posZ = EvaluateProperty(camObj, PropertyType.PositionZ, frame, defaultState.Position.Z);
            
            position = new Vector3(posX, posY, posZ);
            
            var defaultEuler = AnimationHelpers.ToEulerAngles(defaultState.Rotation);
            rotation = AnimationHelpers.EvaluateRotation(camObj, frame, defaultEuler);

            if (defaultState.RelativeToPlayer)
            {
                var player = Services.ObjectTable[0];
                if (player != null)
                {
                    Quaternion playerRot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, player.Rotation);
                    Vector3 playerPos = player.Position;
                    
                    position = Vector3.Transform(position, playerRot) + playerPos;
                    rotation = playerRot * rotation;
                }
            }

            eulerAngles = AnimationHelpers.ToEulerAngles(rotation);
            fov = EvaluateProperty(camObj, PropertyType.CameraFov, frame, defaultState.FieldOfView);
            
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
    }
}