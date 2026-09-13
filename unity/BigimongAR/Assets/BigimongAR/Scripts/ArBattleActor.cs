using System.Collections;
using UnityEngine;

namespace Bigimong.AR
{
    public sealed class ArBattleActor : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        public string Side { get; private set; }
        public int HitPoints { get; private set; }
        public int ArtId => artId;
        private int artId;
        private string stage;
        private ElementSkillProfile profile;
        private Material[] ownedMaterials;
        public void OwnProceduralMaterials(params Material[] materials) => ownedMaterials = materials;
        private Vector3 homeScale;
        private bool idleEnabled = true, terminal, busy;
        private Transform[] parts;
        private Quaternion[] partRotations;
        private Vector3[] partScales;
        private Renderer[] bodyRenderers;
        private MaterialPropertyBlock flash;
        public ElementSkillProfile SkillProfile => profile;
        public bool UsesAnimator => animator != null && animator.enabled && animator.runtimeAnimatorController != null;
        public event System.Action<ElementSkillProfile> AttackStarted;
        public event System.Action<int> ImpactReceived;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Coroutine proceduralMotion;
        private Vector3 summonHomeScale;
        private Renderer[] summonRenderers;
        private bool[] summonRendererStates;
        private bool summonPrepared;

        public void Initialize(string side, BattlePlayerMessage player)
        {
            Side = side;
            HitPoints = player.hp;
            artId = player.artId;
            stage = player.stage;
            profile = ElementSkillCatalog.Resolve(artId);
            if (animator == null) animator = GetComponentInChildren<Animator>();
            SetPhysicalHeight(EvolutionScale.TargetHeightMeters(player.stage));
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
            homeScale = transform.localScale;
            parts = GetComponentsInChildren<Transform>();
            partRotations = new Quaternion[parts.Length];
            partScales = new Vector3[parts.Length];
            for (var i = 0; i < parts.Length; i++) { partRotations[i] = parts[i].localRotation; partScales[i] = parts[i].localScale; }
            bodyRenderers = GetComponentsInChildren<Renderer>();
            flash = new MaterialPropertyBlock();
        }

        public void Face(Vector3 worldTarget)
        {
            var flatTarget = new Vector3(worldTarget.x, transform.position.y, worldTarget.z);
            transform.LookAt(flatTarget, Vector3.up);
            homeRotation = transform.localRotation;
        }

        public void PlayAttack(string direction)
        {
            if (terminal) return;
            AttackStarted?.Invoke(profile);
            if (!SetTrigger($"Attack_{NormalizeDirection(direction)}"))
                StartProcedural(AttackMotion(direction, profile.attackProfile));
        }

        public void PlayDodge(string direction)
        {
            if (terminal) return;
            if (!SetTrigger($"Dodge_{NormalizeDirection(direction)}"))
                StartProcedural(DodgeMotion(direction, profile.dodgeProfile));
        }

        public void PlayHit(int remainingHp, Vector3? worldDirection = null)
        {
            HitPoints = Mathf.Max(0, remainingHp);
            ImpactReceived?.Invoke(HitPoints);
            terminal = HitPoints == 0;
            var trigger = HitPoints == 0 ? "KO" : "Hit";
            if (!SetTrigger(trigger)) StartProcedural(HitReaction(worldDirection ?? -transform.forward), true);
        }

        public void PlayVictory()
        {
            terminal = true;
            if (!SetTrigger("Victory")) StartProcedural(VictoryMotion());
        }

        public IEnumerator PlaySummonRise(float seconds)
        {
            if (!summonPrepared) PrepareSummonRise();
            var duration = Mathf.Max(0.01f, seconds);
            var targetPosition = homePosition;
            SetSummonRenderersVisible(true);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.localPosition = Vector3.Lerp(targetPosition + Vector3.down * 0.45f, targetPosition, progress);
                transform.localScale = Vector3.Lerp(summonHomeScale * 0.05f, summonHomeScale, progress);
                yield return null;
            }
            transform.localPosition = targetPosition;
            transform.localScale = summonHomeScale;
            RestoreSummonRendererStates();
            summonPrepared = false;
            SetIdleEnabled(true);
        }

        public void PrepareSummonRise()
        {
            if (summonPrepared) ResetSummonPose();
            if (proceduralMotion != null) StopCoroutine(proceduralMotion);
            proceduralMotion = null;
            busy = false;
            SetIdleEnabled(false);
            RestoreHome();
            summonHomeScale = homeScale;
            summonRenderers = GetComponentsInChildren<Renderer>(true);
            summonRendererStates = new bool[summonRenderers.Length];
            for (var index = 0; index < summonRenderers.Length; index++)
            {
                summonRendererStates[index] = summonRenderers[index].enabled;
                summonRenderers[index].enabled = false;
            }
            transform.localPosition = homePosition + Vector3.down * 0.45f;
            transform.localRotation = homeRotation;
            transform.localScale = summonHomeScale * 0.05f;
            summonPrepared = true;
        }

        public void ResetSummonPose()
        {
            if (!summonPrepared) return;
            transform.localPosition = homePosition;
            transform.localRotation = homeRotation;
            transform.localScale = summonHomeScale;
            RestoreSummonRendererStates();
            summonPrepared = false;
            SetIdleEnabled(true);
        }

        public void ApplySnapshotHitPoints(int hitPoints)
        {
            HitPoints = Mathf.Max(0, hitPoints);
        }

        private void SetPhysicalHeight(float targetMeters)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            if (bounds.size.y > 0.001f) transform.localScale *= targetMeters / bounds.size.y;
        }

        private bool SetTrigger(string trigger)
        {
            if (!UsesAnimator) return false;
            if (proceduralMotion != null) StopCoroutine(proceduralMotion);
            RestoreHome();
            proceduralMotion = null;
            busy = false;
            animator.SetTrigger(trigger);
            return true;
        }

        private void StartProcedural(IEnumerator routine, bool preservePose = false)
        {
            if (proceduralMotion != null) StopCoroutine(proceduralMotion);
            if (!preservePose) RestoreHome();
            busy = true;
            proceduralMotion = StartCoroutine(FinishMotion(routine));
        }

        private IEnumerator FinishMotion(IEnumerator routine)
        {
            yield return routine;
            busy = false;
            proceduralMotion = null;
        }

        // Stable profile-derived parameters avoid runtime string hash randomization.
        public static float ProfilePhase(string id)
        {
            var hash = 0;
            foreach (var c in id) hash = (hash * 31 + c) % 997;
            return hash / 997f;
        }

        private IEnumerator AttackMotion(string direction, string attackProfile)
        {
            var phase = ProfilePhase(attackProfile);
            var side = direction == "LEFT" ? -1f : direction == "RIGHT" ? 1f : 0f;
            var aerial = attackProfile.Contains("dive") || attackProfile.Contains("descent") || attackProfile.Contains("pounce");
            var swing = attackProfile.Contains("tail") || attackProfile.Contains("hammer") || attackProfile.Contains("slash");
            var stomp = attackProfile.Contains("stomp") || attackProfile.Contains("step") || attackProfile.Contains("tremor");
            yield return PulseMotion(new Vector3(side * .14f, aerial ? .3f : stomp ? .12f : .03f, .18f + phase * .2f),
                Quaternion.Euler(stomp ? 16 : -8, swing ? 65 : side * 15, side * -10), .38f + phase * .16f + artId * .001f);
        }

        private IEnumerator DodgeMotion(string direction, string dodgeProfile)
        {
            var side = direction == "LEFT" ? -1f : direction == "RIGHT" ? 1f : (artId % 2 == 0 ? -1f : 1f);
            var phase = ProfilePhase(dodgeProfile);
            var ascent = dodgeProfile.Contains("ascent") || dodgeProfile.Contains("vault") || dodgeProfile.Contains("leap");
            var roll = dodgeProfile.Contains("roll") || dodgeProfile.Contains("somersault");
            var low = dodgeProfile.Contains("slide") || dodgeProfile.Contains("burrow") || dodgeProfile.Contains("hide") || dodgeProfile.Contains("curl");
            var pivot = dodgeProfile.Contains("pivot") || dodgeProfile.Contains("turn") || dodgeProfile.Contains("guard");
            var duration = .32f + phase * .15f + artId * .001f;
            for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
            {
                var pulse = Mathf.Sin(t * Mathf.PI);
                var zigzag = dodgeProfile.Contains("zigzag") ? Mathf.Sin(t * Mathf.PI * 3) : pulse;
                transform.localPosition = homePosition + homeRotation * new Vector3(side * (.22f + phase * .15f) * zigzag, (ascent ? .38f : low ? -.1f : .06f) * pulse, -.12f * pulse);
                transform.localRotation = homeRotation * Quaternion.Euler(low ? 24 * pulse : 0, pivot ? side * 85 * pulse : 0, roll ? side * 360 * t : -side * 18 * pulse);
                yield return null;
            }
            RestoreHome();
        }

        private IEnumerator HitReaction(Vector3 worldDirection)
        {
            yield return HitMotion(worldDirection);
            if (terminal) yield return KoMotion();
        }

        private IEnumerator HitMotion(Vector3 worldDirection)
        {
            // Only this actor's procedural routine pauses; tracking and VFX keep running.
            yield return new WaitForSecondsRealtime(0.06f);
            var localDirection = transform.parent != null ? transform.parent.InverseTransformDirection(worldDirection) : worldDirection;
            SetFlash(true);
            yield return PulseMotion(Quaternion.Inverse(homeRotation) * localDirection.normalized * .14f, Quaternion.Euler(-12, 0, 0), .36f);
            SetFlash(false);
        }

        private void SetFlash(bool value)
        {
            if (bodyRenderers == null) return;
            foreach (var renderer in bodyRenderers)
            {
                if (renderer == null) continue;
                if (!value) { renderer.SetPropertyBlock(null); continue; }
                flash.Clear();
                flash.SetColor("_BaseColor", Color.white);
                flash.SetColor("_Color", Color.white);
                renderer.SetPropertyBlock(flash);
            }
        }

        public void SetIdleEnabled(bool enabled)
        {
            idleEnabled = enabled;
        }

        private void LateUpdate()
        {
            if (!idleEnabled || terminal || busy || summonPrepared || UsesAnimator || profile == null) return;
            var t = Time.unscaledTime * (1.5f + ProfilePhase(profile.idleProfile)) + artId * .173f;
            GroundedIdleMotion(t);
            WingedIdleMotion(t);
        }

        private void GroundedIdleMotion(float t)
        {
            transform.localScale = Vector3.Scale(homeScale, new Vector3(1, 1 + Mathf.Sin(t) * .018f, 1));
            transform.localPosition = homePosition + homeRotation * new Vector3(Mathf.Sin(t * .37f) * .012f, 0, 0);
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i] == transform) continue;
                if (parts[i].name.Contains("Eye")) parts[i].localScale = Vector3.Scale(partScales[i], new Vector3(1, Mathf.Repeat(t, 8) < .12f ? .12f : 1, 1));
                if (parts[i].name.Contains("Head")) parts[i].localRotation = partRotations[i] * Quaternion.Euler(0, Mathf.Sin(t * .7f) * 6, 0);
                if (parts[i].name.Contains("Tail")) parts[i].localRotation = partRotations[i] * Quaternion.Euler(0, Mathf.Sin(t + .8f) * 9, 0);
            }
        }

        private void WingedIdleMotion(float t)
        {
            var winged = false;
            for (var i = 0; i < parts.Length; i++)
                if (parts[i].name.Contains("Wing"))
                {
                    winged = true;
                    var side = parts[i].localPosition.x < 0 ? -1 : 1;
                    parts[i].localRotation = partRotations[i] * Quaternion.Euler(0, 0, side * Mathf.Sin(t * 2) * 16);
                }
            if (winged) transform.localPosition += Vector3.up * (.025f + Mathf.Sin(t) * .02f + Mathf.Sin(t * 2) * .008f);
        }

        private void OnDestroy()
        {
            if (ownedMaterials == null) return;
            foreach (var material in ownedMaterials)
                if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            ResetSummonPose();
            RestoreHome();
            busy = false;
        }

        private IEnumerator KoMotion()
        {
            var startPosition = homePosition;
            var startRotation = homeRotation;
            var targetRotation = homeRotation * Quaternion.Euler(0, 0, Side == "A" ? 82f : -82f);
            for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.65f)
            {
                transform.localPosition = Vector3.Lerp(startPosition, homePosition + Vector3.down * 0.12f, t);
                transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }
            transform.localRotation = targetRotation;
            transform.localPosition = homePosition + Vector3.down * .12f;
        }

        private IEnumerator VictoryMotion()
        {
            for (var repeat = 0; repeat < 2; repeat++)
                yield return PulseMotion(new Vector3(0, 0.16f + (artId % 5) * 0.015f, 0), Quaternion.Euler(0, 18f * (repeat == 0 ? 1 : -1), 0), 0.42f);
            transform.localRotation = homeRotation * Quaternion.Euler(-8, 0, 0);
        }

        private IEnumerator PulseMotion(Vector3 offset, Quaternion rotationOffset, float duration)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var pulse = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                transform.localPosition = homePosition + homeRotation * offset * pulse;
                transform.localRotation = Quaternion.Slerp(homeRotation, homeRotation * rotationOffset, pulse);
                yield return null;
            }
            RestoreHome();
        }

        private void RestoreHome()
        {
            transform.localPosition = homePosition;
            transform.localRotation = homeRotation;
            transform.localScale = homeScale;
            if (parts != null) for (var i = 0; i < parts.Length; i++)
                if (parts[i] != null && parts[i] != transform) { parts[i].localRotation = partRotations[i]; parts[i].localScale = partScales[i]; }
            SetFlash(false);
        }

        private void SetSummonRenderersVisible(bool visible)
        {
            if (summonRenderers == null) return;
            for (var index = 0; index < summonRenderers.Length; index++)
                if (summonRenderers[index] != null) summonRenderers[index].enabled = visible && summonRendererStates[index];
        }

        private void RestoreSummonRendererStates()
        {
            if (summonRenderers == null) return;
            for (var index = 0; index < summonRenderers.Length; index++)
                if (summonRenderers[index] != null) summonRenderers[index].enabled = summonRendererStates[index];
            summonRenderers = null;
            summonRendererStates = null;
        }

        private static string NormalizeDirection(string direction)
        {
            return direction switch
            {
                "LEFT" => "Left",
                "RIGHT" => "Right",
                _ => "Center",
            };
        }
    }
}
