using UnityEngine;

namespace Bigimong.AR
{
    /// <summary>Moves only a non-interactive crop layered over reference art; hotspots stay fixed.</summary>
    public sealed class ReferenceArtIdleMotion : MonoBehaviour
    {
        [SerializeField] private Vector2 amplitude = new Vector2(1.5f, 3.5f);
        [SerializeField] private float scaleAmplitude = .005f;
        [SerializeField] private float speed = 1.15f;
        [SerializeField] private float phase;
        private RectTransform rect;
        private Vector2 basePosition;

        public void Configure(Vector2 movement, float breathingScale, float cycleSpeed, float phaseOffset)
        {
            amplitude = movement;
            scaleAmplitude = Mathf.Clamp(breathingScale, 0f, .012f);
            speed = Mathf.Max(.1f, cycleSpeed);
            phase = phaseOffset;
            CacheBasePosition();
        }

        private void Awake() => CacheBasePosition();
        private void OnEnable() => CacheBasePosition();

        private void CacheBasePosition()
        {
            rect = transform as RectTransform;
            if (rect != null) basePosition = rect.anchoredPosition;
        }

        private void Update()
        {
            if (rect == null) return;
            var wave = Time.unscaledTime * speed + phase;
            rect.anchoredPosition = basePosition + new Vector2(Mathf.Sin(wave) * amplitude.x,
                Mathf.Sin(wave * .78f) * amplitude.y);
            rect.localScale = Vector3.one * (1f + Mathf.Sin(wave * .91f) * scaleAmplitude);
        }

        private void OnDisable()
        {
            if (rect == null) return;
            rect.anchoredPosition = basePosition;
            rect.localScale = Vector3.one;
        }
    }
}
