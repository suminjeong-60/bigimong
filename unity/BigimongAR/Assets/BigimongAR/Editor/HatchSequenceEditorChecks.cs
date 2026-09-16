#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Bigimong.AR.EditorChecks
{
    public static class HatchSequenceEditorChecks
    {
        public static void RunBehaviorChecks()
        {
            LiteralTimelineAndSettings();
            DurableFailureMatrix();
            RecoveryAndSubscriptions();
            BoundedEffects();
            SubjectRules();
            CallbackExceptionsAndInterruption();
            PresentationBindingsAndPendingSwitch();
            SubjectSwitchFailurePreservesView();
            CallbackReentrancyCannotResumeOldRun();
            NotificationFailuresAreNotSaveFailures();
            CracksFollowEggTransform();
            RecoveryWaitsForOutstandingStageSwitch();
            SilhouetteAndFocusTransitionUseRealStage();
        }

        private static void LiteralTimelineAndSettings()
        {
            Require(HatchSequenceDirector.ResumeTime(HatchCheckpoint.STARTED) == 0f, "STARTED replays at zero");
            Require(HatchSequenceDirector.ResumeTime(HatchCheckpoint.SHELL_BURST) == 2.4f, "burst resumes at warm light");
            Require(HatchSequenceDirector.ResumeTime(HatchCheckpoint.REVEALED) == 5f, "revealed opens directly");
            foreach (var method in typeof(HatchSequenceDirector).GetMethods(BindingFlags.Public | BindingFlags.Instance))
                Require(method.Name.IndexOf("skip", StringComparison.OrdinalIgnoreCase) < 0, "first hatch has no bypass API");
            foreach (var enabled in new[] { false, true })
            {
                using var fixture = new Fixture(Ready());
                var sequence = fixture.Sequence;
                sequence.Settings = new HatchPresentationSettings { Audio = enabled, Haptics = enabled, CameraImpulse = enabled, Particles = enabled };
                fixture.Coordinator.BeginHatch();
                Require(sequence.IsPlaying && fixture.Store.Durable.selectedArtId == 17 && fixture.Coordinator.InputLocked, "selection committed before first frame and input locked");
                sequence.Advance(.7f);
                Require(sequence.Segment == HatchSequenceSegment.Cracks && sequence.CrackStage == 1, "at .7 first crack");
                sequence.Advance(.4f);
                Require(sequence.CrackStage == 2, "at 1.1 second crack");
                sequence.Advance(.4f);
                Require(sequence.CrackStage == 3, "at 1.5 third crack");
                sequence.Advance(.3f);
                Require(sequence.Segment == HatchSequenceSegment.Burst && fixture.Store.Durable.hatchCheckpoint == "SHELL_BURST", "at 1.8 saved burst");
                sequence.Advance(.6f);
                Require(sequence.Segment == HatchSequenceSegment.WarmLight && !sequence.RevealVisible, "at 2.4 warm covered light");
                sequence.Advance(.8f);
                Require(sequence.Segment == HatchSequenceSegment.FocusTransition && !sequence.RevealVisible, "at 3.2 transition stays unrevealed");
                sequence.Advance(1f);
                Require(sequence.Segment == HatchSequenceSegment.Reveal && sequence.RevealVisible && fixture.Store.Durable.phase == "REVEAL", "at 4.2 baby/name only after durable reveal");
                Require(fixture.Store.Durable.hatchedAtUtcTicks == 638712864000000000L, "injected clock supplies exact UTC timestamp");
                sequence.Advance(.79f);
                Require(fixture.Store.Durable.phase == "REVEAL" && fixture.Coordinator.InputLocked, "4.99 is not HOME");
                sequence.Advance(.01f);
                Require(sequence.Elapsed == 5f && !sequence.IsPlaying && !fixture.Coordinator.InputLocked && fixture.Store.Durable.phase == "HOME", "5.0 completes exactly, regardless of feedback settings");
                Require(fixture.Store.Writes.Count == 4 && fixture.Random.Draws == 1, "only selection, burst, reveal and HOME writes");
            }
        }

        private static void CallbackReentrancyCannotResumeOldRun()
        {
            foreach (var boundary in new[] { 0, 1, 2 })
                foreach (var action in new[] { "interrupt", "replace", "disable" })
                    foreach (var result in new[] { false, true })
                    {
                        using var f = new Fixture(Ready());
                        var calls = 0;
                        var newCalls = 0;
                        Func<int, bool> callback = index =>
                        {
                            calls++;
                            if (index != boundary) return true;
                            if (action == "replace") f.Sequence.Play(HatchCheckpoint.SHELL_BURST, _ => { newCalls++; return true; }, () => { newCalls++; return true; }, () => { newCalls++; return true; });
                            else if (action == "disable") { f.Sequence.enabled = false; Invoke(f.Sequence, "OnDisable"); }
                            else f.Sequence.Interrupt();
                            return result;
                        };
                        f.Sequence.Play(HatchCheckpoint.STARTED, _ => callback(0), () => callback(1), () => callback(2));
                        f.Sequence.Advance(5f);
                        Require(calls == boundary + 1 && newCalls == 0, "old callback stack never advances either run after callback reentry");
                        if (action == "replace")
                            Require(f.Sequence.Elapsed == 2.4f && f.Sequence.IsPlaying && !f.Sequence.NeedsRetry && !f.Sequence.RevealVisible,
                                "old callback result cannot halt/finish/uncover replacement run");
                        else
                            Require(f.Sequence.Elapsed == (boundary == 0 ? 1.8f : boundary == 1 ? 4.2f : 5f) && !f.Sequence.IsPlaying && f.Sequence.NeedsRetry && f.Sequence.CoverAlpha == 1f,
                                "callback interrupt remains at exact covered boundary even if callback returns true");
                    }
        }

        private static void NotificationFailuresAreNotSaveFailures()
        {
            foreach (var boundary in new[] { "SHELL_BURST", "REVEAL", "HOME" })
            {
                using var f = new Fixture(Ready());
                var healthyHomeNotifications = 0;
                f.Coordinator.SnapshotChanged += value =>
                {
                    if (value.phase == boundary || value.hatchCheckpoint == boundary) throw new InvalidOperationException("broken UI subscriber");
                };
                f.Coordinator.SnapshotChanged += value => { if (value.phase == "HOME") healthyHomeNotifications++; };
                f.Coordinator.NoticeRequested += _ => throw new InvalidOperationException("broken notice subscriber");
                f.Coordinator.BeginHatch();
                f.Sequence.Advance(5f);
                Require(f.Store.Durable.phase == "HOME" && f.Store.Durable.selectedArtId == 17 && f.Store.Durable.hatchedAtUtcTicks == 638712864000000000L,
                    "notification exceptions cannot convert successful persistence into a failed checkpoint");
                Require(!f.Coordinator.InputLocked && !f.Sequence.NeedsRetry && healthyHomeNotifications == 1, "healthy subscriber still runs and durable HOME unlocks once");
                f.Coordinator.RetryHatch();
                Require(f.Store.Writes.Count == 4 && f.Random.Draws == 1 && !f.Coordinator.InputLocked, "HOME retry never repeats reveal/timestamp/random writes");
            }
        }

        private static void CracksFollowEggTransform()
        {
            var host = new GameObject("Crack pool");
            var egg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                var effect = host.AddComponent<EggCrackVfx>();
                effect.Bind(egg); effect.SetStage(1);
                var lines = UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
                LineRenderer crack = null;
                foreach (var line in lines) if (line.name == "CrackBranch0") { crack = line; break; }
                Require(crack != null, "real first crack exists");
                var before = crack.useWorldSpace ? crack.GetPosition(0) : crack.transform.TransformPoint(crack.GetPosition(0));
                egg.transform.position = new Vector3(3f, 4f, 5f);
                egg.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                var after = crack.useWorldSpace ? crack.GetPosition(0) : crack.transform.TransformPoint(crack.GetPosition(0));
                Require(Vector3.Distance(after, new Vector3(3f - before.y, 4f + before.x, 5f + before.z)) < .00001f,
                    "crack points undergo the egg's actual 90-degree rocking rotation and translation");
                effect.Reset();
                Require(!crack.enabled && crack.transform.IsChildOf(host.transform), "reset returns localized crack to pool owner");
                effect.Bind(egg); effect.SetStage(1);
                Require(crack.transform.localPosition == Vector3.zero && crack.transform.localRotation == Quaternion.identity && crack.transform.localScale == Vector3.one,
                    "reused crack transform removes old parent compensation before local fit");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(egg); }
        }

        private static void RecoveryWaitsForOutstandingStageSwitch()
        {
            HomeFocusStageEditorChecks.PrepareLayer();
            foreach (var entry in new[]
            {
                (HomeSubject.EGG, "EGG_ACTIVE"), (HomeSubject.AVATAR, "EGG_ACTIVE"),
                (HomeSubject.DINOSAUR, "HOME"), (HomeSubject.AVATAR, "HOME"),
                (HomeSubject.DINOSAUR, "REVEAL"), (HomeSubject.AVATAR, "REVEAL"),
            })
            {
                var durableView = entry.Item1;
                var initial = entry.Item2 == "EGG_ACTIVE" ? new HatchHomeSnapshot() : Revealed();
                initial.phase = entry.Item2;
                initial.activeHomeView = durableView.ToString();
                using var f = new Fixture(initial, initialize: false);
                var stageHost = new GameObject("Recovery stage", typeof(HomeFocusStage));
                var pending = new Queue<IEnumerator>();
                try
                {
                    var stage = stageHost.GetComponent<HomeFocusStage>();
                    stage.Initialize();
                    // Actual stage transaction continues independently while the coordinator is disabled.
                    var oldSwitch = StageSwitch(stage, HomeSubject.AVATAR);
                    Require(oldSwitch.MoveNext() && stage.InputLocked, "real stage has a prepared asynchronous avatar switch");
                    SetPrivate(f.Coordinator, "stage", stage);
                    SetPrivate(f.Coordinator, "requestSubject", new Action<HomeSubject>(subject =>
                    {
                        var iterator = StageSwitch(stage, subject);
                        iterator.MoveNext(); // Same synchronous preparation as StartCoroutine.
                        pending.Enqueue(iterator);
                    }));
                    var notifications = 0;
                    f.Coordinator.SnapshotChanged += _ => notifications++;
                    f.Coordinator.Initialize();
                    Invoke(f.Coordinator, "OnDisable");
                    Invoke(f.Coordinator, "OnEnable");
                    Require(f.Coordinator.InputLocked && pending.Count == 0, "re-enable waits instead of issuing a rejected switch while stage busy");
                    var staleWaiter = (IEnumerator)typeof(HatchHomeCoordinator).GetField("presentationTransaction", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(f.Coordinator);
                    Invoke(f.Coordinator, "OnDisable");
                    Invoke(f.Coordinator, "OnEnable");
                    Drain(oldSwitch);
                    Require(!staleWaiter.MoveNext() && notifications == 0, "invalidated waiter cannot publish, request another model or unlock a replacement recovery");
                    for (var frame = 0; frame < 8; frame++)
                    {
                        Invoke(f.Coordinator, "Update");
                        while (pending.Count > 0) Drain(pending.Dequeue());
                    }
                    Require(stage.ActiveSubject == durableView && f.Coordinator.Snapshot.ActiveSubject == durableView && !f.Coordinator.InputLocked,
                        "visible subject reconciles to durable view after the old switch actually finishes");
                    Require(f.Store.Writes.Count == (entry.Item2 == "REVEAL" ? 1 : 0) && f.Random.Draws == 0 && notifications == (entry.Item2 == "REVEAL" ? 2 : 1) && f.Provider.Subscribers == 1 && f.Store.Loads == 1,
                        "recovery causes one confirmed publication, no stale/duplicate saves, draws, or subscriptions");
                    f.Coordinator.CareEgg();
                    Require(f.Coordinator.Snapshot.eggProgress == (durableView == HomeSubject.EGG ? 500 : initial.eggProgress), "care follows actual and durable egg view only");
                }
                finally { UnityEngine.Object.DestroyImmediate(stageHost); }
            }
        }

        private static IEnumerator StageSwitch(HomeFocusStage stage, HomeSubject subject) =>
            (IEnumerator)typeof(HomeFocusStage).GetMethod("SwitchSubject", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(stage,
                new object[] { new Func<GameObject>(() => GameObject.CreatePrimitive(PrimitiveType.Sphere)), subject });
        private static void Drain(IEnumerator value) { while (value.MoveNext()) { } }
        private static void SetPrivate(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static void SilhouetteAndFocusTransitionUseRealStage()
        {
            HomeFocusStageEditorChecks.PrepareLayer();
            foreach (var acceptReveal in new[] { false, true })
            {
                var host = new GameObject("Visual sequence", typeof(HomeFocusStage), typeof(HatchSequenceDirector));
                var ui = new GameObject("Visual UI", typeof(RectTransform));
                var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(HomeStageOrbitInput));
                var overlay = new GameObject("Warm cover", typeof(RectTransform), typeof(Image));
                var silhouetteNode = new GameObject("Silhouette", typeof(RectTransform), typeof(RawImage));
                try
                {
                    viewport.transform.SetParent(ui.transform, false);
                    overlay.transform.SetParent(ui.transform, false);
                    silhouetteNode.transform.SetParent(ui.transform, false);
                    var stage = host.GetComponent<HomeFocusStage>();
                    stage.Initialize(); stage.SetViewportSize(new Vector2(360, 640), 1f);
                    Drain(StageSwitch(stage, HomeSubject.EGG));
                    var sequence = host.GetComponent<HatchSequenceDirector>();
                    var image = silhouetteNode.GetComponent<RawImage>();
                    var cover = overlay.GetComponent<Image>();
                    sequence.Configure(stage, cover, viewport.GetComponent<HomeStageOrbitInput>(), image);
                    IEnumerator switchBaby = null;
                    var requests = 0;
                    SetPrivate(sequence, "requestBaby", new Action<int>(_ => { requests++; switchBaby = StageSwitch(stage, HomeSubject.DINOSAUR); switchBaby.MoveNext(); }));
                    var revealCalls = 0;
                    sequence.Play(HatchCheckpoint.STARTED, _ => true, () => { revealCalls++; return acceptReveal; }, () => true);
                    sequence.Advance(1.8f);
                    Require(requests == 0 && !image.enabled, "no baby transition or silhouette before warm boundary");
                    sequence.Advance(.6f);
                    Require(requests == 1 && stage.InputLocked && cover.color.a == 1f && !image.enabled, "2.4 requests baby behind opaque cover, never exposing pending color");
                    Drain(switchBaby);
                    var baby = stage.SubjectInstance;
                    var scale = baby.transform.localScale;
                    var material = baby.GetComponent<Renderer>().sharedMaterial;
                    var materialColor = material.color;
                    var camera = stage.StageRoot.GetComponentInChildren<Camera>().transform;
                    var focusPosition = camera.localPosition;
                    sequence.Advance(.4f);
                    Require(image.enabled && image.texture == stage.Texture && image.color == Color.black && cover.color.a == 1f && !sequence.RevealVisible && revealCalls == 0,
                        "2.8 renders actual baby-shaped black texture silhouette while full color/name remain covered");
                    sequence.Advance(.4f);
                    var initialDollyDistance = Vector3.Distance(camera.localPosition, focusPosition);
                    Require(initialDollyDistance > .01f && image.enabled && sequence.Segment == HatchSequenceSegment.FocusTransition,
                        "3.2 starts an actual camera transition into the focus-stage framing");
                    var warmColor = cover.color;
                    sequence.Advance(.5f);
                    Require(Vector3.Distance(camera.localPosition, focusPosition) < initialDollyDistance && Vector3.Distance(camera.localPosition, focusPosition) > .001f && cover.color != warmColor,
                        "3.7 changes actual camera framing and warm light tint, not merely an enum");
                    sequence.Advance(.5f);
                    Require(revealCalls == 1 && !image.enabled && camera.localPosition == focusPosition && baby.transform.localScale == scale && baby.GetComponent<Renderer>().sharedMaterial == material && material.color == materialColor,
                        "4.2 restores exact framing and never changes authored proportions or material assets");
                    Require(sequence.RevealVisible == acceptReveal && sequence.NeedsRetry == !acceptReveal && cover.color.a == 1f,
                        "only accepted REVEALED enables full reveal; failure remains opaque with silhouette disabled");
                }
                finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(ui); }
            }
        }

        private static void DurableFailureMatrix()
        {
            using (var incomplete = new Fixture(new HatchHomeSnapshot { eggProgress = 29999 }))
            {
                incomplete.Coordinator.BeginHatch();
                Require(incomplete.Random.Draws == 0 && !incomplete.Sequence.IsPlaying, "29999 never draws or starts");
            }
            using (var start = new Fixture(Ready()))
            {
                start.Store.FailPhase = "HATCHING";
                start.Coordinator.BeginHatch();
                Require(start.Coordinator.Snapshot.phase == "HATCH_READY" && !start.Sequence.IsPlaying && !start.Coordinator.InputLocked, "failed selection remains ready with unlocked retry");
            }
            foreach (var failedBoundary in new[] { "SHELL_BURST", "REVEAL", "HOME" })
            {
                using var f = new Fixture(Ready());
                f.Coordinator.BeginHatch();
                f.Store.FailPhase = failedBoundary;
                f.Sequence.Advance(5f);
                var expectedPhase = failedBoundary == "HOME" ? "REVEAL" : "HATCHING";
                var expectedCheckpoint = failedBoundary == "SHELL_BURST" ? "STARTED" : failedBoundary == "REVEAL" ? "SHELL_BURST" : "REVEALED";
                Require(f.Store.Durable.phase == expectedPhase && f.Store.Durable.hatchCheckpoint == expectedCheckpoint && f.Store.Durable.selectedArtId == 17, "failed boundary preserves previous durable identity");
                Require(f.Sequence.NeedsRetry && !f.Sequence.IsPlaying && f.Sequence.CoverAlpha == 1f && !f.Sequence.RevealVisible && f.Coordinator.InputLocked, "each failure stops covered without navigation exposure");
                var timestamp = f.Store.Durable.hatchedAtUtcTicks;
                f.Clock.Now = new DateTime(638713728000000000L, DateTimeKind.Utc);
                f.Store.FailPhase = null;
                f.Coordinator.RetryHatch();
                f.Sequence.Advance(5f);
                Require(f.Store.Durable.phase == "HOME" && f.Store.Durable.selectedArtId == 17 && f.Random.Draws == 1 && !f.Coordinator.InputLocked, "retry completes same baby without random draw");
                Require(f.Store.Durable.hatchedAtUtcTicks == (timestamp == 0 ? 638713728000000000L : 638712864000000000L), "HOME-only retry never rewrites reveal timestamp");
            }
            // A throwing callback must be contained identically to a false result, even with a large tick.
            using var thrown = new Fixture(Ready());
            thrown.Sequence.Play(HatchCheckpoint.STARTED, _ => throw new InvalidOperationException("disk"), () => true, () => true);
            thrown.Sequence.Advance(5f);
            Require(thrown.Sequence.Elapsed == 1.8f && thrown.Sequence.NeedsRetry && thrown.Sequence.CoverAlpha == 1f, "exception halts at first uncommitted boundary");
        }

        private static void RecoveryAndSubscriptions()
        {
            foreach (var checkpoint in new[] { "STARTED", "SHELL_BURST", "REVEALED" })
            {
                var state = Ready();
                state.selectedArtId = 17;
                state.hatchCheckpoint = checkpoint;
                state.phase = checkpoint == "REVEALED" ? "REVEAL" : "HATCHING";
                state.activeHomeView = checkpoint == "REVEALED" ? "DINOSAUR" : "EGG";
                state.hatchedAtUtcTicks = checkpoint == "REVEALED" ? 638712864000000000L : 0;
                using var f = new Fixture(state, true);
                Require(f.Sequence.Elapsed == (checkpoint == "STARTED" ? 0f : checkpoint == "SHELL_BURST" ? 2.4f : 5f), "literal recovery mapping");
                Require(f.Random.Draws == 0 && f.Notices.Contains("저장된 진행 상황을 복구했어요."), "recovery notice without reroll");
                f.Coordinator.Initialize();
                Require(f.Store.Loads == 1 && f.Provider.Subscribers == 1, "initialize loads once and subscribes once");
                Invoke(f.Coordinator, "OnDisable");
                Require(f.Provider.Subscribers == 0, "disable unsubscribes");
                Invoke(f.Coordinator, "OnEnable");
                Require(f.Provider.Subscribers == 1 && f.Store.Loads == 1 && f.Random.Draws == 0, "enable resumes without duplicate load/draw");
            }
            using var pendingHome = new Fixture(Revealed(), true, "HOME");
            Require(pendingHome.Store.Durable.phase == "REVEAL" && pendingHome.Coordinator.InputLocked && pendingHome.Sequence.NeedsRetry, "relaunch HOME failure remains retryable REVEAL");
            pendingHome.Store.FailPhase = null;
            pendingHome.Coordinator.RetryHatch();
            Require(pendingHome.Store.Durable.phase == "HOME" && pendingHome.Store.Durable.hatchedAtUtcTicks == 638712864000000000L, "HOME retry preserves timestamp");
            using var progress = new Fixture(new HatchHomeSnapshot());
            progress.Provider.Emit(new VerifiedStepDelta { providerId = "trusted", eventId = "one", dayKey = 20260101, cumulativeTotal = 250, delta = 250 });
            Require(progress.Coordinator.Snapshot.eggProgress == 250 && progress.Store.Writes.Count == 1, "provider event is consumed once");
            progress.Coordinator.CareEgg();
            Require(progress.Coordinator.Snapshot.eggProgress == 750, "accepted care consumes real progress service");
            progress.Coordinator.CareEgg();
            Require(progress.Coordinator.Snapshot.eggProgress == 750, "cooldown cannot award duplicate care");
        }

        private static void SubjectRules()
        {
            using var f = new Fixture(Ready());
            f.Coordinator.ShowSubject(HomeSubject.DINOSAUR);
            Require(f.Coordinator.Snapshot.activeHomeView == "EGG", "pre-hatch rejects dinosaur");
            f.Coordinator.ShowSubject(HomeSubject.AVATAR);
            Require(f.Coordinator.Snapshot.activeHomeView == "AVATAR", "pre-hatch permits avatar");
            f.Coordinator.BeginHatch();
            f.Coordinator.ShowSubject(HomeSubject.AVATAR);
            Require(f.Coordinator.Snapshot.activeHomeView == "EGG", "hatching rejects subject changes");
            f.Sequence.Advance(5f);
            f.Coordinator.ShowSubject(HomeSubject.EGG);
            Require(f.Coordinator.Snapshot.activeHomeView == "DINOSAUR", "post-hatch rejects egg");
            f.Coordinator.ShowSubject(HomeSubject.AVATAR);
            Require(f.Coordinator.Snapshot.activeHomeView == "AVATAR", "post-hatch permits avatar");
            f.Coordinator.ShowSubject(HomeSubject.DINOSAUR);
            Require(f.Coordinator.Snapshot.activeHomeView == "DINOSAUR", "post-hatch permits same baby");
        }

        private static void CallbackExceptionsAndInterruption()
        {
            foreach (var boundary in new[] { 0, 1, 2 })
                foreach (var throwing in new[] { false, true })
                {
                    using var f = new Fixture(Ready());
                    var calls = new List<string>();
                    Func<int, bool> commit = index =>
                    {
                        calls.Add(index.ToString());
                        if (index != boundary) return true;
                        if (throwing) throw new InvalidOperationException("simulated disk failure");
                        return false;
                    };
                    f.Sequence.Play(HatchCheckpoint.STARTED, _ => commit(0), () => commit(1), () => commit(2));
                    f.Sequence.Advance(10f);
                    Require(calls.Count == boundary + 1 && calls[calls.Count - 1] == boundary.ToString(), "nothing calls past failed durable boundary");
                    Require(f.Sequence.Elapsed == (boundary == 0 ? 1.8f : boundary == 1 ? 4.2f : 5f) &&
                        f.Sequence.CoverAlpha == 1f && !f.Sequence.RevealVisible && f.Sequence.NeedsRetry,
                        "false and throwing callbacks have literal safe boundary frames");
                    f.Sequence.Advance(10f);
                    Require(calls.Count == boundary + 1, "halted sequence does not retry saves every frame");
                }
            foreach (var time in new[] { .5f, 2f, 3f, 4.5f })
            {
                using var f = new Fixture(Ready());
                f.Coordinator.BeginHatch();
                f.Sequence.Advance(time);
                var previous = f.Store.Durable.Clone();
                f.Sequence.Interrupt();
                Require(f.Sequence.CoverAlpha == 1f && f.Coordinator.InputLocked && f.Sequence.NeedsRetry, "interruption is covered and locked");
                Require(f.Store.Durable.phase == previous.phase && f.Store.Durable.hatchCheckpoint == previous.hatchCheckpoint, "interruption commits nothing");
                f.Coordinator.RetryHatch();
                f.Sequence.Advance(5f);
                Require(f.Store.Durable.phase == "HOME" && f.Store.Durable.selectedArtId == 17 && f.Random.Draws == 1, "interruption recovers same selected baby");
            }
            using var settings = new Fixture(Ready());
            settings.Sequence.Settings.Audio = false;
            settings.Coordinator.BeginHatch();
            Require(!settings.Sequence.Settings.Audio, "saved sound-on never overrides user presentation mute");
        }

        private static void PresentationBindingsAndPendingSwitch()
        {
            using var f = new Fixture(Ready());
            var stageHost = new GameObject("Binding stage", typeof(HomeFocusStage));
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(HomeStageOrbitInput));
            var overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            var silhouette = new GameObject("Silhouette", typeof(RectTransform), typeof(RawImage));
            try
            {
                var stage = stageHost.GetComponent<HomeFocusStage>();
                Require(!f.Sequence.HasPresentationBindings(stage), "real stage requires explicit overlay/orbit binding");
                f.Sequence.Configure(stage, overlay.GetComponent<Image>(), viewport.GetComponent<HomeStageOrbitInput>(), silhouette.GetComponent<RawImage>());
                Require(overlay.GetComponent<Image>().color.a == 0f && !overlay.GetComponent<Image>().raycastTarget, "fresh overlay does not obscure egg/home");
                Invoke(f.Sequence, "OpenHome");
                Require(viewport.GetComponent<HomeStageOrbitInput>().enabled, "opening saved HOME cannot disable an orbit the director never locked");
                f.Sequence.Configure(null, null, null);
                f.Coordinator.BeginHatch();
                f.Sequence.Advance(4.2f);
                f.Sequence.Configure(stage, overlay.GetComponent<Image>(), viewport.GetComponent<HomeStageOrbitInput>(), silhouette.GetComponent<RawImage>());
                Require(f.Sequence.HasPresentationBindings(stage), "complete binding accepted");
                typeof(HomeFocusStage).GetField("inputLocked", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(stage, true);
                f.Sequence.Advance(.8f);
                Require(f.Store.Durable.phase == "REVEAL" && f.Sequence.NeedsRetry && f.Sequence.CoverAlpha == 1f && f.Coordinator.InputLocked,
                    "pending model switch at deadline never commits HOME or unlocks navigation");
                Require(overlay.GetComponent<Image>().color.a == 1f && overlay.GetComponent<Image>().raycastTarget, "safe state actually covers and blocks viewport");
                Require(!viewport.GetComponent<HomeStageOrbitInput>().enabled, "retry failure retains external orbit lock despite stage unlocks");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stageHost);
                UnityEngine.Object.DestroyImmediate(viewport);
                UnityEngine.Object.DestroyImmediate(overlay);
                UnityEngine.Object.DestroyImmediate(silhouette);
            }
        }

        private static void BoundedEffects()
        {
            var host = new GameObject("Effects");
            var egg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                var effect = host.AddComponent<EggCrackVfx>();
                var renderer = egg.GetComponent<Renderer>();
                effect.Bind(egg);
                effect.SetStage(1);
                Require(effect.ActiveCrackGroups == 1, "one predefined branch group");
                effect.SetStage(9);
                Require(effect.ActiveCrackGroups == 3, "cracks bounded to three groups");
                effect.Burst();
                Require(effect.ActiveFragments == 24 && !renderer.enabled, "burst hides intact renderer and emits 24 pooled pieces");
                var created = host.GetComponentsInChildren<MeshRenderer>(true);
                effect.Advance(1.19f);
                Require(effect.ActiveFragments == 24, "fragments still alive before 1.2 seconds");
                effect.Advance(.01f);
                Require(effect.ActiveFragments == 0, "fragments inactive at 1.2 seconds");
                effect.Reset();
                Require(renderer.enabled && effect.ActiveCrackGroups == 0 && effect.ActiveFragments == 0, "reset restores egg and clears every pooled visual");
                effect.Burst();
                Require(host.GetComponentsInChildren<MeshRenderer>(true).Length == created.Length, "second burst reuses pool");
                Invoke(effect, "OnDisable");
                Require(renderer.enabled && effect.ActiveFragments == 0, "interruption restores renderer and effects");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(egg); }
        }

        private static void SubjectSwitchFailurePreservesView()
        {
            using var f = new Fixture(Ready());
            var stageHost = new GameObject("Rejected subject stage", typeof(HomeFocusStage));
            try
            {
                var stage = stageHost.GetComponent<HomeFocusStage>();
                typeof(HatchHomeCoordinator).GetField("stage", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(f.Coordinator, stage);
                var finish = typeof(HatchHomeCoordinator).GetMethod("CompleteSubjectSwitch", BindingFlags.NonPublic | BindingFlags.Instance);
                var generation = typeof(HatchHomeCoordinator).GetField("presentationGeneration", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(f.Coordinator);
                var rejected = (IEnumerator)finish.Invoke(f.Coordinator, new object[] { HomeSubject.AVATAR, HomeSubject.EGG, generation });
                Require(!rejected.MoveNext() && f.Coordinator.Snapshot.activeHomeView == "EGG" && f.Store.Writes.Count == 0 && !f.Coordinator.InputLocked,
                    "model rejection keeps durable and visible egg view aligned, with navigation restored");
                typeof(HomeFocusStage).GetField("<ActiveSubject>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(stage, HomeSubject.AVATAR);
                var accepted = (IEnumerator)finish.Invoke(f.Coordinator, new object[] { HomeSubject.AVATAR, HomeSubject.EGG, generation });
                Require(!accepted.MoveNext() && f.Coordinator.Snapshot.activeHomeView == "AVATAR" && f.Store.Writes.Count == 1,
                    "successful model switch commits active view only after stage confirms requested subject");
            }
            finally { UnityEngine.Object.DestroyImmediate(stageHost); }
        }

        private static HatchHomeSnapshot Ready() => new() { eggProgress = 30000, phase = "HATCH_READY" };
        private static HatchHomeSnapshot Revealed() => new() { eggProgress = 30000, phase = "REVEAL", selectedArtId = 17, hatchCheckpoint = "REVEALED", activeHomeView = "DINOSAUR", hatchedAtUtcTicks = 638712864000000000L };
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Host = new("Hatch fixture");
            public readonly MemoryStore Store;
            public readonly CountingRandom Random = new();
            public readonly FakeClock Clock = new();
            public readonly Provider Provider = new();
            public readonly HatchSequenceDirector Sequence;
            public readonly HatchHomeCoordinator Coordinator;
            public readonly List<string> Notices = new();
            public Fixture(HatchHomeSnapshot initial, bool recovered = false, string failure = null, bool initialize = true)
            {
                Store = new MemoryStore { Durable = initial.Clone(), Recovered = recovered, FailPhase = failure };
                Sequence = Host.AddComponent<HatchSequenceDirector>();
                Coordinator = Host.AddComponent<HatchHomeCoordinator>();
                Coordinator.Configure(Store, new HatchProgressService(Store, Clock), new HatchSelectionService(Store, Random), null, Sequence, Provider, Clock);
                Coordinator.NoticeRequested += Notices.Add;
                if (initialize) Coordinator.Initialize();
            }
            public void Dispose() { Invoke(Coordinator, "OnDisable"); UnityEngine.Object.DestroyImmediate(Host); }
        }
        private sealed class FakeClock : ICareClock { public DateTime Now = new(638712864000000000L, DateTimeKind.Utc); public DateTime UtcNow => Now; }
        private sealed class CountingRandom : IArtIdRandomSource { public int Draws; public int NextArtId() { Draws++; return 17; } }
        private sealed class MemoryStore : IHatchHomeStore
        {
            public HatchHomeSnapshot Durable;
            public bool Recovered;
            public string FailPhase;
            public int Loads;
            public readonly List<HatchHomeSnapshot> Writes = new();
            public HatchLoadResult Load() { Loads++; return new HatchLoadResult(Durable.Clone(), Recovered, "memory"); }
            public bool TrySave(HatchHomeSnapshot snapshot)
            {
                if (snapshot.phase == FailPhase || snapshot.hatchCheckpoint == FailPhase) return false;
                Require(snapshot.HasValidDurableIdentity(), "coordinator only writes valid durable identities");
                Durable = snapshot.Clone(); Writes.Add(Durable.Clone()); return true;
            }
        }
        private sealed class Provider : IWalkingProgressProvider
        {
            private Action<VerifiedStepDelta> received;
            public int Subscribers;
            public bool IsAvailable => true;
            public event Action<VerifiedStepDelta> VerifiedStepsReceived { add { Subscribers++; received += value; } remove { Subscribers--; received -= value; } }
            public void Emit(VerifiedStepDelta value) => received?.Invoke(value);
        }
    }
}
#endif
