using System.Collections.Generic;
using UnityEngine;

namespace Bigimong.AR
{
    /**
     * Creates lightweight 3D stand-ins so the complete AR flow can be tested before
     * the final 90 rigged character prefabs are delivered. Catalog prefabs always win.
     */
    public static class ProceduralDragonFactory
    {
        private static readonly HashSet<int> Winged = new() { 3, 8, 10, 11, 12, 13, 20, 21, 28, 30 };
        private static readonly HashSet<int> Horned = new() { 2, 5, 7, 10, 15, 18, 20, 21, 22, 24 };
        private static readonly HashSet<int> Plated = new() { 4, 6, 7, 15, 22, 24 };
        private static readonly HashSet<int> LongNeck = new() { 5, 27, 29 };
        private static readonly HashSet<int> Aquatic = new() { 11 };
        private static readonly HashSet<int> Feathered = new() { 14, 17, 19, 25, 26, 28 };
        private static readonly HashSet<int> Biped = new() { 1, 3, 8, 9, 10, 12, 13, 14, 16, 17, 19, 20, 21, 25, 26, 28, 30 };

        public static GameObject Create(int artId, string stage)
        {
            artId = Mathf.Clamp(artId, 1, 30);
            var maturity = stage == "ADULT" ? 1f : stage == "YOUTH" ? 0.65f : 0.35f;
            var root = new GameObject($"Procedural_Bigimong_{artId:00}_{stage}");
            var baseColor = Color.HSVToRGB(Mathf.Repeat(artId * 0.113f, 1f), 0.58f, 0.95f);
            var accentColor = ElementSkillCatalog.Resolve(artId).secondaryColor;
            var bodyMaterial = MaterialFor(baseColor);
            var accentMaterial = MaterialFor(accentColor);
            var eyeMaterial = MaterialFor(new Color(0.055f, 0.04f, 0.03f));

            var longNeck = LongNeck.Contains(artId);
            var aquatic = Aquatic.Contains(artId);
            var feathered = Feathered.Contains(artId);
            var bodyScale = aquatic ? new Vector3(0.75f, 0.42f, 1.25f) : new Vector3(0.72f, 0.58f, 1.0f);
            Part(root.transform, PrimitiveType.Sphere, "Body", new Vector3(0, 0.62f, 0), bodyScale, Quaternion.identity, bodyMaterial);

            var neckHeight = longNeck ? 1.15f + maturity * 0.35f : 0.35f;
            if (longNeck)
                Part(root.transform, PrimitiveType.Capsule, "LongNeck", new Vector3(0, 1.0f, 0.34f), new Vector3(0.22f, neckHeight * 0.55f, 0.22f), Quaternion.Euler(-12, 0, 0), bodyMaterial);
            var headPosition = longNeck ? new Vector3(0, 1.55f + maturity * 0.25f, 0.52f) : new Vector3(0, 0.94f, 0.62f);
            Part(root.transform, PrimitiveType.Sphere, "Head", headPosition,
                feathered ? new Vector3(0.48f, 0.48f, 0.56f) : new Vector3(0.52f, 0.46f, 0.62f), Quaternion.identity, bodyMaterial);
            Part(root.transform, PrimitiveType.Sphere, "Muzzle", headPosition + new Vector3(0, -0.08f, 0.42f),
                new Vector3(0.42f, 0.26f, 0.48f), Quaternion.identity, accentMaterial);

            AddEye(root.transform, headPosition, -1f, eyeMaterial);
            AddEye(root.transform, headPosition, 1f, eyeMaterial);
            AddTail(root.transform, bodyMaterial, aquatic, maturity);
            if (aquatic) AddFins(root.transform, accentMaterial, maturity);
            else AddLegs(root.transform, bodyMaterial, Biped.Contains(artId), maturity);
            if (Winged.Contains(artId)) AddWings(root.transform, accentMaterial, maturity);
            if (Horned.Contains(artId)) AddHorns(root.transform, headPosition, accentMaterial, maturity);
            if (Plated.Contains(artId)) AddPlates(root.transform, accentMaterial, maturity);
            if (feathered) AddFeatherCrest(root.transform, headPosition, accentMaterial, maturity);

            root.AddComponent<ArBattleActor>().OwnProceduralMaterials(bodyMaterial, accentMaterial, eyeMaterial);
            return root;
        }

        private static void AddEye(Transform root, Vector3 head, float side, Material material)
        {
            Part(root, PrimitiveType.Sphere, side < 0 ? "EyeL" : "EyeR",
                head + new Vector3(0.34f * side, 0.1f, 0.34f), new Vector3(0.105f, 0.13f, 0.08f), Quaternion.identity, material);
        }

        private static void AddLegs(Transform root, Material material, bool biped, float maturity)
        {
            var zPositions = biped ? new[] { -0.18f } : new[] { -0.34f, 0.35f };
            foreach (var z in zPositions)
            foreach (var side in new[] { -1f, 1f })
            {
                Part(root, PrimitiveType.Capsule, "Leg", new Vector3(0.39f * side, 0.22f, z),
                    new Vector3(0.16f + maturity * 0.04f, 0.32f, 0.16f), Quaternion.identity, material);
            }
        }

        private static void AddTail(Transform root, Material material, bool aquatic, float maturity)
        {
            Part(root, PrimitiveType.Capsule, "Tail", new Vector3(0, 0.62f, -0.86f),
                aquatic ? new Vector3(0.24f, 0.8f, 0.24f) : new Vector3(0.2f, 0.7f + maturity * 0.25f, 0.2f),
                Quaternion.Euler(72, 0, 0), material);
        }

        private static void AddWings(Transform root, Material material, float maturity)
        {
            var width = 0.35f + maturity * 0.65f;
            foreach (var side in new[] { -1f, 1f })
            {
                Part(root, PrimitiveType.Cube, "Wing", new Vector3(side * (0.55f + width * 0.35f), 0.88f, -0.08f),
                    new Vector3(width, 0.055f, 0.55f + maturity * 0.3f), Quaternion.Euler(8, side * 8, side * 22), material);
            }
        }

        private static void AddHorns(Transform root, Vector3 head, Material material, float maturity)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                Part(root, PrimitiveType.Cylinder, "Horn", head + new Vector3(side * 0.24f, 0.35f, 0.06f),
                    new Vector3(0.07f, 0.14f + maturity * 0.13f, 0.07f), Quaternion.Euler(-28, 0, side * 12), material);
            }
        }

        private static void AddPlates(Transform root, Material material, float maturity)
        {
            for (var index = 0; index < 5; index++)
            {
                Part(root, PrimitiveType.Cube, "BackPlate", new Vector3(0, 1.0f, -0.55f + index * 0.28f),
                    new Vector3(0.08f, 0.18f + maturity * 0.18f, 0.18f), Quaternion.Euler(0, 0, 45), material);
            }
        }

        private static void AddFins(Transform root, Material material, float maturity)
        {
            foreach (var side in new[] { -1f, 1f })
                Part(root, PrimitiveType.Cube, "Fin", new Vector3(side * 0.7f, 0.54f, 0.05f),
                    new Vector3(0.52f + maturity * 0.18f, 0.06f, 0.28f), Quaternion.Euler(0, 0, side * 18), material);
        }

        private static void AddFeatherCrest(Transform root, Vector3 head, Material material, float maturity)
        {
            for (var index = 0; index < 4; index++)
                Part(root, PrimitiveType.Capsule, "Feather", head + new Vector3(0, 0.34f + index * 0.07f, -0.1f - index * 0.08f),
                    new Vector3(0.06f, 0.16f + maturity * 0.08f, 0.06f), Quaternion.Euler(55, 0, 0), material);
        }

        private static GameObject Part(Transform root, PrimitiveType primitive, string name, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
            }
            return part;
        }

        private static Material MaterialFor(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            material.SetFloat("_Smoothness", 0.42f);
            return material;
        }
    }
}
