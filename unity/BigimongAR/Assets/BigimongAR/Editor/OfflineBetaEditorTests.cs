#if UNITY_EDITOR
using System;
using System.Reflection;
using Bigimong.AR;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Bigimong.AR.EditorChecks
{
    public static class OfflineBetaEditorChecks
    {
        public static void RunBehaviorChecks()
        {
            Require(OfflineBetaFlowController.FirstPhase(false) == OfflineBetaPhase.AvatarCreate,
                "first launch must require an avatar");
            Require(OfflineBetaFlowController.FirstPhase(true) == OfflineBetaPhase.PetTestSelect,
                "saved avatar must reach the pet selector");
        }

        public static void RunSceneChecks()
        {
            var systems = GameObject.Find("Bigimong AR Systems");
            Require(systems != null, "offline beta systems missing");
            var flow = systems.GetComponent<OfflineBetaFlowController>();
            var bridge = systems.GetComponent<ArBattleNativeBridge>();
            var demo = systems.GetComponent<ArBattleOfflineDemo>();
            Require(flow != null && bridge != null && demo != null, "offline beta components missing");
            Require(ReferenceEquals(Field<OfflineBetaFlowController>(bridge, "offlineFlow"), flow), "host payload gate missing");
            foreach (var field in new[] { "avatarCreator", "arena", "director", "summonDirector", "offlineDemo", "hud", "cameraTransform",
                         "battleCanvas", "petCanvas", "encounterCanvas", "scanCanvas", "resultCanvas", "choosePetButton", "acceptButton", "fallbackButton", "rematchButton", "returnButton" })
                Require(Field<UnityEngine.Object>(flow, field) != null, $"offline beta reference {field} missing");

            var hud = Field<ArBattleHud>(flow, "hud");
            var safe = Field<RectTransform>(hud, "safeArea");
            Require(safe != null, "HUD safe area missing");
            var surrenderButton = Field<Button>(hud, "surrenderButton");
            Require(surrenderButton != null && surrenderButton.GetComponent<ButtonPressMotion>() != null,
                "surrenderButton and tactile motion missing");
            Require(surrenderButton.transform.Find("Label")?.GetComponent<TMP_Text>()?.text == "기권",
                "surrenderButton label wrong");
            foreach (var (name, label) in new[] { ("Left", "←"), ("Center", "↑"), ("Right", "→") })
            {
                var button = safe.Find(name)?.GetComponent<Button>();
                Require(button != null && button.targetGraphic is CircularButtonGraphic, $"{name} circle missing");
                var arrow = button.transform.Find("Arrow")?.GetComponent<TMP_Text>();
                Require(arrow != null && arrow.text == label, $"{name} direction wrong");
            }
            foreach (var field in new[] { "petCanvas", "encounterCanvas", "scanCanvas", "resultCanvas" })
            {
                var canvas = Field<GameObject>(flow, field);
                Require(!canvas.activeSelf && canvas.GetComponent<Canvas>() != null, $"{field} must start hidden");
                Require(canvas.transform.Find("Safe Area")?.GetComponent<DeviceSafeArea>() != null, $"{field} safe area missing");
            }
            Require(Field<ArBattleArenaController>(systems.GetComponent<ArTrackingRecovery>(), "arena") != null,
                "screen-fixed mode must bypass AR tracking loss");
        }

        private static T Field<T>(object source, string name) where T : class
        {
            var info = source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return info?.GetValue(source) as T;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
