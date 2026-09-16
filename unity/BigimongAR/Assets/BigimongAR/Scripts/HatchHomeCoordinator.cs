using System;
using System.Collections;
using UnityEngine;

namespace Bigimong.AR
{
    /// <summary>The only owner that publishes durable hatch/home phase changes to the UI.</summary>
    public sealed class HatchHomeCoordinator : MonoBehaviour
    {
        private IHatchHomeStore store;
        private HatchProgressService progress;
        private HatchSelectionService selection;
        private HomeSideActivityService sideActivities;
        private HomeFocusStage stage;
        private HatchSequenceDirector sequence;
        private IWalkingProgressProvider provider;
        private ICareClock clock;
        private AvatarProfile avatar;
        private HatchHomeSnapshot snapshot;
        private bool initialized, subscribed;
        private bool saveReloadRequired;
        private bool presentationSuspended;
        private long presentationGeneration;
        private IEnumerator presentationTransaction;
        // Stage request scheduling is separate from awaiting/confirming its owned transaction.
        private Action<HomeSubject> requestSubject;
        public HatchHomeSnapshot Snapshot => snapshot?.Clone();
        public bool InputLocked { get; private set; }
        public bool NeedsRetry { get; private set; }
        public DateTime UtcNow => clock?.UtcNow ?? DateTime.UtcNow;
        public event Action<HatchHomeSnapshot> SnapshotChanged;
        public event Action<string> NoticeRequested;
        public event Action<bool> InputLockChanged;

        public void Configure(IHatchHomeStore homeStore, HatchProgressService progressService,
            HatchSelectionService selectionService, HomeFocusStage focusStage, HatchSequenceDirector director,
            IWalkingProgressProvider walkingProvider, ICareClock careClock, AvatarProfile avatarProfile = null)
        {
            if (initialized) throw new InvalidOperationException("Configure before Initialize.");
            store = homeStore ?? throw new ArgumentNullException(nameof(homeStore));
            progress = progressService ?? throw new ArgumentNullException(nameof(progressService));
            selection = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
            sideActivities = new HomeSideActivityService(store);
            sequence = director ?? throw new ArgumentNullException(nameof(director));
            if (focusStage != null && !director.HasPresentationBindings(focusStage))
                throw new InvalidOperationException("Bind the hatch overlay and orbit before configuring the coordinator.");
            clock = careClock ?? throw new ArgumentNullException(nameof(careClock));
            stage = focusStage;
            provider = walkingProvider;
            avatar = avatarProfile ?? AvatarProfile.CreateDefault();
            requestSubject = RequestSubject;
        }

        public void Initialize()
        {
            if (initialized) return;
            if (store == null) throw new InvalidOperationException("Configure before Initialize.");
            initialized = true;
            Subscribe();
            HatchLoadResult loaded;
            try
            {
                loaded = store.Load();
                snapshot = loaded.Snapshot.Clone();
            }
            catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // This placeholder is render-only and is never published as durable state.
                Debug.LogWarning($"Hatch home initial load failed ({error.GetType().Name}); retry is required.");
                snapshot = new HatchHomeSnapshot();
                snapshot.Normalize();
                saveReloadRequired = true;
                SetLocked(true);
                RetryNotice();
                return;
            }
            if (loaded.Recovered) NotifyNoticeSafely("저장된 진행 상황을 복구했어요.");
            if (provider == null || !provider.IsAvailable) NotifyNoticeSafely("걸음 연동 준비 중");
            Recover();
        }

        public void CareEgg()
        {
            if (!initialized || InputLocked || snapshot.ActiveSubject != HomeSubject.EGG ||
                (stage != null && (stage.InputLocked || stage.ActiveSubject != HomeSubject.EGG || stage.SubjectInstance == null))) return;
            HatchProgressResult result;
            try { result = progress.TryCare(snapshot); }
            catch (HatchSaveOutcomeUnknownException) { ReloadAfterUnknownSave(); return; }
            if (result.Accepted)
            {
                snapshot = result.Snapshot;
                Publish();
                PlayTapReaction(true);
                NotifyNoticeSafely("+500");
            }
            else if (result.Status == HatchProgressStatus.COOLDOWN)
            {
                PlayTapReaction(false);
                NotifyNoticeSafely($"다음 돌보기까지 {result.RemainingCooldown:hh\\:mm\\:ss}");
            }
            else if (result.Status == HatchProgressStatus.SAVE_FAILED) RetryNotice();
        }

        public void ApplyVerifiedSteps(VerifiedStepDelta delta)
        {
            if (!initialized || saveReloadRequired) return;
            HatchProgressResult result;
            try { result = progress.ApplyVerifiedSteps(snapshot, delta); }
            catch (HatchSaveOutcomeUnknownException) { ReloadAfterUnknownSave(); return; }
            if (result.Accepted) { snapshot = result.Snapshot; Publish(); }
            else if (result.Status == HatchProgressStatus.SAVE_FAILED) RetryNotice();
        }

        public void BeginHatch()
        {
            if (!initialized || InputLocked || snapshot.Phase != HatchHomePhase.HATCH_READY || (stage != null && stage.InputLocked)) return;
            var generation = presentationGeneration;
            SetLocked(true);
            if (generation != presentationGeneration) return;
            HatchSelectionResult result;
            try { result = selection.TryBegin(snapshot); }
            catch (HatchSaveOutcomeUnknownException) { ReloadAfterUnknownSave(); return; }
            catch (Exception) { SetLocked(false); RetryNotice(); return; }
            if (!result.Accepted) { SetLocked(false); RetryNotice(); return; }
            snapshot = result.Snapshot;
            Publish();
            if (generation != presentationGeneration) return;
            Recover();
        }

        public void RetryHatch()
        {
            if (!initialized) return;
            if (saveReloadRequired) { ReloadAfterUnknownSave(); return; }
            if (sequence.IsPlaying) return;
            NeedsRetry = false;
            if (snapshot.Phase == HatchHomePhase.HATCH_READY && !InputLocked) { BeginHatch(); return; }
            Recover();
        }

        public HomeSideActivityResult ApplySideActivity(HomeSideActivityAction action)
        {
            if (!initialized || InputLocked) return new(false, Snapshot, "locked");
            HomeSideActivityResult result;
            try { result = sideActivities.TryApply(snapshot, action, clock.UtcNow); }
            catch (HatchSaveOutcomeUnknownException)
            {
                ReloadAfterUnknownSave();
                return new(false, Snapshot, saveReloadRequired ? "reload_required" : "save_recovered");
            }
            if (result.Accepted) { snapshot = result.Snapshot.Clone(); Publish(); }
            else if (result.Error == "save_failed") NotifyNoticeSafely("저장하지 못했어요. 다시 시도해 주세요.");
            return result;
        }

        public void TapSubject()
        {
            if (!initialized || InputLocked) return;
            if (snapshot.ActiveSubject == HomeSubject.EGG) CareEgg();
            else stage?.PlayTapReaction();
        }

        private void PlayTapReaction(bool accepted) =>
            stage?.PlayReaction(accepted ? HomeReaction.AcceptedCare : HomeReaction.CooldownWobble);

        public void SetAvatarProfile(AvatarProfile profile) => avatar = profile ?? AvatarProfile.CreateDefault();

        public void SuspendPresentation()
        {
            if (!initialized || presentationSuspended) return;
            presentationSuspended = true;
            ++presentationGeneration;
            presentationTransaction = null;
            sequence.Interrupt();
            stage?.DestroyActiveSubject();
            SetLocked(snapshot.Phase is HatchHomePhase.HATCHING or HatchHomePhase.REVEAL);
            // Walking subscriptions remain alive on secondary screens and during AR handoff.
        }

        public void ResumePresentation()
        {
            if (!initialized) return;
            presentationSuspended = false;
            Recover();
        }

        private void Recover()
        {
            if (saveReloadRequired) { ReloadAfterUnknownSave(); return; }
            if (presentationSuspended) return;
            var generation = ++presentationGeneration;
            presentationTransaction = null;
            sequence.CoverForRecovery();
            SetLocked(true);
            if (generation != presentationGeneration) return;
            presentationTransaction = RecoverTransaction(generation);
            AdvancePresentationTransaction();
        }

        private IEnumerator RecoverTransaction(long generation)
        {
            var desired = snapshot.Phase == HatchHomePhase.HATCHING ? HomeSubject.EGG : snapshot.ActiveSubject;
            if (stage != null)
            {
                while (stage.InputLocked) { if (generation != presentationGeneration) yield break; yield return null; }
                if (generation != presentationGeneration) yield break;
                if (stage.SubjectInstance == null || stage.ActiveSubject != desired)
                {
                    if (!TryRequestSubject(desired)) { RetryNotice(); yield break; }
                    if (generation != presentationGeneration) yield break;
                    while (stage.InputLocked) { if (generation != presentationGeneration) yield break; yield return null; }
                }
                if (generation != presentationGeneration) yield break;
                if (stage.SubjectInstance == null || stage.ActiveSubject != desired) { RetryNotice(); yield break; }
            }
            if (generation != presentationGeneration) yield break;
            Publish();
            if (generation != presentationGeneration) yield break;
            switch (snapshot.Phase)
            {
                case HatchHomePhase.HATCHING: PlayDurableHatch(); break;
                case HatchHomePhase.REVEAL: OpenRevealed(); break;
                case HatchHomePhase.HOME:
                    sequence.OpenHome();
                    if (generation == presentationGeneration) SetLocked(false);
                    break;
                default:
                    sequence.OpenUnhatched();
                    if (generation == presentationGeneration) SetLocked(false);
                    break;
            }
        }

        private bool TryRequestSubject(HomeSubject subject)
        {
            try { requestSubject(subject); return true; }
            catch (Exception) { return false; }
        }

        private void AdvancePresentationTransaction()
        {
            var transaction = presentationTransaction;
            if (transaction == null) return;
            if (!transaction.MoveNext() && ReferenceEquals(presentationTransaction, transaction)) presentationTransaction = null;
        }
        private void Update() => AdvancePresentationTransaction();

        private void PlayDurableHatch()
        {
            var generation = presentationGeneration;
            SetLocked(true);
            if (generation != presentationGeneration) return;
            sequence.SelectedArtId = snapshot.selectedArtId;
            sequence.SoundEnabled = snapshot.soundEnabled;
            sequence.Play(snapshot.Checkpoint, PersistCheckpoint, PersistReveal, PersistHome);
        }

        private void OpenRevealed()
        {
            var generation = presentationGeneration;
            SetLocked(true);
            if (generation != presentationGeneration) return;
            sequence.SelectedArtId = snapshot.selectedArtId;
            sequence.RecoveredSubject = snapshot.ActiveSubject;
            sequence.Play(HatchCheckpoint.REVEALED, PersistCheckpoint, PersistReveal, PersistHome);
        }

        private bool PersistCheckpoint(HatchCheckpoint checkpoint)
        {
            if (snapshot.Phase != HatchHomePhase.HATCHING || checkpoint != HatchCheckpoint.SHELL_BURST) return false;
            if (snapshot.Checkpoint == HatchCheckpoint.SHELL_BURST) return true;
            var candidate = snapshot.Clone();
            candidate.hatchCheckpoint = nameof(HatchCheckpoint.SHELL_BURST);
            return Commit(candidate);
        }

        private bool PersistReveal()
        {
            if (snapshot.Phase == HatchHomePhase.REVEAL) return true;
            if (snapshot.Phase != HatchHomePhase.HATCHING || snapshot.Checkpoint != HatchCheckpoint.SHELL_BURST) return false;
            var candidate = snapshot.Clone();
            candidate.phase = nameof(HatchHomePhase.REVEAL);
            candidate.hatchCheckpoint = nameof(HatchCheckpoint.REVEALED);
            candidate.activeHomeView = nameof(HomeSubject.DINOSAUR);
            if (candidate.hatchedAtUtcTicks == 0) candidate.hatchedAtUtcTicks = clock.UtcNow.ToUniversalTime().Ticks;
            return Commit(candidate);
        }

        private bool PersistHome()
        {
            if (snapshot.Phase != HatchHomePhase.REVEAL) return false;
            var candidate = snapshot.Clone();
            candidate.phase = nameof(HatchHomePhase.HOME);
            return Commit(candidate);
        }

        private bool Commit(HatchHomeSnapshot candidate)
        {
            try { if (!store.TrySave(candidate)) return false; }
            catch (HatchSaveOutcomeUnknownException) { ReloadAfterUnknownSave(); return false; }
            catch (Exception) { return false; }
            snapshot = candidate;
            Publish();
            return true;
        }

        private void ReloadAfterUnknownSave()
        {
            saveReloadRequired = true;
            SetLocked(true);
            sequence.Interrupt();
            try { snapshot = store.Load().Snapshot.Clone(); }
            catch (Exception) { RetryNotice(); return; }
            saveReloadRequired = false;
            NeedsRetry = false;
            NotifyNoticeSafely("저장된 진행 상황을 다시 확인했어요.");
            if (presentationSuspended)
            {
                Publish();
                SetLocked(snapshot.Phase is HatchHomePhase.HATCHING or HatchHomePhase.REVEAL);
                return;
            }
            Recover();
        }

        public void ShowSubject(HomeSubject subject)
        {
            if (!initialized || InputLocked || (stage != null && stage.InputLocked)) return;
            var hatched = snapshot.Phase is HatchHomePhase.REVEAL or HatchHomePhase.HOME;
            if (subject != HomeSubject.AVATAR && subject != (hatched ? HomeSubject.DINOSAUR : HomeSubject.EGG)) return;
            if (stage != null)
            {
                var previous = snapshot.ActiveSubject;
                var generation = ++presentationGeneration;
                SetLocked(true);
                if (generation != presentationGeneration) return;
                if (!TryRequestSubject(subject)) { SetLocked(false); RetryNotice(); return; }
                if (generation != presentationGeneration) return;
                presentationTransaction = CompleteSubjectSwitch(subject, previous, generation);
                AdvancePresentationTransaction();
                return;
            }
            var candidate = snapshot.Clone();
            candidate.activeHomeView = subject.ToString();
            if (!Commit(candidate)) { RetryNotice(); return; }
        }

        private IEnumerator CompleteSubjectSwitch(HomeSubject subject, HomeSubject previous, long generation)
        {
            while (stage != null && stage.InputLocked) { if (generation != presentationGeneration) yield break; yield return null; }
            if (generation != presentationGeneration) yield break;
            if (stage == null || stage.ActiveSubject != subject) { SetLocked(false); RetryNotice(); yield break; }
            var candidate = snapshot.Clone();
            candidate.activeHomeView = subject.ToString();
            if (!Commit(candidate))
            {
                if (generation != presentationGeneration) yield break;
                RetryNotice();
                Recover();
                yield break;
            }
            if (generation == presentationGeneration) SetLocked(false);
        }

        private void RequestSubject(HomeSubject subject)
        {
            switch (subject)
            {
                case HomeSubject.EGG: stage?.ShowEgg(); break;
                case HomeSubject.DINOSAUR: stage?.ShowBaby(snapshot.selectedArtId); break;
                case HomeSubject.AVATAR: stage?.ShowAvatar(avatar); break;
            }
        }

        public void ResetFront() { if (initialized && !InputLocked) stage?.ResetFront(); }
        private void Publish() => NotifySnapshotSafely();
        private void NotifySnapshotSafely()
        {
            var notification = snapshot.Clone();
            if (SnapshotChanged == null) return;
            foreach (Action<HatchHomeSnapshot> subscriber in SnapshotChanged.GetInvocationList())
                try { subscriber(notification.Clone()); }
                catch (Exception) { NotifyNoticeSafely("화면 알림을 표시하지 못했어요. 저장된 진행은 안전해요."); }
        }
        private void NotifyNoticeSafely(string message)
        {
            if (NoticeRequested == null) return;
            foreach (Action<string> subscriber in NoticeRequested.GetInvocationList())
                try { subscriber(message); } catch (Exception) { /* Notice failure must never recursively notify. */ }
        }
        private void SetLocked(bool locked)
        {
            locked |= saveReloadRequired;
            if (!locked) NeedsRetry = false;
            InputLocked = locked;
            if (InputLockChanged == null) return;
            foreach (Action<bool> subscriber in InputLockChanged.GetInvocationList())
                try { subscriber(locked); }
                catch (Exception) { NotifyNoticeSafely("화면 알림을 표시하지 못했어요. 저장된 진행은 안전해요."); }
        }
        private void RetryNotice() { NeedsRetry = true; NotifyNoticeSafely("저장하지 못했어요. 다시 시도해 주세요."); }
        private void SequenceFailed() { SetLocked(true); RetryNotice(); }
        private void SequenceCompleted() { if (snapshot.Phase == HatchHomePhase.HOME) { NeedsRetry = false; SetLocked(false); } }
        private void Subscribe()
        {
            if (subscribed) return;
            if (provider != null) provider.VerifiedStepsReceived += ApplyVerifiedSteps;
            sequence.Failed += SequenceFailed;
            sequence.Completed += SequenceCompleted;
            subscribed = true;
        }
        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (provider != null) provider.VerifiedStepsReceived -= ApplyVerifiedSteps;
            sequence.Failed -= SequenceFailed;
            sequence.Completed -= SequenceCompleted;
            subscribed = false;
        }
        private void OnEnable() { if (initialized) { Subscribe(); Recover(); } }
        private void OnDisable()
        {
            ++presentationGeneration;
            presentationTransaction = null; // Never assumes the stage-owned coroutine has stopped.
            if (initialized) { sequence.Interrupt(); Unsubscribe(); }
        }
        private void OnDestroy() => Unsubscribe();
        private void OnApplicationPause(bool paused)
        {
            if (!initialized) return;
            if (paused) { ++presentationGeneration; presentationTransaction = null; sequence.Interrupt(); }
            else Recover();
        }
    }
}
