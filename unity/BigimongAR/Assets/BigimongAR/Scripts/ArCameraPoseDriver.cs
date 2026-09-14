using UnityEngine;
using UnityEngine.XR;

namespace Bigimong.AR
{
    // ARCore supplies the camera pose through the XR center-eye device.
    // Invalid/lost samples leave the last pose intact for screen-fixed fallback.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ArCameraPoseDriver : MonoBehaviour
    {
        private InputDevice device;
        private void OnEnable() { Application.onBeforeRender += UpdatePose; }
        private void OnDisable() { Application.onBeforeRender -= UpdatePose; }
        private void Update() { UpdatePose(); }
        private void UpdatePose()
        {
            if (!device.isValid) device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) || !tracked) return;
            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out var position) ||
                !device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation)) return;
            TryApplyPose(transform, true, position, rotation);
        }

        public static bool TryApplyPose(Transform target, bool tracked, Vector3 position, Quaternion rotation)
        {
            if (target == null || !tracked || !Finite(position.x) || !Finite(position.y) || !Finite(position.z) ||
                !Finite(rotation.x) || !Finite(rotation.y) || !Finite(rotation.z) || !Finite(rotation.w)) return false;
            var squared = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
            if (!Finite(squared) || squared < .000001f) return false;
            target.SetLocalPositionAndRotation(position, rotation.normalized);
            return true;
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
