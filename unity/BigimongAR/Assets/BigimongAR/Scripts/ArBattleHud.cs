using System;
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
        private double remainingAtStartMilliseconds;
        private float turnStartedAt;
        private bool submitted;

        private void Awake()
        {
            leftButton?.onClick.AddListener(() => Choose("LEFT"));
            centerButton?.onClick.AddListener(() => Choose("CENTER"));
            rightButton?.onClick.AddListener(() => Choose("RIGHT"));
        }

        private void Update()
        {
            var elapsed = (Time.realtimeSinceStartup - turnStartedAt) * 1000d;
            var remaining = Math.Max(0d, remainingAtStartMilliseconds - elapsed);
            if (countdownText != null) countdownText.text = $"{(long)Math.Ceiling(remaining / 1000d)}s";
            SetButtons(!submitted && remaining > 0);
        }

        public void BeginTurn(long deadlineAtMilliseconds, long serverNowMilliseconds)
        {
            remainingAtStartMilliseconds = Math.Max(0, deadlineAtMilliseconds - serverNowMilliseconds);
            turnStartedAt = Time.realtimeSinceStartup;
            submitted = false;
        }

        public void SetBattleState(int round, int hpA, int hpB, string status, string winner)
        {
            if (playerAHpText != null) playerAHpText.text = $"A  HP {Math.Max(0, hpA)}";
            if (playerBHpText != null) playerBHpText.text = $"B  HP {Math.Max(0, hpB)}";
            if (roundStatusText == null) return;
            roundStatusText.text = status == "FINISHED"
                ? $"전투 종료 · {winner} 승리"
                : $"ROUND {Math.Max(1, round)}";
        }

        public void ShowRoundResult(BattleRoundMessage round)
        {
            if (roundStatusText == null || round == null) return;
            var automatic = round.attackAutomatic || round.defendAutomatic ? " · 서버 자동선택" : string.Empty;
            var result = round.outcome.Contains("DODGE") ? "회피" : $"피해 {round.damage}";
            roundStatusText.text = $"ROUND {round.round} · {result}{automatic}";
        }

        private void Choose(string direction)
        {
            if (submitted) return;
            submitted = true;
            bridge?.SubmitChoice(direction);
        }

        private void SetButtons(bool enabled)
        {
            if (leftButton != null) leftButton.interactable = enabled;
            if (centerButton != null) centerButton.interactable = enabled;
            if (rightButton != null) rightButton.interactable = enabled;
        }
    }
}
