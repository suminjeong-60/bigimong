#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Bigimong.AR.EditorChecks
{
    public static class HomeCharacterResolverEditorChecks
    {
        public static void RunBehaviorChecks()
        {
            ImportedPrecedenceAndSourceIsolation();
            NestedProtectedRootsRemainActive();
            AllBabyIdentities();
            EggGeometryAndReuse();
            AvatarBaseAndArDefaults();
        }

        private static void ImportedPrecedenceAndSourceIsolation()
        {
            var source = VarcoAssetImportEditorChecks.CreateQualityFixture(100, 55);
            var catalog = ScriptableObject.CreateInstance<CharacterPrefabCatalog>();
            var fallback = new GameObject("WrongGenericFallback");
            try
            {
                foreach (var name in new[] { "CosmeticRoot", "AccessoryRoot", "Wearables", "SummoningMedallion", "Medallion", "Hat", "Glasses", "Sunglasses", "Backpack", "ExplorerPack", "BaseUnderlayer", "Body", "Face", "HairRoot", "Rig", "Anatomy" })
                    new GameObject(name).transform.SetParent(source.transform, false);
                var serialized = new SerializedObject(catalog);
                serialized.FindProperty("fallbackPrefab").objectReferenceValue = fallback;
                var entries = serialized.FindProperty("characters");
                entries.arraySize = 1;
                entries.GetArrayElementAtIndex(0).FindPropertyRelative("artId").intValue = 7;
                entries.GetArrayElementAtIndex(0).FindPropertyRelative("baby").objectReferenceValue = source;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Require(catalog.Resolve(8, "BABY") == fallback, "AR generic fallback must remain compatible");
                Require(!catalog.TryResolve(8, "BABY", out var absent) && absent == null, "home must not confuse generic AR fallback with identity");
                var resolver = new HomeCharacterResolver(catalog, key =>
                {
                    Require(key == "GeneratedCharacters/Bigimong_Egg" || key == "GeneratedCharacters/Avatar_Masculine" || key == "GeneratedCharacters/Avatar_Feminine", "stable resource key");
                    return source;
                });
                CheckClone(resolver.CreateBaby(7), source);
                CheckClone(resolver.CreateEgg(), source);
                var male = AvatarProfile.CreateDefault();
                male.bodyType = "MASCULINE";
                CheckClone(new HomeCharacterResolver(catalog, key => key == "GeneratedCharacters/Avatar_Masculine" ? source : null).CreateAvatar(male), source);
                var female = AvatarProfile.CreateDefault();
                female.bodyType = "FEMININE";
                CheckClone(new HomeCharacterResolver(catalog, key => key == "GeneratedCharacters/Avatar_Feminine" ? source : null).CreateAvatar(female), source);
                Require(!resolver.UsedFallback, "imported resolution must not claim fallback coverage");
                foreach (Transform child in source.transform) Require(child.gameObject.activeSelf, "source remains untouched after instance destruction");
            }
            finally
            {
                VarcoAssetImportEditorChecks.DestroyQualityFixture(source);
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(fallback);
            }
        }

        private static void CheckClone(GameObject instance, GameObject source)
        {
            try
            {
                Require(instance != source && instance.name == source.name + "(Clone)", "imported prefab wins and is instantiated");
                foreach (var name in new[] { "CosmeticRoot", "AccessoryRoot", "Wearables", "SummoningMedallion", "Medallion", "Hat", "Glasses", "Sunglasses", "Backpack", "ExplorerPack" })
                    Require(!instance.transform.Find(name).gameObject.activeSelf, name + " must start unequipped");
                foreach (var name in new[] { "BaseUnderlayer", "Body", "Face", "HairRoot", "Rig", "Anatomy" })
                    Require(instance.transform.Find(name).gameObject.activeInHierarchy, name + " must remain visible/available");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static void NestedProtectedRootsRemainActive()
        {
            var source = new GameObject("NestedProtectedFixture");
            GameObject instance = null;
            try
            {
                // All fixtures begin active with enabled renderers, including optional containers.
                foreach (var path in new[]
                {
                    "Wearables", "Wearables/BaseUnderlayer", "Wearables/CoatMesh", "Wearables/Hat",
                    "AccessoryRoot", "AccessoryRoot/Rig", "AccessoryRoot/Rig/Body", "AccessoryRoot/Rig/Body/Anatomy",
                    "AccessoryRoot/Rig/Body/Face", "AccessoryRoot/Rig/Body/HairRoot", "AccessoryRoot/Rig/Body/Hat",
                    "AccessoryRoot/Backpack", "AccessoryRoot/Trim", "CosmeticRoot", "CosmeticRoot/Glasses",
                })
                {
                    var slash = path.LastIndexOf('/');
                    var parent = slash < 0 ? source.transform : source.transform.Find(path.Substring(0, slash));
                    var name = slash < 0 ? path : path.Substring(slash + 1);
                    new GameObject(name, typeof(MeshRenderer)).transform.SetParent(parent, false);
                }
                instance = new HomeCharacterResolver(null, _ => source).CreateEgg();
                HomeBaseAppearance.Apply(instance); // Repeated application must remain safe.
                foreach (var path in new[] { "Wearables", "Wearables/BaseUnderlayer", "AccessoryRoot", "AccessoryRoot/Rig", "AccessoryRoot/Rig/Body", "AccessoryRoot/Rig/Body/Anatomy", "AccessoryRoot/Rig/Body/Face", "AccessoryRoot/Rig/Body/HairRoot" })
                    Require(instance.transform.Find(path).gameObject.activeInHierarchy, path + " and its ancestors must remain active");
                foreach (var path in new[] { "Wearables/BaseUnderlayer", "AccessoryRoot/Rig", "AccessoryRoot/Rig/Body", "AccessoryRoot/Rig/Body/Anatomy", "AccessoryRoot/Rig/Body/Face", "AccessoryRoot/Rig/Body/HairRoot" })
                    Require(instance.transform.Find(path).GetComponent<Renderer>().enabled, path + " base renderer must remain enabled");
                foreach (var path in new[] { "Wearables", "AccessoryRoot" })
                    Require(!instance.transform.Find(path).GetComponent<Renderer>().enabled, "optional container's own renderer must be hidden");
                foreach (var path in new[] { "Wearables/CoatMesh", "Wearables/Hat", "AccessoryRoot/Backpack", "AccessoryRoot/Trim", "AccessoryRoot/Rig/Body/Hat", "CosmeticRoot", "CosmeticRoot/Glasses" })
                    Require(!instance.transform.Find(path).gameObject.activeInHierarchy, path + " ordinary optional equipment must remain hidden");
                UnityEngine.Object.DestroyImmediate(instance);
                instance = null;
                foreach (var node in source.GetComponentsInChildren<Transform>(true))
                    Require(node.gameObject.activeSelf, "nested imported source activation must remain untouched after clone destruction");
                foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
                    Require(renderer.enabled, "nested imported source renderer state must remain untouched");
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static void AllBabyIdentities()
        {
            // Literal expectations independent of the production appearance resolver.
            var colors = new[] { 0xE94F45, 0x4DA9E8, 0x8D62C7, 0xF18A3D, 0x65B95B, 0x388DD0, 0x9B7048, 0x3C9B83, 0x65B96D, 0xD94A3B, 0x3B91D5, 0x3E8CBF, 0x4D8EC8, 0x7D67B4, 0x8B6C4D, 0x56A95B, 0x4D9AB5, 0xB9976A, 0xD78B3E, 0x7353A6, 0xA93432, 0x59B76D, 0x77C99B, 0xB87445, 0x806DB5, 0x3F7F83, 0x50AC72, 0x438DC2, 0x67B65B, 0xD85454 };
            var plans = new[] { "Predator", "Ceratopsian", "Pterosaur", "Plated", "LongNeck", "Spinosaur", "Armored", "Runner", "Hadrosaur", "Predator", "Aquatic", "Predator", "Pterosaur", "Feathered", "Armored", "Predator", "Feathered", "Ceratopsian", "Feathered", "Predator", "Predator", "Hadrosaur", "Hadrosaur", "Plated", "Feathered", "Runner", "Hadrosaur", "Pterosaur", "LongNeck", "LongNeck" };
            var anatomy = new[] { "LegL", "Frill", "WingL", "BackPlate", "LongNeck", "Sail", "ArmorShell", "LegL", "HeadCrest", "HornL", "TailFin", "LegL", "WingL", "FeatherCrest", "TailClub", "LegL", "Beak", "Frill", "Beak", "HornL", "LegL", "HeadCrest", "HeadCrest", "BackPlate", "ClawL", "LegL", "HeadCrest", "WingL", "LongNeck", "LongNeck" };
            var empty = ScriptableObject.CreateInstance<CharacterPrefabCatalog>();
            try
            {
                var resolver = new HomeCharacterResolver(empty, _ => null);
                for (var id = 1; id <= 30; id++)
                {
                    var baby = resolver.CreateBaby(id);
                    try
                    {
                        Require(baby.name == $"Procedural_Bigimong_{id:00}_BABY", "baby art ID and stage must survive fallback");
                        var body = baby.transform.Find("Body");
                        var color = (Color32)body.GetComponent<Renderer>().sharedMaterial.color;
                        Require((color.r << 16 | color.g << 8 | color.b) == colors[id - 1], "literal species palette");
                        Require(BigimongAppearanceCatalog.Resolve(id).bodyPlan.ToString() == plans[id - 1], "literal species body plan");
                        var size = body.localScale;
                        var expected = id == 11 ? new Vector3(.78f, .44f, 1.22f) : id == 5 || id == 29 || id == 30 ? new Vector3(.68f, .58f, .94f) : new Vector3(.74f, .62f, .96f);
                        Require(size == expected, "body geometry retains identity");
                        Require(baby.transform.Find(anatomy[id - 1]) != null, "literal distinguishing anatomy survives base appearance");
                        Require(Mathf.Abs(baby.transform.Find("Tail").localScale.y - (id == 11 ? .88f : .757f)) < .00001f, "BABY maturity is .35, not adult geometry with a baby label");
                        Require(!baby.transform.Find("CosmeticRoot").gameObject.activeSelf, "baby equipment hidden");
                        Require(baby.GetComponentsInChildren<MeshFilter>().Length > 10, "fallback must be real 3D geometry");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(baby); }
                }
                Require(resolver.UsedFallback, "missing assets must set development fallback flag");
            }
            finally { UnityEngine.Object.DestroyImmediate(empty); }
        }

        private static void EggGeometryAndReuse()
        {
            var resolver = new HomeCharacterResolver(null, _ => null);
            var first = resolver.CreateEgg();
            var second = resolver.CreateEgg();
            try
            {
                Require(first.name == "Procedural_Bigimong_Egg", "egg fallback name");
                Require(first.transform.childCount == 15, "shell, twelve patches, nest, shadow");
                Require(first.transform.Find("EggShell").localScale == new Vector3(.34f, .50f, .34f), "egg has a vertically stretched shell");
                Require(first.GetComponentsInChildren<Collider>(true).Length == 0, "egg removes every primitive collider in edit mode");
                var materials = new HashSet<Material>();
                long triangles = 0;
                foreach (var filter in first.GetComponentsInChildren<MeshFilter>())
                {
                    for (var i = 0; i < filter.sharedMesh.subMeshCount; i++) triangles += filter.sharedMesh.GetIndexCount(i) / 3;
                    var other = second.transform.Find(filter.name);
                    Require(other != null && other.localPosition == filter.transform.localPosition && other.localRotation == filter.transform.localRotation, "egg placement is deterministic");
                    var material = filter.GetComponent<Renderer>().sharedMaterial;
                    Require(other.GetComponent<Renderer>().sharedMaterial == material, "egg instances reuse shared materials");
                    materials.Add(material);
                }
                Require(triangles > 0 && triangles < 15000, "egg triangle budget is strictly below 15000");
                Require(materials.Count == 5, "egg reuses four palette materials and one cocoa material");
                Require(first.transform.Find("Nest").localPosition.y == -.48f, "nest is below shell");
            }
            finally { UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); }
        }

        private static void AvatarBaseAndArDefaults()
        {
            var profile = AvatarProfile.CreateDefault();
            var home = new HomeCharacterResolver(null, _ => null).CreateAvatar(profile);
            var ar = ProceduralAvatarFactory.Create(profile);
            try
            {
                Require(home.transform.Find("SummoningMedallion") == null, "home never creates a medallion");
                Require(ar.transform.Find("SummoningMedallion") != null, "AR default keeps medallion");
                Require(home.transform.Find("Body").gameObject.activeInHierarchy && home.transform.Find("Shorts").gameObject.activeInHierarchy, "neutral base underlayer remains");
                Require(home.transform.Find("HairRoot").gameObject.activeInHierarchy, "base hair remains");
            }
            finally { UnityEngine.Object.DestroyImmediate(home); UnityEngine.Object.DestroyImmediate(ar); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
