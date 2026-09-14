using System;
using UnityEngine;
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
        private static readonly string[] Species =
        {
            "빨간 티라노사우루스", "트리케라톱스", "익룡", "스테고사우루스", "브라키오사우루스",
            "스피노사우루스", "안킬로사우루스", "벨로키랍토르", "코리토사우루스", "카르노타우루스",
            "모사사우루스", "알로사우루스", "케찰코아틀루스", "데이노니쿠스", "유오플로케팔루스",
            "바리오닉스", "오비랍토르", "프로토케라톱스", "갈리미무스", "드라코렉스",
            "기가노토사우루스", "딜로포사우루스", "이구아노돈", "켄트로사우루스", "테리지노사우루스",
            "콤프소그나투스", "파라사우롤로푸스", "미크로랍토르", "브론토사우루스", "티타노사우루스"
        };
        private static readonly Color Cocoa = new Color(.22f, .12f, .08f, .97f);
        private static readonly Color Cream = new Color(1f, .94f, .78f);
        private static readonly Color Gold = new Color(1f, .70f, .13f);
        private static readonly Color Orange = new Color(.97f, .42f, .14f);
        [SerializeField] private OfflineBetaFlowController flow;
        private Canvas canvas;
        private RectTransform board;
        private RawImage artwork;
        private RectTransform marker;
        private Text statusNotice;
        private GameObject noticePanel;
        private GameObject dialogue;
        private Text dialogueTitle;
        private Text dialogueBody;
        private OfflineReferenceProgress progress;
        private string screen;
        private bool started;
        private bool girl;
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
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
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

        private void Update()
        {
            if (flow == null || !flow.IsActive)
            {
                if (started) { canvas.enabled = false; started = false; }
                return;
            }
            if (!started)
            {
                started = true;
                progress = OfflineReferenceProgressStore.Load();
                AudioListener.volume = progress.soundEnabled ? 1f : 0f;
                girl = AvatarProfileStore.Load().bodyType == "FEMININE";
                Show("loading");
                nextScreenAt = Time.unscaledTime + 1f;
            }
            if (screen == "loading" && Time.unscaledTime >= nextScreenAt)
                Show(!AvatarProfileStore.HasSavedProfile ? "avatar" : progress.hatched ? "home" : "egg");
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
            if (canvas.enabled && Input.GetKeyDown(KeyCode.Escape)) Activate("뒤로가기");
        }

        private static bool IsArtwork(string target)
        {
            return target == "loading" || target == "battle-loading" || target == "avatar" ||
                   target == "egg" || target == "home";
        }

        private void ShowHome()
        {
            Show(!AvatarProfileStore.HasSavedProfile ? "avatar" : progress.hatched ? "home" : "egg");
        }

        private void Show(string target)
        {
            screen = target;
            var art = IsArtwork(target) ? target : "home";
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
            artwork.texture = texture;
            canvas.enabled = true;
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
                    Destroy(child);
                }
            }
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
                return;
            }
            Nav("상점", 12, 150, 212, 211);
            Nav("놀아주기", 12, 365, 215, 200);
            Nav("홈", 217, 1380, 168, 155);
            Nav("도감", 386, 1380, 169, 155);
            Nav("퀘스트", 555, 1380, 170, 155);
            Nav("선물", 706, 1380, 180, 155);
            if (target == "egg")
            {
                Nav("알 닦기", 275, 550, 560, 575);
                BuildProgressHud(false);
            }
            else if (target == "home")
            {
                Nav("1:1 대전", 12, 560, 214, 211);
                Nav("캐릭터", 290, 613, 515, 460);
                Nav("선물", 797, 1154, 222, 223);
                BuildProgressHud(true);
            }
            else BuildPage(target);
        }

        private void Hide() { if (canvas != null) canvas.enabled = false; }

        private void Nav(string action, float x, float y, float w, float h)
        {
            var obj = new GameObject("Hotspot: " + action, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(board, false);
            Layout(obj.GetComponent<RectTransform>(), x, y, w, h);
            var graphic = obj.GetComponent<Image>();
            graphic.color = new Color(1, 1, 1, .001f);
            var button = obj.GetComponent<Button>();
            button.targetGraphic = graphic;
            button.onClick.AddListener(() => Activate(action));
        }

        private static void Layout(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = new Vector2(x / Width, 1f - (y + h) / Height);
            rect.anchorMax = new Vector2((x + w) / Width, 1f - y / Height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public void Activate(string action)
        {
            if (!canvas.enabled || string.IsNullOrEmpty(screen)) return;
            if (dialogue.activeSelf && action != "뒤로가기") return;
            if (dialogue.activeSelf) { dialogue.SetActive(false); return; }
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
                Show(progress.hatched ? "home" : "egg");
                return;
            }
            if (action == "알 닦기" && screen == "egg")
            {
                if (progress.CanHatch)
                {
                    progress.Hatch();
                    SaveAndShow("home");
                    OpenDialogue("비기몽이 태어났어요!", "01 빨간 티라노사우루스를 만나 보세요. 알과 육성 기록은 이 기기에 저장됩니다.");
                }
                else if (progress.PolishEgg())
                {
                    SaveAndShow("egg");
                    if (progress.CanHatch) OpenDialogue("부화 준비 완료!", "알을 한 번 더 누르면 빨간 티라노사우루스를 만납니다.");
                }
                return;
            }
            if (HandleGameAction(action)) return;
            var destination = ResolveDestination(screen, action);
            if (destination == "battle-loading")
            {
                if (!AvatarProfileStore.HasSavedProfile || !progress.hatched || flow.Phase != OfflineBetaPhase.PetTestSelect)
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
            if (action == "알 부화" && screen == "egg")
            {
                if (progress.Hatch()) SaveAndShow("home");
                return true;
            }
            if (action == "함께 놀기" && screen == "play")
            {
                var wasHatched = progress.hatched;
                if (!progress.Play(DateTime.UtcNow))
                {
                    OpenDialogue("잠깐 쉬어요", "놀아주기는 10초마다 할 수 있어요. 하루 최대 100번입니다.");
                    return true;
                }
                SaveAndShow("play");
                if (!wasHatched && progress.CanHatch) OpenDialogue("알이 빛나요!", "홈 화면에서 알을 눌러 비기몽을 만나 보세요.");
                return true;
            }
            if (action == "오늘의 선물 받기" && screen == "gift")
            {
                if (!progress.ClaimGift(DateTime.UtcNow))
                    OpenDialogue("오늘의 선물", "오늘 선물은 이미 받았어요. 내일 다시 만나요!");
                else SaveAndShow("gift");
                return true;
            }
            if (action == "퀘스트 보상 받기" && screen == "quest")
            {
                if (!progress.ClaimQuest(DateTime.UtcNow))
                    OpenDialogue("퀘스트", "오늘 함께 놀기 3회를 완료하면 보상을 받을 수 있어요.");
                else SaveAndShow("quest");
                return true;
            }
            if (action == "간식 구매" && screen == "shop")
            {
                if (!progress.BuySnack()) OpenDialogue("상점", "비기코인이 부족해요. 함께 놀거나 오늘의 선물을 받아 보세요.");
                else SaveAndShow("shop");
                return true;
            }
            if (action == "간식 주기" && screen == "character")
            {
                if (!progress.Feed()) OpenDialogue("캐릭터", "부화한 비기몽과 간식이 필요해요. 상점에서 간식을 구할 수 있어요.");
                else SaveAndShow("character");
                return true;
            }
            if (action == "이전 공룡" || action == "다음 공룡")
            {
                progress.selectedArtId = 1 + ((progress.selectedArtId - 1 + (action == "다음 공룡" ? 1 : 29)) % 30);
                if (flow.Phase == OfflineBetaPhase.PetTestSelect) flow.SelectPet(progress.selectedArtId);
                SaveAndShow("codex");
                return true;
            }
            if (action == "소리 전환" && screen == "settings")
            {
                progress.soundEnabled = !progress.soundEnabled;
                AudioListener.volume = progress.soundEnabled ? 1f : 0f;
                SaveAndShow("settings");
                return true;
            }
            if (action == "아바타 바꾸기" && screen == "settings")
            {
                Show("avatar");
                return true;
            }
            if (action == "연습 대전" && screen == "codex")
            {
                Activate("1:1 대전");
                return true;
            }
            return false;
        }

        private void SaveAndShow(string target)
        {
            if (!OfflineReferenceProgressStore.Save(progress))
                OpenDialogue("저장 오류", "기기에 진행 상태를 저장하지 못했어요. 저장 공간을 확인해 주세요.");
            else Show(target);
        }

        private void BuildProgressHud(bool home)
        {
            var top = home ? 1230 : 1100;
            var panel = Block("Game: Live Progress", board, 205, top, 615, 145, Cocoa);
            var line = home
                ? "Lv." + progress.Level + " " + progress.GrowthStage + "  ·  " + progress.coins.ToString("N0") + " 코인"
                : "알 경험치 " + progress.eggExperience.ToString("N0") + " / 30,000";
            Label("Game: Progress Value", panel.transform, line, 0, 10, 615, 63, 34, Cream);
            Label("Game: Progress Hint", panel.transform,
                home ? "놀아주기와 퀘스트로 함께 성장해요!" :
                progress.CanHatch ? "부화 준비 완료 · 알을 눌러 주세요!" : "알을 닦거나 함께 놀면 경험치가 쌓여요.",
                15, 78, 585, 48, 23, Gold);
            if (home) return;
            var fill = Block("Game: Egg Bar", board, 240, top + 154, 550, 22, new Color(.23f, .15f, .18f));
            Block("Game: Egg Fill", fill.transform, 0, 0,
                Mathf.Max(3, 550f * progress.eggExperience / OfflineReferenceProgress.HatchTarget), 22, Orange, 550, 22);
            if (progress.CanHatch)
                ActionButton("알 부화", board, 320, 1250, 380, 85, "알 부화하기", Gold);
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
            progress.StartDay(today);
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
                        progress.hatched ? "빨간 티라노와 놀기\n경험치 +200 · 비기코인 +50" :
                        "반짝이는 알과 놀기\n알 경험치 +500",
                        65, 251, 700, 270, 39, Cream);
                    Label("Game: Daily Play", panel.transform, "오늘 함께 논 횟수  " + progress.playsToday + " / 100",
                        65, 561, 700, 100, 31, Gold);
                    ActionButton("함께 놀기", panel.transform, 180, 721, 470, 114, "함께 놀기", Orange, 830, 1025);
                    break;
                case "gift":
                    Label("Game: Gift Status", panel.transform,
                        "매일 한 번 받는 선물\n비기코인 +500" +
                        (progress.hatched ? "" : "\n알 경험치 +500"),
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
                        progress.selectedArtId.ToString("00") + "  " + Species[progress.selectedArtId - 1] +
                        "\n30종 · AR 연습 대전에서 체험 가능", 60, 175, 710, 153, 32, Cream);
                    var gallery = Resources.Load<Texture2D>("ReferenceUi/gallery");
                    if (gallery != null) ImageCard("Game: 30 Dinosaurs", panel.transform, gallery,
                        45, 342, 740, 470, 830, 1025);
                    ActionButton("이전 공룡", panel.transform, 42, 846, 220, 99, "이전", Gold, 830, 1025);
                    ActionButton("연습 대전", panel.transform, 287, 846, 260, 99, "AR 연습", Orange, 830, 1025);
                    ActionButton("다음 공룡", panel.transform, 569, 846, 220, 99, "다음", Gold, 830, 1025);
                    break;
                case "character":
                    Label("Game: Character Info", panel.transform,
                        "01  빨간 티라노사우루스\nLv." + progress.Level + " " + progress.GrowthStage +
                        "  ·  경험치 " + progress.dragonExperience.ToString("N0") +
                        " / 29,000\n보유 간식  " + progress.snacks + "개",
                        45, 181, 740, 180, 30, Cream);
                    var evolution = Resources.Load<Texture2D>("ReferenceUi/evolution-01");
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
        }

        private void CreateNotice()
        {
            noticePanel = Block("Sample Values Notice", board, 264, 4, 500, 43, Cocoa);
            var label = Label("Offline Progress Notice", noticePanel.transform,
                "오프라인 게임 · 저장된 진행 상태", 0, 0, 500, 43, 18, Color.white, 500, 43);
            statusNotice = label.GetComponent<Text>();
        }

        private void CreateDialogue()
        {
            dialogue = Block("Reference Screen Destination", board, 0, 0, Width, Height, Cocoa);
            dialogueTitle = Label("Destination Name", dialogue.transform, "", 110, 470, 810, 110, 48, Gold).GetComponent<Text>();
            dialogueBody = Label("Destination Description", dialogue.transform, "",
                120, 595, 790, 255, 29, Cream).GetComponent<Text>();
            var button = ActionButton("닫기", dialogue.transform, 350, 910, 330, 120,
                "돌아가기", Gold);
            button.GetComponent<Button>().onClick.RemoveAllListeners();
            button.GetComponent<Button>().onClick.AddListener(() => dialogue.SetActive(false));
            dialogue.SetActive(false);
        }

        private static GameObject Block(string name, Transform parent, float x, float y,
            float w, float h, Color color, float baseW = Width, float baseH = Height)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Box(obj.GetComponent<RectTransform>(), x, y, w, h, baseW, baseH);
            obj.GetComponent<Image>().color = color;
            return obj;
        }

        private static GameObject Label(string name, Transform parent, string value, float x, float y,
            float w, float h, int fontSize, Color color, float baseW = Width, float baseH = Height)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            Box(obj.GetComponent<RectTransform>(), x, y, w, h, baseW, baseH);
            var label = obj.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.color = color;
            label.text = value;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
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
            button.AddComponent<Button>().onClick.AddListener(() => Activate(action));
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
            dialogueTitle.text = title;
            dialogueBody.text = description;
            dialogue.SetActive(true);
            dialogue.transform.SetAsLastSibling();
        }
    }
}
