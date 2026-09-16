using UnityEngine;

namespace Bigimong.AR
{
    public enum HomeGesture { TAP, DRAG }

    /// <summary>Total path length, not displacement: crossing the threshold is irreversible.</summary>
    public sealed class HomeGestureClassifier
    {
        public const float TapSeconds = 0.22f;
        public const float DragDp = 18f;
        private Vector2 last;
        private float started, traveled, dpi;
        public bool IsDrag { get; private set; }

        public static float PixelsPerDp(float screenDpi) => screenDpi > 0f && !float.IsInfinity(screenDpi) ? screenDpi / 160f : 1f;

        public static HomeGesture Classify(float elapsedSeconds, float traveledPixels, float screenDpi) =>
            elapsedSeconds >= 0f && elapsedSeconds <= TapSeconds && traveledPixels < DragDp * PixelsPerDp(screenDpi)
                ? HomeGesture.TAP : HomeGesture.DRAG;

        public void Begin(Vector2 position, float time, float screenDpi)
        {
            last = position;
            started = time;
            dpi = screenDpi;
            traveled = 0f;
            IsDrag = false;
        }

        public void Move(Vector2 position)
        {
            traveled += Vector2.Distance(last, position);
            last = position;
            IsDrag |= traveled >= DragDp * PixelsPerDp(dpi);
        }

        public HomeGesture End(Vector2 position, float time)
        {
            Move(position);
            return IsDrag ? HomeGesture.DRAG : Classify(time - started, traveled, dpi);
        }
    }
}
