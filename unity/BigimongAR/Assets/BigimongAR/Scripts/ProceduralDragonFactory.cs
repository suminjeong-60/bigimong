using System.Collections.Generic;
using UnityEngine;

namespace Bigimong.AR
{
    /**
     * Creates rounded, readable 3D stand-ins until the final rigged prefabs arrive.
     * Catalog prefabs still win at the CharacterPrefabCatalog boundary.
     */
    public static class ProceduralDragonFactory
    {
        // These sets are intentionally species based. They are also used by the motion fallback.
        private static readonly HashSet<int> Winged = new() { 3, 13, 28 };
        private static readonly HashSet<int> Horned = new() { 2, 10, 18, 20 };
        private static readonly HashSet<int> Plated = new() { 4, 7, 15, 24 };
        private static readonly HashSet<int> LongNeck = new() { 5, 29, 30 };
        private static readonly HashSet<int> Aquatic = new() { 11 };
        private static readonly HashSet<int> Feathered = new() { 14, 17, 19, 25, 28 };
        private static readonly HashSet<int> Biped = new() { 1, 3, 8, 9, 10, 12, 13, 14, 16, 17, 19, 20, 21, 22, 23, 25, 26, 27, 28 };

        public static GameObject Create(int artId, string stage)
        {
            artId = Mathf.Clamp(artId, 1, 30);
            var maturity = stage == "ADULT" ? 1f : stage == "YOUTH" ? .65f : .35f;
            var appearance = BigimongAppearanceCatalog.Resolve(artId);
            var root = new GameObject($"Procedural_Bigimong_{artId:00}_{stage}");

            var bodyMaterial = ProceduralCharacterGeometry.MaterialFor($"Bigimong {artId:00} Body", appearance.bodyColor, .58f);
            var accentMaterial = ProceduralCharacterGeometry.MaterialFor($"Bigimong {artId:00} Accent", appearance.accentColor, .52f);
            var creamMaterial = ProceduralCharacterGeometry.MaterialFor($"Bigimong {artId:00} Cream", new Color(.96f, .84f, .65f), .58f);
            var eyeWhiteMaterial = ProceduralCharacterGeometry.MaterialFor("Bigimong Eye White", new Color(.99f, .98f, .94f), .72f);
            var irisMaterial = ProceduralCharacterGeometry.MaterialFor("Bigimong Iris", Color.Lerp(appearance.bodyColor, new Color(.19f, .08f, .035f), .78f), .72f);
            var pupilMaterial = ProceduralCharacterGeometry.MaterialFor("Bigimong Pupil", new Color(.025f, .018f, .015f), .68f);
            var highlightMaterial = ProceduralCharacterGeometry.MaterialFor("Bigimong Eye Highlight", Color.white, .88f, Color.white * .08f);
            var cosmeticMaterial = ProceduralCharacterGeometry.MaterialFor("Bigimong Cosmetic", new Color(.07f, .18f, .27f), .45f);

            var head = CreateCore(root.transform, appearance.bodyPlan, artId, maturity, bodyMaterial, creamMaterial);
            AddLayeredEyes(root.transform, head, appearance.bodyPlan, eyeWhiteMaterial, irisMaterial, pupilMaterial, highlightMaterial);
            AddLimbs(root.transform, artId, maturity, bodyMaterial, creamMaterial);
            AddTail(root.transform, appearance.bodyPlan, maturity, bodyMaterial, accentMaterial);
            AddDistinctiveAnatomy(root.transform, artId, head, appearance.bodyPlan, maturity, bodyMaterial, accentMaterial, creamMaterial);
            AddCosmetics(root.transform, artId, cosmeticMaterial, accentMaterial);

            root.AddComponent<ArBattleActor>().OwnProceduralMaterials(
                bodyMaterial, accentMaterial, creamMaterial, eyeWhiteMaterial, irisMaterial,
                pupilMaterial, highlightMaterial, cosmeticMaterial);
            return root;
        }

        private static Vector3 CreateCore(Transform root, BigimongBodyPlan plan, int artId, float maturity,
            Material body, Material cream)
        {
            var aquatic = plan == BigimongBodyPlan.Aquatic;
            var longNeck = plan == BigimongBodyPlan.LongNeck;
            var feathered = plan == BigimongBodyPlan.Feathered;
            var bodyScale = aquatic ? new Vector3(.78f, .44f, 1.22f) : longNeck
                ? new Vector3(.68f, .58f, .94f) : new Vector3(.74f, .62f, .96f);
            Part(root, PrimitiveType.Sphere, "Body", new Vector3(0, .68f, 0), bodyScale, Quaternion.identity, body);
            Part(root, PrimitiveType.Sphere, "Belly", new Vector3(0, .59f, .43f),
                new Vector3(bodyScale.x * .68f, bodyScale.y * .69f, bodyScale.z * .42f), Quaternion.identity, cream);

            if (longNeck)
                Part(root, PrimitiveType.Capsule, "LongNeck", new Vector3(0, 1.27f, .27f),
                    new Vector3(.25f, .69f + maturity * .14f, .25f), Quaternion.Euler(-8f, 0, 0), body);

            var head = longNeck ? new Vector3(0, 1.93f + maturity * .16f, .43f)
                : aquatic ? new Vector3(0, .87f, .82f) : new Vector3(0, 1.08f, .62f);
            var headScale = feathered ? new Vector3(.52f, .51f, .56f)
                : aquatic ? new Vector3(.54f, .40f, .69f) : new Vector3(.56f, .50f, .61f);
            Part(root, PrimitiveType.Sphere, "Head", head, headScale, Quaternion.identity, body);
            Part(root, PrimitiveType.Sphere, "Muzzle", head + new Vector3(0, -.10f, headScale.z * .38f),
                new Vector3(headScale.x * .78f, headScale.y * .48f, headScale.z * .55f), Quaternion.identity, cream);

            if (feathered && (artId == 17 || artId == 19))
                ProceduralCharacterGeometry.CreateConeMesh(root, "Beak", head + new Vector3(0, -.08f, .45f),
                    new Vector3(.24f, .42f, .22f), Quaternion.Euler(90f, 0, 0), cream, 12);
            return head;
        }

        private static void AddLayeredEyes(Transform root, Vector3 head, BigimongBodyPlan plan,
            Material eyeWhite, Material iris, Material pupil, Material highlight)
        {
            var headScale = plan == BigimongBodyPlan.Feathered ? new Vector3(.52f, .51f, .56f)
                : plan == BigimongBodyPlan.Aquatic ? new Vector3(.54f, .40f, .69f) : new Vector3(.56f, .50f, .61f);
            foreach (var side in new[] { -1f, 1f })
            {
                var suffix = side < 0 ? "L" : "R";
                var x = headScale.x * .29f * side;
                var y = .08f;
                var surface = EllipsoidSurfaceDepth(headScale, x, y);
                var eyePosition = head + new Vector3(x, y, surface + .022f);
                Part(root, PrimitiveType.Sphere, "EyeWhite" + suffix, eyePosition,
                    new Vector3(.19f, .205f, .105f), Quaternion.identity, eyeWhite);
                Part(root, PrimitiveType.Sphere, "Eye" + suffix, eyePosition + new Vector3(0, 0, .040f),
                    new Vector3(.112f, .133f, .055f), Quaternion.identity, iris);
                Part(root, PrimitiveType.Sphere, "Pupil" + suffix, eyePosition + new Vector3(0, -.004f, .065f),
                    new Vector3(.055f, .076f, .025f), Quaternion.identity, pupil);
                Part(root, PrimitiveType.Sphere, "EyeHighlight" + suffix,
                    eyePosition + new Vector3(-.021f * side, .041f, .078f),
                    new Vector3(.025f, .031f, .014f), Quaternion.identity, highlight);
            }
        }

        private static float EllipsoidSurfaceDepth(Vector3 diameter, float x, float y)
        {
            var radiusX = diameter.x * .5f;
            var radiusY = diameter.y * .5f;
            var radiusZ = diameter.z * .5f;
            var normalized = 1f - x * x / (radiusX * radiusX) - y * y / (radiusY * radiusY);
            return radiusZ * Mathf.Sqrt(Mathf.Max(0f, normalized));
        }

        private static void AddLimbs(Transform root, int artId, float maturity, Material body, Material cream)
        {
            if (Aquatic.Contains(artId))
            {
                foreach (var side in new[] { -1f, 1f })
                    ProceduralCharacterGeometry.CreateWingMesh(root, side < 0 ? "FinL" : "FinR", side,
                        new Vector3(side * .47f, .55f, .22f), new Vector3(.62f, .35f, .52f),
                        Quaternion.Euler(4f, side * 8f, side * -12f), body);
                return;
            }

            var biped = Biped.Contains(artId);
            var zPositions = biped ? new[] { -.24f } : new[] { -.34f, .34f };
            foreach (var z in zPositions)
            foreach (var side in new[] { -1f, 1f })
            {
                var suffix = side < 0 ? "L" : "R";
                var pair = z < 0 ? "Rear" : "Front";
                Part(root, PrimitiveType.Capsule, biped ? "Leg" + suffix : pair + "Leg" + suffix,
                    new Vector3(side * .40f, .29f, z), new Vector3(.17f + maturity * .025f, .30f, .17f),
                    Quaternion.identity, body);
                Part(root, PrimitiveType.Sphere, biped ? "Foot" + suffix : pair + "Foot" + suffix,
                    new Vector3(side * .40f, .06f, z + .09f), new Vector3(.19f, .10f, .26f), Quaternion.identity, cream);
            }

            if (biped)
            foreach (var side in new[] { -1f, 1f })
                Part(root, PrimitiveType.Capsule, side < 0 ? "ArmL" : "ArmR", new Vector3(side * .42f, .78f, .42f),
                    new Vector3(.10f, .22f, .10f), Quaternion.Euler(side * 17f, 0, side * -19f), body);
        }

        private static void AddTail(Transform root, BigimongBodyPlan plan, float maturity, Material body, Material accent)
        {
            var aquatic = plan == BigimongBodyPlan.Aquatic;
            Part(root, PrimitiveType.Capsule, "Tail", new Vector3(0, aquatic ? .62f : .69f, -.84f),
                aquatic ? new Vector3(.27f, .88f, .27f) : new Vector3(.22f, .68f + maturity * .22f, .22f),
                Quaternion.Euler(74f, 0, 0), body);
            if (aquatic)
                ProceduralCharacterGeometry.CreateWingMesh(root, "TailFin", 1f, new Vector3(0, .65f, -1.45f),
                    new Vector3(.45f, .36f, .42f), Quaternion.Euler(90f, 0, 0), accent);
        }

        private static void AddDistinctiveAnatomy(Transform root, int artId, Vector3 head, BigimongBodyPlan plan,
            float maturity, Material body, Material accent, Material cream)
        {
            if (Winged.Contains(artId)) AddWings(root, accent, maturity);
            if (Horned.Contains(artId)) AddHorns(root, head, accent, maturity);
            if (plan == BigimongBodyPlan.Ceratopsian) AddFrill(root, head, accent);
            if (Plated.Contains(artId)) AddArmorOrPlates(root, artId, accent, maturity);
            if (plan == BigimongBodyPlan.Spinosaur) AddSail(root, accent, maturity);
            if (Feathered.Contains(artId)) AddFeathers(root, head, accent, maturity);
            if (plan == BigimongBodyPlan.Hadrosaur) AddCrest(root, artId, head, accent, maturity);
            if (artId == 7 || artId == 15) AddTailClub(root, accent, maturity);
            if (artId == 25) AddTherizinosaurClaws(root, cream, maturity);
        }

        private static void AddWings(Transform root, Material material, float maturity)
        {
            foreach (var side in new[] { -1f, 1f })
                ProceduralCharacterGeometry.CreateWingMesh(root, side < 0 ? "WingL" : "WingR", side,
                    new Vector3(side * .40f, .91f, -.12f), new Vector3(.72f + maturity * .30f, .46f, .72f),
                    Quaternion.Euler(-10f, side * 5f, side * 12f), material);
        }

        private static void AddHorns(Transform root, Vector3 head, Material material, float maturity)
        {
            foreach (var side in new[] { -1f, 1f })
                ProceduralCharacterGeometry.CreateConeMesh(root, side < 0 ? "HornL" : "HornR",
                    head + new Vector3(side * .24f, .34f, .08f), new Vector3(.14f, .34f + maturity * .16f, .14f),
                    Quaternion.Euler(-24f, 0, side * 12f), material, 12);
        }

        private static void AddFrill(Transform root, Vector3 head, Material material) =>
            Part(root, PrimitiveType.Sphere, "Frill", head + new Vector3(0, .03f, -.24f),
                new Vector3(.88f, .78f, .13f), Quaternion.identity, material);

        private static void AddArmorOrPlates(Transform root, int artId, Material material, float maturity)
        {
            if (artId == 7 || artId == 15)
            {
                Part(root, PrimitiveType.Sphere, "ArmorShell", new Vector3(0, .82f, -.07f),
                    new Vector3(.76f, .34f, .86f), Quaternion.identity, material);
                for (var row = 0; row < 3; row++)
                for (var side = -1; side <= 1; side++)
                    Part(root, PrimitiveType.Sphere, "ArmorNodule", new Vector3(side * .25f, 1.01f, -.45f + row * .34f),
                        Vector3.one * (.07f + maturity * .018f), Quaternion.identity, material);
                return;
            }

            for (var index = 0; index < 6; index++)
                ProceduralCharacterGeometry.CreateConeMesh(root, "BackPlate", new Vector3(0, 1.02f, -.57f + index * .25f),
                    new Vector3(.20f, .31f + maturity * .14f, .09f), Quaternion.identity, material, 8);
        }

        private static void AddSail(Transform root, Material material, float maturity) =>
            ProceduralCharacterGeometry.CreateWingMesh(root, "Sail", 1f, new Vector3(0, 1.04f, -.36f),
                new Vector3(.58f + maturity * .16f, .74f, .74f), Quaternion.Euler(0, 0, 90f), material);

        private static void AddFeathers(Transform root, Vector3 head, Material material, float maturity)
        {
            for (var index = 0; index < 5; index++)
                Part(root, PrimitiveType.Capsule, "FeatherCrest", head + new Vector3(0, .34f + index * .035f, -.08f - index * .065f),
                    new Vector3(.055f, .15f + maturity * .055f, .055f), Quaternion.Euler(46f, 0, 0), material);
            foreach (var side in new[] { -1f, 1f })
                Part(root, PrimitiveType.Capsule, side < 0 ? "ArmFeatherL" : "ArmFeatherR",
                    new Vector3(side * .52f, .78f, .24f), new Vector3(.10f, .32f, .08f),
                    Quaternion.Euler(18f, 0, side * 38f), material);
        }

        private static void AddCrest(Transform root, int artId, Vector3 head, Material material, float maturity)
        {
            var backward = artId == 27 ? .36f : .14f;
            Part(root, PrimitiveType.Capsule, "HeadCrest", head + new Vector3(0, .33f, -.12f),
                new Vector3(.14f, .26f + maturity * backward, .14f), Quaternion.Euler(48f, 0, 0), material);
        }

        private static void AddTailClub(Transform root, Material material, float maturity) =>
            Part(root, PrimitiveType.Sphere, "TailClub", new Vector3(0, .68f, -1.44f),
                Vector3.one * (.25f + maturity * .08f), Quaternion.identity, material);

        private static void AddTherizinosaurClaws(Transform root, Material material, float maturity)
        {
            foreach (var side in new[] { -1f, 1f })
            for (var index = 0; index < 3; index++)
                ProceduralCharacterGeometry.CreateConeMesh(root, side < 0 ? "ClawL" : "ClawR",
                    new Vector3(side * (.45f + index * .035f), .59f, .54f + index * .025f),
                    new Vector3(.045f, .25f + maturity * .08f, .045f), Quaternion.Euler(76f, 0, side * 8f), material, 8);
        }

        private static void AddCosmetics(Transform root, int artId, Material material, Material accent)
        {
            var cosmetics = new GameObject("CosmeticRoot");
            cosmetics.transform.SetParent(root, false);
            if (artId != 2 && artId != 5 && artId != 29) return;
            Part(cosmetics.transform, PrimitiveType.Cube, "ExplorerPack", new Vector3(0, .83f, -.63f),
                new Vector3(.42f, .38f, .18f), Quaternion.Euler(8f, 0, 0), material);
            Part(cosmetics.transform, PrimitiveType.Cylinder, "PackRoll", new Vector3(0, 1.04f, -.68f),
                new Vector3(.12f, .25f, .12f), Quaternion.Euler(0, 0, 90f), accent);
        }

        private static GameObject Part(Transform root, PrimitiveType primitive, string name, Vector3 position,
            Vector3 scale, Quaternion rotation, Material material) =>
            ProceduralCharacterGeometry.Primitive(root, primitive, name, position, scale, rotation, material);
    }
}
