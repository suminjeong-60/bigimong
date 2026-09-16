using System;
using UnityEngine;
using UnityEngine.UI;

namespace Bigimong.AR
{
    [Serializable]
    public sealed class HatchPresentationSettings
    {
        public bool Audio = true;
        public bool Haptics;
        public bool CameraImpulse = true;
        public bool Particles = true;
    }

    public enum HatchSequenceSegment { Rock, Cracks, Burst, WarmLight, FocusTransition, Reveal, Home }

    /// <summary>Unscaled presentation timeline. Boolean durable callbacks gate every irreversible boundary.</summary>
    public sealed class HatchSequenceDirector : MonoBehaviour
    {
        private const float RockEnd = 0.7f;
        private const float BurstAt = 1.8f;
        private const float WarmAt = 2.4f;
        private const float FocusAt = 3.2f;
        private const float RevealAt = 4.2f;
        private const float FinishAt = 5.0f;
        private Func<HatchCheckpoint, bool> persistCheckpoint;
        private Func<bool> persistReveal, persistHome;
        private HomeFocusStage stage;
        private HomeStageOrbitInput orbit;
        private Image cover;
        private RawImage silhouette;
        private Action<int> requestBaby;
        private bool warmStarted;
        private Transform focusCamera;
        private Vector3 focusCameraPosition, focusCameraOffset;
        private EggCrackVfx effects;
        private int shellBudget = 24;
        private AudioSource audioSource;
        private AudioClip rockSound, crackSound, burstSound;
        private Transform rockPivot, cameraPivot;
        private Quaternion rockRotation;
        private Vector3 cameraPosition;
        private bool burstSaved, revealSaved, eggBound, orbitWasEnabled, orbitLockHeld;
        private float impulseAge = 1f;
        private bool recoveringHome;
        private long runGeneration;
        private static readonly float[] Boundaries = { RockEnd, 1.1f, 1.5f, BurstAt, WarmAt, FocusAt, RevealAt, FinishAt };
        public HatchPresentationSettings Settings { get; set; } = new();
        private bool soundEnabled = true;
        public bool SoundEnabled
        {
            get => soundEnabled;
            set { soundEnabled = value; if (!value && audioSource != null) audioSource.Stop(); }
        }
        public int SelectedArtId { get; set; }
        internal HomeSubject RecoveredSubject { get; set; } = HomeSubject.DINOSAUR;
        public float Elapsed { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsHatchPresentationLocked => IsPlaying || NeedsRetry || orbitLockHeld;
        public bool NeedsRetry { get; private set; }
        public bool RevealVisible { get; private set; }
        public float CoverAlpha { get; private set; }
        public int CrackStage { get; private set; }
        public HatchSequenceSegment Segment { get; private set; }
        public event Action Failed;
        public event Action Completed;
        public event Action PresentationChanged;

        // The UI owns this viewport-only overlay. It must sit above HomeFocusStage's switch cover;
        // navigation consumes the coordinator lock, while the retry button stays outside this overlay.
        public void Configure(HomeFocusStage focusStage, Image transitionCover, HomeStageOrbitInput stageOrbit, RawImage silhouetteView = null)
        {
            HideSilhouette();
            if (orbit != stageOrbit) UnlockOrbit();
            if (stage != null) stage.RetryNotice -= StageSwitchFailed;
            stage = focusStage;
            if (stage != null) stage.RetryNotice += StageSwitchFailed;
            if (stage != null) stage.GetComponent<HomeStageQualityController>()?.BindSequence(this);
            cover = transitionCover;
            orbit = stageOrbit;
            silhouette = silhouetteView;
            requestBaby = focusStage != null ? new Action<int>(focusStage.ShowBaby) : null;
            if (silhouette != null)
            {
                silhouette.raycastTarget = false;
                silhouette.enabled = false;
                if (cover != null && silhouette.transform.parent == cover.transform.parent)
                    silhouette.transform.SetSiblingIndex(cover.transform.GetSiblingIndex() + 1);
            }
            if (IsPlaying || NeedsRetry) LockOrbit();
            SetCover(CoverAlpha);
        }

        public bool HasPresentationBindings(HomeFocusStage focusStage) =>
            focusStage != null && stage == focusStage && cover != null && orbit != null && silhouette != null &&
            silhouette.transform.parent == cover.transform.parent;

        public void ConfigureAudio(AudioSource source, AudioClip rock, AudioClip crack, AudioClip burst)
        { audioSource = source; rockSound = rock; crackSound = crack; burstSound = burst; }

        public void SetQualityTier(HomeQualityTier tier)
        {
            shellBudget = tier == HomeQualityTier.HIGH ? 24 : 12;
            effects?.SetFragmentBudget(shellBudget);
        }

        public void ReleaseTransientEffects()
        {
            RestoreFeedback();
            RestoreFocusTransition();
            HideSilhouette();
            effects?.ReleaseTransientPool();
        }

        private void StageSwitchFailed(string notice)
        {
            // Existing failure path owns the hatch retry lock; quality never advances a phase.
            if (IsPlaying) Halt(runGeneration);
        }

        public static float ResumeTime(HatchCheckpoint checkpoint) => checkpoint switch
        {
            HatchCheckpoint.SHELL_BURST => 2.4f,
            HatchCheckpoint.REVEALED => 5.0f,
            _ => 0f,
        };

        public void Play(HatchCheckpoint checkpoint, Func<HatchCheckpoint, bool> persistCheckpoint,
            Func<bool> persistReveal, Func<bool> persistHome)
        {
            var run = ++runGeneration;
            RestoreFeedback();
            RestoreFocusTransition();
            HideSilhouette();
            effects?.Reset();
            this.persistCheckpoint = persistCheckpoint ?? throw new ArgumentNullException(nameof(persistCheckpoint));
            this.persistReveal = persistReveal ?? throw new ArgumentNullException(nameof(persistReveal));
            this.persistHome = persistHome ?? throw new ArgumentNullException(nameof(persistHome));
            Settings ??= new HatchPresentationSettings();
            Elapsed = ResumeTime(checkpoint);
            burstSaved = checkpoint is HatchCheckpoint.SHELL_BURST or HatchCheckpoint.REVEALED;
            revealSaved = checkpoint == HatchCheckpoint.REVEALED;
            recoveringHome = revealSaved;
            warmStarted = false;
            eggBound = false;
            CrackStage = 0;
            NeedsRetry = RevealVisible = false;
            IsPlaying = true;
            LockOrbit();
            SetCover(burstSaved ? 1f : 0f);
            if (stage != null && !HasPresentationBindings(stage)) { Halt(run); return; }
            if (revealSaved)
            {
                // Already durable: do not run either earlier callback or touch the completion timestamp.
                if (!IsCurrent(run)) return;
                CompleteHomeWhenReady(true, run);
                return;
            }
            if (burstSaved && !PrepareWarmPresentation(run)) return;
            if (!burstSaved) Feedback(rockSound, false);
            Present();
        }

        public void Advance(float deltaSeconds)
        {
            if (!IsPlaying || deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
            var run = runGeneration;
            if (recoveringHome) { CompleteHomeWhenReady(true, run); return; }
            // Loading an egg precedes the first visual frame; this is not timeline time.
            if (!burstSaved && !eggBound && stage != null)
            {
                if (stage.InputLocked) return;
                if (stage.SubjectInstance == null || stage.ActiveSubject != HomeSubject.EGG) { Halt(run); return; }
                BindEgg();
            }
            var target = Mathf.Min(FinishAt, Elapsed + deltaSeconds);
            foreach (var boundary in Boundaries)
                if (Mathf.Abs(target - boundary) < .000001f) target = boundary;
            if (!burstSaved && target >= BurstAt)
            {
                Elapsed = BurstAt;
                var checkpointCallback = persistCheckpoint;
                if (!CommitBoundary(() => checkpointCallback(HatchCheckpoint.SHELL_BURST), run)) return;
                burstSaved = true;
                RestoreRock();
                effects?.Burst(Settings.Particles);
                Feedback(burstSound, true);
            }
            if (!warmStarted && target >= WarmAt)
            {
                Elapsed = WarmAt;
                if (!PrepareWarmPresentation(run)) return;
            }
            if (!revealSaved && target >= RevealAt)
            {
                Elapsed = RevealAt;
                SetCover(1f);
                if (!CommitBoundary(persistReveal, run)) return;
                revealSaved = true;
                RestoreFeedback(); // Release camera offset before the baby gets its own framing.
                RestoreFocusTransition();
                HideSilhouette();
            }
            Elapsed = target;
            if (Elapsed >= FinishAt)
            {
                CompleteHomeWhenReady(false, run);
                return;
            }
            Present();
        }

        private void CompleteHomeWhenReady(bool waitForSwitch, long run)
        {
            if (!IsCurrent(run)) return;
            if (stage != null)
            {
                if (stage.InputLocked && waitForSwitch) return;
                var desired = recoveringHome ? RecoveredSubject : HomeSubject.DINOSAUR;
                if (stage.InputLocked || stage.SubjectInstance == null || stage.ActiveSubject != desired)
                { Halt(run); return; }
            }
            if (!CommitBoundary(persistHome, run)) return;
            Finish(run);
        }

        private bool PrepareWarmPresentation(long run)
        {
            SetCover(1f);
            RestoreFeedback();
            warmStarted = true;
            if (stage == null) return true;
            if (!HasPresentationBindings(stage) || stage.InputLocked) { Halt(run); return false; }
            if (stage.SubjectInstance == null || stage.ActiveSubject != HomeSubject.DINOSAUR)
            {
                try { requestBaby(SelectedArtId); }
                catch (Exception) { if (IsCurrent(run)) Halt(run); return false; }
                if (!IsCurrent(run)) return false;
            }
            return true;
        }

        private void BindEgg()
        {
            eggBound = true;
            rockPivot = stage.SubjectInstance.transform.parent;
            var motion = rockPivot != null ? rockPivot.GetComponent<HomeSubjectMotion>() : null;
            motion?.CancelAndRestore();
            if (rockPivot != null) rockRotation = rockPivot.localRotation;
            if (effects == null)
            {
                var effectObject = new GameObject("HatchEffects") { layer = stage.SubjectInstance.layer };
                effectObject.transform.SetParent(stage.StageRoot, false);
                effects = effectObject.AddComponent<EggCrackVfx>();
            }
            effects.Bind(stage.SubjectInstance);
            effects.SetFragmentBudget(shellBudget);
            var camera = stage.StageRoot.GetComponentInChildren<Camera>();
            cameraPivot = camera != null ? camera.transform : null;
            if (cameraPivot != null) cameraPosition = cameraPivot.localPosition;
        }

        private void Present()
        {
            Segment = Elapsed < RockEnd ? HatchSequenceSegment.Rock : Elapsed < BurstAt ? HatchSequenceSegment.Cracks :
                Elapsed < WarmAt ? HatchSequenceSegment.Burst : Elapsed < FocusAt ? HatchSequenceSegment.WarmLight :
                Elapsed < RevealAt ? HatchSequenceSegment.FocusTransition : HatchSequenceSegment.Reveal;
            var nextCrack = Elapsed < RockEnd || Elapsed >= BurstAt ? 0 : Elapsed < 1.1f ? 1 : Elapsed < 1.5f ? 2 : 3;
            if (nextCrack != CrackStage)
            {
                CrackStage = nextCrack;
                effects?.SetStage(CrackStage);
                if (CrackStage > 0) Feedback(crackSound, true);
            }
            if (rockPivot != null && Elapsed < BurstAt)
            {
                var envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(Elapsed / BurstAt));
                rockPivot.localRotation = rockRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(Elapsed * 28f) * 9f * envelope);
            }
            RevealVisible = revealSaved && (stage == null || (!stage.InputLocked && stage.ActiveSubject == HomeSubject.DINOSAUR));
            if (Elapsed < BurstAt) SetCover(0f);
            else if (Elapsed < WarmAt) SetCover(Mathf.SmoothStep(0f, 1f, (Elapsed - BurstAt) / (WarmAt - BurstAt)));
            else if (!RevealVisible) SetCover(1f);
            else SetCover(1f - Mathf.SmoothStep(0f, 1f, (Elapsed - RevealAt) / (FinishAt - RevealAt)));
            PresentSilhouette();
            PresentFocusTransition();
            PresentationChanged?.Invoke();
        }

        private void PresentSilhouette()
        {
            if (silhouette == null) return;
            var ready = IsPlaying && !revealSaved && Elapsed >= WarmAt && Elapsed < RevealAt &&
                stage != null && !stage.InputLocked && stage.ActiveSubject == HomeSubject.DINOSAUR && stage.SubjectInstance != null;
            silhouette.enabled = ready;
            if (!ready) return;
            silhouette.texture = stage.Texture;
            // UI tint multiplies the render texture to black; material assets and rig remain untouched.
            silhouette.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, (Elapsed - WarmAt) / .2f));
        }

        private void PresentFocusTransition()
        {
            if (Elapsed < FocusAt || Elapsed >= RevealAt || stage == null || stage.InputLocked || stage.ActiveSubject != HomeSubject.DINOSAUR) return;
            if (focusCamera == null)
            {
                var camera = stage.StageRoot != null ? stage.StageRoot.GetComponentInChildren<Camera>() : null;
                if (camera == null) return;
                focusCamera = camera.transform;
                focusCameraPosition = focusCamera.localPosition;
                focusCameraOffset = -(focusCamera.localRotation * Vector3.forward) * Mathf.Max(.1f, focusCameraPosition.magnitude * .12f);
            }
            var t = Mathf.SmoothStep(0f, 1f, (Elapsed - FocusAt) / (RevealAt - FocusAt));
            focusCamera.localPosition = focusCameraPosition + focusCameraOffset * (1f - t);
            if (cover != null) cover.color = Color.Lerp(new Color(1f, .88f, .59f, 1f), new Color(1f, .97f, .88f, 1f), t);
        }

        private void RestoreFocusTransition()
        {
            if (focusCamera != null) focusCamera.localPosition = focusCameraPosition;
            focusCamera = null;
        }

        private void HideSilhouette()
        {
            if (silhouette == null) return;
            silhouette.enabled = false;
            silhouette.texture = null;
        }

        private void Feedback(AudioClip clip, bool impact)
        {
            // Presentation failure never decides whether a durable boundary can advance.
            try
            {
                if (SoundEnabled && Settings.Audio && audioSource != null && clip != null) audioSource.PlayOneShot(clip);
                if (impact && Settings.Haptics && Application.isMobilePlatform) Handheld.Vibrate();
                if (impact && Settings.CameraImpulse) impulseAge = 0f;
            }
            catch (Exception) { /* Optional platform feedback is non-authoritative. */ }
        }

        private void TickFeedback(float seconds)
        {
            if (cameraPivot == null) return;
            impulseAge += seconds;
            var t = Mathf.Clamp01(impulseAge / .14f);
            var offset = Settings.CameraImpulse ? .008f * (1f - t) * (1f - t) * Mathf.Sin(t * Mathf.PI * 4f) : 0f;
            cameraPivot.localPosition = cameraPosition + Vector3.right * offset;
        }

        private bool IsCurrent(long run) => run == runGeneration && IsPlaying;
        private bool CommitBoundary(Func<bool> callback, long run)
        {
            bool accepted;
            try { accepted = callback(); } catch (Exception) { accepted = false; }
            // Callback observers may interrupt, disable, or replace this run synchronously.
            if (!IsCurrent(run)) return false;
            if (!accepted) { Halt(run); return false; }
            return true;
        }
        private void Halt(long run)
        {
            if (run != runGeneration) return;
            IsPlaying = false;
            NeedsRetry = true;
            RevealVisible = false;
            LockOrbit();
            SetCover(1f);
            RestoreFeedback();
            RestoreFocusTransition();
            HideSilhouette();
            effects?.Reset();
            Failed?.Invoke();
            if (run != runGeneration) return;
            PresentationChanged?.Invoke();
        }
        public void Interrupt()
        {
            var active = IsPlaying || NeedsRetry;
            var run = ++runGeneration;
            if (active) Halt(run);
        }
        internal void OpenHome() => OpenState(true);
        private void OpenState(bool hatched)
        {
            ++runGeneration;
            RestoreFeedback();
            RestoreFocusTransition();
            HideSilhouette();
            effects?.Reset();
            Elapsed = hatched ? FinishAt : 0f;
            IsPlaying = NeedsRetry = false;
            RevealVisible = hatched && (stage == null || stage.ActiveSubject == HomeSubject.DINOSAUR);
            Segment = HatchSequenceSegment.Home;
            SetCover(0f);
            UnlockOrbit();
            PresentationChanged?.Invoke();
        }
        internal void CoverForRecovery()
        {
            ++runGeneration;
            IsPlaying = NeedsRetry = RevealVisible = false;
            RestoreFeedback();
            RestoreFocusTransition();
            HideSilhouette();
            effects?.Reset();
            LockOrbit();
            SetCover(1f);
            if (cover != null) cover.raycastTarget = true;
        }
        internal void OpenUnhatched() => OpenState(false);
        private void Finish(long run)
        {
            if (!IsCurrent(run)) return;
            var completedRun = runGeneration + 1;
            OpenHome();
            if (runGeneration == completedRun) Completed?.Invoke();
        }
        private void SetCover(float alpha)
        {
            CoverAlpha = alpha;
            if (cover == null) return;
            cover.color = new Color(1f, .88f, .59f, alpha);
            cover.raycastTarget = IsPlaying || NeedsRetry;
        }
        private void LockOrbit()
        {
            if (orbit == null) return;
            if (!orbitLockHeld) { orbitWasEnabled = orbit.enabled; orbitLockHeld = true; }
            orbit.SetInputLocked(true);
            orbit.enabled = false; // The stage's asynchronous switch cannot undo this external lock.
        }
        private void UnlockOrbit()
        {
            if (!orbitLockHeld) return;
            if (orbit != null)
            {
                orbit.SetInputLocked(false);
                orbit.enabled = orbitWasEnabled;
            }
            orbitWasEnabled = false;
            orbitLockHeld = false;
        }
        private void RestoreRock() { if (rockPivot != null) rockPivot.localRotation = rockRotation; rockPivot = null; }
        private void RestoreFeedback()
        {
            RestoreRock();
            if (cameraPivot != null) cameraPivot.localPosition = cameraPosition;
            cameraPivot = null;
            impulseAge = 1f;
            if (audioSource != null) audioSource.Stop();
        }
        private void Update() { Advance(Time.unscaledDeltaTime); TickFeedback(Time.unscaledDeltaTime); }
        private void OnDisable() => Interrupt();
        private void OnDestroy() { if (stage != null) stage.RetryNotice -= StageSwitchFailed; RestoreFeedback(); RestoreFocusTransition(); HideSilhouette(); if (effects != null) Release(effects.gameObject); }
        private static void Release(UnityEngine.Object value)
        { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
