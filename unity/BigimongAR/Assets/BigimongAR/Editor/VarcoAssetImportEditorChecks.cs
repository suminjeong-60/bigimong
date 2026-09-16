#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Bigimong.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Bigimong.AR.EditorChecks
{
    public static class VarcoAssetImportEditorChecks
    {
        public static void PrepareAndValidate()
        {
            RunBehaviorChecks();
            VarcoAssetImportPipeline.BuildAcceptedPrefabs();
            RunImportedAssetChecks();
        }

        public static void RunBehaviorChecks()
        {
            ProductionBudgetBoundaries();
            SourceTextureBoundsSurviveDownsampling();
            LodAcceptanceBoundaries();
            DisabledLodGroupsAreNotAccepted();
            var normalization = VarcoAssetImportPipeline.CalculateNormalization(-1f, 2f, 1.5f);
            Require(Mathf.Approximately(normalization.Scale, .5f), "VARCO normalization must fit target height");
            Require(Mathf.Approximately(normalization.OffsetY, .5f), "VARCO normalization must ground the lowest point");

            var rejectedFlatBounds = false;
            try
            {
                VarcoAssetImportPipeline.CalculateNormalization(1f, 1f, 1f);
            }
            catch (ArgumentOutOfRangeException)
            {
                rejectedFlatBounds = true;
            }
            Require(rejectedFlatBounds, "VARCO normalization must reject flat renderer bounds");
        }

        private static void ProductionBudgetBoundaries()
        {
            var fixture = CreateQualityFixture(30000, 16500);
            var fixtureMaterial = fixture.transform.Find("LOD0").GetComponent<Renderer>().sharedMaterial;
            var fixtureTexture = fixtureMaterial.mainTexture;
            var extras = new List<UnityEngine.Object>();
            try
            {
                Require(VarcoAssetImportPipeline.TryValidateProductionQuality(fixture, out _), "30000 peak rendered triangles accepted; LODs are not summed");
                var lod0 = fixture.transform.Find("LOD0").GetComponent<SkinnedMeshRenderer>();
                var original = lod0.sharedMesh;
                lod0.sharedMesh = TriangleMesh(30001);
                extras.Add(lod0.sharedMesh);
                Require(!VarcoAssetImportPipeline.TryValidateProductionQuality(fixture, out _), "30001 rendered triangles rejected");
                lod0.sharedMesh = original;
                // Use a small independent mesh while checking material limits, since extra material
                // slots draw the last submesh again and must count toward rendered triangles.
                lod0.sharedMesh = TriangleMesh(100); extras.Add(lod0.sharedMesh);
                var lod1 = fixture.transform.Find("LOD1").GetComponent<SkinnedMeshRenderer>();
                var originalLow = lod1.sharedMesh;
                lod1.sharedMesh = TriangleMesh(55); extras.Add(lod1.sharedMesh);
                var materials = new Material[5];
                materials[0] = lod0.sharedMaterial;
                for (var index = 1; index < 5; index++) { materials[index] = new Material(materials[0]); extras.Add(materials[index]); }
                // Matching slots at each LOD keep the authored 55% ratio unchanged.
                lod0.sharedMaterials = new[] { materials[0], materials[1], materials[2], materials[3] };
                lod1.sharedMaterials = lod0.sharedMaterials;
                Require(VarcoAssetImportPipeline.TryValidateProductionQuality(fixture, out _), "four unique materials accepted");
                lod0.sharedMaterials = materials;
                lod1.sharedMaterials = materials;
                Require(!VarcoAssetImportPipeline.TryValidateProductionQuality(fixture, out _), "five unique materials rejected");
                lod0.sharedMaterials = new[] { materials[0] };
                lod1.sharedMaterials = new[] { materials[0] };
                lod0.sharedMesh = original;
                lod1.sharedMesh = originalLow;
                var texture = new Texture2D(1024, 1024); extras.Add(texture);
                materials[0].mainTexture = texture;
                Require(VarcoAssetImportPipeline.TryValidateProductionQuality(fixture, out _), "1024 square texture accepted");
                var tooWide = new Texture2D(1025, 1); extras.Add(tooWide);
                materials[0].mainTexture = tooWide;
                Require(!VarcoAssetImportPipeline.TryValidateProductionQuality(fixture, out _), "1025 source width rejected");
                var tooTall = new Texture2D(1, 1025); extras.Add(tooTall);
                materials[0].mainTexture = tooTall;
                Require(!VarcoAssetImportPipeline.TryValidateProductionQuality(fixture, out _), "1025 source height rejected");
                materials[0].mainTexture = texture;
                var bones = new Transform[65];
                for (var index = 0; index < bones.Length; index++) { bones[index] = new GameObject("Bone" + index).transform; bones[index].SetParent(fixture.transform, false); }
                var allowedBones = new Transform[64]; Array.Copy(bones, allowedBones, 64);
                lod0.bones = allowedBones;
                Require(VarcoAssetImportPipeline.TryValidateProductionQuality(fixture, out _), "64 deform bones accepted");
                lod0.bones = bones;
                Require(!VarcoAssetImportPipeline.TryValidateProductionQuality(fixture, out _), "65 deform bones rejected");
            }
            finally
            {
                fixtureMaterial.mainTexture = fixtureTexture;
                foreach (var item in extras) UnityEngine.Object.DestroyImmediate(item);
                DestroyQualityFixture(fixture);
            }
        }

        private static void LodAcceptanceBoundaries()
        {
            foreach (var count in new[] { 49, 50, 55, 60, 61 })
            {
                var fixture = CreateQualityFixture(100, count);
                try { Require(VarcoAssetImportPipeline.TryValidateProductionQuality(fixture, out _) == (count == 50 || count == 55 || count == 60), "LOD1 accepts inclusive 50–60 percent only"); }
                finally { DestroyQualityFixture(fixture); }
            }
            var missing = CreateQualityFixture(100, 55);
            try
            {
                var group = missing.GetComponent<LODGroup>();
                group.SetLODs(new[] { group.GetLODs()[0] });
                Require(!VarcoAssetImportPipeline.TryValidateProductionQuality(missing, out _), "LOD0 alone is not accepted production art");
                UnityEngine.Object.DestroyImmediate(group);
                Require(VarcoAssetImportPipeline.TryValidateProductionQuality(missing, out _), "authored LOD0/LOD1 children can construct a group during import");
                missing.transform.Find("LOD1").name = "NoLodData";
                Require(!VarcoAssetImportPipeline.TryValidateProductionQuality(missing, out _), "missing LOD data remains unaccepted beta art");
            }
            finally { DestroyQualityFixture(missing); }
            var repeatedDraw = CreateQualityFixture(20000, 11000);
            try
            {
                var renderer = repeatedDraw.transform.Find("LOD0").GetComponent<Renderer>();
                var low = repeatedDraw.transform.Find("LOD1").GetComponent<Renderer>();
                renderer.sharedMaterials = new[] { renderer.sharedMaterial, renderer.sharedMaterial };
                low.sharedMaterials = renderer.sharedMaterials;
                Require(!VarcoAssetImportPipeline.TryValidateProductionQuality(repeatedDraw, out _), "repeated material draws count toward the 30000 rendered triangle ceiling");
            }
            finally { DestroyQualityFixture(repeatedDraw); }
            var crossfade = CreateQualityFixture(20000, 11000);
            try
            {
                crossfade.GetComponent<LODGroup>().fadeMode = LODFadeMode.CrossFade;
                Require(!VarcoAssetImportPipeline.TryValidateProductionQuality(crossfade, out _), "crossfade renders both adjacent LODs and must stay within 30000 triangles");
            }
            finally { DestroyQualityFixture(crossfade); }
        }

        private static void SourceTextureBoundsSurviveDownsampling()
        {
            var fixture = CreateQualityFixture(100, 55);
            var material = fixture.transform.Find("LOD0").GetComponent<Renderer>().sharedMaterial;
            var originalTexture = material.mainTexture;
            var sourceTexture = new Texture2D(1025, 1);
            var path = "Assets/BigimongTextureQualityFixture_" + Guid.NewGuid().ToString("N") + ".png";
            try
            {
                File.WriteAllBytes(VarcoAssetImportPipeline.AbsoluteProjectPath(path), sourceTexture.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.maxTextureSize = 32;
                importer.SaveAndReimport();
                var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Require(imported.width <= 32, "fixture really exercises a downsampled imported texture");
                material.mainTexture = imported;
                Require(!VarcoAssetImportPipeline.TryValidateProductionQuality(fixture, out _), "1025 source texture cannot pass by downsampling at import");
            }
            finally
            {
                material.mainTexture = originalTexture;
                AssetDatabase.DeleteAsset(path);
                UnityEngine.Object.DestroyImmediate(sourceTexture);
                DestroyQualityFixture(fixture);
            }
        }

        private static void DisabledLodGroupsAreNotAccepted()
        {
            var fixture = CreateQualityFixture(20000, 11000);
            try
            {
                Require(VarcoAssetImportPipeline.TryValidateProductionQuality(fixture, out _), "enabled exclusive LODs have a 20000 peak and compliant 55% ratio");
                Require(CountPeakTriangles(fixture) == 20000, "enabled authored LODs count their peak, not both levels");
                fixture.GetComponent<LODGroup>().enabled = false;
                Require(!VarcoAssetImportPipeline.TryValidateProductionQuality(fixture, out _), "disabled authored LODGroup cannot qualify 31000 simultaneously rendered triangles");
                Require(CountPeakTriangles(fixture) == 31000, "secondary counter must count both levels when their LODGroup is disabled");
                Require(!fixture.GetComponent<LODGroup>().enabled, "acceptance validation must not change authored LOD enablement");
            }
            finally { DestroyQualityFixture(fixture); }
        }

        public static GameObject CreateQualityFixture(int lod0Triangles, int lod1Triangles)
        {
            var root = new GameObject("AcceptedFixture");
            var material = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));
            material.mainTexture = new Texture2D(2, 2);
            var renderers = new Renderer[2];
            for (var index = 0; index < 2; index++)
            {
                var part = new GameObject("LOD" + index, typeof(SkinnedMeshRenderer));
                part.transform.SetParent(root.transform, false);
                var renderer = part.GetComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = TriangleMesh(index == 0 ? lod0Triangles : lod1Triangles);
                renderer.sharedMaterial = material;
                renderers[index] = renderer;
            }
            root.AddComponent<LODGroup>().SetLODs(new[] { new LOD(.6f, new[] { renderers[0] }), new LOD(.2f, new[] { renderers[1] }) });
            return root;
        }

        private static Mesh TriangleMesh(int count)
        {
            var indices = new int[count * 3];
            for (var i = 0; i < indices.Length; i++) indices[i] = i % 3;
            return new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = indices };
        }

        public static void DestroyQualityFixture(GameObject root)
        {
            var materials = CollectMaterials(root);
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>()) UnityEngine.Object.DestroyImmediate(renderer.sharedMesh);
            foreach (var material in materials)
                if (material != null) { if (material.mainTexture != null) UnityEngine.Object.DestroyImmediate(material.mainTexture); UnityEngine.Object.DestroyImmediate(material); }
            UnityEngine.Object.DestroyImmediate(root);
        }

        public static void RunImportedAssetChecks()
        {
            foreach (var definition in VarcoAssetImportPipeline.LoadDefinitions())
            {
                var sourcePath = VarcoAssetImportPipeline.AbsoluteProjectPath(definition.unitySourceModelPath);
                if (!File.Exists(sourcePath)) continue;

                var source = AssetDatabase.LoadAssetAtPath<GameObject>(definition.unitySourceModelPath);
                if (!VarcoAssetImportPipeline.TryValidateProductionQuality(source, out var reason))
                {
                    Require(AssetDatabase.LoadAssetAtPath<GameObject>(definition.unityPrefabPath) == null,
                        $"{definition.key}: rejected beta art must not retain an accepted Resources prefab");
                    Debug.LogWarning($"{definition.key}: not accepted production art: {reason}");
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.unityPrefabPath);
                Require(prefab != null, $"{definition.key}: normalized prefab was not generated");
                ValidateRig(definition);
                ValidateNormalizedPrefab(prefab, definition);
            }
        }

        private static void ValidateNormalizedPrefab(GameObject prefab, VarcoAssetDefinition definition)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Require(instance != null, $"{definition.key}: normalized prefab cannot be instantiated");
            try
            {
                Require(instance.transform.localScale == Vector3.one, $"{definition.key}: prefab root scale must stay uniform at one");
                Require(VarcoAssetImportPipeline.TryGetRendererBounds(instance, out var bounds),
                    $"{definition.key}: prefab needs renderer bounds");

                var tolerance = Mathf.Max(.002f, definition.targetHeightM * .015f);
                Require(Mathf.Abs(bounds.size.y - definition.targetHeightM) <= tolerance,
                    $"{definition.key}: height {bounds.size.y:F3}m does not match targetHeightM {definition.targetHeightM:F3}m");
                Require(Mathf.Abs(bounds.min.y) <= .002f,
                    $"{definition.key}: lowest renderer point must sit at Y=0, found {bounds.min.y:F4}");

                Require(VarcoAssetImportPipeline.TryValidateProductionQuality(instance, out var qualityReason),
                    $"{definition.key}: production quality rejected: {qualityReason}");
                var triangles = CountPeakTriangles(instance);
                Require(triangles > 0, $"{definition.key}: mesh has no triangles");
                Require(triangles <= definition.triangleLimit,
                    $"{definition.key}: {triangles} triangles exceed triangleLimit {definition.triangleLimit}");

                var materials = CollectMaterials(instance);
                Require(materials.Count > 0, $"{definition.key}: prefab has no materials");
                Require(materials.Count <= definition.materialLimit,
                    $"{definition.key}: {materials.Count} materials exceed materialLimit {definition.materialLimit}");
                foreach (var material in materials)
                {
                    Require(material != null, $"{definition.key}: renderer contains a missing material");
                    var hasTexture = material.mainTexture != null ||
                        material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null;
                    Require(hasTexture, $"{definition.key}: material {material.name} has no imported base texture");
                }

                ValidateSummonerCompatibility(instance, definition);
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void ValidateRig(VarcoAssetDefinition definition)
        {
            var importer = AssetImporter.GetAtPath(definition.unitySourceModelPath) as ModelImporter;
            Require(importer != null, $"{definition.key}: source must use Unity's model importer");
            var expected = definition.rig switch
            {
                "humanoid" => ModelImporterAnimationType.Human,
                "generic" => ModelImporterAnimationType.Generic,
                _ => ModelImporterAnimationType.None,
            };
            Require(importer.animationType == expected,
                $"{definition.key}: rig {importer.animationType} does not match manifest rig {definition.rig}");

            if (definition.rig == "none") return;
            Avatar avatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(definition.unitySourceModelPath))
                if (asset is Avatar candidate)
                {
                    avatar = candidate;
                    break;
                }
            Require(avatar != null, $"{definition.key}: animated import did not generate an Avatar");
            Require(avatar.isValid, $"{definition.key}: imported Avatar mapping is invalid");
            if (definition.rig == "humanoid")
                Require(avatar.isHuman, $"{definition.key}: imported Avatar is not humanoid");
        }

        private static void ValidateSummonerCompatibility(GameObject instance, VarcoAssetDefinition definition)
        {
            if (definition.role != "player-avatar") return;

            var arena = new GameObject("VARCO Summoner Compatibility Arena");
            try
            {
                var summoner = instance.GetComponent<SummonerActor>() ?? instance.AddComponent<SummonerActor>();
                summoner.Initialize(arena.transform);
                Require(summoner.HasThrowPoseRig,
                    $"{definition.key}: avatar needs mapped right-arm humanoid bones for the summon throw");
                Require(summoner.PrepareThrownMedallion() != null,
                    $"{definition.key}: avatar could not create a runtime summoning medallion");
                summoner.ResetPose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(arena);
            }
        }

        private static long CountPeakTriangles(GameObject root)
        {
            long total = 0;
            var covered = new HashSet<Renderer>();
            foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
            {
                // A disabled group does not make its levels mutually exclusive. Leave those
                // renderers uncovered so the final pass counts every simultaneously drawn mesh.
                if (!group.enabled) continue;
                long maximum = 0;
                long previous = 0;
                foreach (var lod in group.GetLODs())
                {
                    long count = 0;
                    foreach (var renderer in lod.renderers) { covered.Add(renderer); count += VarcoAssetImportPipeline.RenderedTriangles(renderer); }
                    maximum = Math.Max(maximum, count + (group.fadeMode == LODFadeMode.None ? 0 : previous));
                    previous = count;
                }
                total += maximum;
            }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if (!covered.Contains(renderer)) total += VarcoAssetImportPipeline.RenderedTriangles(renderer);
            return total;
        }

        private static HashSet<Material> CollectMaterials(GameObject root)
        {
            var materials = new HashSet<Material>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    materials.Add(material);
            return materials;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }

    public sealed class VarcoAssetBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            VarcoAssetImportEditorChecks.PrepareAndValidate();
        }
    }
}
#endif
