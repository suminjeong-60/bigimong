using System;
using System.IO;
using UnityEngine;

namespace Bigimong.AR
{
    /// <summary>Local, offline practice progress. No server currency or online inventory is involved.</summary>
    [Serializable]
    public sealed class OfflineReferenceProgress
    {
        public const int HatchTarget = 30000;
        public int schemaVersion = 1;
        public int eggExperience;
        public bool hatched;
        public int dragonExperience;
        public int coins;
        public int snacks;
        public int selectedArtId = 1;
        public int dayKey;
        public int playsToday;
        public bool giftClaimedToday;
        public bool questClaimedToday;
        public bool soundEnabled = true;
        public long lastPlayTicks;

        public int Level => Mathf.Clamp(1 + dragonExperience / 1000, 1, 30);
        public string GrowthStage => Level >= 20 ? "성장기" : Level >= 10 ? "청소년기" : "아동기";
        public bool CanHatch => !hatched && eggExperience >= HatchTarget;

        public void Normalize()
        {
            eggExperience = Mathf.Clamp(eggExperience, 0, HatchTarget);
            dragonExperience = Mathf.Clamp(dragonExperience, 0, 29000);
            coins = Mathf.Clamp(coins, 0, 1000000);
            snacks = Mathf.Clamp(snacks, 0, 1000);
            selectedArtId = Mathf.Clamp(selectedArtId, 1, 30);
            playsToday = Mathf.Clamp(playsToday, 0, 100);
        }

        public void StartDay(DateTime utcNow)
        {
            var today = utcNow.Year * 1000 + utcNow.DayOfYear;
            if (dayKey == today) return;
            dayKey = today;
            playsToday = 0;
            giftClaimedToday = false;
            questClaimedToday = false;
        }

        public bool PolishEgg()
        {
            if (hatched || CanHatch) return false;
            eggExperience = Mathf.Min(HatchTarget, eggExperience + 500);
            return true;
        }

        public bool Hatch()
        {
            if (!CanHatch) return false;
            hatched = true;
            return true;
        }

        public bool Play(DateTime utcNow)
        {
            StartDay(utcNow);
            if (lastPlayTicks > 0 && utcNow.Ticks - lastPlayTicks < TimeSpan.FromSeconds(10).Ticks) return false;
            if (playsToday >= 100) return false;
            lastPlayTicks = utcNow.Ticks;
            playsToday++;
            if (hatched)
            {
                dragonExperience = Mathf.Min(29000, dragonExperience + 200);
                coins = Mathf.Min(1000000, coins + 50);
            }
            else eggExperience = Mathf.Min(HatchTarget, eggExperience + 500);
            return true;
        }

        public bool ClaimGift(DateTime utcNow)
        {
            StartDay(utcNow);
            if (giftClaimedToday) return false;
            giftClaimedToday = true;
            coins = Mathf.Min(1000000, coins + 500);
            if (!hatched) eggExperience = Mathf.Min(HatchTarget, eggExperience + 500);
            return true;
        }

        public bool ClaimQuest(DateTime utcNow)
        {
            StartDay(utcNow);
            if (questClaimedToday || playsToday < 3) return false;
            questClaimedToday = true;
            coins = Mathf.Min(1000000, coins + 300);
            return true;
        }

        public bool BuySnack()
        {
            if (coins < 300 || snacks >= 1000) return false;
            coins -= 300;
            snacks++;
            return true;
        }

        public bool Feed()
        {
            if (!hatched || snacks == 0 || dragonExperience >= 29000) return false;
            snacks--;
            dragonExperience = Mathf.Min(29000, dragonExperience + 500);
            return true;
        }
    }

    public static class OfflineReferenceProgressStore
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "bigimong-offline-progress-v1.json");

        public static OfflineReferenceProgress Load()
        {
            try
            {
                var saved = JsonUtility.FromJson<OfflineReferenceProgress>(File.ReadAllText(SavePath));
                if (saved != null && saved.schemaVersion == 1)
                {
                    saved.Normalize();
                    saved.StartDay(DateTime.UtcNow);
                    return saved;
                }
            }
            catch (Exception) { /* First launch or an invalid save uses a fresh local game. */ }
            var fresh = new OfflineReferenceProgress();
            fresh.StartDay(DateTime.UtcNow);
            return fresh;
        }

        public static bool Save(OfflineReferenceProgress state)
        {
            try
            {
                state.Normalize();
                File.WriteAllText(SavePath, JsonUtility.ToJson(state));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Offline game progress could not be saved: " + exception.Message);
                return false;
            }
        }
    }
}
