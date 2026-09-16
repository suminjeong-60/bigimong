using System;
using UnityEngine;

namespace Bigimong.AR
{
    /// <summary>Offline model boundary. Registered imported assets win; fallbacks are beta coverage, not accepted art.</summary>
    public sealed class HomeCharacterResolver
    {
        private readonly CharacterPrefabCatalog catalog;
        private readonly Func<string, GameObject> loadResource;
        public bool UsedFallback { get; private set; }

        public HomeCharacterResolver(CharacterPrefabCatalog catalog = null, Func<string, GameObject> loadResource = null)
        {
            this.catalog = catalog ?? Resources.Load<CharacterPrefabCatalog>("ArCharacterCatalog");
            this.loadResource = loadResource ?? (key => Resources.Load<GameObject>(key));
        }

        public GameObject CreateEgg() => InstantiateOrCreate(
            loadResource("GeneratedCharacters/Bigimong_Egg"), ProceduralEggFactory.Create);

        public GameObject CreateBaby(int artId)
        {
            GameObject prefab = null;
            if (catalog != null) catalog.TryResolve(artId, "BABY", out prefab);
            return InstantiateOrCreate(prefab, () => ProceduralDragonFactory.Create(artId, "BABY"));
        }

        public GameObject CreateAvatar(AvatarProfile profile)
        {
            profile = profile ?? AvatarProfile.CreateDefault();
            var key = profile.bodyType == "FEMININE"
                ? "GeneratedCharacters/Avatar_Feminine" : "GeneratedCharacters/Avatar_Masculine";
            return InstantiateOrCreate(loadResource(key), () => ProceduralAvatarFactory.Create(profile, false));
        }

        private GameObject InstantiateOrCreate(GameObject prefab, Func<GameObject> fallback)
        {
            if (prefab == null)
            {
                UsedFallback = true;
                if (Debug.isDebugBuild) Debug.LogWarning("Home uses a procedural 3D fallback; final imported art is not accepted.");
            }
            var instance = prefab != null ? UnityEngine.Object.Instantiate(prefab) : fallback();
            if (prefab == null) instance.AddComponent<HomeProceduralDetail>().Prepare();
            HomeBaseAppearance.Apply(instance);
            return instance;
        }
    }
}
