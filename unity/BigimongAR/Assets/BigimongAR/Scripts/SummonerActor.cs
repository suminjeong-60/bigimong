using System.Collections;
using UnityEngine;

namespace Bigimong.AR
{
    public sealed class SummonerActor : MonoBehaviour
    {
        private Transform arenaRoot;
        private Transform rightUpperArm;
        private Transform rightLowerArm;
        private Transform rightHand;
        private Transform wornMedallion;
        private Quaternion upperHome;
        private Quaternion lowerHome;
        private Quaternion handHome;

        public Transform ThrownMedallion { get; private set; }

        public void Initialize(Transform value)
        {
            arenaRoot = value;
            rightUpperArm = transform.Find("ArmUpperRight");
            rightLowerArm = transform.Find("ArmLowerRight");
            rightHand = transform.Find("HandRight");
            wornMedallion = transform.Find("SummoningMedallion");
            if (rightUpperArm != null) upperHome = rightUpperArm.localRotation;
            if (rightLowerArm != null) lowerHome = rightLowerArm.localRotation;
            if (rightHand != null) handHome = rightHand.localRotation;
        }

        public Transform PrepareThrownMedallion()
        {
            if (ThrownMedallion != null) return ThrownMedallion;
            if (wornMedallion == null || arenaRoot == null) return null;
            ThrownMedallion = Instantiate(wornMedallion.gameObject, wornMedallion.position, wornMedallion.rotation, arenaRoot).transform;
            ThrownMedallion.name = "Thrown Summoning Medallion";
            wornMedallion.gameObject.SetActive(false);
            return ThrownMedallion;
        }

        public IEnumerator ThrowMedallion(Transform target)
        {
            yield return PoseArm(0.5f, new Vector3(-52f, 10f, -18f), new Vector3(-78f, 0, 12f));
            yield return PoseArm(0.7f, new Vector3(38f, -16f, 24f), new Vector3(58f, 0, -18f));
            var medallion = PrepareThrownMedallion();
            if (medallion != null && target != null)
            {
                var start = medallion.position;
                const float duration = 0.3f;
                const float arcHeight = 0.35f;
                for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
                {
                    var t = Mathf.Clamp01(elapsed / duration);
                    medallion.position = Vector3.Lerp(start, target.position, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * arcHeight;
                    medallion.Rotate(Vector3.up, Time.deltaTime * 900f, Space.World);
                    yield return null;
                }
                medallion.position = target.position + Vector3.up * 0.01f;
                medallion.rotation = Quaternion.Euler(0, 0, 0);
            }
            RestoreArm();
        }

        public IEnumerator StepBack(float seconds)
        {
            var start = transform.localPosition;
            var target = start + Vector3.back * 0.32f;
            for (var elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                transform.localPosition = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }
            transform.localPosition = target;
        }

        public void ResetPose()
        {
            RestoreArm();
            if (ThrownMedallion != null)
            {
                if (Application.isPlaying) Destroy(ThrownMedallion.gameObject);
                else DestroyImmediate(ThrownMedallion.gameObject);
            }
            ThrownMedallion = null;
            if (wornMedallion != null) wornMedallion.gameObject.SetActive(true);
        }

        private IEnumerator PoseArm(float seconds, Vector3 upperEuler, Vector3 lowerEuler)
        {
            var upperStart = rightUpperArm != null ? rightUpperArm.localRotation : Quaternion.identity;
            var lowerStart = rightLowerArm != null ? rightLowerArm.localRotation : Quaternion.identity;
            for (var elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                var t = Mathf.SmoothStep(0, 1, elapsed / seconds);
                if (rightUpperArm != null) rightUpperArm.localRotation = Quaternion.Slerp(upperStart, upperHome * Quaternion.Euler(upperEuler), t);
                if (rightLowerArm != null) rightLowerArm.localRotation = Quaternion.Slerp(lowerStart, lowerHome * Quaternion.Euler(lowerEuler), t);
                if (rightHand != null) rightHand.localRotation = Quaternion.Slerp(handHome, handHome * Quaternion.Euler(0, 0, -35f), t);
                yield return null;
            }
        }

        private void RestoreArm()
        {
            if (rightUpperArm != null) rightUpperArm.localRotation = upperHome;
            if (rightLowerArm != null) rightLowerArm.localRotation = lowerHome;
            if (rightHand != null) rightHand.localRotation = handHome;
        }
    }
}
