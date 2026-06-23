using System.Numerics;
using TimelineAnimator.Data;
using TimelineAnimator.ImSequencer;

namespace TimelineAnimator;

public static class AnimationHelpers
{
    public static bool TryGetInterpolationState(TimelineTrack track, int currentFrame, out TrackKeyframe kfA,
        out TrackKeyframe kfB, out float easedT)
    {
        kfA = null!;
        kfB = null!;
        easedT = 0f;

        var keyframes = track.Keyframes;
        if (keyframes.Count == 0) return false;

        kfB = keyframes.FirstOrDefault(k => k.Frame >= currentFrame);
        int kfBIndex = (kfB == null) ? -1 : keyframes.IndexOf(kfB);

        if (kfB == null)
        {
            kfA = keyframes.Last();
            kfB = kfA;
            easedT = 1.0f;
            return true;
        }

        if (kfB.Frame == currentFrame || kfBIndex == 0)
        {
            kfA = kfB;
            easedT = 0.0f;
            return true;
        }

        kfA = keyframes[kfBIndex - 1];

        float t = (float)(currentFrame - kfA.Frame) / (kfB.Frame - kfA.Frame);
        if (float.IsNaN(t) || float.IsInfinity(t)) t = 0;

        easedT = GetEasedT(t, kfB);
        return true;
    }

    public static TransformState? GetInterpolatedTransform(ISequencer sequencer, TimelineTrack track, int currentFrame)
    {
        if (!TryGetInterpolationState(track, currentFrame, out var kfA, out var kfB, out var t))
            return null;

        if (kfA == kfB && currentFrame < kfA.Frame)
        {
            var defaultPose = GetDefaultTransform(sequencer, track.Name);
            if (defaultPose != null)
            {
                float introT = (float)currentFrame / kfA.Frame;
                float easedIntroT = GetEasedT(introT, kfA);
                return TransformState.Lerp(defaultPose.Value, kfA.Transform, easedIntroT);
            }
        }

        return TransformState.Lerp(kfA.Transform, kfB.Transform, t);
    }

    private static TransformState? GetDefaultTransform(ISequencer sequencer, string boneName)
    {
        if (sequencer.DefaultPose.TryGetValue(boneName, out var state)) return state;
        return null;
    }

    private static float GetEasedT(float t, TrackKeyframe kf)
    {
        t = Math.Clamp(t, 0.0f, 1.0f);
        if (t <= 0.0f) return 0.0f;
        if (t >= 1.0f) return 1.0f;
        float tResult = SolveBezierX(t, kf.P1.X, kf.P2.X);
        return CalculateBezierCoordinate(tResult, kf.P1.Y, kf.P2.Y);
    }

    private static float CalculateBezierCoordinate(float t, float p1, float p2)
    {
        float u = 1.0f - t;
        float tt = t * t;
        float uu = u * u;
        float ttt = tt * t;
        return (3 * uu * t * p1) + (3 * u * tt * p2) + ttt;
    }

    private static float SolveBezierX(float x, float p1x, float p2x)
    {
        float t = x;
        for (int i = 0; i < 5; i++)
        {
            float currentX = CalculateBezierCoordinate(t, p1x, p2x);
            float derivative = 3.0f * (1.0f - t) * (1.0f - t) * p1x + 6.0f * (1.0f - t) * t * (p2x - p1x) +
                               3.0f * t * t * (1.0f - p2x);
            if (Math.Abs(derivative) < 1e-6) break;
            t -= (currentX - x) / derivative;
        }

        return Math.Clamp(t, 0.0f, 1.0f);
    }

    //TODO unused can remove later
    public static TransformState FromMatrix(Matrix4x4 matrix)
    {
        Matrix4x4.Decompose(matrix, out var scale, out var rotation, out var translation);
        return new TransformState { Position = translation, Rotation = rotation, Scale = scale };
    }
    //TODO unused can remove later
    public static Matrix4x4 ToMatrix(TransformState state)
    {
        return Matrix4x4.CreateScale(state.Scale) * Matrix4x4.CreateFromQuaternion(state.Rotation) *
               Matrix4x4.CreateTranslation(state.Position);
    }
}