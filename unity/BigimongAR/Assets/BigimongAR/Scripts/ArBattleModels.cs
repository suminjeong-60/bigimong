using System;
using UnityEngine;

namespace Bigimong.AR
{
    [Serializable]
    public sealed class BattleStartMessage
    {
        public int schemaVersion;
        public string battleId;
        public string mode;
        public int stake;
        public int round;
        public long serverNow;
        public long deadlineAt;
        public string youAre;
        public string transport;
        public string cloudAnchorId;
        public BattlePlayerMessage playerA;
        public BattlePlayerMessage playerB;
    }

    [Serializable]
    public sealed class BattlePlayerMessage
    {
        public string userId;
        public int hp;
        public string name;
        public int artId;
        public string stage;
        public int attack;
        public int evasion;
        public int vitality;
    }

    [Serializable]
    public sealed class BattleSnapshotMessage
    {
        public string battleId;
        public string status;
        public int round;
        public string attacker;
        public long serverNow;
        public long deadlineAt;
        public int hpA;
        public int hpB;
        public string winner;
        public string cloudAnchorId;
    }

    [Serializable]
    public sealed class BattleRoundMessage
    {
        public int round;
        public string attacker;
        public string defender;
        public string attackDirection;
        public string defendDirection;
        public bool attackAutomatic;
        public bool defendAutomatic;
        public string outcome;
        public int damage;
        public int hpA;
        public int hpB;
    }

    public static class EvolutionScale
    {
        public static float TargetHeightMeters(string stage)
        {
            return stage switch
            {
                "YOUTH" => 0.9f,
                "ADULT" => 2.1f,
                _ => 0.3f,
            };
        }
    }
}
