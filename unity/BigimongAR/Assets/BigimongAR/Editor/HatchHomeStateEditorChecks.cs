#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Bigimong.AR;

namespace Bigimong.AR.EditorChecks
{
    public static class HatchHomeStateEditorChecks
    {
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
            PhaseProgressBoundariesAreLiteral();
            NormalizeInvalidAndBoundaryValues();
            PreserveAllowedHomeSubjects();
            CloneIsDeep();
            RetainNewest256ProcessedEvents();
            DeduplicateProviderCursorsByHighestTotal();
            ResolveAllThirtyUniqueSpecies();
        }

        private static void PhaseProgressBoundariesAreLiteral()
        {
            foreach (var example in new[]
            {
                ("EGG_ACTIVE", 0, 0, "NONE", "EGG", true),
                ("EGG_ACTIVE", 29999, 0, "NONE", "AVATAR", true),
                ("EGG_ACTIVE", 30000, 0, "NONE", "EGG", false),
                ("HATCH_READY", 29999, 0, "NONE", "EGG", false),
                ("HATCH_READY", 30000, 0, "NONE", "AVATAR", true),
                ("HATCHING", 30000, 17, "STARTED", "EGG", true),
                ("HATCHING", 29999, 17, "SHELL_BURST", "EGG", false),
                ("REVEAL", 30000, 17, "REVEALED", "DINOSAUR", true),
                ("REVEAL", 0, 17, "REVEALED", "DINOSAUR", false),
                ("HOME", 30000, 17, "REVEALED", "AVATAR", true),
                ("HOME", 29999, 17, "REVEALED", "DINOSAUR", false),
                ("HOME", 30001, 17, "REVEALED", "DINOSAUR", false),
            })
            {
                var snapshot = new HatchHomeSnapshot
                {
                    phase = example.Item1, eggProgress = example.Item2, selectedArtId = example.Item3,
                    hatchCheckpoint = example.Item4, activeHomeView = example.Item5,
                };
                Require(snapshot.HasValidDurableIdentity() == example.Item6, "literal phase/progress boundary " + example);
            }
        }

        private static void NormalizeInvalidAndBoundaryValues()
        {
            var snapshot = new HatchHomeSnapshot
            {
                schemaVersion = 99,
                phase = "not-a-phase",
                eggProgress = -1,
                nextCareAtUtcTicks = -1,
                lastObservedUtcTicks = -1,
                selectedArtId = 31,
                hatchCheckpoint = "not-a-checkpoint",
                hatchedAtUtcTicks = -1,
                activeHomeView = "not-a-subject",
                dragonExperience = -1,
                coins = -1,
                snacks = -1,
                dayKey = -1,
                playsToday = -1,
                lastPlayTicks = -1,
            };

            snapshot.Normalize();

            Require(snapshot.schemaVersion == 2, "normalization must set schema version 2");
            Require(snapshot.phase == "EGG_ACTIVE", "invalid phase must fall back to EGG_ACTIVE");
            Require(snapshot.hatchCheckpoint == "NONE", "egg phase must use NONE checkpoint");
            Require(snapshot.activeHomeView == "EGG", "invalid egg subject must fall back to EGG");
            Require(snapshot.eggProgress == 0 && snapshot.selectedArtId == 0, "egg values must clamp to zero");
            Require(snapshot.nextCareAtUtcTicks == 0 && snapshot.lastObservedUtcTicks == 0 && snapshot.hatchedAtUtcTicks == 0,
                "negative UTC ticks must clamp to zero");
            Require(snapshot.dragonExperience == 0 && snapshot.coins == 0 && snapshot.snacks == 0 &&
                snapshot.dayKey == 0 && snapshot.playsToday == 0 && snapshot.lastPlayTicks == 0,
                "negative counters must clamp to zero");

            var numericEnumIdentity = new HatchHomeSnapshot
            {
                phase = "EGG_ACTIVE",
                hatchCheckpoint = "0",
                activeHomeView = "0",
            };
            Require(!numericEnumIdentity.HasValidDurableIdentity(),
                "raw numeric enum strings must not pass durable identity validation");

            var ready = new HatchHomeSnapshot
            {
                phase = "HATCH_READY",
                selectedArtId = 12,
                activeHomeView = "AVATAR",
            };
            ready.Normalize();
            Require(ready.selectedArtId == 0 && ready.activeHomeView == "AVATAR",
                "ready phase must clear art but preserve AVATAR view");

            var boundary = new HatchHomeSnapshot
            {
                phase = "HATCHING",
                selectedArtId = 31,
                eggProgress = 30001,
                dragonExperience = 29001,
                coins = 1000001,
                snacks = 1001,
                playsToday = 101,
            };
            boundary.Normalize();
            Require(boundary.selectedArtId == 30 && boundary.eggProgress == 30000,
                "art ID and egg progress must clamp to their upper boundaries");
            Require(boundary.dragonExperience == 29000 && boundary.coins == 1000000 &&
                boundary.snacks == 1000 && boundary.playsToday == 100,
                "legacy home counters must retain their established upper boundaries");
        }

        private static void PreserveAllowedHomeSubjects()
        {
            var hatching = new HatchHomeSnapshot
            {
                phase = "HATCHING",
                selectedArtId = 7,
                hatchCheckpoint = "SHELL_BURST",
                activeHomeView = "AVATAR",
            };
            hatching.Normalize();
            Require(hatching.activeHomeView == "EGG" && hatching.hatchCheckpoint == "SHELL_BURST",
                "hatching must force EGG while preserving a valid shell checkpoint");

            var reveal = new HatchHomeSnapshot
            {
                phase = "REVEAL",
                selectedArtId = 7,
                hatchCheckpoint = "STARTED",
                activeHomeView = "AVATAR",
            };
            reveal.Normalize();
            Require(reveal.activeHomeView == "AVATAR" && reveal.hatchCheckpoint == "REVEALED",
                "reveal must preserve AVATAR and force REVEALED checkpoint");

            reveal.activeHomeView = "EGG";
            reveal.Normalize();
            Require(reveal.activeHomeView == "DINOSAUR", "reveal must use DINOSAUR as its invalid-subject fallback");
        }

        private static void CloneIsDeep()
        {
            var original = new HatchHomeSnapshot
            {
                processedStepEvents = new List<string> { "event-1" },
                stepProviderCursors = new List<StepProviderCursor>
                {
                    new() { providerId = "health", dayKey = 2026260, cumulativeTotal = 50 },
                },
            };
            var clone = original.Clone();
            clone.processedStepEvents.Add("event-2");
            clone.stepProviderCursors[0].cumulativeTotal = 999;

            Require(original.processedStepEvents.Count == 1, "clone event changes must not mutate original list");
            Require(original.stepProviderCursors[0].cumulativeTotal == 50,
                "clone cursor changes must not mutate original cursor");
        }

        private static void RetainNewest256ProcessedEvents()
        {
            var snapshot = new HatchHomeSnapshot { processedStepEvents = new List<string>() };
            snapshot.processedStepEvents.Add(" ");
            for (var index = 0; index < 260; index++)
                snapshot.processedStepEvents.Add($" event-{index} ");
            snapshot.processedStepEvents.Add("event-259");

            snapshot.Normalize();

            Require(snapshot.processedStepEvents.Count == 256, "processed events must retain exactly 256 IDs");
            Require(snapshot.processedStepEvents[0] == "event-4" && snapshot.processedStepEvents[255] == "event-259",
                "processed events must keep the newest 256 trimmed IDs in order");
        }

        private static void DeduplicateProviderCursorsByHighestTotal()
        {
            var snapshot = new HatchHomeSnapshot
            {
                stepProviderCursors = new List<StepProviderCursor>
                {
                    new() { providerId = " health ", dayKey = 2026260, cumulativeTotal = 100 },
                    new() { providerId = "health", dayKey = 2026260, cumulativeTotal = 150 },
                    new() { providerId = "health", dayKey = 2026261, cumulativeTotal = 9 },
                    new() { providerId = "", dayKey = 2026261, cumulativeTotal = 1000 },
                    new() { providerId = "health", dayKey = 2026260, cumulativeTotal = 130 },
                },
            };

            snapshot.Normalize();

            Require(snapshot.stepProviderCursors.Count == 2, "cursor normalization must remove duplicate provider/day entries");
            Require(snapshot.stepProviderCursors[0].providerId == "health" &&
                snapshot.stepProviderCursors[0].dayKey == 2026260 &&
                snapshot.stepProviderCursors[0].cumulativeTotal == 150,
                "cursor normalization must retain the highest total for the first provider/day entry");
            Require(snapshot.stepProviderCursors[1].dayKey == 2026261 && snapshot.stepProviderCursors[1].cumulativeTotal == 9,
                "cursor normalization must retain separate provider days");
        }

        private static void ResolveAllThirtyUniqueSpecies()
        {
            for (var artId = 1; artId <= 30; artId++)
            {
                var species = BigimongSpeciesCatalog.Resolve(artId);
                Require(species.ArtId == artId, $"species {artId} must resolve to its own art ID");
                Require(species.KoreanName == ExpectedKoreanNames[artId - 1],
                    $"species {artId} must retain its exact Korean catalog name");
                Require(species.GrowthStage == "아동기" && species.Grade == "기본",
                    $"species {artId} must use the v0.19 default growth and grade");
            }

            Require(ExpectedKoreanNames.Length == 30, "catalog check must define all 30 literal Korean names");
            Require(BigimongSpeciesCatalog.Resolve(0).ArtId == 1 && BigimongSpeciesCatalog.Resolve(31).ArtId == 30,
                "catalog resolve must clamp art IDs to its two boundary species");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
#endif
