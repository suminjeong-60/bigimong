using System;

namespace Bigimong.AR
{
    public interface IArtIdRandomSource
    {
        int NextArtId();
    }

    public sealed class UnityArtIdRandomSource : IArtIdRandomSource
    {
        public int NextArtId() => UnityEngine.Random.Range(1, 31);
    }

    public readonly struct HatchSelectionResult
    {
        public bool Accepted { get; }
        public HatchHomeSnapshot Snapshot { get; }
        public BigimongSpecies Species { get; }
        public string Error { get; }

        private HatchSelectionResult(
            bool accepted,
            HatchHomeSnapshot snapshot,
            BigimongSpecies species,
            string error)
        {
            Accepted = accepted;
            Snapshot = snapshot;
            Species = species;
            Error = error;
        }

        public static HatchSelectionResult Rejected(HatchHomeSnapshot state, string error) =>
            new(false, state, default, error);

        public static HatchSelectionResult AcceptedResult(
            HatchHomeSnapshot state,
            BigimongSpecies species) =>
            new(true, state, species, string.Empty);
    }

    public sealed class HatchSelectionService
    {
        private readonly IHatchHomeStore store;
        private readonly IArtIdRandomSource random;

        public HatchSelectionService(IHatchHomeStore store, IArtIdRandomSource random)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public HatchSelectionResult TryBegin(HatchHomeSnapshot current)
        {
            if (current == null || current.Phase != HatchHomePhase.HATCH_READY ||
                current.eggProgress != HatchHomeSnapshot.HatchTarget || current.selectedArtId != 0)
                return HatchSelectionResult.Rejected(current, "not_ready");

            var durableBeforeDraw = store.Load().Snapshot;
            if (TryResolveStartedSelection(durableBeforeDraw, out var durableSpecies))
                return HatchSelectionResult.AcceptedResult(durableBeforeDraw, durableSpecies);
            if (HasDurableSelection(durableBeforeDraw))
                return HatchSelectionResult.Rejected(current, "not_ready");

            var selectedArtId = random.NextArtId();
            if (selectedArtId is < 1 or > BigimongSpeciesCatalog.Count)
                return HatchSelectionResult.Rejected(current, "random_out_of_range");

            var candidate = current.Clone();
            candidate.selectedArtId = selectedArtId;
            candidate.phase = nameof(HatchHomePhase.HATCHING);
            candidate.hatchCheckpoint = nameof(HatchCheckpoint.STARTED);
            candidate.activeHomeView = nameof(HomeSubject.EGG);

            var candidateSpecies = BigimongSpeciesCatalog.Resolve(candidate.selectedArtId);
            if (store.TrySave(candidate))
                return HatchSelectionResult.AcceptedResult(candidate, candidateSpecies);

            var durableAfterFailure = store.Load().Snapshot;
            if (MatchesCandidate(durableAfterFailure, candidate, candidateSpecies, out durableSpecies))
                return HatchSelectionResult.AcceptedResult(durableAfterFailure, durableSpecies);

            return HatchSelectionResult.Rejected(current, "save_failed");
        }

        private static bool MatchesCandidate(
            HatchHomeSnapshot durable,
            HatchHomeSnapshot candidate,
            BigimongSpecies candidateSpecies,
            out BigimongSpecies durableSpecies)
        {
            if (!TryResolveStartedSelection(durable, out durableSpecies) ||
                durable.selectedArtId != candidate.selectedArtId ||
                durable.eggProgress != candidate.eggProgress ||
                durable.phase != candidate.phase ||
                durable.hatchCheckpoint != candidate.hatchCheckpoint ||
                durable.activeHomeView != candidate.activeHomeView)
                return false;

            return durableSpecies.ArtId == candidateSpecies.ArtId &&
                durableSpecies.KoreanName == candidateSpecies.KoreanName &&
                durableSpecies.GrowthStage == candidateSpecies.GrowthStage &&
                durableSpecies.Grade == candidateSpecies.Grade;
        }

        private static bool TryResolveStartedSelection(
            HatchHomeSnapshot snapshot,
            out BigimongSpecies species)
        {
            species = default;
            if (!HasDurableSelection(snapshot) ||
                snapshot.eggProgress != HatchHomeSnapshot.HatchTarget ||
                snapshot.phase != nameof(HatchHomePhase.HATCHING) ||
                snapshot.hatchCheckpoint != nameof(HatchCheckpoint.STARTED) ||
                snapshot.activeHomeView != nameof(HomeSubject.EGG))
                return false;

            species = BigimongSpeciesCatalog.Resolve(snapshot.selectedArtId);
            return species.ArtId == snapshot.selectedArtId;
        }

        private static bool HasDurableSelection(HatchHomeSnapshot snapshot) =>
            snapshot != null && snapshot.HasValidDurableIdentity() &&
            snapshot.selectedArtId is >= 1 and <= BigimongSpeciesCatalog.Count;
    }
}
