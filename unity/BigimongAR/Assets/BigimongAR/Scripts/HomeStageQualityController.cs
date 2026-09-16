using UnityEngine;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine.Profiling;
#endif

namespace Bigimong.AR
{
    public enum HomeQualityTier { HIGH, MEDIUM, LOW }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public readonly struct HomePerformanceSnapshot
    {
        public readonly float averageFps;
        public readonly long totalAllocatedBytes;
        public readonly HomeQualityTier tier;
        public readonly int activeRenderers;
        public readonly int activeAnimators;
        public HomePerformanceSnapshot(float fps, long bytes, HomeQualityTier quality, int renderers, int animators)
        { averageFps = fps; totalAllocatedBytes = bytes; tier = quality; activeRenderers = renderers; activeAnimators = animators; }
    }
#endif

    /// <summary>Bounded policy, not measured device performance acceptance. Never resolves an identity.</summary>
    [DisallowMultipleComponent]
    public sealed class HomeStageQualityController : MonoBehaviour
    {
        private static readonly float[] RenderScales = { 1f, 0.75f, 0.6f };
        private HomeFocusStage stage;
        private HatchSequenceDirector sequence;
        private bool subscribed, windowHadHatch;
        private int frames, slowWindows, fastWindows;
        private double windowSeconds;
        private LODGroup[] groups;
        private bool[] acceptedLods;
        private float[] transitions;
        private int[] selectedLods;
        private HomeProceduralDetail procedural;
        // Fallible subject admission is separate from publication; editor checks inject query OOM.
        private System.Func<GameObject, LODGroup[]> collectLodGroups = subject => subject.GetComponentsInChildren<LODGroup>(true);
        internal struct SubjectAdmission
        {
            internal LODGroup[] groups;
            internal bool[] acceptedLods;
            internal float[] transitions;
            internal int[] selectedLods;
            internal HomeProceduralDetail procedural;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            internal Renderer[] renderers;
            internal Animator[] animators;
            internal LOD[][] diagnosticLods;
#endif
        }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private Renderer[] renderers;
        private Animator[] animators;
        private LOD[][] diagnosticLods;
        private int warned;
        public HomePerformanceSnapshot Snapshot { get; private set; }
#endif
        public HomeQualityTier Tier { get; private set; }
        public float RenderScale => RenderScales[(int)Tier];
        public int ActiveLod { get; private set; }

        public void Initialize(HomeFocusStage focusStage)
        {
            stage = focusStage;
            Application.targetFrameRate = 60;
            Subscribe();
        }

        public void BindSequence(HatchSequenceDirector director)
        {
            sequence = director;
            sequence?.SetQualityTier(Tier);
        }

        public void SampleFrame(float unscaledSeconds, bool hatching)
        {
            if (unscaledSeconds <= 0f || float.IsNaN(unscaledSeconds) || float.IsInfinity(unscaledSeconds)) return;
            windowSeconds += unscaledSeconds;
            windowHadHatch |= hatching;
            if (++frames < 120) return;
            var milliseconds = (float)(windowSeconds * 1000.0 / 120);
            slowWindows = milliseconds > 33.333f ? slowWindows + 1 : 0;
            fastWindows = milliseconds < 18f && !windowHadHatch ? fastWindows + 1 : 0;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            PublishDiagnostics(1000f / milliseconds, windowHadHatch);
#endif
            if (slowWindows >= 3 && Tier != HomeQualityTier.LOW) ApplyTier((HomeQualityTier)((int)Tier + 1));
            else if (fastWindows >= 10 && !hatching && Tier != HomeQualityTier.HIGH) ApplyTier((HomeQualityTier)((int)Tier - 1));
            // Saturate streaks at their policy limits even at the end tiers.
            slowWindows = Mathf.Min(slowWindows, 3);
            fastWindows = Mathf.Min(fastWindows, 10);
            frames = 0; windowSeconds = 0; windowHadHatch = false;
        }

        public void ApplyTier(HomeQualityTier tier)
        {
            if (tier < HomeQualityTier.HIGH || tier > HomeQualityTier.LOW) return;
            Tier = tier;
            slowWindows = fastWindows = 0;
            stage?.ApplyQuality(tier, RenderScale);
            sequence?.SetQualityTier(Tier);
            ApplyProjectedHeight(stage != null ? stage.ProjectedSubjectHeight : 1f);
        }

        public void BindSubject(GameObject subject) => CommitSubject(PrepareSubject(subject));

        internal SubjectAdmission PrepareSubject(GameObject subject)
        {
            if (subject == null) return default;
            var admission = new SubjectAdmission { groups = collectLodGroups(subject) };
            var count = admission.groups.Length;
            admission.acceptedLods = new bool[count]; admission.transitions = new float[count]; admission.selectedLods = new int[count];
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            admission.diagnosticLods = new LOD[count][];
#endif
            for (var i = 0; i < count; i++)
            {
                var lods = admission.groups[i].GetLODs();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                admission.diagnosticLods[i] = lods;
#endif
                admission.acceptedLods[i] = admission.groups[i].enabled && ValidatedPair(subject.transform, lods);
                admission.transitions[i] = lods.Length > 0 ? lods[0].screenRelativeTransitionHeight : .6f;
                admission.selectedLods[i] = -1;
            }
            admission.procedural = subject.GetComponent<HomeProceduralDetail>();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            admission.renderers = subject.GetComponentsInChildren<Renderer>(true);
            admission.animators = subject.GetComponentsInChildren<Animator>(true);
#endif
            return admission;
        }

        internal void CommitSubject(SubjectAdmission admission)
        {
            groups = admission.groups;
            acceptedLods = admission.acceptedLods;
            transitions = admission.transitions;
            selectedLods = admission.selectedLods;
            procedural = admission.procedural;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            renderers = admission.renderers;
            animators = admission.animators;
            diagnosticLods = admission.diagnosticLods;
#endif
            ApplyProjectedHeight(stage != null ? stage.ProjectedSubjectHeight : 1f);
        }

        public void ApplyProjectedHeight(float projectedHeight)
        {
            ActiveLod = 0;
            if (groups != null)
                for (var i = 0; i < groups.Length; i++)
                {
                    if (groups[i] == null) continue;
                    var next = acceptedLods[i] && (Tier == HomeQualityTier.LOW || projectedHeight < transitions[i]) ? 1 : 0;
                    if (next != selectedLods[i]) { groups[i].ForceLOD(next); selectedLods[i] = next; }
                    ActiveLod = Mathf.Max(ActiveLod, next);
                }
            if (procedural != null)
            {
                var low = Tier == HomeQualityTier.LOW || projectedHeight < .6f;
                procedural.SetLowDetail(low);
                if (low) ActiveLod = 1;
            }
        }

        // Task 5 owns import acceptance. Defense in depth: never force an arbitrary LOD1 mesh.
        private static bool ValidatedPair(Transform root, LOD[] lods)
        {
            if (lods.Length < 2) return false;
            long high = 0, low = 0;
            for (var level = 0; level < 2; level++)
                for (var i = 0; i < lods[level].renderers.Length; i++)
                {
                    var renderer = lods[level].renderers[i];
                    if (renderer == null || !renderer.transform.IsChildOf(root)) return false;
                    for (var earlier = 0; earlier <= level; earlier++)
                        for (var j = 0; j < (earlier == level ? i : lods[earlier].renderers.Length); j++)
                            if (renderer == lods[earlier].renderers[j]) return false;
                    var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null || mesh.subMeshCount == 0) return false;
                    long triangles = 0;
                    var draws = Mathf.Max(mesh.subMeshCount, renderer.sharedMaterials.Length);
                    for (var draw = 0; draw < draws; draw++)
                    {
                        var subMesh = Mathf.Min(draw, mesh.subMeshCount - 1);
                        if (mesh.GetTopology(subMesh) == MeshTopology.Triangles) triangles += (long)mesh.GetIndexCount(subMesh) / 3;
                    }
                    if (level == 0) high += triangles; else low += triangles;
                }
            return high > 0 && low * 100 >= high * 50 && low * 100 <= high * 60;
        }

        public void HandleLowMemory()
        {
            if (stage == null || !stage.isActiveAndEnabled || stage.StageRoot == null) return;
            // Do not recreate the just-released RT on this callback. The next viewport tick owns recovery.
            Tier = HomeQualityTier.LOW;
            frames = slowWindows = fastWindows = 0; windowSeconds = 0; windowHadHatch = false;
            stage.LockForMemoryPressure();
            sequence?.ReleaseTransientEffects();
            sequence?.SetQualityTier(Tier);
            stage.HandleLowMemory(RenderScale);
            ApplyProjectedHeight(stage.ProjectedSubjectHeight);
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        public int DiagnosticWarnings(float fps, long bytes, bool hatch, int activeAnimators)
        {
            var violations = 0;
            if (!hatch && bytes > 350L * 1024 * 1024) violations |= 1;
            if (fps < 30f) violations |= 2;
            if (hatch && bytes > 450L * 1024 * 1024) violations |= 4;
            if (activeAnimators > 1) violations |= 8;
            var first = violations & ~warned;
            warned |= violations;
            return first;
        }

        private void PublishDiagnostics(float fps, bool hatch)
        {
            var rendererCount = 0; var animatorCount = 0;
            if (renderers != null)
                for (var i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null && renderers[i].enabled && renderers[i].gameObject.activeInHierarchy && SelectedRenderer(renderers[i])) rendererCount++;
            if (animators != null)
                for (var i = 0; i < animators.Length; i++)
                    if (animators[i] != null && animators[i].isActiveAndEnabled) animatorCount++;
            var bytes = Profiler.GetTotalAllocatedMemoryLong();
            Snapshot = new HomePerformanceSnapshot(fps, bytes, Tier, rendererCount, animatorCount);
            var flags = DiagnosticWarnings(slowWindows >= 3 ? fps : Mathf.Max(30f, fps), bytes, hatch, animatorCount);
            if ((flags & 1) != 0) Debug.LogWarning("Home allocated memory exceeds 350 MiB; device profiling required.");
            if ((flags & 2) != 0) Debug.LogWarning("Home average FPS remained below 30 for three windows.");
            if ((flags & 4) != 0) Debug.LogWarning("Hatch allocated memory exceeds 450 MiB; device profiling required.");
            if ((flags & 8) != 0) Debug.LogWarning("More than one subject Animator is active.");
        }

        private bool SelectedRenderer(Renderer renderer)
        {
            if (diagnosticLods != null)
                for (var group = 0; group < diagnosticLods.Length; group++)
                {
                    if (groups[group] == null || !groups[group].enabled) continue;
                    for (var level = 0; level < diagnosticLods[group].Length; level++)
                    {
                        var members = diagnosticLods[group][level].renderers;
                        for (var i = 0; i < members.Length; i++)
                            if (members[i] == renderer) return selectedLods[group] == level;
                    }
                }
            return true;
        }
#endif
        private void Update()
        {
            if (stage == null || stage.StageRoot == null) return;
            SampleFrame(Time.unscaledDeltaTime, sequence != null && sequence.IsHatchPresentationLocked);
            ApplyProjectedHeight(stage.ProjectedSubjectHeight);
        }
        private void Subscribe() { if (!subscribed && isActiveAndEnabled) { Application.lowMemory += HandleLowMemory; subscribed = true; } }
        private void OnEnable() { Application.targetFrameRate = 60; Subscribe(); }
        private void OnDisable()
        {
            if (subscribed) { Application.lowMemory -= HandleLowMemory; subscribed = false; }
            frames = slowWindows = fastWindows = 0; windowSeconds = 0; windowHadHatch = false;
        }
        private void OnDestroy() => OnDisable();
    }

    /// <summary>Fallback-only tessellation branch. Every anatomical part, material and transform stays put.</summary>
    internal sealed class HomeProceduralDetail : MonoBehaviour
    {
        private MeshFilter[] filters;
        private Mesh[] originals;
        private Mesh sphere;
        private bool low;
        public void Prepare()
        {
            filters = GetComponentsInChildren<MeshFilter>(true);
            originals = new Mesh[filters.Length];
            for (var i = 0; i < filters.Length; i++)
                if (filters[i].sharedMesh != null && filters[i].sharedMesh.name == "Sphere") originals[i] = filters[i].sharedMesh;
            sphere = CreateSphere();
        }
        public void SetLowDetail(bool value)
        {
            if (low == value) return;
            low = value;
            for (var i = 0; i < filters.Length; i++)
                if (filters[i] != null && originals[i] != null) filters[i].sharedMesh = low ? sphere : originals[i];
        }
        private static Mesh CreateSphere()
        {
            const int rings = 12, sectors = 16;
            var vertices = new Vector3[(rings + 1) * (sectors + 1)];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var indices = new int[(rings - 1) * sectors * 6];
            for (var y = 0; y <= rings; y++)
                for (var x = 0; x <= sectors; x++)
                {
                    var latitude = y * Mathf.PI / rings; var longitude = x * Mathf.PI * 2f / sectors;
                    var index = y * (sectors + 1) + x;
                    normals[index] = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                    vertices[index] = normals[index] * .5f;
                    uv[index] = new Vector2((float)x / sectors, (float)y / rings);
                }
            var cursor = 0;
            for (var y = 0; y < rings; y++)
                for (var x = 0; x < sectors; x++)
                {
                    var a = y * (sectors + 1) + x; var b = a + sectors + 1;
                    if (y != 0) { indices[cursor++] = a; indices[cursor++] = a + 1; indices[cursor++] = b; }
                    if (y != rings - 1) { indices[cursor++] = a + 1; indices[cursor++] = b + 1; indices[cursor++] = b; }
                }
            var mesh = new Mesh { name = "HomeProceduralSphereLOD1", vertices = vertices, normals = normals, uv = uv, triangles = indices };
            mesh.RecalculateBounds();
            return mesh;
        }
        private void OnDestroy() { if (sphere != null) { if (Application.isPlaying) Destroy(sphere); else DestroyImmediate(sphere); } }
    }
}
