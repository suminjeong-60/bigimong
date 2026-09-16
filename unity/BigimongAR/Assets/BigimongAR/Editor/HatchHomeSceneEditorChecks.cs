#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bigimong.AR.EditorChecks
{
    // Literal outcomes: these checks run against real production objects, not source regular expressions.
    public static class HatchHomeSceneEditorChecks
    {
        private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Hatch home: " + message); }
        private static object Field(object target, string name) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);

        public static void RunSceneChecks()
        {
            var view = UnityEngine.Object.FindObjectOfType<HatchHomeView>(true);
            Require(view != null, "view missing");
            var safe = view.transform.Find("Safe Area");
            Require(safe != null && safe.GetComponent<DeviceSafeArea>() != null, "safe area missing");
            foreach (var node in new[] { "Backdrop", "Top Status", "Subject Switch", "Stage Viewport", "Front View Button",
                "Progress Panel", "Hatch Button", "Species Plate", "Notice Panel", "Bottom Navigation" })
                Require(safe.Find(node) != null, "missing hierarchy node " + node);
            var scaler = view.GetComponent<CanvasScaler>();
            Require(view.name == "Hatch Home UI" && view.GetComponentsInChildren<Canvas>(true).Length == 1,
                "one named canvas required");
            Require(scaler.referenceResolution == new Vector2(1080, 1920) && scaler.matchWidthOrHeight == .5f,
                "scaler must be 1080x1920 / .5");
            var viewport = safe.Find("Stage Viewport").GetComponent<RawImage>();
            Require(viewport != null && viewport.raycastTarget && viewport.GetComponent<HomeStageOrbitInput>() != null,
                "viewport owns orbit");
            var rect = viewport.rectTransform;
            Require(Mathf.Abs(rect.anchorMax.y - rect.anchorMin.y - .68f) < .001f, "approximately 70 percent viewport");
            foreach (var name in new[] { "Top Status", "Subject Switch", "Front View Button", "Progress Panel", "Hatch Button", "Species Plate", "Notice Panel", "Bottom Navigation" })
            {
                var control = (RectTransform)safe.Find(name);
                Require(control.anchorMax.y <= rect.anchorMin.y || control.anchorMin.y >= rect.anchorMax.y,
                    "controls stay outside stage: " + name);
            }
            foreach (var button in view.GetComponentsInChildren<Button>(true))
                Require(button.GetComponent<ButtonPressMotion>() != null, "button motion " + button.name);
            foreach (var label in view.GetComponentsInChildren<TMP_Text>(true)) CheckTypography(label);
            foreach (var label in UnityEngine.Object.FindObjectsOfType<TMP_Text>(true)) CheckTypography(label);
            Require(UnityEngine.Object.FindObjectsOfType<Text>(true).Length == 0, "no legacy text overlays");
            foreach (var graphic in view.GetComponentsInChildren<Graphic>(true))
                if (graphic != viewport && graphic.GetComponent<Button>() == null && graphic.name != "Hatch Transition Cover")
                    Require(!graphic.raycastTarget, "decorative raycast " + graphic.name);
            PresentationBindings(view);
            TypographyRoles();
            LiteralPhasePresentation(view);
            SideActivityRules();
            FailedSaveKeepsSnapshot();
            RejectedResultsAreIsolated();
            PortraitNavigationTouchTargets(view);
            RetainedOverlayInputOwnership(view);
            CodexIsPreviewOnly();
        }

        private static void CheckTypography(TMP_Text text)
        {
            Require(text.font != null && (text.font.name == "BigimongKR-Bold" || text.font.name == "BigimongKR-Black"),
                "shared Korean font " + text.name);
            Require(!text.raycastTarget && text.fontSharedMaterial != null &&
                text.fontSharedMaterial.IsKeywordEnabled("UNDERLAY_ON"), "shared underlay " + text.name);
            Require(text.font.atlasPopulationMode == AtlasPopulationMode.Static && !text.font.isMultiAtlasTexturesEnabled,
                "bounded static atlas " + text.name);
            Require(text.fontSharedMaterial.name == text.font.name + "-Cocoa", "no per-screen material clone " + text.name);
        }

        private static void PresentationBindings(HatchHomeView view)
        {
            foreach (var binding in new[] { "coordinator", "stage", "sequence", "viewport", "cover", "silhouette", "orbit", "referenceUi", "flow", "theme" })
                Require(Field(view, binding) != null, "serialized binding " + binding);
            var cover = (Image)Field(view, "cover");
            var silhouette = (RawImage)Field(view, "silhouette");
            Require(cover.transform.parent == silhouette.transform.parent, "same-parent reveal layers");
            Require(cover.rectTransform.anchorMin == silhouette.rectTransform.anchorMin &&
                cover.rectTransform.anchorMax == silhouette.rectTransform.anchorMax &&
                cover.rectTransform.offsetMin == silhouette.rectTransform.offsetMin &&
                cover.rectTransform.offsetMax == silhouette.rectTransform.offsetMax, "aligned reveal layers");
            Require(!view.transform.Find("Safe Area/Notice Panel").IsChildOf(cover.transform), "retry outside cover");
            var reference = (BigimongReferenceUi)Field(view, "referenceUi");
            Require(Field(reference, "hatchHomeView") == view && Field(reference, "hatchHomeCoordinator") == Field(view, "coordinator"),
                "reference delegation bindings");
            var stage = (HomeFocusStage)Field(view, "stage");
            var sequence = (HatchSequenceDirector)Field(view, "sequence");
            sequence.Configure(stage, cover, (HomeStageOrbitInput)Field(view, "orbit"), silhouette);
            Require(sequence.HasPresentationBindings(stage), "director four-argument scene bindings");
            var coordinator = (HatchHomeCoordinator)Field(view, "coordinator");
            var buttons = (Button[])Field(view, "navigationButtons");
            var routes = (string[])Field(view, "navigationRoutes");
            Require(buttons != null && buttons.Length == 9 && buttons.All(button => button != null) &&
                routes.SequenceEqual(new[] { "홈", "상점", "놀아주기", "1:1 대전", "도감", "퀘스트", "선물", "설정", "캐릭터" }),
                "literal navigation button/route bindings");
            var retryState = typeof(HatchHomeCoordinator).GetField("<NeedsRetry>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            retryState.SetValue(coordinator, true);
            typeof(HatchHomeView).GetMethod("SetInputLocked", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { true });
            var retry = (Button)Field(view, "retryButton");
            Require(retry.gameObject.activeSelf && retry.interactable && !retry.transform.IsChildOf(cover.transform), "retry accessible outside cover");
            Require(view.GetComponentsInChildren<Button>(true).Where(button => button != retry).All(button => !button.interactable), "recovery locks unrelated navigation");
            retryState.SetValue(coordinator, false);
        }

        private static void TypographyRoles()
        {
            var node = new GameObject("Typography role check", typeof(RectTransform), typeof(TextMeshProUGUI));
            try
            {
                var text = node.GetComponent<TMP_Text>();
                var theme = BigimongTypographyTheme.Shared;
                var glyphs = Bigimong.Editor.BigimongFontAssetBuilder.ShippedGlyphs();
                foreach (BigimongTextRole role in Enum.GetValues(typeof(BigimongTextRole)))
                {
                    theme.Apply(text, role);
                    var heavy = role is BigimongTextRole.TITLE or BigimongTextRole.ACTION;
                    Require(text.font.name == (heavy ? "BigimongKR-Black" : "BigimongKR-Bold"), "literal role weight " + role);
                    Require(text.fontSharedMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth) == (heavy ? .22f : .12f), "role outline");
                    var shared = text.fontSharedMaterial;
                    theme.Apply(text, role);
                    Require(ReferenceEquals(shared, text.fontSharedMaterial), "style cannot clone materials");
                    foreach (var character in glyphs) Require(text.font.HasCharacter(character), "missing shipped glyph " + character);
                    foreach (var character in text.font.characterTable)
                        Require(character.unicode <= 32 || glyphs.IndexOf((char)character.unicode) >= 0, "unrestricted atlas glyph");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(node); }
        }

        private static void LiteralPhasePresentation(HatchHomeView view)
        {
            var coordinator = (HatchHomeCoordinator)Field(view, "coordinator");
            var clock = typeof(HatchHomeCoordinator).GetField("clock", BindingFlags.Instance | BindingFlags.NonPublic);
            var previousClock = clock.GetValue(coordinator);
            clock.SetValue(coordinator, new FixedClock());
            var snapshot = new HatchHomeSnapshot { eggProgress = 29999 };
            view.Render(snapshot);
            Require(((TMP_Text)Field(view, "progressLabel")).text == "29,999 / 30,000", "exact progress format");
            Require(((Image)Field(view, "progressFill")).fillAmount == 29999f / 30000f, "progress bar");
            Require(!((Button)Field(view, "hatchButton")).interactable, "29999 disabled");
            snapshot.nextCareAtUtcTicks = Now.AddSeconds(3661).Ticks;
            view.Render(snapshot);
            var cooldown = ((TMP_Text)Field(view, "cooldownLabel")).text;
            Require(cooldown == "01:01:01", "HH:MM:SS cooldown");
            Require(((TMP_Text)Field(view, "hatchLabel")).text == "1 더 필요해요", "remaining requirement");
            Require(((TMP_Text)Field(view, "subjectLabel")).text == "비기알", "pre-hatch label");
            snapshot.phase = "HATCH_READY"; snapshot.eggProgress = 30000;
            view.Render(snapshot);
            Require(((Button)Field(view, "hatchButton")).interactable, "ready enabled");
            Require(((TMP_Text)Field(view, "hatchLabel")).text == "부화하기", "explicit hatch label");
            snapshot.phase = "HATCHING"; snapshot.selectedArtId = 1; snapshot.hatchCheckpoint = "STARTED";
            view.Render(snapshot);
            Require(view.GetComponentsInChildren<Button>(true).All(b => !b.interactable), "hatching locks buttons");
            snapshot.phase = "HOME"; snapshot.hatchCheckpoint = "REVEALED"; snapshot.activeHomeView = "DINOSAUR";
            view.Render(snapshot);
            Require(((TMP_Text)Field(view, "speciesLabel")).text == "01 티라노사우루스\n아동기 · 등급: 기본", "literal reveal identity");
            Require(((TMP_Text)Field(view, "subjectLabel")).text == "공룡", "post-hatch label");
            view.Render(new HatchHomeSnapshot());
            clock.SetValue(coordinator, previousClock);
        }

        private static void Identity(HatchHomeSnapshot before, HatchHomeSnapshot after)
        {
            Require(before.eggProgress == after.eggProgress && before.phase == after.phase &&
                before.selectedArtId == after.selectedArtId && before.hatchCheckpoint == after.hatchCheckpoint &&
                before.hatchedAtUtcTicks == after.hatchedAtUtcTicks && before.activeHomeView == after.activeHomeView &&
                before.nextCareAtUtcTicks == after.nextCareAtUtcTicks && before.lastObservedUtcTicks == after.lastObservedUtcTicks &&
                before.processedStepEvents.SequenceEqual(after.processedStepEvents) &&
                before.stepProviderCursors.Count == after.stepProviderCursors.Count &&
                before.stepProviderCursors.Zip(after.stepProviderCursors, (a, b) => a.providerId == b.providerId &&
                    a.dayKey == b.dayKey && a.cumulativeTotal == b.cumulativeTotal).All(equal => equal), "side activity mutated hatch identity");
        }

        private static void SideActivityRules()
        {
            var store = new MemoryStore();
            var service = new HomeSideActivityService(store);
            var egg = new HatchHomeSnapshot { eggProgress = 1234, nextCareAtUtcTicks = 99, lastObservedUtcTicks = 42 };
            var original = JsonUtility.ToJson(egg);
            var play = service.TryApply(egg, HomeSideActivityAction.Play, Now);
            Require(play.Accepted && play.Snapshot.playsToday == 1 && play.Snapshot.coins == 0 && play.Snapshot.dragonExperience == 0, "pre-hatch play counters only");
            Identity(egg, play.Snapshot);
            Require(JsonUtility.ToJson(egg) == original, "clone before save");
            Require(!service.TryApply(play.Snapshot, HomeSideActivityAction.Play, Now.AddSeconds(9)).Accepted, "9-second cooldown");
            Require(service.TryApply(play.Snapshot, HomeSideActivityAction.Play, Now.AddSeconds(10)).Accepted, "10-second boundary");
            var capped = play.Snapshot.Clone(); capped.playsToday = 100;
            Require(!service.TryApply(capped, HomeSideActivityAction.Play, Now.AddSeconds(20)).Accepted, "100/day cap");
            var gift = service.TryApply(play.Snapshot, HomeSideActivityAction.DailyGift, Now);
            Require(gift.Accepted && gift.Snapshot.coins == 500 && gift.Snapshot.giftClaimedToday, "500 daily gift");
            Identity(egg, gift.Snapshot);
            Require(!service.TryApply(gift.Snapshot, HomeSideActivityAction.DailyGift, Now).Accepted, "gift once per day");
            Require(!service.TryApply(gift.Snapshot, HomeSideActivityAction.QuestReward, Now).Accepted, "quest requires three plays");
            var questSource = gift.Snapshot.Clone(); questSource.playsToday = 3;
            var quest = service.TryApply(questSource, HomeSideActivityAction.QuestReward, Now);
            Require(quest.Accepted && quest.Snapshot.coins == 800 && quest.Snapshot.questClaimedToday, "300 quest coins");
            Require(!service.TryApply(quest.Snapshot, HomeSideActivityAction.QuestReward, Now).Accepted, "quest once per day");
            var snack = service.TryApply(quest.Snapshot, HomeSideActivityAction.BuySnack, Now);
            Require(snack.Accepted && snack.Snapshot.coins == 500 && snack.Snapshot.snacks == 1, "300 snack cost");
            Require(!service.TryApply(snack.Snapshot, HomeSideActivityAction.Feed, Now).Accepted, "prehatch cannot feed");
            var home = snack.Snapshot.Clone(); home.phase = "HOME"; home.selectedArtId = 17; home.hatchCheckpoint = "REVEALED";
            home.hatchedAtUtcTicks = 98765; home.activeHomeView = "DINOSAUR";
            var feed = service.TryApply(home, HomeSideActivityAction.Feed, Now);
            Require(feed.Accepted && feed.Snapshot.snacks == 0 && feed.Snapshot.dragonExperience == 500, "feed 500 XP");
            Identity(home, feed.Snapshot);
            var homePlay = service.TryApply(home, HomeSideActivityAction.Play, Now.AddSeconds(30));
            Require(homePlay.Accepted && homePlay.Snapshot.coins == 550 && homePlay.Snapshot.dragonExperience == 200, "hatched play rewards");
            var max = home.Clone(); max.dragonExperience = 28900; max.coins = 999999;
            var bounded = service.TryApply(max, HomeSideActivityAction.Play, Now.AddSeconds(30));
            Require(bounded.Snapshot.dragonExperience == 29000 && bounded.Snapshot.coins == 1000000, "reward ceilings");
            var sound = service.TryApply(home, HomeSideActivityAction.ToggleSound, Now);
            Require(sound.Accepted && !sound.Snapshot.soundEnabled, "sound preference");
            var day = service.TryApply(home, HomeSideActivityAction.DayRollover, Now.AddDays(1));
            Require(day.Accepted && day.Snapshot.playsToday == 0 && !day.Snapshot.giftClaimedToday && !day.Snapshot.questClaimedToday, "day rollover");
            Identity(egg, quest.Snapshot); Identity(egg, snack.Snapshot);
            foreach (var result in new[] { sound, day, homePlay }) Identity(home, result.Snapshot);
            store.Fail = true;
            Require(!service.TryApply(egg, HomeSideActivityAction.DailyGift, Now).Accepted && JsonUtility.ToJson(egg) == original, "save failure preserves source");
            store.Fail = false;
            foreach (var phase in new[] { "EGG_ACTIVE", "HATCH_READY", "HATCHING", "REVEAL", "HOME" })
                foreach (HomeSideActivityAction action in Enum.GetValues(typeof(HomeSideActivityAction)))
                {
                    var current = home.Clone(); current.phase = phase; current.coins = 999000;
                    current.playsToday = 3; current.giftClaimedToday = current.questClaimedToday = false;
                    current.lastPlayTicks = 0; current.dayKey = 2026259;
                    var result = service.TryApply(current, action, action == HomeSideActivityAction.DayRollover ? Now.AddDays(1) : Now);
                    Identity(current, result.Snapshot);
                }
        }

        private static void FailedSaveKeepsSnapshot()
        {
            var root = new GameObject("Side activity coordinator check");
            try
            {
                var coordinator = root.AddComponent<HatchHomeCoordinator>();
                var sequence = root.AddComponent<HatchSequenceDirector>();
                var store = new MemoryStore();
                var clock = new FixedClock();
                coordinator.Configure(store, new HatchProgressService(store, clock),
                    new HatchSelectionService(store, new UnityArtIdRandomSource()), null, sequence, null, clock);
                coordinator.Initialize();
                var events = 0; coordinator.SnapshotChanged += _ => events++;
                var before = JsonUtility.ToJson(coordinator.Snapshot);
                store.Fail = true;
                Require(!coordinator.ApplySideActivity(HomeSideActivityAction.DailyGift).Accepted &&
                    JsonUtility.ToJson(coordinator.Snapshot) == before && events == 0, "coordinator failed save must not swap/publish");
                store.Fail = false;
                Require(coordinator.ApplySideActivity(HomeSideActivityAction.DailyGift).Accepted &&
                    coordinator.Snapshot.coins == 500 && events == 1, "coordinator swaps once after successful save");
                var result = coordinator.ApplySideActivity(HomeSideActivityAction.ToggleSound);
                result.Snapshot.selectedArtId = 29;
                Require(coordinator.Snapshot.selectedArtId == 0, "returned result cannot mutate owned snapshot");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CodexIsPreviewOnly()
        {
            var reference = UnityEngine.Object.FindObjectOfType<BigimongReferenceUi>(true);
            var coordinator = (HatchHomeCoordinator)Field(reference, "hatchHomeCoordinator");
            var before = coordinator.Snapshot == null ? null : JsonUtility.ToJson(coordinator.Snapshot);
            var preview = (int)Field(reference, "codexPreviewArtId");
            var flow = (OfflineBetaFlowController)Field(reference, "flow");
            var battleArt = flow.SelectedArtId;
            typeof(BigimongReferenceUi).GetMethod("BrowseCodex", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(reference, new object[] { 1 });
            Require((int)Field(reference, "codexPreviewArtId") == preview % 30 + 1, "codex preview changes");
            Require((coordinator.Snapshot == null ? null : JsonUtility.ToJson(coordinator.Snapshot)) == before, "codex cannot change ownership");
            Require(flow.SelectedArtId == battleArt, "codex cannot change AR battle selection");
            typeof(BigimongReferenceUi).GetMethod("BrowseCodex", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(reference, new object[] { -1 });
        }

        private static void RejectedResultsAreIsolated()
        {
            var root = new GameObject("Rejected result ownership check");
            try
            {
                var coordinator = root.AddComponent<HatchHomeCoordinator>();
                var store = new MemoryStore(); var clock = new FixedClock();
                coordinator.Configure(store, new HatchProgressService(store, clock), new HatchSelectionService(store, new UnityArtIdRandomSource()),
                    null, root.AddComponent<HatchSequenceDirector>(), null, clock);
                coordinator.Initialize();
                coordinator.ApplySideActivity(HomeSideActivityAction.DayRollover);
                var published = 0; coordinator.SnapshotChanged += _ => published++;
                foreach (var action in new[] { HomeSideActivityAction.Feed, HomeSideActivityAction.DayRollover,
                    HomeSideActivityAction.DailyGift, HomeSideActivityAction.ToggleSound, (HomeSideActivityAction)999 })
                {
                    store.Fail = action == HomeSideActivityAction.DailyGift;
                    SetField(coordinator, "<InputLocked>k__BackingField", action == HomeSideActivityAction.ToggleSound);
                    var liveBefore = JsonUtility.ToJson(coordinator.Snapshot);
                    var durableBefore = JsonUtility.ToJson(store.Durable);
                    var result = coordinator.ApplySideActivity(action);
                    Require(!result.Accepted, "fixture action must reject " + action);
                    MutateResult(result.Snapshot);
                    Require(JsonUtility.ToJson(coordinator.Snapshot) == liveBefore && JsonUtility.ToJson(store.Durable) == durableBefore && published == 0,
                        "rejected/no-op/failed/locked result cannot mutate coordinator, store, or emit events: " + action);
                }
                var current = new HatchHomeSnapshot { dayKey = 2026259 };
                var before = JsonUtility.ToJson(current);
                store.Fail = true;
                foreach (var result in new[] { new HomeSideActivityResult(true, current), new HomeSideActivityResult(false, current),
                    new HomeSideActivityService(store).TryApply(current, HomeSideActivityAction.DailyGift, Now),
                    new HomeSideActivityService(store).TryApply(current, HomeSideActivityAction.DayRollover, Now) })
                {
                    MutateResult(result.Snapshot);
                    Require(JsonUtility.ToJson(current) == before, "every outward result must detach its snapshot");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void MutateResult(HatchHomeSnapshot snapshot)
        {
            snapshot.eggProgress = 30000; snapshot.phase = "HOME"; snapshot.selectedArtId = 29;
            snapshot.hatchCheckpoint = "REVEALED"; snapshot.hatchedAtUtcTicks = 12345;
            snapshot.processedStepEvents.Add("foreign");
            snapshot.stepProviderCursors.Add(new StepProviderCursor { providerId = "foreign", dayKey = 1, cumulativeTotal = 900 });
        }

        private static void PortraitNavigationTouchTargets(HatchHomeView sceneView)
        {
            var copy = UnityEngine.Object.Instantiate(sceneView);
            try
            {
                copy.GetComponent<CanvasScaler>().enabled = false;
                copy.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var safe = (RectTransform)copy.transform.Find("Safe Area");
                safe.GetComponent<DeviceSafeArea>().enabled = false;
                foreach (var height in new[] { 1920f, 2340f, 2400f })
                    foreach (var safeInset in new[] { 0f, 72f })
                    {
                        var root = (RectTransform)copy.transform;
                        root.sizeDelta = new Vector2(1080, height);
                        safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
                        safe.offsetMin = new Vector2(0, safeInset); safe.offsetMax = new Vector2(0, -safeInset);
                        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
                        foreach (var button in (Button[])Field(copy, "navigationButtons"))
                        {
                            var size = ((RectTransform)button.transform).rect.size / 3f;
                            Require(size.x >= 44f && size.y >= 44f, "44dp navigation target at 360x" + height / 3f + ": " + button.name + " " + size);
                        }
                        var viewport = (RectTransform)safe.Find("Stage Viewport");
                        Require(Mathf.Abs(viewport.rect.height / safe.rect.height - .68f) < .001f, "portrait stage retains 68 percent");
                    }
            }
            finally { UnityEngine.Object.DestroyImmediate(copy.gameObject); }
        }

        private static void RetainedOverlayInputOwnership(HatchHomeView sceneView)
        {
            var reference = (BigimongReferenceUi)Field(sceneView, "referenceUi");
            var groups = (CanvasGroup[])Field(reference, "retainedOverlayGroups");
            Require(groups != null && groups.Length == 7 && groups.All(group => group != null), "all seven retained interactive canvases serialized");
            var expected = new[] { "Avatar Creator", "Avatar Editor Entry", "Beta Pet Selection", "Tyrannosaurus Encounter", "Arena Scan Guidance", "Beta Battle Result", "AR Battle HUD" };
            Require(groups.Select(group => group.name).SequenceEqual(expected), "literal retained canvas owners");
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
                if (canvas != sceneView.GetComponent<Canvas>() && canvas.gameObject != reference.gameObject && canvas.GetComponentsInChildren<Selectable>(true).Length > 0)
                    Require(groups.Any(group => canvas.transform.IsChildOf(group.transform)), "unowned interactive canvas " + canvas.name);
            var copy = UnityEngine.Object.Instantiate(reference);
            var home = UnityEngine.Object.Instantiate(sceneView);
            var flowObject = new GameObject("Retained overlay flow fixture");
            var clones = groups.Select(group => UnityEngine.Object.Instantiate(group.gameObject).GetComponent<CanvasGroup>()).ToArray();
            try
            {
                var flow = flowObject.AddComponent<OfflineBetaFlowController>();
                SetField(flow, "<IsActive>k__BackingField", true);
                SetField(flow, "<Phase>k__BackingField", OfflineBetaPhase.PetTestSelect);
                SetField(copy, "flow", flow); SetField(copy, "hatchHomeView", home); SetField(copy, "retainedOverlayGroups", clones);
                SetField(home, "referenceUi", copy); SetField(home, "flow", flow); SetField(home, "initialized", true);
                var coordinator = home.GetComponent<HatchHomeCoordinator>(); var store = new MemoryStore(); var clock = new FixedClock();
                coordinator.Configure(store, new HatchProgressService(store, clock), new HatchSelectionService(store, new UnityArtIdRandomSource()),
                    null, home.GetComponent<HatchSequenceDirector>(), null, clock);
                coordinator.Initialize(); SetField(copy, "hatchHomeCoordinator", coordinator);
                InvokePrivate(home, "Awake"); InvokePrivate(copy, "Awake");
                foreach (var group in clones) { group.gameObject.SetActive(true); group.interactable = group.blocksRaycasts = true; }
                var entry = clones[1].GetComponentInChildren<Button>(true);
                EventSystem.current.SetSelectedGameObject(entry.gameObject);
                home.Show();
                Require(EventSystem.current.currentSelectedGameObject != entry.gameObject, "home clears returning-player editor focus");
                Require(clones.All(group => !group.interactable && !group.blocksRaycasts), "home suspends every retained owner");
                foreach (var selectable in clones.SelectMany(group => group.GetComponentsInChildren<Selectable>(true)))
                    Require(!selectable.IsInteractable(), "underlying keyboard navigation disabled: " + selectable.name);
                foreach (var button in home.GetComponentsInChildren<Button>(true))
                    foreach (var next in new[] { button.FindSelectableOnDown(), button.FindSelectableOnUp(), button.FindSelectableOnLeft(), button.FindSelectableOnRight() })
                        Require(next == null || next.transform.IsChildOf(home.transform), "home keyboard navigation cannot reach retained controls");
                var clicks = 0; entry.onClick.AddListener(() => clicks++);
                ExecuteEvents.Execute(entry.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Require(clicks == 0, "hidden editor cannot receive keyboard submit");
                foreach (var graphic in clones.SelectMany(group => group.GetComponentsInChildren<Graphic>(true)))
                    Require(!graphic.Raycast(Vector2.zero, null), "retained raycast blocked: " + graphic.name);
                InvokePrivate(home, "SetInputLocked", true);
                Require(home.GetComponentsInChildren<Button>(true).All(button => !button.interactable) && clones.All(group => !group.interactable && !group.blocksRaycasts), "hatching has no underlying selectable");
                home.Hide();
                Require(clones.All(group => !group.interactable && !group.blocksRaycasts), "hide alone cannot restore retained input");
                SetField(flow, "<Phase>k__BackingField", OfflineBetaPhase.Battle);
                SetField(coordinator, "<InputLocked>k__BackingField", true);
                InvokePrivate(copy, "Hide");
                Require(clones.All(group => !group.interactable && !group.blocksRaycasts), "hatch lock overrides an underlying battle route");
                SetField(coordinator, "<InputLocked>k__BackingField", false);
                InvokePrivate(copy, "Hide");
                Require(clones[6].interactable && clones[6].blocksRaycasts && clones.Take(6).All(group => !group.interactable && !group.blocksRaycasts), "battle route restores only battle owner");
                Require(EventSystem.current.currentSelectedGameObject == null || EventSystem.current.currentSelectedGameObject.transform.IsChildOf(clones[6].transform), "restored focus belongs only to authorized battle owner");
                SetField(flow, "<Phase>k__BackingField", OfflineBetaPhase.ArScan);
                InvokePrivate(copy, "ApplyRetainedInputPolicy");
                Require(clones[4].interactable && clones[6].interactable && !clones[1].interactable && !clones[2].interactable, "scan restores only scan and battle owners");
                foreach (var route in new[]
                {
                    (OfflineBetaPhase.AvatarCreate, new[] { 0 }), (OfflineBetaPhase.PetTestSelect, new[] { 1, 2 }),
                    (OfflineBetaPhase.TyrannosaurEncounter, new[] { 3 }), (OfflineBetaPhase.ArScan, new[] { 4, 6 }),
                    (OfflineBetaPhase.SummonSequence, new[] { 6 }), (OfflineBetaPhase.Battle, new[] { 6 }),
                    (OfflineBetaPhase.Result, new[] { 5, 6 })
                })
                {
                    SetField(flow, "<Phase>k__BackingField", route.Item1);
                    InvokePrivate(copy, "ApplyRetainedInputPolicy");
                    for (var index = 0; index < clones.Length; index++)
                        Require(clones[index].interactable == route.Item2.Contains(index) && clones[index].blocksRaycasts == route.Item2.Contains(index),
                            "literal retained restore matrix " + route.Item1 + "/" + index);
                    var focus = EventSystem.current.currentSelectedGameObject;
                    Require(focus == null || route.Item2.Any(index => focus.transform.IsChildOf(clones[index].transform)), "focus follows authorized route only");
                }
                RetainedFocusSurvivesRepeatedHide(copy, home, flow, coordinator, clones);
                RealSummonToBattleRestoresFocus(copy, clones);
                SetField(flow, "<IsActive>k__BackingField", false);
                clones[6].gameObject.SetActive(false);
                InvokePrivate(copy, "ApplyRetainedInputPolicy");
                Require(clones.All(group => !group.interactable && !group.blocksRaycasts), "inactive flow alone does not authorize retained input");
                clones[6].gameObject.SetActive(true);
                InvokePrivate(copy, "ApplyRetainedInputPolicy");
                Require(clones[6].interactable && clones.Take(6).All(group => !group.interactable), "host handoff restores only AR owner");
                InvokePrivate(copy, "Show", "shop");
                Require(clones.All(group => !group.interactable && !group.blocksRaycasts), "secondary reference route suspends retained owners");
            }
            finally
            {
                EventSystem.current.SetSelectedGameObject(null);
                foreach (var group in clones) UnityEngine.Object.DestroyImmediate(group.gameObject);
                UnityEngine.Object.DestroyImmediate(copy.gameObject); UnityEngine.Object.DestroyImmediate(home.gameObject);
                UnityEngine.Object.DestroyImmediate(flowObject);
            }
        }

        private static void RetainedFocusSurvivesRepeatedHide(BigimongReferenceUi reference, HatchHomeView home,
            OfflineBetaFlowController flow, HatchHomeCoordinator coordinator, CanvasGroup[] groups)
        {
            SetField(reference, "started", true);
            SetField(reference, "screen", "battle-loading");
            // Keep cloned UI interactions local; no gameplay/AR listeners are needed for this focus regression.
            foreach (var button in groups.SelectMany(group => group.GetComponentsInChildren<Button>(true)))
                button.onClick = new Button.ButtonClickedEvent();
            foreach (var route in new[]
            {
                (OfflineBetaPhase.Battle, 6, "Left", "Center", MoveDirection.Right, Vector2.right),
                (OfflineBetaPhase.Result, 5, "Rematch", "Return to Pet Selection", MoveDirection.Down, Vector2.down)
            })
            {
                SetField(flow, "<Phase>k__BackingField", route.Item1);
                InvokePrivate(reference, "Update");
                var buttons = groups[route.Item2].GetComponentsInChildren<Button>(true);
                var first = buttons.Single(button => button.name == route.Item3);
                var selected = buttons.Single(button => button.name == route.Item4);
                Require(first != selected && first.IsActive() && first.IsInteractable() && selected.IsActive() && selected.IsInteractable(),
                    "real retained first/non-first controls are available: " + route.Item1);
                var firstActions = 0; var selectedActions = 0;
                first.onClick.AddListener(() => firstActions++);
                selected.onClick.AddListener(() => selectedActions++);
                EventSystem.current.SetSelectedGameObject(first.gameObject);
                ExecuteEvents.Execute(first.gameObject, new AxisEventData(EventSystem.current)
                { moveDir = route.Item5, moveVector = route.Item6 }, ExecuteEvents.moveHandler);
                Require(EventSystem.current.currentSelectedGameObject == selected.gameObject, "real directional navigation reaches non-first " + route.Item1 + " control");
                for (var frame = 0; frame < 4; frame++)
                {
                    home.Hide();
                    reference.ReleaseHomeInputOwnership();
                    InvokePrivate(reference, "Hide");
                    InvokePrivate(reference, "Update");
                    Require(EventSystem.current.currentSelectedGameObject == selected.gameObject && selected.IsInteractable(),
                        "repeated real Hide/Update preserves non-first " + route.Item1 + " selection at frame " + frame);
                }
                ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Require(selectedActions == 1 && firstActions == 0, "Submit dispatches exactly once to selected retained action, never the first action");
                EventSystem.current.SetSelectedGameObject(null);
                InvokePrivate(reference, "Hide"); InvokePrivate(reference, "Update");
                Require(EventSystem.current.currentSelectedGameObject == null, "unchanged policy does not rerun initial-focus restoration");
                EventSystem.current.SetSelectedGameObject(selected.gameObject);
            }
            SetField(flow, "<Phase>k__BackingField", OfflineBetaPhase.Battle);
            InvokePrivate(reference, "Update");
            Require(!groups[5].interactable && groups[6].interactable && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(groups[6].transform), "result-to-battle route clears unauthorized result focus and restores battle owner");
            home.Show();
            Require(groups.All(group => !group.interactable && !group.blocksRaycasts) &&
                EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(home.transform), "actual home claim clears retained focus");
            reference.ClaimHomeInputOwnership();
            home.Hide();
            Require(groups.All(group => !group.interactable && !group.blocksRaycasts) && EventSystem.current.currentSelectedGameObject == null,
                "actual home release clears home focus without authorizing another owner");
            InvokePrivate(reference, "Update");
            Require(groups[6].interactable && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(groups[6].transform), "actual home transition restores authorized battle focus once");
            SetField(coordinator, "<InputLocked>k__BackingField", true);
            InvokePrivate(reference, "Update");
            Require(groups.All(group => !group.interactable && !group.blocksRaycasts) && EventSystem.current.currentSelectedGameObject == null,
                "hatch lock invalidates retained policy even with home already hidden");
            SetField(coordinator, "<InputLocked>k__BackingField", false);
            InvokePrivate(reference, "Update");
            Require(groups[6].interactable && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(groups[6].transform), "hatch unlock restores the authorized owner");
            groups[6].gameObject.SetActive(false);
            EventSystem.current.SetSelectedGameObject(null);
            InvokePrivate(reference, "Update");
            groups[6].gameObject.SetActive(true);
            InvokePrivate(reference, "Update");
            Require(EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(groups[6].transform), "late retained canvas activation restores focus without a phase change");
        }

        private static void RealSummonToBattleRestoresFocus(BigimongReferenceUi reference, CanvasGroup[] groups)
        {
            var root = new GameObject("Real summon-to-battle focus fixture");
            var previousFlow = Field(reference, "flow");
            var active = groups.Select(group => group.gameObject.activeSelf).ToArray();
            var selectionPath = System.IO.Path.Combine(Application.persistentDataPath, "beta-pet-selection-v1.json");
            var selectionExisted = System.IO.File.Exists(selectionPath);
            var selectionBytes = selectionExisted ? System.IO.File.ReadAllBytes(selectionPath) : null;
            var storeField = typeof(AvatarProfileStore).GetField("defaultStore", BindingFlags.Static | BindingFlags.NonPublic);
            var savedField = typeof(AvatarProfileStore).GetField("<HasSavedProfile>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var previousStore = storeField.GetValue(null); var previousSaved = savedField.GetValue(null);
            EventTrigger probe = null;
            try
            {
                // Use a never-written profile path and restore the separately persisted pet-selection bytes.
                storeField.SetValue(null, new AvatarProfileStore(System.IO.Path.Combine(Application.temporaryCachePath, "focus-profile-" + Guid.NewGuid() + ".json")));
                var flow = root.AddComponent<OfflineBetaFlowController>();
                var demo = root.AddComponent<ArBattleOfflineDemo>();
                var hud = groups[6].GetComponent<ArBattleHud>();
                SetField(hud, "bridge", null);
                InvokePrivate(hud, "Awake");
                InvokePrivate(flow, "Unbind");
                SetField(flow, "hud", hud); SetField(flow, "offlineDemo", demo); SetField(demo, "hud", hud);
                SetField(flow, "petCanvas", groups[2].gameObject); SetField(flow, "encounterCanvas", groups[3].gameObject);
                SetField(flow, "scanCanvas", groups[4].gameObject); SetField(flow, "resultCanvas", groups[5].gameObject);
                SetField(flow, "battleCanvas", groups[6].gameObject);
                SetField(reference, "flow", flow); SetField(reference, "started", true); SetField(reference, "screen", "battle-loading");
                var left = (Button)Field(hud, "leftButton"); var center = (Button)Field(hud, "centerButton");
                var surrender = (Button)Field(hud, "surrenderButton");
                var initialFocusCount = 0;
                probe = left.gameObject.AddComponent<EventTrigger>();
                var selectedEvent = new EventTrigger.Entry { eventID = EventTriggerType.Select };
                selectedEvent.callback.AddListener(_ => initialFocusCount++); probe.triggers.Add(selectedEvent);

                flow.StartBeta();
                Require(flow.Phase == OfflineBetaPhase.AvatarCreate, "isolated profile enters actual avatar phase");
                InvokePrivate(flow, "OnAvatarSaved", AvatarProfile.CreateDefault());
                flow.PreviewEncounter(); flow.AcceptEncounter();
                Require(flow.Phase == OfflineBetaPhase.ArScan && demo.IsActive, "real encounter starts the offline demo");
                InvokePrivate(demo, "OnArenaPlaced", (object)null); // Actual BattlePrepared event drives the flow callback.
                Require(flow.Phase == OfflineBetaPhase.SummonSequence && hud.InputLocked, "real preparation enters summoning with HUD locked");
                EventSystem.current.SetSelectedGameObject(null);
                InvokePrivate(hud, "Update"); InvokePrivate(reference, "Update"); InvokePrivate(reference, "Update");
                Require(EventSystem.current.currentSelectedGameObject == null && !left.IsActive() && !center.IsActive() && !surrender.IsActive(),
                    "summoning has no eligible retained control or focus");

                InvokePrivate(flow, "OnSummoningCompleted"); // Real callback enables demo turns and applies Battle HUD state.
                Require(flow.Phase == OfflineBetaPhase.Battle && !hud.InputLocked, "summon completion enters actual battle");
                InvokePrivate(hud, "Update"); InvokePrivate(reference, "Update");
                Require(left.IsInteractable() && center.IsInteractable() && surrender.IsInteractable() &&
                    (int)Field(reference, "appliedRetainedPhase") == (int)OfflineBetaPhase.Battle &&
                    EventSystem.current.currentSelectedGameObject == left.gameObject && initialFocusCount == 1,
                    "next real update selects newly available battle control exactly once");
                for (var frame = 0; frame < 3; frame++) { InvokePrivate(hud, "Update"); InvokePrivate(reference, "Update"); }
                Require(EventSystem.current.currentSelectedGameObject == left.gameObject && initialFocusCount == 1, "stable battle frames do not repeat initial focus");
                ExecuteEvents.Execute(left.gameObject, new AxisEventData(EventSystem.current)
                { moveDir = MoveDirection.Right, moveVector = Vector2.right }, ExecuteEvents.moveHandler);
                for (var frame = 0; frame < 3; frame++) { InvokePrivate(hud, "Update"); InvokePrivate(reference, "Update"); }
                Require(EventSystem.current.currentSelectedGameObject == center.gameObject && initialFocusCount == 1, "real battle non-first focus survives stable frames");
                EventSystem.current.SetSelectedGameObject(null);
                InvokePrivate(hud, "Update"); InvokePrivate(reference, "Update");
                Require(EventSystem.current.currentSelectedGameObject == null && initialFocusCount == 1, "intentional null focus survives stabilized battle");

                hud.SetInputLocked(true); hud.SetSurrenderVisible(false);
                InvokePrivate(hud, "Update"); InvokePrivate(reference, "Update");
                Require(EventSystem.current.currentSelectedGameObject == null, "same-phase HUD lock removes eligibility");
                hud.SetInputLocked(false); hud.SetSurrenderVisible(true); hud.BeginTurn(10000, 0);
                InvokePrivate(hud, "Update"); InvokePrivate(reference, "Update");
                Require(flow.Phase == OfflineBetaPhase.Battle && EventSystem.current.currentSelectedGameObject == left.gameObject && initialFocusCount == 2,
                    "same-phase actual control activation restores focus once");
                hud.BeginTurn(0, 0); InvokePrivate(hud, "Update"); InvokePrivate(reference, "Update");
                Require(left.IsActive() && !left.interactable && EventSystem.current.currentSelectedGameObject == surrender.gameObject,
                    "disabled selected control yields focus to the remaining eligible control");
                EventSystem.current.SetSelectedGameObject(null); InvokePrivate(reference, "Update");
                Require(EventSystem.current.currentSelectedGameObject == null, "stable disabled-control signature preserves intentional null");
                hud.BeginTurn(10000, 0); InvokePrivate(hud, "Update"); InvokePrivate(reference, "Update");
                Require(EventSystem.current.currentSelectedGameObject == left.gameObject && initialFocusCount == 3,
                    "same-phase interactable re-enable restores first eligible control once");
                EventSystem.current.SetSelectedGameObject(null);
                InvokePrivate(hud, "Update"); InvokePrivate(reference, "Update");
                Require(EventSystem.current.currentSelectedGameObject == null && initialFocusCount == 3, "re-enabled stable frames do not poll-reselect");
            }
            finally
            {
                EventSystem.current.SetSelectedGameObject(null);
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
                UnityEngine.Object.DestroyImmediate(root);
                SetField(reference, "flow", previousFlow);
                for (var index = 0; index < groups.Length; index++) groups[index].gameObject.SetActive(active[index]);
                storeField.SetValue(null, previousStore); savedField.SetValue(null, previousSaved);
                if (selectionExisted) System.IO.File.WriteAllBytes(selectionPath, selectionBytes);
                else if (System.IO.File.Exists(selectionPath)) System.IO.File.Delete(selectionPath);
            }
        }

        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void InvokePrivate(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        private sealed class FixedClock : ICareClock { public DateTime UtcNow => Now; }
        private sealed class MemoryStore : IHatchHomeStore
        {
            public bool Fail;
            public HatchHomeSnapshot Durable = new();
            public HatchLoadResult Load() => new(Durable.Clone(), false, "test");
            public bool TrySave(HatchHomeSnapshot snapshot) { if (Fail) return false; Durable = snapshot.Clone(); return true; }
        }
    }
}
#endif
