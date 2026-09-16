#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Bigimong.AR;
using UnityEngine;

namespace Bigimong.AR.EditorChecks
{
    public static class HatchHomeStoreEditorChecks
    {
        private const string Root = "/hatch-home-checks";
        private const string PrimaryName = "bigimong-hatch-home-v2.json";
        private const string BackupName = "bigimong-hatch-home-v2.backup.json";
        private const string TemporaryName = "bigimong-hatch-home-v2.tmp.json";
        private const string LegacyName = "bigimong-offline-progress-v1.json";
        private const string AvatarName = "avatar-profile-v1.json";

        public static void RunBehaviorChecks()
        {
            CommitResultTracksEveryPersistenceBoundary();
            CommittedCareStepsAndSideActivitiesPublishNewestState();
            UnknownOutcomeRequiresReloadBeforeAnyFurtherWrite();
            CoordinatorRecoversUnknownOutcomeBeforeRetry();
            RestartedStoreFailsClosedUntilNewestReplicaRecovers();
            CoordinatorInitialLoadRetriesNewestReplica();
            SystemFileSystemExistsPreservesProbeErrors();
            RawPhaseProgressMismatchLosesToValidReplica();
            InvalidPhaseProgressCannotBeSavedAsOwnership();
            SaveRoundTripsAndCreatesFirstBackup();
            LoadPrefersPrimaryThenRecoversFromBackup();
            LaterSaveRedundantlyCommitsNewestSnapshot();
            Selected17SurvivesPrimaryCorruptionBeforeCheckpoint();
            EveryRedundancyBoundaryPreservesSelection();
            TornOrSubstitutedTemporaryWritesCannotPublishUnvalidatedSelection();
            NewerBackupWinsAndIsProtectedBeforeNextWrite();
            LegacyV2RevisionZeroAndEqualRevisionPrimaryWin();
            FailedPromotionLeavesDurableIdentityRecoverableAndCallerUntouched();
            TempWriteFailurePreservesValidPrimary();
            TempReadFailurePreservesValidPrimaryAndCleansTemp();
            ExistingPrimaryReadFailurePreservesValidPrimaryAndCleansTemp();
            BackupPromotionFailurePreservesPrimaryBackupAndCleansTemp();
            PrimaryPromotionFailureRecoversNewerBackup();
            FirstSaveBackupFailureDoesNotPromotePrimary();
            CleanupDeleteFailureIsBestEffort();
            RawNumericIdentityIsRejectedBeforeNormalization();
            MigrateReadyEggWithoutClaimingAnArtId();
            MigrateHatchedHomeWithoutTouchingAvatarBytes();
            LegacyHatchedProgressIsCompletedWithoutChangingOwnership();
            MigrationWriteAmbiguityRetainsValidatedLegacyState();
        }

        private static void SaveRoundTripsAndCreatesFirstBackup()
        {
            var fileSystem = new FakeHatchHomeFileSystem();
            var store = new HatchHomeStore(Root, fileSystem);
            Require(store.Load().Source == "fresh", "first load must be fresh");

            var snapshot = new HatchHomeSnapshot { eggProgress = 12500 };
            snapshot.coins = -4;

            Require(store.TrySave(snapshot), "valid v2 save must succeed");
            Require(store.Load().Snapshot.eggProgress == 12500, "primary round trip failed");
            Require(fileSystem.Exists(BackupName), "first save must create an immediate backup");
            Require(snapshot.coins == -4, "successful save must not normalize the caller");
        }

        private static void LoadPrefersPrimaryThenRecoversFromBackup()
        {
            var fileSystem = new FakeHatchHomeFileSystem();
            var store = new HatchHomeStore(Root, fileSystem);
            Require(store.TrySave(HomeSnapshot(7)), "recovery fixture save must succeed");

            fileSystem.Seed(BackupName, JsonUtility.ToJson(HomeSnapshot(11)));
            var primary = store.Load();
            Require(primary.Source == "primary" && primary.Snapshot.selectedArtId == 7 && !primary.Recovered,
                "valid primary must win over backup");

            fileSystem.Corrupt(PrimaryName);
            var backup = store.Load();
            Require(backup.Source == "backup", "corrupt primary must use backup");
            Require(backup.Snapshot.selectedArtId == 11 && backup.Recovered,
                "backup recovery must return the literal backup identity");
        }

        private static void LaterSaveRedundantlyCommitsNewestSnapshot()
        {
            var fileSystem = new FakeHatchHomeFileSystem();
            var store = new HatchHomeStore(Root, fileSystem);
            Require(store.TrySave(HomeSnapshot(7)), "first version save must succeed");
            Require(store.TrySave(HomeSnapshot(9)), "second version save must succeed");

            fileSystem.Corrupt(PrimaryName);
            var recovered = store.Load();
            Require(recovered.Source == "backup" && recovered.Snapshot.selectedArtId == 9 && recovered.Snapshot.revision == 2,
                "later save must immediately recover the newly committed identity and revision");
        }

        private static HatchHomeSnapshot Ready() => new() { phase = "HATCH_READY", eggProgress = 30000 };

        // Mutations caught: false after commit, true before commit, stale-primary load, or caller mutation.
        private static void CommitResultTracksEveryPersistenceBoundary()
        {
            foreach (var existing in new[] { false, true })
            {
                var boundaries = new List<(string operation, string path, int occurrence, bool after, bool committed)>
                {
                    ("exists", PrimaryName, 1, false, false), ("exists", PrimaryName, 1, true, false),
                    ("exists", BackupName, 1, false, false), ("exists", BackupName, 1, true, false),
                    ("write", TemporaryName, 1, false, false), ("write", TemporaryName, 1, true, false),
                    ("exists", TemporaryName, 1, false, false), ("exists", TemporaryName, 1, true, false),
                    ("read", TemporaryName, 1, false, false), ("read", TemporaryName, 1, true, false),
                    ("move", BackupName, 1, false, false), ("move", BackupName, 1, true, true),
                    ("write", TemporaryName, 2, false, true), ("write", TemporaryName, 2, true, true),
                    ("exists", TemporaryName, 2, false, true), ("exists", TemporaryName, 2, true, true),
                    ("read", TemporaryName, 2, false, true), ("read", TemporaryName, 2, true, true),
                    ("move", PrimaryName, 1, false, true), ("move", PrimaryName, 1, true, true),
                    ("exists", TemporaryName, 3, false, true), ("exists", TemporaryName, 3, true, true),
                };
                if (existing)
                    boundaries.AddRange(new[]
                    {
                        ("read", PrimaryName, 1, false, false), ("read", PrimaryName, 1, true, false),
                        ("read", BackupName, 1, false, false), ("read", BackupName, 1, true, false),
                    });
                foreach (var boundary in boundaries)
                {
                    var fs = new FakeHatchHomeFileSystem();
                    var store = new HatchHomeStore(Root, fs);
                    if (existing) Require(store.TrySave(new HatchHomeSnapshot { eggProgress = 1000, coins = 7 }), "existing fixture");
                    var prior = JsonUtility.ToJson(store.Load().Snapshot);
                    var primaryBefore = fs.Exists(PrimaryName) ? fs.ReadAllText(PrimaryName) : null;
                    var backupBefore = fs.Exists(BackupName) ? fs.ReadAllText(BackupName) : null;
                    var candidate = new HatchHomeSnapshot { eggProgress = 1500, coins = 19, nextCareAtUtcTicks = 88 };
                    var callerBefore = JsonUtility.ToJson(candidate);
                    fs.FailAt(boundary.operation, boundary.path, boundary.occurrence, boundary.after);
                    var saved = store.TrySave(candidate);
                    Require(fs.FaultConsumed, "must execute exact boundary " + boundary);
                    Require(saved == boundary.committed, "boolean must represent candidate commit at " + boundary);
                    Require(JsonUtility.ToJson(candidate) == callerBefore, "store never mutates its caller");
                    var loaded = new HatchHomeStore(Root, fs).Load();
                    if (boundary.committed)
                    {
                        Require(loaded.Snapshot.eggProgress == 1500 && loaded.Snapshot.coins == 19 &&
                            loaded.Snapshot.nextCareAtUtcTicks == 88 && loaded.Snapshot.revision == (existing ? 2 : 1),
                            "true exposes the exact committed candidate, including on first save");
                        if (boundary.path != PrimaryName || !boundary.after)
                            if (boundary.operation != "exists" || boundary.occurrence != 3)
                                Require(loaded.Source == "backup", "incomplete primary catch-up loads newer backup");
                        fs.Corrupt(PrimaryName);
                        Require(store.Load().Snapshot.eggProgress == 1500, "true remains recoverable from candidate backup");
                    }
                    else
                    {
                        Require(JsonUtility.ToJson(loaded.Snapshot) == prior, "false keeps prior logical state");
                        Require((fs.Exists(PrimaryName) ? fs.ReadAllText(PrimaryName) : null) == primaryBefore &&
                            (fs.Exists(BackupName) ? fs.ReadAllText(BackupName) : null) == backupBefore,
                            "pre-commit failure cannot change synchronized durable replica bytes");
                    }
                }
                // A cleanup error after a committed candidate cannot change its truth value.
                var cleanupFs = new FakeHatchHomeFileSystem();
                var cleanupStore = new HatchHomeStore(Root, cleanupFs);
                if (existing) Require(cleanupStore.TrySave(new HatchHomeSnapshot()), "cleanup fixture");
                cleanupFs.FailAt("read", TemporaryName, 2, false);
                cleanupFs.ThrowOnNextDelete(TemporaryName);
                Require(cleanupStore.TrySave(new HatchHomeSnapshot { eggProgress = 500 }), "post-commit cleanup failure still returns true");
                Require(cleanupFs.FaultConsumed && cleanupFs.Exists(TemporaryName) && cleanupStore.Load().Snapshot.eggProgress == 500,
                    "stale temporary bytes never outrank committed backup");
            }
        }

        private static void CommittedCareStepsAndSideActivitiesPublishNewestState()
        {
            foreach (var existing in new[] { false, true })
                foreach (var action in new[] { "care", "steps", "gift" })
                    foreach (var boundary in new[]
                    {
                        ("write", TemporaryName, 1, false, false), ("read", TemporaryName, 1, false, false),
                        ("move", BackupName, 1, false, false), ("move", BackupName, 1, true, true),
                        ("write", TemporaryName, 2, false, true), ("write", TemporaryName, 2, true, true),
                        ("exists", TemporaryName, 2, false, true), ("exists", TemporaryName, 2, true, true),
                        ("read", TemporaryName, 2, false, true), ("read", TemporaryName, 2, true, true),
                        ("move", PrimaryName, 1, false, true), ("move", PrimaryName, 1, true, true),
                    })
                    {
                        var fs = new FakeHatchHomeFileSystem();
                        var store = new HatchHomeStore(Root, fs);
                        if (existing) Require(store.TrySave(new HatchHomeSnapshot()), "caller fixture");
                        var current = store.Load().Snapshot;
                        var before = JsonUtility.ToJson(current);
                        var progress = new HatchProgressService(store, new FixedClock());
                        var sides = new HomeSideActivityService(store);
                        fs.FailAt(boundary.Item1, boundary.Item2, boundary.Item3, boundary.Item4);
                        bool accepted;
                        HatchHomeSnapshot published;
                        if (action == "gift")
                        {
                            var result = sides.TryApply(current, HomeSideActivityAction.DailyGift, new FixedClock().UtcNow);
                            accepted = result.Accepted; published = result.Snapshot;
                        }
                        else
                        {
                            var result = action == "care" ? progress.TryCare(current) : progress.ApplyVerifiedSteps(current, Step(60, 60, "first"));
                            accepted = result.Accepted; published = result.Snapshot;
                        }
                        Require(fs.FaultConsumed && accepted == boundary.Item5, "service publishes according to real store commit: " + action + boundary);
                        Require(JsonUtility.ToJson(current) == before, "service transaction leaves original caller untouched");
                        if (!accepted)
                        {
                            Require(JsonUtility.ToJson(published) == before && JsonUtility.ToJson(store.Load().Snapshot) == before,
                                "uncommitted operation leaves caller and durable logical state unchanged");
                            continue;
                        }
                        RequireSamePayload(published, store.Load().Snapshot, "committed service result must match durable candidate");
                        if (action == "care")
                        {
                            Require(published.eggProgress == 500 && published.lastObservedUtcTicks == 638712864000000000L &&
                                published.nextCareAtUtcTicks == 638712936000000000L, "care publishes literal progress and two-hour cooldown");
                            var next = sides.TryApply(published, HomeSideActivityAction.DailyGift, new FixedClock().UtcNow);
                            Require(next.Accepted && next.Snapshot.coins == 500 && next.Snapshot.eggProgress == 500 &&
                                next.Snapshot.nextCareAtUtcTicks == 638712936000000000L, "next economy transaction cannot overwrite committed care/cooldown");
                            Require(progress.TryCare(next.Snapshot).Status == HatchProgressStatus.COOLDOWN, "committed care cannot be awarded twice");
                            RequireSamePayload(next.Snapshot, store.Load().Snapshot, "care follow-on stays durable");
                        }
                        else if (action == "steps")
                        {
                            Require(published.eggProgress == 60 && published.stepProviderCursors.Count == 1 &&
                                published.stepProviderCursors[0].cumulativeTotal == 60 && published.processedStepEvents[0] == "health:first",
                                "verified steps publish progress, cursor and event together");
                            var next = progress.ApplyVerifiedSteps(published, Step(25, 85, "second"));
                            Require(next.Accepted && next.Snapshot.eggProgress == 85 && next.Snapshot.processedStepEvents.Count == 2 &&
                                next.Snapshot.stepProviderCursors[0].cumulativeTotal == 85, "next delta cannot overwrite committed cursor/progress from stale caller");
                            Require(progress.ApplyVerifiedSteps(next.Snapshot, Step(60, 60, "first")).Status == HatchProgressStatus.DUPLICATE,
                                "committed first step remains protected against replay");
                            RequireSamePayload(next.Snapshot, store.Load().Snapshot, "step follow-on stays durable");
                        }
                        else
                        {
                            Require(published.coins == 500 && published.giftClaimedToday && published.dayKey == 2025001, "gift publishes currency and daily claim");
                            var next = sides.TryApply(published, HomeSideActivityAction.BuySnack, new FixedClock().UtcNow);
                            Require(next.Accepted && next.Snapshot.coins == 200 && next.Snapshot.snacks == 1 && next.Snapshot.giftClaimedToday,
                                "next purchase uses committed currency and preserves gift claim");
                            Require(!sides.TryApply(next.Snapshot, HomeSideActivityAction.DailyGift, new FixedClock().UtcNow).Accepted,
                                "committed gift cannot be claimed twice");
                            RequireSamePayload(next.Snapshot, store.Load().Snapshot, "side-activity follow-on stays durable");
                        }
                    }
        }

        private static VerifiedStepDelta Step(int delta, long total, string id) => new()
        { providerId = "health", dayKey = 2025001, eventId = id, delta = delta, cumulativeTotal = total };

        private sealed class FixedClock : ICareClock
        { public DateTime UtcNow => new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc); }

        private static void UnknownOutcomeRequiresReloadBeforeAnyFurtherWrite()
        {
            var fs = new FakeHatchHomeFileSystem();
            var store = new HatchHomeStore(Root, fs);
            fs.FailAt("move", BackupName, 1, true);
            fs.ReadFailureName = BackupName;
            var threwUnknown = false;
            try { store.TrySave(new HatchHomeSnapshot { eggProgress = 500 }); }
            catch (HatchSaveOutcomeUnknownException) { threwUnknown = true; }
            Require(threwUnknown && fs.FaultConsumed, "second I/O failure cannot be reported as an uncommitted false");
            Require(!store.TrySave(new HatchHomeSnapshot { coins = 500 }), "unknown outcome fences a stale follow-on write");
            var reloadFailed = false;
            try { store.Load(); }
            catch (IOException) { reloadFailed = true; }
            Require(reloadFailed, "reload cannot silently fall back to fresh while committed backup is unreadable");
            fs.ReadFailureName = null;
            Require(!store.TrySave(new HatchHomeSnapshot { coins = 500 }), "restored I/O alone cannot clear reload fence");
            var recovered = store.Load();
            Require(recovered.Source == "backup" && recovered.Snapshot.eggProgress == 500 && recovered.Snapshot.coins == 0,
                "strict reload restores actual committed candidate rather than stale caller");
            recovered.Snapshot.coins = 500;
            Require(store.TrySave(recovered.Snapshot) && store.Load().Snapshot.eggProgress == 500,
                "only after reload may the next transaction preserve and extend committed progress");
        }

        private static void CoordinatorRecoversUnknownOutcomeBeforeRetry()
        {
            foreach (var action in new[] { "care", "steps", "gift" })
                foreach (var reloadBlocked in new[] { false, true })
                    foreach (var suspended in new[] { false, true })
                {
                    var root = new GameObject("Unknown save recovery " + action);
                    try
                    {
                        var fs = new FakeHatchHomeFileSystem();
                        var store = new HatchHomeStore(Root, fs);
                        var clock = new FixedClock();
                        var director = root.AddComponent<HatchSequenceDirector>();
                        var coordinator = root.AddComponent<HatchHomeCoordinator>();
                        coordinator.Configure(store, new HatchProgressService(store, clock), new HatchSelectionService(store, new CountingRandom()),
                            null, director, null, clock);
                        coordinator.Initialize();
                        if (suspended) coordinator.SuspendPresentation();
                        fs.FailAt("move", BackupName, 1, true);
                        if (reloadBlocked) fs.ReadFailureName = BackupName;
                        else fs.ThrowOnNextRead(BackupName);
                        if (action == "care") coordinator.CareEgg();
                        else if (action == "steps") coordinator.ApplyVerifiedSteps(Step(60, 60, "first"));
                        else coordinator.ApplySideActivity(HomeSideActivityAction.DailyGift);
                        Require(fs.FaultConsumed, "real coordinator reached ambiguous commit without leaking exception into app loop");
                        if (reloadBlocked)
                        {
                            Require(coordinator.InputLocked && coordinator.NeedsRetry, "unreadable outcome locks gameplay until explicit reload retry");
                            coordinator.CareEgg(); coordinator.ApplyVerifiedSteps(Step(10, 10, "blocked"));
                            Require(!coordinator.ApplySideActivity(HomeSideActivityAction.ToggleSound).Accepted &&
                                !store.TrySave(new HatchHomeSnapshot { coins = 999 }), "no callback can write stale state while reload is blocked");
                            fs.ReadFailureName = null;
                            coordinator.RetryHatch();
                        }
                        var published = coordinator.Snapshot;
                        Require(!coordinator.InputLocked && !coordinator.NeedsRetry, "successful strict reload restores normal unhatched interaction");
                        Require(published.eggProgress == (action == "care" ? 500 : action == "steps" ? 60 : 0) &&
                            published.coins == (action == "gift" ? 500 : 0), "coordinator publishes recovered commit, not stale pre-call state");
                        var next = coordinator.ApplySideActivity(HomeSideActivityAction.ToggleSound);
                        Require(next.Accepted && !next.Snapshot.soundEnabled, "next real coordinator transaction succeeds after reload");
                        RequireSamePayload(next.Snapshot, store.Load().Snapshot, "follow-on coordinator transaction preserves all committed fields");
                        if (action == "care") Require(next.Snapshot.nextCareAtUtcTicks == 638712936000000000L, "recovery preserves care cooldown");
                        if (action == "steps") Require(next.Snapshot.processedStepEvents.Count == 1 && next.Snapshot.stepProviderCursors[0].cumulativeTotal == 60,
                            "recovery preserves step ledger/cursor");
                        if (action == "gift") Require(next.Snapshot.giftClaimedToday && next.Snapshot.coins == 500, "recovery preserves claimed gift/currency");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
        }

        private static void RestartedStoreFailsClosedUntilNewestReplicaRecovers()
        {
            foreach (var operation in new[] { "exists", "read" })
            {
                var ioFs = new FakeHatchHomeFileSystem();
                var ioStore = new HatchHomeStore(Root, ioFs);
                Require(ioStore.TrySave(new HatchHomeSnapshot { eggProgress = 500 }), "initial I/O fixture");
                ioFs.FailAt(operation, BackupName, 1, false);
                var ioFailed = false;
                try { new HatchHomeStore(Root, ioFs).Load(); }
                catch (IOException) { ioFailed = true; }
                Require(ioFailed && ioFs.FaultConsumed,
                    "every new store fails closed on replica " + operation + " I/O instead of accepting stale state");
            }

            PrepareRestartRecoveryFixture(out var fs, out var staleReady, out var random);
            var restartedLoadFailed = false;
            try { new HatchHomeStore(Root, fs).Load(); }
            catch (IOException) { restartedLoadFailed = true; }
            Require(restartedLoadFailed, "a restarted store cannot return stale primary while newer backup is unreadable");

            fs.ReadFailureName = null;
            var restarted = new HatchHomeStore(Root, fs);
            var recovered = restarted.Load();
            Require(recovered.Source == "backup" && recovered.Snapshot.revision == 2 &&
                recovered.Snapshot.phase == "HATCHING" && recovered.Snapshot.selectedArtId == 17 &&
                recovered.Snapshot.nextCareAtUtcTicks == 638712936000000000L &&
                recovered.Snapshot.processedStepEvents.Count == 1 &&
                recovered.Snapshot.processedStepEvents[0] == "health:first" &&
                recovered.Snapshot.stepProviderCursors.Count == 1 &&
                recovered.Snapshot.stepProviderCursors[0].cumulativeTotal == 30000 &&
                recovered.Snapshot.giftClaimedToday && recovered.Snapshot.coins == 73 && !recovered.Snapshot.soundEnabled,
                "restart recovery returns the literal selected rev2 payload with care, steps, and side state intact");
            var retry = new HatchSelectionService(restarted, random).TryBegin(staleReady);
            Require(retry.Accepted && retry.Snapshot.selectedArtId == 17 && random.Draws == 1,
                "stale ready retry resolves durable selection 17 without drawing 23");
            var followOn = new HomeSideActivityService(restarted).TryApply(
                recovered.Snapshot, HomeSideActivityAction.ToggleSound, new FixedClock().UtcNow);
            Require(followOn.Accepted && followOn.Snapshot.revision == 2 && followOn.Snapshot.soundEnabled,
                "follow-on preference transaction publishes from recovered rev2 caller without mutating its store-owned revision");
            var durable = restarted.Load().Snapshot;
            Require(durable.revision == 3 && durable.selectedArtId == 17 && durable.phase == "HATCHING" &&
                durable.nextCareAtUtcTicks == 638712936000000000L &&
                durable.processedStepEvents.Count == 1 && durable.processedStepEvents[0] == "health:first" &&
                durable.stepProviderCursors.Count == 1 && durable.stepProviderCursors[0].cumulativeTotal == 30000 &&
                durable.giftClaimedToday && durable.coins == 73 && durable.soundEnabled,
                "rev3 preserves recovered care, step ledger, gift, and irreversible selection payload");

            foreach (var operation in new[] { "exists", "read" })
            {
                var legacyIoFs = new FakeHatchHomeFileSystem();
                legacyIoFs.Seed(LegacyName, "{\"schemaVersion\":1,\"eggExperience\":12500,\"hatched\":true," +
                    "\"selectedArtId\":17,\"coins\":73,\"soundEnabled\":false}");
                legacyIoFs.FailAt(operation, LegacyName, 1, false);
                var legacyIoFailed = false;
                try { new HatchHomeStore(Root, legacyIoFs).Load(); }
                catch (IOException) { legacyIoFailed = true; }
                Require(legacyIoFailed && legacyIoFs.FaultConsumed,
                    "existing legacy " + operation + " I/O cannot fall through to fresh and mask migration");
            }

            var legacyFs = new FakeHatchHomeFileSystem();
            legacyFs.Seed(LegacyName, "{\"schemaVersion\":1,\"eggExperience\":12500,\"hatched\":true," +
                "\"selectedArtId\":17,\"coins\":73,\"soundEnabled\":false}");
            legacyFs.ReadFailureName = null;
            var migrated = new HatchHomeStore(Root, legacyFs).Load();
            Require(migrated.Source == "v1" && migrated.Snapshot.phase == "HOME" &&
                migrated.Snapshot.selectedArtId == 17 && migrated.Snapshot.coins == 73 && !migrated.Snapshot.soundEnabled,
                "legacy retry migrates the exact literal payload after I/O recovery");
            var malformedLegacy = new FakeHatchHomeFileSystem();
            malformedLegacy.Seed(LegacyName, "{");
            Require(new HatchHomeStore(Root, malformedLegacy).Load().Source == "fresh",
                "malformed legacy JSON remains invalid data with fresh fallback");

            var fenceFs = new FakeHatchHomeFileSystem();
            var fencedStore = new HatchHomeStore(Root, fenceFs);
            fenceFs.FailAtThenReadFails("move", BackupName, 1, true, BackupName);
            var fenceUnknown = false;
            try { fencedStore.TrySave(new HatchHomeSnapshot { eggProgress = 500 }); }
            catch (HatchSaveOutcomeUnknownException) { fenceUnknown = true; }
            Require(fenceUnknown && fenceFs.FaultConsumed, "legacy fence fixture reaches an unknown first commit");
            fenceFs.ReadFailureName = null;
            fenceFs.Corrupt(BackupName);
            fenceFs.Seed(LegacyName, "{\"schemaVersion\":1,\"eggExperience\":12500,\"hatched\":false}");
            fenceFs.ReadFailureName = LegacyName;
            var fencedLoadFailed = false;
            try { fencedStore.Load(); }
            catch (IOException) { fencedLoadFailed = true; }
            fenceFs.ReadFailureName = null;
            Require(fencedLoadFailed && !fencedStore.TrySave(new HatchHomeSnapshot { coins = 999 }),
                "failed legacy recovery cannot clear an existing unknown-outcome write fence");
        }

        private static void CoordinatorInitialLoadRetriesNewestReplica()
        {
            PrepareRestartRecoveryFixture(out var fs, out var staleReady, out _);
            var root = new GameObject("Restart load recovery");
            Application.LogCallback logger = null;
            try
            {
                var store = new HatchHomeStore(Root, fs);
                var clock = new FixedClock();
                var director = root.AddComponent<HatchSequenceDirector>();
                var coordinator = root.AddComponent<HatchHomeCoordinator>();
                var provider = new CountingProvider();
                var coordinatorRandom = new CountingRandom();
                var published = 0;
                var lockEvents = 0;
                var initialWarning = string.Empty;
                logger = (condition, _, type) =>
                {
                    if (type == LogType.Warning && condition.StartsWith("Hatch home initial load failed (", StringComparison.Ordinal))
                        initialWarning = condition;
                };
                Application.logMessageReceived += logger;
                coordinator.SnapshotChanged += _ => published++;
                coordinator.InputLockChanged += locked => { if (locked) lockEvents++; };
                coordinator.Configure(store, new HatchProgressService(store, clock),
                    new HatchSelectionService(store, coordinatorRandom), null, director, provider, clock);
                coordinator.Initialize();
                Require(coordinator.Snapshot != null && coordinator.Snapshot.revision == 0 &&
                    coordinator.Snapshot.eggProgress == 0 && coordinator.Snapshot.selectedArtId == 0,
                    "failed initial load retains only a safe UI placeholder");
                Require(coordinator.InputLocked && coordinator.NeedsRetry && lockEvents > 0 && published == 0 && provider.Subscribers == 1 &&
                    initialWarning == "Hatch home initial load failed (IOException); retry is required.",
                    "initial I/O failure initializes subscriptions but publishes no placeholder as durable state");

                fs.ReadFailureName = null;
                var primaryRev1Bytes = fs.Peek(PrimaryName);
                var backupRev2Bytes = fs.Peek(BackupName);
                var expectedRev2 = JsonUtility.FromJson<HatchHomeSnapshot>(backupRev2Bytes);
                RequireRestartPayload(expectedRev2, 2, "HATCHING", "STARTED", false,
                    "literal committed backup is rev2 before coordinator retry");
                var writesBeforeCallbacks = fs.WriteCalls;
                var movesBeforeCallbacks = fs.MoveCalls;
                coordinator.CareEgg();
                provider.Emit(Step(10, 10, "blocked"));
                coordinator.BeginHatch();
                Require(!coordinator.ApplySideActivity(HomeSideActivityAction.ToggleSound).Accepted,
                    "locked side activity rejects before reaching the restored store");
                Require(fs.WriteCalls == writesBeforeCallbacks && fs.MoveCalls == movesBeforeCallbacks &&
                    fs.Peek(PrimaryName) == primaryRev1Bytes && fs.Peek(BackupName) == backupRev2Bytes &&
                    published == 0 && coordinatorRandom.Draws == 0,
                    "restored I/O cannot let any pre-retry callback write, publish, or draw from the placeholder");

                coordinator.RetryHatch();
                var recovered = coordinator.Snapshot;
                Require(published == 1 && !coordinator.NeedsRetry && coordinator.InputLocked && director.IsPlaying &&
                    coordinatorRandom.Draws == 0 && fs.WriteCalls == writesBeforeCallbacks && fs.MoveCalls == movesBeforeCallbacks &&
                    fs.Peek(PrimaryName) == primaryRev1Bytes && fs.Peek(BackupName) == backupRev2Bytes,
                    "successful retry publishes latest rev2 once and resumes its normal locked hatch presentation");
                RequireRestartPayload(recovered, 2, "HATCHING", "STARTED", false,
                    "coordinator publishes the complete latest rev2 payload");

                director.Advance(5f);
                Require(!director.IsPlaying && !coordinator.InputLocked && coordinator.Snapshot.phase == "HOME",
                    "recovered normal presentation completes before an unlocked coordinator transaction");
                var followOn = coordinator.ApplySideActivity(HomeSideActivityAction.ToggleSound);
                Require(followOn.Accepted && published == 5 && coordinatorRandom.Draws == 0,
                    "coordinator follow-on commits once after burst, reveal, and home without reroll");
                RequireRestartPayload(followOn.Snapshot, 2, "HOME", "REVEALED", true,
                    "coordinator follow-on preserves every recovered payload field");
                var durable = store.Load().Snapshot;
                RequireRestartPayload(durable, 6, "HOME", "REVEALED", true,
                    "durable rev6 preserves rev2 care, steps, gift, sound, and selected identity");
                RequireSamePayload(followOn.Snapshot, durable,
                    "coordinator follow-on publication matches the complete durable payload apart from store revision");
                var retry = new HatchSelectionService(store, coordinatorRandom).TryBegin(staleReady);
                Require(!retry.Accepted && retry.Snapshot.selectedArtId == 0 && coordinatorRandom.Draws == 0,
                    "completed durable ownership rejects stale ready retry without drawing");
            }
            finally
            {
                if (logger != null) Application.logMessageReceived -= logger;
                UnityEngine.Object.DestroyImmediate(root);
            }

            var programmingRoot = new GameObject("Programming failure propagation");
            try
            {
                var throwingStore = new ThrowingLoadStore();
                var clock = new FixedClock();
                var coordinator = programmingRoot.AddComponent<HatchHomeCoordinator>();
                coordinator.Configure(throwingStore, new HatchProgressService(throwingStore, clock),
                    new HatchSelectionService(throwingStore, new CountingRandom()), null,
                    programmingRoot.AddComponent<HatchSequenceDirector>(), null, clock);
                var propagated = false;
                try { coordinator.Initialize(); }
                catch (InvalidOperationException) { propagated = true; }
                Require(propagated, "initialization does not disguise a non-storage programming failure as retryable I/O");
            }
            finally { UnityEngine.Object.DestroyImmediate(programmingRoot); }
        }

        private static void RequireRestartPayload(
            HatchHomeSnapshot snapshot,
            long revision,
            string phase,
            string checkpoint,
            bool soundEnabled,
            string message)
        {
            Require(snapshot != null && snapshot.schemaVersion == 2 && snapshot.revision == revision &&
                snapshot.phase == phase && snapshot.eggProgress == 30000 &&
                snapshot.lastObservedUtcTicks == 638712864000000000L &&
                snapshot.nextCareAtUtcTicks == 638712936000000000L && snapshot.selectedArtId == 17 &&
                snapshot.hatchCheckpoint == checkpoint && snapshot.activeHomeView == (phase == "HOME" ? "DINOSAUR" : "EGG") &&
                snapshot.hatchedAtUtcTicks == (phase == "HOME" ? 638712864000000000L : 0L) &&
                snapshot.processedStepEvents.Count == 1 && snapshot.processedStepEvents[0] == "health:first" &&
                snapshot.stepProviderCursors.Count == 1 && snapshot.stepProviderCursors[0].providerId == "health" &&
                snapshot.stepProviderCursors[0].dayKey == 2025001 && snapshot.stepProviderCursors[0].cumulativeTotal == 30000 &&
                snapshot.dayKey == 2025001 && snapshot.giftClaimedToday && snapshot.coins == 73 &&
                snapshot.soundEnabled == soundEnabled && snapshot.dragonExperience == 0 && snapshot.snacks == 0 &&
                snapshot.playsToday == 0 && !snapshot.questClaimedToday && snapshot.lastPlayTicks == 0,
                message);
        }

        private static void SystemFileSystemExistsPreservesProbeErrors()
        {
            var adapterType = typeof(HatchHomeStore).GetNestedType(
                "SystemHatchHomeFileSystem", System.Reflection.BindingFlags.NonPublic);
            var adapter = (IHatchHomeFileSystem)Activator.CreateInstance(adapterType, true);
            var directory = Path.Combine(Path.GetTempPath(), "bigimong-hatch-exists-" + Guid.NewGuid().ToString("N"));
            var existing = Path.Combine(directory, "existing.json");
            var missing = Path.Combine(directory, "missing.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(existing, "{}");
            try
            {
                Require(adapter.Exists(existing), "system adapter reports a real existing file");
                Require(!adapter.Exists(missing), "system adapter returns false for a genuinely missing file");
                var invalidPropagated = false;
                try { adapter.Exists("\0"); }
                catch (ArgumentException) { invalidPropagated = true; }
                Require(invalidPropagated,
                    "system adapter propagates an invalid/unexpected probe instead of treating it as missing");
            }
            finally { Directory.Delete(directory, true); }
        }

        private static void PrepareRestartRecoveryFixture(
            out FakeHatchHomeFileSystem fs,
            out HatchHomeSnapshot staleReady,
            out CountingRandom random)
        {
            fs = new FakeHatchHomeFileSystem();
            var store = new HatchHomeStore(Root, fs);
            var ready = Ready();
            ready.lastObservedUtcTicks = 638712864000000000L;
            ready.nextCareAtUtcTicks = 638712936000000000L;
            ready.processedStepEvents.Add("health:first");
            ready.stepProviderCursors.Add(new StepProviderCursor
            {
                providerId = "health",
                dayKey = 2025001,
                cumulativeTotal = 30000,
            });
            ready.dayKey = 2025001;
            ready.giftClaimedToday = true;
            ready.coins = 73;
            ready.soundEnabled = false;
            Require(store.TrySave(ready), "restart fixture synchronizes literal rev1 to primary and backup");
            staleReady = store.Load().Snapshot;
            random = new CountingRandom();
            fs.FailAtThenReadFails("move", BackupName, 1, true, BackupName);
            var unknown = false;
            try { new HatchSelectionService(store, random).TryBegin(staleReady); }
            catch (HatchSaveOutcomeUnknownException) { unknown = true; }
            Require(unknown && fs.FaultConsumed && random.Draws == 1,
                "rev2 backup atomic move commits selection 17 before the reconciliation read becomes unavailable");
            var strictReloadFailed = false;
            try { store.Load(); }
            catch (IOException) { strictReloadFailed = true; }
            Require(strictReloadFailed && !store.TrySave(staleReady),
                "same-store strict reload failure retains the unknown-outcome write fence");
        }

        private static void RequireSamePayload(HatchHomeSnapshot published, HatchHomeSnapshot durable, string message)
        {
            var copy = durable.Clone();
            copy.revision = published.revision; // Revision is store-owned, never written into the caller.
            Require(JsonUtility.ToJson(published) == JsonUtility.ToJson(copy), message);
        }

        private static readonly string[] InvalidPhaseProgressJson =
        {
            "{\"schemaVersion\":2,\"revision\":9,\"phase\":\"HATCH_READY\",\"eggProgress\":29999,\"selectedArtId\":0,\"hatchCheckpoint\":\"NONE\",\"activeHomeView\":\"EGG\"}",
            "{\"schemaVersion\":2,\"revision\":9,\"phase\":\"EGG_ACTIVE\",\"eggProgress\":30000,\"selectedArtId\":0,\"hatchCheckpoint\":\"NONE\",\"activeHomeView\":\"EGG\"}",
            "{\"schemaVersion\":2,\"revision\":9,\"phase\":\"EGG_ACTIVE\",\"eggProgress\":-1,\"selectedArtId\":0,\"hatchCheckpoint\":\"NONE\",\"activeHomeView\":\"EGG\"}",
            "{\"schemaVersion\":2,\"revision\":9,\"phase\":\"HATCHING\",\"eggProgress\":29999,\"selectedArtId\":17,\"hatchCheckpoint\":\"STARTED\",\"activeHomeView\":\"EGG\"}",
            "{\"schemaVersion\":2,\"revision\":9,\"phase\":\"REVEAL\",\"eggProgress\":0,\"selectedArtId\":17,\"hatchCheckpoint\":\"REVEALED\",\"activeHomeView\":\"DINOSAUR\"}",
            "{\"schemaVersion\":2,\"revision\":9,\"phase\":\"HOME\",\"eggProgress\":29999,\"selectedArtId\":17,\"hatchCheckpoint\":\"REVEALED\",\"activeHomeView\":\"AVATAR\"}",
            "{\"schemaVersion\":2,\"revision\":9,\"phase\":\"HOME\",\"eggProgress\":30001,\"selectedArtId\":17,\"hatchCheckpoint\":\"REVEALED\",\"activeHomeView\":\"DINOSAUR\"}",
            "{\"schemaVersion\":2,\"revision\":9,\"phase\":\"HATCH_READY\",\"eggProgress\":30001,\"selectedArtId\":0,\"hatchCheckpoint\":\"NONE\",\"activeHomeView\":\"EGG\"}",
        };

        private static void RawPhaseProgressMismatchLosesToValidReplica()
        {
            const string valid = "{\"schemaVersion\":2,\"revision\":3,\"phase\":\"EGG_ACTIVE\",\"eggProgress\":29999,\"selectedArtId\":0,\"hatchCheckpoint\":\"NONE\",\"activeHomeView\":\"EGG\",\"coins\":73}";
            foreach (var invalid in InvalidPhaseProgressJson)
                foreach (var invalidPrimary in new[] { true, false })
                {
                    var fs = new FakeHatchHomeFileSystem();
                    fs.Seed(invalidPrimary ? PrimaryName : BackupName, invalid);
                    fs.Seed(invalidPrimary ? BackupName : PrimaryName, valid);
                    var store = new HatchHomeStore(Root, fs);
                    var loaded = store.Load();
                    Require(loaded.Source == (invalidPrimary ? "backup" : "primary") && loaded.Recovered == invalidPrimary &&
                        loaded.Snapshot.revision == 3 && loaded.Snapshot.phase == "EGG_ACTIVE" &&
                        loaded.Snapshot.eggProgress == 29999 && loaded.Snapshot.coins == 73 && loaded.Snapshot.selectedArtId == 0,
                        "raw mismatch cannot outrank valid replica or invent ownership even at newer revision");
                    var care = new HatchProgressService(store, new FixedClock()).TryCare(loaded.Snapshot);
                    Require(care.Accepted && care.Snapshot.eggProgress == 30000 && care.Snapshot.phase == "HATCH_READY",
                        "valid recovered egg can reach hatch-ready rather than remaining permanently blocked");
                    var selected = new HatchSelectionService(store, new CountingRandom()).TryBegin(care.Snapshot);
                    Require(selected.Accepted && selected.Snapshot.selectedArtId == 17, "only explicit selection grants ownership after recovery");
                    fs = new FakeHatchHomeFileSystem();
                    fs.Seed(PrimaryName, invalid); fs.Seed(BackupName, invalid);
                    loaded = new HatchHomeStore(Root, fs).Load();
                    Require(loaded.Source == "fresh" && loaded.Snapshot.selectedArtId == 0 && loaded.Snapshot.eggProgress == 0,
                        "two invalid raw snapshots cannot normalize into owned dinosaur");
                }
        }

        private static void InvalidPhaseProgressCannotBeSavedAsOwnership()
        {
            foreach (var invalid in InvalidPhaseProgressJson)
            {
                var fs = new FakeHatchHomeFileSystem();
                var store = new HatchHomeStore(Root, fs);
                Require(store.TrySave(new HatchHomeSnapshot { eggProgress = 1234 }), "valid save fixture");
                var primary = fs.ReadAllText(PrimaryName); var backup = fs.ReadAllText(BackupName);
                var candidate = JsonUtility.FromJson<HatchHomeSnapshot>(invalid);
                var callerBefore = JsonUtility.ToJson(candidate);
                Require(!candidate.HasValidDurableIdentity() && !store.TrySave(candidate), "raw cross-field mismatch is rejected before save normalization");
                Require(fs.ReadAllText(PrimaryName) == primary && fs.ReadAllText(BackupName) == backup &&
                    JsonUtility.ToJson(candidate) == callerBefore, "invalid input preserves both durable bytes and caller");
            }
        }

        private static void Selected17SurvivesPrimaryCorruptionBeforeCheckpoint()
        {
            var fs = new FakeHatchHomeFileSystem();
            var store = new HatchHomeStore(Root, fs);
            var ready = Ready();
            Require(store.TrySave(ready), "ready fixture is durable before the first draw");
            var random = new CountingRandom();
            var selected = new HatchSelectionService(store, random).TryBegin(ready);
            Require(selected.Accepted && selected.Snapshot.selectedArtId == 17 && random.Draws == 1,
                "real store commits deterministic 17 once");
            RequireStoredIdentity(fs, BackupName, 17, "STARTED selection is redundant before any checkpoint");
            fs.Corrupt(PrimaryName);
            var reopened = new HatchHomeStore(Root, fs);
            var loaded = reopened.Load();
            Require(loaded.Source == "backup" && loaded.Snapshot.selectedArtId == 17 &&
                loaded.Snapshot.phase == "HATCHING" && loaded.Snapshot.hatchCheckpoint == "STARTED",
                "corrupt primary before burst cannot restore ready or lose art ID 17");
            var retry = new HatchSelectionService(reopened, random).TryBegin(ready);
            Require(retry.Accepted && retry.Snapshot.selectedArtId == 17 && random.Draws == 1,
                "stale ready retry cannot consume queued ID 23 after corruption");
        }

        private static void EveryRedundancyBoundaryPreservesSelection()
        {
            // A failure before the backup promotion exposes no result. From its atomic promotion
            // onward, the new revision must be recoverable even if primary is corrupt/older.
            foreach (var step in new[]
            {
                ("write", TemporaryName, 1, false, false), ("write", TemporaryName, 1, true, false),
                ("read", TemporaryName, 1, false, false), ("read", TemporaryName, 1, true, false),
                ("move", BackupName, 1, false, false), ("move", BackupName, 1, true, true),
                ("write", TemporaryName, 2, false, true), ("write", TemporaryName, 2, true, true),
                ("read", TemporaryName, 2, false, true), ("read", TemporaryName, 2, true, true),
                ("move", PrimaryName, 1, false, true), ("move", PrimaryName, 1, true, true),
            })
            {
                var fs = new FakeHatchHomeFileSystem();
                var store = new HatchHomeStore(Root, fs);
                Require(store.TrySave(Ready()), "interruption fixture starts with redundant ready");
                fs.FailAt(step.Item1, step.Item2, step.Item3, step.Item4);
                var random = new CountingRandom();
                var result = new HatchSelectionService(store, random).TryBegin(Ready());
                Require(fs.FaultConsumed, "fixture must reach its exact injected I/O boundary");
                Require(result.Accepted == step.Item5 && random.Draws == 1,
                    "acceptance must agree with the first durable candidate promotion");
                Require(!fs.Exists(TemporaryName), "interrupted temporary candidate is cleaned and never loaded");
                fs.Corrupt(PrimaryName);
                var restarted = new HatchHomeStore(Root, fs);
                var durable = restarted.Load().Snapshot;
                if (step.Item5)
                {
                    Require(durable.selectedArtId == 17 && durable.hatchCheckpoint == "STARTED", "accepted 17 never falls back to ready");
                    var retry = new HatchSelectionService(restarted, random).TryBegin(Ready());
                    Require(retry.Accepted && retry.Snapshot.selectedArtId == 17 && random.Draws == 1,
                        "retry after every post-promotion interruption never draws 23");
                }
                else
                    Require(result.Snapshot.selectedArtId == 0 && durable.selectedArtId == 0 && durable.phase == "HATCH_READY",
                        "pre-promotion failure must not accept or show an uncommitted selection");
            }
        }

        private static void NewerBackupWinsAndIsProtectedBeforeNextWrite()
        {
            foreach (var step in new[]
            {
                ("write", TemporaryName, 1, false), ("read", TemporaryName, 1, false),
                ("move", PrimaryName, 1, false), ("move", PrimaryName, 1, true),
                ("write", TemporaryName, 2, false), ("move", BackupName, 1, false),
            })
            {
                var fs = new FakeHatchHomeFileSystem();
                var older = Ready(); older.revision = 4;
                var newest = new HatchHomeSnapshot { phase = "HATCHING", eggProgress = 30000, selectedArtId = 17,
                    hatchCheckpoint = "STARTED", revision = 5 };
                fs.Seed(PrimaryName, JsonUtility.ToJson(older));
                fs.Seed(BackupName, JsonUtility.ToJson(newest));
                var store = new HatchHomeStore(Root, fs);
                Require(store.Load().Snapshot.selectedArtId == 17 && store.Load().Source == "backup",
                    "newer backup must beat a valid but stale ready primary");
                fs.FailAt(step.Item1, step.Item2, step.Item3, step.Item4);
                newest.hatchCheckpoint = "SHELL_BURST";
                Require(!store.TrySave(newest) && fs.FaultConsumed, "repair/promotion interruption is exercised");
                Require(store.Load().Snapshot.selectedArtId == 17, "newest durable identity is protected before replacing backup");
                fs.Corrupt(PrimaryName);
                Require(store.Load().Snapshot.selectedArtId == 17, "old ready cannot displace selected backup after failed repair");
            }
        }

        private static void TornOrSubstitutedTemporaryWritesCannotPublishUnvalidatedSelection()
        {
            foreach (var occurrence in new[] { 1, 2 })
                foreach (var replacement in new[] { "{", JsonUtility.ToJson(Ready()) })
                {
                    var fs = new FakeHatchHomeFileSystem();
                    var store = new HatchHomeStore(Root, fs);
                    Require(store.TrySave(Ready()), "torn write fixture starts ready");
                    fs.SubstituteWrite(occurrence, replacement);
                    var random = new CountingRandom();
                    var result = new HatchSelectionService(store, random).TryBegin(Ready());
                    Require(fs.FaultConsumed, "targeted temporary write was actually torn/substituted");
                    Require(result.Accepted == (occurrence == 2), "only already-promoted backup can authorize selection after a bad write");
                    fs.Corrupt(PrimaryName);
                    var loaded = new HatchHomeStore(Root, fs).Load().Snapshot;
                    if (occurrence == 2)
                        Require(loaded.selectedArtId == 17 && random.Draws == 1, "bad second temporary write cannot erase committed 17");
                    else
                        Require(loaded.selectedArtId == 0 && result.Snapshot.selectedArtId == 0,
                            "invalid or substituted candidate must never be shown/accepted before promotion");
                }
        }

        private static void LegacyV2RevisionZeroAndEqualRevisionPrimaryWin()
        {
            var fs = new FakeHatchHomeFileSystem();
            fs.Seed(PrimaryName, "{\"schemaVersion\":2,\"phase\":\"HATCH_READY\",\"eggProgress\":30000," +
                "\"selectedArtId\":0,\"hatchCheckpoint\":\"NONE\",\"activeHomeView\":\"EGG\"}");
            fs.Seed(BackupName, JsonUtility.ToJson(HomeSnapshot(11)));
            var store = new HatchHomeStore(Root, fs);
            var loaded = store.Load();
            Require(loaded.Source == "primary" && loaded.Snapshot.revision == 0 && loaded.Snapshot.selectedArtId == 0,
                "old v2 without a revision remains valid; equal revisions prefer primary");
            Require(store.TrySave(loaded.Snapshot), "old v2 can acquire revision one without a schema migration");
            Require(store.Load().Snapshot.revision == 1 && store.Load().Snapshot.schemaVersion == 2,
                "revision is independent of schema version");
            var invalid = HomeSnapshot(17); invalid.revision = -1;
            fs.Seed(PrimaryName, JsonUtility.ToJson(invalid));
            Require(store.Load().Source == "backup" && store.Load().Snapshot.selectedArtId == 0,
                "negative raw revisions are invalid, not normalized into ownership");
            invalid.revision = long.MaxValue;
            fs.Seed(PrimaryName, JsonUtility.ToJson(invalid));
            Require(!store.TrySave(HomeSnapshot(17)) && store.Load().Snapshot.revision == long.MaxValue,
                "revision overflow fails closed without replacing valid state");
        }

        private sealed class CountingRandom : IArtIdRandomSource
        { public int Draws; public int NextArtId() { Draws++; return Draws == 1 ? 17 : 23; } }

        private sealed class CountingProvider : IWalkingProgressProvider
        {
            private event Action<VerifiedStepDelta> received;
            public bool IsAvailable => true;
            public int Subscribers { get; private set; }
            public event Action<VerifiedStepDelta> VerifiedStepsReceived
            {
                add { Subscribers++; received += value; }
                remove { Subscribers--; received -= value; }
            }
            public void Emit(VerifiedStepDelta delta) => received?.Invoke(delta);
        }

        private sealed class ThrowingLoadStore : IHatchHomeStore
        {
            public HatchLoadResult Load() => throw new InvalidOperationException("programming failure");
            public bool TrySave(HatchHomeSnapshot snapshot) => throw new InvalidOperationException("unexpected save");
        }

        private static void FailedPromotionLeavesDurableIdentityRecoverableAndCallerUntouched()
        {
            var fileSystem = new FakeHatchHomeFileSystem();
            var store = new HatchHomeStore(Root, fileSystem);
            const int originalId = 7;
            Require(store.TrySave(HomeSnapshot(originalId)), "failed-promotion fixture save must succeed");
            fileSystem.Corrupt(PrimaryName);
            Require(store.Load().Source == "backup", "corrupt primary must use backup");

            var changed = HomeSnapshot(9);
            changed.coins = -8;
            fileSystem.ThrowOnNextMove(PrimaryName);
            Require(!store.TrySave(changed), "failed promotion must report false");
            Require(store.Load().Snapshot.selectedArtId == originalId, "failed save changed durable identity");
            Require(changed.coins == -8, "failed save must not normalize the caller");
        }

        private static void TempWriteFailurePreservesValidPrimary()
        {
            var fileSystem = new FakeHatchHomeFileSystem();
            var store = new HatchHomeStore(Root, fileSystem);
            Require(store.TrySave(HomeSnapshot(7)), "temp-write fixture save must succeed");
            var primaryBefore = fileSystem.ReadAllText(PrimaryName);
            var backupBefore = fileSystem.ReadAllText(BackupName);
            var changed = ChangedSnapshot();

            fileSystem.ThrowOnNextWrite(TemporaryName);
            Require(!store.TrySave(changed), "temp write failure must report false");

            Require(fileSystem.ReadAllText(PrimaryName) == primaryBefore,
                "temp write failure must preserve original primary bytes");
            Require(fileSystem.ReadAllText(BackupName) == backupBefore,
                "temp write failure must preserve original backup bytes");
            Require(!fileSystem.Exists(TemporaryName), "failed temp write must not leave temp bytes");
            RequireUnchangedCaller(changed, "temp write failure");
        }

        private static void TempReadFailurePreservesValidPrimaryAndCleansTemp()
        {
            var fileSystem = new FakeHatchHomeFileSystem();
            var store = new HatchHomeStore(Root, fileSystem);
            Require(store.TrySave(HomeSnapshot(7)), "temp-read fixture save must succeed");
            var primaryBefore = fileSystem.ReadAllText(PrimaryName);
            var backupBefore = fileSystem.ReadAllText(BackupName);
            var changed = ChangedSnapshot();

            fileSystem.ThrowOnNextRead(TemporaryName);
            Require(!store.TrySave(changed), "temp read failure must report false");

            Require(fileSystem.ReadAllText(PrimaryName) == primaryBefore,
                "temp read failure must preserve original primary bytes");
            Require(fileSystem.ReadAllText(BackupName) == backupBefore,
                "temp read failure must preserve original backup bytes");
            Require(!fileSystem.Exists(TemporaryName), "temp read failure must clean temp bytes");
            RequireUnchangedCaller(changed, "temp read failure");
        }

        private static void ExistingPrimaryReadFailurePreservesValidPrimaryAndCleansTemp()
        {
            var fileSystem = new FakeHatchHomeFileSystem();
            var store = new HatchHomeStore(Root, fileSystem);
            Require(store.TrySave(HomeSnapshot(7)), "primary-read fixture save must succeed");
            var primaryBefore = fileSystem.ReadAllText(PrimaryName);
            var backupBefore = fileSystem.ReadAllText(BackupName);
            var changed = ChangedSnapshot();

            fileSystem.ThrowOnNextRead(PrimaryName);
            Require(!store.TrySave(changed), "existing-primary read failure must report false");

            Require(fileSystem.ReadAllText(PrimaryName) == primaryBefore,
                "existing-primary read failure must preserve original primary bytes");
            Require(fileSystem.ReadAllText(BackupName) == backupBefore,
                "existing-primary read failure must preserve original backup bytes");
            Require(!fileSystem.Exists(TemporaryName), "existing-primary read failure must clean temp bytes");
            Require(store.Load().Source == "primary" && store.Load().Snapshot.selectedArtId == 7,
                "existing-primary read failure must leave literal identity 7 loadable from primary");
            RequireUnchangedCaller(changed, "existing-primary read failure");
        }

        private static void BackupPromotionFailurePreservesPrimaryBackupAndCleansTemp()
        {
            var fileSystem = new FakeHatchHomeFileSystem();
            var store = new HatchHomeStore(Root, fileSystem);
            Require(store.TrySave(HomeSnapshot(5)), "backup-copy first fixture save must succeed");
            Require(store.TrySave(HomeSnapshot(7)), "backup-copy second fixture save must succeed");
            var primaryBefore = fileSystem.ReadAllText(PrimaryName);
            var backupBefore = fileSystem.ReadAllText(BackupName);
            var changed = ChangedSnapshot();

            fileSystem.ThrowOnNextMove(BackupName);
            Require(!store.TrySave(changed), "backup promotion failure must report false");

            Require(fileSystem.ReadAllText(PrimaryName) == primaryBefore,
                "backup copy failure must preserve original primary bytes");
            Require(fileSystem.ReadAllText(BackupName) == backupBefore,
                "backup promotion failure must preserve the current backup bytes");
            RequireStoredIdentity(fileSystem, PrimaryName, 7,
                "backup copy failure must preserve primary identity 7");
            RequireStoredIdentity(fileSystem, BackupName, 7,
                "backup promotion failure must preserve backup identity 7");
            Require(!fileSystem.Exists(TemporaryName), "backup copy failure must clean temp bytes");
            RequireUnchangedCaller(changed, "backup copy failure");
        }

        private static void PrimaryPromotionFailureRecoversNewerBackup()
        {
            var fileSystem = new FakeHatchHomeFileSystem();
            var store = new HatchHomeStore(Root, fileSystem);
            Require(store.TrySave(HomeSnapshot(5)), "promotion first fixture save must succeed");
            Require(store.TrySave(HomeSnapshot(7)), "promotion second fixture save must succeed");
            var primaryBefore = fileSystem.ReadAllText(PrimaryName);
            var changed = ChangedSnapshot();

            fileSystem.ThrowOnNextMove(PrimaryName);
            Require(store.TrySave(changed), "primary catch-up failure must report an already committed candidate");

            Require(fileSystem.ReadAllText(PrimaryName) == primaryBefore,
                "promotion failure must preserve original primary bytes");
            Require(store.Load().Snapshot.selectedArtId == 9 && store.Load().Source == "backup",
                "promotion failure must recover newer backup instead of stale valid primary");
            RequireStoredIdentity(fileSystem, PrimaryName, 7,
                "promotion failure must preserve primary identity 7");
            RequireStoredIdentity(fileSystem, BackupName, 9,
                "promotion failure must keep new identity 9 in backup");
            Require(!fileSystem.Exists(TemporaryName), "promotion failure must clean temp bytes");
            RequireUnchangedCaller(changed, "promotion failure");
        }

        private static void FirstSaveBackupFailureDoesNotPromotePrimary()
        {
            var fileSystem = new FakeHatchHomeFileSystem();
            var store = new HatchHomeStore(Root, fileSystem);
            var changed = ChangedSnapshot();

            fileSystem.ThrowOnNextMove(BackupName);
            Require(!store.TrySave(changed), "first-save backup copy failure must report false");

            Require(!fileSystem.Exists(PrimaryName), "first save cannot promote primary before redundancy");
            Require(!fileSystem.Exists(BackupName),
                "first-save backup copy failure must not invent a completed backup");
            Require(!fileSystem.Exists(TemporaryName),
                "first-save backup copy failure must leave no temp after promotion");
            Require(store.Load().Source == "fresh" && store.Load().Snapshot.selectedArtId == 0,
                "failure before first backup promotion exposes no selected result");
            RequireUnchangedCaller(changed, "first-save backup copy failure");
        }

        private static void CleanupDeleteFailureIsBestEffort()
        {
            var fileSystem = new FakeHatchHomeFileSystem();
            var store = new HatchHomeStore(Root, fileSystem);
            Require(store.TrySave(HomeSnapshot(7)), "cleanup-delete fixture save must succeed");
            var primaryBefore = fileSystem.ReadAllText(PrimaryName);
            var backupBefore = fileSystem.ReadAllText(BackupName);
            var changed = ChangedSnapshot();

            fileSystem.ThrowOnNextRead(TemporaryName);
            fileSystem.ThrowOnNextDelete(TemporaryName);
            Require(!store.TrySave(changed), "cleanup delete failure must preserve the original false result");

            Require(fileSystem.ReadAllText(PrimaryName) == primaryBefore,
                "cleanup delete failure must preserve original primary bytes");
            Require(fileSystem.ReadAllText(BackupName) == backupBefore,
                "cleanup delete failure must preserve original backup bytes");
            Require(fileSystem.Exists(TemporaryName),
                "best-effort cleanup may leave temp bytes when delete fails");
            RequireUnchangedCaller(changed, "cleanup delete failure");

            Require(store.TrySave(HomeSnapshot(11)), "later save must replace stale temp after cleanup failure");
            Require(store.Load().Snapshot.selectedArtId == 11,
                "later save after cleanup failure must promote literal identity 11");
            Require(!fileSystem.Exists(TemporaryName), "later successful save must consume stale temp bytes");
        }

        private static void RawNumericIdentityIsRejectedBeforeNormalization()
        {
            var fileSystem = new FakeHatchHomeFileSystem();
            fileSystem.Seed(PrimaryName,
                "{\"schemaVersion\":2,\"phase\":\"4\",\"selectedArtId\":7," +
                "\"hatchCheckpoint\":\"3\",\"activeHomeView\":\"1\"}");
            fileSystem.Seed(BackupName, JsonUtility.ToJson(HomeSnapshot(13)));

            var loaded = new HatchHomeStore(Root, fileSystem).Load();
            Require(loaded.Source == "backup" && loaded.Snapshot.selectedArtId == 13,
                "raw numeric enum text must be rejected before normalization");
        }

        private static void MigrateReadyEggWithoutClaimingAnArtId()
        {
            var fileSystem = new FakeHatchHomeFileSystem();
            fileSystem.Seed(LegacyName,
                "{\"schemaVersion\":1,\"eggExperience\":30000,\"hatched\":false," +
                "\"selectedArtId\":22,\"dragonExperience\":120,\"coins\":33,\"snacks\":4," +
                "\"dayKey\":2026259,\"playsToday\":2,\"giftClaimedToday\":true," +
                "\"questClaimedToday\":false,\"soundEnabled\":false,\"lastPlayTicks\":99}");

            var migrated = new HatchHomeStore(Root, fileSystem).Load();
            Require(migrated.Source == "v1" && migrated.Recovered, "valid unhatched v1 must migrate");
            Require(migrated.Snapshot.phase == "HATCH_READY" && migrated.Snapshot.eggProgress == 30000,
                "unhatched 30,000 progress must migrate to HATCH_READY");
            Require(migrated.Snapshot.selectedArtId == 0 && migrated.Snapshot.hatchCheckpoint == "NONE" &&
                migrated.Snapshot.activeHomeView == "EGG", "unhatched migration must retain egg identity");
            Require(migrated.Snapshot.dragonExperience == 120 && migrated.Snapshot.coins == 33 &&
                migrated.Snapshot.snacks == 4 && migrated.Snapshot.dayKey == 2026259 &&
                migrated.Snapshot.playsToday == 2 && migrated.Snapshot.giftClaimedToday &&
                !migrated.Snapshot.questClaimedToday && !migrated.Snapshot.soundEnabled &&
                migrated.Snapshot.lastPlayTicks == 99, "v1 migration must preserve every legacy home value");
        }

        private static void MigrateHatchedHomeWithoutTouchingAvatarBytes()
        {
            var fileSystem = new FakeHatchHomeFileSystem();
            var avatarBytes = JsonUtility.ToJson(new AvatarProfile
            {
                displayName = "별이",
                skinToneId = 2,
                hairStyleId = 3,
            });
            fileSystem.Seed(AvatarName, avatarBytes);
            var avatarBeforeMigration = fileSystem.ReadAllText(AvatarName);
            fileSystem.Seed(LegacyName,
                "{\"schemaVersion\":1,\"eggExperience\":30000,\"hatched\":true," +
                "\"selectedArtId\":7,\"soundEnabled\":true}");

            var migrated = new HatchHomeStore(Root, fileSystem).Load();
            Require(migrated.Source == "v1" && migrated.Snapshot.phase == "HOME",
                "hatched v1 must migrate to HOME");
            Require(migrated.Snapshot.selectedArtId == 7 && migrated.Snapshot.hatchCheckpoint == "REVEALED" &&
                migrated.Snapshot.activeHomeView == "DINOSAUR", "hatched v1 art ID 7 must retain home identity");
            Require(fileSystem.ReadAllText(AvatarName) == avatarBeforeMigration,
                "hatch migration must not change serialized AvatarProfileStore bytes");
            Require(fileSystem.Exists(PrimaryName) && fileSystem.Exists(BackupName),
                "successful v1 migration must persist primary and immediate backup");
        }

        private static HatchHomeSnapshot HomeSnapshot(int artId)
        {
            return new HatchHomeSnapshot
            {
                phase = nameof(HatchHomePhase.HOME),
                eggProgress = 30000,
                selectedArtId = artId,
                hatchCheckpoint = nameof(HatchCheckpoint.REVEALED),
                activeHomeView = nameof(HomeSubject.DINOSAUR),
            };
        }

        private static void LegacyHatchedProgressIsCompletedWithoutChangingOwnership()
        {
            foreach (var hatched in new[] { false, true })
            {
                var fs = new FakeHatchHomeFileSystem();
                fs.Seed(AvatarName, "avatar bytes remain external to the hatch store");
                fs.Seed(LegacyName,
                    "{\"schemaVersion\":1,\"eggExperience\":12500,\"hatched\":" + (hatched ? "true" : "false") +
                    ",\"selectedArtId\":17,\"dragonExperience\":120,\"coins\":73,\"snacks\":4,\"dayKey\":2025001," +
                    "\"playsToday\":2,\"giftClaimedToday\":true,\"questClaimedToday\":true,\"soundEnabled\":false,\"lastPlayTicks\":99}");
                var store = new HatchHomeStore(Root, fs);
                var loaded = store.Load();
                var state = loaded.Snapshot;
                Require(loaded.Source == "v1" && state.eggProgress == (hatched ? 30000 : 12500) &&
                    state.phase == (hatched ? "HOME" : "EGG_ACTIVE") && state.selectedArtId == (hatched ? 17 : 0),
                    "only an already-hatched v1 save may complete progress; unhatched migration gains neither progress nor ownership");
                Require(state.dragonExperience == 120 && state.coins == 73 && state.snacks == 4 && state.dayKey == 2025001 &&
                    state.playsToday == 2 && state.giftClaimedToday && state.questClaimedToday && !state.soundEnabled && state.lastPlayTicks == 99,
                    "legacy completion preserves every legacy counter and preference");
                Require(fs.ReadAllText(AvatarName) == "avatar bytes remain external to the hatch store", "legacy completion leaves avatar bytes untouched");
                Require(state.HasValidDurableIdentity() && store.Load().Source == "primary", "migrated identity is persisted and re-loadable under v2 invariants");
            }
        }

        private static void MigrationWriteAmbiguityRetainsValidatedLegacyState()
        {
            var fs = new FakeHatchHomeFileSystem();
            fs.Seed(LegacyName, "{\"schemaVersion\":1,\"eggExperience\":12500,\"hatched\":true,\"selectedArtId\":17,\"coins\":73}");
            fs.FailAt("move", BackupName, 1, true);
            fs.ReadFailureName = BackupName;
            var store = new HatchHomeStore(Root, fs);
            var loaded = store.Load();
            Require(fs.FaultConsumed && loaded.Source == "v1" && loaded.Snapshot.selectedArtId == 17 &&
                loaded.Snapshot.phase == "HOME" && loaded.Snapshot.eggProgress == 30000 && loaded.Snapshot.coins == 73,
                "an ambiguous migration write cannot discard the already validated legacy ownership into fresh state");
            Require(!store.TrySave(loaded.Snapshot), "unavailable existing replica aborts the next transaction before mutation");
            fs.ReadFailureName = null;
            loaded.Snapshot.soundEnabled = false;
            Require(store.TrySave(loaded.Snapshot) && store.Load().Snapshot.selectedArtId == 17 && store.Load().Snapshot.coins == 73,
                "successful legacy Load supplies the exact candidate payload for safe later transactions");
        }

        private static HatchHomeSnapshot ChangedSnapshot()
        {
            var changed = HomeSnapshot(9);
            changed.coins = -8;
            return changed;
        }

        private static void RequireUnchangedCaller(HatchHomeSnapshot snapshot, string boundary)
        {
            Require(snapshot.selectedArtId == 9 && snapshot.coins == -8,
                $"{boundary} must not normalize or mutate the caller");
        }

        private static void RequireStoredIdentity(
            FakeHatchHomeFileSystem fileSystem,
            string name,
            int expectedArtId,
            string message)
        {
            var stored = JsonUtility.FromJson<HatchHomeSnapshot>(fileSystem.ReadAllText(name));
            Require(stored != null && stored.selectedArtId == expectedArtId, message);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class FakeHatchHomeFileSystem : IHatchHomeFileSystem
        {
            private readonly Dictionary<string, string> files = new();

            private string throwOnWriteOnce;
            private string throwOnReadOnce;
            private string throwOnCopyOnce;
            private string throwOnMoveOnce;
            private string throwOnDeleteOnce;
            private string faultOperation, faultName;
            private int faultOccurrence;
            private bool faultAfter;
            private string readFailureAfterFault;
            private int substituteOccurrence;
            private string substituteContents;
            private readonly Dictionary<string, int> operationCounts = new();
            public bool FaultConsumed { get; private set; }
            public string ReadFailureName;
            public int WriteCalls { get; private set; }
            public int MoveCalls { get; private set; }

            public void FailAt(string operation, string name, int occurrence, bool after)
            {
                operationCounts.Clear(); FaultConsumed = false;
                readFailureAfterFault = null;
                faultOperation = operation; faultName = name; faultOccurrence = occurrence; faultAfter = after;
            }

            public void FailAtThenReadFails(
                string operation, string name, int occurrence, bool after, string readName)
            {
                FailAt(operation, name, occurrence, after);
                readFailureAfterFault = readName;
            }

            public void SubstituteWrite(int occurrence, string contents)
            { operationCounts.Clear(); FaultConsumed = false; substituteOccurrence = occurrence; substituteContents = contents; }

            private void Boundary(string operation, string path, bool after)
            {
                var key = operation + ":" + Name(path);
                operationCounts.TryGetValue(key, out var count);
                if (!after) operationCounts[key] = ++count;
                if (!FaultConsumed && faultOperation == operation && faultName == Name(path) &&
                    faultOccurrence == count && faultAfter == after)
                {
                    FaultConsumed = true;
                    if (readFailureAfterFault != null) ReadFailureName = readFailureAfterFault;
                    throw new IOException("Injected interruption: " + key);
                }
            }

            public bool Exists(string path)
            {
                Boundary("exists", path, false);
                var exists = files.ContainsKey(Name(path));
                Boundary("exists", path, true);
                return exists;
            }

            public string ReadAllText(string path)
            {
                Boundary("read", path, false);
                if (Name(path) == ReadFailureName) throw new IOException("Persistent injected read failure.");
                if (Consume(ref throwOnReadOnce, path))
                    throw new IOException("Injected read failure.");
                if (!files.TryGetValue(Name(path), out var contents))
                    throw new FileNotFoundException(path);
                Boundary("read", path, true);
                return contents;
            }

            public void WriteAllText(string path, string contents)
            {
                WriteCalls++;
                Boundary("write", path, false);
                if (Consume(ref throwOnWriteOnce, path))
                    throw new IOException("Injected write failure.");
                files[Name(path)] = contents;
                if (substituteOccurrence > 0 && operationCounts["write:" + Name(path)] == substituteOccurrence)
                { files[Name(path)] = substituteContents; substituteOccurrence = 0; FaultConsumed = true; }
                Boundary("write", path, true);
            }

            public void Copy(string source, string destination, bool overwrite)
            {
                if (Consume(ref throwOnCopyOnce, destination))
                    throw new IOException("Injected copy failure.");
                var destinationName = Name(destination);
                if (!overwrite && files.ContainsKey(destinationName))
                    throw new IOException("Destination exists.");
                files[destinationName] = ReadAllText(source);
            }

            public void Move(string source, string destination, bool overwrite)
            {
                MoveCalls++;
                Boundary("move", destination, false);
                if (Consume(ref throwOnMoveOnce, destination))
                    throw new IOException("Injected move failure.");

                var sourceName = Name(source);
                var destinationName = Name(destination);
                if (!overwrite && files.ContainsKey(destinationName))
                    throw new IOException("Destination exists.");
                var contents = files[sourceName];
                files[destinationName] = contents;
                files.Remove(sourceName);
                Boundary("move", destination, true);
            }

            public void Delete(string path)
            {
                if (Consume(ref throwOnDeleteOnce, path))
                    throw new IOException("Injected delete failure.");
                files.Remove(Name(path));
            }

            public void Seed(string name, string contents) => files[name] = contents;
            public void Corrupt(string name) => files[name] = "{";
            public string Peek(string name) => files[name];
            public void ThrowOnNextWrite(string name) => throwOnWriteOnce = name;
            public void ThrowOnNextRead(string name) => throwOnReadOnce = name;
            public void ThrowOnNextCopy(string name) => throwOnCopyOnce = name;
            public void ThrowOnNextMove(string name) => throwOnMoveOnce = name;
            public void ThrowOnNextDelete(string name) => throwOnDeleteOnce = name;

            private static string Name(string path) => Path.GetFileName(path);

            private static bool Consume(ref string faultName, string path)
            {
                if (!string.Equals(faultName, Name(path), StringComparison.Ordinal))
                    return false;

                faultName = null;
                return true;
            }
        }
    }
}
#endif
