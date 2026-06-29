using System.Collections.Generic;
using TimelineAnimator.Data;

namespace TimelineAnimator.ImSequencer
{
    public interface ISequencer
    {
        string Name { get; }
        TimelineSequence Sequence { get; }
        ImSequencerState State { get; }

        Dictionary<string, TransformState> DefaultPose { get; set; }

        bool IsVisible { get; set; }
        void Draw(ImSequencerCore uiCore, ref int currentFrame, ref int selectedEntry, bool modifierHeld);
        void ApplyPose(int frame);
        void RebuildHierarchy();
        
        void DrawInspector(int currentFrame);
    }
}