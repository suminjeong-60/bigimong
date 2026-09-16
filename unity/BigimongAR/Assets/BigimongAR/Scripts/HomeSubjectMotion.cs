using UnityEngine;

namespace Bigimong.AR
{
    public enum HomeReaction { AcceptedCare, CooldownWobble, DinosaurIdle, AvatarWave }

    /// <summary>Additive presentation-only motion; the bound pivot must be above the authored model/rig.</summary>
    public sealed class HomeSubjectMotion : MonoBehaviour
    {
        private Transform pivot;
        private Animator animator;
        private Vector3 position, scale;
        private Quaternion rotation;
        private HomeReaction reaction;
        private float elapsed, duration;
        private bool playing, triggered;
        private static readonly int IdleReact = Animator.StringToHash("IdleReact");

        public void Bind(Transform presentationPivot, Animator modelAnimator)
        {
            CancelAndRestore();
            pivot = presentationPivot;
            animator = modelAnimator;
            if (pivot == null) return;
            position = pivot.localPosition;
            rotation = pivot.localRotation;
            scale = pivot.localScale;
        }

        public void Play(HomeReaction kind)
        {
            CancelAndRestore();
            if (pivot == null) return;
            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (var parameter in animator.parameters)
                    if (parameter.nameHash == IdleReact && parameter.type == AnimatorControllerParameterType.Trigger)
                    {
                        animator.SetTrigger(IdleReact);
                        triggered = true;
                        return;
                    }
            reaction = kind;
            duration = kind == HomeReaction.AcceptedCare ? .65f : kind == HomeReaction.CooldownWobble ? .3f : kind == HomeReaction.DinosaurIdle ? .85f : .75f;
            elapsed = 0f;
            playing = true;
        }

        public void Advance(float deltaSeconds)
        {
            if (!playing || pivot == null || deltaSeconds <= 0f) return;
            elapsed += deltaSeconds;
            if (elapsed >= duration) { CancelAndRestore(); return; }
            var t = elapsed / duration;
            var envelope = Mathf.Sin(Mathf.PI * t);
            float lift = 0f, roll = 0f;
            switch (reaction)
            {
                case HomeReaction.AcceptedCare: lift = .06f * envelope; roll = 8f * Mathf.Sin(4f * Mathf.PI * t) * envelope; break;
                case HomeReaction.CooldownWobble: roll = 4f * Mathf.Sin(3f * Mathf.PI * t) * envelope; break;
                case HomeReaction.DinosaurIdle: lift = .025f * envelope; roll = 2f * Mathf.Sin(2f * Mathf.PI * t) * envelope; break;
                case HomeReaction.AvatarWave: lift = .02f * envelope; roll = 6f * Mathf.Sin(4f * Mathf.PI * t) * envelope; break;
            }
            pivot.localPosition = position + Vector3.up * lift;
            pivot.localRotation = rotation * Quaternion.Euler(0f, 0f, roll);
            // No scale tween: authored body proportions and every bone remain untouched.
        }

        public void CancelAndRestore()
        {
            playing = false;
            if (triggered && animator != null && animator.runtimeAnimatorController != null) animator.ResetTrigger(IdleReact);
            triggered = false;
            if (pivot == null) return;
            pivot.localPosition = position;
            pivot.localRotation = rotation;
            pivot.localScale = scale;
        }

        private void LateUpdate() => Advance(Time.unscaledDeltaTime);
        private void OnDisable() => CancelAndRestore();
        private void OnDestroy() => CancelAndRestore();
    }
}
