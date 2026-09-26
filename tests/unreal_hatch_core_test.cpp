#include "../unreal/Bigimong/Source/Bigimong/Public/BigimongHatchCore.h"

#include <cassert>
#include <cstdint>
#include <iostream>
#include <stdexcept>

using namespace BigimongHatch;

struct MemorySelectionStore : ISelectionStore
{
    State saved;
    bool available = true;
    bool failBeforeCommit = false;
    bool failAfterCommit = false;
    bool throwAfterCommit = false;
    int competingArtId = 0;
    int commitCalls = 0;

    bool Load(State& result) override
    {
        if (!available) return false;
        result = saved;
        return true;
    }

    bool TryCommit(int64_t expectedRevision, const State& candidate) override
    {
        ++commitCalls;
        if (competingArtId != 0)
        {
            saved = candidate;
            saved.selectedArtId = competingArtId;
            return false;
        }
        if (failBeforeCommit || saved.revision != expectedRevision ||
            candidate.revision != expectedRevision + 1) return false;
        saved = candidate;
        if (throwAfterCommit) throw std::runtime_error("unknown commit result");
        return !failAfterCommit;
    }
};

struct FixedArtId : IArtIdSource
{
    int choice;
    int draws = 0;
    explicit FixedArtId(int choice) : choice(choice) {}
    int NextArtId() override { ++draws; return choice; }
};

int main()
{
    constexpr int64_t Start = 638945760000000000LL; // .NET UTC ticks, not Unix ms
    constexpr int64_t TwoHours = 72000000000LL;

    const State initial;
    assert(Valid(initial));
    const auto firstCare = Care(initial, Start);
    assert(firstCare.status == Status::Applied && firstCare.added == 500);
    assert(firstCare.state.eggProgress == 500);
    assert(firstCare.state.nextCareAtUtcTicks == Start + TwoHours);
    assert(initial.eggProgress == 0); // rejected/unsaved candidates cannot mutate durable input
    const auto duplicateCare = Care(firstCare.state, Start + TwoHours - 1);
    assert(duplicateCare.status == Status::Cooldown);
    assert(duplicateCare.state.eggProgress == 500);
    const auto rollback = Care(firstCare.state, Start - 1);
    assert(rollback.status == Status::Cooldown);
    const auto secondCare = Care(firstCare.state, Start + TwoHours);
    assert(secondCare.status == Status::Applied && secondCare.state.eggProgress == 1000);

    const StepDelta firstSteps{"health-connect", "event-1", 20260926, 1000, 1000};
    const auto synced = VerifiedSteps(initial, firstSteps);
    assert(synced.status == Status::Applied && synced.state.eggProgress == 1000);
    assert(VerifiedSteps(synced.state, firstSteps).status == Status::Duplicate);
    assert(VerifiedSteps(synced.state, {"health-connect", "new-id", 20260926, 1000, 1000}).status == Status::Duplicate);
    assert(VerifiedSteps(synced.state, {"health-connect", "event-2", 20260926, 3000, 1000}).status == Status::Invalid);
    assert(VerifiedSteps(synced.state, {"health-connect", "event-3", 20260925, 2000, 2000}).status == Status::Duplicate);
    assert(VerifiedSteps(synced.state, {"", "event-4", 20260926, 2000, 1000}).status == Status::Invalid);
    assert(VerifiedSteps(synced.state, {"  ", "event-4", 20260926, 2000, 1000}).status == Status::Invalid);
    assert(VerifiedSteps(synced.state, {"health-connect", " \t ", 20260926, 2000, 1000}).status == Status::Invalid);
    assert(VerifiedSteps(synced.state, {" health-connect ", " event-1 ", 20260926, 1000, 1000}).status == Status::Duplicate);
    assert(VerifiedSteps(synced.state, {" health-connect ", "event-new", 20260926, 1000, 1000}).status == Status::Duplicate);
    const auto trimmed = VerifiedSteps(initial, {" health-connect ", " event-1 ", 20260926, 1000, 1000});
    assert(trimmed.status == Status::Applied);
    assert(trimmed.state.stepCursors.front().providerId == "health-connect");
    assert(trimmed.state.processedStepEvents.front() == "health-connect:event-1");
    assert(VerifiedSteps(synced.state, {"health-connect", "event-5", 20260926, 2000, -1}).status == Status::Invalid);
    const auto nextDay = VerifiedSteps(synced.state, {"health-connect", "event-6", 20260927, 400, 400});
    assert(nextDay.status == Status::Applied && nextDay.state.eggProgress == 1400);
    assert(nextDay.state.stepCursors.size() == 2);

    State almostReady;
    almostReady.eggProgress = 29999;
    assert(Valid(almostReady));
    assert(BeginHatch(almostReady, 1).status == Status::WrongPhase);
    const auto ready = VerifiedSteps(almostReady, {"health-connect", "last-step", 20260926, 1, 1});
    assert(ready.status == Status::Applied && ready.added == 1);
    assert(ready.state.phase == Phase::HatchReady && ready.state.eggProgress == 30000);
    assert(Care(ready.state, Start).status == Status::WrongPhase);
    assert(VerifiedSteps(ready.state, {"health-connect", "extra", 20260926, 2, 1}).status == Status::WrongPhase);
    assert(BeginHatch(ready.state, 0).status == Status::Invalid);
    assert(BeginHatch(ready.state, 31).status == Status::Invalid);

    const auto started = BeginHatch(ready.state, 17);
    assert(started.status == Status::Applied && started.state.selectedArtId == 17);
    assert(started.state.phase == Phase::Hatching && started.state.checkpoint == Checkpoint::Started);
    assert(ready.state.selectedArtId == 0);
    assert(BeginHatch(started.state, 22).status == Status::WrongPhase);
    assert(Reveal(started.state).status == Status::WrongPhase);
    const auto burst = ShellBurst(started.state);
    assert(burst.status == Status::Applied);
    assert(ShellBurst(burst.state).status == Status::Duplicate);
    const auto revealed = Reveal(burst.state);
    assert(revealed.status == Status::Applied && revealed.state.selectedArtId == 17);
    assert(revealed.state.subject == Subject::Dinosaur);
    const auto home = EnterHome(revealed.state);
    assert(home.status == Status::Applied && home.state.selectedArtId == 17);
    assert(Valid(home.state));

    MemorySelectionStore store;
    store.saved = ready.state;
    FixedArtId random(17);
    const auto selected = BeginHatchDurably(store, random);
    assert(selected.status == Status::Applied && selected.state.selectedArtId == 17);
    assert(selected.state.revision == ready.state.revision + 1);
    assert(store.saved.selectedArtId == 17 && random.draws == 1);
    FixedArtId otherRandom(22);
    const auto repeated = BeginHatchDurably(store, otherRandom);
    assert(repeated.status == Status::Duplicate && repeated.state.selectedArtId == 17);
    assert(otherRandom.draws == 0 && store.commitCalls == 1);

    MemorySelectionStore failBefore;
    failBefore.saved = ready.state;
    failBefore.failBeforeCommit = true;
    FixedArtId beforeChoice(20);
    const auto before = BeginHatchDurably(failBefore, beforeChoice);
    assert(before.status == Status::SaveFailed && before.state.selectedArtId == 0);
    assert(failBefore.saved.selectedArtId == 0);

    MemorySelectionStore failAfter;
    failAfter.saved = ready.state;
    failAfter.failAfterCommit = true;
    FixedArtId afterChoice(21);
    const auto recovered = BeginHatchDurably(failAfter, afterChoice);
    assert(recovered.status == Status::Applied && recovered.state.selectedArtId == 21);
    assert(failAfter.saved.selectedArtId == 21 && failAfter.commitCalls == 1);

    MemorySelectionStore threwAfter;
    threwAfter.saved = ready.state;
    threwAfter.throwAfterCommit = true;
    FixedArtId thrownChoice(23);
    assert(BeginHatchDurably(threwAfter, thrownChoice).status == Status::Applied);
    assert(threwAfter.saved.selectedArtId == 23);

    MemorySelectionStore competing;
    competing.saved = ready.state;
    competing.competingArtId = 25;
    FixedArtId loserChoice(24);
    const auto lostRace = BeginHatchDurably(competing, loserChoice);
    assert(lostRace.status == Status::Duplicate && lostRace.state.selectedArtId == 25);
    assert(competing.saved.selectedArtId == 25);

    MemorySelectionStore unavailable;
    unavailable.saved = ready.state;
    unavailable.available = false;
    FixedArtId noDraw(2);
    assert(BeginHatchDurably(unavailable, noDraw).status == Status::SaveFailed);
    assert(noDraw.draws == 0);

    MemorySelectionStore invalidChoice;
    invalidChoice.saved = ready.state;
    FixedArtId outOfRange(31);
    assert(BeginHatchDurably(invalidChoice, outOfRange).status == Status::Invalid);
    assert(invalidChoice.commitCalls == 0);

    State forged = started.state;
    forged.eggProgress = 29999;
    assert(!Valid(forged));
    forged = ready.state;
    forged.selectedArtId = 17;
    assert(!Valid(forged));

    std::cout << "Unreal portable hatch core: passed\n";
}
