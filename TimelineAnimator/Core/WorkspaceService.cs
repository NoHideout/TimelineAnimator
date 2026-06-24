using System;
using System.Collections.Generic;
using TimelineAnimator.Data;
using TimelineAnimator.ImSequencer;

namespace TimelineAnimator;

public class WorkspaceService
{
    public event Action<List<ITrackKeyframe>?>? EditEasingRequested;
    public void RequestEditEasing(List<ITrackKeyframe>? keyframes) => EditEasingRequested?.Invoke(keyframes);
    
    public int SharedSelectedEntry { get; set; } = -1;
    public int ActiveSequencerIndex { get; set; } = -1;

    public ISequencer? GetActiveSequencer()
    {
        var sequencers = Services.ProjectService.Sequencers;
        if (sequencers.Count == 0 || ActiveSequencerIndex < 0 || ActiveSequencerIndex >= sequencers.Count)
            return null;

        return sequencers[ActiveSequencerIndex];
    }

    //Todo sub in instead of manually clearing
    public void ClearWorkspace()
    {
        ActiveSequencerIndex = -1;
        SharedSelectedEntry = -1;
    }
}