using System;
using System.Collections.Generic;
using TimelineAnimator.Data;

namespace TimelineAnimator;

public static class Clipboard
{
    public class CopiedKeyframe
    {
        public ITrackKeyframe Keyframe { get; }
        public int TrackIndex { get; }

        public CopiedKeyframe(ITrackKeyframe keyframe, int trackIndex)
        {
            Keyframe = keyframe;
            TrackIndex = trackIndex;
        }
    }

    private static List<CopiedKeyframe> data = new();

    public static bool HasData => data.Count > 0;

    public static void Copy(List<ITrackKeyframe> keyframes, Dictionary<Guid, int> trackMap)
    {
        data.Clear();
        foreach (var kf in keyframes)
        {
            if (trackMap.TryGetValue(kf.Id, out var trackIndex))
            {
                data.Add(new CopiedKeyframe(kf, trackIndex));
            }
        }
    }

    public static List<CopiedKeyframe> GetKeyframesForPasting() => data;
}