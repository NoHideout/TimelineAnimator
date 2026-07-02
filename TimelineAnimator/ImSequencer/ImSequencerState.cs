using System;
using System.Collections.Generic;
using System.Numerics;
using TimelineAnimator.Data;

namespace TimelineAnimator.ImSequencer
{
    public struct SelectedKeyframe : IEquatable<SelectedKeyframe>
    {
        public int trackIndex;
        public Guid keyframeId;

        public SelectedKeyframe(int trackIndex, Guid keyframeId)
        {
            this.trackIndex = trackIndex;
            this.keyframeId = keyframeId;
        }

        public bool Equals(SelectedKeyframe other)
        {
            return trackIndex == other.trackIndex && keyframeId.Equals(other.keyframeId);
        }
    }

    public class ImSequencerState
    {
        public float framePixelWidth = 10f;
        public HashSet<SelectedKeyframe> SelectedKeyframes = new();
        public float LegendWidth = 180f;

        public bool IsDraggingSplitter = false;
        public bool MovingCurrentFrame = false;
        public int movingPos = -1;
        public bool IsDragging = false;
        
        public bool IsBoxSelecting = false;
        public Vector2 BoxSelectionStart;
        public Vector2 BoxSelectionEnd;
        
        public ZoomScrollbar.State ZoomState = new();
        
        internal int contextTrackIndex = -1;
        internal CurveKeyframe? contextKeyframe = null;
        internal int contextMouseFrame = -1;
    }
}