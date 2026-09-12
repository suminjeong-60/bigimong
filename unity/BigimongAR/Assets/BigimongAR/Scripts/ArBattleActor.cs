using System.Collections;
using UnityEngine;

namespace Bigimong.AR
{
    public sealed class ArBattleActor : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        public string Side { get; private set; }
        public int HitPoints { get; private set; }
        private int artId;
        private string stage;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Coroutine proceduralMotion;

        public void Initialize(string side, BattlePlayerMessage player)
        {
            Side = side;
            HitPoints = player.hp;
            artId = player.artId;
            stage = player.stage;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            SetPhysicalHeight(EvolutionScale.TargetHeightMeters(player.stage));
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
        }

        public void Face(Vector3 worldTarget)
        {
            var flatTarget = new Vector3(worldTarget.x, transform.position.y, worldTarget.z);
            transform.LookAt(flatTarget, Vector3.up);
            homeRotation = transform.localRotation;
        }

        public void PlayAttack(string direction)
        {
            if (!SetTrigger($"Attack_{NormalizeDirection(direction)}"))
                StartProcedural(AttackMotion(direction));
        }

        public void PlayDodge(string direction)
        {
            if (!SetTrigger($"Dodge_{NormalizeDirection(direction)}"))
                StartProcedural(DodgeMotion(direction));
        }

        public void PlayHit(int remainingHp)
        {
            HitPoints = Mathf.Max(0, remainingHp);
            var trigger = HitPoints == 0 ? "KO" : "Hit";
            if (!SetTrigger(trigger)) StartProcedural(HitPoints == 0 ? KoMotion() : HitMotion());
        }

        public void PlayVictory()
        {
            if (!SetTrigger("Victory")) StartProcedural(VictoryMotion());
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
            if (animator == null) return false;
            animator.SetTrigger(trigger);
            return true;
        }

        private void StartProcedural(IEnumerator routine)
        {
            if (proceduralMotion != null) StopCoroutine(proceduralMotion);
            transform.localPosition = homePosition;
            transform.localRotation = homeRotation;
            proceduralMotion = StartCoroutine(routine);
        }

        private IEnumerator AttackMotion(string direction)
        {
            var agePower = stage == "ADULT" ? 1.25f : stage == "YOUTH" ? 1f : 0.72f;
            var side = direction == "LEFT" ? -1f : direction == "RIGHT" ? 1f : 0f;
            var style = artId % 5;
            var distance = (0.28f + (artId % 7) * 0.018f) * agePower;
            var lift = style == 1 || style == 3 ? 0.16f * agePower : 0.04f;
            var twist = (style - 2) * 5f + side * 9f;
            yield return PulseMotion(new Vector3(side * 0.13f, lift, distance), Quaternion.Euler(0, twist, -side * 8f), 0.38f + (artId % 4) * 0.035f);
        }

        private IEnumerator DodgeMotion(string direction)
        {
            var side = direction == "LEFT" ? -1f : direction == "RIGHT" ? 1f : (artId % 2 == 0 ? -1f : 1f);
            var distance = 0.18f + (artId % 6) * 0.018f;
            yield return PulseMotion(new Vector3(side * distance, 0.04f, -0.08f), Quaternion.Euler(0, 0, -side * (10f + artId % 9)), 0.32f);
        }

        private IEnumerator HitMotion()
        {
            var duration = 0.36f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                var shake = Mathf.Sin(elapsed * (42f + artId)) * 0.035f;
                transform.localPosition = homePosition + new Vector3(shake, 0, -Mathf.Sin(elapsed / duration * Mathf.PI) * 0.12f);
                yield return null;
            }
            RestoreHome();
        }

        private IEnumerator KoMotion()
        {
            var startPosition = homePosition;
            var startRotation = homeRotation;
            var targetRotation = homeRotation * Quaternion.Euler(0, 0, Side == "A" ? 82f : -82f);
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.65f)
            {
                transform.localPosition = Vector3.Lerp(startPosition, homePosition + Vector3.down * 0.12f, t);
                transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }
            transform.localRotation = targetRotation;
        }

        private IEnumerator VictoryMotion()
        {
            for (var repeat = 0; repeat < 2; repeat++)
                yield return PulseMotion(new Vector3(0, 0.16f + (artId % 5) * 0.015f, 0), Quaternion.Euler(0, 18f * (repeat == 0 ? 1 : -1), 0), 0.42f);
        }

        private IEnumerator PulseMotion(Vector3 offset, Quaternion rotationOffset, float duration)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                var pulse = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                transform.localPosition = homePosition + offset * pulse;
                transform.localRotation = Quaternion.Slerp(homeRotation, homeRotation * rotationOffset, pulse);
                yield return null;
            }
            RestoreHome();
        }

        private void RestoreHome()
        {
            transform.localPosition = homePosition;
            transform.localRotation = homeRotation;
            proceduralMotion = null;
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
