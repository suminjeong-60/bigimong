#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bigimong.AR.EditorChecks
{
    public static class HomeFocusStageEditorChecks
    {
        public static void PrepareLayer()
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = settings.FindProperty("layers");
            if (LayerMask.NameToLayer("HomeStage") >= 0) return;
            for (var i = 8; i < 32; i++)
            {
                if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) continue;
                layers.GetArrayElementAtIndex(i).stringValue = "HomeStage";
                settings.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
            throw new InvalidOperationException("No free HomeStage layer");
        }

        public static void RunBehaviorChecks()
        {
            PrepareLayer();
            GestureBoundaries();
            PointerOwnershipAndOrbit();
            SubjectTransactions();
            ProceduralActorCannotCollapseCandidate();
            ResolutionAndCleanup();
            AllocationFailurePreservesTarget();
            DisableTransactionRestoresAfterRuntimeLifecycle();
            MotionRestoresAuthoredPose();
        }

        private static void GestureBoundaries()
        {
            Require(HomeGestureClassifier.Classify(.22f, 17.99f, 160f) == HomeGesture.TAP, "inclusive time, exclusive distance tap boundary");
            Require(HomeGestureClassifier.Classify(.221f, 0f, 160f) == HomeGesture.DRAG, "long press is never care");
            Require(HomeGestureClassifier.Classify(.1f, 18f, 160f) == HomeGesture.DRAG, "exactly 18dp cancels tap");
            Require(HomeGestureClassifier.Classify(.1f, 35.99f, 320f) == HomeGesture.TAP, "density conversion below 18dp");
            Require(HomeGestureClassifier.Classify(.1f, 36f, 320f) == HomeGesture.DRAG, "density conversion at 18dp");
            Require(HomeGestureClassifier.Classify(.1f, 18f, 0f) == HomeGesture.DRAG, "unknown density uses 160dpi");
            var gesture = new HomeGestureClassifier();
            gesture.Begin(Vector2.zero, 0f, 160f);
            gesture.Move(new Vector2(10f, 0f));
            gesture.Move(Vector2.zero);
            Require(gesture.End(Vector2.zero, .1f) == HomeGesture.DRAG, "total travel remains sticky after returning to origin");
        }

        private static void PointerOwnershipAndOrbit()
        {
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RawImage), typeof(HomeStageOrbitInput));
            var other = new GameObject("Button", typeof(RectTransform), typeof(Button));
            var pivot = new GameObject("Yaw");
            var events = new GameObject("Events", typeof(EventSystem));
            try
            {
                var orbit = viewport.GetComponent<HomeStageOrbitInput>();
                orbit.Bind(pivot.transform, 37f);
                var taps = 0;
                orbit.Tapped += () => taps++;
                var data = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 1, position = Vector2.zero, button = PointerEventData.InputButton.Left };
                data.pointerCurrentRaycast = new RaycastResult { gameObject = other };
                data.pointerPressRaycast = data.pointerCurrentRaycast;
                orbit.OnPointerDown(data);
                data.position = new Vector2(500f, 0f);
                orbit.OnDrag(data);
                orbit.OnPointerUp(data);
                Require(taps == 0 && orbit.Yaw == 37f, "UI-origin press cannot care or rotate");
                data.position = Vector2.zero;
                data.pointerCurrentRaycast = new RaycastResult { gameObject = viewport };
                data.pointerPressRaycast = data.pointerCurrentRaycast;
                orbit.OnPointerDown(data);
                orbit.OnPointerUp(data);
                Require(taps == 1, "owned short stationary press emits exactly one tap");
                data.useDragThreshold = true;
                orbit.OnInitializePotentialDrag(data);
                Require(!data.useDragThreshold, "stage, not EventSystem's pixel threshold, owns drag classification");
                orbit.OnPointerDown(data);
                var pixelsPerDp = Screen.dpi > 0 ? Screen.dpi / 160f : 1f;
                data.position = new Vector2(20f * pixelsPerDp, 0f);
                orbit.OnDrag(data);
                Require(Mathf.Abs(orbit.Yaw - 46f) < .0001f, "20dp horizontal drag produces 9 degrees of visible yaw");
                data.position = Vector2.zero;
                orbit.OnDrag(data);
                orbit.OnPointerUp(data);
                Require(taps == 1, "return-to-origin drag never emits care");
                orbit.Bind(pivot.transform, 37f);
                orbit.AddYaw(720f);
                Require(orbit.Yaw == 757f, "two complete rotations are accumulated, never clamped");
                orbit.ResetFront();
                orbit.Advance(.5f);
                Require(orbit.Yaw == 37f, "reset reaches literal authored front exactly");
                // The same input velocity integrates identically at different frame rates.
                Set(orbit, "velocity", 100f);
                Set(orbit, "inertiaRemaining", .35f);
                orbit.Advance(.35f);
                var oneStep = orbit.Yaw;
                Require(Mathf.Abs(oneStep - 48.739874f) < .0001f, "analytic damping integral over .35 seconds");
                orbit.Advance(2f);
                Require(orbit.Yaw == oneStep, "inertia stops by .35 seconds");
                orbit.Bind(pivot.transform, 37f);
                Set(orbit, "velocity", 100f);
                Set(orbit, "inertiaRemaining", .35f);
                for (var i = 0; i < 35; i++) orbit.Advance(.01f);
                Require(Mathf.Abs(orbit.Yaw - oneStep) < .0002f, "damping must not vary with FPS");
                orbit.OnPointerDown(data);
                var foreign = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 2, position = new Vector2(400, 0) };
                orbit.OnDrag(foreign);
                orbit.OnPointerUp(foreign);
                Require(Mathf.Abs(orbit.Yaw - oneStep) < .0002f, "second pointer cannot take ownership");
                orbit.SetInputLocked(true);
                orbit.OnPointerUp(data);
                Require(taps == 1, "lock cancels an in-flight tap");
            }
            finally { UnityEngine.Object.DestroyImmediate(viewport); UnityEngine.Object.DestroyImmediate(other); UnityEngine.Object.DestroyImmediate(pivot); UnityEngine.Object.DestroyImmediate(events); }
        }

        private static void SubjectTransactions()
        {
            var host = new GameObject("Stage");
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            source.transform.localScale = new Vector3(2f, 3f, 4f);
            source.transform.localRotation = Quaternion.Euler(0f, 37f, 0f);
            source.AddComponent<Animator>();
            source.AddComponent<Camera>();
            source.AddComponent<Light>();
            var effects = new GameObject("Effects", typeof(ParticleSystem));
            effects.transform.SetParent(source.transform, false);
            try
            {
                var stage = host.AddComponent<HomeFocusStage>();
                stage.Initialize(new HomeCharacterResolver(null, _ => source));
                for (var i = 0; i < 30; i++)
                {
                    var old = stage.SubjectInstance;
                    var transaction = Switch(stage, () => UnityEngine.Object.Instantiate(source), HomeSubject.EGG);
                    Require(transaction.MoveNext(), "validated candidate pauses under cover before commit");
                    if (old != null) Require(old.activeInHierarchy, "old subject stays alive through validation");
                    Require(stage.StageRoot.GetComponentsInChildren<Animator>().Length == (i == 0 ? 0 : 1), "staged candidate never activates alongside outgoing Animator");
                    Require(stage.InputLocked, "switch locks stage input through cover");
                    Require(transaction.MoveNext(), "commit yields uncover");
                    if (old != null) Require(!old.activeInHierarchy, "outgoing model deactivated before deferred destruction");
                    while (transaction.MoveNext()) { }
                    Require(stage.SubjectInstance.transform.localScale == new Vector3(2f, 3f, 4f), "authored proportions unchanged");
                    Require(Quaternion.Angle(stage.SubjectInstance.transform.localRotation, Quaternion.Euler(0f, 37f, 0f)) < .0001f, "resolver authored heading preserved");
                    Require(stage.StageRoot.GetComponentsInChildren<Animator>().Length == 1, "exactly one active subject Animator after switch");
                    Require(stage.SubjectInstance.GetComponent<Camera>().enabled == false && stage.SubjectInstance.GetComponent<Light>().enabled == false, "prefab camera/light cannot escape isolated rig");
                    Require(stage.StageRoot.GetComponentsInChildren<Camera>().Length == 2 && stage.StageRoot.GetComponentsInChildren<Light>().Length == 4, "one studio rig plus current disabled asset camera/light; no outgoing components remain");
                    Require(stage.StageRoot.GetComponentsInChildren<ParticleSystem>().Length == 1, "no outgoing particle system remains");
                    Require(!stage.InputLocked, "switch unlocks after uncover");
                }
                var previous = stage.SubjectInstance;
                var invalid = new GameObject("Invalid");
                Drain(Switch(stage, () => invalid, HomeSubject.AVATAR));
                Require(stage.SubjectInstance == previous && previous.activeInHierarchy && !stage.InputLocked, "invalid model leaves previous subject and unlocks navigation");
                Drain(Switch(stage, () => throw new InvalidOperationException("fixture resolver failure"), HomeSubject.AVATAR));
                Require(stage.SubjectInstance == previous && stage.ActiveSubject == HomeSubject.EGG && !stage.InputLocked, "resolver failure cannot publish requested identity");
                var malformed = GameObject.CreatePrimitive(PrimitiveType.Cube);
                malformed.GetComponent<Renderer>().enabled = false;
                Drain(Switch(stage, () => malformed, HomeSubject.AVATAR));
                Require(stage.SubjectInstance == previous, "hidden-only geometry rejected before old subject is removed");
                var motion = stage.SubjectInstance.transform.parent.GetComponent<HomeSubjectMotion>();
                motion.Play(HomeReaction.AcceptedCare);
                motion.Advance(.15f);
                Drain(Switch(stage, () => throw new InvalidOperationException("interrupted resolver"), HomeSubject.AVATAR));
                Require(motion.transform.localPosition.Equals(Vector3.zero) && motion.transform.localRotation.Equals(Quaternion.identity), "even rejected switch cancels and exactly restores previous reaction");
                var pending = Switch(stage, () => UnityEngine.Object.Instantiate(source), HomeSubject.AVATAR);
                Require(pending.MoveNext(), "candidate prepared before interrupted cover");
                stage.DestroyActiveSubject();
                Require(stage.SubjectInstance == null && stage.StageRoot == null, "cleanup releases subject and stage rig");
            }
            finally { host.GetComponent<HomeFocusStage>()?.DestroyActiveSubject(); UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(source); }
        }

        private static void ResolutionAndCleanup()
        {
            var host = new GameObject("Stage");
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RawImage));
            try
            {
                var stage = host.AddComponent<HomeFocusStage>();
                stage.Initialize();
                stage.BindViewport(viewport.GetComponent<RawImage>());
                stage.SetViewportSize(new Vector2(1440, 2560), 1f);
                Require(stage.Texture.width == 608 && stage.Texture.height == 1080, "portrait caps longest edge at 1080");
                Require(viewport.GetComponent<RawImage>().texture == stage.Texture, "viewport displays current stage target");
                var first = stage.Texture;
                stage.SetViewportSize(new Vector2(1440, 2560), 1f);
                Require(stage.Texture == first, "same integer target never reallocates");
                stage.SetViewportSize(new Vector2(float.NaN, 100f), 1f);
                stage.SetViewportSize(Vector2.zero, 1f);
                Require(stage.Texture == first, "invalid transient layout keeps current render target");
                stage.SetViewportSize(new Vector2(720, 1280), .5f);
                Require(stage.Texture.width == 360 && stage.Texture.height == 640 && stage.Texture != first, "render scale allocates only changed target");
                first = stage.Texture;
                stage.SetViewportSize(new Vector2(720.1f, 1280.1f), .5f);
                Require(stage.Texture == first, "fractional layout changes rounding to same pixels do not churn");
                stage.SetViewportSize(new Vector2(2560, 1440), 1f);
                Require(stage.Texture.width == 1080 && stage.Texture.height == 608, "landscape cap");
                Drain(Switch(stage, () => GameObject.CreatePrimitive(PrimitiveType.Cube), HomeSubject.EGG));
                var orbit = viewport.GetComponent<HomeStageOrbitInput>();
                var motion = stage.SubjectInstance.transform.parent.GetComponent<HomeSubjectMotion>();
                motion.Play(HomeReaction.AcceptedCare);
                motion.Advance(.15f);
                orbit.AddYaw(180f);
                stage.ResetFront();
                Require(motion.transform.localPosition.Equals(Vector3.zero) && motion.transform.localRotation.Equals(Quaternion.identity), "front reset restores reaction before rotating");
                orbit.Advance(.25f);
                Require(orbit.Yaw == 0f, "stage reset returns yaw wrapper to authored model front");
                var camera = stage.StageRoot.GetComponentInChildren<Camera>();
                Require(camera.enabled && camera.targetTexture == stage.Texture && camera.cullingMask == 1 << LayerMask.NameToLayer("HomeStage"), "only isolated stage layer renders into target");
                var shadowMaterial = stage.StageRoot.Find("GroundShadow").GetComponent<Renderer>().sharedMaterial;
                var shadowTexture = shadowMaterial.mainTexture;
                host.SetActive(false);
                // Edit-mode MonoBehaviours do not receive runtime lifecycle messages automatically.
                InvokeLifecycle(stage, "OnDisable");
                Require(stage.Texture == null && stage.StageRoot == null, "disable releases RT and inactive camera/lights");
                Require(viewport.GetComponent<RawImage>().texture == null && shadowMaterial == null && shadowTexture == null, "owned runtime shadow resources and UI texture references released");
            }
            finally { host.GetComponent<HomeFocusStage>()?.DestroyActiveSubject(); UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(viewport); }
        }

        private static void AllocationFailurePreservesTarget()
        {
            var host = new GameObject("AllocationStage");
            var viewport = new GameObject("AllocationViewport", typeof(RectTransform), typeof(RawImage));
            try
            {
                var stage = host.AddComponent<HomeFocusStage>();
                stage.Initialize();
                var image = viewport.GetComponent<RawImage>();
                stage.BindViewport(image);
                stage.SetViewportSize(new Vector2(360, 640), 1f);
                var original = stage.Texture;
                Require(original != null && original.IsCreated() && original.width == 360 && original.height == 640, "valid original allocation fixture");
                var camera = stage.StageRoot.GetComponentInChildren<Camera>();
                var originalAspect = camera.aspect;
                RenderTexture failed = null;
                var allocations = 0;
                Set(stage, "createRenderTexture", new Func<RenderTexture, bool>(texture =>
                {
                    allocations++;
                    if (allocations == 1)
                    {
                        failed = texture;
                        Require(texture.width == 640 && texture.height == 360, "failure requested new landscape dimensions");
                        return false;
                    }
                    return texture.Create();
                }));
                stage.SetViewportSize(new Vector2(640, 360), 1f);
                Require(allocations == 1 && stage.Texture == original && original.IsCreated(), "allocation failure preserves previous valid GPU target");
                Require(original.width == 360 && original.height == 640, "allocation failure preserves original dimensions");
                Require(camera.targetTexture == original && image.texture == original && camera.aspect == originalAspect, "failure preserves camera and UI bindings and camera aspect");
                Require(!ReferenceEquals(failed, null) && failed == null, "failed native candidate is destroyed, not leaked or published");
                stage.SetViewportSize(new Vector2(640, 360), 1f);
                var recovered = stage.Texture;
                Require(allocations == 2 && recovered != null && recovered.IsCreated() && recovered.width == 640 && recovered.height == 360, "same requested size retries successfully after failure");
                Require(original == null && camera.targetTexture == recovered && image.texture == recovered, "only successful replacement releases old target and rebinds consumers");
                stage.SetViewportSize(new Vector2(640, 360), 1f);
                Require(allocations == 2 && stage.Texture == recovered, "created same-size target does not reallocate");
                recovered.Release(); // Simulate device/context loss without changing dimensions.
                Require(!recovered.IsCreated(), "context-loss fixture invalidates native target");
                stage.SetViewportSize(new Vector2(640, 360), 1f);
                Require(allocations == 3 && stage.Texture.IsCreated() && recovered == null, "same-size lost GPU target must be recreated");
                Require(stage.Texture.width == 640 && stage.Texture.height == 360 && camera.targetTexture == stage.Texture && image.texture == stage.Texture, "context recovery preserves dimensions and publishes new valid bindings");
            }
            finally { host.GetComponent<HomeFocusStage>()?.DestroyActiveSubject(); UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(viewport); }
        }

        private static void DisableTransactionRestoresAfterRuntimeLifecycle()
        {
            var candidate = new GameObject("RuntimeDisableFixture", typeof(ArBattleActor));
            try
            {
                var actor = candidate.GetComponent<ArBattleActor>();
                var node = candidate.transform;
                node.localPosition = new Vector3(3, 4, 5);
                node.localRotation = Quaternion.Euler(11, 22, 33);
                node.localScale = new Vector3(2, 3, 4);
                var authoredRotation = node.localRotation;
                var calls = 0;
                Action<ArBattleActor> disable = value =>
                {
                    value.enabled = false;
                    // Exactly where Play Mode invokes OnDisable, between capture and restoration.
                    InvokeLifecycle(value, "OnDisable");
                    calls++;
                    Require(node.localPosition == Vector3.zero && node.localScale == Vector3.zero, "real uninitialized AR lifecycle must corrupt pose before restoration; fixture cannot silently skip it");
                };
                typeof(HomeFocusStage).GetMethod("DisablePreservingAuthoredPose", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { actor, disable });
                Require(calls == 1 && !actor.enabled, "disable action runs exactly once and actor stays suppressed");
                Require(node.localPosition.Equals(new Vector3(3, 4, 5)), "removing position restoration leaves zero and fails");
                Require(node.localRotation.Equals(authoredRotation), "removing rotation restoration loses authored heading and fails");
                Require(node.localScale.Equals(new Vector3(2, 3, 4)), "removing scale restoration leaves zero and fails");
            }
            finally { UnityEngine.Object.DestroyImmediate(candidate); }
        }

        private static void ProceduralActorCannotCollapseCandidate()
        {
            var host = new GameObject("Stage");
            try
            {
                var stage = host.AddComponent<HomeFocusStage>();
                stage.Initialize(new HomeCharacterResolver(null, _ => null));
                var resolver = new HomeCharacterResolver(null, _ => null);
                Drain(Switch(stage, () => resolver.CreateBaby(7), HomeSubject.DINOSAUR));
                Require(stage.SubjectInstance != null && stage.ActiveSubject == HomeSubject.DINOSAUR, "actual fallback resolves into stage");
                var actor = stage.SubjectInstance.GetComponent<ArBattleActor>();
                Require(actor != null && !actor.enabled, "AR-only actor remains for material ownership but cannot run deforming home idle or reset pose on disable");
                Require(stage.SubjectInstance.transform.localScale == Vector3.one, "uninitialized AR actor must not collapse the stage model to zero scale");
                Require(stage.SubjectInstance.transform.Find("Body").localScale == new Vector3(.74f, .62f, .96f), "fallback geometry retains authored body shape");
            }
            finally { host.GetComponent<HomeFocusStage>()?.DestroyActiveSubject(); UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void MotionRestoresAuthoredPose()
        {
            var pivot = new GameObject("Presentation");
            var model = new GameObject("Model", typeof(Animator));
            model.transform.SetParent(pivot.transform, false);
            var bone = new GameObject("Bone").transform;
            bone.SetParent(model.transform, false);
            bone.localPosition = new Vector3(1, 2, 3);
            var controller = new AnimatorController();
            try
            {
                var animator = model.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                pivot.transform.localPosition = new Vector3(3, 4, 5);
                pivot.transform.localRotation = Quaternion.Euler(11, 22, 33);
                pivot.transform.localScale = new Vector3(2, 2, 2);
                var authoredRotation = pivot.transform.localRotation;
                var motion = pivot.AddComponent<HomeSubjectMotion>();
                motion.Bind(pivot.transform, animator);
                foreach (HomeReaction reaction in Enum.GetValues(typeof(HomeReaction)))
                    for (var i = 0; i < 50; i++)
                    {
                        motion.Play(reaction);
                        motion.Advance(.15f);
                        Require(pivot.transform.localPosition != new Vector3(3, 4, 5) || pivot.transform.localRotation != authoredRotation, "fallback applies a real additive reaction");
                        Require(pivot.transform.localScale == new Vector3(2, 2, 2), "reaction never changes proportions");
                        motion.Advance(2f);
                        Require(pivot.transform.localPosition.Equals(new Vector3(3, 4, 5)) && pivot.transform.localRotation.Equals(authoredRotation), "50 reactions restore exact captured pose without drift");
                        Require(bone.localPosition == new Vector3(1, 2, 3) && bone.localRotation == Quaternion.identity && bone.localScale == Vector3.one, "fallback never writes bones");
                        Require(animator.runtimeAnimatorController == controller, "controller identity is never replaced");
                    }
                motion.Play(HomeReaction.AcceptedCare);
                motion.Advance(.15f);
                motion.CancelAndRestore();
                Require(pivot.transform.localPosition.Equals(new Vector3(3, 4, 5)) && pivot.transform.localRotation.Equals(authoredRotation), "cancel before reset/switch restores exactly");
                motion.Play(HomeReaction.AcceptedCare);
                motion.Advance(.325f);
                Require(Mathf.Abs(pivot.transform.localPosition.y - 4.06f) < .00001f, "accepted care midpoint lifts exactly .06 units");
                var midpoint = pivot.transform.localPosition;
                motion.Play(HomeReaction.AcceptedCare);
                for (var i = 0; i < 13; i++) motion.Advance(.025f);
                Require(Vector3.Distance(pivot.transform.localPosition, midpoint) < .00001f, "reaction curve independent of frame subdivision");
                motion.Play(HomeReaction.AvatarWave);
                motion.Advance(.15f);
                motion.enabled = false;
                InvokeLifecycle(motion, "OnDisable");
                Require(pivot.transform.localPosition.Equals(new Vector3(3, 4, 5)) && pivot.transform.localRotation.Equals(authoredRotation), "disable restores pose");
                motion.enabled = true;
                controller.AddParameter("IdleReact", AnimatorControllerParameterType.Float);
                motion.Play(HomeReaction.AcceptedCare);
                motion.Advance(.15f);
                Require(pivot.transform.localPosition.y > 4f, "same-name non-trigger must still use fallback");
                motion.CancelAndRestore();
                controller.RemoveParameter(0);
                controller.AddParameter("IdleReact", AnimatorControllerParameterType.Trigger);
                motion.Play(HomeReaction.AcceptedCare);
                motion.Advance(.15f);
                Require(pivot.transform.localPosition.Equals(new Vector3(3, 4, 5)) && pivot.transform.localRotation.Equals(authoredRotation), "existing IdleReact trigger suppresses fallback pivot motion");
                Require(animator.runtimeAnimatorController == controller, "existing trigger never replaces controller");
                motion.CancelAndRestore();
            }
            finally { UnityEngine.Object.DestroyImmediate(pivot); UnityEngine.Object.DestroyImmediate(controller); }
        }

        private static IEnumerator Switch(HomeFocusStage stage, Func<GameObject> create, HomeSubject subject) =>
            (IEnumerator)typeof(HomeFocusStage).GetMethod("SwitchSubject", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(stage, new object[] { create, subject });
        private static void Drain(IEnumerator iterator) { while (iterator.MoveNext()) { } }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void InvokeLifecycle(object target, string method) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
