using System.Collections.Generic;
using UnityEngine;

namespace Bigimong.AR
{
    public static class ProceduralAvatarFactory
    {
        private const float HeadCenterY = 2.15f;
        private static readonly Vector3[] HeadScales =
        {
            new Vector3(0.72f, 0.76f, 0.66f),
            new Vector3(0.76f, 0.74f, 0.66f),
            new Vector3(0.70f, 0.80f, 0.65f),
            new Vector3(0.78f, 0.78f, 0.69f),
            new Vector3(0.68f, 0.74f, 0.70f),
        };

        private static readonly float[] BrowRotations = { 4f, 0f, -5f, 8f, -10f, 13f };
        private static readonly float[] BrowCurves = { 0.00f, 0.04f, -0.035f, 0.065f, -0.06f, 0.09f };
        private static readonly Dictionary<ulong, Material> SharedMaterials = new Dictionary<ulong, Material>();

        public static GameObject Create(AvatarProfile profile)
        {
            var root = new GameObject("AvatarRoot");
            Apply(root, profile);
            return root;
        }

        public static void Apply(GameObject avatarRoot, AvatarProfile profile)
        {
            if (avatarRoot == null)
                return;

            profile = profile ?? AvatarProfile.CreateDefault();
            profile.Normalize();
            ClearVisuals(avatarRoot.transform);

            var skin = MaterialFor(AvatarCustomizationCatalog.ColorForSkinTone(profile.skinToneId), 0f, 0.48f);
            var hair = MaterialFor(AvatarCustomizationCatalog.ColorForHair(profile.hairColorId), 0f, 0.34f);
            var eyeWhite = MaterialFor(new Color(0.98f, 0.97f, 0.94f), 0f, 0.62f);
            var eye = MaterialFor(AvatarCustomizationCatalog.ColorForEye(profile.eyeColorId), 0f, 0.68f);
            var pupil = MaterialFor(new Color(0.025f, 0.018f, 0.015f), 0f, 0.55f);
            var eyeHighlight = MaterialFor(Color.white, 0f, 0.88f);
            var cheek = MaterialFor(new Color(0.96f, 0.49f, 0.45f), 0f, 0.62f);
            var smile = MaterialFor(new Color(0.36f, 0.12f, 0.10f), 0f, 0.48f);
            var outfit = MaterialFor(new Color(0.075f, 0.082f, 0.105f), 0f, 0.28f);
            var gold = MaterialFor(new Color(0.94f, 0.66f, 0.16f), 0.72f, 0.72f);

            var feminine = profile.bodyType == "FEMININE";
            CreateBody(avatarRoot.transform, feminine, skin, outfit);
            var headScale = HeadScales[Mathf.Clamp(profile.faceShapeId, 1, HeadScales.Length) - 1];
            Part(avatarRoot.transform, PrimitiveType.Sphere, "Head", new Vector3(0, HeadCenterY, 0), headScale,
                Quaternion.identity, skin);
            CreateEyes(avatarRoot.transform, headScale, eyeWhite, eye, pupil, eyeHighlight);
            CreateFace(avatarRoot.transform, headScale, skin, cheek, smile);
            CreateEyebrows(avatarRoot.transform, profile.eyebrowId, hair, headScale);
            CreateHair(avatarRoot.transform, profile.hairStyleId, hair, headScale);
            CreateSummoningMedallion(avatarRoot.transform, gold);
            var cosmeticRoot = new GameObject("CosmeticRoot");
            cosmeticRoot.transform.SetParent(avatarRoot.transform, false);
        }

        private static void CreateBody(Transform root, bool feminine, Material skin, Material outfit)
        {
            var torsoScale = feminine ? new Vector3(0.56f, 0.72f, 0.30f) : new Vector3(0.64f, 0.76f, 0.34f);
            Part(root, PrimitiveType.Capsule, "Body", new Vector3(0, 1.22f, 0), torsoScale,
                Quaternion.identity, outfit);
            Part(root, PrimitiveType.Cylinder, "Neck", new Vector3(0, 1.77f, 0), new Vector3(0.19f, 0.20f, 0.19f),
                Quaternion.identity, skin);
            Part(root, PrimitiveType.Cube, "Shorts", new Vector3(0, 0.76f, 0),
                feminine ? new Vector3(0.62f, 0.28f, 0.34f) : new Vector3(0.68f, 0.30f, 0.37f), Quaternion.identity, outfit);

            CreateArm(root, -1f, skin, feminine);
            CreateArm(root, 1f, skin, feminine);
            CreateLeg(root, -1f, skin, feminine);
            CreateLeg(root, 1f, skin, feminine);
        }

        private static void CreateArm(Transform root, float side, Material skin, bool feminine)
        {
            var suffix = side < 0 ? "Left" : "Right";
            var shoulder = feminine ? 0.46f : 0.52f;
            var armWidth = feminine ? 0.115f : 0.135f;
            Part(root, PrimitiveType.Capsule, "ArmUpper" + suffix, new Vector3(side * shoulder, 1.30f, 0),
                new Vector3(armWidth, 0.34f, armWidth), Quaternion.Euler(0, 0, side * -7f), skin);
            Part(root, PrimitiveType.Capsule, "ArmLower" + suffix, new Vector3(side * (shoulder + 0.07f), 0.83f, 0),
                new Vector3(armWidth * 0.90f, 0.31f, armWidth * 0.90f), Quaternion.Euler(0, 0, side * -4f), skin);
            Part(root, PrimitiveType.Sphere, "Hand" + suffix, new Vector3(side * (shoulder + 0.10f), 0.49f, 0),
                new Vector3(armWidth * 1.18f, armWidth * 1.42f, armWidth * 0.92f), Quaternion.identity, skin);
        }

        private static void CreateLeg(Transform root, float side, Material skin, bool feminine)
        {
            var suffix = side < 0 ? "Left" : "Right";
            var spacing = feminine ? 0.23f : 0.25f;
            var width = feminine ? 0.16f : 0.18f;
            Part(root, PrimitiveType.Capsule, "Leg" + suffix, new Vector3(side * spacing, 0.21f, 0),
                new Vector3(width, 0.42f, width), Quaternion.identity, skin);
            Part(root, PrimitiveType.Sphere, "Foot" + suffix, new Vector3(side * spacing, -0.25f, 0.09f),
                new Vector3(width * 1.12f, 0.11f, width * 1.65f), Quaternion.identity, skin);
        }

        private static void CreateEyes(Transform root, Vector3 headScale, Material eyeWhite, Material iris,
            Material pupil, Material highlight)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                var suffix = side < 0 ? "Left" : "Right";
                var x = side * headScale.x * 0.31f;
                var eyeScale = new Vector3(0.19f, 0.22f, 0.085f);
                var eyeY = HeadCenterY + 0.06f;
                var eyeDepth = FeatureDepth(headScale, x, eyeY - HeadCenterY, eyeScale.z, 0.55f);
                var eyePosition = new Vector3(x, eyeY, eyeDepth);
                Part(root, PrimitiveType.Sphere, "Eye" + suffix, eyePosition, eyeScale,
                    Quaternion.identity, eyeWhite);
                var irisScale = new Vector3(0.092f, 0.115f, 0.035f);
                var irisDepth = LayerDepth(eyeDepth + eyeScale.z * 0.5f, irisScale.z, 0.55f);
                Part(root, PrimitiveType.Sphere, "Iris" + suffix, new Vector3(x, eyeY, irisDepth),
                    irisScale, Quaternion.identity, iris);
                var pupilScale = new Vector3(0.043f, 0.059f, 0.018f);
                var pupilDepth = LayerDepth(irisDepth + irisScale.z * 0.5f, pupilScale.z, 0.55f);
                Part(root, PrimitiveType.Sphere, "Pupil" + suffix, new Vector3(x, eyeY, pupilDepth),
                    pupilScale, Quaternion.identity, pupil);
                var highlightScale = new Vector3(0.026f, 0.034f, 0.014f);
                var highlightDepth = LayerDepth(pupilDepth + pupilScale.z * 0.5f, highlightScale.z, 0.52f);
                Part(root, PrimitiveType.Sphere, "EyeHighlight" + suffix,
                    new Vector3(x - side * 0.018f, eyeY + 0.041f, highlightDepth),
                    highlightScale, Quaternion.identity, highlight);
            }
        }

        private static void CreateFace(Transform root, Vector3 headScale, Material skin, Material cheek, Material smile)
        {
            var noseScale = new Vector3(.11f, .095f, .085f);
            var noseY = HeadCenterY - .075f;
            var noseDepth = FeatureDepth(headScale, 0f, noseY - HeadCenterY, noseScale.z, .62f);
            Part(root, PrimitiveType.Sphere, "Nose", new Vector3(0, noseY, noseDepth), noseScale,
                Quaternion.identity, skin);

            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * headScale.x * .31f;
                var y = HeadCenterY - .13f;
                var scale = new Vector3(.13f, .075f, .026f);
                var depth = FeatureDepth(headScale, x, y - HeadCenterY, scale.z, .42f);
                Part(root, PrimitiveType.Sphere, side < 0 ? "CheekLeft" : "CheekRight",
                    new Vector3(x, y, depth), scale, Quaternion.identity, cheek);
            }

            var smileY = HeadCenterY - .235f;
            var smileScale = new Vector3(.022f, .115f, .018f);
            var smileDepth = FeatureDepth(headScale, 0f, smileY - HeadCenterY, smileScale.z, .52f);
            Part(root, PrimitiveType.Capsule, "Smile", new Vector3(0, smileY, smileDepth), smileScale,
                Quaternion.Euler(0, 0, 90f), smile);
            foreach (var side in new[] { -1f, 1f })
                Part(root, PrimitiveType.Sphere, side < 0 ? "SmileCornerLeft" : "SmileCornerRight",
                    new Vector3(side * .105f, smileY + .018f, smileDepth), Vector3.one * .022f,
                    Quaternion.identity, smile);
        }

        private static void CreateEyebrows(Transform root, int eyebrowId, Material material, Vector3 headScale)
        {
            var index = Mathf.Clamp(eyebrowId, 1, 6) - 1;
            foreach (var side in new[] { -1f, 1f })
            {
                var suffix = side < 0 ? "Left" : "Right";
                var rotation = BrowRotations[index] * side;
                var curve = BrowCurves[index];
                var x = side * 0.23f;
                var y = HeadCenterY + 0.235f + Mathf.Abs(curve) * 0.10f;
                var browScale = new Vector3(0.042f, 0.15f + Mathf.Abs(curve), 0.035f);
                var depth = FeatureDepth(headScale, x, y - HeadCenterY, browScale.z, 0.55f);
                Part(root, PrimitiveType.Capsule, "Eyebrow" + suffix,
                    new Vector3(x, y, depth), browScale,
                    Quaternion.Euler(0, 0, 90f + rotation), material);
            }
        }

        private static float FeatureDepth(Vector3 headDiameter, float x, float y, float featureDiameter, float exposedFraction)
        {
            return LayerDepth(EllipsoidSurfaceDepth(headDiameter, x, y), featureDiameter, exposedFraction);
        }

        private static float LayerDepth(float surfaceDepth, float featureDiameter, float exposedFraction)
        {
            return surfaceDepth + featureDiameter * 0.5f * Mathf.Clamp01(exposedFraction);
        }

        private static float EllipsoidSurfaceDepth(Vector3 diameter, float x, float y)
        {
            var radiusX = diameter.x * 0.5f;
            var radiusY = diameter.y * 0.5f;
            var radiusZ = diameter.z * 0.5f;
            var normalizedDepth = 1f - x * x / (radiusX * radiusX) - y * y / (radiusY * radiusY);
            return radiusZ * Mathf.Sqrt(Mathf.Max(0f, normalizedDepth));
        }

        private static void CreateHair(Transform root, int hairStyleId, Material material, Vector3 headScale)
        {
            var hairRoot = new GameObject("HairRoot");
            hairRoot.transform.SetParent(root, false);
            hairStyleId = Mathf.Clamp(hairStyleId, 1, 12);

            AddHairCap(hairRoot.transform, material, headScale);
            switch (hairStyleId)
            {
                case 1: AddSweptQuiff(hairRoot.transform, material); break;
                case 2: AddFringe(hairRoot.transform, material, 5, 16f); break;
                case 3: AddSideLocks(hairRoot.transform, material, 0.32f); break;
                case 4: AddBob(hairRoot.transform, material); break;
                case 5: AddPonytail(hairRoot.transform, material); break;
                case 6: AddBraids(hairRoot.transform, material); break;
                case 7: AddCurls(hairRoot.transform, material); break;
                case 8: AddMohawk(hairRoot.transform, material); break;
                case 9: AddBun(hairRoot.transform, material); break;
                case 10: AddLongHair(hairRoot.transform, material); break;
                case 11: AddPixie(hairRoot.transform, material); break;
                case 12: AddTwinTails(hairRoot.transform, material); break;
            }
        }

        private static void AddSweptQuiff(Transform root, Material material)
        {
            for (var index = 0; index < 5; index++)
                Part(root, PrimitiveType.Capsule, "SweptQuiff", new Vector3(-.26f + index * .13f, 2.52f + index * .015f, .39f),
                    new Vector3(.085f, .22f - index * .012f, .075f), Quaternion.Euler(20f, 0, -34f + index * 8f), material);
        }

        private static void AddHairCap(Transform root, Material material, Vector3 headScale)
        {
            Part(root, PrimitiveType.Sphere, "HairCap", new Vector3(0, 2.39f, -0.05f),
                new Vector3(headScale.x * 1.04f, headScale.y * 0.64f, headScale.z * 1.06f),
                Quaternion.identity, material);
        }

        private static void AddFringe(Transform root, Material material, int count, float sweep)
        {
            for (var index = 0; index < count; index++)
            {
                var offset = index - (count - 1) * 0.5f;
                Part(root, PrimitiveType.Capsule, "Fringe", new Vector3(offset * 0.11f, 2.49f - Mathf.Abs(offset) * 0.025f, 0.48f),
                    new Vector3(0.075f, 0.18f, 0.07f), Quaternion.Euler(0, 0, 18f * offset + sweep), material);
            }
        }

        private static void AddSideLocks(Transform root, Material material, float length)
        {
            foreach (var side in new[] { -1f, 1f })
                Part(root, PrimitiveType.Capsule, "SideLock", new Vector3(side * 0.55f, 2.18f, -0.02f),
                    new Vector3(0.12f, length, 0.13f), Quaternion.Euler(0, 0, side * -5f), material);
        }

        private static void AddBob(Transform root, Material material)
        {
            AddSideLocks(root, material, 0.43f);
            for (var index = -2; index <= 2; index++)
                Part(root, PrimitiveType.Capsule, "BobBack", new Vector3(index * 0.18f, 2.10f, -0.43f),
                    new Vector3(0.13f, 0.34f, 0.13f), Quaternion.identity, material);
        }

        private static void AddPonytail(Transform root, Material material)
        {
            Part(root, PrimitiveType.Sphere, "PonytailTie", new Vector3(0, 2.40f, -0.58f), new Vector3(0.14f, 0.14f, 0.14f), Quaternion.identity, material);
            Part(root, PrimitiveType.Capsule, "Ponytail", new Vector3(0, 2.05f, -0.70f), new Vector3(0.20f, 0.43f, 0.19f), Quaternion.Euler(12f, 0, 0), material);
        }

        private static void AddBraids(Transform root, Material material)
        {
            foreach (var side in new[] { -1f, 1f })
            for (var index = 0; index < 4; index++)
                Part(root, PrimitiveType.Sphere, "Braid", new Vector3(side * 0.47f, 2.17f - index * 0.18f, -0.02f),
                    Vector3.one * (0.14f - index * 0.008f), Quaternion.identity, material);
        }

        private static void AddCurls(Transform root, Material material)
        {
            for (var index = 0; index < 10; index++)
            {
                var angle = index * Mathf.PI * 2f / 10f;
                Part(root, PrimitiveType.Sphere, "Curl", new Vector3(Mathf.Sin(angle) * 0.49f, 2.37f + Mathf.Cos(angle) * 0.26f, -0.03f),
                    Vector3.one * 0.19f, Quaternion.identity, material);
            }
        }

        private static void AddMohawk(Transform root, Material material)
        {
            for (var index = 0; index < 5; index++)
                Part(root, PrimitiveType.Capsule, "Mohawk", new Vector3(0, 2.66f, 0.32f - index * 0.17f),
                    new Vector3(0.09f, 0.23f, 0.09f), Quaternion.Euler(18f, 0, 0), material);
        }

        private static void AddBun(Transform root, Material material)
        {
            Part(root, PrimitiveType.Sphere, "Bun", new Vector3(0, 2.77f, -0.16f), new Vector3(0.31f, 0.28f, 0.30f), Quaternion.identity, material);
            AddFringe(root, material, 3, 8f);
        }

        private static void AddLongHair(Transform root, Material material)
        {
            AddSideLocks(root, material, 0.55f);
            for (var index = -2; index <= 2; index++)
                Part(root, PrimitiveType.Capsule, "LongBack", new Vector3(index * 0.19f, 1.86f, -0.43f),
                    new Vector3(0.15f, 0.60f, 0.14f), Quaternion.identity, material);
        }

        private static void AddPixie(Transform root, Material material)
        {
            for (var index = 0; index < 5; index++)
                Part(root, PrimitiveType.Capsule, "PixieTuft", new Vector3(-0.26f + index * 0.13f, 2.62f, 0.20f - Mathf.Abs(index - 2) * 0.06f),
                    new Vector3(0.065f, 0.18f, 0.06f), Quaternion.Euler(0, 0, -24f + index * 12f), material);
        }

        private static void AddTwinTails(Transform root, Material material)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                Part(root, PrimitiveType.Sphere, "TwinTailTie", new Vector3(side * 0.54f, 2.38f, -0.21f), Vector3.one * 0.13f, Quaternion.identity, material);
                Part(root, PrimitiveType.Capsule, "TwinTail", new Vector3(side * 0.66f, 1.99f, -0.24f),
                    new Vector3(0.18f, 0.45f, 0.17f), Quaternion.Euler(0, 0, side * -12f), material);
            }
        }

        private static void CreateSummoningMedallion(Transform root, Material gold)
        {
            var chain = new GameObject("NecklaceChain");
            chain.transform.SetParent(root, false);
            foreach (var side in new[] { -1f, 1f })
                Part(chain.transform, PrimitiveType.Cylinder, "ChainLink", new Vector3(side * 0.13f, 1.55f, 0.30f),
                    new Vector3(0.018f, 0.19f, 0.018f), Quaternion.Euler(0, 0, side * 38f), gold);
            Part(root, PrimitiveType.Cylinder, "SummoningMedallion", new Vector3(0, 1.40f, 0.34f),
                new Vector3(0.12f, 0.025f, 0.12f), Quaternion.Euler(90f, 0, 0), gold);
        }

        private static GameObject Part(Transform parent, PrimitiveType primitive, string name, Vector3 position,
            Vector3 scale, Quaternion rotation, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }
            return part;
        }

        private static Material MaterialFor(Color color, float metallic, float smoothness)
        {
            var color32 = (Color32)color;
            var key = (ulong)color32.r | ((ulong)color32.g << 8) | ((ulong)color32.b << 16) | ((ulong)color32.a << 24)
                | ((ulong)Mathf.RoundToInt(metallic * 255f) << 32) | ((ulong)Mathf.RoundToInt(smoothness * 255f) << 40);
            if (SharedMaterials.TryGetValue(key, out var material))
                return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { color = color, name = $"Avatar_{key:X}" };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            SharedMaterials[key] = material;
            return material;
        }

        private static void ClearVisuals(Transform root)
        {
            for (var index = root.childCount - 1; index >= 0; index--)
            {
                var child = root.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }
        }
    }
}
