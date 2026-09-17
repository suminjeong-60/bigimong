#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace Bigimong.AR.EditorChecks
{
    public static class HomeStagePerformanceEditorChecks
    {
        public static void RunBehaviorChecks()
        {
            PolicyBoundaries();
            TierIdentityAndLod();
            MemoryRecovery();
            BurstWindowMemoryPressure();
            AdmissionAllocationRecovery();
            PressureBetweenBurstAndWarm();
            Diagnostics();
            ContextLoss();
            SteadyLoopAllocation();
        }

        private static void PolicyBoundaries()
        {
            var host = new GameObject("QualityPolicy");
            try
            {
                var quality = host.AddComponent<HomeStageQualityController>();
                quality.Initialize(null);
                Require(Application.targetFrameRate == 60, "literal 60 fps target");
                for (var i = 0; i < 359; i++) quality.SampleFrame(.034f, false);
                Require(quality.Tier == HomeQualityTier.HIGH, "no downshift before frame 360");
                quality.SampleFrame(.034f, false);
                Require(quality.Tier == HomeQualityTier.MEDIUM, "three slow 120-frame windows downshift exactly one tier");
                Frames(quality, .034f, 360, true);
                Require(quality.Tier == HomeQualityTier.LOW, "slow hatch may downshift");
                Frames(quality, .017f, 1200, true);
                Require(quality.Tier == HomeQualityTier.LOW, "ten fast hatch windows cannot upshift");
                Frames(quality, .017f, 1199, false);
                Require(quality.Tier == HomeQualityTier.LOW, "hatch windows cannot bank an upshift");
                quality.SampleFrame(.017f, false);
                Require(quality.Tier == HomeQualityTier.MEDIUM, "ten fast home windows upshift one tier");
                Frames(quality, .018f, 1200, false);
                Require(quality.Tier == HomeQualityTier.MEDIUM, "18 ms is not strictly below threshold");
                Frames(quality, .034f, 240, false);
                Frames(quality, .020f, 120, false);
                Frames(quality, .034f, 240, false);
                Require(quality.Tier == HomeQualityTier.MEDIUM, "neutral window resets consecutive slow streak");
                Frames(quality, .033333f, 120, false);
                Require(quality.Tier == HomeQualityTier.MEDIUM, "33.333 ms boundary is not slow");
                quality.SampleFrame(float.NaN, false);
                quality.SampleFrame(float.PositiveInfinity, false);
                quality.SampleFrame(-1f, false);
                Require(quality.Tier == HomeQualityTier.MEDIUM, "invalid samples ignored");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void TierIdentityAndLod()
        {
            var host = new GameObject("QualityStage");
            try
            {
                var stage = host.AddComponent<HomeFocusStage>();
                stage.Initialize();
                stage.SetViewportSize(new Vector2(600, 1000), 1f);
                var resolver = new HomeCharacterResolver(null, _ => null);
                Drain(Switch(stage, () => resolver.CreateBaby(7), HomeSubject.DINOSAUR));
                var quality = host.GetComponent<HomeStageQualityController>();
                quality.ApplyProjectedHeight(1f);
                var subject = stage.SubjectInstance;
                var body = subject.transform.Find("Body");
                var filter = body.GetComponent<MeshFilter>();
                var highMesh = filter.sharedMesh;
                var material = body.GetComponent<Renderer>().sharedMaterial;
                var color = material.color;
                var position = subject.transform.localPosition;
                var rotation = subject.transform.localRotation;
                var scale = body.localScale;
                var parts = subject.GetComponentsInChildren<Transform>(true);
                var poses = new Vector3[parts.Length];
                for (var part = 0; part < parts.Length; part++) poses[part] = parts[part].localScale;
                var tiers = new[] { HomeQualityTier.HIGH, HomeQualityTier.MEDIUM, HomeQualityTier.LOW };
                var widths = new[] { 600, 450, 360 };
                var heights = new[] { 1000, 750, 600 };
                for (var i = 0; i < 3; i++)
                {
                    quality.ApplyTier(tiers[i]);
                    Require(stage.Texture.width == widths[i] && stage.Texture.height == heights[i], "literal render scales 1/.75/.6");
                    Require(stage.SubjectInstance == subject && stage.ActiveSubject == HomeSubject.DINOSAUR, "tier never resolves another subject");
                    Require(subject.name.Contains("7") || subject.name.Contains("07"), "literal art 7 fallback identity");
                    Require(body.localScale.Equals(scale) && subject.transform.localPosition.Equals(position) && subject.transform.localRotation.Equals(rotation), "body proportion and authored root/front unchanged");
                    Require(body.GetComponent<Renderer>().sharedMaterial == material && material.color.Equals(color), "material and palette identical");
                    Require(subject.GetComponentsInChildren<Transform>(true).Length == parts.Length, "no species anatomy added/removed at any tier");
                    for (var part = 0; part < parts.Length; part++) Require(parts[part].localScale.Equals(poses[part]), "every authored anatomical proportion stays exact");
                    var key = stage.StageRoot.Find("WarmKey").GetComponent<Light>();
                    Require(key.shadows == (i == 2 ? LightShadows.None : LightShadows.Soft), "only LOW disables realtime key shadow");
                    Require(key.shadowCustomResolution == (i == 0 ? 1024 : 512), "medium key shadow resolution is half");
                    Require(stage.StageRoot.Find("Rim").GetComponent<Light>().enabled == (i != 2), "LOW disables rim");
                }
                Require(filter.sharedMesh != highMesh && filter.sharedMesh.triangles.Length < highMesh.triangles.Length, "procedural LOW uses deterministic lower-detail sphere branch");
                quality.ApplyTier(HomeQualityTier.HIGH);
                quality.ApplyProjectedHeight(1f);
                Require(filter.sharedMesh == highMesh, "high projected size restores original exact mesh");
                quality.ApplyProjectedHeight(.1f);
                Require(filter.sharedMesh != highMesh, "small projected size uses same procedural LOD transition");
                var effects = host.AddComponent<EggCrackVfx>();
                var egg = ProceduralEggFactory.Create();
                try
                {
                    effects.Bind(egg);
                    effects.SetFragmentBudget(24); effects.Burst();
                    Require(effects.ActiveFragments == 24, "HIGH emits exactly 24 pooled shell pieces");
                    effects.SetFragmentBudget(12);
                    Require(effects.ActiveFragments == 12, "mid-burst downshift sheds excess live shell pieces");
                    effects.Reset(); effects.Burst();
                    Require(effects.ActiveFragments == 12, "MEDIUM/LOW emit exactly 12");
                    effects.Reset();
                    Require(effects.ActiveFragments == 0 && effects.ActiveCrackGroups == 0, "pool reset leaves no transient presentation");
                }
                finally { UnityEngine.Object.DestroyImmediate(egg); }
                ImportedLod(stage, quality);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void ImportedLod(HomeFocusStage stage, HomeStageQualityController quality)
        {
            var root = new GameObject("AcceptedIdentity", typeof(Animator));
            var extraAnimator = new GameObject("ExtraAnimator", typeof(Animator));
            extraAnimator.transform.SetParent(root.transform, false);
            var high = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var low = GameObject.CreatePrimitive(PrimitiveType.Cube);
            high.transform.SetParent(root.transform, false); low.transform.SetParent(root.transform, false);
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 1, 2, 0, 2, 3, 0, 1, 2, 0, 2, 3 }; // 6 / 12 = 50%.
            mesh.RecalculateBounds();
            low.GetComponent<MeshFilter>().sharedMesh = mesh;
            var highMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.red };
            var lowMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.green };
            high.GetComponent<Renderer>().sharedMaterial = highMaterial;
            low.GetComponent<Renderer>().sharedMaterial = lowMaterial;
            high.transform.localPosition = Vector3.left * .75f;
            low.transform.localPosition = Vector3.right * .75f;
            var group = root.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(.6f, new[] { high.GetComponent<Renderer>() }), new LOD(.2f, new[] { low.GetComponent<Renderer>() }) });
            group.RecalculateBounds();
            var controller = new AnimatorController();
            controller.AddLayer("Base");
            controller.layers[0].stateMachine.AddState("Idle");
            controller.AddParameter("IdentityPose", AnimatorControllerParameterType.Float);
            try
            {
                Drain(Switch(stage, () => root, HomeSubject.AVATAR));
                var animator = root.GetComponent<Animator>();
                Require(animator.isActiveAndEnabled && !extraAnimator.GetComponent<Animator>().isActiveAndEnabled, "candidate admission enforces at most one active subject Animator");
                animator.runtimeAnimatorController = controller;
                animator.SetFloat("IdentityPose", .37f);
                animator.Play("Idle", 0, .37f);
                animator.Update(0f);
                var state = animator.GetCurrentAnimatorStateInfo(0);
                var speed = animator.speed = .42f;
                quality.ApplyTier(HomeQualityTier.LOW);
                Require(quality.ActiveLod == 1, "validated 50% imported LOD1 selected at LOW");
                CaptureLodPixels(root, false);
                Frames(quality, .02f, 120, false);
                Require(quality.Snapshot.activeRenderers == 1 && quality.Snapshot.activeAnimators == 1, "diagnostics count selected LOD only and exactly one active Animator");
                Require(stage.SubjectInstance == root && animator.enabled && animator.speed == speed, "LOD leaves exact Animator instance/state intact");
                Require(animator.runtimeAnimatorController == controller && animator.GetFloat("IdentityPose") == .37f, "tier preserves literal animation parameter and controller");
                Require(animator.GetCurrentAnimatorStateInfo(0).fullPathHash == state.fullPathHash && animator.GetCurrentAnimatorStateInfo(0).normalizedTime == state.normalizedTime, "LOD cannot reset Animator state/time");
                quality.ApplyTier(HomeQualityTier.HIGH); quality.ApplyProjectedHeight(.6f);
                Require(quality.ActiveLod == 0, "exact transition stays LOD0");
                CaptureLodPixels(root, true);
                quality.ApplyProjectedHeight(.59f);
                Require(quality.ActiveLod == 1, "below configured .6 uses LOD1");
                quality.ApplyProjectedHeight(1f);
                Require(quality.ActiveLod == 0, "large projected size selects original imported LOD0");
                mesh.triangles = new[] { 0, 1, 2 };
                quality.BindSubject(root);
                quality.ApplyTier(HomeQualityTier.LOW);
                Require(quality.ActiveLod == 0, "unvalidated 8% asset cannot become LOD1");
            }
            finally
            {
                stage.DestroyActiveSubject();
                UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(controller);
                UnityEngine.Object.DestroyImmediate(highMaterial); UnityEngine.Object.DestroyImmediate(lowMaterial);
            }
        }

        private static void CaptureLodPixels(GameObject subject, bool high)
        {
            var cameraObject = new GameObject("LODProof", typeof(Camera));
            var target = new RenderTexture(64, 32, 24);
            var pixels = new Texture2D(64, 32, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            var stageRenderers = subject.transform.root.GetComponentsInChildren<Renderer>(true);
            var rendererStates = new bool[stageRenderers.Length];
            try
            {
                // The proof camera shares the HomeStage layer with its studio backdrop. Isolate the
                // admitted subject so studio geometry cannot affect the LOD color classification.
                for (var i = 0; i < stageRenderers.Length; i++)
                {
                    rendererStates[i] = stageRenderers[i].enabled;
                    if (!stageRenderers[i].transform.IsChildOf(subject.transform)) stageRenderers[i].enabled = false;
                }
                target.Create();
                var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
                camera.targetTexture = target;
                // Keep this offscreen proof independent of the AR/XR eye layout.
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.orthographic = true; camera.orthographicSize = 1f; camera.aspect = 2f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << subject.layer;
                camera.transform.position = subject.transform.position + Vector3.back * 5f;
                camera.transform.rotation = Quaternion.identity;
                camera.ResetProjectionMatrix();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 64, 32), 0, 0); pixels.Apply();
                var highPixels = 0; var lowPixels = 0; var maxRed = 0f; var maxGreen = 0f;
                for (var y = 0; y < 32; y++)
                    for (var x = 0; x < 64; x++)
                    {
                        var color = pixels.GetPixel(x, y);
                        if (color.r > .5f && color.g < .25f) highPixels++;
                        if (color.g > .5f && color.r < .25f) lowPixels++;
                        maxRed = Mathf.Max(maxRed, color.r); maxGreen = Mathf.Max(maxGreen, color.g);
                    }
                var group = subject.GetComponent<LODGroup>();
                var lods = group.GetLODs();
                var highRenderer = lods[0].renderers[0]; var lowRenderer = lods[1].renderers[0];
                var highScreen = camera.WorldToScreenPoint(highRenderer.bounds.center);
                var lowScreen = camera.WorldToScreenPoint(lowRenderer.bounds.center);
                Require(highPixels > 0 == high && lowPixels > 0 == !high,
                    $"actual rendered exclusive LOD colors: expectedHigh={high}, colored={highPixels}/{lowPixels}, " +
                    $"max={maxRed:F3}/{maxGreen:F3}, " +
                    $"centers={highScreen.x:F2},{highScreen.y:F2}/{lowScreen.x:F2},{lowScreen.y:F2}, " +
                    $"subjectActive={subject.activeInHierarchy}, groupActive={group.enabled && group.gameObject.activeInHierarchy}, " +
                    $"renderers={highRenderer.enabled}:{highRenderer.gameObject.activeInHierarchy}/" +
                    $"{lowRenderer.enabled}:{lowRenderer.gameObject.activeInHierarchy}; deleting ForceLOD must fail, not just a selected-index assertion");
            }
            finally
            {
                for (var i = 0; i < stageRenderers.Length; i++)
                    if (stageRenderers[i] != null) stageRenderers[i].enabled = rendererStates[i];
                RenderTexture.active = previous; target.Release();
                UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static void MemoryRecovery()
        {
            var host = new GameObject("MemoryStage");
            try
            {
                var stage = host.AddComponent<HomeFocusStage>(); stage.Initialize();
                Drain(Switch(stage, () => GameObject.CreatePrimitive(PrimitiveType.Cube), HomeSubject.EGG));
                var identity = stage.SubjectInstance;
                var quality = host.GetComponent<HomeStageQualityController>();
                stage.SetViewportSize(new Vector2(600, 1000), 1f);
                quality.HandleLowMemory();
                Require(quality.Tier == HomeQualityTier.LOW && stage.SubjectInstance == identity && identity.activeInHierarchy, "no pending switch sheds resources without changing identity");
                Require(stage.Texture == null && !stage.InputLocked, "low memory releases RT and restores navigation");
                var attempts = 0;
                Drain(Switch(stage, () =>
                {
                    attempts++;
                    if (attempts == 1) { quality.HandleLowMemory(); throw new OutOfMemoryException("first allocation"); }
                    return GameObject.CreatePrimitive(PrimitiveType.Sphere);
                }, HomeSubject.DINOSAUR));
                Require(attempts == 2 && stage.ActiveSubject == HomeSubject.DINOSAUR && !stage.InputLocked, "pending factory retried once at LOW");
                identity = stage.SubjectInstance;
                attempts = 0;
                var notices = 0;
                stage.RetryNotice += text => { Require(!string.IsNullOrEmpty(text), "visible retry text"); notices++; };
                Drain(Switch(stage, () => { attempts++; quality.HandleLowMemory(); throw new OutOfMemoryException("still exhausted"); }, HomeSubject.AVATAR));
                Require(attempts == 2 && notices == 1 && stage.SubjectInstance == identity && !stage.InputLocked, "second failure keeps original identity, restores nav and notifies once");
                quality.HandleLowMemory();
                Require(attempts == 2 && notices == 1, "later callback cannot loop stale pending factory");
                SuspendedMemoryRecovery(stage, quality);
                var pending = Switch(stage, () => GameObject.CreatePrimitive(PrimitiveType.Cube), HomeSubject.EGG);
                Require(pending.MoveNext(), "prepared pending candidate");
                stage.DestroyActiveSubject();
                Require(!pending.MoveNext() && stage.SubjectInstance == null, "stale suspended switch cannot publish after cleanup");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void SuspendedMemoryRecovery(HomeFocusStage stage, HomeStageQualityController quality)
        {
            var previous = stage.SubjectInstance;
            var attempts = 0;
            GameObject first = null;
            IEnumerator recovery = null;
            Set(stage, "recoveryScheduler", new Action<IEnumerator>(iterator =>
            {
                recovery = iterator;
                Require(recovery.MoveNext(), "replacement recovery prepares synchronously like StartCoroutine");
            }));
            var pending = Switch(stage, () =>
            {
                attempts++;
                var candidate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                candidate.name = "PersistedArt19";
                candidate.AddComponent<Animator>();
                if (attempts == 1) first = candidate;
                return candidate;
            }, HomeSubject.DINOSAUR);
            Require(pending.MoveNext() && attempts == 1, "factory prepares before fade suspension");
            quality.HandleLowMemory();
            Require(first == null && attempts == 2 && stage.SubjectInstance == previous, "callback releases inactive prepared candidate and retries same factory once");
            Require(!pending.MoveNext(), "old suspended transaction cannot publish retry candidate");
            Drain(recovery);
            Require(stage.SubjectInstance.name == "PersistedArt19" && !stage.InputLocked, "same pending identity publishes once");
            Require(stage.StageRoot.GetComponentsInChildren<Animator>().Length == 1, "no outgoing/pending Animator survives recovery");
            previous = stage.SubjectInstance;
            attempts = 0;
            pending = Switch(stage, () => { attempts++; return GameObject.CreatePrimitive(PrimitiveType.Sphere); }, HomeSubject.AVATAR);
            Require(pending.MoveNext(), "second suspended recovery fixture prepared");
            quality.HandleLowMemory();
            quality.HandleLowMemory();
            Require(attempts == 2 && stage.SubjectInstance == previous && !stage.InputLocked, "second pressure during retry fade abandons retry without another factory call");
            Require(!pending.MoveNext() && !recovery.MoveNext(), "neither stale suspended owner can resurrect an abandoned identity");
            Set(stage, "recoveryScheduler", null);

            var directorObject = new GameObject("MemoryHatch", typeof(HatchSequenceDirector));
            var overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            var silhouette = new GameObject("Silhouette", typeof(RectTransform), typeof(RawImage));
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RawImage));
            try
            {
                stage.BindViewport(viewport.GetComponent<RawImage>());
                var director = directorObject.GetComponent<HatchSequenceDirector>();
                director.Configure(stage, overlay.GetComponent<Image>(), viewport.GetComponent<HomeStageOrbitInput>(), silhouette.GetComponent<RawImage>());
                Require((int)Get(director, "shellBudget") == 12, "late sequence binding inherits existing LOW cap");
                var failures = 0; var commits = 0;
                director.Failed += () => failures++;
                director.Play(HatchCheckpoint.STARTED, _ => { commits++; return true; }, () => { commits++; return true; }, () => { commits++; return true; });
                var effectsObject = new GameObject("MemoryEffects");
                effectsObject.transform.SetParent(stage.StageRoot, false);
                var effects = effectsObject.AddComponent<EggCrackVfx>(); effects.Bind(stage.SubjectInstance); effects.Burst(); effects.SetStage(3);
                Set(director, "effects", effects);
                var orbit = viewport.GetComponent<HomeStageOrbitInput>();
                Require(director.IsPlaying && !orbit.enabled, "first-hatch ownership fixture holds external orbit lock");
                Drain(Switch(stage, () => throw new OutOfMemoryException("hatch factory retry failure"), HomeSubject.AVATAR));
                Require(failures == 1 && director.NeedsRetry && !director.IsPlaying && commits == 0, "second model failure uses existing hatch retry without phase/checkpoint writes");
                Require(director.IsHatchPresentationLocked, "failed/paused first hatch remains in no-upshift policy until retry completes");
                Require(!stage.InputLocked && !orbit.enabled, "stage restores own lock but never overrides first-hatch external lock");
                Require(effects.ActiveFragments == 0 && effects.ActiveCrackGroups == 0, "memory callback returns connected crack/shell VFX to their pools");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(directorObject); UnityEngine.Object.DestroyImmediate(overlay);
                UnityEngine.Object.DestroyImmediate(silhouette); UnityEngine.Object.DestroyImmediate(viewport);
            }
        }

        // Mutation: moving cache admission after publication must lose the previous visible subject
        // or escape the bounded retry. Inject the allocating component query, not the factory.
        private static void AdmissionAllocationRecovery()
        {
            var host = new GameObject("AdmissionAllocationStage");
            try
            {
                var stage = host.AddComponent<HomeFocusStage>(); stage.Initialize();
                Drain(Switch(stage, () => GameObject.CreatePrimitive(PrimitiveType.Cube), HomeSubject.EGG));
                var quality = host.GetComponent<HomeStageQualityController>();
                var query = (Func<GameObject, LODGroup[]>)Get(quality, "collectLodGroups");
                var original = stage.SubjectInstance;
                var calls = 0; var notices = 0;
                var candidates = new GameObject[2];
                stage.RetryNotice += text => { Require(!string.IsNullOrEmpty(text), "admission failure has visible retry text"); notices++; };
                Set(quality, "collectLodGroups", new Func<GameObject, LODGroup[]>(subject =>
                {
                    var groups = query(subject);
                    if (++calls == 1) throw new OutOfMemoryException("admission allocation");
                    return groups;
                }));
                var attempts = 0;
                Drain(Switch(stage, () => candidates[attempts++] = GameObject.CreatePrimitive(PrimitiveType.Sphere), HomeSubject.DINOSAUR));
                Require(attempts == 2 && calls == 2 && candidates[0] == null, "admission allocation retries exact factory once and releases failed candidate");
                Require(stage.SubjectInstance == candidates[1] && original == null && stage.SubjectInstance.activeInHierarchy,
                    "only successfully admitted candidate replaces previous visible identity");
                Require(!stage.InputLocked && stage.CoverAlpha == 0f && notices == 0, "successful admission retry restores stage without notice");

                original = stage.SubjectInstance;
                calls = attempts = 0;
                Set(quality, "collectLodGroups", new Func<GameObject, LODGroup[]>(subject =>
                {
                    query(subject); calls++;
                    throw new OutOfMemoryException("admission still exhausted");
                }));
                Drain(Switch(stage, () => candidates[attempts++] = GameObject.CreatePrimitive(PrimitiveType.Cube), HomeSubject.AVATAR));
                Require(attempts == 2 && calls == 2 && candidates[0] == null && candidates[1] == null, "second admission failure releases both candidates and stops retrying");
                Require(stage.SubjectInstance == original && original.activeInHierarchy && stage.ActiveSubject == HomeSubject.DINOSAUR,
                    "failed admission never releases or replaces previous identity");
                Require(!stage.InputLocked && stage.CoverAlpha == 0f && notices == 1, "exhausted admission restores input and one visible notice");
                Frames(quality, .02f, 120, false);
                Require(quality.Snapshot.activeRenderers == 1, "failed admission preserves previous subject diagnostic cache");
                quality.HandleLowMemory();
                Require(attempts == 2 && notices == 1, "later pressure cannot resurrect exhausted admission factory");
                Set(quality, "collectLodGroups", query);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        // Mutation: calling Reset during pressure restores shell renderers after the durable burst.
        private static void PressureBetweenBurstAndWarm()
        {
            var host = new GameObject("BurstPressureStage");
            var directorObject = new GameObject("BurstPressureDirector", typeof(HatchSequenceDirector));
            var overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            var silhouette = new GameObject("Silhouette", typeof(RectTransform), typeof(RawImage));
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RawImage));
            try
            {
                var stage = host.AddComponent<HomeFocusStage>(); stage.Initialize();
                stage.BindViewport(viewport.GetComponent<RawImage>());
                stage.SetViewportSize(new Vector2(600, 1000), 1f);
                Drain(Switch(stage, ProceduralEggFactory.Create, HomeSubject.EGG));
                var egg = stage.SubjectInstance;
                var shell = egg.GetComponentsInChildren<Renderer>(true);
                var director = directorObject.GetComponent<HatchSequenceDirector>();
                var orbit = viewport.GetComponent<HomeStageOrbitInput>();
                director.Configure(stage, overlay.GetComponent<Image>(), orbit, silhouette.GetComponent<RawImage>());
                var bursts = 0; var reveals = 0; var homes = 0;
                director.Play(HatchCheckpoint.STARTED, checkpoint => { Require(checkpoint == HatchCheckpoint.SHELL_BURST, "only burst checkpoint"); bursts++; return true; },
                    () => { reveals++; return true; }, () => { homes++; return true; });
                director.Advance(2f); // Strictly between the literal 1.8 s burst and 2.4 s warm boundary.
                var effects = (EggCrackVfx)Get(director, "effects");
                Require(effects.ActiveFragments == 24 && director.CoverAlpha > 0f && director.CoverAlpha < 1f, "pressure fixture has a live burst behind a translucent cover");
                host.GetComponent<HomeStageQualityController>().HandleLowMemory();
                Require(effects.ActiveFragments == 0 && effects.ActiveCrackGroups == 0 && stage.Texture == null, "pressure recycles transient pool and render target");
                foreach (var renderer in shell) Require(!renderer.enabled, "pressure cannot resurrect the already-burst shell");
                stage.SetViewportSize(new Vector2(600, 1000), 1f);
                Require(stage.Texture != null && stage.Texture.IsCreated() && stage.SubjectInstance == egg, "target recovery preserves exact egg instance");
                director.Advance(.1f);
                foreach (var renderer in shell) Require(!renderer.enabled, "next timeline frame cannot reveal an intact shell after target recovery");
                Require(director.IsPlaying && !director.NeedsRetry && director.Elapsed == 2.1f && !orbit.enabled,
                    "pressure preserves timeline and external hatch navigation lock");
                Require(bursts == 1 && reveals == 0 && homes == 0, "pressure does not repeat burst or write a later durable boundary");
                effects.Reset();
                Require(shell[0].enabled, "explicit full reset still restores shell visibility for replay");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(directorObject); UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(overlay); UnityEngine.Object.DestroyImmediate(silhouette); UnityEngine.Object.DestroyImmediate(viewport);
            }
        }

        private static void Diagnostics()
        {
            var host = new GameObject("Diagnostics");
            try
            {
                var quality = host.AddComponent<HomeStageQualityController>(); quality.Initialize(null);
                Require(quality.DiagnosticWarnings(30f, 350L * 1024 * 1024, false, 1) == 0, "literal home memory/fps limits inclusive");
                Require(quality.DiagnosticWarnings(29f, 350L * 1024 * 1024 + 1, false, 2) == 11, "first home memory/fps/Animator violations warn");
                Require(quality.DiagnosticWarnings(20f, 500L * 1024 * 1024, false, 3) == 0, "same violations never log per-frame");
                Require(quality.DiagnosticWarnings(30f, 450L * 1024 * 1024, true, 1) == 0, "hatch ceiling inclusive");
                Require(quality.DiagnosticWarnings(30f, 450L * 1024 * 1024 + 1, true, 1) == 4, "hatch memory warning independent and one-shot");
                Invoke(quality, "OnEnable"); Invoke(quality, "OnEnable");
                Require((bool)Get(quality, "subscribed"), "subscribe-once guard held");
                Invoke(quality, "OnDisable"); Invoke(quality, "OnDisable");
                Require(!(bool)Get(quality, "subscribed"), "idempotent unsubscribe");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void ContextLoss()
        {
            var host = new GameObject("QualityContext");
            var view = new GameObject("View", typeof(RectTransform), typeof(RawImage));
            try
            {
                var stage = host.AddComponent<HomeFocusStage>(); stage.Initialize(); stage.BindViewport(view.GetComponent<RawImage>());
                stage.SetViewportSize(new Vector2(2000, 4000), 1f);
                Drain(Switch(stage, () => GameObject.CreatePrimitive(PrimitiveType.Cube), HomeSubject.EGG));
                var camera = stage.StageRoot.GetComponentInChildren<Camera>();
                Require(camera.enabled && camera.targetTexture == stage.Texture && stage.SubjectInstance.activeInHierarchy, "context fixture starts with a real active rendered subject and enabled camera");
                camera.Render();
                Require(stage.Texture.width == 540 && stage.Texture.height == 1080, "1080 target cap");
                var quality = host.GetComponent<HomeStageQualityController>(); quality.ApplyTier(HomeQualityTier.LOW);
                Require(stage.Texture.width == 324 && stage.Texture.height == 648, "quality scales capped reference dimensions");
                var lost = stage.Texture; lost.Release();
                Set(stage, "createRenderTexture", new Func<RenderTexture, bool>(_ => false));
                stage.SetViewportSize(new Vector2(2000, 4000), 1f);
                Require(stage.Texture == lost && !camera.enabled, "failed context recovery does not render through lost native target");
                Drain(Switch(stage, () => GameObject.CreatePrimitive(PrimitiveType.Sphere), HomeSubject.AVATAR));
                Require(stage.ActiveSubject == HomeSubject.AVATAR && !stage.InputLocked && !camera.enabled, "completing a subject switch cannot reenable camera against lost native target");
                Require(stage.Texture == lost && !lost.IsCreated() && camera.targetTexture == lost && view.GetComponent<RawImage>().texture == lost,
                    "switch keeps lost bindings inert and never routes stage camera to framebuffer");
                Set(stage, "createRenderTexture", new Func<RenderTexture, bool>(texture => texture.Create()));
                stage.SetViewportSize(new Vector2(2000, 4000), 1f);
                Require(stage.Texture != lost && stage.Texture.IsCreated() && stage.Texture.height == 648, "lost native target recreates at retained LOW tier");
                Require(view.GetComponent<RawImage>().texture == stage.Texture, "context recovery rebinds viewport");
                Require(camera.enabled && camera.targetTexture == stage.Texture && stage.SubjectInstance.activeInHierarchy, "successful context recreation reenables the active subject camera");
                Drain(Switch(stage, () => GameObject.CreatePrimitive(PrimitiveType.Cube), HomeSubject.EGG));
                Require(camera.enabled && stage.Texture.IsCreated() && camera.targetTexture == stage.Texture && stage.ActiveSubject == HomeSubject.EGG,
                    "ordinary switch with valid native target stays rendered");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(view); }
        }

        private static void BurstWindowMemoryPressure()
        {
            var host = new GameObject("BurstPressureStage");
            var directorObject = new GameObject("BurstPressureSequence", typeof(HatchSequenceDirector));
            var viewport = new GameObject("BurstViewport", typeof(RectTransform), typeof(RawImage));
            var overlay = new GameObject("BurstOverlay", typeof(RectTransform), typeof(Image));
            var silhouette = new GameObject("BurstSilhouette", typeof(RectTransform), typeof(RawImage));
            try
            {
                var stage = host.AddComponent<HomeFocusStage>(); stage.Initialize(); stage.BindViewport(viewport.GetComponent<RawImage>());
                stage.SetViewportSize(new Vector2(600, 1000), 1f);
                Drain(Switch(stage, ProceduralEggFactory.Create, HomeSubject.EGG));
                var egg = stage.SubjectInstance;
                var shell = egg.GetComponentsInChildren<Renderer>(true);
                var originalVisibility = new bool[shell.Length];
                for (var i = 0; i < shell.Length; i++) originalVisibility[i] = shell[i].enabled;
                var director = directorObject.GetComponent<HatchSequenceDirector>();
                director.Configure(stage, overlay.GetComponent<Image>(), viewport.GetComponent<HomeStageOrbitInput>(), silhouette.GetComponent<RawImage>());
                director.Settings = new HatchPresentationSettings { Particles = true, Audio = false, Haptics = false, CameraImpulse = false };
                director.SelectedArtId = 19;
                var checkpoint = HatchCheckpoint.STARTED;
                var commits = 0;
                director.Play(checkpoint, value => { checkpoint = value; commits++; return true; },
                    () => throw new InvalidOperationException("burst window must not commit reveal"),
                    () => throw new InvalidOperationException("burst window must not commit home"));
                director.Advance(1.8f);
                var effects = (EggCrackVfx)Get(director, "effects");
                Require(checkpoint == HatchCheckpoint.SHELL_BURST && commits == 1 && effects.ActiveFragments == 24,
                    "real timeline persists burst once and emits 24 pieces at 1.8 seconds");
                for (var i = 0; i < shell.Length; i++) Require(!shell[i].enabled, "burst hides intact egg before memory pressure");
                var quality = host.GetComponent<HomeStageQualityController>();
                quality.HandleLowMemory();
                Require(stage.Texture == null && effects.ActiveFragments == 0 && effects.ActiveCrackGroups == 0,
                    "burst-window callback releases native target and returns crack/shell pool");
                Require(director.SelectedArtId == 19 && checkpoint == HatchCheckpoint.SHELL_BURST && commits == 1 && director.Elapsed == 1.8f,
                    "memory shedding cannot alter selected ID, persisted checkpoint or timeline");
                for (var i = 0; i < shell.Length; i++) Require(!shell[i].enabled, "memory shedding must not reconstruct intact egg");
                stage.SetViewportSize(new Vector2(600, 1000), 1f);
                var camera = stage.StageRoot.GetComponentInChildren<Camera>();
                Require(camera.enabled && stage.Texture.IsCreated() && stage.Texture.height == 600 && viewport.GetComponent<RawImage>().texture == stage.Texture,
                    "burst viewport recovers at LOW with real native target");
                camera.Render();
                director.Advance(.3f);
                director.Advance(.2f);
                Require(director.Elapsed > 2.29f && director.Elapsed < 2.4f && director.IsPlaying && stage.SubjectInstance == egg,
                    "fixture remains in the persisted 1.8–2.4 second burst window after viewport recovery");
                Require(effects.ActiveFragments == 0 && commits == 1 && checkpoint == HatchCheckpoint.SHELL_BURST && director.SelectedArtId == 19,
                    "recovered burst window neither emits another burst nor recommits/rerolls");
                for (var i = 0; i < shell.Length; i++) Require(!shell[i].enabled, "intact shell stays hidden for rest of burst window");
                effects.Reset();
                for (var i = 0; i < shell.Length; i++) Require(shell[i].enabled == originalVisibility[i], "normal new-sequence Reset still restores original shell presentation");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(directorObject); UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(viewport); UnityEngine.Object.DestroyImmediate(overlay); UnityEngine.Object.DestroyImmediate(silhouette);
            }
        }

        private static void SteadyLoopAllocation()
        {
            var host = new GameObject("SteadyQualityAllocation");
            try
            {
                var stage = host.AddComponent<HomeFocusStage>(); stage.Initialize();
                Drain(Switch(stage, () => GameObject.CreatePrimitive(PrimitiveType.Cube), HomeSubject.EGG));
                var quality = host.GetComponent<HomeStageQualityController>();
                // Warm profiler/caches/JIT before measuring the complete steady sampling path.
                Frames(quality, .02f, 240, false);
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 1200; i++) { quality.SampleFrame(.02f, false); quality.ApplyProjectedHeight(.7f); }
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Require(allocated == 0L, "literal zero managed allocations across 1200 steady policy/detail frames");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void Frames(HomeStageQualityController quality, float seconds, int count, bool hatch)
        { for (var i = 0; i < count; i++) quality.SampleFrame(seconds, hatch); }
        private static IEnumerator Switch(HomeFocusStage stage, Func<GameObject> factory, HomeSubject subject) =>
            (IEnumerator)typeof(HomeFocusStage).GetMethod("SwitchSubject", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(stage, new object[] { factory, subject });
        private static void Drain(IEnumerator iterator) { while (iterator.MoveNext()) { } }
        private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
