using System.Collections;
using UnityEngine;

namespace Bigimong.AR
{
    public sealed class ArBattleDirector : MonoBehaviour
    {
        [SerializeField] private ArBattleArenaController arena;
        [SerializeField] private CharacterPrefabCatalog characterCatalog;
        [SerializeField] private SharedAnchorCoordinator sharedAnchor;
        [SerializeField] private ArBattleHud hud;
        [SerializeField] private GameObject trackingLostPanel;
        [SerializeField] private GameObject disconnectedPanel;

        private BattleStartMessage battle;
        private Transform arenaRoot;
        private ArBattleActor actorA;
        private ArBattleActor actorB;
        private Coroutine roundPlayback;
        private BattleSnapshotMessage pendingSnapshot;
        private int lastPlayedRound;

        private void OnEnable()
        {
            if (arena != null) arena.ArenaPlaced += OnArenaPlaced;
        }

        private void OnDisable()
        {
            if (arena != null) arena.ArenaPlaced -= OnArenaPlaced;
        }

        public void StartBattle(BattleStartMessage message)
        {
            battle = message;
            lastPlayedRound = Mathf.Max(0, message.round - 1);
            pendingSnapshot = null;
            sharedAnchor?.Configure(message);
            if (arenaRoot != null) SpawnActors();
        }

        public void ApplySnapshot(BattleSnapshotMessage snapshot)
        {
            if (snapshot == null) return;
            if (!string.IsNullOrWhiteSpace(snapshot.cloudAnchorId))
                sharedAnchor?.ApplyCloudAnchor(snapshot.cloudAnchorId);
            if (actorA == null || actorB == null) return;
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
            lastPlayedRound = round.round;
            if (roundPlayback != null) StopCoroutine(roundPlayback);
            roundPlayback = StartCoroutine(PlayRound(round));
        }

        private IEnumerator PlayRound(BattleRoundMessage round)
        {
            var attacker = round.attacker == "A" ? actorA : actorB;
            var defender = round.defender == "A" ? actorA : actorB;
            hud?.ShowRoundResult(round);
            attacker.PlayAttack(round.attackDirection);
            yield return new WaitForSecondsRealtime(0.18f);
            if (round.outcome.Contains("DODGE")) defender.PlayDodge(round.defendDirection);
            else defender.PlayHit(round.defender == "A" ? round.hpA : round.hpB);
            yield return new WaitForSecondsRealtime(0.65f);
            roundPlayback = null;
            if (pendingSnapshot != null)
            {
                var snapshot = pendingSnapshot;
                pendingSnapshot = null;
                ReconcileSnapshot(snapshot);
            }
        }

        public void SetTrackingLost(bool value) => trackingLostPanel?.SetActive(value);
        public void SetDisconnected(bool value) => disconnectedPanel?.SetActive(value);
        public void RequestReanchor()
        {
            if (sharedAnchor != null) sharedAnchor.RequestReanchor();
            else arena?.RequestReanchor();
        }

        private void OnArenaPlaced(Transform value)
        {
            arenaRoot = value;
            SpawnActors();
        }

        private void SpawnActors()
        {
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
