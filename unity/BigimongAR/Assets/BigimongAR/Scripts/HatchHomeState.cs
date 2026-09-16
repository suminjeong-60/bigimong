using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bigimong.AR
{
    public enum HatchHomePhase { EGG_ACTIVE, HATCH_READY, HATCHING, REVEAL, HOME }
    public enum HatchCheckpoint { NONE, STARTED, SHELL_BURST, REVEALED }
    public enum HomeSubject { EGG, DINOSAUR, AVATAR }

    [Serializable]
    public sealed class StepProviderCursor
    {
        public string providerId = string.Empty;
        public int dayKey;
        public long cumulativeTotal;
    }

    [Serializable]
    public sealed class HatchHomeSnapshot
    {
        public const int CurrentSchemaVersion = 2;
        public const int HatchTarget = 30000;
        public const int ProcessedEventLimit = 256;

        public int schemaVersion = CurrentSchemaVersion;
        // Missing in older v2 JSON means revision zero. Only the store assigns revisions.
        public long revision;
        public string phase = nameof(HatchHomePhase.EGG_ACTIVE);
        public int eggProgress;
        public long nextCareAtUtcTicks;
        public long lastObservedUtcTicks;
        public int selectedArtId;
        public string hatchCheckpoint = nameof(HatchCheckpoint.NONE);
        public long hatchedAtUtcTicks;
        public string activeHomeView = nameof(HomeSubject.EGG);
        public List<StepProviderCursor> stepProviderCursors = new();
        public List<string> processedStepEvents = new();
        public int dragonExperience;
        public int coins;
        public int snacks;
        public int dayKey;
        public int playsToday;
        public bool giftClaimedToday;
        public bool questClaimedToday;
        public bool soundEnabled = true;
        public long lastPlayTicks;

        public HatchHomePhase Phase =>
            Enum.TryParse(phase, out HatchHomePhase value) ? value : HatchHomePhase.EGG_ACTIVE;
        public HatchCheckpoint Checkpoint =>
            Enum.TryParse(hatchCheckpoint, out HatchCheckpoint value) ? value : HatchCheckpoint.NONE;
        public HomeSubject ActiveSubject =>
            Enum.TryParse(activeHomeView, out HomeSubject value) ? value : HomeSubject.EGG;
        public HatchHomeSnapshot Clone() => JsonUtility.FromJson<HatchHomeSnapshot>(JsonUtility.ToJson(this));

        public bool HasValidDurableIdentity()
        {
            if (!TryParseDefined(phase, out HatchHomePhase parsedPhase) ||
                !TryParseDefined(hatchCheckpoint, out HatchCheckpoint parsedCheckpoint) ||
                !TryParseDefined(activeHomeView, out HomeSubject parsedSubject)) return false;

            // A phase cannot claim readiness or ownership without completed hatch progress.
            // Validate raw JSON before normalization so clamping cannot invent completion.
            if (eggProgress < 0 || !(parsedPhase == HatchHomePhase.EGG_ACTIVE
                ? eggProgress < HatchTarget : eggProgress == HatchTarget)) return false;

            var hasArt = selectedArtId is >= 1 and <= BigimongSpeciesCatalog.Count;
            return parsedPhase switch
            {
                HatchHomePhase.EGG_ACTIVE or HatchHomePhase.HATCH_READY =>
                    selectedArtId == 0 && parsedCheckpoint == HatchCheckpoint.NONE &&
                    parsedSubject is HomeSubject.EGG or HomeSubject.AVATAR,
                HatchHomePhase.HATCHING =>
                    hasArt && parsedCheckpoint is HatchCheckpoint.STARTED or HatchCheckpoint.SHELL_BURST &&
                    parsedSubject == HomeSubject.EGG,
                HatchHomePhase.REVEAL or HatchHomePhase.HOME =>
                    hasArt && parsedCheckpoint == HatchCheckpoint.REVEALED &&
                    parsedSubject is HomeSubject.DINOSAUR or HomeSubject.AVATAR,
                _ => false,
            };
        }

        public void Normalize()
        {
            schemaVersion = CurrentSchemaVersion;
            eggProgress = Mathf.Clamp(eggProgress, 0, HatchTarget);
            nextCareAtUtcTicks = Math.Max(0, nextCareAtUtcTicks);
            lastObservedUtcTicks = Math.Max(0, lastObservedUtcTicks);
            hatchedAtUtcTicks = Math.Max(0, hatchedAtUtcTicks);
            lastPlayTicks = Math.Max(0, lastPlayTicks);
            selectedArtId = Mathf.Clamp(selectedArtId, 0, BigimongSpeciesCatalog.Count);
            dragonExperience = Mathf.Clamp(dragonExperience, 0, 29000);
            coins = Mathf.Clamp(coins, 0, 1000000);
            snacks = Mathf.Clamp(snacks, 0, 1000);
            dayKey = Mathf.Max(0, dayKey);
            playsToday = Mathf.Clamp(playsToday, 0, 100);

            NormalizePhaseIdentity();
            NormalizeProcessedEvents();
            NormalizeStepProviderCursors();
        }

        private void NormalizePhaseIdentity()
        {
            var normalizedPhase = ParseOrDefault(phase, HatchHomePhase.EGG_ACTIVE);
            phase = normalizedPhase.ToString();

            switch (normalizedPhase)
            {
                case HatchHomePhase.EGG_ACTIVE:
                case HatchHomePhase.HATCH_READY:
                    selectedArtId = 0;
                    hatchCheckpoint = HatchCheckpoint.NONE.ToString();
                    activeHomeView = IsEggOrAvatar(ActiveSubject) ? ActiveSubject.ToString() : HomeSubject.EGG.ToString();
                    break;
                case HatchHomePhase.HATCHING:
                    hatchCheckpoint = Checkpoint is HatchCheckpoint.STARTED or HatchCheckpoint.SHELL_BURST
                        ? Checkpoint.ToString()
                        : HatchCheckpoint.STARTED.ToString();
                    activeHomeView = HomeSubject.EGG.ToString();
                    break;
                case HatchHomePhase.REVEAL:
                case HatchHomePhase.HOME:
                    hatchCheckpoint = HatchCheckpoint.REVEALED.ToString();
                    activeHomeView = IsDinosaurOrAvatar(ActiveSubject) ? ActiveSubject.ToString() : HomeSubject.DINOSAUR.ToString();
                    break;
            }
        }

        private void NormalizeProcessedEvents()
        {
            var normalized = new List<string>();
            var knownEvents = new HashSet<string>();
            foreach (var eventId in processedStepEvents ?? new List<string>())
            {
                var trimmed = eventId?.Trim();
                if (string.IsNullOrEmpty(trimmed) || !knownEvents.Add(trimmed)) continue;
                normalized.Add(trimmed);
            }

            var firstRetained = Math.Max(0, normalized.Count - ProcessedEventLimit);
            processedStepEvents = normalized.GetRange(firstRetained, normalized.Count - firstRetained);
        }

        private void NormalizeStepProviderCursors()
        {
            var normalized = new List<StepProviderCursor>();
            var cursorIndexes = new Dictionary<string, int>();
            foreach (var cursor in stepProviderCursors ?? new List<StepProviderCursor>())
            {
                var providerId = cursor?.providerId?.Trim();
                if (string.IsNullOrEmpty(providerId)) continue;

                var normalizedDayKey = Mathf.Max(0, cursor.dayKey);
                var normalizedTotal = Math.Max(0, cursor.cumulativeTotal);
                var key = providerId + "\u001f" + normalizedDayKey;
                if (cursorIndexes.TryGetValue(key, out var index))
                {
                    normalized[index].cumulativeTotal = Math.Max(normalized[index].cumulativeTotal, normalizedTotal);
                    continue;
                }

                cursorIndexes[key] = normalized.Count;
                normalized.Add(new StepProviderCursor
                {
                    providerId = providerId,
                    dayKey = normalizedDayKey,
                    cumulativeTotal = normalizedTotal,
                });
            }

            stepProviderCursors = normalized;
        }

        private static TEnum ParseOrDefault<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            return TryParseDefined(value, out TEnum parsed) ? parsed : fallback;
        }

        private static bool TryParseDefined<TEnum>(string value, out TEnum parsed) where TEnum : struct
        {
            return Enum.TryParse(value, out parsed) &&
                Enum.IsDefined(typeof(TEnum), parsed) &&
                string.Equals(value, Enum.GetName(typeof(TEnum), parsed), StringComparison.Ordinal);
        }

        private static bool IsEggOrAvatar(HomeSubject subject) =>
            subject is HomeSubject.EGG or HomeSubject.AVATAR;

        private static bool IsDinosaurOrAvatar(HomeSubject subject) =>
            subject is HomeSubject.DINOSAUR or HomeSubject.AVATAR;
    }
}
