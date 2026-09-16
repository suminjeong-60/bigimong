using System;
using UnityEngine;

namespace Bigimong.AR
{
    public interface IWalkingProgressProvider
    {
        bool IsAvailable { get; }
        event Action<VerifiedStepDelta> VerifiedStepsReceived;
    }

    public sealed class HatchHomeStepBridge : MonoBehaviour, IWalkingProgressProvider
    {
        private bool hostAvailable;

        public bool IsAvailable => hostAvailable;
        public event Action<VerifiedStepDelta> VerifiedStepsReceived;
        public event Action<string> InvalidPayload;

        public void SetProviderAvailable(string available)
        {
            hostAvailable = string.Equals(available, "1", StringComparison.Ordinal) ||
                string.Equals(available, "true", StringComparison.OrdinalIgnoreCase);
        }

        public void ApplyVerifiedStepsJson(string json)
        {
            VerifiedStepDelta envelope;
            try
            {
                envelope = JsonUtility.FromJson<VerifiedStepDelta>(json);
            }
            catch (ArgumentException)
            {
                InvalidPayload?.Invoke("invalid_step_payload");
                return;
            }

            if (!VerifiedStepDelta.IsValidShape(envelope))
            {
                InvalidPayload?.Invoke("invalid_step_payload");
                return;
            }

            hostAvailable = true;
            VerifiedStepsReceived?.Invoke(envelope);
        }
    }
}
