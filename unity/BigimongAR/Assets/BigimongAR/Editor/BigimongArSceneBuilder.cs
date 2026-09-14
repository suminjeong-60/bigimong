#if UNITY_EDITOR
using System.IO;
using Bigimong.AR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Bigimong.Editor
{
    public static class BigimongArSceneBuilder
    {
        private const string ScenePath = "Assets/BigimongAR/Scenes/ArBattle.unity";
        private const string RingPrefabPath = "Assets/BigimongAR/Prefabs/ArBattleRing.prefab";
        private const string RingMaterialPath = "Assets/BigimongAR/Materials/ArBattleRingCyan.mat";

        [MenuItem("Bigimong/Create AR Battle Scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/BigimongAR/Scenes");
            Directory.CreateDirectory("Assets/BigimongAR/Prefabs");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sessionObject = new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));
            var originObject = new GameObject("XR Origin", typeof(XROrigin), typeof(ARRaycastManager), typeof(ARPlaneManager));
            var cameraObject = new GameObject("AR Camera", typeof(Camera), typeof(AudioListener), typeof(ARCameraManager), typeof(ARCameraBackground), typeof(ArCameraPoseDriver));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(originObject.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = 0.01f;
            originObject.GetComponent<XROrigin>().Camera = camera;
            originObject.GetComponent<ARPlaneManager>().requestedDetectionMode = PlaneDetectionMode.Horizontal;

            var systems = new GameObject("Bigimong AR Systems");
            var arena = systems.AddComponent<ArBattleArenaController>();
            var director = systems.AddComponent<ArBattleDirector>();
            var summonDirector = systems.AddComponent<SummonSequenceDirector>();
            var bridge = systems.AddComponent<ArBattleNativeBridge>();
            var recovery = systems.AddComponent<ArTrackingRecovery>();
            var sharedAnchor = systems.AddComponent<SharedAnchorCoordinator>();
            var offlineDemo = systems.AddComponent<ArBattleOfflineDemo>();
            var offlineFlow = systems.AddComponent<OfflineBetaFlowController>();

            var placement = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            placement.name = "Placement Indicator";
            placement.transform.localScale = new Vector3(0.55f, 0.008f, 0.55f);
            Object.DestroyImmediate(placement.GetComponent<Collider>());
            placement.SetActive(false);
            var ringPrefab = CreateRingPrefab();

            var hud = CreateHud(bridge, out var trackingPanel, out var disconnectedPanel);
            var creator = CreateAvatarCreator(cameraObject.transform, hud.gameObject);
            CreateBetaMenus(offlineFlow);
            Assign(arena, "raycastManager", originObject.GetComponent<ARRaycastManager>());
            Assign(arena, "planeManager", originObject.GetComponent<ARPlaneManager>());
            Assign(arena, "placementIndicator", placement);
            Assign(arena, "battleRingPrefab", ringPrefab);
            Assign(director, "arena", arena);
            Assign(director, "sharedAnchor", sharedAnchor);
            Assign(director, "hud", hud);
            Assign(director, "summonDirector", summonDirector);
            Assign(summonDirector, "hud", hud);
            Assign(director, "trackingLostPanel", trackingPanel);
            Assign(director, "disconnectedPanel", disconnectedPanel);
            Assign(bridge, "director", director);
            Assign(bridge, "hud", hud);
            Assign(bridge, "offlineDemo", offlineDemo);
            Assign(bridge, "offlineFlow", offlineFlow);
            Assign(recovery, "director", director);
            Assign(recovery, "arena", arena);
            Assign(sharedAnchor, "arena", arena);
            Assign(sharedAnchor, "nativeBridge", bridge);
            Assign(offlineDemo, "arena", arena);
            Assign(offlineDemo, "bridge", bridge);
            Assign(offlineDemo, "hud", hud);
            Assign(offlineDemo, "director", director);
            Assign(offlineFlow, "avatarCreator", creator);
            Assign(offlineFlow, "arena", arena);
            Assign(offlineFlow, "director", director);
            Assign(offlineFlow, "summonDirector", summonDirector);
            Assign(offlineFlow, "offlineDemo", offlineDemo);
            Assign(offlineFlow, "hud", hud);
            Assign(offlineFlow, "cameraTransform", cameraObject.transform);
            Assign(offlineFlow, "battleCanvas", hud.gameObject);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeGameObject = systems;
            Debug.Log($"Bigimong AR battle scene created: {ScenePath}", sessionObject);
        }

        private static GameObject CreateRingPrefab()
        {
            Directory.CreateDirectory("Assets/BigimongAR/Materials");
            var material = AssetDatabase.LoadAssetAtPath<Material>(RingMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = "Ar Battle Ring Cyan" };
                AssetDatabase.CreateAsset(material, RingMaterialPath);
            }
            var cyan = new Color(.08f, .92f, .98f, .9f);
            material.color = cyan;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", cyan);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", cyan * .55f);
            }
            EditorUtility.SetDirty(material);

            var source = new GameObject("ArBattleRing");
            source.name = "ArBattleRing";
            for (var index = 0; index < 40; index++)
            {
                var angle = index * Mathf.PI * 2f / 40f;
                var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = $"Ring Segment {index + 1:00}";
                segment.transform.SetParent(source.transform, false);
                segment.transform.localPosition = new Vector3(Mathf.Sin(angle) * .49f, .012f, Mathf.Cos(angle) * .49f);
                segment.transform.localRotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0);
                segment.transform.localScale = new Vector3(.105f, .024f, .035f);
                Object.DestroyImmediate(segment.GetComponent<Collider>());
                segment.GetComponent<Renderer>().sharedMaterial = material;
            }
            var center = new GameObject("Ring Center Cutout");
            center.transform.SetParent(source.transform, false);
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, RingPrefabPath);
            Object.DestroyImmediate(source);
            return prefab;
        }

        private static ArBattleHud CreateHud(ArBattleNativeBridge bridge, out GameObject trackingPanel, out GameObject disconnectedPanel)
        {
            var canvasObject = new GameObject("AR Battle HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var safeObject = new GameObject("Battle Safe Area", typeof(RectTransform));
            safeObject.transform.SetParent(canvasObject.transform, false);
            var safeArea = safeObject.GetComponent<RectTransform>();
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;

            var impactObject = new GameObject("Impact Flash", typeof(RectTransform), typeof(Image));
            impactObject.transform.SetParent(safeArea, false);
            var impactRect = impactObject.GetComponent<RectTransform>();
            impactRect.anchorMin = Vector2.zero;
            impactRect.anchorMax = Vector2.one;
            impactRect.offsetMin = impactRect.offsetMax = Vector2.zero;
            var impactFlash = impactObject.GetComponent<Image>();
            impactFlash.color = new Color(.25f, .08f, .05f, 0);
            impactFlash.raycastTarget = false;

            var playerAPanel = CreateHudPanel(safeArea, "Player A Panel", new Vector2(-275, -120), new Vector2(430, 176));
            var playerBPanel = CreateHudPanel(safeArea, "Player B Panel", new Vector2(275, -120), new Vector2(430, 176));
            AnchorToTop(playerAPanel.GetComponent<RectTransform>());
            AnchorToTop(playerBPanel.GetComponent<RectTransform>());
            var playerAHp = CreateText(playerAPanel.transform, "Player A HP", "A  HP 5", new Vector2(0, 34), 31);
            var playerBHp = CreateText(playerBPanel.transform, "Player B HP", "B  HP 5", new Vector2(0, 34), 31);
            playerAHp.rectTransform.sizeDelta = new Vector2(380, 62);
            playerBHp.rectTransform.sizeDelta = new Vector2(380, 62);
            var playerAHpFill = CreateHpBar(playerAPanel.transform, "Player A HP Bar", new Vector2(0, -39), false);
            var playerBHpFill = CreateHpBar(playerBPanel.transform, "Player B HP Bar", new Vector2(0, -39), true);

            var countdownPanel = CreateHudPanel(safeArea, "Countdown Panel", new Vector2(0, -258), new Vector2(188, 108));
            AnchorToTop(countdownPanel.GetComponent<RectTransform>());
            var countdown = CreateText(countdownPanel.transform, "Countdown", "10s", Vector2.zero, 56);
            countdown.rectTransform.sizeDelta = new Vector2(170, 92);
            var roundPanel = CreateHudPanel(safeArea, "Round Panel", new Vector2(0, -370), new Vector2(620, 74));
            AnchorToTop(roundPanel.GetComponent<RectTransform>());
            var roundStatus = CreateText(roundPanel.transform, "Round Status", "ROUND 1", Vector2.zero, 29);
            roundStatus.rectTransform.sizeDelta = new Vector2(580, 62);
            var badge = CreateText(safeArea, "Beta Badge", "OFFLINE AR BETA · 서버 정산 없음", Vector2.zero, 24);
            AnchorToTop(badge.rectTransform); badge.rectTransform.anchoredPosition = new Vector2(0, -20);
            var left = CreateCircleButton(safeArea, "Left", "←", -210);
            var center = CreateCircleButton(safeArea, "Center", "↑", 0);
            var right = CreateCircleButton(safeArea, "Right", "→", 210);
            var restart = CreateButton(safeArea, "Restart", "다시 대전", Vector2.zero);
            AnchorToBottom(restart.GetComponent<RectTransform>()); restart.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 260);
            trackingPanel = CreateStatusPanel(safeArea, "TrackingLost", "바닥 인식이 끊겼어요 · 앵커를 다시 잡아주세요", new Vector2(0, 520));
            disconnectedPanel = CreateStatusPanel(safeArea, "Disconnected", "연결 복구 중 · 서버 자동플레이 진행", new Vector2(0, 410));
            trackingPanel.SetActive(false);
            disconnectedPanel.SetActive(false);

            var hud = canvasObject.AddComponent<ArBattleHud>();
            Assign(hud, "bridge", bridge);
            Assign(hud, "countdownText", countdown);
            Assign(hud, "playerAHpText", playerAHp);
            Assign(hud, "playerBHpText", playerBHp);
            Assign(hud, "playerAHpFill", playerAHpFill);
            Assign(hud, "playerBHpFill", playerBHpFill);
            Assign(hud, "roundStatusText", roundStatus);
            Assign(hud, "leftButton", left);
            Assign(hud, "centerButton", center);
            Assign(hud, "rightButton", right);
            Assign(hud, "restartButton", restart);
            Assign(hud, "safeArea", safeArea);
            Assign(hud, "impactFlash", impactFlash);
            Assign(hud, "betaBadge", badge);
            return hud;
        }

        private static AvatarCreatorController CreateAvatarCreator(Transform cameraTransform, GameObject battleCanvas)
        {
            battleCanvas.SetActive(false);
            var systems = new GameObject("Avatar Creator Systems");
            var controller = systems.AddComponent<AvatarCreatorController>();

            var canvasObject = new GameObject("Avatar Creator", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            var safeAreaObject = new GameObject("Avatar Creator Safe Area", typeof(RectTransform));
            safeAreaObject.transform.SetParent(canvasObject.transform, false);
            var safeArea = safeAreaObject.GetComponent<RectTransform>();
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;

            var previewPanel = CreatePanel(safeArea, "Avatar Studio Backdrop", new Vector2(0, 365), new Vector2(880, 740),
                new Color(.19f, .29f, .39f, .42f));
            previewPanel.GetComponent<Image>().raycastTarget = false;
            CreateHudFrame(previewPanel.transform, new Vector2(860, 720));
            var title = CreateText(safeArea, "Creator Title", "나만의 아바타", new Vector2(0, -50), 44);
            AnchorToTop(title.rectTransform);
            var previewName = CreateText(previewPanel.transform, "Preview Name", "플레이어", new Vector2(0, 300), 34);

            var previewAnchor = new GameObject("Avatar Preview Anchor").transform;
            previewAnchor.SetParent(cameraTransform, false);
            previewAnchor.localPosition = new Vector3(0, .08f, 3.1f);
            previewAnchor.localRotation = Quaternion.identity;
            previewAnchor.gameObject.SetActive(false);

            var studioLighting = new GameObject("Avatar Studio Lighting");
            studioLighting.transform.SetParent(previewAnchor, false);
            CreateStudioLight(studioLighting.transform, "Studio Key Light", new Vector3(-1.5f, 2.7f, -1.2f), 1.15f);
            CreateStudioLight(studioLighting.transform, "Studio Fill Light", new Vector3(1.4f, 1.9f, -1f), .58f);

            var scrollObject = new GameObject("Avatar Creator Scroll View", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(safeArea, false);
            var scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0, 0.02f);
            scrollRectTransform.anchorMax = new Vector2(1, 0.47f);
            scrollRectTransform.offsetMin = new Vector2(24, 0);
            scrollRectTransform.offsetMax = new Vector2(-24, 0);

            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportObject.transform.SetParent(scrollObject.transform, false);
            var viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewportObject.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);

            var contentObject = new GameObject("Avatar Creator Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);
            var content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, 720);

            var scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 45f;

            var nameInput = CreateInputField(content, "Avatar Name", new Vector2(0, -62), new Vector2(760, 88));
            var categoryLabel = CreateText(content, "Category", "BODY", new Vector2(0, -150), 30);
            var valueLabel = CreateText(content, "Selection", "남성형", new Vector2(0, -330), 32);
            AnchorToTop(nameInput.GetComponent<RectTransform>());
            AnchorToTop(categoryLabel.rectTransform);
            AnchorToTop(valueLabel.rectTransform);
            categoryLabel.rectTransform.sizeDelta = new Vector2(900, 50);
            valueLabel.rectTransform.sizeDelta = new Vector2(900, 60);

            var categoryNames = new[] { "체형", "얼굴", "피부", "눈썹", "눈", "머리", "머리색" };
            var categoryButtons = new Button[categoryNames.Length];
            for (var index = 0; index < categoryNames.Length; index++)
            {
                var x = -450f + index * 150f;
                categoryButtons[index] = CreateButton(content, $"Category {index + 1}", categoryNames[index],
                    new Vector2(x, -235), new Vector2(132, 76), 23);
                AnchorToTop(categoryButtons[index].GetComponent<RectTransform>());
            }

            var previous = CreateButton(content, "Previous", "◀ 이전", new Vector2(-275, -415), new Vector2(300, 92), 29);
            var next = CreateButton(content, "Next", "다음 ▶", new Vector2(275, -415), new Vector2(300, 92), 29);
            var masculine = CreateButton(content, "Masculine Body", "남성형", new Vector2(-220, -515), new Vector2(340, 82), 27);
            var feminine = CreateButton(content, "Feminine Body", "여성형", new Vector2(220, -515), new Vector2(340, 82), 27);
            var save = CreateButton(content, "Save Avatar", "이 아바타로 저장", new Vector2(0, -630), new Vector2(720, 96), 31);
            AnchorToTop(previous.GetComponent<RectTransform>());
            AnchorToTop(next.GetComponent<RectTransform>());
            AnchorToTop(masculine.GetComponent<RectTransform>());
            AnchorToTop(feminine.GetComponent<RectTransform>());
            AnchorToTop(save.GetComponent<RectTransform>());

            var editorEntry = new GameObject("Avatar Editor Entry", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var editorCanvas = editorEntry.GetComponent<Canvas>();
            editorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            editorCanvas.sortingOrder = 19;
            var editorScaler = editorEntry.GetComponent<CanvasScaler>();
            editorScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            editorScaler.referenceResolution = new Vector2(1080, 1920);
            var editorSafeAreaObject = new GameObject("Avatar Editor Entry Safe Area", typeof(RectTransform));
            editorSafeAreaObject.transform.SetParent(editorEntry.transform, false);
            var editorSafeArea = editorSafeAreaObject.GetComponent<RectTransform>();
            editorSafeArea.anchorMin = Vector2.zero;
            editorSafeArea.anchorMax = Vector2.one;
            editorSafeArea.offsetMin = Vector2.zero;
            editorSafeArea.offsetMax = Vector2.zero;
            var editButton = CreateButton(editorSafeArea, "Edit Avatar", "아바타 편집", Vector2.zero, new Vector2(250, 76), 25);
            var editRect = editButton.GetComponent<RectTransform>();
            editRect.anchorMin = Vector2.one;
            editRect.anchorMax = Vector2.one;
            editRect.pivot = Vector2.one;
            editRect.anchoredPosition = new Vector2(-28, -28);
            editorEntry.SetActive(false);

            Assign(controller, "creatorCanvas", canvasObject);
            Assign(controller, "battleCanvas", battleCanvas);
            Assign(controller, "editorEntry", editorEntry);
            Assign(controller, "safeArea", safeArea);
            Assign(controller, "editorEntrySafeArea", editorSafeArea);
            Assign(controller, "previewAnchor", previewAnchor);
            Assign(controller, "nameInput", nameInput);
            Assign(controller, "previewNameText", previewName);
            Assign(controller, "categoryLabel", categoryLabel);
            Assign(controller, "valueLabel", valueLabel);
            Assign(controller, "previousButton", previous);
            Assign(controller, "nextButton", next);
            Assign(controller, "masculineButton", masculine);
            Assign(controller, "feminineButton", feminine);
            Assign(controller, "saveButton", save);
            Assign(controller, "editButton", editButton);
            AssignArray(controller, "categoryButtons", categoryButtons);
            return controller;
        }

        private static void CreateBetaMenus(OfflineBetaFlowController flow)
        {
            var petCanvas = CreateBetaCanvas("Beta Pet Selection", out var petSafeArea);
            var petPanel = CreatePanel(petSafeArea, "Pet Test Panel", Vector2.zero, new Vector2(960, 1030), new Color(.07f, .12f, .17f, .94f));
            CreateText(petPanel.transform, "Pet Select Title", "베타 테스트 선택 · 보상 없음", new Vector2(0, 385), 42);
            var petText = CreateText(petPanel.transform, "Pet Selection", "01 / 30", new Vector2(0, 140), 37);
            petText.rectTransform.sizeDelta = new Vector2(880, 210);
            var prev = CreateButton(petPanel.transform, "Previous Pet", "◀ 이전", new Vector2(-230, -85), new Vector2(340, 110), 34);
            var next = CreateButton(petPanel.transform, "Next Pet", "다음 ▶", new Vector2(230, -85), new Vector2(340, 110), 34);
            var choose = CreateButton(petPanel.transform, "Choose Pet", "이 비기몽으로 대전", new Vector2(0, -305), new Vector2(670, 120), 34);

            var encounterCanvas = CreateBetaCanvas("Tyrannosaurus Encounter", out var encounterSafeArea);
            var encounterPanel = CreatePanel(encounterSafeArea, "Encounter Panel", Vector2.zero, new Vector2(940, 920), new Color(.13f, .08f, .09f, .94f));
            CreateText(encounterPanel.transform, "Encounter Title", "티라노사우루스가 나타났다!", new Vector2(0, 305), 44);
            var encounterText = CreateText(encounterPanel.transform, "Encounter Description", "오프라인 체험전 · 보상 없음", new Vector2(0, 55), 35);
            var accept = CreateButton(encounterPanel.transform, "Accept Encounter", "대전 수락", new Vector2(0, -210), new Vector2(660, 115), 36);
            var back = CreateButton(encounterPanel.transform, "Back to Pet", "다른 비기몽 선택", new Vector2(0, -335), new Vector2(590, 90), 28);

            var scanCanvas = CreateBetaCanvas("Arena Scan Guidance", out var scanSafeArea);
            var scanText = CreateText(scanSafeArea, "Scan Guidance", "바닥을 천천히 비춰 주세요", Vector2.zero, 31);
            AnchorToTop(scanText.rectTransform);
            scanText.rectTransform.anchoredPosition = new Vector2(0, -350);
            var fallback = CreateButton(scanSafeArea, "Screen Fixed Fallback", "화면 고정 테스트", Vector2.zero, new Vector2(650, 105), 32);
            AnchorToBottom(fallback.GetComponent<RectTransform>());
            fallback.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 300);
            fallback.gameObject.SetActive(false);

            var resultCanvas = CreateBetaCanvas("Beta Battle Result", out var resultSafeArea);
            var resultPanel = CreatePanel(resultSafeArea, "Result Panel", Vector2.zero, new Vector2(920, 760), new Color(.08f, .12f, .18f, .94f));
            var resultText = CreateText(resultPanel.transform, "Result", "전투 종료", new Vector2(0, 190), 44);
            var rematch = CreateButton(resultPanel.transform, "Rematch", "다시 대전", new Vector2(0, -60), new Vector2(650, 110), 35);
            var returnButton = CreateButton(resultPanel.transform, "Return to Pet Selection", "비기몽 다시 선택", new Vector2(0, -220), new Vector2(650, 105), 30);

            Assign(flow, "petCanvas", petCanvas);
            Assign(flow, "petText", petText);
            Assign(flow, "previousPetButton", prev);
            Assign(flow, "nextPetButton", next);
            Assign(flow, "choosePetButton", choose);
            Assign(flow, "encounterCanvas", encounterCanvas);
            Assign(flow, "encounterText", encounterText);
            Assign(flow, "acceptButton", accept);
            Assign(flow, "backButton", back);
            Assign(flow, "scanCanvas", scanCanvas);
            Assign(flow, "scanText", scanText);
            Assign(flow, "fallbackButton", fallback);
            Assign(flow, "resultCanvas", resultCanvas);
            Assign(flow, "resultText", resultText);
            Assign(flow, "rematchButton", rematch);
            Assign(flow, "returnButton", returnButton);
        }

        private static GameObject CreateBetaCanvas(string name, out RectTransform safeArea)
        {
            var canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<Canvas>().sortingOrder = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            var safeObject = new GameObject("Safe Area", typeof(RectTransform));
            safeObject.transform.SetParent(canvasObject.transform, false);
            safeObject.AddComponent<DeviceSafeArea>();
            safeArea = safeObject.GetComponent<RectTransform>();
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            canvasObject.SetActive(false);
            return canvasObject;
        }

        private static Button CreateCircleButton(Transform parent, string name, string label, float offsetX)
        {
            var circle = new GameObject(name, typeof(RectTransform), typeof(CircularButtonGraphic), typeof(Button));
            circle.transform.SetParent(parent, false);
            var rect = circle.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(210, 210);
            AnchorToBottom(rect);
            rect.anchoredPosition = new Vector2(offsetX, 76);
            var graphic = circle.GetComponent<CircularButtonGraphic>();
            graphic.color = new Color(.035f, .16f, .25f, .94f);
            var button = circle.GetComponent<Button>();
            button.targetGraphic = graphic;
            var symbol = CreateText(circle.transform, "Arrow", label, Vector2.zero, 76);
            symbol.rectTransform.sizeDelta = rect.sizeDelta;
            symbol.raycastTarget = false;
            CreateHudFrame(circle.transform, new Vector2(196, 196));
            return button;
        }

        private static GameObject CreateHudPanel(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var panel = CreatePanel(parent, name, position, size, new Color(.025f, .10f, .18f, .76f));
            panel.GetComponent<Image>().raycastTarget = false;
            CreateHudFrame(panel.transform, size - new Vector2(8, 8));
            return panel;
        }

        private static Image CreateHpBar(Transform parent, string name, Vector2 position, bool rightToLeft)
        {
            var track = new GameObject(name, typeof(RectTransform), typeof(Image));
            track.transform.SetParent(parent, false);
            var rect = track.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(354, 31);
            rect.anchoredPosition = position;
            var trackImage = track.GetComponent<Image>();
            trackImage.color = new Color(.015f, .045f, .07f, .92f);
            trackImage.raycastTarget = false;

            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(track.transform, false);
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(.03f, .18f);
            fillRect.anchorMax = new Vector2(.97f, .82f);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            var fill = fillObject.GetComponent<Image>();
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.color = new Color(.24f, .91f, .31f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = rightToLeft ? 1 : 0;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;
            return fill;
        }

        private static void CreateHudFrame(Transform parent, Vector2 size)
        {
            var cyan = new Color(.08f, .92f, .98f, .92f);
            CreateDecorativeEdge(parent, "Cyan Border Top", new Vector2(0, size.y * .5f), new Vector2(size.x, 5), cyan);
            CreateDecorativeEdge(parent, "Cyan Border Bottom", new Vector2(0, -size.y * .5f), new Vector2(size.x, 5), cyan);
            CreateDecorativeEdge(parent, "Cyan Border Left", new Vector2(-size.x * .5f, 0), new Vector2(5, size.y), cyan);
            CreateDecorativeEdge(parent, "Cyan Border Right", new Vector2(size.x * .5f, 0), new Vector2(5, size.y), cyan);
        }

        private static void CreateDecorativeEdge(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var edge = new GameObject(name, typeof(RectTransform), typeof(Image));
            edge.transform.SetParent(parent, false);
            edge.GetComponent<RectTransform>().anchoredPosition = position;
            edge.GetComponent<RectTransform>().sizeDelta = size;
            edge.GetComponent<Image>().color = color;
            edge.GetComponent<Image>().raycastTarget = false;
        }

        private static void CreateStudioLight(Transform parent, string name, Vector3 position, float intensity)
        {
            var lightObject = new GameObject(name, typeof(Light));
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            lightObject.transform.localRotation = Quaternion.LookRotation(-position.normalized, Vector3.up);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = name.Contains("Key") ? new Color(1f, .88f, .78f) : new Color(.60f, .82f, 1f);
        }

        private static void AnchorToBottom(RectTransform rect)
        {
            rect.anchorMin = new Vector2(.5f, 0);
            rect.anchorMax = new Vector2(.5f, 0);
            rect.pivot = new Vector2(.5f, 0);
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position)
        {
            return CreateButton(parent, name, label, position, new Vector2(270, 110), 32);
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, int fontSize)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            buttonObject.GetComponent<Image>().color = new Color(0.42f, 0.22f, 0.08f, 0.92f);
            var text = CreateText(buttonObject.transform, "Label", label, Vector2.zero, fontSize);
            text.rectTransform.sizeDelta = size;
            return buttonObject.GetComponent<Button>();
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static InputField CreateInputField(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var inputObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            inputObject.transform.SetParent(parent, false);
            var rect = inputObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            inputObject.GetComponent<Image>().color = new Color(0.96f, 0.96f, 0.98f, 0.96f);

            var value = CreateText(inputObject.transform, "Text", string.Empty, Vector2.zero, 30);
            value.alignment = TextAnchor.MiddleLeft;
            value.color = new Color(0.08f, 0.08f, 0.10f);
            value.rectTransform.sizeDelta = size - new Vector2(48, 12);
            var placeholder = CreateText(inputObject.transform, "Placeholder", "이름을 입력하세요", Vector2.zero, 30);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(0.38f, 0.39f, 0.44f, 0.72f);
            placeholder.rectTransform.sizeDelta = size - new Vector2(48, 12);

            var input = inputObject.GetComponent<InputField>();
            input.textComponent = value;
            input.placeholder = placeholder;
            input.characterLimit = 16;
            return input;
        }

        private static void AnchorToTop(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static GameObject CreateStatusPanel(Transform parent, string name, string label, Vector2 position)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(920, 90);
            rect.anchoredPosition = position;
            panel.GetComponent<Image>().color = new Color(0.08f, 0.05f, 0.03f, 0.82f);
            CreateText(panel.transform, "Label", label, Vector2.zero, 26);
            return panel;
        }

        private static Text CreateText(Transform parent, string name, string value, Vector2 position, int size)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(900, 100);
            rect.anchoredPosition = position;
            var text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void Assign(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignArray(Object target, string propertyName, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
