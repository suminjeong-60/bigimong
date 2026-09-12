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

        [MenuItem("Bigimong/Create AR Battle Scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/BigimongAR/Scenes");
            Directory.CreateDirectory("Assets/BigimongAR/Prefabs");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sessionObject = new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));
            var originObject = new GameObject("XR Origin", typeof(XROrigin), typeof(ARRaycastManager), typeof(ARPlaneManager));
            var cameraObject = new GameObject("AR Camera", typeof(Camera), typeof(AudioListener), typeof(ARCameraManager), typeof(ARCameraBackground));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(originObject.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = 0.01f;
            originObject.GetComponent<XROrigin>().Camera = camera;
            originObject.GetComponent<ARPlaneManager>().requestedDetectionMode = PlaneDetectionMode.Horizontal;

            var systems = new GameObject("Bigimong AR Systems");
            var arena = systems.AddComponent<ArBattleArenaController>();
            var director = systems.AddComponent<ArBattleDirector>();
            var bridge = systems.AddComponent<ArBattleNativeBridge>();
            var recovery = systems.AddComponent<ArTrackingRecovery>();
            var sharedAnchor = systems.AddComponent<SharedAnchorCoordinator>();

            var placement = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            placement.name = "Placement Indicator";
            placement.transform.localScale = new Vector3(0.55f, 0.008f, 0.55f);
            Object.DestroyImmediate(placement.GetComponent<Collider>());
            placement.SetActive(false);
            var ringPrefab = CreateRingPrefab();

            var hud = CreateHud(bridge, out var trackingPanel, out var disconnectedPanel);
            Assign(arena, "raycastManager", originObject.GetComponent<ARRaycastManager>());
            Assign(arena, "planeManager", originObject.GetComponent<ARPlaneManager>());
            Assign(arena, "placementIndicator", placement);
            Assign(arena, "battleRingPrefab", ringPrefab);
            Assign(director, "arena", arena);
            Assign(director, "sharedAnchor", sharedAnchor);
            Assign(director, "hud", hud);
            Assign(director, "trackingLostPanel", trackingPanel);
            Assign(director, "disconnectedPanel", disconnectedPanel);
            Assign(bridge, "director", director);
            Assign(bridge, "hud", hud);
            Assign(recovery, "director", director);
            Assign(sharedAnchor, "arena", arena);
            Assign(sharedAnchor, "nativeBridge", bridge);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeGameObject = systems;
            Debug.Log($"Bigimong AR battle scene created: {ScenePath}", sessionObject);
        }

        private static GameObject CreateRingPrefab()
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            source.name = "ArBattleRing";
            source.transform.localScale = new Vector3(1f, 0.018f, 1f);
            Object.DestroyImmediate(source.GetComponent<Collider>());
            var renderer = source.GetComponent<Renderer>();
            renderer.sharedMaterial.color = new Color(0.95f, 0.48f, 0.12f, 0.72f);
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

            var countdown = CreateText(canvasObject.transform, "Countdown", "10s", new Vector2(0, 690), 60);
            var playerAHp = CreateText(canvasObject.transform, "Player A HP", "A  HP 5", new Vector2(-300, 810), 34);
            var playerBHp = CreateText(canvasObject.transform, "Player B HP", "B  HP 5", new Vector2(300, 810), 34);
            var roundStatus = CreateText(canvasObject.transform, "Round Status", "ROUND 1", new Vector2(0, 590), 30);
            var left = CreateButton(canvasObject.transform, "Left", "LEFT", new Vector2(-330, -720));
            var center = CreateButton(canvasObject.transform, "Center", "CENTER", new Vector2(0, -720));
            var right = CreateButton(canvasObject.transform, "Right", "RIGHT", new Vector2(330, -720));
            trackingPanel = CreateStatusPanel(canvasObject.transform, "TrackingLost", "바닥 인식이 끊겼어요 · 앵커를 다시 잡아주세요", new Vector2(0, 520));
            disconnectedPanel = CreateStatusPanel(canvasObject.transform, "Disconnected", "연결 복구 중 · 서버 자동플레이 진행", new Vector2(0, 410));
            trackingPanel.SetActive(false);
            disconnectedPanel.SetActive(false);

            var hud = canvasObject.AddComponent<ArBattleHud>();
            Assign(hud, "bridge", bridge);
            Assign(hud, "countdownText", countdown);
            Assign(hud, "playerAHpText", playerAHp);
            Assign(hud, "playerBHpText", playerBHp);
            Assign(hud, "roundStatusText", roundStatus);
            Assign(hud, "leftButton", left);
            Assign(hud, "centerButton", center);
            Assign(hud, "rightButton", right);
            return hud;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(270, 110);
            rect.anchoredPosition = position;
            buttonObject.GetComponent<Image>().color = new Color(0.42f, 0.22f, 0.08f, 0.92f);
            CreateText(buttonObject.transform, "Label", label, Vector2.zero, 32);
            return buttonObject.GetComponent<Button>();
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
            return text;
        }

        private static void Assign(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
