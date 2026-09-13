using UnityEngine;

namespace Bigimong.AR
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class DeviceSafeArea : MonoBehaviour
    {
        private Rect previous;
        private RectTransform target;

        private void Awake() => target = GetComponent<RectTransform>();

        private void Update()
        {
            if (previous == Screen.safeArea || Screen.width <= 0 || Screen.height <= 0) return;
            previous = Screen.safeArea;
            target.anchorMin = new Vector2(previous.xMin / Screen.width, previous.yMin / Screen.height);
            target.anchorMax = new Vector2(previous.xMax / Screen.width, previous.yMax / Screen.height);
            target.offsetMin = target.offsetMax = Vector2.zero;
        }
    }
}
