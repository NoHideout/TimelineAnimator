using System;
using System.IO;
using Newtonsoft.Json;

namespace TimelineAnimator.Data
{
    public static class AnimationSerializer
    {
        public const int CurrentVersion = 1;

        private static readonly JsonSerializerSettings Settings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
        };

        public static void Save(string path, AnimationFile animationFile)
        {
            animationFile.Version = CurrentVersion;
            
            string json = JsonConvert.SerializeObject(animationFile, Settings);
            File.WriteAllText(path, json);
        }

        public static AnimationFile Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Could not find animation file at {path}");

            string json = File.ReadAllText(path);
            var animationFile = JsonConvert.DeserializeObject<AnimationFile>(json, Settings);

            if (animationFile == null)
                throw new Exception("Failed to deserialize animation file.");

            if (animationFile.Version > CurrentVersion)
            {
                throw new Exception($"Cannot load file. It was created in version {animationFile.Version}, but you are running version {CurrentVersion}.");
            }
            
            // could mig here
            
            return animationFile;
        }
    }
}