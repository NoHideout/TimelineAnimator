using System;
using System.Collections.Generic;
using TimelineAnimator.Data;

namespace TimelineAnimator
{
    public static class Clipboard
    {
        public class CopiedKeyframe
        {
            public CurveKeyframe Keyframe { get; }
            public Guid TrackId { get; }

            public CopiedKeyframe(CurveKeyframe keyframe, Guid trackId)
            {
                Keyframe = keyframe;
                TrackId = trackId;
            }
        }

        private static List<CopiedKeyframe> data = new();

        public static bool HasData => data.Count > 0;

        public static void Copy(List<CurveKeyframe> keyframes, Dictionary<Guid, Guid> trackMap)
        {
            data.Clear();
            foreach (var kf in keyframes)
            {
                if (trackMap.TryGetValue(kf.Id, out var trackId))
                {
                    data.Add(new CopiedKeyframe(kf, trackId));
                }
            }
        }

        public static List<CopiedKeyframe> GetKeyframesForPasting() => data;
    }
}