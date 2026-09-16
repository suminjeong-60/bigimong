#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Bigimong.AR;

namespace Bigimong.AR.EditorChecks
{
    public static class HatchSelectionEditorChecks
    {
        private static readonly int[] ExpectedArtIds =
        {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
            11, 12, 13, 14, 15, 16, 17, 18, 19, 20,
            21, 22, 23, 24, 25, 26, 27, 28, 29, 30,
        };

        private static readonly string[] ExpectedKoreanNames =
        {
            "티라노사우루스", "트리케라톱스", "익룡", "스테고사우루스", "브라키오사우루스",
            "스피노사우루스", "안킬로사우루스", "벨로키랍토르", "코리토사우루스", "카르노타우루스",
            "모사사우루스", "알로사우루스", "케찰코아틀루스", "데이노니쿠스", "유오플로케팔루스",
            "바리오닉스", "오비랍토르", "프로토케라톱스", "갈리미무스", "드라코렉스",
            "기가노토사우루스", "딜로포사우루스", "이구아노돈", "켄트로사우루스", "테리지노사우루스",
            "콤프소그나투스", "파라사우롤로푸스", "미크로랍토르", "브론토사우루스", "티타노사우루스",
        };

        public static void RunBehaviorChecks()
        {
            EveryArtIdIsPersistedBeforeAcceptance();
            WrongStateAndDurableIdentityDoNotDraw();
            OutOfRangeDrawsDoNotSaveOrMutateCaller();
            SaveFailurePreservesCallerAndShowsNoSelection();
            PromotedPrimaryFalseIsReconciledBeforeStaleRetry();
            RelaunchAndRetryKeepDurableSelectionWithoutReroll();
        }

        private static void EveryArtIdIsPersistedBeforeAcceptance()
        {
            var random = new QueueRandomSource(ExpectedArtIds);
            var observedNames = new HashSet<string>();

            for (var index = 0; index < ExpectedArtIds.Length; index++)
            {
                var expectedArtId = ExpectedArtIds[index];
                var current = ReadySnapshot();
                var store = new FakeStore();

                var result = new HatchSelectionService(store, random).TryBegin(current);

                Require(result.Accepted && result.Error == string.Empty,
                    $"literal art ID {expectedArtId} must return accepted with no error");
                Require(store.SaveCount == 1 && store.DurableSnapshot != null,
                    $"literal art ID {expectedArtId} must be durable before acceptance is observed");
                Require(store.DurableSnapshot.selectedArtId == expectedArtId &&
                    store.DurableSnapshot.phase == "HATCHING" &&
                    store.DurableSnapshot.hatchCheckpoint == "STARTED" &&
                    store.DurableSnapshot.activeHomeView == "EGG",
                    $"literal art ID {expectedArtId} must persist HATCHING/STARTED/EGG together");
                Require(result.Snapshot.selectedArtId == expectedArtId &&
                    result.Snapshot.phase == "HATCHING" &&
                    result.Snapshot.hatchCheckpoint == "STARTED" &&
                    result.Snapshot.activeHomeView == "EGG",
                    $"accepted literal art ID {expectedArtId} must expose its persisted state");
                Require(result.Species.ArtId == expectedArtId &&
                    result.Species.KoreanName == ExpectedKoreanNames[index] &&
                    result.Species.GrowthStage == "아동기" && result.Species.Grade == "기본",
                    $"literal art ID {expectedArtId} must resolve its exact Korean baby identity and 기본 grade");
                Require(observedNames.Add(result.Species.KoreanName),
                    $"literal art ID {expectedArtId} must resolve to a unique Korean name");
                Require(current.selectedArtId == 0 && current.phase == "HATCH_READY" &&
                    current.hatchCheckpoint == "NONE" && current.activeHomeView == "EGG",
                    $"literal art ID {expectedArtId} must be applied to a clone, not the caller");
            }

            Require(random.DrawCount == 30 && random.Remaining == 0,
                "the literal equal pool 1 through 30 must consume exactly 30 queued draws");
            Require(observedNames.Count == 30,
                "the literal equal pool 1 through 30 must resolve to 30 unique Korean names");
        }

        private static void WrongStateAndDurableIdentityDoNotDraw()
        {
            var random = new QueueRandomSource(new[] { 9, 10, 11 });
            var store = new FakeStore();
            var service = new HatchSelectionService(store, random);
            var wrongPhase = ReadySnapshot();
            wrongPhase.phase = nameof(HatchHomePhase.EGG_ACTIVE);
            var incomplete = ReadySnapshot();
            incomplete.eggProgress = 29999;
            var durable = HatchingSnapshot(7);

            var wrongPhaseResult = service.TryBegin(wrongPhase);
            var incompleteResult = service.TryBegin(incomplete);
            var durableResult = service.TryBegin(durable);

            Require(!wrongPhaseResult.Accepted && wrongPhaseResult.Error == "not_ready" &&
                ReferenceEquals(wrongPhaseResult.Snapshot, wrongPhase),
                "EGG_ACTIVE at literal 30,000 must return not_ready with its caller");
            Require(!incompleteResult.Accepted && incompleteResult.Error == "not_ready" &&
                ReferenceEquals(incompleteResult.Snapshot, incomplete),
                "HATCH_READY at literal 29,999 must return not_ready with its caller");
            Require(!durableResult.Accepted && durableResult.Error == "not_ready" &&
                ReferenceEquals(durableResult.Snapshot, durable) && durableResult.Snapshot.selectedArtId == 7,
                "durable HATCHING art ID 7 must return not_ready and retain art ID 7");
            Require(random.DrawCount == 0 && random.Remaining == 3 && store.SaveCount == 0,
                "wrong phase, incomplete progress, and durable identity must consume zero random draws and saves");
        }

        private static void OutOfRangeDrawsDoNotSaveOrMutateCaller()
        {
            var random = new QueueRandomSource(new[] { 0, 31 });
            var store = new FakeStore();
            var service = new HatchSelectionService(store, random);
            var zeroCaller = ReadySnapshot();
            var thirtyOneCaller = ReadySnapshot();

            var zero = service.TryBegin(zeroCaller);
            var thirtyOne = service.TryBegin(thirtyOneCaller);

            Require(!zero.Accepted && zero.Error == "random_out_of_range" &&
                ReferenceEquals(zero.Snapshot, zeroCaller) && zeroCaller.selectedArtId == 0,
                "literal random art ID 0 must be rejected without changing the caller");
            Require(!thirtyOne.Accepted && thirtyOne.Error == "random_out_of_range" &&
                ReferenceEquals(thirtyOne.Snapshot, thirtyOneCaller) && thirtyOneCaller.selectedArtId == 0,
                "literal random art ID 31 must be rejected without changing the caller");
            Require(random.DrawCount == 2 && store.SaveCount == 0 && store.DurableSnapshot == null,
                "two out-of-range draws must consume exactly two values and persist nothing");
        }

        private static void SaveFailurePreservesCallerAndShowsNoSelection()
        {
            var current = ReadySnapshot();
            var store = new FakeStore { SaveSucceeds = false };
            var random = new QueueRandomSource(new[] { 12 });

            var result = new HatchSelectionService(store, random).TryBegin(current);

            Require(!result.Accepted && result.Error == "save_failed" &&
                ReferenceEquals(result.Snapshot, current),
                "failed save for literal art ID 12 must return save_failed with the caller");
            Require(current.selectedArtId == 0 && current.phase == "HATCH_READY" &&
                current.hatchCheckpoint == "NONE" && current.activeHomeView == "EGG",
                "failed save must show no selection and retain HATCH_READY/NONE/EGG");
            Require(store.SaveCount == 1 && store.LastAttempt != null &&
                store.LastAttempt.selectedArtId == 12 && store.LastAttempt.phase == "HATCHING" &&
                store.LastAttempt.hatchCheckpoint == "STARTED" && store.LastAttempt.activeHomeView == "EGG",
                "failed save must attempt one cloned literal 12/HATCHING/STARTED/EGG candidate");
            Require(store.DurableSnapshot == null && random.DrawCount == 1,
                "failed save must expose no durable selection after exactly one draw");
        }

        private static void PromotedPrimaryFalseIsReconciledBeforeStaleRetry()
        {
            var staleReadyCaller = ReadySnapshot();
            var store = new FakeStore { PromoteBeforeFailure = true };
            var random = new QueueRandomSource(new[] { 14, 26 });
            var service = new HatchSelectionService(store, random);

            var first = service.TryBegin(staleReadyCaller);

            Require(first.Accepted && first.Error == string.Empty &&
                first.Snapshot.selectedArtId == 14 && first.Snapshot.phase == "HATCHING" &&
                first.Snapshot.hatchCheckpoint == "STARTED" && first.Snapshot.activeHomeView == "EGG",
                "promoted-primary false must reconcile literal 14/HATCHING/STARTED/EGG as accepted");
            Require(first.Species.ArtId == 14 && first.Species.KoreanName == "데이노니쿠스" &&
                first.Species.GrowthStage == "아동기" && first.Species.Grade == "기본",
                "promoted-primary false must resolve literal art ID 14 as 데이노니쿠스/아동기/기본");
            Require(store.SaveCount == 1 && store.DurableSnapshot != null &&
                store.DurableSnapshot.selectedArtId == 14 && random.DrawCount == 1,
                "promoted-primary false must retain exactly one durable art ID 14 after one draw and save");

            var retry = service.TryBegin(staleReadyCaller);

            Require(retry.Accepted && retry.Error == string.Empty &&
                retry.Snapshot.selectedArtId == 14 && retry.Species.KoreanName == "데이노니쿠스",
                "stale ready retry must reconcile and return the existing durable literal art ID 14");
            Require(staleReadyCaller.selectedArtId == 0 && staleReadyCaller.phase == "HATCH_READY" &&
                staleReadyCaller.hatchCheckpoint == "NONE" && staleReadyCaller.activeHomeView == "EGG",
                "post-promotion reconciliation must never mutate the stale ready caller");
            Require(store.SaveCount == 1 && random.DrawCount == 1 && random.Remaining == 1 &&
                store.DurableSnapshot.selectedArtId == 14,
                "stale retry must not overwrite art ID 14 or consume queued literal art ID 26");
        }

        private static void RelaunchAndRetryKeepDurableSelectionWithoutReroll()
        {
            var store = new FakeStore();
            var random = new QueueRandomSource(new[] { 17, 23 });
            var service = new HatchSelectionService(store, random);
            var first = service.TryBegin(ReadySnapshot());

            Require(first.Accepted && first.Snapshot.selectedArtId == 17 &&
                store.DurableSnapshot != null && store.DurableSnapshot.selectedArtId == 17,
                "first draw must durably accept literal art ID 17");

            var retry = service.TryBegin(first.Snapshot);
            var relaunched = store.Load().Snapshot;
            var afterRelaunch = service.TryBegin(relaunched);

            Require(!retry.Accepted && retry.Error == "not_ready" &&
                ReferenceEquals(retry.Snapshot, first.Snapshot) && retry.Snapshot.selectedArtId == 17,
                "retry with durable literal art ID 17 must preserve that exact snapshot");
            Require(!afterRelaunch.Accepted && afterRelaunch.Error == "not_ready" &&
                ReferenceEquals(afterRelaunch.Snapshot, relaunched) && afterRelaunch.Snapshot.selectedArtId == 17,
                "relaunch with durable literal art ID 17 must preserve the loaded identity");
            Require(store.SaveCount == 1 && random.DrawCount == 1 && random.Remaining == 1,
                "retry and relaunch must not save again or consume queued literal art ID 23");
        }

        private static HatchHomeSnapshot ReadySnapshot() => new()
        {
            phase = nameof(HatchHomePhase.HATCH_READY),
            eggProgress = HatchHomeSnapshot.HatchTarget,
            selectedArtId = 0,
            hatchCheckpoint = nameof(HatchCheckpoint.NONE),
            activeHomeView = nameof(HomeSubject.EGG),
        };

        private static HatchHomeSnapshot HatchingSnapshot(int artId) => new()
        {
            phase = nameof(HatchHomePhase.HATCHING),
            eggProgress = HatchHomeSnapshot.HatchTarget,
            selectedArtId = artId,
            hatchCheckpoint = nameof(HatchCheckpoint.STARTED),
            activeHomeView = nameof(HomeSubject.EGG),
        };

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class QueueRandomSource : IArtIdRandomSource
        {
            private readonly Queue<int> values;

            public int DrawCount { get; private set; }
            public int Remaining => values.Count;

            public QueueRandomSource(IEnumerable<int> values)
            {
                this.values = new Queue<int>(values);
            }

            public int NextArtId()
            {
                DrawCount++;
                return values.Dequeue();
            }
        }

        private sealed class FakeStore : IHatchHomeStore
        {
            public bool SaveSucceeds { get; set; } = true;
            public bool PromoteBeforeFailure { get; set; }
            public int SaveCount { get; private set; }
            public HatchHomeSnapshot LastAttempt { get; private set; }
            public HatchHomeSnapshot DurableSnapshot { get; private set; }

            public HatchLoadResult Load()
            {
                var snapshot = DurableSnapshot?.Clone() ?? new HatchHomeSnapshot();
                return new HatchLoadResult(snapshot, false, "fake");
            }

            public bool TrySave(HatchHomeSnapshot snapshot)
            {
                SaveCount++;
                LastAttempt = snapshot.Clone();
                if (PromoteBeforeFailure)
                {
                    DurableSnapshot = LastAttempt.Clone();
                    return false;
                }
                if (!SaveSucceeds)
                    return false;

                DurableSnapshot = LastAttempt.Clone();
                return true;
            }
        }
    }
}
#endif
