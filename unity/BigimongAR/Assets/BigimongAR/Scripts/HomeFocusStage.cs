using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Bigimong.AR
{
    /// <summary>One isolated, transparent 3D slot. Owns instances and stage resources, never source assets.</summary>
    public sealed class HomeFocusStage : MonoBehaviour
    {
        private HomeCharacterResolver resolver;
        private HomeStageOrbitInput orbit;
        private HomeSubjectMotion motion;
        private GameObject rig, activeSlot, pendingSlot;
        private Camera stageCamera;
        private Light keyLight, rimLight;
        private HomeStageQualityController quality;
        private Vector2 viewportPixels;
        private float viewportScale = 1f, qualityScale = 1f;
        private Func<GameObject> pendingFactory;
        // Allows editor checks to drive the same suspended recovery transaction without a player loop.
        private Action<IEnumerator> recoveryScheduler;
        private HomeSubject pendingSubject;
        private int pendingRetries, switchGeneration;
        private bool preparingCandidate, memoryDuringPreparation;
        private Transform shadow;
        private Material shadowMaterial;
        private Texture2D shadowTexture;
        // Native allocation is a fallible dependency; ownership/publication remain with the stage.
        private Func<RenderTexture, bool> createRenderTexture = texture => texture.Create();
        private RawImage viewport;
        private Image cover;
        private int homeStageLayer;
        private bool inputLocked;
        private Bounds subjectBounds;
        private bool hasBounds;
        public HomeSubject ActiveSubject { get; private set; }
        public GameObject SubjectInstance { get; private set; }
        public Transform StageRoot => rig != null ? rig.transform : null;
        public RenderTexture Texture { get; private set; }
        public bool InputLocked => inputLocked;
        public float CoverAlpha { get; private set; }
        public event Action<string> RetryNotice;
        public float ProjectedSubjectHeight
        {
            get
            {
                if (!hasBounds || stageCamera == null) return 1f;
                var distance = Vector3.Distance(stageCamera.transform.localPosition, subjectBounds.center);
                return subjectBounds.size.y / Mathf.Max(.001f, 2f * distance * Mathf.Tan(stageCamera.fieldOfView * Mathf.Deg2Rad * .5f));
            }
        }

        public void Initialize(HomeCharacterResolver characterResolver = null)
        {
            resolver = characterResolver ?? resolver ?? new HomeCharacterResolver();
            EnsureStage();
            if (quality == null) quality = GetComponent<HomeStageQualityController>() ?? gameObject.AddComponent<HomeStageQualityController>();
            quality.Initialize(this);
            ApplyQuality(quality.Tier, quality.RenderScale);
        }

        public void BindViewport(RawImage target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (orbit != null) { orbit.Resetting -= CancelMotion; orbit.Bind(null); }
            if (viewport != null) viewport.texture = null;
            if (cover != null) Release(cover.gameObject);
            viewport = target;
            orbit = target.GetComponent<HomeStageOrbitInput>() ?? target.gameObject.AddComponent<HomeStageOrbitInput>();
            orbit.Resetting += CancelMotion;
            orbit.Bind(activeSlot != null ? activeSlot.transform : null);
            orbit.SetInputLocked(inputLocked);
            viewport.texture = Texture;
            var coverObject = new GameObject("HomeSwitchCover", typeof(RectTransform), typeof(Image));
            coverObject.transform.SetParent(target.transform, false);
            cover = coverObject.GetComponent<Image>();
            cover.raycastTarget = false;
            cover.rectTransform.anchorMin = Vector2.zero;
            cover.rectTransform.anchorMax = Vector2.one;
            cover.rectTransform.offsetMin = cover.rectTransform.offsetMax = Vector2.zero;
            SetCover(CoverAlpha);
        }

        public void ShowEgg() => RequestSwitch(() => resolver.CreateEgg(), HomeSubject.EGG);
        public void ShowBaby(int artId) => RequestSwitch(() => resolver.CreateBaby(artId), HomeSubject.DINOSAUR);
        public void ShowAvatar(AvatarProfile profile) => RequestSwitch(() => resolver.CreateAvatar(profile), HomeSubject.AVATAR);
        public void ResetFront() { if (!inputLocked) { CancelMotion(); orbit?.ResetFront(); } }

        // Egg care acceptance belongs to the coordinator; a bare egg tap is harmless by default.
        public void PlayTapReaction() => PlayReaction(ActiveSubject == HomeSubject.EGG ? HomeReaction.CooldownWobble :
            ActiveSubject == HomeSubject.DINOSAUR ? HomeReaction.DinosaurIdle : HomeReaction.AvatarWave);
        public void PlayReaction(HomeReaction reaction) { if (!inputLocked && motion != null) motion.Play(reaction); }

        private void RequestSwitch(Func<GameObject> create, HomeSubject subject)
        {
            if (inputLocked || !isActiveAndEnabled) return;
            Initialize();
            StartCoroutine(SwitchSubject(create, subject));
        }

        private IEnumerator SwitchSubject(Func<GameObject> create, HomeSubject subject) => RunSwitch(create, subject, false);

        private IEnumerator RunSwitch(Func<GameObject> create, HomeSubject subject, bool retry)
        {
            var run = ++switchGeneration;
            pendingFactory = create;
            pendingSubject = subject;
            if (!retry) pendingRetries = 0;
            SetInputLocked(true);
            CancelMotion();
            GameObject candidate = null;
            HomeStageQualityController.SubjectAdmission admission = default;
            Bounds bounds = default;
            Exception failure = null;
            while (true)
            {
                preparingCandidate = true;
                memoryDuringPreparation = false;
                failure = null;
                try
                {
                    EnsureStage();
                    pendingSlot = new GameObject("HomeSubjectYaw");
                    pendingSlot.SetActive(false);
                    pendingSlot.transform.SetParent(rig.transform, false);
                    var presentation = new GameObject("HomePresentation");
                    presentation.transform.SetParent(pendingSlot.transform, false);
                    candidate = create();
                    if (candidate == null) throw new InvalidOperationException("Resolver returned no subject");
                    SuppressArMotion(candidate);
                    candidate.SetActive(false);
                    candidate.transform.SetParent(presentation.transform, false);
                    AssignLayerRecursively(pendingSlot, homeStageLayer);
                    // Asset cameras/lights cannot compete with the stage's fixed rig.
                    foreach (var camera in candidate.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                    foreach (var light in candidate.GetComponentsInChildren<Light>(true)) light.enabled = false;
                    candidate.SetActive(true); // Parent remains inactive until atomic commit.
                    var animatorSeen = false;
                    foreach (var animator in candidate.GetComponentsInChildren<Animator>(true))
                    {
                        if (!animator.enabled || !ActiveWithin(animator.transform, candidate.transform)) continue;
                        if (animatorSeen) animator.enabled = false;
                        animatorSeen = true;
                    }
                    bounds = ValidateRenderable(candidate);
                    var nextMotion = presentation.AddComponent<HomeSubjectMotion>();
                    nextMotion.Bind(presentation.transform, candidate.GetComponentInChildren<Animator>(true));
                    // Validate framing while the previous subject is still untouched.
                    CameraDistance(bounds, stageCamera.aspect);
                    // All allocating quality queries finish before releasing the visible subject.
                    admission = quality != null ? quality.PrepareSubject(candidate) : default;
                }
                catch (Exception exception)
                {
                    failure = exception;
                    if (exception is OutOfMemoryException) quality?.HandleLowMemory();
                }
                finally { preparingCandidate = false; }
                if (run != switchGeneration) { ReleaseSlot(candidate); yield break; }
                if (failure == null && !memoryDuringPreparation) break;
                if (candidate != null && (pendingSlot == null || !candidate.transform.IsChildOf(pendingSlot.transform))) Release(candidate);
                ReleaseSlot(pendingSlot);
                pendingSlot = null;
                candidate = null;
                if (memoryDuringPreparation && pendingRetries == 0) { pendingRetries = 1; continue; }
                failure ??= new OutOfMemoryException("Low-memory retry exhausted");
                break;
            }
            if (failure != null)
            {
                if (candidate != null && (pendingSlot == null || !candidate.transform.IsChildOf(pendingSlot.transform))) Release(candidate);
                ReleaseSlot(pendingSlot);
                pendingSlot = null;
                Debug.LogWarning($"Home subject switch rejected: {failure.Message}");
                pendingFactory = null;
                SetCover(0f);
                SetInputLocked(false);
                RetryNotice?.Invoke("모델을 불러오지 못했어요. 다시 선택해 주세요.");
                yield break;
            }

            yield return FadeCover(0f, 1f, .12f);
            if (run != switchGeneration || pendingSlot == null) yield break;
            var previous = activeSlot;
            // OnEnable/OnDisable observers can report pressure synchronously. Finish the ownership
            // transfer before shedding resources; never restart a factory that has already published.
            preparingCandidate = true;
            memoryDuringPreparation = false;
            try
            {
                CancelMotion();
                if (previous != null) previous.SetActive(false);
                if (run != switchGeneration) yield break;
                activeSlot = pendingSlot;
                pendingSlot = null;
                SubjectInstance = candidate;
                subjectBounds = bounds;
                hasBounds = true;
                FrameWithoutChangingProportions(bounds);
                motion = activeSlot.GetComponentInChildren<HomeSubjectMotion>(true);
                orbit?.Bind(activeSlot.transform); // Model root retains authored heading below this pivot.
                ActiveSubject = subject;
                pendingFactory = null;
                ReleaseSlot(previous);
                if (run != switchGeneration) yield break;
                quality?.CommitSubject(admission);
                activeSlot.SetActive(true);
            }
            finally { preparingCandidate = false; }
            if (run != switchGeneration) yield break;
            if (memoryDuringPreparation) { quality?.HandleLowMemory(); yield break; }
            yield return FadeCover(1f, 0f, .13f);
            if (run != switchGeneration) yield break;
            SetInputLocked(false);
        }

        public void SetViewportSize(Vector2 pixels, float renderScale)
        {
            if (!Finite(pixels.x) || !Finite(pixels.y) || !Finite(renderScale) || pixels.x <= 0f || pixels.y <= 0f || renderScale <= 0f) return;
            EnsureStage();
            viewportPixels = pixels;
            viewportScale = renderScale;
            var scaled = pixels * Mathf.Min(renderScale, 1f);
            scaled *= Mathf.Min(1f, 1080f / Mathf.Max(scaled.x, scaled.y));
            scaled *= qualityScale;
            var width = Mathf.Clamp(Mathf.RoundToInt(scaled.x), 1, 1080);
            var height = Mathf.Clamp(Mathf.RoundToInt(scaled.y), 1, 1080);
            if (Texture != null && Texture.IsCreated() && Texture.width == width && Texture.height == height)
            { RefreshCameraEnabled(); return; }
            var next = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            { name = "HomeStageTexture", antiAliasing = 1, useMipMap = false };
            if (!createRenderTexture(next) || !next.IsCreated())
            {
                next.Release();
                Release(next);
                RefreshCameraEnabled();
                return; // Preserve the previous target and consumer bindings; a later call may retry.
            }
            var previous = Texture;
            Texture = next;
            stageCamera.targetTexture = next;
            stageCamera.aspect = (float)width / height;
            if (viewport != null) viewport.texture = next;
            if (previous != null) { previous.Release(); Release(previous); }
            if (hasBounds) FrameWithoutChangingProportions(subjectBounds);
        }

        public void ApplyQuality(HomeQualityTier tier, float renderScale)
        {
            qualityScale = renderScale;
            ApplyLights(tier);
            if (rig != null && viewportPixels.x > 0f) SetViewportSize(viewportPixels, viewportScale);
        }

        private void ApplyLights(HomeQualityTier tier)
        {
            if (keyLight != null)
            {
                keyLight.shadows = tier == HomeQualityTier.LOW ? LightShadows.None : LightShadows.Soft;
                keyLight.shadowCustomResolution = tier == HomeQualityTier.HIGH ? 1024 : 512;
            }
            if (rimLight != null) rimLight.enabled = tier != HomeQualityTier.LOW;
        }

        public void HandleLowMemory(float renderScale)
        {
            SetInputLocked(true);
            CancelMotion();
            qualityScale = renderScale;
            ApplyLights(HomeQualityTier.LOW);
            ReleaseRenderTarget();
            if (preparingCandidate) { memoryDuringPreparation = true; return; }
            ++switchGeneration;
            StopAllCoroutines();
            ReleaseSlot(pendingSlot);
            pendingSlot = null;
            var retryFactory = pendingFactory;
            if (retryFactory != null && pendingRetries == 0)
            {
                pendingRetries = 1;
                var recovery = RunSwitch(retryFactory, pendingSubject, true);
                if (recoveryScheduler != null) recoveryScheduler(recovery);
                else StartCoroutine(recovery);
                return;
            }
            pendingFactory = null;
            SetCover(0f);
            SetInputLocked(false);
            if (retryFactory != null) RetryNotice?.Invoke("모델을 불러오지 못했어요. 다시 선택해 주세요.");
        }

        public void LockForMemoryPressure() { SetInputLocked(true); CancelMotion(); }

        private void ReleaseRenderTarget()
        {
            if (stageCamera != null) { stageCamera.enabled = false; stageCamera.targetTexture = null; }
            if (viewport != null) viewport.texture = null;
            if (Texture != null) { Texture.Release(); Release(Texture); Texture = null; }
        }

        private void EnsureStage()
        {
            if (rig != null) return;
            homeStageLayer = LayerMask.NameToLayer("HomeStage");
            if (homeStageLayer < 0) throw new InvalidOperationException("HomeStage layer must be prepared before playing/building");
            rig = new GameObject("HomeStage");
            // Keep the offscreen studio away from AR/world geometry, independent of scaled UI transforms.
            rig.transform.position = new Vector3(10000f, 10000f, 10000f);
            var cameraObject = new GameObject("HomeStageCamera", typeof(Camera));
            cameraObject.transform.SetParent(rig.transform, false);
            stageCamera = cameraObject.GetComponent<Camera>();
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = Color.clear;
            stageCamera.cullingMask = 1 << homeStageLayer;
            stageCamera.fieldOfView = 32f;
            stageCamera.allowHDR = false;
            stageCamera.allowMSAA = false;
            // Do not render to the device framebuffer before a viewport supplies a target.
            stageCamera.enabled = false;
            keyLight = CreateLight("WarmKey", new Color(1f, .85f, .68f), 1.1f, new Vector3(35f, -35f, 0f));
            CreateLight("CoolFill", new Color(.65f, .8f, 1f), .55f, new Vector3(20f, 140f, 0f));
            rimLight = CreateLight("Rim", new Color(1f, .91f, .77f), .7f, new Vector3(20f, 180f, 0f));
            ApplyLights(quality != null ? quality.Tier : HomeQualityTier.HIGH);
            CreateShadow();
            AssignLayerRecursively(rig, homeStageLayer);
        }

        private Light CreateLight(string name, Color color, float intensity, Vector3 angles)
        {
            var node = new GameObject(name, typeof(Light));
            node.transform.SetParent(rig.transform, false);
            node.transform.localRotation = Quaternion.Euler(angles);
            var light = node.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.cullingMask = 1 << homeStageLayer;
            light.shadows = LightShadows.None;
            return light;
        }

        private void CreateShadow()
        {
            var node = GameObject.CreatePrimitive(PrimitiveType.Quad);
            node.name = "GroundShadow";
            node.transform.SetParent(rig.transform, false);
            Release(node.GetComponent<Collider>());
            shadow = node.transform;
            shadow.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadowTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "HomeShadow", wrapMode = TextureWrapMode.Clamp };
            for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                {
                    var radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                    shadowTexture.SetPixel(x, y, new Color(.18f, .09f, .04f, Mathf.Pow(Mathf.Clamp01(1f - radius), 2f) * .3f));
                }
            shadowTexture.Apply(false, true);
            shadowMaterial = new Material(Shader.Find("Sprites/Default")) { name = "HomeShadowMaterial", mainTexture = shadowTexture };
            node.GetComponent<Renderer>().sharedMaterial = shadowMaterial;
        }

        private Bounds ValidateRenderable(GameObject candidate)
        {
            var found = false;
            var combined = new Bounds();
            foreach (var renderer in candidate.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !ActiveWithin(renderer.transform, candidate.transform)) continue;
                var skin = renderer as SkinnedMeshRenderer;
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = skin != null ? skin.sharedMesh : filter != null ? filter.sharedMesh : null;
                if (mesh == null || mesh.vertexCount == 0 || renderer.sharedMaterial == null) continue;
                var local = renderer.localBounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = local.center + Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    point = rig.transform.InverseTransformPoint(renderer.transform.TransformPoint(point));
                    if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z)) throw new InvalidOperationException("Non-finite subject bounds");
                    if (!found) { combined = new Bounds(point, Vector3.zero); found = true; }
                    else combined.Encapsulate(point);
                }
            }
            if (!found || combined.size.sqrMagnitude <= .000001f) throw new InvalidOperationException("Subject has no visible 3D geometry");
            return combined;
        }

        private static bool ActiveWithin(Transform node, Transform root)
        {
            for (var current = node; current != null; current = current.parent)
            {
                if (!current.gameObject.activeSelf) return false;
                if (current == root) return true;
            }
            return false;
        }

        private static void SuppressArMotion(GameObject candidate)
        {
            foreach (var actor in candidate.GetComponentsInChildren<ArBattleActor>(true))
                DisablePreservingAuthoredPose(actor, value => value.enabled = false);
        }

        private static void DisablePreservingAuthoredPose(ArBattleActor actor, Action<ArBattleActor> disable)
        {
            // Uninitialized AR actors restore default (zero-scale) pose in OnDisable.
            // Keep the component for material ownership, but contain its synchronous pose writes.
            var node = actor.transform;
            var position = node.localPosition;
            var rotation = node.localRotation;
            var scale = node.localScale;
            try { disable(actor); }
            finally
            {
                node.localPosition = position;
                node.localRotation = rotation;
                node.localScale = scale;
            }
        }

        private float CameraDistance(Bounds bounds, float aspect)
        {
            // Rotation-safe sphere around the yaw axis covers asymmetric tails at every heading.
            var radius = bounds.extents.magnitude + new Vector2(bounds.center.x, bounds.center.z).magnitude;
            var halfFov = stageCamera.fieldOfView * Mathf.Deg2Rad * .5f;
            var limitingAngle = Mathf.Min(halfFov, Mathf.Atan(Mathf.Tan(halfFov) * Mathf.Max(.01f, aspect)));
            var distance = radius * 1.15f / Mathf.Sin(limitingAngle);
            if (!Finite(distance) || distance <= 0f) throw new InvalidOperationException("Subject cannot be framed");
            return distance;
        }

        private void FrameWithoutChangingProportions(Bounds bounds)
        {
            var distance = CameraDistance(bounds, stageCamera.aspect);
            var aim = new Vector3(0f, bounds.center.y, 0f);
            var offset = new Vector3(0f, .12f, -1f).normalized * distance;
            stageCamera.transform.localPosition = aim + offset;
            stageCamera.transform.localRotation = Quaternion.LookRotation(-offset);
            stageCamera.nearClipPlane = Mathf.Max(.001f, distance * .01f);
            stageCamera.farClipPlane = distance * 3f;
            RefreshCameraEnabled();
            shadow.localPosition = new Vector3(0f, bounds.min.y - .01f, 0f);
            var diameter = Mathf.Max(bounds.size.x, bounds.size.z) * 1.25f;
            shadow.localScale = new Vector3(diameter, diameter, 1f);
        }

        private void RefreshCameraEnabled()
        {
            if (stageCamera != null)
                stageCamera.enabled = hasBounds && Texture != null && Texture.IsCreated() && stageCamera.targetTexture == Texture;
        }

        private IEnumerator FadeCover(float from, float to, float seconds)
        {
            var elapsed = 0f;
            SetCover(from);
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetCover(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds)));
                yield return null;
            }
            SetCover(to);
        }

        private void SetCover(float alpha)
        {
            CoverAlpha = alpha;
            if (cover != null) cover.color = new Color(.98f, .90f, .74f, alpha);
        }
        private void SetInputLocked(bool locked) { inputLocked = locked; orbit?.SetInputLocked(locked); }
        private void CancelMotion() { if (motion != null) motion.CancelAndRestore(); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void AssignLayerRecursively(GameObject node, int layer)
        {
            foreach (var child in node.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
        }
        private static void ReleaseSlot(GameObject slot)
        {
            if (slot == null) return;
            foreach (var reaction in slot.GetComponentsInChildren<HomeSubjectMotion>(true)) reaction.CancelAndRestore();
            slot.SetActive(false); // No outgoing Animator, light, camera or particles survive until deferred Destroy.
            Release(slot);
        }
        private static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        public void DestroyActiveSubject()
        {
            ++switchGeneration;
            pendingFactory = null;
            StopAllCoroutines();
            CancelMotion();
            if (orbit != null) { orbit.Resetting -= CancelMotion; orbit.SetInputLocked(true); orbit.Bind(null); }
            ReleaseSlot(activeSlot);
            ReleaseSlot(pendingSlot);
            activeSlot = pendingSlot = null;
            SubjectInstance = null;
            motion = null;
            hasBounds = false;
            quality?.BindSubject(null);
            ReleaseRenderTarget();
            if (rig != null) { rig.SetActive(false); Release(rig); rig = null; }
            Release(shadowMaterial);
            Release(shadowTexture);
            shadowMaterial = null;
            shadowTexture = null;
            if (cover != null) { Release(cover.gameObject); cover = null; }
            viewport = null;
            orbit = null;
            inputLocked = false;
            CoverAlpha = 0f;
        }
        private void OnDisable() => DestroyActiveSubject();
        private void OnDestroy() => DestroyActiveSubject();
    }
}
