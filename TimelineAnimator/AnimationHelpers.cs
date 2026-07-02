using System;
using System.Linq;
using System.Numerics;
using TimelineAnimator.Data;

namespace TimelineAnimator
{
    public static class AnimationHelpers
    {
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
        
            for (int i = 0; i < 8; i++)
            {
                float currentX = CalculateBezierCoordinate(t, p1x, p2x);
                float diff = currentX - x;
                if (Math.Abs(diff) < 1e-5f) return Math.Clamp(t, 0.0f, 1.0f);

                float derivative = 3.0f * (1.0f - t) * (1.0f - t) * p1x + 6.0f * (1.0f - t) * t * (p2x - p1x) + 3.0f * t * t * (1.0f - p2x);
                if (Math.Abs(derivative) < 1e-5f) break;
            
                t -= diff / derivative;
                
                t = Math.Clamp(t, 0.0f, 1.0f); 
            }
            
            float t0 = 0.0f;
            float t1 = 1.0f;
            t = x;
        
            for (int i = 0; i < 32; i++)
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

        private static float CalculateAbsoluteBezierY(float t, float p0, float p1, float p2, float p3)
        {
            float u = 1.0f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            return (uuu * p0) + (3 * uu * t * p1) + (3 * u * tt * p2) + (ttt * p3);
        }

        private static float CalculateProgressionT(CurveKeyframe kfA, CurveKeyframe kfB, int currentFrame)
        {
            if (kfA.Interpolation == InterpolationMode.Constant) return 0f;
            
            if (kfA.Interpolation == InterpolationMode.Linear)
            {
                return (float)(currentFrame - kfA.Frame) / (kfB.Frame - kfA.Frame);
            }

            Vector2 p0 = new Vector2(kfA.Frame, kfA.Value);
            Vector2 p1 = p0 + kfA.Tangents.Out;
            Vector2 p2 = new Vector2(kfB.Frame, kfB.Value) + kfB.Tangents.In;
            Vector2 p3 = new Vector2(kfB.Frame, kfB.Value);

            float spanX = p3.X - p0.X;
            if (spanX <= 0) return 0f;

            float targetX = (currentFrame - p0.X) / spanX;
            float normP1x = (p1.X - p0.X) / spanX;
            float normP2x = (p2.X - p0.X) / spanX;

            return SolveBezierX(targetX, normP1x, normP2x);
        }
        
        public static float EvaluateCurve(AnimationCurve curve, int currentFrame, float defaultVal)
        {
            if (curve.Keys.Count == 0) return defaultVal;
            if (curve.Keys.Count == 1) return curve.Keys[0].Value;

            var kfB = curve.Keys.FirstOrDefault(k => k.Frame >= currentFrame);
            if (kfB == null) return curve.Keys.Last().Value;
    
            int kfBIndex = curve.Keys.IndexOf(kfB);
            
            if (kfBIndex == 0) 
            {
                if (kfB.Frame == 0 && currentFrame == 0) return kfB.Value;
                if (currentFrame <= 0) return defaultVal;
                
                float t = (float)currentFrame / kfB.Frame;
                return defaultVal + (kfB.Value - defaultVal) * t;
            }

            var kfA = curve.Keys[kfBIndex - 1];

            if (kfA.Interpolation == InterpolationMode.Constant)
                return kfA.Value;

            float tResult = CalculateProgressionT(kfA, kfB, currentFrame);

            if (kfA.Interpolation == InterpolationMode.Linear)
            {
                return kfA.Value + (kfB.Value - kfA.Value) * tResult;
            }

            Vector2 p0 = new Vector2(kfA.Frame, kfA.Value);
            Vector2 p1 = p0 + kfA.Tangents.Out;
            Vector2 p2 = new Vector2(kfB.Frame, kfB.Value) + kfB.Tangents.In;
            Vector2 p3 = new Vector2(kfB.Frame, kfB.Value);

            return CalculateAbsoluteBezierY(tResult, p0.Y, p1.Y, p2.Y, p3.Y);
        }

        public static Vector3 ToEulerAngles(Quaternion q)
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
        
        public static float UnwrapAngle(float newAngle, float previousAngle)
        {
            float diff = newAngle - previousAngle;
    
            while (diff > MathF.PI) diff -= 2 * MathF.PI;
            while (diff < -MathF.PI) diff += 2 * MathF.PI;
    
            return previousAngle + diff;
        }
        
        public static Quaternion FromEulerAngles(Vector3 euler)
        {
            return Quaternion.CreateFromYawPitchRoll(euler.Y, euler.X, euler.Z);
        }

        public static Quaternion EvaluateRotation(AnimationObject obj, int currentFrame, Vector3 defaultEuler)
        {
            var trackX = obj.GetTrack(PropertyType.RotationX);
            var trackY = obj.GetTrack(PropertyType.RotationY);
            var trackZ = obj.GetTrack(PropertyType.RotationZ);

            float rx = trackX != null ? EvaluateCurve(trackX.Curve, currentFrame, defaultEuler.X) : defaultEuler.X;
            float ry = trackY != null ? EvaluateCurve(trackY.Curve, currentFrame, defaultEuler.Y) : defaultEuler.Y;
            float rz = trackZ != null ? EvaluateCurve(trackZ.Curve, currentFrame, defaultEuler.Z) : defaultEuler.Z;

            return FromEulerAngles(new Vector3(rx, ry, rz));
        }
    }
}