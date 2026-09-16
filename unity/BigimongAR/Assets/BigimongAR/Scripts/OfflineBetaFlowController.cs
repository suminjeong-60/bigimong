using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.ARFoundation;

namespace Bigimong.AR
{
    public enum OfflineBetaPhase
    {
        AvatarCreate, PetTestSelect, TyrannosaurEncounter, ArScan,
        SummonSequence, Battle, Result
    }

    /// <summary>The standalone beta only. An Android battle payload never starts this component.</summary>
    public sealed class OfflineBetaFlowController : MonoBehaviour
    {
        [Serializable] private sealed class PetSelection { public int schemaVersion = 1; public int artId = 1; }
        private const int opponentArtId = 1;
        private static readonly string[] BetaSpeciesNames =
        {
            "티라노사우루스", "트리케라톱스", "익룡", "스테고사우루스", "브라키오사우루스",
            "스피노사우루스", "안킬로사우루스", "벨로키랍토르", "코리토사우루스", "카르노타우루스",
            "모사사우루스", "알로사우루스", "케찰코아틀루스", "데이노니쿠스", "유오플로케팔루스",
            "바리오닉스", "오비랍토르", "프로토케라톱스", "갈리미무스", "드라코렉스",
            "기가노토사우루스", "딜로포사우루스", "이구아노돈", "켄트로사우루스", "테리지노사우루스",
            "콤프소그나투스", "파라사우롤로푸스", "미크로랍토르", "브론토사우루스", "티타노사우루스"
        };
        [SerializeField] private AvatarCreatorController avatarCreator;
        [SerializeField] private ArBattleArenaController arena;
        [SerializeField] private ArBattleDirector director;
        [SerializeField] private SummonSequenceDirector summonDirector;
        [SerializeField] private ArBattleOfflineDemo offlineDemo;
        [SerializeField] private ArBattleHud hud;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private GameObject battleCanvas;
        [SerializeField] private GameObject petCanvas;
        [SerializeField] private GameObject encounterCanvas;
        [SerializeField] private GameObject scanCanvas;
        [SerializeField] private GameObject resultCanvas;
        [SerializeField] private TMP_Text petText;
        [SerializeField] private TMP_Text encounterText;
        [SerializeField] private TMP_Text scanText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button previousPetButton;
        [SerializeField] private Button nextPetButton;
        [SerializeField] private Button choosePetButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button fallbackButton;
        [SerializeField] private Button rematchButton;
        [SerializeField] private Button returnButton;

        private AvatarProfile profile;
        private int selectedArtId = 1;
        private float scanStartedAt;
        private bool listenersBound;
        public bool IsActive { get; private set; }
        public OfflineBetaPhase Phase { get; private set; }
        public int SelectedArtId => selectedArtId;
        public event Action HomeRequested;
        public static OfflineBetaPhase FirstPhase(bool savedProfileExists) =>
            savedProfileExists ? OfflineBetaPhase.PetTestSelect : OfflineBetaPhase.AvatarCreate;
        public bool CanSubmitChoice => IsActive && Phase == OfflineBetaPhase.Battle &&
            offlineDemo != null && !offlineDemo.IsResolving && director != null && !director.IsRoundPlaying &&
            (arena != null && arena.IsScreenFixed || ARSession.state == ARSessionState.SessionTracking);

        private string SelectionPath => Path.Combine(Application.persistentDataPath, "beta-pet-selection-v1.json");

        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();

        private void Bind()
        {
            if (listenersBound) return;
            listenersBound = true;
            if (avatarCreator != null)
            {
                avatarCreator.Completed += OnAvatarSaved;
                avatarCreator.EditingStarted += OnAvatarEditing;
            }
            if (offlineDemo != null)
            {
                offlineDemo.BattlePrepared += OnBattlePrepared;
                offlineDemo.BattleFinished += OnBattleFinished;
            }
            if (summonDirector != null) summonDirector.SummoningCompleted += OnSummoningCompleted;
            if (hud != null) hud.SurrenderRequested += SurrenderToHome;
            previousPetButton?.onClick.AddListener(PreviousPet);
            nextPetButton?.onClick.AddListener(NextPet);
            choosePetButton?.onClick.AddListener(PreviewEncounter);
            backButton?.onClick.AddListener(BackToPetSelection);
            acceptButton?.onClick.AddListener(AcceptEncounter);
            fallbackButton?.onClick.AddListener(UseScreenFixedFallback);
            rematchButton?.onClick.AddListener(Rematch);
            returnButton?.onClick.AddListener(ReturnToPetSelection);
        }

        private void Unbind()
        {
            if (!listenersBound) return;
            listenersBound = false;
            if (avatarCreator != null)
            {
                avatarCreator.Completed -= OnAvatarSaved;
                avatarCreator.EditingStarted -= OnAvatarEditing;
            }
            if (offlineDemo != null)
            {
                offlineDemo.BattlePrepared -= OnBattlePrepared;
                offlineDemo.BattleFinished -= OnBattleFinished;
            }
            if (summonDirector != null) summonDirector.SummoningCompleted -= OnSummoningCompleted;
            if (hud != null) hud.SurrenderRequested -= SurrenderToHome;
            previousPetButton?.onClick.RemoveListener(PreviousPet);
            nextPetButton?.onClick.RemoveListener(NextPet);
            choosePetButton?.onClick.RemoveListener(PreviewEncounter);
            backButton?.onClick.RemoveListener(BackToPetSelection);
            acceptButton?.onClick.RemoveListener(AcceptEncounter);
            fallbackButton?.onClick.RemoveListener(UseScreenFixedFallback);
            rematchButton?.onClick.RemoveListener(Rematch);
            returnButton?.onClick.RemoveListener(ReturnToPetSelection);
        }

        public void StartBeta()
        {
            Bind();
            IsActive = true;
            profile = AvatarProfileStore.Load();
            selectedArtId = LoadPetSelection();
            arena?.SetPlacementEnabled(false);
            hud?.SetInputLocked(true);
            hud?.SetOfflineBadgeVisible(true);
            SetPhase(FirstPhase(AvatarProfileStore.HasSavedProfile));
        }

        public void DisableForOnlineHost()
        {
            IsActive = false;
            offlineDemo?.StopDemo();
            petCanvas?.SetActive(false);
            encounterCanvas?.SetActive(false);
            scanCanvas?.SetActive(false);
            resultCanvas?.SetActive(false);
            avatarCreator?.SetOnlineBattleMode();
            arena?.SetPlacementEnabled(true);
            hud?.SetInputLocked(false);
            hud?.SetOfflineBadgeVisible(false);
            hud?.SetSurrenderVisible(false);
        }

        private void Update()
        {
            if (!IsActive) return;
            if (Phase == OfflineBetaPhase.ArScan)
            {
                var unsupported = ARSession.state == ARSessionState.Unsupported;
                if (fallbackButton != null)
                    fallbackButton.gameObject.SetActive(unsupported || Time.unscaledTime - scanStartedAt >= 8f);
                if (scanText != null) scanText.text = unsupported
                    ? "AR을 사용할 수 없어요 · 화면 고정 테스트를 선택하세요"
                    : "바닥을 천천히 비춘 뒤 눌러서 경기장을 배치하세요";
            }
            if (Phase == OfflineBetaPhase.SummonSequence && summonDirector != null && !summonDirector.IsPlaying)
            {
                // Tracking loss cancels the generation in ArBattleDirector; a new anchor starts a new sequence.
                arena?.RequestReanchor();
                SetPhase(OfflineBetaPhase.ArScan);
            }
            if (Phase == OfflineBetaPhase.Battle)
                offlineDemo?.SetTrackingPaused(arena == null || !arena.IsScreenFixed && ARSession.state != ARSessionState.SessionTracking);
            if (Phase == OfflineBetaPhase.Battle || Phase == OfflineBetaPhase.SummonSequence)
                hud?.SetInputLocked(!CanSubmitChoice);
        }

        private void OnApplicationPause(bool paused)
        {
            if (IsActive && Phase == OfflineBetaPhase.Battle)
                offlineDemo?.SetTrackingPaused(paused || arena == null || !arena.IsScreenFixed && ARSession.state != ARSessionState.SessionTracking);
        }

        private void OnAvatarSaved(AvatarProfile saved)
        {
            if (!IsActive) return;
            profile = saved;
            SetPhase(OfflineBetaPhase.PetTestSelect);
        }

        /// <summary>Persist the reference art's two avatar choices through the existing profile store.</summary>
        public bool ApplyReferenceAvatar(AvatarProfile selected)
        {
            if (!IsActive || (Phase != OfflineBetaPhase.AvatarCreate && Phase != OfflineBetaPhase.PetTestSelect) || selected == null) return false;
            selected.Normalize();
            if (!selected.IsComplete || !AvatarProfileStore.Save(selected)) return false;
            avatarCreator?.SetOnlineBattleMode();
            OnAvatarSaved(selected);
            return true;
        }

        private void OnAvatarEditing()
        {
            if (!IsActive) return;
            offlineDemo?.StopDemo();
            director?.EndOfflineBattle();
            arena?.SetPlacementEnabled(false);
            arena?.RequestReanchor();
            SetPhase(OfflineBetaPhase.AvatarCreate);
        }

        public void SelectPet(int artId)
        {
            if (!IsActive || Phase != OfflineBetaPhase.PetTestSelect) return;
            selectedArtId = Mathf.Clamp(artId, 1, 30);
            SavePetSelection();
            if (petText != null) petText.text = $"베타 테스트 선택 · 보상 없음\n{selectedArtId:00} / 30  ·  {BetaSpeciesNames[selectedArtId - 1]}\n{ElementSkillCatalog.Resolve(selectedArtId).displayName}";
        }
        public void PreviousPet() => SelectPet(selectedArtId == 1 ? 30 : selectedArtId - 1);
        public void NextPet() => SelectPet(selectedArtId == 30 ? 1 : selectedArtId + 1);

        public void PreviewEncounter()
        {
            if (!IsActive || Phase != OfflineBetaPhase.PetTestSelect) return;
            SetPhase(OfflineBetaPhase.TyrannosaurEncounter);
        }

        public void BackToPetSelection()
        {
            if (IsActive && Phase == OfflineBetaPhase.TyrannosaurEncounter)
                SetPhase(OfflineBetaPhase.PetTestSelect);
        }

        public void AcceptEncounter()
        {
            if (!IsActive || Phase != OfflineBetaPhase.TyrannosaurEncounter) return;
            offlineDemo?.ConfigureSelection(selectedArtId);
            arena?.SetPlacementEnabled(true);
            offlineDemo?.BeginEncounter();
            scanStartedAt = Time.unscaledTime;
            SetPhase(OfflineBetaPhase.ArScan);
            hud?.ShowPlacementPrompt();
        }

        public void UseScreenFixedFallback()
        {
            if (!IsActive || Phase != OfflineBetaPhase.ArScan || cameraTransform == null) return;
            arena?.PlaceScreenFixed(cameraTransform);
        }

        private void OnBattlePrepared()
        {
            if (!IsActive || Phase != OfflineBetaPhase.ArScan) return;
            SetPhase(OfflineBetaPhase.SummonSequence);
            director?.BeginSummoning(profile);
        }

        private void OnSummoningCompleted()
        {
            if (!IsActive || Phase != OfflineBetaPhase.SummonSequence) return;
            offlineDemo?.EnableTurns();
            SetPhase(OfflineBetaPhase.Battle);
        }

        private void OnBattleFinished(string winner)
        {
            if (!IsActive || Phase != OfflineBetaPhase.Battle) return;
            if (resultText != null) resultText.text = winner == "A" ? "승리! 다시 대전할까요?" : "이번엔 티라노가 이겼어요 · 다시 도전!";
            SetPhase(OfflineBetaPhase.Result);
        }

        public void Rematch()
        {
            if (!IsActive || Phase != OfflineBetaPhase.Result) return;
            ReturnToPetSelection();
            PreviewEncounter();
            AcceptEncounter();
        }

        public void ReturnToPetSelection()
        {
            if (!IsActive || Phase != OfflineBetaPhase.Result) return;
            offlineDemo?.StopDemo();
            director?.EndOfflineBattle();
            arena?.SetPlacementEnabled(false);
            arena?.RequestReanchor();
            SetPhase(OfflineBetaPhase.PetTestSelect);
        }

        public void SurrenderToHome()
        {
            if (!IsActive || Phase != OfflineBetaPhase.ArScan && Phase != OfflineBetaPhase.SummonSequence &&
                Phase != OfflineBetaPhase.Battle && Phase != OfflineBetaPhase.Result) return;
            offlineDemo?.StopDemo();
            director?.EndOfflineBattle();
            summonDirector?.CancelAndReset();
            arena?.SetPlacementEnabled(false);
            arena?.RequestReanchor();
            hud?.SetSurrenderVisible(false);
            SetPhase(OfflineBetaPhase.PetTestSelect);
            HomeRequested?.Invoke();
        }

        private void SetPhase(OfflineBetaPhase value)
        {
            Phase = value;
            petCanvas?.SetActive(value == OfflineBetaPhase.PetTestSelect);
            encounterCanvas?.SetActive(value == OfflineBetaPhase.TyrannosaurEncounter);
            scanCanvas?.SetActive(value == OfflineBetaPhase.ArScan);
            resultCanvas?.SetActive(value == OfflineBetaPhase.Result);
            battleCanvas?.SetActive(value == OfflineBetaPhase.ArScan || value == OfflineBetaPhase.SummonSequence ||
                value == OfflineBetaPhase.Battle || value == OfflineBetaPhase.Result);
            hud?.SetInputLocked(value != OfflineBetaPhase.Battle);
            hud?.SetSurrenderVisible(value == OfflineBetaPhase.Battle);
            if (value == OfflineBetaPhase.PetTestSelect) SelectPet(selectedArtId);
            if (value == OfflineBetaPhase.TyrannosaurEncounter && encounterText != null)
                encounterText.text = $"{selectedArtId:00} 비기몽 VS 티라노사우루스\n오프라인 체험전 · 보상 없음";
            if (value == OfflineBetaPhase.ArScan && fallbackButton != null) fallbackButton.gameObject.SetActive(false);
        }

        private int LoadPetSelection()
        {
            try
            {
                var saved = JsonUtility.FromJson<PetSelection>(File.ReadAllText(SelectionPath));
                return saved != null && saved.schemaVersion == 1 ? Mathf.Clamp(saved.artId, 1, 30) : 1;
            }
            catch (Exception) { return 1; }
        }

        private void SavePetSelection()
        {
            try { File.WriteAllText(SelectionPath, JsonUtility.ToJson(new PetSelection { artId = selectedArtId })); }
            catch (Exception exception) { Debug.LogWarning($"Beta pet selection could not be saved: {exception.Message}"); }
        }
    }
}
