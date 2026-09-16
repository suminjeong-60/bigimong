using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Bigimong.AR
{
    /// <summary>Attach only to the viewport raycast target, never its control-containing parent.</summary>
    public sealed class HomeStageOrbitInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        public const float MaxInertiaSeconds = 0.35f;
        private const float DegreesPerDp = .45f;
        private const float Damping = 8f;
        private const float FrontSeconds = .25f;
        private readonly HomeGestureClassifier gesture = new HomeGestureClassifier();
        private Transform pivot;
        private int? owner;
        private Vector2 lastPosition;
        private float lastTime, pointerDpi, velocity, inertiaRemaining, authoredHeading;
        private float resetElapsed, resetStart;
        private bool resetting, inputLocked;
        public float Yaw { get; private set; }
        public event Action Tapped;
        public event Action Resetting;

        public void Bind(Transform yawPivot, float frontHeading = 0f)
        {
            CancelSequence();
            pivot = yawPivot;
            authoredHeading = frontHeading;
            Yaw = frontHeading;
            ApplyYaw();
        }

        public void SetInputLocked(bool locked)
        {
            inputLocked = locked;
            if (locked) CancelSequence();
        }

        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;

        public void OnPointerDown(PointerEventData data)
        {
            // Exact hit ownership rejects controls layered over the viewport, including child buttons.
            if (inputLocked || owner.HasValue || pivot == null || data.button != PointerEventData.InputButton.Left ||
                data.pointerCurrentRaycast.gameObject != gameObject) return;
            CancelSequence();
            owner = data.pointerId;
            lastPosition = data.position;
            lastTime = Time.unscaledTime;
            pointerDpi = Screen.dpi;
            gesture.Begin(data.position, lastTime, pointerDpi);
        }

        public void OnDrag(PointerEventData data)
        {
            if (inputLocked || owner != data.pointerId) return;
            gesture.Move(data.position);
            var now = Time.unscaledTime;
            if (gesture.IsDrag)
            {
                var delta = (data.position.x - lastPosition.x) / HomeGestureClassifier.PixelsPerDp(pointerDpi) * DegreesPerDp;
                AddYaw(delta);
                velocity = now > lastTime ? delta / (now - lastTime) : 0f;
            }
            lastPosition = data.position;
            lastTime = now;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (inputLocked || owner != data.pointerId) return;
            owner = null;
            var result = gesture.End(data.position, Time.unscaledTime);
            if (result == HomeGesture.TAP) { velocity = 0f; Tapped?.Invoke(); }
            else
            {
                if (Time.unscaledTime - lastTime > .08f) velocity = 0f;
                inertiaRemaining = MaxInertiaSeconds;
            }
        }

        public void AddYaw(float degrees)
        {
            Yaw += degrees;
            ApplyYaw();
        }

        public void ResetFront()
        {
            if (inputLocked) return;
            Resetting?.Invoke();
            CancelSequence();
            resetStart = Yaw;
            resetElapsed = 0f;
            resetting = true;
        }

        public void Advance(float deltaSeconds)
        {
            if (inputLocked || pivot == null || deltaSeconds <= 0f) return;
            if (resetting)
            {
                resetElapsed += deltaSeconds;
                var t = Mathf.Clamp01(resetElapsed / FrontSeconds);
                // Shortest visual route, then exact stored heading (including its unwrapped value).
                Yaw = t == 1f ? authoredHeading : resetStart + Mathf.DeltaAngle(resetStart, authoredHeading) * (t * t * (3f - 2f * t));
                resetting = t < 1f;
                ApplyYaw();
            }
            else if (!owner.HasValue && inertiaRemaining > 0f)
            {
                var dt = Mathf.Min(deltaSeconds, inertiaRemaining);
                var decay = Mathf.Exp(-Damping * dt);
                AddYaw(velocity * (1f - decay) / Damping);
                velocity *= decay;
                inertiaRemaining = Mathf.Max(0f, inertiaRemaining - dt);
                if (inertiaRemaining == 0f) velocity = 0f;
            }
        }

        private void ApplyYaw() { if (pivot != null) pivot.localRotation = Quaternion.Euler(0f, Yaw, 0f); }
        private void CancelSequence() { owner = null; velocity = 0f; inertiaRemaining = 0f; resetting = false; }
        private void LateUpdate() => Advance(Time.unscaledDeltaTime);
        private void OnDisable() => CancelSequence();
    }
}
