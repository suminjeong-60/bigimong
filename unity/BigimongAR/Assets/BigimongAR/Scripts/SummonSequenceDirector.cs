using System;
using System.Collections;
using UnityEngine;

namespace Bigimong.AR
{
    public sealed class SummonSequenceDirector : MonoBehaviour
    {
        [SerializeField] private ArBattleHud hud;
        private Transform arenaRoot;
        private Coroutine activeSequence;
        private GameObject avatar;
        private ProceduralMagicCircle magicCircle;
        private GameObject lightColumn;
        private Coroutine lightPulse;
        private SummonerActor summoner;
        private ArBattleActor activePet;
        private GameObject throwTarget;
        private int activeGeneration;

        public event Action SummoningStarted;
        public event Action SummoningCompleted;
        public int ActiveGeneration => activeGeneration;
        public bool IsPlaying => activeSequence != null;

        public void Configure(Transform value, ArBattleHud battleHud)
        {
            arenaRoot = value;
            hud = battleHud;
        }

        public void Begin(AvatarProfile profile, ArBattleActor pet)
        {
            CancelAndReset();
            if (arenaRoot == null || pet == null) return;
            activeSequence = StartCoroutine(Play(profile, pet));
        }

        public IEnumerator Play(AvatarProfile profile, ArBattleActor pet)
        {
            var generation = ++activeGeneration;
            hud?.SetInputLocked(true);
            SummoningStarted?.Invoke();
            activePet = pet;
            var petHomePosition = pet.transform.localPosition;
            var petHomeRotation = pet.transform.localRotation;
            pet.PrepareSummonRise();
            var avatarAssetName = profile != null && profile.bodyType == "FEMININE" ? "Feminine" : "Masculine";
            var avatarPrefab = Resources.Load<GameObject>("GeneratedCharacters/Avatar_" + avatarAssetName);
            avatar = avatarPrefab != null ? Instantiate(avatarPrefab) : ProceduralAvatarFactory.Create(profile);
            avatar.transform.SetParent(arenaRoot, false);
            avatar.transform.localPosition = petHomePosition + new Vector3(0, 0, -0.72f);
            avatar.transform.localRotation = petHomeRotation;
            summoner = avatar.AddComponent<SummonerActor>();
            summoner.Initialize(arenaRoot);

            magicCircle = ProceduralMagicCircle.Create(arenaRoot);
            magicCircle.transform.localPosition = new Vector3(petHomePosition.x, 0.01f, petHomePosition.z);
            magicCircle.gameObject.SetActive(false);
            throwTarget = new GameObject("Medallion Throw Target");
            throwTarget.transform.SetParent(arenaRoot, false);
            throwTarget.transform.localPosition = magicCircle.transform.localPosition;

            yield return summoner.ThrowMedallion(throwTarget.transform);
            if (generation != activeGeneration) yield break;
            Destroy(throwTarget);
            throwTarget = null;
            magicCircle.gameObject.SetActive(true);
            hud?.PlayImpactPulse(.28f);
            yield return magicCircle.ImpactBurst(.34f);
            if (generation != activeGeneration) yield break;
            yield return magicCircle.Expand(0.7f);
            if (generation != activeGeneration) yield break;

            lightColumn = CreateLightColumn(pet, petHomePosition);
            lightPulse = StartCoroutine(PulseLightColumn(1f));
            yield return new WaitForSecondsRealtime(0.3f);
            if (generation != activeGeneration) yield break;
            yield return pet.PlaySummonRise(1f);
            if (generation != activeGeneration) yield break;
            yield return summoner.StepBack(0.5f);
            if (generation != activeGeneration) yield break;

            if (lightColumn != null) Destroy(lightColumn);
            lightColumn = null;
            lightPulse = null;
            activeSequence = null;
            activePet = null;
            hud?.SetInputLocked(false);
            SummoningCompleted?.Invoke();
        }

        public void CancelAndReset()
        {
            activeGeneration++;
            if (activeSequence != null) StopCoroutine(activeSequence);
            if (lightPulse != null) StopCoroutine(lightPulse);
            activeSequence = null;
            activePet?.ResetSummonPose();
            summoner?.ResetPose();
            if (avatar != null) Destroy(avatar);
            if (magicCircle != null) Destroy(magicCircle.gameObject);
            if (lightColumn != null) Destroy(lightColumn);
            if (throwTarget != null) Destroy(throwTarget);
            avatar = null;
            magicCircle = null;
            lightColumn = null;
            lightPulse = null;
            summoner = null;
            activePet = null;
            throwTarget = null;
            hud?.SetInputLocked(true);
        }

        private GameObject CreateLightColumn(ArBattleActor pet, Vector3 petHomePosition)
        {
            var column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "Element Summon Light Column";
            column.transform.SetParent(arenaRoot, false);
            column.transform.localPosition = petHomePosition + Vector3.up * 0.75f;
            column.transform.localScale = new Vector3(0.34f, 0.75f, 0.34f);
            var profile = ElementSkillCatalog.Resolve(pet.ArtId);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            column.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = profile.primaryColor };
            var collider = column.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return column;
        }

        private IEnumerator PulseLightColumn(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                var pulse = 0.65f + Mathf.Sin(elapsed / seconds * Mathf.PI) * 0.35f;
                if (lightColumn != null) lightColumn.transform.localScale = new Vector3(0.34f * pulse, 0.75f, 0.34f * pulse);
                yield return null;
            }
        }
    }
}
