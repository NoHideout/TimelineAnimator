using System;
using System.Linq;
using System.Numerics;
using TimelineAnimator.Data;
using TimelineAnimator.ImSequencer;

namespace TimelineAnimator
{
    public static class AnimationHelpers
    {
        public static bool TryGetInterpolationState<T>(TimelineTrack<T> track, int currentFrame, out TrackKeyframe<T> kfA, out TrackKeyframe<T> kfB, out float easedT)
        {
            kfA = null!; kfB = null!; easedT = 0f;

            var keyframes = track.Keyframes;
            if (keyframes.Count == 0) return false;

            kfB = keyframes.FirstOrDefault(k => k.Frame >= currentFrame);
            int kfBIndex = (kfB == null) ? -1 : keyframes.IndexOf(kfB);

            if (kfB == null)
            {
                kfA = keyframes.Last(); kfB = kfA;
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

            easedT = GetEasedT(t, kfA); 
            return true;
        }

        public static TransformState? GetInterpolatedTransform(ISequencer sequencer, TimelineTrack<TransformState> track, int currentFrame)
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
                    return TransformState.Lerp(defaultPose.Value, kfA.Value, easedIntroT); 
                }
            }
        
            return TransformState.Lerp(kfA.Value, kfB.Value, t);
        }

        public static Vector3? GetInterpolatedVector3(TimelineTrack<Vector3> track, int currentFrame, Vector3 defaultValue)
        {
            if (!TryGetInterpolationState(track, currentFrame, out var kfA, out var kfB, out var t)) return null;

            if (kfA == kfB && currentFrame < kfA.Frame)
            {
                float introT = (float)currentFrame / kfA.Frame;
                return Vector3.Lerp(defaultValue, kfA.Value, GetEasedT(introT, kfA));
            }
            return Vector3.Lerp(kfA.Value, kfB.Value, t);
        }

        public static Quaternion? GetInterpolatedQuaternion(TimelineTrack<Quaternion> track, int currentFrame, Quaternion defaultValue)
        {
            if (!TryGetInterpolationState(track, currentFrame, out var kfA, out var kfB, out var t)) return null;

            if (kfA == kfB && currentFrame < kfA.Frame)
            {
                float introT = (float)currentFrame / kfA.Frame;
                float dotIntro = Quaternion.Dot(defaultValue, kfA.Value);
                var bRotIntro = dotIntro < 0.0f ? new Quaternion(-kfA.Value.X, -kfA.Value.Y, -kfA.Value.Z, -kfA.Value.W) : kfA.Value;
                return Quaternion.Slerp(defaultValue, bRotIntro, GetEasedT(introT, kfA));
            }

            float dot = Quaternion.Dot(kfA.Value, kfB.Value);
            var bRot = dot < 0.0f ? new Quaternion(-kfB.Value.X, -kfB.Value.Y, -kfB.Value.Z, -kfB.Value.W) : kfB.Value;
            return Quaternion.Slerp(kfA.Value, bRot, t);
        }

        public static float GetInterpolatedFloat(TimelineTrack<float> track, int frame, float defaultVal)
        {
            if (AnimationHelpers.TryGetInterpolationState(track, frame, out var kfA, out var kfB, out var t))
            {
                if (kfA == kfB && frame < kfA.Frame) return defaultVal;
                return kfA.Value + (kfB.Value - kfA.Value) * t;
            }
            return defaultVal;
        }
        
        private static TransformState? GetDefaultTransform(ISequencer sequencer, string boneName)
        {
            if (sequencer.DefaultPose.TryGetValue(boneName, out var state)) return state;
            return null;
        }

        private static float GetEasedT(float t, ITrackKeyframe kf)
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
            if (Math.Abs(p1x - 0.25f) < 1e-4 && Math.Abs(p2x - 0.75f) < 1e-4) return x;
        
            float t = x;
        
            // Try Newton-Raphson
            for (int i = 0; i < 8; i++)
            {
                float currentX = CalculateBezierCoordinate(t, p1x, p2x);
                float diff = currentX - x;
                if (Math.Abs(diff) < 1e-5f) return Math.Clamp(t, 0.0f, 1.0f);

                float derivative = 3.0f * (1.0f - t) * (1.0f - t) * p1x + 6.0f * (1.0f - t) * t * (p2x - p1x) + 3.0f * t * t * (1.0f - p2x);
                if (Math.Abs(derivative) < 1e-5f) break;
            
                t -= diff / derivative;
            }
            // Bisection search fallback
            float t0 = 0.0f;
            float t1 = 1.0f;
            t = x;
        
            for (int i = 0; i < 16; i++)
            {
                float currentX = CalculateBezierCoordinate(t, p1x, p2x);
                float diff = currentX - x;
                if (Math.Abs(diff) < 1e-5f) return t;

                if (diff > 0) t1 = t;
                else t0 = t;

                t = (t1 + t0) * 0.5f;
            }
            return Math.Clamp(t, 0.0f, 1.0f);
        }
    }
}