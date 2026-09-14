using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bigimong.AR
{
    /// <summary>Shared tactile feedback for every runtime-generated Bigimong button.</summary>
    public sealed class ButtonPressMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, ICancelHandler, ISubmitHandler
    {
        private const float PressedScale = .92f;
        private const float ReboundScale = 1.04f;
        private Coroutine animation;
        private bool pointerDown;
        private Graphic targetGraphic;
        private Color restingColor;

        private void Awake() => CacheGraphic();

        private void OnEnable() => CacheGraphic();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanAnimate()) return;
            pointerDown = true;
            ShowPressedTint();
            StartMotion(ScaleTo(PressedScale, .06f));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pointerDown) return;
            pointerDown = false;
            StartMotion(Rebound());
        }

        public void OnPointerExit(PointerEventData eventData) => Release();
        public void OnCancel(BaseEventData eventData) => Release();

        public void OnSubmit(BaseEventData eventData)
        {
            if (CanAnimate()) StartMotion(SubmitPulse());
        }

        private void Release()
        {
            if (!pointerDown) return;
            pointerDown = false;
            StartMotion(Rebound());
        }

        private bool CanAnimate()
        {
            var button = GetComponent<Button>();
            return isActiveAndEnabled && (button == null || button.IsInteractable());
        }

        private void StartMotion(IEnumerator routine)
        {
            if (animation != null) StopCoroutine(animation);
            animation = StartCoroutine(routine);
        }

        private IEnumerator SubmitPulse()
        {
            yield return ScaleTo(PressedScale, .06f);
            yield return Rebound();
        }

        private IEnumerator Rebound()
        {
            yield return ScaleTo(ReboundScale, .08f);
            yield return ScaleTo(1f, .08f);
            transform.localScale = Vector3.one;
            RestoreGraphic();
            animation = null;
        }

        private void CacheGraphic()
        {
            var button = GetComponent<Button>();
            targetGraphic = button != null ? button.targetGraphic : GetComponent<Graphic>();
            if (targetGraphic != null) restingColor = targetGraphic.color;
        }

        private void ShowPressedTint()
        {
            if (targetGraphic == null) return;
            targetGraphic.color = restingColor.a < .01f
                ? new Color(1f, .86f, .28f, .16f)
                : Color.Lerp(restingColor, Color.white, .24f);
        }

        private void RestoreGraphic()
        {
            if (targetGraphic != null) targetGraphic.color = restingColor;
        }

        private IEnumerator ScaleTo(float target, float seconds)
        {
            var start = transform.localScale.x;
            for (var elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                var eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds));
                transform.localScale = Vector3.one * Mathf.Lerp(start, target, eased);
                yield return null;
            }
            transform.localScale = Vector3.one * target;
        }

        private void OnDisable()
        {
            if (animation != null) StopCoroutine(animation);
            animation = null;
            pointerDown = false;
            transform.localScale = Vector3.one;
            RestoreGraphic();
        }
    }
}
