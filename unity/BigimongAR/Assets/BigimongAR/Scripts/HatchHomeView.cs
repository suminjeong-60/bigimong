using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bigimong.AR
{
    /// <summary>Event-driven focus-stage UI. The only Update work is visual pulse/countdown/viewport sizing.</summary>
    public sealed class HatchHomeView : MonoBehaviour
    {
        [SerializeField] private HatchHomeCoordinator coordinator;
        [SerializeField] private HomeFocusStage stage;
        [SerializeField] private HatchSequenceDirector sequence;
        [SerializeField] private HomeStageOrbitInput orbit;
        [SerializeField] private HatchHomeStepBridge walkingProvider;
        [SerializeField] private CharacterPrefabCatalog characterCatalog;
        [SerializeField] private BigimongReferenceUi referenceUi;
        [SerializeField] private OfflineBetaFlowController flow;
        [SerializeField] private BigimongTypographyTheme theme;
        [SerializeField] private RawImage viewport;
        [SerializeField] private Image cover;
        [SerializeField] private RawImage silhouette;
        [SerializeField] private TMP_Text statusLabel, subjectLabel, avatarLabel, progressLabel, cooldownLabel, hatchLabel, speciesLabel, noticeLabel;
        [SerializeField] private Image progressFill;
        [SerializeField] private Button subjectButton, avatarButton, frontButton, hatchButton, retryButton;
        [SerializeField] private Button[] navigationButtons;
        [SerializeField] private string[] navigationRoutes;
        private HatchHomeSnapshot rendered;
        private bool initialized, locked, visible;
        private int countdownSecond = -1;
        private Canvas canvas;
        public bool IsVisible => visible;

        private void Awake() { canvas = GetComponent<Canvas>(); canvas.enabled = false; }

        public void EnsureInitialized()
        {
            if (initialized) return;
            if (coordinator == null || stage == null || sequence == null || orbit == null || cover == null || silhouette == null ||
                viewport == null || theme == null || referenceUi == null || flow == null)
                throw new InvalidOperationException("Hatch home scene bindings are incomplete.");
            initialized = true;
            stage.Initialize(new HomeCharacterResolver(characterCatalog));
            stage.BindViewport(viewport);
            cover.transform.SetAsLastSibling();
            silhouette.transform.SetAsLastSibling();
            sequence.Configure(stage, cover, orbit, silhouette);
            GetComponent<HatchAudioBinding>().EnsureBound();
            var store = new HatchHomeStore();
            var clock = new SystemCareClock();
            coordinator.Configure(store, new HatchProgressService(store, clock),
                new HatchSelectionService(store, new UnityArtIdRandomSource()), stage, sequence, walkingProvider, clock, AvatarProfileStore.Load());
            coordinator.SnapshotChanged += Render;
            coordinator.NoticeRequested += ShowNotice;
            stage.RetryNotice += ShowNotice;
            coordinator.InputLockChanged += SetInputLocked;
            orbit.Tapped += coordinator.TapSubject;
            subjectButton.onClick.AddListener(() => coordinator.ShowSubject(IsHatched(rendered) ? HomeSubject.DINOSAUR : HomeSubject.EGG));
            avatarButton.onClick.AddListener(() => coordinator.ShowSubject(HomeSubject.AVATAR));
            frontButton.onClick.AddListener(coordinator.ResetFront);
            hatchButton.onClick.AddListener(coordinator.BeginHatch);
            retryButton.onClick.AddListener(() => { ShowNotice(""); coordinator.RetryHatch(); });
            for (var i = 0; i < navigationButtons.Length; i++)
            {
                var route = navigationRoutes[i];
                navigationButtons[i].onClick.AddListener(() => referenceUi.Activate(route));
            }
            coordinator.Initialize();
            Render(coordinator.Snapshot);
        }

        public void Show()
        {
            referenceUi.ClaimHomeInputOwnership();
            EnsureInitialized();
            canvas ??= GetComponent<Canvas>();
            if (!visible)
            {
                stage.Initialize(new HomeCharacterResolver(characterCatalog));
                stage.BindViewport(viewport);
                cover.transform.SetAsLastSibling();
                silhouette.transform.SetAsLastSibling();
                sequence.Configure(stage, cover, orbit, silhouette);
                coordinator.ResumePresentation();
            }
            canvas.enabled = visible = true;
            GetComponent<CanvasGroup>().interactable = GetComponent<CanvasGroup>().blocksRaycasts = true;
            Render(coordinator.Snapshot);
            if (EventSystem.current != null && !locked) EventSystem.current.SetSelectedGameObject(subjectButton.gameObject);
        }

        public void Hide()
        {
            canvas ??= GetComponent<Canvas>();
            canvas.enabled = visible = false;
            GetComponent<CanvasGroup>().interactable = GetComponent<CanvasGroup>().blocksRaycasts = false;
            referenceUi?.ReleaseHomeInputOwnership();
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform)) EventSystem.current.SetSelectedGameObject(null);
            if (!initialized) return;
            // Disable presentation only after navigation has been accepted (never during hatch).
            coordinator.SuspendPresentation();
        }

        private static bool IsHatched(HatchHomeSnapshot snapshot) => snapshot != null &&
            snapshot.Phase is HatchHomePhase.REVEAL or HatchHomePhase.HOME;
        private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

        public void Render(HatchHomeSnapshot snapshot)
        {
            if (snapshot == null) return;
            rendered = snapshot.Clone();
            var hatched = IsHatched(snapshot);
            subjectLabel.text = hatched ? "공룡" : "비기알";
            avatarLabel.text = "아바타";
            frontButton.GetComponentInChildren<TMP_Text>().text = "정면 보기";
            statusLabel.text = Number(snapshot.coins) + " 비기코인";
            progressLabel.text = Number(snapshot.eggProgress) + " / 30,000";
            progressFill.fillAmount = snapshot.eggProgress / 30000f;
            progressLabel.transform.parent.gameObject.SetActive(!hatched);
            hatchButton.gameObject.SetActive(!hatched);
            hatchLabel.text = snapshot.eggProgress == 30000 ? "부화하기" : Number(30000 - snapshot.eggProgress) + " 더 필요해요";
            hatchButton.GetComponent<Image>().color = snapshot.Phase == HatchHomePhase.HATCH_READY
                ? BigimongTypographyTheme.Gold : new Color(.55f, .43f, .33f);
            speciesLabel.transform.parent.gameObject.SetActive(hatched && snapshot.ActiveSubject == HomeSubject.DINOSAUR);
            if (hatched)
            {
                var species = BigimongSpeciesCatalog.Resolve(snapshot.selectedArtId);
                speciesLabel.text = species.ArtId.ToString("00") + " " + species.KoreanName + "\n" + species.GrowthStage + " · 등급: " + species.Grade;
            }
            countdownSecond = -1;
            RefreshCountdown();
            SetInputLocked(coordinator != null && coordinator.InputLocked);
        }

        private void SetInputLocked(bool value)
        {
            if (visible || value) referenceUi?.SuspendRetainedOverlayInput();
            locked = value || rendered?.Phase == HatchHomePhase.HATCHING;
            foreach (var button in GetComponentsInChildren<Button>(true)) button.interactable = !locked;
            hatchButton.interactable = !locked && rendered?.Phase == HatchHomePhase.HATCH_READY;
            retryButton.gameObject.SetActive(coordinator != null && coordinator.NeedsRetry);
            retryButton.interactable = coordinator != null && coordinator.NeedsRetry && !sequence.IsPlaying;
            if (initialized) orbit.SetInputLocked(locked || stage.InputLocked);
            if (visible && EventSystem.current != null && (EventSystem.current.currentSelectedGameObject == null ||
                !EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform) ||
                EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>()?.IsInteractable() == false))
                EventSystem.current.SetSelectedGameObject(retryButton.interactable ? retryButton.gameObject : !locked ? subjectButton.gameObject : null);
        }

        private void ShowNotice(string message)
        {
            if (message == "걸음 연동 준비 중" && !string.IsNullOrEmpty(noticeLabel.text)) return;
            noticeLabel.text = message;
            SetInputLocked(coordinator.InputLocked);
        }

        private void RefreshCountdown()
        {
            if (rendered == null) return;
            var effective = Math.Max((coordinator != null ? coordinator.UtcNow : DateTime.UtcNow).Ticks, rendered.lastObservedUtcTicks);
            var seconds = (int)Math.Max(0, Math.Ceiling((rendered.nextCareAtUtcTicks - effective) / (double)TimeSpan.TicksPerSecond));
            if (seconds == countdownSecond) return;
            countdownSecond = seconds;
            cooldownLabel.text = seconds == 0 ? "알을 살짝 눌러 돌봐주세요" : TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
        }

        private void Update()
        {
            if (!visible || !initialized) return;
            RefreshCountdown();
            if (hatchButton.interactable)
                hatchButton.GetComponent<Image>().color = Color.Lerp(BigimongTypographyTheme.Gold, BigimongTypographyTheme.Cream,
                    .12f + .12f * Mathf.Sin(Time.unscaledTime * 3f));
            var size = viewport.rectTransform.rect.size * GetComponent<Canvas>().scaleFactor;
            stage.SetViewportSize(size, 1f);
            if (walkingProvider != null && !walkingProvider.IsAvailable && string.IsNullOrEmpty(noticeLabel.text))
                noticeLabel.text = "걸음 연동 준비 중";
        }

        private void OnDestroy()
        {
            if (!initialized) return;
            coordinator.SnapshotChanged -= Render;
            coordinator.NoticeRequested -= ShowNotice;
            stage.RetryNotice -= ShowNotice;
            coordinator.InputLockChanged -= SetInputLocked;
            orbit.Tapped -= coordinator.TapSubject;
        }
    }
}
