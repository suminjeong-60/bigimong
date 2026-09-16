using System;

namespace Bigimong.AR
{
    public enum HomeSideActivityAction { Play, DailyGift, QuestReward, BuySnack, Feed, DayRollover, ToggleSound }

    public readonly struct HomeSideActivityResult
    {
        public bool Accepted { get; }
        public HatchHomeSnapshot Snapshot { get; }
        public string Error { get; }
        public HomeSideActivityResult(bool accepted, HatchHomeSnapshot snapshot, string error = "")
        { Accepted = accepted; Snapshot = snapshot?.Clone(); Error = error; }
    }

    /// <summary>Economy-only transactions. Never normalizes or writes hatch identity/progress fields.</summary>
    public sealed class HomeSideActivityService
    {
        private readonly IHatchHomeStore store;
        public HomeSideActivityService(IHatchHomeStore store) => this.store = store ?? throw new ArgumentNullException(nameof(store));

        public HomeSideActivityResult TryApply(HatchHomeSnapshot current, HomeSideActivityAction action, DateTime utcNow)
        {
            if (current == null) return new(false, null, "invalid");
            var candidate = current.Clone();
            utcNow = utcNow.ToUniversalTime();
            var today = utcNow.Year * 1000 + utcNow.DayOfYear;
            var newDay = candidate.dayKey != today;
            if (newDay)
            {
                candidate.dayKey = today;
                candidate.playsToday = 0;
                candidate.giftClaimedToday = candidate.questClaimedToday = false;
            }
            var hatched = current.Phase is HatchHomePhase.REVEAL or HatchHomePhase.HOME;
            switch (action)
            {
                case HomeSideActivityAction.Play:
                    if (candidate.playsToday >= 100 || (candidate.lastPlayTicks > 0 &&
                        utcNow.Ticks - candidate.lastPlayTicks < TimeSpan.FromSeconds(10).Ticks)) return Rejected(current);
                    candidate.lastPlayTicks = utcNow.Ticks;
                    candidate.playsToday++;
                    if (hatched)
                    {
                        candidate.dragonExperience = Math.Min(29000, candidate.dragonExperience + 200);
                        candidate.coins = Math.Min(1000000, candidate.coins + 50);
                    }
                    break;
                case HomeSideActivityAction.DailyGift:
                    if (candidate.giftClaimedToday) return Rejected(current);
                    candidate.giftClaimedToday = true;
                    candidate.coins = Math.Min(1000000, candidate.coins + 500);
                    break;
                case HomeSideActivityAction.QuestReward:
                    if (candidate.questClaimedToday || candidate.playsToday < 3) return Rejected(current);
                    candidate.questClaimedToday = true;
                    candidate.coins = Math.Min(1000000, candidate.coins + 300);
                    break;
                case HomeSideActivityAction.BuySnack:
                    if (candidate.coins < 300 || candidate.snacks >= 1000) return Rejected(current);
                    candidate.coins -= 300; candidate.snacks++;
                    break;
                case HomeSideActivityAction.Feed:
                    if (!hatched || candidate.snacks <= 0 || candidate.dragonExperience >= 29000) return Rejected(current);
                    candidate.snacks--; candidate.dragonExperience = Math.Min(29000, candidate.dragonExperience + 500);
                    break;
                case HomeSideActivityAction.DayRollover:
                    if (!newDay) return Rejected(current);
                    break;
                case HomeSideActivityAction.ToggleSound: candidate.soundEnabled = !candidate.soundEnabled; break;
                default: return Rejected(current);
            }
            try
            {
                // Pass another clone so a failing store cannot mutate the candidate or the live snapshot.
                if (store.TrySave(candidate.Clone())) return new(true, candidate);
            }
            catch (HatchSaveOutcomeUnknownException) { throw; } // Coordinator must reload, never assume rollback.
            catch (Exception) { /* The caller retains the last published snapshot. */ }
            return new(false, current, "save_failed");
        }

        private static HomeSideActivityResult Rejected(HatchHomeSnapshot current) => new(false, current, "not_available");
    }
}
