using TimelineAnimator.Data;

namespace TimelineAnimator.ImSequencer
{
    public interface ISequencer
    {
        string Name { get; }
        AnimationClip Clip { get; }
        bool IsVisible { get; set; }
        void Draw(ImSequencerCore uiCore, ref int currentFrame, ref int selectedEntry, bool modifierHeld);
        void ApplyPose(int frame);
        void RebuildHierarchy();
        void DrawInspector(int currentFrame);
    }
}