#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Bigimong.AR;
using UnityEditor;
using UnityEngine;

namespace Bigimong.Editor
{
    [Serializable]
    public sealed class VarcoAssetManifest
    {
        public VarcoAssetDefinition[] assets;
    }

    [Serializable]
    public sealed class VarcoAssetDefinition
    {
        public string key;
        public string providerTitle;
        public string role;
        public string status;
        public string unitySourceModelPath;
        public string unityPrefabPath;
        public string rig;
        public float targetHeightM;
        public int triangles;
        public int triangleLimit;
        public int materialLimit;
        public bool tPose;
    }

    public readonly struct VarcoNormalization
    {
        public readonly float Scale;
        public readonly float OffsetY;

        public VarcoNormalization(float scale, float offsetY)
        {
            Scale = scale;
            OffsetY = offsetY;
        }
    }

    public static class VarcoAssetImportPipeline
    {
        private const string ManifestRepositoryPath = "art/varco3d/v0.18-ar-pilot.json";
        private const string CatalogAssetPath = "Assets/BigimongAR/Resources/ArCharacterCatalog.asset";
        private const string GeneratedAssetLabel = "Bigimong.VARCO.Generated";
        public const string SourceFolder = "Assets/BigimongAR/Art/Varco/Source/";

        public static IReadOnlyList<VarcoAssetDefinition> LoadDefinitions()
        {
            var manifestPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../../../",
                ManifestRepositoryPath
            ));
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("VARCO asset manifest is missing.", manifestPath);

            var manifest = JsonUtility.FromJson<VarcoAssetManifest>(File.ReadAllText(manifestPath));
            if (manifest?.assets == null || manifest.assets.Length == 0)
                throw new InvalidDataException("VARCO asset manifest does not contain any assets.");
            return manifest.assets;
        }

        public static VarcoAssetDefinition FindBySourceModelPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return null;
            var normalizedPath = assetPath.Replace('\\', '/');
            foreach (var definition in LoadDefinitions())
                if (string.Equals(definition.unitySourceModelPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                    return definition;
            return null;
        }

        public static string AbsoluteProjectPath(string unityAssetPath)
        {
            if (string.IsNullOrWhiteSpace(unityAssetPath) || !unityAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Path must be project-relative and start with Assets/.", nameof(unityAssetPath));
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", unityAssetPath));
        }

        public static VarcoNormalization CalculateNormalization(float minY, float maxY, float targetHeightM)
        {
            var height = maxY - minY;
            if (float.IsNaN(height) || float.IsInfinity(height) || height <= Mathf.Epsilon)
                throw new ArgumentOutOfRangeException(nameof(maxY), "VARCO model bounds must have a positive finite height.");
            if (float.IsNaN(targetHeightM) || float.IsInfinity(targetHeightM) || targetHeightM <= 0f)
                throw new ArgumentOutOfRangeException(nameof(targetHeightM), "Target height must be positive and finite.");

            var scale = targetHeightM / height;
            return new VarcoNormalization(scale, -minY * scale);
        }

        [MenuItem("Bigimong/VARCO/Rebuild Accepted Prefabs")]
        public static void BuildAcceptedPrefabs()
        {
            var changed = false;
            foreach (var definition in LoadDefinitions())
            {
                RequireOwnedOutputPath(definition.unityPrefabPath);
                if (!File.Exists(AbsoluteProjectPath(definition.unitySourceModelPath)))
                {
                    changed |= DeleteOwnedGeneratedPrefab(definition.unityPrefabPath);
                    continue;
                }

                var source = AssetDatabase.LoadAssetAtPath<GameObject>(definition.unitySourceModelPath);
                if (source == null)
                    throw new InvalidDataException($"VARCO source model could not be loaded: {definition.unitySourceModelPath}");

                if (!TryValidateProductionQuality(source, out var reason))
                {
                    changed |= DeleteOwnedGeneratedPrefab(definition.unityPrefabPath);
                    Debug.LogWarning($"{definition.key}: unaccepted beta art ({reason}); procedural coverage is not final art acceptance.");
                    continue;
                }

                CreateNormalizedPrefab(source, definition);
                changed = true;
            }

            var catalogExists = AssetDatabase.LoadAssetAtPath<CharacterPrefabCatalog>(CatalogAssetPath) != null;
            if (!changed && !AnyOwnedGeneratedPrefabExists() && !catalogExists) return;
            UpdateCharacterCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static bool DeleteOwnedGeneratedPrefab(string prefabPath)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(prefabPath);
            if (!IsOwnedGeneratedAsset(asset)) return false;
            if (!AssetDatabase.DeleteAsset(prefabPath))
                throw new IOException($"Could not delete stale VARCO prefab: {prefabPath}");
            return true;
        }

        private static bool AnyOwnedGeneratedPrefabExists()
        {
            foreach (var definition in LoadDefinitions())
                if (IsOwnedGeneratedAsset(AssetDatabase.LoadMainAssetAtPath(definition.unityPrefabPath)))
                    return true;
            return false;
        }

        private static bool IsOwnedGeneratedAsset(UnityEngine.Object asset)
        {
            if (asset == null) return false;
            return Array.IndexOf(AssetDatabase.GetLabels(asset), GeneratedAssetLabel) >= 0;
        }

        private static void RequireOwnedOutputPath(string prefabPath)
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(prefabPath);
            if (existing == null || IsOwnedGeneratedAsset(existing)) return;
            throw new InvalidOperationException(
                $"An unowned asset occupies the reserved VARCO output path: {prefabPath}. " +
                "Move it before importing the VARCO source so the pipeline never overwrites user content."
            );
        }

        private static void CreateNormalizedPrefab(GameObject source, VarcoAssetDefinition definition)
        {
            EnsureAssetFolder(Path.GetDirectoryName(definition.unityPrefabPath)?.Replace('\\', '/'));
            var root = new GameObject(Path.GetFileNameWithoutExtension(definition.unityPrefabPath));
            try
            {
                var model = PrefabUtility.InstantiatePrefab(source) as GameObject;
                if (model == null) throw new InvalidOperationException($"Could not instantiate VARCO model: {definition.key}");

                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                // Only construct a group from authored, quality-checked LOD meshes. Never invent a duplicate LOD.
                if (model.GetComponentsInChildren<LODGroup>(true).Length == 0)
                {
                    FindNamedLods(model, out var lod0, out var lod1);
                    var group = model.AddComponent<LODGroup>();
                    group.SetLODs(new[] { new LOD(.6f, lod0), new LOD(.2f, lod1) });
                    group.RecalculateBounds();
                }

                if (!TryGetRendererBounds(model, out var bounds))
                    throw new InvalidDataException($"VARCO model has no renderer bounds: {definition.key}");
                var normalization = CalculateNormalization(bounds.min.y, bounds.max.y, definition.targetHeightM);
                model.transform.localScale = Vector3.one * normalization.Scale;
                model.transform.localPosition = Vector3.up * normalization.OffsetY;

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, definition.unityPrefabPath, out var success);
                if (!success || prefab == null)
                    throw new InvalidOperationException($"Could not save normalized VARCO prefab: {definition.unityPrefabPath}");
                var labels = new List<string>(AssetDatabase.GetLabels(prefab));
                if (!labels.Contains(GeneratedAssetLabel)) labels.Add(GeneratedAssetLabel);
                AssetDatabase.SetLabels(prefab, labels.ToArray());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            return true;
        }

        /// <summary>Production import acceptance only; never call with procedural fallback coverage.</summary>
        public static bool TryValidateProductionQuality(GameObject root, out string reason)
        {
            reason = null;
            if (root == null) { reason = "missing model"; return false; }
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var materials = new HashSet<Material>();
            var bones = new HashSet<Transform>();
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials) materials.Add(material);
                if (renderer is SkinnedMeshRenderer skinned)
                    foreach (var bone in skinned.bones) if (bone != null) bones.Add(bone);
            }
            if (materials.Count == 0 || materials.Count > 4 || materials.Contains(null))
            { reason = "requires 1–4 non-null unique materials"; return false; }
            if (bones.Count > 64) { reason = "more than 64 deform bones"; return false; }
            foreach (var material in materials)
            {
                foreach (var property in material.GetTexturePropertyNames())
                {
                    var texture = material.GetTexture(property);
                    if (texture == null) continue;
                    var width = texture.width;
                    var height = texture.height;
                    var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
                    if (importer != null) importer.GetSourceTextureWidthAndHeight(out width, out height);
                    if (width > 1024 || height > 1024)
                    { reason = "source texture exceeds 1024×1024"; return false; }
                }
            }

            long peakTriangles = 0;
            var covered = new HashSet<Renderer>();
            var groups = root.GetComponentsInChildren<LODGroup>(true);
            if (groups.Length == 0)
            {
                FindNamedLods(root, out var lod0, out var lod1);
                if (!ValidateLodPair(lod0, lod1, covered, out peakTriangles))
                { reason = "authored LOD0/LOD1 required with LOD1 at 50–60%"; return false; }
            }
            else
            {
                foreach (var group in groups)
                {
                    if (!group.enabled)
                    { reason = "disabled authored LODGroup cannot provide exclusive LOD rendering"; return false; }
                    var lods = group.GetLODs();
                    if (lods.Length < 2 || !ValidateLodPair(lods[0].renderers, lods[1].renderers, covered, out var groupTriangles))
                    { reason = "LOD0/LOD1 required with LOD1 at 50–60%"; return false; }
                    var groupPeak = groupTriangles;
                    var previousCount = groupTriangles;
                    for (var index = 1; index < lods.Length; index++)
                    {
                        long currentCount = 0;
                        foreach (var renderer in lods[index].renderers)
                            if (renderer != null) currentCount += RenderedTriangles(renderer);
                        if (group.fadeMode != LODFadeMode.None) groupPeak = Math.Max(groupPeak, previousCount + currentCount);
                        previousCount = currentCount;
                    }
                    // Later LODs must not exceed LOD0 and must not reuse any earlier renderer.
                    for (var index = 2; index < lods.Length; index++)
                    {
                        long count = 0;
                        foreach (var renderer in lods[index].renderers)
                        {
                            if (renderer == null || !covered.Add(renderer)) { reason = "overlapping LOD renderers"; return false; }
                            count += RenderedTriangles(renderer);
                        }
                        if (count > groupTriangles) { reason = "later LOD exceeds LOD0"; return false; }
                    }
                    peakTriangles += groupPeak;
                }
            }
            foreach (var renderer in renderers)
                if (!covered.Contains(renderer)) peakTriangles += RenderedTriangles(renderer);
            foreach (var renderer in covered)
                if (!renderer.transform.IsChildOf(root.transform))
                { reason = "LOD renderer outside character root"; return false; }
            if (peakTriangles <= 0 || peakTriangles > 30000)
            { reason = "rendered triangle count outside 1–30000"; return false; }
            return true;
        }

        private static bool ValidateLodPair(Renderer[] lod0, Renderer[] lod1, HashSet<Renderer> covered, out long high)
        {
            high = 0;
            long low = 0;
            foreach (var renderer in lod0)
            {
                if (renderer == null || !covered.Add(renderer)) return false;
                high += RenderedTriangles(renderer);
            }
            foreach (var renderer in lod1)
            {
                if (renderer == null || !covered.Add(renderer)) return false;
                low += RenderedTriangles(renderer);
            }
            return high > 0 && low * 100 >= high * 50 && low * 100 <= high * 60;
        }

        private static void FindNamedLods(GameObject root, out Renderer[] lod0, out Renderer[] lod1)
        {
            var high = new List<Renderer>();
            var low = new List<Renderer>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                for (var node = renderer.transform; node != null; node = node.parent)
                {
                    if (node.name == "LOD0") { high.Add(renderer); break; }
                    if (node.name == "LOD1") { low.Add(renderer); break; }
                    if (node == root.transform) break;
                }
            }
            lod0 = high.ToArray();
            lod1 = low.ToArray();
        }

        public static long RenderedTriangles(Renderer renderer)
        {
            var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) return 0;
            if (mesh.subMeshCount == 0) return 0;
            long count = 0;
            // Extra material slots redraw the final submesh; count those actual draws too.
            var drawCount = Math.Max(mesh.subMeshCount, renderer.sharedMaterials.Length);
            for (var draw = 0; draw < drawCount; draw++)
            {
                var subMesh = Math.Min(draw, mesh.subMeshCount - 1);
                if (mesh.GetTopology(subMesh) == MeshTopology.Triangles) count += (long)mesh.GetIndexCount(subMesh) / 3;
            }
            return count;
        }

        private static void UpdateCharacterCatalog()
        {
            EnsureAssetFolder(Path.GetDirectoryName(CatalogAssetPath)?.Replace('\\', '/'));
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterPrefabCatalog>(CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterPrefabCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            var serializedCatalog = new SerializedObject(catalog);
            var characters = serializedCatalog.FindProperty("characters")
                ?? throw new MissingFieldException(nameof(CharacterPrefabCatalog), "characters");
            var entry = FindOrCreateArtEntry(characters, 1);
            entry.FindPropertyRelative("artId").intValue = 1;

            foreach (var definition in LoadDefinitions())
            {
                var stageProperty = definition.key switch
                {
                    "bigimong_01_baby" => entry.FindPropertyRelative("baby"),
                    "bigimong_01_teen" => entry.FindPropertyRelative("teen"),
                    "bigimong_01_adult" => entry.FindPropertyRelative("adult"),
                    _ => null,
                };
                if (stageProperty == null) continue;

                var prefab = LoadOwnedPrefabForCatalog(definition);
                stageProperty.objectReferenceValue = prefab;
            }

            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static GameObject LoadOwnedPrefabForCatalog(VarcoAssetDefinition definition)
        {
            if (!File.Exists(AbsoluteProjectPath(definition.unitySourceModelPath))) return null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.unityPrefabPath);
            if (!TryValidateProductionQuality(prefab, out _)) return null;
            return IsOwnedGeneratedAsset(prefab) ? prefab : null;
        }

        private static SerializedProperty FindOrCreateArtEntry(SerializedProperty characters, int artId)
        {
            for (var index = 0; index < characters.arraySize; index++)
            {
                var candidate = characters.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("artId").intValue == artId) return candidate;
            }

            characters.InsertArrayElementAtIndex(characters.arraySize);
            var created = characters.GetArrayElementAtIndex(characters.arraySize - 1);
            created.FindPropertyRelative("artId").intValue = artId;
            created.FindPropertyRelative("baby").objectReferenceValue = null;
            created.FindPropertyRelative("teen").objectReferenceValue = null;
            created.FindPropertyRelative("adult").objectReferenceValue = null;
            return created;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath)) return;
            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(parent))
                throw new InvalidOperationException($"Invalid Unity asset folder: {folderPath}");
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }
    }

    public sealed class VarcoModelImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(VarcoAssetImportPipeline.SourceFolder, StringComparison.OrdinalIgnoreCase)) return;
            var definition = VarcoAssetImportPipeline.FindBySourceModelPath(assetPath);
            if (definition == null) return;

            var importer = (ModelImporter)assetImporter;
            importer.addCollider = false;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

            switch (definition.rig)
            {
                case "humanoid":
                    importer.importAnimation = true;
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    break;
                case "generic":
                    importer.importAnimation = true;
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    break;
                default:
                    importer.importAnimation = false;
                    importer.animationType = ModelImporterAnimationType.None;
                    break;
            }
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths
        )
        {
            if (!ContainsVarcoSourceChange(importedAssets) &&
                !ContainsVarcoSourceChange(deletedAssets) &&
                !ContainsVarcoSourceChange(movedAssets) &&
                !ContainsVarcoSourceChange(movedFromAssetPaths)) return;

            EditorApplication.delayCall -= VarcoAssetImportPipeline.BuildAcceptedPrefabs;
            EditorApplication.delayCall += VarcoAssetImportPipeline.BuildAcceptedPrefabs;
        }

        private static bool ContainsVarcoSourceChange(string[] assetPaths)
        {
            foreach (var assetPath in assetPaths)
            {
                if (assetPath.StartsWith(VarcoAssetImportPipeline.SourceFolder, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
#endif
