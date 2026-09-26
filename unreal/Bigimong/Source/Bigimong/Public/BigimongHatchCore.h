#pragma once

// Portable, engine-independent transition rules for the Unity v0.19 hatch v2
// contract. A caller must save Change::state durably before showing success.
#include <algorithm>
#include <cctype>
#include <cstdint>
#include <limits>
#include <string>
#include <vector>

namespace BigimongHatch
{
    enum class Phase { EggActive, HatchReady, Hatching, Reveal, Home };
    enum class Checkpoint { None, Started, ShellBurst, Revealed };
    enum class Subject { Egg, Dinosaur, Avatar };
    enum class Status { Applied, Cooldown, Duplicate, Invalid, WrongPhase, SaveFailed };

    constexpr int HatchTarget = 30000;
    constexpr int CareAmount = 500;
    constexpr int SpeciesCount = 30;
    constexpr int64_t CareCooldownTicks = 2LL * 60 * 60 * 10000000;
    constexpr size_t EventLimit = 256;

    struct StepCursor
    {
        std::string providerId;
        int dayKey = 0;
        int64_t cumulativeTotal = 0;
    };

    struct StepDelta
    {
        std::string providerId;
        std::string eventId;
        int dayKey = 0;
        int64_t cumulativeTotal = 0;
        int64_t delta = 0;
    };

    struct State
    {
        int schemaVersion = 2;
        int64_t revision = 0;
        Phase phase = Phase::EggActive;
        Checkpoint checkpoint = Checkpoint::None;
        Subject subject = Subject::Egg;
        int eggProgress = 0;
        int selectedArtId = 0;
        int64_t nextCareAtUtcTicks = 0;
        int64_t lastObservedUtcTicks = 0;
        std::vector<StepCursor> stepCursors;
        std::vector<std::string> processedStepEvents;
    };

    struct Change
    {
        Status status;
        State state;
        int added = 0;
    };

    inline bool Valid(const State& state)
    {
        if (state.schemaVersion != 2 || state.revision < 0 ||
            state.eggProgress < 0 || state.eggProgress > HatchTarget ||
            state.nextCareAtUtcTicks < 0 || state.lastObservedUtcTicks < 0 ||
            state.processedStepEvents.size() > EventLimit)
            return false;
        for (const StepCursor& cursor : state.stepCursors)
            if (cursor.providerId.empty() || cursor.dayKey <= 0 || cursor.cumulativeTotal < 0)
                return false;
        for (const std::string& event : state.processedStepEvents)
            if (event.empty()) return false;

        const bool hasArt = state.selectedArtId >= 1 && state.selectedArtId <= SpeciesCount;
        switch (state.phase)
        {
        case Phase::EggActive:
            return state.eggProgress < HatchTarget && state.selectedArtId == 0 &&
                state.checkpoint == Checkpoint::None &&
                (state.subject == Subject::Egg || state.subject == Subject::Avatar);
        case Phase::HatchReady:
            return state.eggProgress == HatchTarget && state.selectedArtId == 0 &&
                state.checkpoint == Checkpoint::None &&
                (state.subject == Subject::Egg || state.subject == Subject::Avatar);
        case Phase::Hatching:
            return state.eggProgress == HatchTarget && hasArt &&
                (state.checkpoint == Checkpoint::Started || state.checkpoint == Checkpoint::ShellBurst) &&
                state.subject == Subject::Egg;
        case Phase::Reveal:
        case Phase::Home:
            return state.eggProgress == HatchTarget && hasArt &&
                state.checkpoint == Checkpoint::Revealed &&
                (state.subject == Subject::Dinosaur || state.subject == Subject::Avatar);
        }
        return false;
    }

    inline Change Reject(Status status, const State& original)
    {
        return {status, original, 0};
    }

    inline std::string TrimId(const std::string& value)
    {
        const auto first = std::find_if_not(value.begin(), value.end(),
            [](unsigned char c) { return std::isspace(c) != 0; });
        const auto last = std::find_if_not(value.rbegin(), value.rend(),
            [](unsigned char c) { return std::isspace(c) != 0; }).base();
        return first >= last ? std::string() : std::string(first, last);
    }

    inline Change Care(const State& original, int64_t nowUtcTicks)
    {
        if (!Valid(original) || nowUtcTicks < 0) return Reject(Status::Invalid, original);
        if (original.phase != Phase::EggActive) return Reject(Status::WrongPhase, original);
        const int64_t effectiveNow = std::max(nowUtcTicks, original.lastObservedUtcTicks);
        if (effectiveNow < original.nextCareAtUtcTicks) return Reject(Status::Cooldown, original);
        if (effectiveNow > std::numeric_limits<int64_t>::max() - CareCooldownTicks)
            return Reject(Status::Invalid, original);

        State next = original;
        next.lastObservedUtcTicks = effectiveNow;
        next.nextCareAtUtcTicks = effectiveNow + CareCooldownTicks;
        const int added = std::min(CareAmount, HatchTarget - next.eggProgress);
        next.eggProgress += added;
        if (next.eggProgress == HatchTarget) next.phase = Phase::HatchReady;
        return {Status::Applied, next, added};
    }

    inline Change VerifiedSteps(const State& original, const StepDelta& delta)
    {
        const std::string providerId = TrimId(delta.providerId);
        const std::string eventId = TrimId(delta.eventId);
        if (!Valid(original) || providerId.empty() || eventId.empty() ||
            delta.dayKey <= 0 || delta.delta <= 0 || delta.cumulativeTotal < delta.delta)
            return Reject(Status::Invalid, original);
        if (original.phase != Phase::EggActive) return Reject(Status::WrongPhase, original);

        const std::string event = providerId + ":" + eventId;
        if (std::find(original.processedStepEvents.begin(), original.processedStepEvents.end(), event)
                != original.processedStepEvents.end())
            return Reject(Status::Duplicate, original);

        int newestDay = 0;
        int64_t previous = 0;
        for (const StepCursor& cursor : original.stepCursors)
        {
            if (TrimId(cursor.providerId) != providerId) continue;
            newestDay = std::max(newestDay, cursor.dayKey);
            if (cursor.dayKey == delta.dayKey) previous = std::max(previous, cursor.cumulativeTotal);
        }
        if (delta.dayKey < newestDay || delta.cumulativeTotal <= previous)
            return Reject(Status::Duplicate, original);
        if (delta.cumulativeTotal - previous != delta.delta)
            return Reject(Status::Invalid, original);

        State next = original;
        StepCursor* matching = nullptr;
        for (StepCursor& cursor : next.stepCursors)
            if (TrimId(cursor.providerId) == providerId && cursor.dayKey == delta.dayKey)
            {
                matching = &cursor;
                break;
            }
        if (matching)
        {
            matching->providerId = providerId;
            matching->cumulativeTotal = delta.cumulativeTotal;
        }
        else next.stepCursors.push_back({providerId, delta.dayKey, delta.cumulativeTotal});

        next.processedStepEvents.push_back(event);
        if (next.processedStepEvents.size() > EventLimit)
            next.processedStepEvents.erase(next.processedStepEvents.begin(),
                next.processedStepEvents.begin() + (next.processedStepEvents.size() - EventLimit));

        const int added = static_cast<int>(std::min<int64_t>(delta.delta, HatchTarget - next.eggProgress));
        next.eggProgress += added;
        if (next.eggProgress == HatchTarget) next.phase = Phase::HatchReady;
        return {Status::Applied, next, added};
    }

    inline Change BeginHatch(const State& original, int selectedArtId)
    {
        if (!Valid(original)) return Reject(Status::Invalid, original);
        if (original.phase != Phase::HatchReady) return Reject(Status::WrongPhase, original);
        if (selectedArtId < 1 || selectedArtId > SpeciesCount)
            return Reject(Status::Invalid, original);
        State next = original;
        next.selectedArtId = selectedArtId;
        next.phase = Phase::Hatching;
        next.checkpoint = Checkpoint::Started;
        next.subject = Subject::Egg;
        return {Status::Applied, next, 0};
    }

    // The platform adapter must load the latest durable snapshot, and TryCommit
    // must atomically compare its revision and persist candidate before returning true.
    // NextArtId must use trusted uniform randomness over [1, SpeciesCount].
    struct ISelectionStore
    {
        virtual ~ISelectionStore() = default;
        virtual bool Load(State& snapshot) = 0;
        virtual bool TryCommit(int64_t expectedRevision, const State& candidate) = 0;
    };

    struct IArtIdSource
    {
        virtual ~IArtIdSource() = default;
        virtual int NextArtId() = 0;
    };

    inline Change BeginHatchDurably(ISelectionStore& store, IArtIdSource& random)
    {
        State durable;
        try
        {
            if (!store.Load(durable)) return Reject(Status::SaveFailed, durable);
        }
        catch (...) { return Reject(Status::SaveFailed, durable); }
        if (!Valid(durable)) return Reject(Status::Invalid, durable);
        if (durable.phase == Phase::Hatching && durable.checkpoint == Checkpoint::Started)
            return Reject(Status::Duplicate, durable);
        if (durable.phase != Phase::HatchReady) return Reject(Status::WrongPhase, durable);
        if (durable.revision == std::numeric_limits<int64_t>::max())
            return Reject(Status::Invalid, durable);

        int chosenId = 0;
        try { chosenId = random.NextArtId(); }
        catch (...) { return Reject(Status::Invalid, durable); }
        const Change candidate = BeginHatch(durable, chosenId);
        if (candidate.status != Status::Applied) return Reject(candidate.status, durable);
        State next = candidate.state;
        next.revision = durable.revision + 1;

        try
        {
            if (store.TryCommit(durable.revision, next)) return {Status::Applied, next, 0};
        }
        catch (...) { /* Unknown commit outcome: reload before returning. */ }

        State after;
        try
        {
            if (!store.Load(after)) return Reject(Status::SaveFailed, durable);
        }
        catch (...) { return Reject(Status::SaveFailed, durable); }
        if (!Valid(after)) return Reject(Status::SaveFailed, durable);
        if (after.revision == next.revision && after.phase == Phase::Hatching &&
            after.checkpoint == Checkpoint::Started && after.subject == Subject::Egg &&
            after.eggProgress == next.eggProgress && after.selectedArtId == next.selectedArtId)
            return {Status::Applied, after, 0};
        if (after.phase == Phase::Hatching && after.checkpoint == Checkpoint::Started)
            return Reject(Status::Duplicate, after);
        return Reject(Status::SaveFailed, after);
    }

    inline Change ShellBurst(const State& original)
    {
        if (!Valid(original)) return Reject(Status::Invalid, original);
        if (original.phase != Phase::Hatching) return Reject(Status::WrongPhase, original);
        if (original.checkpoint == Checkpoint::ShellBurst) return Reject(Status::Duplicate, original);
        State next = original;
        next.checkpoint = Checkpoint::ShellBurst;
        return {Status::Applied, next, 0};
    }

    inline Change Reveal(const State& original)
    {
        if (!Valid(original)) return Reject(Status::Invalid, original);
        if (original.phase != Phase::Hatching || original.checkpoint != Checkpoint::ShellBurst)
            return Reject(Status::WrongPhase, original);
        State next = original;
        next.phase = Phase::Reveal;
        next.checkpoint = Checkpoint::Revealed;
        next.subject = Subject::Dinosaur;
        return {Status::Applied, next, 0};
    }

    inline Change EnterHome(const State& original)
    {
        if (!Valid(original)) return Reject(Status::Invalid, original);
        if (original.phase != Phase::Reveal) return Reject(Status::WrongPhase, original);
        State next = original;
        next.phase = Phase::Home;
        return {Status::Applied, next, 0};
    }
}
