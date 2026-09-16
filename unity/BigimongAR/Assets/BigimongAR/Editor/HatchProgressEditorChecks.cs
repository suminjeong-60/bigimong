#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Bigimong.AR;
using UnityEngine;

namespace Bigimong.AR.EditorChecks
{
    public static class HatchProgressEditorChecks
    {
        private const long TwoHoursInTicks = 72000000000L;
        private static readonly DateTime Start = new(2026, 9, 16, 8, 0, 0, DateTimeKind.Utc);

        public static void RunBehaviorChecks()
        {
            CareFrom29499StopsAt29999();
            CareFrom29500ReachesReadyAt30000();
            CareBeforeTwoHoursReportsLiteralRemainder();
            CareAtExactlyTwoHoursIsAccepted();
            BackwardDeviceTimeCannotShortenCooldown();
            DuplicateCompositeEventIsAppliedOnce();
            SameDayStaleCumulativeCursorIsRejected();
            MismatchedDeltaAndCursorIsRejected();
            OlderProviderDayIsRejected();
            NewerProviderDayStartsAtZero();
            VerifiedStepsClampAt30000AndAdvanceTrustedCursor();
            InvalidStepShapesAreRejected();
            ProcessedEventHistoryRetainsLatest256();
            CareSaveFailureRollsBackCaller();
            StepSaveFailureRollsBackCaller();
            PostReadyProgressIsNoOp();
            JsonBridgeForwardsOnlyValidHostPayloads();
        }

        private static void CareFrom29499StopsAt29999()
        {
            var store = new FakeStore();
            var service = new HatchProgressService(store, new FakeClock(Start));
            var original = ActiveSnapshot(29499);

            var result = service.TryCare(original);

            Require(result.Status == HatchProgressStatus.APPLIED && result.Accepted,
                "29,499 care must return literal APPLIED");
            Require(result.Added == 500 && result.Snapshot.eggProgress == 29999,
                "29,499 + care must add literal 500 and stop at 29,999");
            Require(result.Snapshot.phase == "EGG_ACTIVE",
                "29,999 must remain literal EGG_ACTIVE");
            Require(result.Snapshot.lastObservedUtcTicks == Start.Ticks &&
                result.Snapshot.nextCareAtUtcTicks == Start.Ticks + TwoHoursInTicks,
                "accepted care must record now and a literal two-hour deadline");
            Require(store.SaveCount == 1, "accepted care must save exactly once");
            Require(original.eggProgress == 29499 && original.nextCareAtUtcTicks == 0,
                "accepted care must not mutate the caller before save");
        }

        private static void CareFrom29500ReachesReadyAt30000()
        {
            var store = new FakeStore();
            var result = new HatchProgressService(store, new FakeClock(Start))
                .TryCare(ActiveSnapshot(29500));

            Require(result.Status == HatchProgressStatus.APPLIED && result.Added == 500,
                "29,500 care must apply literal 500");
            Require(result.Snapshot.eggProgress == 30000 && result.Snapshot.phase == "HATCH_READY",
                "29,500 + care must produce literal 30,000/HATCH_READY");
            Require(store.SaveCount == 1 && store.LastSaved.eggProgress == 30000,
                "ready care must persist literal 30,000 exactly once");
        }

        private static void CareBeforeTwoHoursReportsLiteralRemainder()
        {
            var store = new FakeStore();
            var clock = new FakeClock(Start);
            var service = new HatchProgressService(store, clock);
            var first = service.TryCare(ActiveSnapshot(1000));
            clock.UtcNow = Start.AddHours(2).AddTicks(-1);

            var result = service.TryCare(first.Snapshot);

            Require(first.Status == HatchProgressStatus.APPLIED && first.Snapshot.eggProgress == 1500,
                "first care must apply and produce literal 1,500 before cooldown check");
            Require(result.Status == HatchProgressStatus.COOLDOWN && !result.Accepted,
                "second care one tick before deadline must return literal COOLDOWN");
            Require(result.RemainingCooldown.Ticks == 1 && result.Added == 0,
                "second care one tick early must report a literal one-tick remainder and zero added");
            Require(ReferenceEquals(result.Snapshot, first.Snapshot) && result.Snapshot.eggProgress == 1500,
                "cooldown must return and preserve the caller snapshot");
            Require(store.SaveCount == 1, "second care during cooldown must not perform another save");
        }

        private static void CareAtExactlyTwoHoursIsAccepted()
        {
            var store = new FakeStore();
            var clock = new FakeClock(Start);
            var service = new HatchProgressService(store, clock);
            var first = service.TryCare(ActiveSnapshot(1000));
            var exactDeadline = Start.AddHours(2);
            clock.UtcNow = exactDeadline;

            var result = service.TryCare(first.Snapshot);

            Require(result.Status == HatchProgressStatus.APPLIED && result.Added == 500,
                "second care at the exact two-hour boundary must apply literal 500");
            Require(result.Snapshot.eggProgress == 2000 &&
                result.Snapshot.nextCareAtUtcTicks == exactDeadline.Ticks + TwoHoursInTicks,
                "exact-boundary second care must produce literal 2,000 and a new two-hour deadline");
            Require(store.SaveCount == 2, "two accepted care events must save exactly twice");
        }

        private static void BackwardDeviceTimeCannotShortenCooldown()
        {
            var current = ActiveSnapshot(2000);
            current.lastObservedUtcTicks = Start.AddHours(1).Ticks;
            current.nextCareAtUtcTicks = Start.AddHours(3).Ticks;
            var store = new FakeStore();

            var result = new HatchProgressService(store, new FakeClock(Start)).TryCare(current);

            Require(result.Status == HatchProgressStatus.COOLDOWN && result.Added == 0,
                "backward time care must return literal COOLDOWN with zero added");
            Require(result.RemainingCooldown.Ticks == TwoHoursInTicks,
                "backward time must retain the literal two-hour remainder from last observed time");
            Require(current.lastObservedUtcTicks == Start.AddHours(1).Ticks && store.SaveCount == 0,
                "backward time rejection must not mutate or save");
        }

        private static void DuplicateCompositeEventIsAppliedOnce()
        {
            var store = new FakeStore();
            var service = new HatchProgressService(store, new FakeClock(Start));
            var first = service.ApplyVerifiedSteps(ActiveSnapshot(100), Step("health", "event-7", 20260916, 50, 50));
            var duplicate = service.ApplyVerifiedSteps(first.Snapshot, Step("health", "event-7", 20260916, 50, 50));

            Require(first.Status == HatchProgressStatus.APPLIED && first.Added == 50 &&
                first.Snapshot.eggProgress == 150, "first verified event must add literal 50");
            Require(first.Snapshot.processedStepEvents.Count == 1 &&
                first.Snapshot.processedStepEvents[0] == "health:event-7",
                "accepted event must persist the literal composite key health:event-7");
            Require(duplicate.Status == HatchProgressStatus.DUPLICATE && duplicate.Added == 0 &&
                duplicate.Snapshot.eggProgress == 150,
                "duplicate composite event must return literal DUPLICATE and retain 150");
            Require(store.SaveCount == 1, "duplicate event must not perform a second save");
        }

        private static void SameDayStaleCumulativeCursorIsRejected()
        {
            var current = ActiveSnapshot(700);
            current.stepProviderCursors.Add(Cursor("health", 20260916, 120));
            var store = new FakeStore();
            var service = new HatchProgressService(store, new FakeClock(Start));

            var equal = service.ApplyVerifiedSteps(current, Step("health", "equal", 20260916, 120, 10));
            var lower = service.ApplyVerifiedSteps(current, Step("health", "lower", 20260916, 119, 10));

            Require(equal.Status == HatchProgressStatus.DUPLICATE && equal.Snapshot.eggProgress == 700,
                "equal same-day cumulative total must return literal DUPLICATE and retain 700");
            Require(lower.Status == HatchProgressStatus.DUPLICATE && lower.Snapshot.eggProgress == 700,
                "lower same-day cumulative total must return literal DUPLICATE and retain 700");
            Require(store.SaveCount == 0, "stale same-day cursors must never save");
        }

        private static void MismatchedDeltaAndCursorIsRejected()
        {
            var current = ActiveSnapshot(800);
            current.stepProviderCursors.Add(Cursor("health", 20260916, 100));
            var store = new FakeStore();

            var result = new HatchProgressService(store, new FakeClock(Start))
                .ApplyVerifiedSteps(current, Step("health", "mismatch", 20260916, 160, 50));

            Require(result.Status == HatchProgressStatus.INVALID && result.Added == 0 &&
                result.Snapshot.eggProgress == 800,
                "cursor 100, cumulative 160, delta 50 must return literal INVALID and retain 800");
            Require(store.SaveCount == 0, "mismatched trusted cumulative delta must not save");
        }

        private static void OlderProviderDayIsRejected()
        {
            var current = ActiveSnapshot(900);
            current.stepProviderCursors.Add(Cursor("health", 20260916, 1000));
            var store = new FakeStore();

            var result = new HatchProgressService(store, new FakeClock(Start))
                .ApplyVerifiedSteps(current, Step("health", "old-day", 20260915, 20, 20));

            Require(result.Status == HatchProgressStatus.DUPLICATE && result.Added == 0 &&
                result.Snapshot.eggProgress == 900,
                "day 20260915 after 20260916 must return literal DUPLICATE and retain 900");
            Require(store.SaveCount == 0, "older provider days must not save");
        }

        private static void NewerProviderDayStartsAtZero()
        {
            var current = ActiveSnapshot(1000);
            current.stepProviderCursors.Add(Cursor("health", 20260916, 5000));
            var store = new FakeStore();

            var result = new HatchProgressService(store, new FakeClock(Start))
                .ApplyVerifiedSteps(current, Step("health", "new-day", 20260917, 40, 40));

            Require(result.Status == HatchProgressStatus.APPLIED && result.Added == 40 &&
                result.Snapshot.eggProgress == 1040,
                "new day cumulative 40 must start at zero and add literal 40");
            Require(result.Snapshot.stepProviderCursors.Count == 2 &&
                result.Snapshot.stepProviderCursors[1].dayKey == 20260917 &&
                result.Snapshot.stepProviderCursors[1].cumulativeTotal == 40,
                "new day must append literal cursor 20260917/40");
            Require(store.SaveCount == 1, "accepted newer-day event must save exactly once");
        }

        private static void VerifiedStepsClampAt30000AndAdvanceTrustedCursor()
        {
            var current = ActiveSnapshot(29990);
            current.stepProviderCursors.Add(Cursor("health", 20260916, 100));
            var store = new FakeStore();

            var result = new HatchProgressService(store, new FakeClock(Start))
                .ApplyVerifiedSteps(current, Step("health", "finish", 20260916, 125, 25));

            Require(result.Status == HatchProgressStatus.APPLIED && result.Added == 10,
                "25 trusted steps with 10 remaining must report literal APPLIED/10");
            Require(result.Snapshot.eggProgress == 30000 && result.Snapshot.phase == "HATCH_READY",
                "verified steps must clamp at literal 30,000 and transition to HATCH_READY");
            Require(result.Snapshot.stepProviderCursors[0].cumulativeTotal == 125 &&
                result.Snapshot.processedStepEvents[0] == "health:finish",
                "clamped progress must still record literal trusted cursor 125 and composite event");
            Require(store.SaveCount == 1, "clamped verified steps must save exactly once");
        }

        private static void InvalidStepShapesAreRejected()
        {
            var store = new FakeStore();
            var service = new HatchProgressService(store, new FakeClock(Start));
            var current = ActiveSnapshot(1100);

            Require(service.ApplyVerifiedSteps(current, Step(" ", "event", 20260916, 1, 1)).Status ==
                HatchProgressStatus.INVALID, "blank provider must return literal INVALID");
            Require(service.ApplyVerifiedSteps(current, Step("health", " ", 20260916, 1, 1)).Status ==
                HatchProgressStatus.INVALID, "blank event must return literal INVALID");
            Require(service.ApplyVerifiedSteps(current, Step("health", "bad-day", 0, 1, 1)).Status ==
                HatchProgressStatus.INVALID, "day key zero must return literal INVALID");
            Require(service.ApplyVerifiedSteps(current, Step("health", "negative", 20260916, 1, -1)).Status ==
                HatchProgressStatus.INVALID, "negative delta must return literal INVALID");
            Require(service.ApplyVerifiedSteps(current, Step("health", "too-large", 20260916, 4, 5)).Status ==
                HatchProgressStatus.INVALID, "cumulative below delta must return literal INVALID");
            Require(current.eggProgress == 1100 && store.SaveCount == 0,
                "invalid step shapes must retain literal 1,100 and never save");
        }

        private static void ProcessedEventHistoryRetainsLatest256()
        {
            var current = ActiveSnapshot(1200);
            for (var index = 0; index < 256; index++)
                current.processedStepEvents.Add("health:old-" + index);
            var store = new FakeStore();

            var result = new HatchProgressService(store, new FakeClock(Start))
                .ApplyVerifiedSteps(current, Step("health", "new", 20260916, 2, 2));

            Require(result.Status == HatchProgressStatus.APPLIED && result.Snapshot.eggProgress == 1202,
                "event 257 must apply literal 2 points");
            Require(result.Snapshot.processedStepEvents.Count == 256 &&
                result.Snapshot.processedStepEvents[0] == "health:old-1" &&
                result.Snapshot.processedStepEvents[255] == "health:new",
                "event history must retain the latest literal 256 composite keys");
            Require(current.processedStepEvents.Count == 256 && current.processedStepEvents[0] == "health:old-0",
                "history trimming must occur on a clone, not the caller");
        }

        private static void CareSaveFailureRollsBackCaller()
        {
            var store = new FakeStore { SaveSucceeds = false };
            var current = ActiveSnapshot(1300);

            var result = new HatchProgressService(store, new FakeClock(Start)).TryCare(current);

            Require(result.Status == HatchProgressStatus.SAVE_FAILED && result.Added == 0 &&
                ReferenceEquals(result.Snapshot, current),
                "failed care save must return literal SAVE_FAILED with original snapshot and zero added");
            Require(current.eggProgress == 1300 && current.nextCareAtUtcTicks == 0,
                "failed care save must retain literal caller progress 1,300 and zero deadline");
            Require(store.SaveCount == 1 && store.LastSaved.eggProgress == 1800,
                "failed care save must attempt exactly one cloned candidate at literal 1,800");
        }

        private static void StepSaveFailureRollsBackCaller()
        {
            var store = new FakeStore { SaveSucceeds = false };
            var current = ActiveSnapshot(1400);

            var result = new HatchProgressService(store, new FakeClock(Start))
                .ApplyVerifiedSteps(current, Step("health", "failed", 20260916, 60, 60));

            Require(result.Status == HatchProgressStatus.SAVE_FAILED && result.Added == 0 &&
                ReferenceEquals(result.Snapshot, current),
                "failed step save must return literal SAVE_FAILED with original snapshot and zero added");
            Require(current.eggProgress == 1400 && current.processedStepEvents.Count == 0 &&
                current.stepProviderCursors.Count == 0,
                "failed step save must not mutate caller progress, event history, or cursors");
            Require(store.SaveCount == 1 && store.LastSaved.eggProgress == 1460 &&
                store.LastSaved.processedStepEvents[0] == "health:failed",
                "failed step save must attempt one cloned candidate with literal 1,460 and event key");
        }

        private static void PostReadyProgressIsNoOp()
        {
            var ready = ActiveSnapshot(30000);
            ready.phase = nameof(HatchHomePhase.HATCH_READY);
            var store = new FakeStore();
            var service = new HatchProgressService(store, new FakeClock(Start));

            var care = service.TryCare(ready);
            var steps = service.ApplyVerifiedSteps(ready, Step("health", "after-ready", 20260916, 100, 100));

            Require(care.Status == HatchProgressStatus.WRONG_PHASE && care.Added == 0 &&
                steps.Status == HatchProgressStatus.WRONG_PHASE && steps.Added == 0,
                "care and steps after ready must both return literal WRONG_PHASE with zero added");
            Require(ReferenceEquals(care.Snapshot, ready) && ReferenceEquals(steps.Snapshot, ready) &&
                ready.eggProgress == 30000 && store.SaveCount == 0,
                "post-ready requests must retain literal 30,000 and never save");
        }

        private static void JsonBridgeForwardsOnlyValidHostPayloads()
        {
            var gameObject = new GameObject("HatchHomeStepBridge-check");
            try
            {
                var bridge = gameObject.AddComponent<HatchHomeStepBridge>();
                VerifiedStepDelta received = null;
                var invalidCount = 0;
                var invalidReason = string.Empty;
                bridge.VerifiedStepsReceived += value => received = value;
                bridge.InvalidPayload += reason =>
                {
                    invalidCount++;
                    invalidReason = reason;
                };

                Require(!bridge.IsAvailable && received == null,
                    "bridge without a host must report literal unavailable and emit no delta");
                bridge.ApplyVerifiedStepsJson(
                    "{\"providerId\":\"health\",\"eventId\":\"native-1\",\"dayKey\":20260916," +
                    "\"cumulativeTotal\":25,\"delta\":25}");
                Require(received != null && received.providerId == "health" &&
                    received.eventId == "native-1" && received.dayKey == 20260916 &&
                    received.cumulativeTotal == 25 && received.delta == 25,
                    "valid host JSON must forward literal health/native-1/20260916/25/25 unchanged");
                Require(bridge.IsAvailable,
                    "a valid host payload must mark the walking provider literally available");

                received = null;
                bridge.ApplyVerifiedStepsJson(
                    "{\"providerId\":\"health\",\"eventId\":\"bad\",\"dayKey\":20260916," +
                    "\"cumulativeTotal\":1,\"delta\":-1}");
                Require(received == null && invalidCount == 1 && invalidReason == "invalid_step_payload",
                    "negative host JSON must emit one literal invalid_step_payload and no delta");

                bridge.ApplyVerifiedStepsJson("{");
                Require(received == null && invalidCount == 2 && invalidReason == "invalid_step_payload",
                    "malformed host JSON must emit a second literal invalid_step_payload and no delta");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static HatchHomeSnapshot ActiveSnapshot(int progress) => new()
        {
            phase = nameof(HatchHomePhase.EGG_ACTIVE),
            eggProgress = progress,
        };

        private static VerifiedStepDelta Step(
            string providerId, string eventId, int dayKey, long cumulativeTotal, int delta) => new()
        {
            providerId = providerId,
            eventId = eventId,
            dayKey = dayKey,
            cumulativeTotal = cumulativeTotal,
            delta = delta,
        };

        private static StepProviderCursor Cursor(string providerId, int dayKey, long cumulativeTotal) => new()
        {
            providerId = providerId,
            dayKey = dayKey,
            cumulativeTotal = cumulativeTotal,
        };

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class FakeClock : ICareClock
        {
            public DateTime UtcNow { get; set; }

            public FakeClock(DateTime utcNow)
            {
                UtcNow = utcNow;
            }
        }

        private sealed class FakeStore : IHatchHomeStore
        {
            public bool SaveSucceeds { get; set; } = true;
            public int SaveCount { get; private set; }
            public HatchHomeSnapshot LastSaved { get; private set; }

            public HatchLoadResult Load() => new(ActiveSnapshot(0), false, "fake");

            public bool TrySave(HatchHomeSnapshot snapshot)
            {
                SaveCount++;
                LastSaved = snapshot.Clone();
                return SaveSucceeds;
            }
        }
    }
}
#endif
