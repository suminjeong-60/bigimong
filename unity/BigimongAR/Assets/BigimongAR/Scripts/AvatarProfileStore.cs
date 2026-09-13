using System;
using System.IO;
using UnityEngine;

namespace Bigimong.AR
{
    public interface IAvatarProfileFileSystem
    {
        bool FileExists(string path);
        void CreateDirectory(string path);
        string ReadAllText(string path);
        void WriteAllText(string path, string contents);
        void Replace(string sourcePath, string destinationPath);
        void Move(string sourcePath, string destinationPath);
        void Delete(string path);
    }

    public sealed class AvatarProfileStore
    {
        private const string FileName = "avatar-profile-v1.json";
        private static AvatarProfileStore defaultStore;

        private readonly string profilePath;
        private readonly string temporaryPath;
        private readonly IAvatarProfileFileSystem fileSystem;

        public static bool HasSavedProfile { get; private set; }
        public bool HasSavedProfileAtPath { get; private set; }

        public AvatarProfileStore(string profilePath)
            : this(profilePath, new SystemAvatarProfileFileSystem())
        {
        }

        public AvatarProfileStore(string profilePath, IAvatarProfileFileSystem fileSystem)
        {
            if (string.IsNullOrWhiteSpace(profilePath))
                throw new ArgumentException("A profile path is required.", nameof(profilePath));
            this.profilePath = profilePath;
            temporaryPath = profilePath + ".tmp";
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public static AvatarProfile Load()
        {
            var profile = DefaultStore.LoadProfile();
            HasSavedProfile = DefaultStore.HasSavedProfileAtPath;
            return profile;
        }

        public static bool Save(AvatarProfile profile)
        {
            var saved = DefaultStore.SaveProfile(profile);
            HasSavedProfile = DefaultStore.HasSavedProfileAtPath;
            return saved;
        }

        public AvatarProfile LoadProfile()
        {
            HasSavedProfileAtPath = false;
            try
            {
                if (!fileSystem.FileExists(profilePath))
                    return AvatarProfile.CreateDefault();

                var profile = JsonUtility.FromJson<AvatarProfile>(fileSystem.ReadAllText(profilePath));
                if (profile == null)
                    throw new InvalidDataException("Avatar profile JSON was empty.");

                profile.Normalize();
                HasSavedProfileAtPath = true;
                return profile;
            }
            catch (Exception)
            {
                RecoverFromInvalidProfile();
                return AvatarProfile.CreateDefault();
            }
        }

        public bool SaveProfile(AvatarProfile profile)
        {
            if (profile == null)
                return false;

            profile.Normalize();
            try
            {
                var directory = Path.GetDirectoryName(profilePath);
                if (!string.IsNullOrEmpty(directory))
                    fileSystem.CreateDirectory(directory);
                fileSystem.WriteAllText(temporaryPath, JsonUtility.ToJson(profile));
                if (fileSystem.FileExists(profilePath))
                    fileSystem.Replace(temporaryPath, profilePath);
                else
                    fileSystem.Move(temporaryPath, profilePath);

                HasSavedProfileAtPath = true;
                return true;
            }
            catch (Exception)
            {
                DeleteTemporaryFile();
                return false;
            }
        }

        private static AvatarProfileStore DefaultStore
        {
            get
            {
                if (defaultStore == null)
                {
                    var path = Path.Combine(Application.persistentDataPath, FileName);
                    defaultStore = new AvatarProfileStore(path);
                }

                return defaultStore;
            }
        }

        private void RecoverFromInvalidProfile()
        {
            HasSavedProfileAtPath = false;
            try
            {
                if (fileSystem.FileExists(profilePath))
                    fileSystem.Delete(profilePath);
            }
            catch (Exception)
            {
                // Recovery must still return a usable default profile when storage is unavailable.
            }

            DeleteTemporaryFile();
        }

        private void DeleteTemporaryFile()
        {
            try
            {
                if (fileSystem.FileExists(temporaryPath))
                    fileSystem.Delete(temporaryPath);
            }
            catch (Exception)
            {
                // A later load can safely ignore a stale temporary file.
            }
        }

        private sealed class SystemAvatarProfileFileSystem : IAvatarProfileFileSystem
        {
            public bool FileExists(string path) => File.Exists(path);
            public void CreateDirectory(string path) => Directory.CreateDirectory(path);
            public string ReadAllText(string path) => File.ReadAllText(path);
            public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
            public void Replace(string sourcePath, string destinationPath) => File.Replace(sourcePath, destinationPath, null);
            public void Move(string sourcePath, string destinationPath) => File.Move(sourcePath, destinationPath);
            public void Delete(string path) => File.Delete(path);
        }
    }
}
