using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Bigimong.AR
{
    public sealed class ArTrackingRecovery : MonoBehaviour
    {
        [SerializeField] private ArBattleDirector director;
        [SerializeField, Min(0.1f)] private float lostDelaySeconds = 1.5f;
        private float lostAt = -1f;

        private void Update()
        {
            var tracking = ARSession.state == ARSessionState.SessionTracking;
            if (tracking)
            {
                lostAt = -1f;
                director?.SetTrackingLost(false);
                return;
            }
            if (lostAt < 0f) lostAt = Time.unscaledTime;
            if (Time.unscaledTime - lostAt >= lostDelaySeconds) director?.SetTrackingLost(true);
        }

        public void Reanchor() => director?.RequestReanchor();
    }
}
