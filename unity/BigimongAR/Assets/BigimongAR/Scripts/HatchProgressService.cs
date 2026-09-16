using System;
using System.Collections.Generic;

namespace Bigimong.AR
{
    [Serializable]
    public sealed class VerifiedStepDelta
    {
        public string providerId = string.Empty;
        public string eventId = string.Empty;
        public int dayKey;
        public long cumulativeTotal;
        public int delta;

        public static bool IsValidShape(VerifiedStepDelta value) => value != null &&
            !string.IsNullOrWhiteSpace(value.providerId) &&
            !string.IsNullOrWhiteSpace(value.eventId) &&
            value.dayKey > 0 && value.cumulativeTotal >= value.delta && value.delta > 0;
    }

    public enum HatchProgressStatus
    {
        APPLIED,
        COOLDOWN,
        DUPLICATE,
        INVALID,
        WRONG_PHASE,
        SAVE_FAILED,
    }

    public readonly struct HatchProgressResult
    {
        public HatchProgressStatus Status { get; }
        public HatchHomeSnapshot Snapshot { get; }
        public int Added { get; }
        public TimeSpan RemainingCooldown { get; }
        public bool Accepted => Status == HatchProgressStatus.APPLIED;

        public HatchProgressResult(
            HatchProgressStatus status,
            HatchHomeSnapshot snapshot,
            int added,
            TimeSpan remainingCooldown)
        {
            Status = status;
            Snapshot = snapshot;
            Added = added;
            RemainingCooldown = remainingCooldown;
        }
    }

    public sealed class HatchProgressService
    {
        public const int CareAmount = 500;

        private readonly IHatchHomeStore store;
        private readonly ICareClock clock;

        public HatchProgressService(IHatchHomeStore store, ICareClock clock)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public HatchProgressResult TryCare(HatchHomeSnapshot current)
        {
            if (current == null)
                return Invalid(current);
            if (current.Phase != HatchHomePhase.EGG_ACTIVE)
                return WrongPhase(current);

            var utcNow = clock.UtcNow;
            var effectiveTicks = Math.Max(utcNow.ToUniversalTime().Ticks, current.lastObservedUtcTicks);
            if (effectiveTicks < current.nextCareAtUtcTicks)
                return Cooldown(current, TimeSpan.FromTicks(current.nextCareAtUtcTicks - effectiveTicks));

            var candidate = current.Clone();
            if (candidate == null)
                return Invalid(current);

            candidate.lastObservedUtcTicks = effectiveTicks;
            candidate.nextCareAtUtcTicks = effectiveTicks + TimeSpan.FromHours(2).Ticks;
            candidate.eggProgress = Math.Min(
                HatchHomeSnapshot.HatchTarget,
                candidate.eggProgress + CareAmount);
            if (candidate.eggProgress == HatchHomeSnapshot.HatchTarget)
                candidate.phase = nameof(HatchHomePhase.HATCH_READY);

            return Commit(current, candidate, candidate.eggProgress - current.eggProgress);
        }

        public HatchProgressResult ApplyVerifiedSteps(HatchHomeSnapshot current, VerifiedStepDelta step)
        {
            if (current == null || !VerifiedStepDelta.IsValidShape(step))
                return Invalid(current);
            if (current.Phase != HatchHomePhase.EGG_ACTIVE)
                return WrongPhase(current);

            var providerId = step.providerId.Trim();
            var eventId = step.eventId.Trim();
            var compositeEventId = providerId + ":" + eventId;
            if ((current.processedStepEvents ?? new List<string>()).Contains(compositeEventId))
                return Duplicate(current);

            var newestProviderDay = 0;
            long previousCursor = 0;
            foreach (var cursor in current.stepProviderCursors ?? new List<StepProviderCursor>())
            {
                if (cursor == null ||
                    !string.Equals(cursor.providerId?.Trim(), providerId, StringComparison.Ordinal))
                    continue;

                newestProviderDay = Math.Max(newestProviderDay, cursor.dayKey);
                if (cursor.dayKey == step.dayKey)
                    previousCursor = Math.Max(previousCursor, cursor.cumulativeTotal);
            }

            if (step.dayKey < newestProviderDay)
                return Duplicate(current);
            if (step.cumulativeTotal <= previousCursor)
                return Duplicate(current);
            if (step.cumulativeTotal - previousCursor != step.delta)
                return Invalid(current);

            var candidate = current.Clone();
            if (candidate == null)
                return Invalid(current);

            candidate.stepProviderCursors ??= new List<StepProviderCursor>();
            var savedCursor = FindCursor(candidate.stepProviderCursors, providerId, step.dayKey);
            if (savedCursor == null)
            {
                candidate.stepProviderCursors.Add(new StepProviderCursor
                {
                    providerId = providerId,
                    dayKey = step.dayKey,
                    cumulativeTotal = step.cumulativeTotal,
                });
            }
            else
            {
                savedCursor.providerId = providerId;
                savedCursor.cumulativeTotal = step.cumulativeTotal;
            }

            candidate.processedStepEvents ??= new List<string>();
            candidate.processedStepEvents.Add(compositeEventId);
            if (candidate.processedStepEvents.Count > HatchHomeSnapshot.ProcessedEventLimit)
            {
                candidate.processedStepEvents.RemoveRange(
                    0,
                    candidate.processedStepEvents.Count - HatchHomeSnapshot.ProcessedEventLimit);
            }

            var added = Math.Min(step.delta, HatchHomeSnapshot.HatchTarget - candidate.eggProgress);
            candidate.eggProgress += added;
            if (candidate.eggProgress == HatchHomeSnapshot.HatchTarget)
                candidate.phase = nameof(HatchHomePhase.HATCH_READY);

            return Commit(current, candidate, added);
        }

        private static StepProviderCursor FindCursor(
            IEnumerable<StepProviderCursor> cursors,
            string providerId,
            int dayKey)
        {
            foreach (var cursor in cursors)
            {
                if (cursor != null && cursor.dayKey == dayKey &&
                    string.Equals(cursor.providerId?.Trim(), providerId, StringComparison.Ordinal))
                    return cursor;
            }

            return null;
        }

        private HatchProgressResult Commit(
            HatchHomeSnapshot original,
            HatchHomeSnapshot candidate,
            int added)
        {
            return store.TrySave(candidate)
                ? new HatchProgressResult(HatchProgressStatus.APPLIED, candidate, added, TimeSpan.Zero)
                : new HatchProgressResult(HatchProgressStatus.SAVE_FAILED, original, 0, TimeSpan.Zero);
        }

        private static HatchProgressResult WrongPhase(HatchHomeSnapshot current) =>
            new(HatchProgressStatus.WRONG_PHASE, current, 0, TimeSpan.Zero);

        private static HatchProgressResult Cooldown(HatchHomeSnapshot current, TimeSpan remaining) =>
            new(HatchProgressStatus.COOLDOWN, current, 0, remaining);

        private static HatchProgressResult Duplicate(HatchHomeSnapshot current) =>
            new(HatchProgressStatus.DUPLICATE, current, 0, TimeSpan.Zero);

        private static HatchProgressResult Invalid(HatchHomeSnapshot current) =>
            new(HatchProgressStatus.INVALID, current, 0, TimeSpan.Zero);
    }
}
