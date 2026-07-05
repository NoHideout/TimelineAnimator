using System;
using System.IO;
using System.Linq;
using TimelineAnimator.ImSequencer;
using TimelineAnimator.Sequencers;

namespace TimelineAnimator.Core;

public class AutosaveService
{
    private float timeSinceLastSave = 0f;

    public void Update(float dt)
    {
        if (!Services.Configuration.EnableAutosave) return;
        
        if (Services.ProjectService.Sequencers.Count == 0) return;
        
        timeSinceLastSave += dt;
        float saveIntervalSeconds = Services.Configuration.AutosaveIntervalMinutes * 60f;
        if (timeSinceLastSave >= saveIntervalSeconds)
        {
            PerformAutoSave();
            timeSinceLastSave = 0f;
        }
    }

    private void PerformAutoSave()
    {
        foreach (var sequencer in Services.ProjectService.Sequencers)
        {
            SaveSequencer(sequencer);
        }
    }

    private void SaveSequencer(ISequencer sequencer)
    {
        try
        {
            string dir = string.IsNullOrWhiteSpace(Services.Configuration.AutosaveDirectory) ? Services.PluginInterface.GetPluginConfigDirectory() : Services.Configuration.AutosaveDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string safeName = string.Join("_", sequencer.Name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
            string fileName = $"autosave_{safeName}_{timestamp}.xivanim";
            string fullPath = Path.Combine(dir, fileName);
            Services.ProjectService.SaveAnimation(fullPath, sequencer);
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