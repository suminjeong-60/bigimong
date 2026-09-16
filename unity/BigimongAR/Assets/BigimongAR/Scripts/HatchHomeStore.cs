using System;
using System.IO;
using UnityEngine;

namespace Bigimong.AR
{
    public interface IHatchHomeStore
    {
        HatchLoadResult Load();
        // True: the candidate is durably recoverable. False: the candidate was not committed.
        // A caller publishes its candidate only on true; replica catch-up is not a new commit.
        // An unknown outcome throws HatchSaveOutcomeUnknownException and requires Load before retry.
        bool TrySave(HatchHomeSnapshot snapshot);
    }

    public sealed class HatchSaveOutcomeUnknownException : IOException
    {
        public HatchSaveOutcomeUnknownException(Exception inner)
            : base("Hatch commit outcome is unknown; reload before another transaction.", inner) { }
    }

    public interface IHatchHomeFileSystem
    {
        bool Exists(string path);
        string ReadAllText(string path);
        void WriteAllText(string path, string contents);
        void Copy(string source, string destination, bool overwrite);
        void Move(string source, string destination, bool overwrite);
        void Delete(string path);
    }

    public readonly struct HatchLoadResult
    {
        public HatchHomeSnapshot Snapshot { get; }
        public bool Recovered { get; }
        public string Source { get; }

        public HatchLoadResult(HatchHomeSnapshot snapshot, bool recovered, string source)
        {
            Snapshot = snapshot;
            Recovered = recovered;
            Source = source;
        }
    }

    public sealed class HatchHomeStore : IHatchHomeStore
    {
        private const string PrimaryName = "bigimong-hatch-home-v2.json";
        private const string BackupName = "bigimong-hatch-home-v2.backup.json";
        private const string TemporaryName = "bigimong-hatch-home-v2.tmp.json";
        private const string LegacyName = "bigimong-offline-progress-v1.json";

        private readonly IHatchHomeFileSystem fileSystem;
        private readonly string primaryPath;
        private readonly string backupPath;
        private readonly string temporaryPath;
        private readonly string legacyPath;
        private bool reloadRequired;

        public HatchHomeStore()
            : this(Application.persistentDataPath, new SystemHatchHomeFileSystem())
        {
        }

        public HatchHomeStore(string persistentDataPath, IHatchHomeFileSystem fileSystem)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
                throw new ArgumentException("A persistent data path is required.", nameof(persistentDataPath));

            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            primaryPath = Path.Combine(persistentDataPath, PrimaryName);
            backupPath = Path.Combine(persistentDataPath, BackupName);
            temporaryPath = Path.Combine(persistentDataPath, TemporaryName);
            legacyPath = Path.Combine(persistentDataPath, LegacyName);
        }

        public HatchLoadResult Load()
        {
            // Every instance must distinguish unavailable storage from corrupt bytes. Otherwise
            // a restart can accept stale primary while a newer committed backup is unreadable.
            TryReadValid(primaryPath, out var primary);
            TryReadValid(backupPath, out var backup);
            var newest = Newest(primary, backup);
            if (newest != null)
            {
                reloadRequired = false;
                return new HatchLoadResult(newest, newest == backup, newest == backup ? "backup" : "primary");
            }
            if (TryMigrateV1(out var migrated))
            {
                reloadRequired = false;
                return new HatchLoadResult(migrated, true, "v1");
            }

            var fresh = new HatchHomeSnapshot();
            fresh.Normalize();
            reloadRequired = false;
            return new HatchLoadResult(fresh, false, "fresh");
        }

        public bool TrySave(HatchHomeSnapshot snapshot)
        {
            if (snapshot == null || reloadRequired)
                return false;

            var candidateCommitted = false;
            string candidateJson = null;
            try
            {
                var candidate = snapshot.Clone();
                if (candidate == null || !candidate.HasValidDurableIdentity())
                    return false;

                candidate.Normalize();
                if (!candidate.HasValidDurableIdentity())
                    return false;

                // Read errors abort; malformed bytes are invalid. Never overwrite the only
                // newest copy left by an interrupted save before repairing the other replica.
                TryReadValid(primaryPath, out var primary);
                TryReadValid(backupPath, out var backup);
                var newest = Newest(primary, backup);
                if (newest?.revision == long.MaxValue) return false;
                candidate.revision = (newest?.revision ?? 0) + 1;
                if (newest != null)
                {
                    var previousJson = JsonUtility.ToJson(newest);
                    if (primary == null || JsonUtility.ToJson(primary) != previousJson)
                        PromoteValidated(previousJson, primaryPath);
                    if (backup == null || JsonUtility.ToJson(backup) != previousJson)
                        PromoteValidated(previousJson, backupPath);
                }

                // Backup is a redundant CURRENT snapshot, not the pre-selection ready state.
                // Its atomic promotion is the commit point. A crash before primary catches up
                // is recoverable because Load compares revisions instead of preferring stale primary.
                candidateJson = JsonUtility.ToJson(candidate);
                PromoteValidated(candidateJson, backupPath);
                candidateCommitted = true;
                PromoteValidated(candidateJson, primaryPath);

                return true;
            }
            catch (Exception)
            {
                // An adapter may throw just after its atomic move. Reconcile that boundary
                // against the exact validated candidate (including the store-assigned revision).
                // Once backup committed, later primary failures cannot turn success into rollback.
                try { return candidateCommitted || CandidateIsDurable(candidateJson); }
                catch (Exception recoveryFailure)
                {
                    reloadRequired = true;
                    throw new HatchSaveOutcomeUnknownException(recoveryFailure);
                }
            }
            finally
            {
                DeleteTemporary();
            }
        }

        private bool CandidateIsDurable(string candidateJson)
        {
            // Do not swallow a second I/O error here and misreport an unknown outcome as false.
            // With storage unavailable the caller must reload before retrying, not assume rollback.
            return candidateJson != null && TryReadValid(backupPath, out var backup) &&
                JsonUtility.ToJson(backup) == candidateJson;
        }

        private static HatchHomeSnapshot Newest(HatchHomeSnapshot primary, HatchHomeSnapshot backup) =>
            backup != null && (primary == null || backup.revision > primary.revision) ? backup : primary;

        private void PromoteValidated(string json, string destination)
        {
            fileSystem.WriteAllText(temporaryPath, json);
            if (!TryReadValid(temporaryPath, out var validated) || JsonUtility.ToJson(validated) != json)
                throw new IOException("Hatch snapshot read-back validation failed.");
            fileSystem.Move(temporaryPath, destination, true);
        }

        private bool TryReadValid(string path, out HatchHomeSnapshot snapshot)
        {
            snapshot = null;
            if (!fileSystem.Exists(path))
                return false;

            var json = fileSystem.ReadAllText(path);
            try
            {
                var raw = JsonUtility.FromJson<HatchHomeSnapshot>(json);
                if (raw == null || raw.schemaVersion != HatchHomeSnapshot.CurrentSchemaVersion || raw.revision < 0 ||
                    !raw.HasValidDurableIdentity())
                    return false;

                var normalized = raw.Clone();
                if (normalized == null)
                    return false;
                normalized.Normalize();
                if (normalized.schemaVersion != HatchHomeSnapshot.CurrentSchemaVersion ||
                    !normalized.HasValidDurableIdentity())
                    return false;

                snapshot = normalized;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool TryMigrateV1(out HatchHomeSnapshot snapshot)
        {
            snapshot = null;
            if (!fileSystem.Exists(legacyPath))
                return false;

            // I/O failure must escape so a transiently unreadable legacy save cannot be
            // replaced by fresh v2 state. Only malformed/invalid JSON is a corrupt replica.
            var legacyJson = fileSystem.ReadAllText(legacyPath);
            LegacyProgressV1 legacy;
            try
            {
                legacy = JsonUtility.FromJson<LegacyProgressV1>(legacyJson);
                if (legacy == null || legacy.schemaVersion != 1)
                    return false;

                snapshot = new HatchHomeSnapshot();
                // v1's explicit hatched flag already proves completion/ownership. Only this
                // migration may lift old progress; contradictory raw v2 snapshots are rejected.
                snapshot.eggProgress = legacy.hatched ? HatchHomeSnapshot.HatchTarget
                    : Mathf.Clamp(legacy.eggExperience, 0, HatchHomeSnapshot.HatchTarget);
                snapshot.selectedArtId = legacy.hatched ? Mathf.Clamp(legacy.selectedArtId, 1, 30) : 0;
                snapshot.phase = legacy.hatched ? nameof(HatchHomePhase.HOME)
                    : snapshot.eggProgress == HatchHomeSnapshot.HatchTarget ? nameof(HatchHomePhase.HATCH_READY)
                    : nameof(HatchHomePhase.EGG_ACTIVE);
                snapshot.hatchCheckpoint = legacy.hatched ? nameof(HatchCheckpoint.REVEALED) : nameof(HatchCheckpoint.NONE);
                snapshot.activeHomeView = legacy.hatched ? nameof(HomeSubject.DINOSAUR) : nameof(HomeSubject.EGG);
                snapshot.dragonExperience = legacy.dragonExperience;
                snapshot.coins = legacy.coins;
                snapshot.snacks = legacy.snacks;
                snapshot.dayKey = legacy.dayKey;
                snapshot.playsToday = legacy.playsToday;
                snapshot.giftClaimedToday = legacy.giftClaimedToday;
                snapshot.questClaimedToday = legacy.questClaimedToday;
                snapshot.soundEnabled = legacy.soundEnabled;
                snapshot.lastPlayTicks = legacy.lastPlayTicks;
                snapshot.Normalize();
                if (!snapshot.HasValidDurableIdentity())
                {
                    snapshot = null;
                    return false;
                }

            }
            catch (Exception)
            {
                snapshot = null;
                return false;
            }

            try { TrySave(snapshot); }
            catch (HatchSaveOutcomeUnknownException)
            {
                // This Load already validated the v1 source and the exact migrated payload.
                // Its ownership/counters are known regardless of whether the v2 write committed.
                // Return that payload, not fresh state; later writes still require readable replicas.
                reloadRequired = false;
            }
            return true;
        }

        private void DeleteTemporary()
        {
            try
            {
                if (fileSystem.Exists(temporaryPath))
                    fileSystem.Delete(temporaryPath);
            }
            catch (Exception)
            {
                // A later save can safely replace a stale temporary file.
            }
        }

        [Serializable]
        private sealed class LegacyProgressV1
        {
            public int schemaVersion;
            public int eggExperience;
            public bool hatched;
            public int dragonExperience;
            public int coins;
            public int snacks;
            public int selectedArtId;
            public int dayKey;
            public int playsToday;
            public bool giftClaimedToday;
            public bool questClaimedToday;
            public bool soundEnabled = true;
            public long lastPlayTicks;
        }

        private sealed class SystemHatchHomeFileSystem : IHatchHomeFileSystem
        {
            public bool Exists(string path)
            {
                try
                {
                    File.GetAttributes(path);
                    return true;
                }
                catch (FileNotFoundException) { return false; }
                catch (DirectoryNotFoundException) { return false; }
            }
            public string ReadAllText(string path) => File.ReadAllText(path);
            public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
            public void Copy(string source, string destination, bool overwrite) => File.Copy(source, destination, overwrite);
            public void Move(string source, string destination, bool overwrite)
            {
                if (!overwrite || !File.Exists(destination))
                {
                    File.Move(source, destination);
                    return;
                }

                File.Replace(source, destination, null);
            }
            public void Delete(string path) => File.Delete(path);
        }
    }
}
