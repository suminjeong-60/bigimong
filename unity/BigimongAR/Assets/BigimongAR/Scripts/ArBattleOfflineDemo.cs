using System;
using System.Collections;
using UnityEngine;

namespace Bigimong.AR
{
    public sealed class ArBattleOfflineDemo : MonoBehaviour
    {
        private const float TurnSeconds = 10f;
        private static readonly string[] Directions = { "LEFT", "CENTER", "RIGHT" };

        [SerializeField] private ArBattleArenaController arena;
        [SerializeField] private ArBattleNativeBridge bridge;
        [SerializeField] private ArBattleHud hud;
        [SerializeField] private ArBattleDirector director;

        private int hpA;
        private int hpB;
        private int round;
        private string attacker;
        private float turnDeadlineRealtime;
        private bool battleStarted;
        private bool resolving;
        private bool trackingPaused;
        private float pausedRemaining;
        private int selectedArtId = 1;
        private const int opponentArtId = 1;

        public bool IsActive { get; private set; }
        public bool IsResolving => resolving;
        public event Action BattlePrepared;
        public event Action<string> BattleFinished;

        private void OnEnable()
        {
            if (arena != null) arena.ArenaPlaced += OnArenaPlaced;
        }

        private void OnDisable()
        {
            if (arena != null) arena.ArenaPlaced -= OnArenaPlaced;
        }

        private void Update()
        {
            if (!IsActive || !battleStarted || resolving || trackingPaused || Time.realtimeSinceStartup < turnDeadlineRealtime) return;
            StartResolution(RandomDirection(), true);
        }

        public void ConfigureSelection(int artId)
        {
            selectedArtId = Mathf.Clamp(artId, 1, 30);
        }

        public void BeginEncounter()
        {
            StopDemo();
            arena?.RequestReanchor();
            IsActive = true;
            hud?.ShowPlacementPrompt();
        }

        public void RestartDemo()
        {
            if (!IsActive) return;
            BeginEncounter();
        }

        public void StopDemo()
        {
            StopAllCoroutines();
            IsActive = false;
            battleStarted = false;
            resolving = false;
            trackingPaused = false;
        }

        public void SetTrackingPaused(bool paused)
        {
            if (!IsActive || !battleStarted || trackingPaused == paused) return;
            trackingPaused = paused;
            if (paused)
            {
                pausedRemaining = Mathf.Max(0, turnDeadlineRealtime - Time.realtimeSinceStartup);
                return;
            }
            turnDeadlineRealtime = Time.realtimeSinceStartup + pausedRemaining;
            if (resolving) return;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            hud?.BeginTurn(now + (long)(pausedRemaining * 1000f), now);
        }

        public void EnableTurns()
        {
            if (!IsActive || battleStarted) return;
            battleStarted = true;
            BeginTurn();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            hud?.BeginTurn(now + (long)(TurnSeconds * 1000f), now);
        }

        public void SubmitChoice(string direction)
        {
            StartResolution(direction, false);
        }

        private void StartResolution(string direction, bool automatic)
        {
            if (!IsActive || !battleStarted || resolving || trackingPaused || !IsDirection(direction)) return;
            resolving = true;
            StartCoroutine(ResolveRound(direction, automatic));
        }

        private IEnumerator ResolveRound(string direction, bool automatic)
        {
            var playerIsAttacker = attacker == "A";
            var attackDirection = playerIsAttacker ? direction : RandomDirection();
            var defendDirection = playerIsAttacker ? RandomDirection() : direction;
            var defender = playerIsAttacker ? "B" : "A";
            var attackStat = playerIsAttacker ? 12 : 9;
            var defenderEvasion = playerIsAttacker ? 8 : 10;
            var outcome = "MANUAL_DODGE";
            var damage = 0;

            if (attackDirection == defendDirection)
            {
                if (UnityEngine.Random.value < defenderEvasion / 100f)
                {
                    outcome = "STAT_DODGE";
                }
                else
                {
                    damage = UnityEngine.Random.value < attackStat / 100f ? 2 : 1;
                    outcome = damage == 2 ? "CRITICAL_HIT" : "HIT";
                    if (defender == "A") hpA = Mathf.Max(0, hpA - damage);
                    else hpB = Mathf.Max(0, hpB - damage);
                }
            }

            bridge?.ApplyRoundMessage(new BattleRoundMessage
            {
                round = round,
                attacker = attacker,
                defender = defender,
                attackDirection = attackDirection,
                defendDirection = defendDirection,
                attackAutomatic = automatic || !playerIsAttacker,
                defendAutomatic = automatic || playerIsAttacker,
                outcome = outcome,
                damage = damage,
                hpA = hpA,
                hpB = hpB,
            });

            yield return new WaitForSecondsRealtime(0.9f);

            var winner = hpA <= 0 ? "B" : hpB <= 0 ? "A" : null;
            if (winner == null)
            {
                round += 1;
                attacker = attacker == "A" ? "B" : "A";
            }
            else
            {
                battleStarted = false;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            bridge?.ApplySnapshotMessage(new BattleSnapshotMessage
            {
                battleId = "offline-beta",
                status = winner == null ? "ACTIVE" : "FINISHED",
                round = round,
                attacker = attacker,
                serverNow = now,
                deadlineAt = winner == null ? now + (long)(TurnSeconds * 1000f) : now,
                hpA = hpA,
                hpB = hpB,
                winner = winner ?? string.Empty,
                cloudAnchorId = string.Empty,
            });

            // The following turn and the result overlay both wait for the actual actor playback.
            while (director != null && director.IsRoundPlaying) yield return null;
            if (!IsActive) yield break;
            if (winner == null)
            {
                BeginTurn();
                var turnNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                hud?.BeginTurn(turnNow + (long)(TurnSeconds * 1000f), turnNow);
            }
            else yield return new WaitForSecondsRealtime(1.2f);

            resolving = false;
            if (winner != null) BattleFinished?.Invoke(winner);
        }

        private void OnArenaPlaced(Transform unused)
        {
            if (IsActive && !battleStarted) BeginBattle();
        }

        private void BeginBattle()
        {
            hpA = 5;
            hpB = 5;
            round = 1;
            attacker = "A";
            battleStarted = false;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            bridge?.StartBattleMessage(new BattleStartMessage
            {
                schemaVersion = 1,
                battleId = "offline-beta",
                mode = "OFFLINE_BETA",
                stake = 0,
                round = round,
                serverNow = now,
                deadlineAt = now + (long)(TurnSeconds * 1000f),
                youAre = "A",
                transport = "OFFLINE",
                cloudAnchorId = string.Empty,
                playerA = Player("나의 비기몽", selectedArtId, "GROWTH", 12, 10),
                playerB = Player("티라노사우루스", opponentArtId, "GROWTH", 9, 8),
            }, false);
            BattlePrepared?.Invoke();
        }

        private void BeginTurn()
        {
            turnDeadlineRealtime = Time.realtimeSinceStartup + TurnSeconds;
            trackingPaused = false;
        }

        private static BattlePlayerMessage Player(string name, int artId, string stage, int attack, int evasion)
        {
            return new BattlePlayerMessage
            {
                userId = $"offline-{artId}",
                hp = 5,
                name = name,
                artId = artId,
                stage = stage,
                attack = attack,
                evasion = evasion,
                vitality = 10,
            };
        }

        private static bool IsDirection(string direction) =>
            direction == "LEFT" || direction == "CENTER" || direction == "RIGHT";

        private static string RandomDirection() => Directions[UnityEngine.Random.Range(0, Directions.Length)];
    }
}
