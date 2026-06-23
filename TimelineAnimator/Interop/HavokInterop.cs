using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using TimelineAnimator.Data;

namespace TimelineAnimator.Interop;

public static unsafe class HavokInterop
{
    public static Dictionary<string, (TransformState Transform, string ParentName)> GetNativeBones(
        nint gameObjectAddress)
    {
        var result = new Dictionary<string, (TransformState, string)>();

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

            var localTransforms = havokPose->LocalPose.Data;
            var modelTransforms = havokPose->ModelPose.Data;

            var parentIndices = hkaSkeleton->ParentIndices.Data;

            for (int b = 0; b < hkaSkeleton->Bones.Length; b++)
            {
                string boneName = hkaSkeleton->Bones[b].Name.String.Trim();
                if (result.ContainsKey(boneName)) continue;

                var localTrans = localTransforms[b];
                var modelTrans = modelTransforms[b];

                string parentName = string.Empty;
                short parentIndex = parentIndices[b];
                if (parentIndex >= 0 && parentIndex < hkaSkeleton->Bones.Length)
                {
                    parentName = hkaSkeleton->Bones[parentIndex].Name.String.Trim();
                }

                var transformState = new TransformState
                {
                    Position =
                        new Vector3(localTrans.Translation.X, localTrans.Translation.Y, localTrans.Translation.Z),
                    Rotation = new Quaternion(localTrans.Rotation.X, localTrans.Rotation.Y, localTrans.Rotation.Z,
                        localTrans.Rotation.W),
                    Scale = new Vector3(modelTrans.Scale.X, modelTrans.Scale.Y, modelTrans.Scale.Z),
                    FieldOfView = 0.785398f
                };

                result[boneName] = (transformState, parentName);
            }
        }

        return result;
    }
}