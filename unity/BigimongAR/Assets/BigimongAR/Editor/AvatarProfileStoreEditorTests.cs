using System;
using System.Collections.Generic;
using System.IO;
using Bigimong.AR;

namespace Bigimong.AR.EditorChecks
{
    public static class AvatarProfileStoreEditorChecks
    {
        public static void Run()
        {
            NormalizeProfileValues();
            RecoverMalformedProfile();
            PreserveProfileAfterFailedPromotion();
            CleanTemporaryFileAfterPromotionFailure();
        }

        private static void NormalizeProfileValues()
        {
            var profile = new AvatarProfile
            {
                schemaVersion = 99,
                displayName = "  Mina  ",
                bodyType = "OTHER",
                faceShapeId = 8,
                skinToneId = 0,
                eyebrowId = 9,
                eyeColorId = 0,
                hairStyleId = 18,
                hairColorId = 0,
            };

            profile.Normalize();

            AssertEqual(AvatarProfile.CurrentSchemaVersion, profile.schemaVersion, "schema version");
            AssertEqual("Mina", profile.displayName, "display name");
            AssertEqual("MASCULINE", profile.bodyType, "body type");
            AssertEqual(5, profile.faceShapeId, "face shape");
            AssertEqual(1, profile.skinToneId, "skin tone");
            AssertEqual(6, profile.eyebrowId, "eyebrow");
            AssertEqual(1, profile.eyeColorId, "eye color");
            AssertEqual(12, profile.hairStyleId, "hair style");
            AssertEqual(1, profile.hairColorId, "hair color");
        }

        private static void RecoverMalformedProfile()
        {
            const string path = "profiles/avatar.json";
            var files = new FakeProfileFileSystem();
            files.Put(path, "{ malformed-json");
            files.Put(path + ".tmp", "partial-write");
            var store = new AvatarProfileStore(path, files);

            var profile = store.LoadProfile();

            AssertEqual("플레이어", profile.displayName, "recovered display name");
            AssertFalse(store.HasSavedProfileAtPath, "malformed data must not count as saved");
            AssertFalse(files.FileExists(path), "malformed profile must be removed");
            AssertFalse(files.FileExists(path + ".tmp"), "stale temporary profile must be removed");
        }

        private static void PreserveProfileAfterFailedPromotion()
        {
            const string path = "profiles/avatar.json";
            var files = new FakeProfileFileSystem();
            var store = new AvatarProfileStore(path, files);
            AssertTrue(store.SaveProfile(new AvatarProfile { displayName = "First" }), "initial profile must save");
            var original = files.ReadAllText(path);

            files.FailReplace = true;
            AssertFalse(store.SaveProfile(new AvatarProfile { displayName = "Second" }), "replacement failure must fail save");

            AssertEqual(original, files.ReadAllText(path), "failed replacement must preserve prior profile");
            AssertTrue(store.HasSavedProfileAtPath, "prior saved profile must remain marked saved");
            AssertFalse(files.FileExists(path + ".tmp"), "failed replacement must clean temporary profile");
        }

        private static void CleanTemporaryFileAfterPromotionFailure()
        {
            const string path = "profiles/avatar.json";
            var files = new FakeProfileFileSystem { FailMove = true };
            var store = new AvatarProfileStore(path, files);

            AssertFalse(store.SaveProfile(new AvatarProfile { displayName = "Mina" }), "initial promotion failure must fail save");

            AssertFalse(store.HasSavedProfileAtPath, "failed initial save must remain unsaved");
            AssertFalse(files.FileExists(path), "failed initial save must not create profile");
            AssertFalse(files.FileExists(path + ".tmp"), "failed initial save must clean temporary profile");
        }

        private static void AssertTrue(bool value, string message)
        {
            if (!value)
                throw new InvalidOperationException(message);
        }

        private static void AssertFalse(bool value, string message)
        {
            AssertTrue(!value, message);
        }

        private static void AssertEqual<T>(T expected, T actual, string field)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"Unexpected {field}: expected '{expected}', got '{actual}'.");
        }

        private sealed class FakeProfileFileSystem : IAvatarProfileFileSystem
        {
            private readonly Dictionary<string, string> files = new Dictionary<string, string>();

            public bool FailMove { get; set; }
            public bool FailReplace { get; set; }

            public bool FileExists(string path) => files.ContainsKey(path);
            public void CreateDirectory(string path) { }
            public string ReadAllText(string path) => files[path];
            public void WriteAllText(string path, string contents) => files[path] = contents;

            public void Replace(string sourcePath, string destinationPath)
            {
                if (FailReplace)
                    throw new IOException("Injected replace failure.");
                files[destinationPath] = files[sourcePath];
                files.Remove(sourcePath);
            }

            public void Move(string sourcePath, string destinationPath)
            {
                if (FailMove)
                    throw new IOException("Injected move failure.");
                files[destinationPath] = files[sourcePath];
                files.Remove(sourcePath);
            }

            public void Delete(string path) => files.Remove(path);
            public void Put(string path, string contents) => files[path] = contents;
        }
    }
}
