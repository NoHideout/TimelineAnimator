using System;
using System.IO;
using System.Linq;
using TimelineAnimator.Sequencers;

namespace TimelineAnimator.Core;

public class AutosaveService
{
    private float timeSinceLastSave = 0f;

    public void Update(float Dt)
    {
        if (!Services.Configuration.EnableAutosave) return;
        var activeSeq = Services.WorkspaceService.GetActiveSequencer() as SequencerBase;
        
        if (activeSeq == null || activeSeq.Clip.Objects.Count == 0) return;
        
        timeSinceLastSave += Dt;
        float saveIntervalSeconds = Services.Configuration.AutosaveIntervalMinutes * 60f;
        if (timeSinceLastSave >= saveIntervalSeconds)
        {
            PerformAutoSave(activeSeq);
            timeSinceLastSave = 0f;
        }
    }

    private void PerformAutoSave(SequencerBase activeSeq)
    {
        try
        {
            string dir = string.IsNullOrWhiteSpace(Services.Configuration.AutosaveDirectory) ? Services.PluginInterface.GetPluginConfigDirectory() : Services.Configuration.AutosaveDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string safeName = string.Join("_", activeSeq.Name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
            string fileName = $"autosave_{safeName}_{timestamp}.xivanim";
            string fullPath = Path.Combine(dir, fileName);
            Services.ProjectService.SaveAnimation(fullPath, activeSeq);
            Services.Log.Debug($"Autosaved to: {fullPath}");

            CleanupOldAutosaves(dir, safeName);
        }
        catch (Exception e)
        {
            Services.Log.Error(e, "Failed to perform autosave.");
        }
    }

    private void CleanupOldAutosaves(string dir, string safeName)
    {
        int maxBackups = Math.Max(1, Services.Configuration.MaxAutosaveBackups);
        var dirInfo = new DirectoryInfo(dir);

        var autosaves = dirInfo.GetFiles($"autosave_{safeName}_*.xivanim")
            .OrderByDescending(f => f.CreationTime)
            .ToList();
        if (autosaves.Count > maxBackups)
        {
            var filesToDelete = autosaves.Skip(maxBackups);
            foreach (var file in filesToDelete)
            {
                try
                {
                    file.Delete();
                }
                catch (Exception e)
                {
                    Services.Log.Error(e, "Failed to delete autosave.");
                }
            }
        }
    }
}