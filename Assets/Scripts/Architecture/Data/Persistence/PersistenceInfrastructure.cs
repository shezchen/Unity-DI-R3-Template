using System;
using System.IO;

namespace Architecture.Data.Persistence
{
    public interface IFileStore
    {
        bool Exists(string path);
        string ReadAllText(string path);
        void WriteAllTextAtomic(string path, string contents, string backupPath);
    }

    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    public sealed class PhysicalFileStore : IFileStore
    {
        public bool Exists(string path) => File.Exists(path);

        public string ReadAllText(string path) => File.ReadAllText(path);

        public void WriteAllTextAtomic(string path, string contents, string backupPath)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = path + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, contents);

                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, backupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }

    public sealed class PersistencePaths
    {
        private const string SettingsFileName = "Settings.v1.json";

        public PersistencePaths(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("Persistence root path cannot be empty.", nameof(rootPath));
            }

            RootPath = rootPath;
        }

        public string RootPath { get; }
        public string SettingsPath => Path.Combine(RootPath, SettingsFileName);
        public string SettingsBackupPath => SettingsPath + ".bak";

        public string GetGameSavePath(int slotIndex)
        {
            ValidateSlot(slotIndex);
            return Path.Combine(RootPath, $"SaveSlot{slotIndex}.v1.json");
        }

        public string GetGameSaveBackupPath(int slotIndex) => GetGameSavePath(slotIndex) + ".bak";

        private static void ValidateSlot(int slotIndex)
        {
            if (slotIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Save slot cannot be negative.");
            }
        }
    }
}
