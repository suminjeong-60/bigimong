using System;
using UnityEngine;
using UnityEngine.UI;

namespace Bigimong.AR
{
    /// <summary>Portrait reference artwork with functional controls. Artwork is supplied as Unity Resources.</summary>
    public sealed class BigimongReferenceUi : MonoBehaviour
    {
        private const float Width = 1030f;
        private const float Height = 1536f;
        [SerializeField] private OfflineBetaFlowController flow;
        private static readonly string[] ArtScreens =
        {
            "ReferenceUi/loading", "ReferenceUi/battle-loading", "ReferenceUi/avatar", "ReferenceUi/egg", "ReferenceUi/home"
        };
        private Canvas canvas;
        private RectTransform board;
        private RawImage artwork;
        private RectTransform marker;
        private Text statusNotice;
        private GameObject dialogue;
        private Text dialogueTitle;
        private Text dialogueBody;
        private string screen;
        private bool started;
        private bool girl;
        private int eggTouches;
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
            var boardObject = new GameObject("Reference Portrait", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            boardObject.transform.SetParent(transform, false);
            board = boardObject.GetComponent<RectTransform>();
            board.anchorMin = Vector2.zero;
            board.anchorMax = Vector2.one;
            board.offsetMin = board.offsetMax = Vector2.zero;
            var fitter = boardObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = Width / Height;
            artwork = boardObject.GetComponent<RawImage>();
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
                girl = AvatarProfileStore.Load().bodyType == "FEMININE";
                Show("loading");
                nextScreenAt = Time.unscaledTime + 1f;
            }
            if (screen == "loading" && Time.unscaledTime >= nextScreenAt)
                Show(AvatarProfileStore.HasSavedProfile ? "home" : "avatar");
            if (screen == "battle-loading" && Time.unscaledTime >= nextScreenAt)
            {
                Hide();
                if (flow.Phase == OfflineBetaPhase.PetTestSelect)
                {
                    flow.PreviewEncounter();
                    flow.AcceptEncounter();
                }
            }
            if (flow.Phase == OfflineBetaPhase.ArScan || flow.Phase == OfflineBetaPhase.SummonSequence ||
                flow.Phase == OfflineBetaPhase.Battle || flow.Phase == OfflineBetaPhase.Result)
                Hide();
            if (canvas.enabled && Input.GetKeyDown(KeyCode.Escape)) Activate("뒤로가기");
        }

        private void Show(string target)
        {
            screen = target;
            var path = "ReferenceUi/" + target;
            if (Array.IndexOf(ArtScreens, path) < 0)
            {
                Debug.LogError("Unknown reference screen: " + target, this);
                return;
            }
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogError("Missing private Bigimong reference UI texture: " + target, this);
                Hide();
                return;
            }
            artwork.texture = texture;
            canvas.enabled = true;
            statusNotice.text = "화면 시안 · 표시된 코인/경험치는 예시";
            dialogue.SetActive(false);
            marker.gameObject.SetActive(false);
            for (var i = board.childCount - 1; i >= 0; i--)
            {
                var child = board.GetChild(i).gameObject;
                if (child.name.StartsWith("Hotspot:", StringComparison.Ordinal))
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
            }
            else
            {
                Nav("상점", 12, 150, 212, 211);
                Nav("놀아주기", 12, 365, 215, 200);
                Nav("홈", 217, 1380, 168, 155);
                Nav("도감", 386, 1380, 169, 155);
                Nav("퀘스트", 555, 1380, 170, 155);
                Nav("선물", 706, 1380, 180, 155);
                if (target == "egg") Nav("알 닦기", 275, 550, 560, 575);
                else
                {
                    Nav("1:1 대전", 12, 560, 214, 211);
                    Nav("캐릭터", 290, 613, 515, 460);
                    Nav("선물", 797, 1154, 222, 223);
                }
            }
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
                if (!flow.ApplyReferenceAvatar(profile)) { OpenDialogue("아바타 선택", "저장하지 못했어요. 다시 시도해 주세요."); return; }
                Show("egg");
                return;
            }
            if (action == "알 닦기" && screen == "egg")
            {
                eggTouches++;
                if (eggTouches >= 5)
                {
                    eggTouches = 0;
                    Show("home");
                    OpenDialogue("부화 후 화면 미리보기", "오프라인 화면 체험입니다. 실제 부화 보상과 캐릭터 소유권은 지급되지 않습니다.");
                }
                else statusNotice.text = $"알 닦기 {eggTouches} / 5 · 보상 없음";
                return;
            }
            var destination = ResolveDestination(screen, action);
            if (destination == "battle-loading")
            {
                if (flow.Phase != OfflineBetaPhase.PetTestSelect)
                {
                    OpenDialogue("1:1 대전", "아바타 선택을 마친 뒤 대전을 시작해 주세요.");
                    return;
                }
                Show(destination);
                nextScreenAt = Time.unscaledTime + 1.2f;
            }
            else if (destination == "avatar" || destination == "egg" || destination == "home")
            {
                Show(destination);
            }
            else
            {
                var description = destination switch
                {
                    "shop" => "상점 화면 · 결제 기능은 아직 연결되지 않았습니다.",
                    "play" => "놀아주기 화면 · 실제 경험치와 보상은 바뀌지 않습니다.",
                    "codex" => "도감 화면 · 캐릭터 목록과 상세 페이지를 연결할 자리입니다.",
                    "quest" => "퀘스트 화면 · 실제 달성 기록은 바뀌지 않습니다.",
                    "gift" => "선물 화면 · 아이템은 지급되지 않습니다.",
                    "settings" => "설정 화면 · 기기 설정은 바뀌지 않습니다.",
                    "character" => "01 빨간 티라노사우루스 · 아동기 캐릭터 상세 화면입니다.",
                    _ => "이전 화면으로 돌아갑니다."
                };
                OpenDialogue(action, description);
            }
        }

        private void UpdateMarker()
        {
            Layout(marker, girl ? 595 : 194, 1060, girl ? 290 : 280, 156);
            marker.gameObject.SetActive(true);
        }

        private void CreateNotice()
        {
            var notice = new GameObject("Sample Values Notice", typeof(RectTransform), typeof(Image));
            notice.transform.SetParent(board, false);
            Layout(notice.GetComponent<RectTransform>(), 266, 4, 498, 40);
            notice.GetComponent<Image>().color = new Color(.16f, .10f, .05f, .84f);
            notice.GetComponent<Image>().raycastTarget = false;
            var label = CreateText("Reference UI Preview Notice", notice.transform, Color.white,
                "화면 시안 · 표시된 코인/경험치는 예시", 20);
            statusNotice = label.GetComponent<Text>();
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            label.GetComponent<Text>().raycastTarget = false;
        }

        private void CreateDialogue()
        {
            dialogue = new GameObject("Reference Screen Destination", typeof(RectTransform), typeof(Image));
            dialogue.transform.SetParent(board, false);
            Layout(dialogue.GetComponent<RectTransform>(), 0, 0, Width, Height);
            dialogue.GetComponent<Image>().color = new Color(.16f, .10f, .05f, .97f);
            dialogueTitle = CreateText("Destination Name", dialogue.transform, Color.white, "", 48).GetComponent<Text>();
            Layout(dialogueTitle.rectTransform, 110, 470, 810, 110);
            dialogueBody = CreateText("Destination Description", dialogue.transform, Color.white, "", 29).GetComponent<Text>();
            Layout(dialogueBody.rectTransform, 120, 595, 790, 255);
            var close = new GameObject("Close Destination", typeof(RectTransform), typeof(Image), typeof(Button));
            close.transform.SetParent(dialogue.transform, false);
            Layout(close.GetComponent<RectTransform>(), 350, 910, 330, 120);
            close.GetComponent<Image>().color = new Color(.99f, .63f, .16f, 1f);
            close.GetComponent<Button>().onClick.AddListener(() => dialogue.SetActive(false));
            var closeText = CreateText("Close Label", close.transform, Color.black, "돌아가기", 36);
            closeText.GetComponent<Text>().raycastTarget = false;
            var closeRect = closeText.GetComponent<RectTransform>();
            closeRect.anchorMin = Vector2.zero; closeRect.anchorMax = Vector2.one;
            closeRect.offsetMin = closeRect.offsetMax = Vector2.zero;
            dialogue.SetActive(false);
        }

        private static GameObject CreateText(string name, Transform parent, Color color, string text, int size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var label = obj.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            return obj;
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
