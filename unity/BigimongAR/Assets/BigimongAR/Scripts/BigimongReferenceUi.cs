using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bigimong.AR
{
    /// <summary>Uses the supplied portrait artwork, with local game controls layered over it.</summary>
    public sealed class BigimongReferenceUi : MonoBehaviour
    {
        private const float Width = 1030f;
        private const float Height = 1536f;
        private static readonly string[] ArtScreens =
        {
            "ReferenceUi/loading", "ReferenceUi/battle-loading", "ReferenceUi/avatar",
            "ReferenceUi/egg", "ReferenceUi/home"
        };
        private static readonly Color Cocoa = new Color(.22f, .12f, .08f, .97f);
        private static readonly Color Cream = new Color(1f, .94f, .78f);
        private static readonly Color Gold = new Color(1f, .70f, .13f);
        private static readonly Color Orange = new Color(.97f, .42f, .14f);
        [SerializeField] private OfflineBetaFlowController flow;
        [SerializeField] private HatchHomeView hatchHomeView;
        [SerializeField] private HatchHomeCoordinator hatchHomeCoordinator;
        [SerializeField] private BigimongTypographyTheme theme;
        // Ordered owners: creator, editor entry, pet selection, encounter, scan, result, battle.
        [SerializeField] private CanvasGroup[] retainedOverlayGroups = Array.Empty<CanvasGroup>();
        private bool homeInputOwned;
        private int appliedRetainedInputMask = -1;
        private int appliedRetainedActiveMask = -1;
        private int appliedRetainedPhase = -2;
        private string appliedRetainedScreen;
        private List<Selectable> appliedRetainedFocusControls = new();
        private List<Selectable> retainedFocusScratch = new();
        private readonly List<Selectable> retainedSelectableScratch = new();
        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private RectTransform board;
        private RawImage artwork;
        private RectTransform marker;
        private TMP_Text statusNotice;
        private GameObject noticePanel;
        private GameObject dialogue;
        private TMP_Text dialogueTitle;
        private TMP_Text dialogueBody;
        private HatchHomeSnapshot progress => hatchHomeCoordinator.Snapshot;
        private bool Hatched => progress != null && progress.Phase is HatchHomePhase.REVEAL or HatchHomePhase.HOME;
        private int Level => Mathf.Clamp(1 + progress.dragonExperience / 1000, 1, 30);
        private string GrowthStage => Level >= 20 ? "성장기" : Level >= 10 ? "청소년기" : "아동기";
        private int codexPreviewArtId = 1;
        private string screen;
        private bool started;
        private bool girl;
        private bool galleryVisible = true;
        private bool dialogueFromHome;
        private float nextScreenAt;
        public string CurrentScreen => screen;

        public static string ResolveDestination(string source, string action)
        {
            switch (action)
            {
                case "홈": return "home";
                case "설정": return "settings";
                case "상점": return "shop";
                case "놀아주기": return "play";
                case "1:1 대전": return "battle-loading";
                case "도감": return "codex";
                case "퀘스트": return "quest";
                case "선물": return "gift";
                case "캐릭터": return "character";
                case "남자": case "여자": return "avatar";
                case "선택 완료": return source == "avatar" ? "egg" : source;
                case "알 닦기": return source == "egg" ? "egg" : source;
                case "뒤로가기": return source == "egg" ? "avatar" : "home";
                default: return source;
            }
        }

        private void Awake()
        {
            if (canvas != null) return;
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.interactable = canvasGroup.blocksRaycasts = false;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height);
            gameObject.AddComponent<GraphicRaycaster>();
            var portrait = new GameObject("Reference Portrait", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            portrait.transform.SetParent(transform, false);
            board = portrait.GetComponent<RectTransform>();
            board.anchorMin = Vector2.zero;
            board.anchorMax = Vector2.one;
            board.offsetMin = board.offsetMax = Vector2.zero;
            var fitter = portrait.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = Width / Height;
            artwork = portrait.GetComponent<RawImage>();
            artwork.raycastTarget = false;
            var markerObject = new GameObject("Avatar Selection Marker", typeof(RectTransform), typeof(Image));
            markerObject.transform.SetParent(board, false);
            marker = markerObject.GetComponent<RectTransform>();
            markerObject.GetComponent<Image>().color = new Color(.09f, .88f, .99f, .32f);
            markerObject.GetComponent<Image>().raycastTarget = false;
            markerObject.SetActive(false);
            CreateNotice();
            CreateDialogue();
            canvas.enabled = false;
        }

        private void OnEnable()
        {
            if (flow != null) flow.HomeRequested += OnHomeRequested;
            SuspendRetainedOverlayInput();
        }

        private void OnDisable()
        {
            if (flow != null) flow.HomeRequested -= OnHomeRequested;
        }

        private void OnHomeRequested()
        {
            ShowHome();
        }

        private void Update()
        {
            if (flow == null || !flow.IsActive)
            {
                if (started) { Hide(); started = false; }
                ApplyRetainedInputPolicy();
                return;
            }
            if (!started)
            {
                started = true;
                hatchHomeView.EnsureInitialized();
                AudioListener.volume = progress.soundEnabled ? 1f : 0f;
                girl = AvatarProfileStore.Load().bodyType == "FEMININE";
                Show("loading");
                nextScreenAt = Time.unscaledTime + 1f;
            }
            if (screen == "loading" && Time.unscaledTime >= nextScreenAt)
                ShowHome();
            if (screen == "battle-loading" && canvas.enabled && Time.unscaledTime >= nextScreenAt)
            {
                Hide();
                if (flow.Phase == OfflineBetaPhase.PetTestSelect)
                {
                    flow.SelectPet(progress.selectedArtId);
                    flow.PreviewEncounter();
                    flow.AcceptEncounter();
                }
            }
            if (flow.Phase == OfflineBetaPhase.ArScan || flow.Phase == OfflineBetaPhase.SummonSequence ||
                flow.Phase == OfflineBetaPhase.Battle || flow.Phase == OfflineBetaPhase.Result)
                Hide();
            if (!canvas.enabled && screen == "battle-loading" && flow.Phase == OfflineBetaPhase.PetTestSelect &&
                Time.unscaledTime > nextScreenAt + 1f)
                ShowHome();
            if ((canvas.enabled || hatchHomeView.IsVisible) && Input.GetKeyDown(KeyCode.Escape)) Activate("뒤로가기");
        }

        private static bool IsArtwork(string target)
        {
            return target == "loading" || target == "battle-loading" || target == "avatar" ||
                   target == "egg" || target == "home";
        }

        private void ShowHome()
        {
            if (progress.Phase is HatchHomePhase.HATCHING or HatchHomePhase.REVEAL)
            {
                Show(progress.Phase == HatchHomePhase.HATCHING ? "egg" : "home");
                return; // Durable reveal recovery precedes optional avatar/profile navigation.
            }
            Show(!AvatarProfileStore.HasSavedProfile ? "avatar" : Hatched ? "home" : "egg");
        }

        private void Show(string target)
        {
            SuspendRetainedOverlayInput();
            screen = target;
            if (target == "egg" || target == "home")
            {
                canvas.enabled = false;
                canvasGroup.interactable = canvasGroup.blocksRaycasts = false;
                artwork.texture = null;
                dialogue.SetActive(false);
                hatchHomeView.Show();
                return;
            }
            hatchHomeView.Hide();
            var art = IsArtwork(target) ? target : "loading";
            var path = "ReferenceUi/" + art;
            if (Array.IndexOf(ArtScreens, path) < 0)
            {
                Debug.LogError("Unknown reference screen: " + target, this);
                return;
            }
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogError("Missing Bigimong reference artwork: " + art, this);
                Hide();
                return;
            }
            artwork.texture = IsArtwork(target) ? texture : null;
            artwork.color = IsArtwork(target) ? Color.white : Cream;
            canvas.enabled = true;
            canvasGroup.interactable = canvasGroup.blocksRaycasts = true;
            noticePanel.SetActive(target != "loading" && target != "battle-loading");
            statusNotice.text = "오프라인 게임 · 저장된 진행 상태";
            dialogue.SetActive(false);
            marker.gameObject.SetActive(false);
            for (var i = board.childCount - 1; i >= 0; i--)
            {
                var child = board.GetChild(i).gameObject;
                if (child.name.StartsWith("Hotspot:", StringComparison.Ordinal) ||
                    child.name.StartsWith("Game:", StringComparison.Ordinal))
                {
                    child.SetActive(false);
                    if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
                }
            }
            if (target == "avatar") CreateAvatarCursorRepair(texture);
            if (target == "loading" || target == "battle-loading") return;
            Nav("뒤로가기", 10, 10, 120, 120);
            Nav("설정", 897, 7, 120, 120);
            Nav("뒤로가기", 8, 1395, 143, 137);
            Nav("설정", 892, 1390, 133, 140);
            if (target == "avatar")
            {
                Nav("남자", 195, 1045, 280, 210);
                Nav("여자", 594, 1045, 302, 210);
                Nav("선택 완료", 273, 1339, 488, 190);
                Nav("선물", 773, 1170, 234, 195);
                UpdateMarker();
                FocusFirstControl();
                return;
            }
            Nav("상점", 12, 150, 212, 211);
            Nav("놀아주기", 12, 365, 215, 200);
            Nav("홈", 217, 1380, 168, 155);
            Nav("도감", 386, 1380, 169, 155);
            Nav("퀘스트", 555, 1380, 170, 155);
            Nav("선물", 706, 1380, 180, 155);
            BuildPage(target);
            FocusFirstControl();
        }

        private void Hide()
        {
            if (canvas != null) canvas.enabled = false;
            if (canvasGroup != null) SetOverlayInput(canvasGroup, false);
            hatchHomeView?.Hide();
            ApplyRetainedInputPolicy();
        }

        public void ClaimHomeInputOwnership()
        {
            if (homeInputOwned) return;
            homeInputOwned = true;
            if (canvas != null) canvas.enabled = false;
            if (canvasGroup != null) SetOverlayInput(canvasGroup, false);
            SuspendRetainedOverlayInput();
        }

        public void ReleaseHomeInputOwnership()
        {
            if (!homeInputOwned) return;
            homeInputOwned = false;
            // Releasing home is not authority to restore another screen. Show/Hide owns the next route.
            SuspendRetainedOverlayInput();
        }

        public void SuspendRetainedOverlayInput()
        {
            appliedRetainedInputMask = -1;
            foreach (var group in retainedOverlayGroups) SetOverlayInput(group, false);
        }

        private void ApplyRetainedInputPolicy()
        {
            var blocked = homeInputOwned || (canvas != null && canvas.enabled) ||
                (hatchHomeView != null && hatchHomeView.IsVisible) || (hatchHomeCoordinator != null && hatchHomeCoordinator.InputLocked);
            var inputMask = 0;
            var activeMask = 0;
            for (var index = 0; index < retainedOverlayGroups.Length; index++)
            {
                if (retainedOverlayGroups[index] != null && retainedOverlayGroups[index].gameObject.activeInHierarchy)
                    activeMask |= 1 << index;
                var allowed = false;
                if (!blocked && flow != null)
                {
                    if (!flow.IsActive) allowed = index == 6 && retainedOverlayGroups[index] != null &&
                        retainedOverlayGroups[index].gameObject.activeInHierarchy; // Only the HUD activated by the Android host.
                    else allowed = index switch
                    {
                        0 => flow.Phase == OfflineBetaPhase.AvatarCreate,
                        1 or 2 => flow.Phase == OfflineBetaPhase.PetTestSelect,
                        3 => flow.Phase == OfflineBetaPhase.TyrannosaurEncounter,
                        4 => flow.Phase == OfflineBetaPhase.ArScan,
                        5 => flow.Phase == OfflineBetaPhase.Result,
                        6 => flow.Phase is OfflineBetaPhase.ArScan or OfflineBetaPhase.SummonSequence or OfflineBetaPhase.Battle or OfflineBetaPhase.Result,
                        _ => false
                    };
                }
                if (allowed) inputMask |= 1 << index;
            }
            var phase = flow != null && flow.IsActive ? (int)flow.Phase : -1;
            var ownershipChanged = inputMask != appliedRetainedInputMask || activeMask != appliedRetainedActiveMask ||
                phase != appliedRetainedPhase || screen != appliedRetainedScreen;
            if (ownershipChanged)
            {
                appliedRetainedInputMask = inputMask;
                appliedRetainedActiveMask = activeMask;
                appliedRetainedPhase = phase;
                appliedRetainedScreen = screen;
                for (var index = 0; index < retainedOverlayGroups.Length; index++)
                    SetOverlayInput(retainedOverlayGroups[index], (inputMask & (1 << index)) != 0);
            }
            // Sample the exact ordered eligible controls into reusable buffers, without per-frame LINQ/arrays.
            // Read after applying owner permissions: IsInteractable includes the owning CanvasGroup.
            retainedFocusScratch.Clear();
            for (var index = 0; index < retainedOverlayGroups.Length; index++)
            {
                var group = retainedOverlayGroups[index];
                if ((inputMask & activeMask & (1 << index)) == 0 || group == null) continue;
                group.GetComponentsInChildren(false, retainedSelectableScratch);
                foreach (var selectable in retainedSelectableScratch)
                    if (selectable.IsActive() && selectable.IsInteractable() && selectable.navigation.mode != Navigation.Mode.None)
                        retainedFocusScratch.Add(selectable);
            }
            var availabilityChanged = retainedFocusScratch.Count != appliedRetainedFocusControls.Count;
            for (var index = 0; !availabilityChanged && index < retainedFocusScratch.Count; index++)
                availabilityChanged = retainedFocusScratch[index] != appliedRetainedFocusControls[index];
            if (!ownershipChanged && !availabilityChanged) return; // Preserve non-first and intentional null focus on stable frames.
            var previous = appliedRetainedFocusControls;
            appliedRetainedFocusControls = retainedFocusScratch;
            retainedFocusScratch = previous;
            if (EventSystem.current == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && !appliedRetainedFocusControls.Contains(selected.GetComponent<Selectable>()))
                foreach (var group in retainedOverlayGroups)
                    if (group != null && selected.transform.IsChildOf(group.transform))
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                        break;
                    }
            if (EventSystem.current.currentSelectedGameObject == null && appliedRetainedFocusControls.Count > 0)
                EventSystem.current.SetSelectedGameObject(appliedRetainedFocusControls[0].gameObject);
        }

        private static void SetOverlayInput(CanvasGroup group, bool allowed)
        {
            if (group == null) return;
            group.interactable = group.blocksRaycasts = allowed;
            if (!allowed && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(group.transform))
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void FocusFirstControl()
        {
            var button = board.GetComponentInChildren<Button>();
            if (EventSystem.current != null && button != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private void Nav(string action, float x, float y, float w, float h)
        {
            var obj = new GameObject("Hotspot: " + action, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(board, false);
            Layout(obj.GetComponent<RectTransform>(), x, y, w, h);
            var graphic = obj.GetComponent<Image>();
            graphic.color = new Color(1, 1, 1, .001f);
            var button = obj.GetComponent<Button>();
            button.targetGraphic = graphic;
            obj.AddComponent<ButtonPressMotion>();
            button.onClick.AddListener(() => Activate(action));
            if (screen != "avatar")
                Label("Navigation Label", obj.transform, action, 0, 0, w, h, 31, Cream, w, h);
        }

        private static void Layout(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = new Vector2(x / Width, 1f - (y + h) / Height);
            rect.anchorMax = new Vector2((x + w) / Width, 1f - y / Height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public void Activate(string action)
        {
            if ((!canvas.enabled && !hatchHomeView.IsVisible) || string.IsNullOrEmpty(screen) || hatchHomeCoordinator.InputLocked) return;
            if (dialogue.activeSelf && action != "뒤로가기") return;
            if (dialogue.activeSelf) { CloseDialogue(); return; }
            if (action == "남자" || action == "여자")
            {
                girl = action == "여자";
                UpdateMarker();
                return;
            }
            if (action == "선택 완료" && screen == "avatar")
            {
                var profile = AvatarProfileStore.Load();
                profile.bodyType = girl ? "FEMININE" : "MASCULINE";
                if (!flow.ApplyReferenceAvatar(profile))
                {
                    OpenDialogue("아바타 선택", "아바타를 저장하지 못했어요. 다시 시도해 주세요.");
                    return;
                }
                hatchHomeCoordinator.SetAvatarProfile(profile);
                Show(Hatched ? "home" : "egg");
                return;
            }
            if (HandleGameAction(action)) return;
            var destination = ResolveDestination(screen, action);
            if (destination == "battle-loading")
            {
                if (!AvatarProfileStore.HasSavedProfile || !Hatched || flow.Phase != OfflineBetaPhase.PetTestSelect)
                {
                    OpenDialogue("연습 대전", "아바타를 선택하고 알을 부화시킨 후 AR 연습 대전을 시작할 수 있어요.");
                    return;
                }
                Show(destination);
                nextScreenAt = Time.unscaledTime + 1.2f;
            }
            else if (destination == "home") ShowHome();
            else if (destination == "avatar" || destination == "egg") Show(destination);
            else if (destination != screen) Show(destination);
        }

        private bool HandleGameAction(string action)
        {
            HomeSideActivityAction? sideAction = action switch
            {
                "함께 놀기" when screen == "play" => HomeSideActivityAction.Play,
                "오늘의 선물 받기" when screen == "gift" => HomeSideActivityAction.DailyGift,
                "퀘스트 보상 받기" when screen == "quest" => HomeSideActivityAction.QuestReward,
                "간식 구매" when screen == "shop" => HomeSideActivityAction.BuySnack,
                "간식 주기" when screen == "character" => HomeSideActivityAction.Feed,
                "소리 전환" when screen == "settings" => HomeSideActivityAction.ToggleSound,
                _ => null
            };
            if (sideAction.HasValue)
            {
                var result = hatchHomeCoordinator.ApplySideActivity(sideAction.Value);
                if (result.Accepted)
                {
                    AudioListener.volume = progress.soundEnabled ? 1f : 0f;
                    Show(screen);
                }
                else OpenDialogue("알림", result.Error == "save_failed" ? "저장하지 못했어요. 다시 시도해 주세요." : sideAction.Value switch
                {
                    HomeSideActivityAction.Play => "놀아주기는 10초마다 할 수 있어요. 하루 최대 100번입니다.",
                    HomeSideActivityAction.DailyGift => "오늘 선물은 이미 받았어요. 내일 다시 만나요!",
                    HomeSideActivityAction.QuestReward => "오늘 함께 놀기 3회를 완료하면 보상을 받을 수 있어요.",
                    HomeSideActivityAction.BuySnack => "비기코인이 부족하거나 간식이 가득해요.",
                    HomeSideActivityAction.Feed => "부화한 비기몽과 간식이 필요해요. 최대 경험치는 29,000입니다.",
                    _ => "지금은 할 수 없어요."
                });
                return true;
            }
            if (action == "전체 도감" && screen == "codex")
            { galleryVisible = !galleryVisible; Show("codex"); return true; }
            if ((action == "이전 공룡" || action == "다음 공룡") && screen == "codex")
            { BrowseCodex(action == "다음 공룡" ? 1 : -1); Show("codex"); return true; }
            if (action == "아바타 바꾸기" && screen == "settings") { Show("avatar"); return true; }
            if (action == "연습 대전" && screen == "codex") { Activate("1:1 대전"); return true; }
            return false;
        }

        private void BrowseCodex(int direction)
        {
            galleryVisible = false;
            codexPreviewArtId = 1 + ((codexPreviewArtId - 1 + direction + 30) % 30);
        }

        private void BuildPage(string page)
        {
            var panel = Block("Game: " + page, board, 100, 295, 830, 1025, Cocoa);
            var title = page switch
            {
                "shop" => "상점", "play" => "놀아주기", "codex" => "비기몽 도감",
                "quest" => "오늘의 퀘스트", "gift" => "선물", "settings" => "설정",
                "character" => "내 비기몽", _ => "비기몽"
            };
            Label("Game: Page Title", panel.transform, title, 40, 36, 750, 90, 53, Gold);
            Block("Game: Gold Separator", panel.transform, 72, 137, 686, 7, Orange, 830, 1025);
            var today = DateTime.UtcNow;
            hatchHomeCoordinator.ApplySideActivity(HomeSideActivityAction.DayRollover);
            var wallet = progress.coins.ToString("N0");
            switch (page)
            {
                case "shop":
                    Label("Game: Wallet", panel.transform, "보유 비기코인  " + wallet, 45, 192, 740, 93, 38, Cream);
                    Label("Game: Shop Item", panel.transform,
                        "모험가의 간식  ·  300 코인\n보유 간식 " + progress.snacks + "개\n캐릭터 화면에서 주면 경험치 +500",
                        60, 350, 710, 260, 32, Cream);
                    ActionButton("간식 구매", panel.transform, 180, 704, 470, 110, "간식 구매", Gold, 830, 1025);
                    break;
                case "play":
                    Label("Game: Play Status", panel.transform,
                        Hatched ? BigimongSpeciesCatalog.Resolve(progress.selectedArtId).KoreanName + "와 놀기\n경험치 +200 · 비기코인 +50" :
                        "반짝이는 알과 놀기\n오늘의 놀이 기록을 쌓아요",
                        65, 251, 700, 270, 39, Cream);
                    Label("Game: Daily Play", panel.transform, "오늘 함께 논 횟수  " + progress.playsToday + " / 100",
                        65, 561, 700, 100, 31, Gold);
                    ActionButton("함께 놀기", panel.transform, 180, 721, 470, 114, "함께 놀기", Orange, 830, 1025);
                    break;
                case "gift":
                    Label("Game: Gift Status", panel.transform,
                        "매일 한 번 받는 선물\n비기코인 +500",
                        65, 249, 700, 285, 42, Cream);
                    ActionButton("오늘의 선물 받기", panel.transform, 105, 707, 620, 114,
                        progress.giftClaimedToday ? "오늘 수령 완료" : "오늘의 선물 받기", Gold, 830, 1025);
                    break;
                case "quest":
                    Label("Game: Quest Status", panel.transform,
                        "오늘 함께 놀기 3회\n진행 " + Mathf.Min(3, progress.playsToday) +
                        " / 3\n보상 비기코인 300",
                        65, 271, 700, 301, 39, Cream);
                    ActionButton("퀘스트 보상 받기", panel.transform, 105, 718, 620, 111,
                        progress.questClaimedToday ? "오늘 완료" : "퀘스트 보상 받기", Gold, 830, 1025);
                    break;
                case "settings":
                    Label("Game: Settings Status", panel.transform,
                        "오프라인 저장 게임\n화면 속 온라인 재화와 별개인 연습 기록입니다.",
                        60, 240, 710, 240, 32, Cream);
                    ActionButton("소리 전환", panel.transform, 150, 561, 530, 103,
                        "소리 " + (progress.soundEnabled ? "켜짐" : "꺼짐"), Gold, 830, 1025);
                    ActionButton("아바타 바꾸기", panel.transform, 150, 724, 530, 103,
                        "아바타 바꾸기", Orange, 830, 1025);
                    break;
                case "codex":
                    Label("Game: Codex Selection", panel.transform,
                        codexPreviewArtId.ToString("00") + "  " + BigimongSpeciesCatalog.Resolve(codexPreviewArtId).KoreanName +
                        "\n30종 · AR 연습 대전에서 체험 가능", 60, 175, 710, 153, 32, Cream);
                    var codexArt = galleryVisible
                        ? Resources.Load<Texture2D>("ReferenceUi/gallery")
                        : Resources.Load<Texture2D>("ReferenceUi/evolution-" + codexPreviewArtId.ToString("00"));
                    if (codexArt != null) ImageCard("Game: 30 Dinosaurs", panel.transform, codexArt,
                        45, 342, 740, 470, 830, 1025);
                    ActionButton("이전 공룡", panel.transform, 34, 819, 232, 84, "이전", Gold, 830, 1025);
                    ActionButton("전체 도감", panel.transform, 287, 819, 260, 84,
                        galleryVisible ? "개별 보기" : "전체 보기", Cream, 830, 1025);
                    ActionButton("다음 공룡", panel.transform, 569, 819, 232, 84, "다음", Gold, 830, 1025);
                    ActionButton("연습 대전", panel.transform, 205, 930, 420, 81, "AR 연습 대전", Orange, 830, 1025);
                    break;
                case "character":
                    Label("Game: Character Info", panel.transform,
                        progress.selectedArtId.ToString("00") + " " + BigimongSpeciesCatalog.Resolve(progress.selectedArtId).KoreanName + "\nLv." + Level + " " + GrowthStage +
                        "  ·  경험치 " + progress.dragonExperience.ToString("N0") +
                        " / 29,000\n보유 간식  " + progress.snacks + "개",
                        45, 181, 740, 180, 30, Cream);
                    var evolution = Resources.Load<Texture2D>("ReferenceUi/evolution-" + Math.Max(1, progress.selectedArtId).ToString("00"));
                    if (evolution != null) ImageCard("Game: Three Stages", panel.transform, evolution,
                        71, 395, 688, 394, 830, 1025);
                    ActionButton("간식 주기", panel.transform, 180, 831, 470, 108, "간식 주기", Orange, 830, 1025);
                    break;
            }
        }

        private void UpdateMarker()
        {
            Layout(marker, girl ? 595 : 194, 1060, girl ? 290 : 280, 156);
            marker.gameObject.SetActive(true);
            marker.SetAsLastSibling();
        }

        private void CreateAvatarCursorRepair(Texture2D texture)
        {
            // Mirror the clean left edge of the blue button over the baked white mouse cursor.
            var source = new Rect(151f, 1060f, 120f, 170f);
            var target = new Rect(399f, 1060f, 120f, 170f);
            CreateReferenceCrop("Game: Avatar Cursor Repair", texture, target, source, true);
        }

        private GameObject CreateReferenceCrop(string name, Texture2D texture, Rect target, Rect source, bool mirrorX)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            obj.transform.SetParent(board, false);
            Layout(obj.GetComponent<RectTransform>(), target.x, target.y, target.width, target.height);
            var image = obj.GetComponent<RawImage>();
            image.texture = texture;
            var u = mirrorX ? (source.x + source.width) / texture.width : source.x / texture.width;
            var width = (mirrorX ? -source.width : source.width) / texture.width;
            image.uvRect = new Rect(u, 1f - (source.y + source.height) / texture.height,
                width, source.height / texture.height);
            image.raycastTarget = false;
            return obj;
        }

        private void CreateNotice()
        {
            noticePanel = Block("Sample Values Notice", board, 264, 4, 500, 43, Cocoa);
            var label = Label("Offline Progress Notice", noticePanel.transform,
                "오프라인 게임 · 저장된 진행 상태", 0, 0, 500, 43, 18, Color.white, 500, 43);
            statusNotice = label.GetComponent<TMP_Text>();
        }

        private void CreateDialogue()
        {
            dialogue = Block("Reference Screen Destination", board, 0, 0, Width, Height, Cocoa);
            dialogue.GetComponent<Image>().raycastTarget = true;
            dialogueTitle = Label("Destination Name", dialogue.transform, "", 110, 470, 810, 110, 48, Gold).GetComponent<TMP_Text>();
            dialogueBody = Label("Destination Description", dialogue.transform, "",
                120, 595, 790, 255, 29, Cream).GetComponent<TMP_Text>();
            var button = ActionButton("닫기", dialogue.transform, 350, 910, 330, 120,
                "돌아가기", Gold);
            button.GetComponent<Button>().onClick.RemoveAllListeners();
            button.GetComponent<Button>().onClick.AddListener(CloseDialogue);
            dialogue.SetActive(false);
        }

        private static GameObject Block(string name, Transform parent, float x, float y,
            float w, float h, Color color, float baseW = Width, float baseH = Height)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Box(obj.GetComponent<RectTransform>(), x, y, w, h, baseW, baseH);
            obj.GetComponent<Image>().color = color;
            obj.GetComponent<Image>().raycastTarget = false;
            return obj;
        }

        private static GameObject Label(string name, Transform parent, string value, float x, float y,
            float w, float h, int fontSize, Color color, float baseW = Width, float baseH = Height)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            Box(obj.GetComponent<RectTransform>(), x, y, w, h, baseW, baseH);
            var label = obj.GetComponent<TMP_Text>();
            BigimongTypographyTheme.Shared.Apply(label, fontSize >= 37 ? BigimongTextRole.ACTION : BigimongTextRole.COUNTER);
            label.fontSize = fontSize;
            label.color = fontSize >= 37 ? Cream : color;
            label.text = value;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return obj;
        }

        private static void Box(RectTransform rect, float x, float y, float w, float h, float baseW, float baseH)
        {
            rect.anchorMin = new Vector2(x / baseW, 1f - (y + h) / baseH);
            rect.anchorMax = new Vector2((x + w) / baseW, 1f - y / baseH);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private GameObject ActionButton(string action, Transform parent, float x, float y, float w, float h,
            string caption, Color color, float baseW = Width, float baseH = Height)
        {
            var button = Block("Game: Button " + action, parent, x, y, w, h, color, baseW, baseH);
            button.GetComponent<Image>().raycastTarget = true;
            button.AddComponent<Button>().onClick.AddListener(() => Activate(action));
            button.AddComponent<ButtonPressMotion>();
            Label("Game: Button Label", button.transform, caption, 0, 0, w, h, 37,
                Cocoa, w, h);
            return button;
        }

        private static void ImageCard(string name, Transform parent, Texture2D texture,
            float x, float y, float w, float h, float baseW, float baseH)
        {
            var card = new GameObject(name, typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            card.transform.SetParent(parent, false);
            Box(card.GetComponent<RectTransform>(), x, y, w, h, baseW, baseH);
            card.GetComponent<RawImage>().texture = texture;
            card.GetComponent<RawImage>().raycastTarget = false;
            var fit = card.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)texture.width / texture.height;
        }

        private void OpenDialogue(string title, string description)
        {
            dialogueFromHome = hatchHomeView.IsVisible;
            if (dialogueFromHome) { hatchHomeView.Hide(); canvas.enabled = true; canvasGroup.interactable = canvasGroup.blocksRaycasts = true; }
            dialogueTitle.text = title;
            dialogueBody.text = description;
            dialogue.SetActive(true);
            dialogue.transform.SetAsLastSibling();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(dialogue.GetComponentInChildren<Button>().gameObject);
        }

        private void CloseDialogue()
        {
            dialogue.SetActive(false);
            if (dialogueFromHome) { dialogueFromHome = false; ShowHome(); }
            else FocusFirstControl();
        }
    }
}
