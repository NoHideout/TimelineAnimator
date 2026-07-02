namespace TimelineAnimator.Data
{
    public class AnimationFile
    {
        public int Version { get; set; } = 1;
        public string AnimationType { get; set; } = "Actor";
        public AnimationClip Clip { get; set; } = new();
    }
}