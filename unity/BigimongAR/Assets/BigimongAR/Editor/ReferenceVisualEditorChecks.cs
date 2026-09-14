#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Bigimong.AR.EditorChecks
{
    public static class ReferenceVisualEditorChecks
    {
        private static readonly string[] Stages = { "GROWTH", "YOUTH", "ADULT" };
        private const string RingPrefabPath = "Assets/BigimongAR/Prefabs/ArBattleRing.prefab";

        public static void RunBehaviorChecks()
        {
            VerifyMonsterConstruction();
            VerifyCustomMeshWinding();
            VerifyAvatarConstruction();
            VerifyPreviewFraming();
            VerifyHudState();
        }

        public static void RunSceneChecks()
        {
            VerifyHudState(RequiredSceneObject("AR Battle HUD").GetComponent<ArBattleHud>(), true);
            VerifyDecorativeGraphics();
            VerifyBattleRing();
            VerifyCreatorStudio();
        }

        private static void VerifyMonsterConstruction()
        {
            for (var artId = 1; artId <= 30; artId++)
            foreach (var stage in Stages)
            {
                var root = ProceduralDragonFactory.Create(artId, stage);
                try
                {
                    var bounds = RendererBounds(root);
                    Require(bounds.size.x > .01f && bounds.size.y > .01f && bounds.size.z > .01f,
                        $"{artId:00} {stage} must have nonzero 3D renderer bounds");
                    VerifyFiniteScales(root.transform, artId, stage);
                    Require(FindRecursive(root.transform, "EyeHighlightL") != null, $"{artId:00} needs a left eye catchlight");
                    Require(FindRecursive(root.transform, "EyeHighlightR") != null, $"{artId:00} needs a right eye catchlight");
                    Require(FindRecursive(root.transform, "Belly") != null, $"{artId:00} needs a cream belly");
                    VerifyMonsterFaceLayering(root.transform, artId);
                    VerifyDistinctiveAnatomy(root.transform, artId);
                    VerifyRemovableCosmetics(root.transform, artId);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }

        private static void VerifyMonsterFaceLayering(Transform root, int artId)
        {
            foreach (var suffix in new[] { "L", "R" })
            {
                AssertSurfaceOverlap(root, "Head", "EyeWhite" + suffix, artId);
                AssertLayerOverlap(root, "EyeWhite" + suffix, "Eye" + suffix, artId);
                AssertLayerOverlap(root, "Eye" + suffix, "Pupil" + suffix, artId);
                AssertLayerOverlap(root, "Pupil" + suffix, "EyeHighlight" + suffix, artId);
            }
        }

        private static void AssertSurfaceOverlap(Transform root, string surfaceName, string featureName, int artId)
        {
            var surface = FindRecursive(root, surfaceName);
            var feature = FindRecursive(root, featureName);
            Require(surface != null && feature != null, $"{artId:00} is missing {surfaceName}/{featureName}");
            var offset = feature.localPosition - surface.localPosition;
            var radiusX = surface.localScale.x * .5f;
            var radiusY = surface.localScale.y * .5f;
            var radiusZ = surface.localScale.z * .5f;
            var normalized = 1f - offset.x * offset.x / (radiusX * radiusX) - offset.y * offset.y / (radiusY * radiusY);
            Require(normalized >= 0f, $"{artId:00} {featureName} center must sit inside the head silhouette");
            var surfaceDepth = surface.localPosition.z + radiusZ * Mathf.Sqrt(normalized);
            var featureRadius = feature.localScale.z * .5f;
            Require(feature.localPosition.z - featureRadius < surfaceDepth && feature.localPosition.z + featureRadius > surfaceDepth,
                $"{artId:00} {featureName} must be inset into {surfaceName}");
        }

        private static void AssertLayerOverlap(Transform root, string backName, string frontName, int artId)
        {
            var back = FindRecursive(root, backName);
            var front = FindRecursive(root, frontName);
            Require(back != null && front != null, $"{artId:00} is missing {backName}/{frontName}");
            var backFront = back.localPosition.z + back.localScale.z * .5f;
            var frontBack = front.localPosition.z - front.localScale.z * .5f;
            Require(frontBack < backFront && front.localPosition.z > back.localPosition.z,
                $"{artId:00} {frontName} must overlap and sit in front of {backName}");
        }

        private static void VerifyDistinctiveAnatomy(Transform root, int artId)
        {
            var plan = BigimongAppearanceCatalog.Resolve(artId).bodyPlan;
            if (plan == BigimongBodyPlan.Ceratopsian)
            {
                Require(FindRecursive(root, "HornL") != null, $"{artId:00} ceratopsian needs horns");
                Require(FindRecursive(root, "Frill") != null, $"{artId:00} ceratopsian needs a frill");
            }
            if (plan == BigimongBodyPlan.Pterosaur)
                Require(FindRecursive(root, "WingL") != null && FindRecursive(root, "WingR") != null,
                    $"{artId:00} pterosaur needs paired shaped wings");
            if (plan == BigimongBodyPlan.Plated)
                Require(FindRecursive(root, "BackPlate") != null, $"{artId:00} plated dinosaur needs back plates");
            if (plan == BigimongBodyPlan.Spinosaur)
                Require(FindRecursive(root, "Sail") != null, $"{artId:00} spinosaur needs a sail");
            if (plan == BigimongBodyPlan.Aquatic)
            {
                Require(FindRecursive(root, "FinL") != null && FindRecursive(root, "FinR") != null,
                    $"{artId:00} aquatic reptile needs flippers");
                Require(FindRecursive(root, "WingL") == null, $"{artId:00} aquatic reptile cannot have arbitrary wings");
            }
            if (plan == BigimongBodyPlan.LongNeck)
                Require(FindRecursive(root, "LongNeck") != null, $"{artId:00} sauropod needs a long neck");
            if (plan == BigimongBodyPlan.Armored)
                Require(FindRecursive(root, "ArmorShell") != null && FindRecursive(root, "TailClub") != null,
                    $"{artId:00} armored dinosaur needs shell and club");
            if (artId == 1)
                Require(FindRecursive(root, "WingL") == null, "ordinary Tyrannosaurus cannot have wings");
        }

        private static void VerifyRemovableCosmetics(Transform root, int artId)
        {
            var cosmetic = FindRecursive(root, "CosmeticRoot");
            var body = FindRecursive(root, "Body");
            Require(cosmetic != null, $"{artId:00} needs a detachable cosmetic root");
            Require(body != null && !body.IsChildOf(cosmetic), $"{artId:00} body cannot live under cosmetics");
            var renderer = body.GetComponent<Renderer>();
            var size = renderer.bounds.size;
            UnityEngine.Object.DestroyImmediate(cosmetic.gameObject);
            Require(renderer != null && renderer.enabled && renderer.bounds.size == size,
                $"{artId:00} cosmetic removal must not affect body rendering");
        }

        private static void VerifyAvatarConstruction()
        {
            foreach (var bodyType in new[] { "MASCULINE", "FEMININE" })
            for (var hairStyleId = 1; hairStyleId <= 12; hairStyleId++)
            {
                var root = ProceduralAvatarFactory.Create(new AvatarProfile
                {
                    bodyType = bodyType,
                    hairStyleId = hairStyleId,
                });
                try
                {
                    var bounds = RendererBounds(root);
                    Require(bounds.size.x > .01f && bounds.size.y > .01f, $"{bodyType} hair {hairStyleId} must render");
                    VerifyFiniteScales(root.transform, hairStyleId, bodyType);
                    foreach (var name in new[]
                    {
                        "EyeHighlightLeft", "EyeHighlightRight", "Nose", "Smile", "CheekLeft", "CheekRight",
                        "HairRoot", "SummoningMedallion", "CosmeticRoot"
                    })
                        Require(FindRecursive(root.transform, name) != null, $"avatar is missing {name}");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }

        private static void VerifyHudState()
        {
            var host = new GameObject("HUD Visual Behavior Check");
            try
            {
                var hud = host.AddComponent<ArBattleHud>();
                var fillA = ImageChild(host.transform, "Player A Fill");
                var fillB = ImageChild(host.transform, "Player B Fill");
                SetField(hud, "playerAHpFill", fillA);
                SetField(hud, "playerBHpFill", fillB);
                VerifyHudState(hud, false);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void VerifyPreviewFraming()
        {
            var cameraObject = new GameObject("Portrait Preview Camera", typeof(Camera));
            var anchorObject = new GameObject("Portrait Preview Anchor");
            var avatar = ProceduralAvatarFactory.Create(AvatarProfile.CreateDefault());
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                camera.aspect = 1080f / 1920f;
                camera.fieldOfView = 60f;
                anchorObject.transform.SetParent(cameraObject.transform, false);
                anchorObject.transform.localPosition = new Vector3(0, .08f, 3.1f);
                avatar.transform.SetParent(anchorObject.transform, false);
                avatar.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                var frame = typeof(AvatarCreatorController).GetMethod("FramePreview",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Require(frame != null, "avatar creator needs bounds-safe preview framing");
                frame.Invoke(null, new object[] { avatar });

                var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                foreach (var renderer in avatar.GetComponentsInChildren<Renderer>(true))
                {
                    var bounds = renderer.bounds;
                    for (var corner = 0; corner < 8; corner++)
                    {
                        var world = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                            (corner & 1) == 0 ? -1f : 1f,
                            (corner & 2) == 0 ? -1f : 1f,
                            (corner & 4) == 0 ? -1f : 1f));
                        var viewport = camera.WorldToViewportPoint(world);
                        Require(viewport.z > 0f, "avatar preview must stay in front of camera");
                        min = Vector2.Min(min, viewport);
                        max = Vector2.Max(max, viewport);
                    }
                }
                Require(min.x >= .05f && max.x <= .95f, $"avatar preview exceeds portrait width: {min.x}..{max.x}");
                Require(min.y >= .50f && max.y <= .91f, $"avatar preview overlaps creator controls: {min.y}..{max.y}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(avatar);
                UnityEngine.Object.DestroyImmediate(anchorObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static void VerifyHudState(ArBattleHud hud, bool requireRenderedSprite)
        {
            Require(hud != null, "battle HUD component is missing");
            hud.SetBattleState(2, -2, 9, "ACTIVE", string.Empty);
            var fillA = GetField<Image>(hud, "playerAHpFill");
            var fillB = GetField<Image>(hud, "playerBHpFill");
            Require(fillA != null && Mathf.Approximately(fillA.fillAmount, 0f), "player A HP fill must clamp low");
            Require(fillB != null && Mathf.Approximately(fillB.fillAmount, 1f), "player B HP fill must clamp high");
            if (requireRenderedSprite)
                Require(fillA.sprite != null && fillB.sprite != null, "filled HP images need sprites so fillAmount changes rendered geometry");
            hud.SetBattleState(3, 3, 1, "ACTIVE", string.Empty);
            Require(Mathf.Approximately(fillA.fillAmount, .6f), "player A HP fill must track battle state");
            Require(Mathf.Approximately(fillB.fillAmount, .2f), "player B HP fill must track battle state");
        }

        private static void VerifyCustomMeshWinding()
        {
            var host = new GameObject("Custom Mesh Winding Check");
            var material = ProceduralCharacterGeometry.MaterialFor("Winding Check", Color.white);
            try
            {
                var cone = ProceduralCharacterGeometry.CreateConeMesh(host.transform, "Cone", Vector3.zero,
                    Vector3.one, Quaternion.identity, material, 10).GetComponent<MeshFilter>().sharedMesh;
                var coneTriangles = cone.triangles;
                var coneVertices = cone.vertices;
                Require(TriangleNormal(coneVertices, coneTriangles, 0).y > 0f ||
                    Vector3.Dot(TriangleNormal(coneVertices, coneTriangles, 0), TriangleCenter(coneVertices, coneTriangles, 0)) > 0f,
                    "cone side triangles must face outward");
                Require(TriangleNormal(coneVertices, coneTriangles, 1).y < 0f, "cone base triangles must face downward");

                foreach (var side in new[] { -1f, 1f })
                {
                    var wing = ProceduralCharacterGeometry.CreateWingMesh(host.transform, "Wing", side, Vector3.zero,
                        Vector3.one, Quaternion.identity, material).GetComponent<MeshFilter>().sharedMesh;
                    Require(TriangleNormal(wing.vertices, wing.triangles, 0).y > 0f, $"wing {side} top must face upward");
                    Require(TriangleNormal(wing.vertices, wing.triangles, 2).y < 0f, $"wing {side} bottom must face downward");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static Vector3 TriangleNormal(Vector3[] vertices, int[] triangles, int triangle)
        {
            var offset = triangle * 3;
            var a = vertices[triangles[offset]];
            var b = vertices[triangles[offset + 1]];
            var c = vertices[triangles[offset + 2]];
            return Vector3.Cross(b - a, c - a).normalized;
        }

        private static Vector3 TriangleCenter(Vector3[] vertices, int[] triangles, int triangle)
        {
            var offset = triangle * 3;
            return (vertices[triangles[offset]] + vertices[triangles[offset + 1]] + vertices[triangles[offset + 2]]) / 3f;
        }

        private static void VerifyDecorativeGraphics()
        {
            var hud = RequiredSceneObject("AR Battle HUD");
            foreach (var graphic in hud.GetComponentsInChildren<Graphic>(true))
            {
                var decorative = graphic.name.Contains("Cyan Border") || graphic.name == "Impact Flash" ||
                    graphic.name.Contains("Panel") || graphic.name.Contains("HP Bar");
                if (decorative) Require(!graphic.raycastTarget, $"decorative HUD graphic '{graphic.name}' cannot swallow input");
            }

            foreach (var name in new[] { "Left", "Center", "Right" })
            {
                var button = RequiredSceneObject(name).GetComponent<Button>();
                Require(button != null, $"{name} direction control needs a Button");
                Require(button.GetComponent<RectTransform>().rect.width >= 200f, $"{name} direction control must be thumb sized");
            }
        }

        private static void VerifyBattleRing()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RingPrefabPath);
            Require(prefab != null, "battle ring prefab was not generated");
            var cutout = FindRecursive(prefab.transform, "Ring Center Cutout");
            Require(cutout != null && cutout.GetComponent<Renderer>() == null, "ring center must remain transparent to the live camera");
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Require(renderers.Length >= 32, "cyan ring needs a continuous visible outline");
            foreach (var renderer in renderers)
                Require(new Vector2(renderer.transform.localPosition.x, renderer.transform.localPosition.z).magnitude > .35f,
                    "ring geometry must leave its center open");
        }

        private static void VerifyCreatorStudio()
        {
            var backdrop = RequiredSceneObject("Avatar Studio Backdrop").GetComponent<Image>();
            Require(backdrop != null && !backdrop.raycastTarget, "studio backdrop must not intercept creator input");
            Require(RequiredSceneObject("Studio Key Light").GetComponent<Light>() != null, "creator needs a studio key light");
            Require(RequiredSceneObject("Studio Fill Light").GetComponent<Light>() != null, "creator needs a studio fill light");
        }

        private static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Require(renderers.Length > 0, $"{root.name} needs renderers");
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static void VerifyFiniteScales(Transform root, int id, string variant)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            foreach (var value in new[] { transform.localScale.x, transform.localScale.y, transform.localScale.z })
                Require(!float.IsNaN(value) && !float.IsInfinity(value) && value > 0f,
                    $"{id} {variant} contains an invalid scale on {transform.name}");
        }

        private static GameObject RequiredSceneObject(string name)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var match = FindRecursive(root.transform, name);
                if (match != null) return match.gameObject;
            }
            throw new InvalidOperationException($"Scene object '{name}' was not created.");
        }

        private static Transform FindRecursive(Transform current, string name)
        {
            if (current.name == name) return current;
            for (var index = 0; index < current.childCount; index++)
            {
                var match = FindRecursive(current.GetChild(index), name);
                if (match != null) return match;
            }
            return null;
        }

        private static Image ImageChild(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            return child.GetComponent<Image>();
        }

        private static T GetField<T>(object target, string name) where T : class
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(target) as T;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException($"Missing serialized field '{name}'.");
            field.SetValue(target, value);
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
#endif
