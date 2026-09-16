#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bigimong.AR.EditorChecks
{
    public static class BigimongReferenceUiEditorChecks
    {
        private static readonly string[] Screens = { "loading", "battle-loading", "avatar", "egg", "home" };

        public static void RunSceneChecks()
        {
            foreach (var screen in Screens)
            {
                var path = "ReferenceUi/" + screen;
                var art = Resources.Load<Texture2D>(path);
                if (art == null) throw new InvalidOperationException($"Reference image missing from Unity project: {path}");
                if (art.width != 1030 || art.height != 1536)
                    throw new InvalidOperationException($"Reference art has wrong size: {path} ({art.width}x{art.height})");
            }

            if (Resources.Load<Texture2D>("ReferenceUi/gallery") == null)
                throw new InvalidOperationException("Offline codex gallery is missing");
            for (var artId = 1; artId <= 30; artId++)
            {
                var path = "ReferenceUi/evolution-" + artId.ToString("00");
                if (Resources.Load<Texture2D>(path) == null)
                    throw new InvalidOperationException("Offline species illustration is missing: " + path);
            }

            var view = UnityEngine.Object.FindObjectOfType<BigimongReferenceUi>();
            if (view == null) throw new InvalidOperationException("Reference screen controller is missing from the scene");
            var field = typeof(BigimongReferenceUi).GetField("flow", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null || field.GetValue(view) is not OfflineBetaFlowController)
                throw new InvalidOperationException("Reference screen controller must connect to offline beta flow");
            foreach (var route in new[]
            {
                ("home", "상점", "shop"), ("home", "놀아주기", "play"),
                ("home", "1:1 대전", "battle-loading"), ("home", "도감", "codex"),
                ("home", "퀘스트", "quest"), ("home", "선물", "gift"),
                ("home", "설정", "settings"), ("avatar", "선택 완료", "egg")
            })
                if (BigimongReferenceUi.ResolveDestination(route.Item1, route.Item2) != route.Item3)
                    throw new InvalidOperationException($"Reference button route is invalid: {route.Item1}/{route.Item2}");
            VerifySecondaryOverlaysAndDelegation(view);
            VerifyRetainedTextBindings();
            VerifySurrenderRequestsHome();
        }

        private static void VerifySecondaryOverlaysAndDelegation(BigimongReferenceUi sceneReference)
        {
            var sceneView = (HatchHomeView)Field(sceneReference, "hatchHomeView");
            if (sceneView == null || Field(sceneReference, "hatchHomeCoordinator") == null)
                throw new InvalidOperationException("Core screens must have serialized 3D-home bindings.");
            var reference = UnityEngine.Object.Instantiate(sceneReference);
            var home = UnityEngine.Object.Instantiate(sceneView);
            try
            {
                var coordinator = home.GetComponent<HatchHomeCoordinator>();
                var sequence = home.GetComponent<HatchSequenceDirector>();
                var store = new MemoryStore(); var clock = new SystemCareClock();
                coordinator.Configure(store, new HatchProgressService(store, clock),
                    new HatchSelectionService(store, new UnityArtIdRandomSource()), null, sequence, null, clock);
                coordinator.Initialize();
                Set(home, "coordinator", coordinator); Set(home, "initialized", true);
                Set(home, "referenceUi", reference);
                Set(reference, "retainedOverlayGroups", Array.Empty<CanvasGroup>()); // This fixture never mutates the real scene's input owners.
                Set(reference, "hatchHomeCoordinator", coordinator); Set(reference, "hatchHomeView", home);
                Invoke(home, "Awake"); Invoke(reference, "Awake");
                foreach (var screen in new[] { "egg", "home" })
                {
                    Invoke(reference, "Show", screen);
                    if (!home.IsVisible || reference.GetComponent<Canvas>().enabled ||
                        ((RawImage)Field(reference, "artwork")).texture != null)
                        throw new InvalidOperationException(screen + " must delegate to the home canvas without 2D artwork.");
                }
                foreach (var page in new[] { "shop", "play", "codex", "quest", "gift", "settings", "character", "avatar", "loading", "battle-loading" })
                {
                    Invoke(reference, "Show", page);
                    if (home.IsVisible || !reference.GetComponent<Canvas>().enabled)
                        throw new InvalidOperationException("Secondary route lost ownership: " + page);
                    if (reference.GetComponentsInChildren<Text>(true).Length != 0)
                        throw new InvalidOperationException("Legacy text in active reference overlay " + page);
                    foreach (var text in reference.GetComponentsInChildren<TMP_Text>(true))
                        if (text.font == null || text.raycastTarget || !text.fontSharedMaterial.IsKeywordEnabled("UNDERLAY_ON"))
                            throw new InvalidOperationException("Secondary overlay typography/raycast: " + text.name);
                    foreach (var button in reference.GetComponentsInChildren<Button>(true))
                        if (button.GetComponent<ButtonPressMotion>() == null)
                            throw new InvalidOperationException("Secondary button lacks motion: " + button.name);
                }
                Set(coordinator, "snapshot", new HatchHomeSnapshot
                {
                    phase = "HOME", eggProgress = 30000, selectedArtId = 17, hatchCheckpoint = "REVEALED",
                    hatchedAtUtcTicks = 98765, activeHomeView = "DINOSAUR"
                });
                Invoke(reference, "Show", "play");
                var playLabel = Array.Find(reference.GetComponentsInChildren<TMP_Text>(true), label => label.name == "Game: Play Status");
                if (playLabel == null || playLabel.text != "오비랍토르와 놀기\n경험치 +200 · 비기코인 +50" || coordinator.Snapshot.selectedArtId != 17)
                    throw new InvalidOperationException("Play must show the selected species without changing ownership.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(reference.gameObject);
                UnityEngine.Object.DestroyImmediate(home.gameObject);
            }
        }

        private static void VerifyRetainedTextBindings()
        {
            foreach (var component in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (component is not ArBattleHud && component is not AvatarCreatorController && component is not OfflineBetaFlowController) continue;
                foreach (var field in component.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (field.FieldType == typeof(Text) || field.FieldType == typeof(InputField))
                        throw new InvalidOperationException("Legacy serialized overlay component " + field.Name);
                    if (field.FieldType == typeof(TMP_Text) && field.GetValue(component) is not TMP_Text)
                        throw new InvalidOperationException("TMP serialized binding missing " + field.Name);
                    if (field.FieldType == typeof(TMP_InputField) && (field.GetValue(component) is not TMP_InputField input ||
                        input.textComponent == null || input.textViewport == null))
                        throw new InvalidOperationException("TMP name input binding missing " + field.Name);
                }
            }
        }

        private static void VerifySurrenderRequestsHome()
        {
            var root = new GameObject("Surrender route check");
            // Retained flow writes its independent practice-selection file. Preserve the exact prior bytes.
            var path = Path.Combine(Application.persistentDataPath, "beta-pet-selection-v1.json");
            var existed = File.Exists(path);
            var previous = existed ? File.ReadAllBytes(path) : null;
            try
            {
                var flow = root.AddComponent<OfflineBetaFlowController>();
                Set(flow, "<IsActive>k__BackingField", true);
                Set(flow, "<Phase>k__BackingField", OfflineBetaPhase.Battle);
                var requests = 0; flow.HomeRequested += () => requests++;
                flow.SurrenderToHome();
                if (requests != 1 || flow.Phase != OfflineBetaPhase.PetTestSelect)
                    throw new InvalidOperationException("AR surrender must emit exactly one home request.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (existed) File.WriteAllBytes(path, previous);
                else if (File.Exists(path)) File.Delete(path);
            }
        }

        private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
        private sealed class MemoryStore : IHatchHomeStore
        {
            public HatchLoadResult Load() => new(new HatchHomeSnapshot(), false, "check");
            public bool TrySave(HatchHomeSnapshot snapshot) => true;
        }
    }
}
#endif
