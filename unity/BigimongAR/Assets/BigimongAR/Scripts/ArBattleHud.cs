using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Bigimong.AR
{
    public sealed class ArBattleHud : MonoBehaviour
    {
        [SerializeField] private ArBattleNativeBridge bridge;
        [SerializeField] private Text countdownText;
        [SerializeField] private Text playerAHpText;
        [SerializeField] private Text playerBHpText;
        [SerializeField] private Text roundStatusText;
        [SerializeField] private Button leftButton;
        [SerializeField] private Button centerButton;
        [SerializeField] private Button rightButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private Image impactFlash;
        [SerializeField] private Text betaBadge;
        private double remainingAtStartMilliseconds;
        private float turnStartedAt;
        private bool submitted;
        private bool inputLocked;
        private Rect lastSafeArea;
        private Coroutine pressAnimation;
        private Coroutine impactAnimation;
        private bool offlineBadgeVisible;

        private void Awake()
        {
            leftButton?.onClick.AddListener(() => Choose("LEFT"));
            centerButton?.onClick.AddListener(() => Choose("CENTER"));
            rightButton?.onClick.AddListener(() => Choose("RIGHT"));
            restartButton?.onClick.AddListener(() => bridge?.RestartOfflineDemo());
            if (restartButton != null) restartButton.gameObject.SetActive(false);
            ApplySafeArea();
        }

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea) ApplySafeArea();
            var elapsed = (Time.realtimeSinceStartup - turnStartedAt) * 1000d;
            var remaining = Math.Max(0d, remainingAtStartMilliseconds - elapsed);
            if (countdownText != null) countdownText.text = $"{(long)Math.Ceiling(remaining / 1000d)}s";
            SetButtons(!inputLocked && !submitted && remaining > 0);
        }

        public void BeginTurn(long deadlineAtMilliseconds, long serverNowMilliseconds)
        {
            remainingAtStartMilliseconds = Math.Max(0, deadlineAtMilliseconds - serverNowMilliseconds);
            turnStartedAt = Time.realtimeSinceStartup;
            submitted = false;
            if (restartButton != null) restartButton.gameObject.SetActive(false);
        }

        public void SetInputLocked(bool value)
        {
            inputLocked = value;
            if (value) SetButtons(false);
            if (leftButton != null) leftButton.gameObject.SetActive(!value);
            if (centerButton != null) centerButton.gameObject.SetActive(!value);
            if (rightButton != null) rightButton.gameObject.SetActive(!value);
        }

        public bool InputLocked => inputLocked;

        public void SetOfflineBadgeVisible(bool visible)
        {
            offlineBadgeVisible = visible;
            if (betaBadge != null) betaBadge.gameObject.SetActive(visible);
            if (!visible && restartButton != null) restartButton.gameObject.SetActive(false);
        }

        public void PlayImpactPulse(float intensity)
        {
            if (impactFlash == null) return;
            if (impactAnimation != null) StopCoroutine(impactAnimation);
            impactAnimation = StartCoroutine(ImpactPulse(Mathf.Clamp(intensity, 0f, .45f)));
        }

        private IEnumerator ImpactPulse(float intensity)
        {
            for (var elapsed = 0f; elapsed < .24f; elapsed += Time.unscaledDeltaTime)
            {
                var alpha = intensity * Mathf.Sin(Mathf.Clamp01(elapsed / .24f) * Mathf.PI);
                impactFlash.color = new Color(.25f, .08f, .05f, alpha);
                yield return null;
            }
            impactFlash.color = new Color(.25f, .08f, .05f, 0);
            impactAnimation = null;
        }

        public void SetBattleState(int round, int hpA, int hpB, string status, string winner)
        {
            if (playerAHpText != null) playerAHpText.text = $"A  HP {Math.Max(0, hpA)}";
            if (playerBHpText != null) playerBHpText.text = $"B  HP {Math.Max(0, hpB)}";
            if (roundStatusText == null) return;
            roundStatusText.text = status == "FINISHED"
                ? $"전투 종료 · {winner} 승리"
                : $"ROUND {Math.Max(1, round)}";
            if (status == "FINISHED")
            {
                submitted = true;
                SetButtons(false);
                if (restartButton != null) restartButton.gameObject.SetActive(offlineBadgeVisible);
            }
        }

        public void ShowPlacementPrompt()
        {
            submitted = true;
            remainingAtStartMilliseconds = 0;
            if (countdownText != null) countdownText.text = "바닥을 찾는 중";
            if (roundStatusText != null) roundStatusText.text = "바닥이 보이면 화면을 눌러 경기장을 배치하세요";
            SetButtons(false);
            if (restartButton != null) restartButton.gameObject.SetActive(false);
        }

        public void ShowRoundResult(BattleRoundMessage round)
        {
            if (roundStatusText == null || round == null) return;
            var automatic = round.attackAutomatic || round.defendAutomatic
                ? (offlineBadgeVisible ? " · 자동선택" : " · 서버 자동선택") : string.Empty;
            var result = round.outcome.Contains("DODGE") ? "회피" : $"피해 {round.damage}";
            roundStatusText.text = $"ROUND {round.round} · {result}{automatic}";
        }

        private void Choose(string direction)
        {
            if (submitted || inputLocked) return;
            submitted = true;
            var button = direction == "LEFT" ? leftButton : direction == "RIGHT" ? rightButton : centerButton;
            if (button != null)
            {
                if (pressAnimation != null) StopCoroutine(pressAnimation);
                pressAnimation = StartCoroutine(PressFeedback(button.transform as RectTransform));
            }
            bridge?.SubmitChoice(direction);
        }

        private IEnumerator PressFeedback(RectTransform target)
        {
            if (target == null) yield break;
            yield return Scale(target, 1f, .92f, .06f);
            yield return Scale(target, .92f, 1.05f, .08f);
            yield return Scale(target, 1.05f, 1f, .06f);
            target.localScale = Vector3.one;
            pressAnimation = null;
        }

        private static IEnumerator Scale(RectTransform target, float from, float to, float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                target.localScale = Vector3.one * Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }
            target.localScale = Vector3.one * to;
        }

        private void OnDisable()
        {
            if (leftButton != null) leftButton.transform.localScale = Vector3.one;
            if (centerButton != null) centerButton.transform.localScale = Vector3.one;
            if (rightButton != null) rightButton.transform.localScale = Vector3.one;
            if (impactFlash != null) impactFlash.color = new Color(.25f, .08f, .05f, 0);
        }

        private void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            if (safeArea == null || Screen.width <= 0 || Screen.height <= 0) return;
            safeArea.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        private void SetButtons(bool enabled)
        {
            if (leftButton != null) leftButton.interactable = enabled;
            if (centerButton != null) centerButton.interactable = enabled;
            if (rightButton != null) rightButton.interactable = enabled;
        }
    }
}
