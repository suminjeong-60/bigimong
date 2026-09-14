using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bigimong.AR
{
    public sealed class ProceduralMagicCircle : MonoBehaviour
    {
        public const int RingCount = 2;
        public const int RuneCount = 8;
        private const float ArenaDiameter = 2.2f;
        private Material goldMaterial;
        private Vector3 expandedScale;

        public static ProceduralMagicCircle Create(Transform arenaRoot)
        {
            var root = new GameObject("Gold Summoning Circle");
            root.transform.SetParent(arenaRoot, false);
            var circle = root.AddComponent<ProceduralMagicCircle>();
            circle.Build();
            return circle;
        }

        public IEnumerator Expand(float seconds)
        {
            transform.localScale = Vector3.zero;
            var duration = Mathf.Max(0.01f, seconds);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.localScale = expandedScale * progress;
                SetEmission(1.4f + Mathf.Sin(progress * Mathf.PI * 3f) * 0.8f);
                RotateRings(Time.unscaledDeltaTime);
                yield return null;
            }
            transform.localScale = expandedScale;
            SetEmission(1.7f);
        }

        public IEnumerator ImpactBurst(float seconds)
        {
            transform.localScale = expandedScale;
            var duration = Mathf.Clamp(seconds, .12f, .6f);
            const int segmentCount = 24;
            var segments = new List<GameObject>(segmentCount);
            for (var index = 0; index < segmentCount; index++)
            {
                var angle = index * 360f / segmentCount;
                var segment = Primitive(PrimitiveType.Cube, $"Shockwave Segment {index + 1:00}", transform);
                segment.transform.localRotation = Quaternion.Euler(0, angle, 0);
                segment.transform.localScale = new Vector3(.05f, .005f, .025f);
                segments.Add(segment);
            }

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var progress = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - progress, 3f);
                var radius = Mathf.Lerp(.08f, .64f, eased);
                var thickness = Mathf.Lerp(.055f, .008f, progress);
                for (var index = 0; index < segments.Count; index++)
                {
                    var radians = index * Mathf.PI * 2f / segments.Count;
                    var segment = segments[index];
                    if (segment == null) continue;
                    segment.transform.localPosition = new Vector3(Mathf.Sin(radians) * radius, .012f,
                        Mathf.Cos(radians) * radius);
                    segment.transform.localScale = new Vector3(.055f + progress * .04f, .005f, thickness);
                }
                SetEmission(2.3f - progress * .8f);
                RotateRings(Time.unscaledDeltaTime * 1.6f);
                yield return null;
            }

            foreach (var segment in segments)
            {
                if (segment == null) continue;
                if (Application.isPlaying) Destroy(segment);
                else DestroyImmediate(segment);
            }
        }

        private void Build()
        {
            goldMaterial = CreateGoldMaterial();
            expandedScale = Vector3.one * ArenaDiameter;
            transform.localScale = expandedScale;
            CreateDisc();
            for (var index = 0; index < RingCount; index++) CreateRing(index);
            for (var index = 0; index < RuneCount; index++) CreateRune(index);
        }

        private void CreateDisc()
        {
            var disc = Primitive(PrimitiveType.Cylinder, "Center Disc", transform);
            disc.transform.localPosition = new Vector3(0, 0.003f, 0);
            disc.transform.localScale = new Vector3(0.12f, 0.002f, 0.12f);
        }

        private void CreateRing(int index)
        {
            var ring = Primitive(PrimitiveType.Cylinder, $"Rotating Ring {index + 1}", transform);
            ring.transform.localPosition = new Vector3(0, 0.004f + index * 0.002f, 0);
            var diameter = 0.56f + index * 0.28f;
            ring.transform.localScale = new Vector3(diameter, 0.0015f, diameter);
            var center = Primitive(PrimitiveType.Cylinder, "Ring Cutout", ring.transform);
            center.transform.localPosition = Vector3.zero;
            center.transform.localScale = new Vector3(0.88f, 1.5f, 0.88f);
            var renderer = center.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateDarkMaterial();
        }

        private void CreateRune(int index)
        {
            var angle = index * 360f / RuneCount;
            var radians = angle * Mathf.Deg2Rad;
            var rune = Primitive(PrimitiveType.Cube, $"Radial Rune {index + 1}", transform);
            rune.transform.localPosition = new Vector3(Mathf.Sin(radians) * 0.39f, 0.009f, Mathf.Cos(radians) * 0.39f);
            rune.transform.localRotation = Quaternion.Euler(0, angle, 0);
            rune.transform.localScale = new Vector3(0.035f, 0.003f, index % 2 == 0 ? 0.13f : 0.09f);
        }

        private GameObject Primitive(PrimitiveType type, string name, Transform parent)
        {
            var value = GameObject.CreatePrimitive(type);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.GetComponent<Renderer>().sharedMaterial = goldMaterial;
            var collider = value.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
            return value;
        }

        private void RotateRings(float delta)
        {
            for (var index = 0; index < RingCount; index++)
            {
                var ring = transform.Find($"Rotating Ring {index + 1}");
                if (ring != null) ring.Rotate(Vector3.up, delta * (index == 0 ? 80f : -55f), Space.Self);
            }
        }

        private void SetEmission(float intensity)
        {
            if (goldMaterial != null && goldMaterial.HasProperty("_EmissionColor"))
                goldMaterial.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.08f) * intensity);
        }

        private static Material CreateGoldMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = new Color(1f, 0.63f, 0.08f, 0.92f) };
            material.EnableKeyword("_EMISSION");
            return material;
        }

        private static Material CreateDarkMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            return new Material(shader) { color = new Color(0.08f, 0.025f, 0.005f, 0.75f) };
        }
    }
}
