using System.Collections.Generic;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace TimelineAnimator.Interop
{
    public static unsafe class HavokInterop
    {
        public static Dictionary<string, ((Vector3 Position, Quaternion Rotation, Vector3 Scale, float FieldOfView) Transform, string ParentName)> GetNativeBones(
            nint gameObjectAddress)
        {
            var result = new Dictionary<string, ((Vector3, Quaternion, Vector3, float), string)>();

            var gameObj = (GameObject*)gameObjectAddress;
            if (gameObj == null || gameObj->DrawObject == null) return result;

            var charaBase = (CharacterBase*)gameObj->DrawObject;
            if (charaBase == null || charaBase->Skeleton == null) return result;

            var skeleton = charaBase->Skeleton;

            for (int i = 0; i < skeleton->PartialSkeletonCount; i++)
            {
                var partialSk = &skeleton->PartialSkeletons[i];
                var havokPose = partialSk->GetHavokPose(0);

                if (havokPose == null || havokPose->Skeleton == null) continue;

                var hkaSkeleton = havokPose->Skeleton;
                var modelTransforms = havokPose->ModelPose.Data;
                var parentIndices = hkaSkeleton->ParentIndices.Data;

                short connectedBoneIndex = i > 0 ? partialSk->ConnectedBoneIndex : (short)-1;
                short connectedParentBoneIndex = i > 0 ? partialSk->ConnectedParentBoneIndex : (short)-1;

                FFXIVClientStructs.Havok.Animation.Rig.hkaSkeleton* parentHkaSkeleton = null;
                var parentModelTransforms = havokPose->ModelPose.Data;
                bool hasParentModelTransforms = false;

                if (i > 0 && connectedBoneIndex >= 0 && connectedParentBoneIndex >= 0)
                {
                    var parentPartial = &skeleton->PartialSkeletons[0];
                    var parentPose = parentPartial->GetHavokPose(0);
                    if (parentPose != null && parentPose->Skeleton != null)
                    {
                        parentHkaSkeleton = parentPose->Skeleton;
                        parentModelTransforms = parentPose->ModelPose.Data;
                        hasParentModelTransforms = true;
                    }
                }

                for (int b = 0; b < hkaSkeleton->Bones.Length; b++)
                {
                    if (i > 0 && b == connectedBoneIndex)
                        continue;

                    string boneName = hkaSkeleton->Bones[b].Name.String.Trim();
                    if (result.ContainsKey(boneName)) continue;

                    var modelTrans = modelTransforms[b];
                    var boneRot = new Quaternion(modelTrans.Rotation.X, modelTrans.Rotation.Y, modelTrans.Rotation.Z, modelTrans.Rotation.W);
                    var boneScale = new Vector3(modelTrans.Scale.X, modelTrans.Scale.Y, modelTrans.Scale.Z);
                    var bonePos = new Vector3(modelTrans.Translation.X, modelTrans.Translation.Y, modelTrans.Translation.Z);

                    short parentIndex = parentIndices[b];

                    Vector3 localPos;
                    Quaternion localRot;
                    string parentName;

                    if (parentIndex >= 0 && parentIndex < hkaSkeleton->Bones.Length && parentIndex != connectedBoneIndex)
                    {
                        var parentTrans = modelTransforms[parentIndex];
                        var parentRot = new Quaternion(parentTrans.Rotation.X, parentTrans.Rotation.Y, parentTrans.Rotation.Z, parentTrans.Rotation.W);
                        var parentPos = new Vector3(parentTrans.Translation.X, parentTrans.Translation.Y, parentTrans.Translation.Z);
                        var invParentRot = Quaternion.Inverse(parentRot);

                        localPos = Vector3.Transform(bonePos - parentPos, invParentRot);
                        localRot = Quaternion.Normalize(invParentRot * boneRot);
                        parentName = hkaSkeleton->Bones[parentIndex].Name.String.Trim();
                    }
                    else if (i > 0 && parentIndex == connectedBoneIndex && parentHkaSkeleton != null && hasParentModelTransforms)
                    {
                        var parentTrans = parentModelTransforms[connectedParentBoneIndex];
                        var parentRot = new Quaternion(parentTrans.Rotation.X, parentTrans.Rotation.Y, parentTrans.Rotation.Z, parentTrans.Rotation.W);
                        var parentPos = new Vector3(parentTrans.Translation.X, parentTrans.Translation.Y, parentTrans.Translation.Z);
                        var invParentRot = Quaternion.Inverse(parentRot);

                        localPos = Vector3.Transform(bonePos - parentPos, invParentRot);
                        localRot = Quaternion.Normalize(invParentRot * boneRot);
                        parentName = parentHkaSkeleton->Bones[connectedParentBoneIndex].Name.String.Trim();
                    }
                    else
                    {
                        localPos = bonePos;
                        localRot = boneRot;
                        parentName = string.Empty;
                    }

                    var transform = (
                        Position: localPos,
                        Rotation: localRot,
                        Scale: boneScale,
                        FieldOfView: 0.785398f
                    );

                    result[boneName] = (transform, parentName);
                }
            }

            return result;
        }
    }
}