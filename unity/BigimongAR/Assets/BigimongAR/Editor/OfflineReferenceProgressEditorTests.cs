#if UNITY_EDITOR
using System;

namespace Bigimong.AR.EditorChecks
{
    public static class OfflineReferenceProgressEditorTests
    {
        public static void RunBehaviorChecks()
        {
            var state = new OfflineReferenceProgress();
            var now = new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);
            state.StartDay(now);
            for (var index = 0; index < 59; index++)
                if (!state.PolishEgg()) throw new InvalidOperationException("Egg polishing stopped before hatch target");
            if (state.CanHatch || state.Hatch()) throw new InvalidOperationException("Egg hatched too early");
            state.PolishEgg();
            if (!state.CanHatch || !state.Hatch() || state.PolishEgg())
                throw new InvalidOperationException("Egg hatch did not transition exactly once");
            if (!state.Play(now) || state.Play(now.AddSeconds(9)) || !state.Play(now.AddSeconds(10)))
                throw new InvalidOperationException("Play interval did not protect rewards");
            if (!state.ClaimGift(now) || state.ClaimGift(now))
                throw new InvalidOperationException("Daily gift could be claimed twice");
            if (state.ClaimQuest(now) || !state.Play(now.AddSeconds(20)) || !state.ClaimQuest(now) || state.ClaimQuest(now))
                throw new InvalidOperationException("Quest is not limited to three plays and one reward");
            if (!state.BuySnack() || !state.Feed() || state.snacks != 0 || state.dragonExperience != 1100)
                throw new InvalidOperationException("Buying and feeding did not update saved local progress");
            state.StartDay(now.AddDays(1));
            if (state.playsToday != 0 || !state.ClaimGift(now.AddDays(1)))
                throw new InvalidOperationException("Daily progress did not reset on the next UTC day");
        }
    }
}
#endif
