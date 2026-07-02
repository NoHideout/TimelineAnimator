using System;
using System.Collections.Generic;
using TimelineAnimator.Data;
using TimelineAnimator.ImSequencer;

namespace TimelineAnimator.Core
{
    public class WorkspaceService
    {
        public event Action<List<CurveKeyframe>?>? EditGraphRequested;
        public void RequestEditGraph(List<CurveKeyframe>? keyframes) => EditGraphRequested?.Invoke(keyframes);

        public int SharedSelectedEntry { get; set; } = -1;
        public int ActiveSequencerIndex { get; set; } = -1;
        
        public Guid SelectedObjectId { get; set; } = Guid.Empty;
        public PropertyType? SelectedProperty { get; set; } = null;

        public ISequencer? GetActiveSequencer()
        {
            var sequencers = Services.ProjectService.Sequencers;
            if (sequencers.Count == 0 || ActiveSequencerIndex < 0 || ActiveSequencerIndex >= sequencers.Count)
                return null;
            
            return sequencers[ActiveSequencerIndex];
        }
    }
}