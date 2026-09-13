using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bigimong.AR
{
    public sealed class ArBattleDirector : MonoBehaviour
    {
        [SerializeField] private ArBattleArenaController arena;
        [SerializeField] private CharacterPrefabCatalog characterCatalog;
        [SerializeField] private SharedAnchorCoordinator sharedAnchor;
        [SerializeField] private ArBattleHud hud;
        [SerializeField] private SummonSequenceDirector summonDirector;
        [SerializeField] private GameObject trackingLostPanel;
        [SerializeField] private GameObject disconnectedPanel;

        private ProceduralCombatVfx combatVfx;
        private BattleStartMessage battle;
        private Transform arenaRoot;
        private ArBattleActor actorA;
        private ArBattleActor actorB;
        private Coroutine roundPlayback;
        private BattleSnapshotMessage pendingSnapshot;
        private int lastPlayedRound;
        private bool summoning;
        private readonly List<BattleRoundMessage> pendingRounds = new List<BattleRoundMessage>();
        public bool IsRoundPlaying => roundPlayback != null || pendingRounds.Count > 0;

        private void OnEnable()
        {
            if (arena != null) arena.ArenaPlaced += OnArenaPlaced;
            if (summonDirector != null) summonDirector.SummoningCompleted += OnSummoningCompleted;
        }

        private void OnDisable()
        {
            CancelPlayback();
            if (arena != null) arena.ArenaPlaced -= OnArenaPlaced;
            if (summonDirector != null) summonDirector.SummoningCompleted -= OnSummoningCompleted;
        }

        public void StartBattle(BattleStartMessage message)
        {
            CancelPlayback();
            summoning = false;
            summonDirector?.CancelAndReset();
            hud?.SetInputLocked(false);
            battle = message;
            lastPlayedRound = Mathf.Max(0, message.round - 1);
            pendingSnapshot = null;
            pendingRounds.Clear();
            sharedAnchor?.Configure(message);
            if (arenaRoot == null) arenaRoot = arena?.ArenaTransform;
            if (arenaRoot != null) SpawnActors();
        }

        public void EndOfflineBattle()
        {
            if (battle == null || battle.mode != "OFFLINE_BETA") return;
            CancelPlayback();
            summoning = false;
            summonDirector?.CancelAndReset();
            battle = null;
            actorA = null;
            actorB = null;
            pendingSnapshot = null;
            pendingRounds.Clear();
            arenaRoot = null;
            hud?.SetInputLocked(true);
        }

        public void ApplySnapshot(BattleSnapshotMessage snapshot)
        {
            if (snapshot == null) return;
            if (!string.IsNullOrWhiteSpace(snapshot.cloudAnchorId))
                sharedAnchor?.ApplyCloudAnchor(snapshot.cloudAnchorId);
            if (actorA == null || actorB == null) return;
            if (summoning)
            {
                pendingSnapshot = snapshot;
                return;
            }
            if (roundPlayback != null)
            {
                pendingSnapshot = snapshot;
                return;
            }
            ReconcileSnapshot(snapshot);
        }

        private void ReconcileSnapshot(BattleSnapshotMessage snapshot)
        {
            if (snapshot == null || actorA == null || actorB == null) return;
            var actorAWasAlive = actorA.HitPoints > 0;
            var actorBWasAlive = actorB.HitPoints > 0;
            actorA.ApplySnapshotHitPoints(snapshot.hpA);
            actorB.ApplySnapshotHitPoints(snapshot.hpB);
            hud?.SetBattleState(snapshot.round, snapshot.hpA, snapshot.hpB, snapshot.status, snapshot.winner);
            if (snapshot.hpA <= 0 && actorAWasAlive) actorA.PlayHit(0);
            if (snapshot.hpB <= 0 && actorBWasAlive) actorB.PlayHit(0);
            if (snapshot.status == "FINISHED")
            {
                if (snapshot.winner == "A") actorA.PlayVictory();
                if (snapshot.winner == "B") actorB.PlayVictory();
            }
        }

        public void ApplyRound(BattleRoundMessage round)
        {
            if (round == null || actorA == null || actorB == null || round.round <= lastPlayedRound) return;
            if (summoning || roundPlayback != null)
            {
                QueuePendingRound(round);
                return;
            }
            StartRoundPlayback(round);
        }

        private void StartRoundPlayback(BattleRoundMessage round)
        {
            lastPlayedRound = round.round;
            roundPlayback = StartCoroutine(PlayRound(round));
        }

        private void QueuePendingRound(BattleRoundMessage round)
        {
            for (var index = 0; index < pendingRounds.Count; index++)
            {
                if (pendingRounds[index].round == round.round) return;
                if (pendingRounds[index].round > round.round)
                {
                    pendingRounds.Insert(index, round);
                    return;
                }
            }
            pendingRounds.Add(round);
        }

        public void BeginSummoning(AvatarProfile profile)
        {
            var playerActor = battle != null && battle.youAre == "B" ? actorB : actorA;
            if (summonDirector == null || arenaRoot == null || playerActor == null) return;
            summoning = true;
            hud?.SetInputLocked(true);
            summonDirector.Configure(arenaRoot, hud);
            summonDirector.SummoningCompleted -= OnSummoningCompleted;
            summonDirector.SummoningCompleted += OnSummoningCompleted;
            summonDirector.Begin(profile, playerActor);
        }

        private void OnSummoningCompleted()
        {
            summoning = false;
            hud?.SetInputLocked(false);
            TryStartPendingPlaybackOrReconcile();
        }

        private IEnumerator PlayRound(BattleRoundMessage round)
        {
            var attacker = round.attacker == "A" ? actorA : actorB;
            var defender = round.defender == "A" ? actorA : actorB;
            hud?.ShowRoundResult(round);
            attacker.PlayAttack(round.attackDirection);
            if (combatVfx == null) combatVfx = gameObject.AddComponent<ProceduralCombatVfx>();
            combatVfx.PlaySkill(attacker.SkillProfile, attacker.transform, defender.transform, round.attackDirection);
            yield return new WaitForSecondsRealtime(ProceduralCombatVfx.TravelTime(attacker.SkillProfile, attacker.transform.position, defender.transform.position));
            if (round.outcome.Contains("DODGE")) defender.PlayDodge(round.defendDirection);
            else
            {
                var critical = round.outcome.Contains("CRITICAL");
                combatVfx.PlayImpact(attacker.SkillProfile, defender.transform.position + Vector3.up * .25f, critical);
                hud?.PlayImpactPulse(attacker.SkillProfile.cameraImpulse * (critical ? 1.4f : 1f));
                defender.PlayHit(round.defender == "A" ? round.hpA : round.hpB, defender.transform.position - attacker.transform.position);
            }
            yield return new WaitForSecondsRealtime(0.65f);
            roundPlayback = null;
            TryStartPendingPlaybackOrReconcile();
        }

        private void TryStartPendingPlaybackOrReconcile()
        {
            if (summoning || roundPlayback != null) return;
            while (pendingRounds.Count > 0 && pendingRounds[0].round <= lastPlayedRound)
                pendingRounds.RemoveAt(0);
            if (pendingRounds.Count > 0)
            {
                var nextRound = pendingRounds[0];
                pendingRounds.RemoveAt(0);
                StartRoundPlayback(nextRound);
                return;
            }
            if (pendingSnapshot != null)
            {
                var snapshot = pendingSnapshot;
                pendingSnapshot = null;
                ReconcileSnapshot(snapshot);
            }
        }

        public void SetTrackingLost(bool value)
        {
            trackingLostPanel?.SetActive(value);
            if (value) hud?.SetInputLocked(true);
            else if (!summoning && battle != null && battle.mode != "OFFLINE_BETA") hud?.SetInputLocked(false);
            if (!value || !summoning) return;
            summoning = false;
            summonDirector?.CancelAndReset();
            hud?.SetInputLocked(true);
        }
        public void SetDisconnected(bool value) => disconnectedPanel?.SetActive(value);
        public void RequestReanchor()
        {
            summoning = false;
            summonDirector?.CancelAndReset();
            if (sharedAnchor != null) sharedAnchor.RequestReanchor();
            else arena?.RequestReanchor();
        }

        private void OnArenaPlaced(Transform value)
        {
            if (arenaRoot == value && actorA != null && actorB != null) return;
            arenaRoot = value;
            summonDirector?.Configure(value, hud);
            SpawnActors();
        }

        private void CancelPlayback()
        {
            if (roundPlayback != null) StopCoroutine(roundPlayback);
            roundPlayback = null;
            combatVfx?.ReleaseAll();
        }

        private void SpawnActors()
        {
            CancelPlayback();
            if (battle?.playerA == null || battle.playerB == null) return;
            if (actorA != null) Destroy(actorA.gameObject);
            if (actorB != null) Destroy(actorB.gameObject);

            var maxHeight = Mathf.Max(
                EvolutionScale.TargetHeightMeters(battle.playerA.stage),
                EvolutionScale.TargetHeightMeters(battle.playerB.stage));
            arena?.ConfigureBattleSize(maxHeight);
            var halfDistance = Mathf.Clamp(maxHeight * 0.62f, 0.45f, 1.55f);
            actorA = Spawn("A", battle.playerA, new Vector3(-halfDistance, 0f, 0f));
            actorB = Spawn("B", battle.playerB, new Vector3(halfDistance, 0f, 0f));
            hud?.SetBattleState(battle.round, battle.playerA.hp, battle.playerB.hp, "ACTIVE", string.Empty);
            if (actorA != null && actorB != null)
            {
                actorA.Face(actorB.transform.position);
                actorB.Face(actorA.transform.position);
            }
        }

        private ArBattleActor Spawn(string side, BattlePlayerMessage player, Vector3 localPosition)
        {
            var prefab = characterCatalog != null ? characterCatalog.Resolve(player.artId, player.stage) : null;
            var instance = prefab != null
                ? Instantiate(prefab, arenaRoot)
                : ProceduralDragonFactory.Create(player.artId, player.stage);
            instance.transform.SetParent(arenaRoot, false);
            instance.name = $"Bigimong_{side}_{player.artId:00}_{player.stage}";
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.identity;
            var actor = instance.GetComponent<ArBattleActor>() ?? instance.AddComponent<ArBattleActor>();
            actor.Initialize(side, player);
            return actor;
        }
    }
}
