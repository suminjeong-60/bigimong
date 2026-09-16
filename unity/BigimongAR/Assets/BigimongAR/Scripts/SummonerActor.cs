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
        private Material runtimeMedallionMaterial;

        public Transform ThrownMedallion { get; private set; }
        public bool HasThrowPoseRig => rightUpperArm != null && rightLowerArm != null && rightHand != null;

        public void Initialize(Transform value)
        {
            arenaRoot = value;
            var animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman)
            {
                rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }

            rightUpperArm ??= FindRecursive(transform, "ArmUpperRight");
            rightLowerArm ??= FindRecursive(transform, "ArmLowerRight");
            rightHand ??= FindRecursive(transform, "HandRight");
            wornMedallion = FindRecursive(transform, "SummoningMedallion");
            if (wornMedallion == null) wornMedallion = CreateRuntimeMedallion();
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
                for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    var t = Mathf.Clamp01(elapsed / duration);
                    var travel = Mathf.SmoothStep(0f, 1f, t);
                    medallion.position = Vector3.Lerp(start, target.position, travel) + Vector3.up * Mathf.Sin(t * Mathf.PI) * arcHeight;
                    medallion.Rotate(new Vector3(1f, 1f, .35f).normalized, Time.unscaledDeltaTime * 960f, Space.World);
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
            for (var elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                transform.localPosition = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds)));
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
            for (var elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
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

        private Transform CreateRuntimeMedallion()
        {
            var medallion = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            medallion.name = "SummoningMedallion";
            medallion.transform.SetParent(rightHand != null ? rightHand : transform, false);
            medallion.transform.localPosition = rightHand != null ? new Vector3(0f, -0.02f, 0.08f) : Vector3.up * 0.9f;
            medallion.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            medallion.transform.localScale = new Vector3(0.055f, 0.012f, 0.055f);

            var collider = medallion.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                runtimeMedallionMaterial = new Material(shader)
                {
                    name = "Runtime Summoning Medallion",
                    color = new Color(0.96f, 0.67f, 0.12f, 1f),
                };
                medallion.GetComponent<Renderer>().sharedMaterial = runtimeMedallionMaterial;
            }
            return medallion.transform;
        }

        private static Transform FindRecursive(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (var index = 0; index < root.childCount; index++)
            {
                var match = FindRecursive(root.GetChild(index), objectName);
                if (match != null) return match;
            }
            return null;
        }

        private void OnDestroy()
        {
            if (runtimeMedallionMaterial == null) return;
            if (Application.isPlaying) Destroy(runtimeMedallionMaterial);
            else DestroyImmediate(runtimeMedallionMaterial);
        }
    }
}
