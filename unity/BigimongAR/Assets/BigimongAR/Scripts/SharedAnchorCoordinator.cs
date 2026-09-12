using UnityEngine;

namespace Bigimong.AR
{
    public sealed class SharedAnchorCoordinator : MonoBehaviour
    {
        [SerializeField] private ArBattleArenaController arena;
        [SerializeField] private SharedAnchorProvider provider;
        [SerializeField] private ArBattleNativeBridge nativeBridge;
        private BattleStartMessage battle;
        private string activeCloudAnchorId;
        private bool hosting;
        private bool resolving;
        private int operationGeneration;

        private void OnEnable()
        {
            if (arena != null) arena.ArenaPlaced += OnArenaPlaced;
        }

        private void OnDisable()
        {
            if (arena != null) arena.ArenaPlaced -= OnArenaPlaced;
        }

        public void Configure(BattleStartMessage message)
        {
            battle = message;
            ApplyCloudAnchor(message.cloudAnchorId);
        }

        public void ApplyCloudAnchor(string cloudAnchorId)
        {
            if (battle == null || battle.mode != "NEARBY" || provider == null) return;
            if (string.IsNullOrWhiteSpace(cloudAnchorId) || cloudAnchorId == activeCloudAnchorId || resolving) return;
            resolving = true;
            var generation = ++operationGeneration;
            provider.Resolve(
                cloudAnchorId,
                pose =>
                {
                    if (generation != operationGeneration) return;
                    resolving = false;
                    activeCloudAnchorId = cloudAnchorId;
                    arena?.PlaceAtPose(pose);
                },
                error =>
                {
                    if (generation != operationGeneration) return;
                    resolving = false;
                    nativeBridge?.ReportAnchorError(error);
                });
        }

        public void RequestReanchor()
        {
            operationGeneration++;
            hosting = false;
            resolving = false;
            activeCloudAnchorId = null;
            arena?.RequestReanchor();
        }

        private void OnArenaPlaced(Transform localArena)
        {
            if (battle == null || battle.mode != "NEARBY" || battle.youAre != "A") return;
            if (provider == null || hosting || !string.IsNullOrWhiteSpace(activeCloudAnchorId)) return;
            hosting = true;
            var generation = ++operationGeneration;
            provider.Host(
                localArena,
                cloudAnchorId =>
                {
                    if (generation != operationGeneration) return;
                    hosting = false;
                    activeCloudAnchorId = cloudAnchorId;
                    nativeBridge?.PublishCloudAnchorId(cloudAnchorId);
                },
                error =>
                {
                    if (generation != operationGeneration) return;
                    hosting = false;
                    nativeBridge?.ReportAnchorError(error);
                });
        }
    }
}
