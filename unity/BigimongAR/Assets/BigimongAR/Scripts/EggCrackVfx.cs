using UnityEngine;

namespace Bigimong.AR
{
    /// <summary>Bounded, deterministic presentation pool; never owns hatch progress or identity.</summary>
    public sealed class EggCrackVfx : MonoBehaviour
    {
        private const int FragmentLimit = 24;
        private const float Lifetime = 1.2f;
        private readonly LineRenderer[] cracks = new LineRenderer[3];
        private readonly GameObject[] fragments = new GameObject[FragmentLimit];
        private readonly Vector3[] velocities = new Vector3[FragmentLimit];
        private Renderer[] eggRenderers;
        private bool[] rendererEnabled;
        private Bounds bounds;
        private Material crackMaterial;
        private float age;
        private bool bursting;
        private int fragmentBudget = FragmentLimit;
        public int ActiveCrackGroups { get; private set; }
        public int ActiveFragments { get; private set; }

        public void SetFragmentBudget(int limit)
        {
            fragmentBudget = Mathf.Clamp(limit, 0, FragmentLimit);
            if (!bursting) return;
            ActiveFragments = Mathf.Min(ActiveFragments, fragmentBudget);
            for (var i = ActiveFragments; i < FragmentLimit; i++)
                if (fragments[i] != null) fragments[i].SetActive(false);
        }

        public void Bind(GameObject egg)
        {
            Reset();
            eggRenderers = egg != null ? egg.GetComponentsInChildren<Renderer>(true) : null;
            rendererEnabled = eggRenderers != null ? new bool[eggRenderers.Length] : null;
            var found = false;
            if (eggRenderers != null)
                for (var i = 0; i < eggRenderers.Length; i++)
                {
                    var renderer = eggRenderers[i];
                    rendererEnabled[i] = renderer.enabled;
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            if (!found) return;
            EnsurePool();
            for (var branch = 0; branch < 3; branch++)
            {
                var line = cracks[branch];
                line.transform.SetParent(egg.transform, false);
                line.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                line.transform.localScale = Vector3.one;
                line.positionCount = 9;
                line.widthMultiplier = Mathf.Max(.003f, bounds.size.y * .012f);
                for (var point = 0; point < 9; point++)
                {
                    var y = -.75f + point * .18f;
                    var angle = branch * 2.094395f + (point % 2 == 0 ? -.09f : .09f);
                    var ring = Mathf.Sqrt(1f - y * y) * 1.015f;
                    var worldPoint = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3(Mathf.Sin(angle) * ring, y, -Mathf.Cos(angle) * ring));
                    line.SetPosition(point, egg.transform.InverseTransformPoint(worldPoint));
                }
            }
        }

        private void EnsurePool()
        {
            if (crackMaterial == null)
                crackMaterial = new Material(Shader.Find("Sprites/Default")) { name = "EggCracks", color = new Color(.35f, .17f, .06f) };
            for (var i = 0; i < 3; i++)
            {
                if (cracks[i] != null) continue;
                var node = new GameObject("CrackBranch" + i, typeof(LineRenderer));
                node.transform.SetParent(transform, false);
                node.layer = gameObject.layer;
                cracks[i] = node.GetComponent<LineRenderer>();
                cracks[i].sharedMaterial = crackMaterial;
                cracks[i].useWorldSpace = false;
                cracks[i].numCornerVertices = 2;
                cracks[i].enabled = false;
            }
            for (var i = 0; i < FragmentLimit; i++)
            {
                if (fragments[i] != null) continue;
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "PooledShell" + i;
                piece.transform.SetParent(transform, false);
                piece.layer = gameObject.layer;
                piece.GetComponent<Collider>().enabled = false;
                Release(piece.GetComponent<Collider>());
                fragments[i] = piece;
                piece.SetActive(false);
            }
        }

        public void SetStage(int stage)
        {
            ActiveCrackGroups = Mathf.Clamp(stage, 0, 3);
            for (var i = 0; i < cracks.Length; i++)
                if (cracks[i] != null) cracks[i].enabled = i < ActiveCrackGroups;
        }

        public void Burst(bool particlesEnabled = true)
        {
            SetStage(0);
            foreach (var line in cracks) if (line != null) line.transform.SetParent(transform, true);
            age = 0f;
            bursting = particlesEnabled && fragments[0] != null;
            ActiveFragments = bursting ? fragmentBudget : 0;
            for (var i = 0; i < FragmentLimit; i++)
            {
                if (fragments[i] == null) continue;
                var piece = fragments[i];
                var angle = i * 2.399963f;
                var radial = new Vector3(Mathf.Sin(angle), .4f + (i % 5) * .15f, Mathf.Cos(angle));
                velocities[i] = radial * bounds.size.y * .9f;
                piece.transform.position = bounds.center + Vector3.Scale(bounds.extents, radial.normalized) * .5f;
                piece.transform.localScale = Vector3.one * bounds.size.y * .075f;
                piece.transform.rotation = Quaternion.Euler(i * 19f, i * 31f, i * 13f);
                if (eggRenderers != null && eggRenderers.Length > 0 && eggRenderers[0] != null)
                    piece.GetComponent<Renderer>().sharedMaterial = eggRenderers[0].sharedMaterial;
                piece.SetActive(bursting && i < ActiveFragments);
            }
            // Emit and hide in the same rendered frame: no intact shell remains behind the pieces.
            if (eggRenderers != null)
                foreach (var renderer in eggRenderers) if (renderer != null) renderer.enabled = false;
        }

        public void Advance(float deltaSeconds)
        {
            if (!bursting || deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
            age += deltaSeconds;
            if (age >= Lifetime - .000001f)
            {
                bursting = false;
                ActiveFragments = 0;
                foreach (var piece in fragments) if (piece != null) piece.SetActive(false);
                return;
            }
            for (var i = 0; i < ActiveFragments; i++)
            {
                var radial = velocities[i].normalized;
                fragments[i].transform.position = bounds.center + Vector3.Scale(bounds.extents, radial) * .5f +
                    velocities[i] * age + Vector3.down * (bounds.size.y * .7f * age * age);
            }
        }

        // Memory shedding is not a sequence rewind: preserve the intact shell's current visibility.
        public void ReleaseTransientPool()
        {
            bursting = false;
            age = 0f;
            SetStage(0);
            foreach (var line in cracks) if (line != null) line.transform.SetParent(transform, true);
            ActiveFragments = 0;
            foreach (var piece in fragments) if (piece != null) piece.SetActive(false);
        }

        public void Reset()
        {
            ReleaseTransientPool();
            if (eggRenderers != null)
                for (var i = 0; i < eggRenderers.Length; i++)
                    if (eggRenderers[i] != null) eggRenderers[i].enabled = rendererEnabled[i];
        }
        private void Update() => Advance(Time.unscaledDeltaTime);
        private void OnDisable() => Reset();
        private void OnDestroy() { Reset(); Release(crackMaterial); }
        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
