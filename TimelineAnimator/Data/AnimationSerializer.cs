using System;
using System.IO;
using System.Numerics;

namespace TimelineAnimator.Data
{
    public static class AnimationSerializer
    {
        public static void Save(string path, AnimationFile animationFile)
        {
            using var stream = File.Open(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);

            writer.Write(Constants.FileFormat.Signature);
            writer.Write(Constants.FileFormat.Version);

            writer.Write(animationFile.StartFrame);
            writer.Write(animationFile.EndFrame);

            writer.Write(animationFile.BaseState.Count);
            foreach (var (TrackName, transformState) in animationFile.BaseState)
            {
                writer.Write(TrackName);
                WriteTransformState(writer, transformState);
            }

            writer.Write(animationFile.Tracks.Count);
            foreach (var track in animationFile.Tracks)
            {
                writer.Write(track.TrackName);
                writer.Write(track.Keyframes.Count);
                foreach (var keyframe in track.Keyframes)
                {
                    writer.Write(keyframe.Frame);
                    WriteTransformState(writer, keyframe.Transform);

                    writer.Write((int)keyframe.Shape);
                    writer.Write(keyframe.CustomColor.HasValue);
                    if (keyframe.CustomColor.HasValue) writer.Write(keyframe.CustomColor.Value);

                    writer.Write(keyframe.P1.X);
                    writer.Write(keyframe.P1.Y);
                    writer.Write(keyframe.P2.X);
                    writer.Write(keyframe.P2.Y);
                }
            }
        }

        public static AnimationFile Load(string path)
        {
            var animationFile = new AnimationFile();
            using var stream = File.Open(path, FileMode.Open);
            using var reader = new BinaryReader(stream);

            if (reader.ReadString() != Constants.FileFormat.Signature) throw new Exception("Invalid file format.");

            int version = reader.ReadInt32();
            if (version != Constants.FileFormat.Version) throw new Exception("Invalid file version.");

            animationFile.StartFrame = reader.ReadInt32();
            animationFile.EndFrame = reader.ReadInt32();

            var BaseStateCount = reader.ReadInt32();
            for (var i = 0; i < BaseStateCount; i++)
            {
                var TrackName = reader.ReadString();
                animationFile.BaseState[TrackName] = ReadTransformState(reader);
            }

            var trackCount = reader.ReadInt32();
            for (var i = 0; i < trackCount; i++)
            {
                var track = new AnimationTrack { TrackName = reader.ReadString() };
                var keyframeCount = reader.ReadInt32();
                for (var j = 0; j < keyframeCount; j++)
                {
                    var keyframe = new AnimationKeyframe { Frame = reader.ReadInt32() };

                    keyframe.Transform = ReadTransformState(reader);
                    keyframe.Shape = (KeyframeShape)reader.ReadInt32();
                    keyframe.CustomColor = reader.ReadBoolean() ? reader.ReadUInt32() : null;
                    keyframe.P1 = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                    keyframe.P2 = new Vector2(reader.ReadSingle(), reader.ReadSingle());

                    track.Keyframes.Add(keyframe);
                }

                animationFile.Tracks.Add(track);
            }

            return animationFile;
        }

        private static void WriteTransformState(BinaryWriter writer, TransformState state)
        {
            writer.Write(state.Position.X);
            writer.Write(state.Position.Y);
            writer.Write(state.Position.Z);
            writer.Write(state.Rotation.X);
            writer.Write(state.Rotation.Y);
            writer.Write(state.Rotation.Z);
            writer.Write(state.Rotation.W);
            writer.Write(state.Scale.X);
            writer.Write(state.Scale.Y);
            writer.Write(state.Scale.Z);
            writer.Write(state.FieldOfView);
        }

        private static TransformState ReadTransformState(BinaryReader reader)
        {
            return new TransformState
            {
                Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                    reader.ReadSingle()),
                Scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                FieldOfView = reader.ReadSingle()
            };
        }
    }
}