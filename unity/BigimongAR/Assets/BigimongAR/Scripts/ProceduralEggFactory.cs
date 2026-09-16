using UnityEngine;

namespace Bigimong.AR
{
    public static class ProceduralEggFactory
    {
        private static readonly Color[] Colors =
        {
            new Color(.20f, .78f, .75f), new Color(.98f, .73f, .32f),
            new Color(.94f, .42f, .55f), new Color(.63f, .50f, .86f), new Color(.30f, .16f, .09f),
        };
        private static readonly Material[] Materials = new Material[5];
        // Latitude/longitude in degrees; fixed instead of consuming gameplay randomness.
        private static readonly Vector2[] PatchAngles =
        {
            new Vector2(52, 15), new Vector2(52, 135), new Vector2(52, 255),
            new Vector2(12, 0), new Vector2(12, 90), new Vector2(12, 180), new Vector2(12, 270),
            new Vector2(-30, 35), new Vector2(-30, 110), new Vector2(-30, 185), new Vector2(-30, 260), new Vector2(-58, 320),
        };

        public static GameObject Create()
        {
            var root = new GameObject("Procedural_Bigimong_Egg");
            // Unity's primitive sphere has radius .5; scales are the authored egg diameters.
            Part(root.transform, PrimitiveType.Sphere, "EggShell", Vector3.zero, new Vector3(.34f, .50f, .34f), Quaternion.identity, 0);
            for (var index = 0; index < 12; index++) CreateSurfacePatch(root.transform, index);
            CreateNestAndShadow(root.transform);
            return root;
        }

        private static void CreateSurfacePatch(Transform root, int index)
        {
            var latitude = PatchAngles[index].x * Mathf.Deg2Rad;
            var longitude = PatchAngles[index].y * Mathf.Deg2Rad;
            var unit = new Vector3(Mathf.Cos(latitude) * Mathf.Sin(longitude), Mathf.Sin(latitude), Mathf.Cos(latitude) * Mathf.Cos(longitude));
            var surface = Vector3.Scale(unit, new Vector3(.17f, .25f, .17f));
            var normal = new Vector3(unit.x / .17f, unit.y / .25f, unit.z / .17f).normalized;
            Part(root, PrimitiveType.Sphere, "EggPatch" + index.ToString("00"), surface + normal * .004f,
                new Vector3(.075f, .058f, .018f), Quaternion.LookRotation(normal), index % 4);
        }

        private static void CreateNestAndShadow(Transform root)
        {
            Part(root, PrimitiveType.Cylinder, "Nest", new Vector3(0, -.48f, 0), new Vector3(.46f, .025f, .46f), Quaternion.identity, 4);
            Part(root, PrimitiveType.Quad, "Shadow", new Vector3(0, -.51f, 0), new Vector3(.52f, .52f, 1), Quaternion.Euler(90, 0, 0), 4);
        }

        private static void Part(Transform root, PrimitiveType type, string name, Vector3 position, Vector3 scale, Quaternion rotation, int materialIndex)
        {
            if (Materials[materialIndex] == null)
                Materials[materialIndex] = ProceduralCharacterGeometry.MaterialFor("Egg Palette " + materialIndex, Colors[materialIndex], .48f);
            ProceduralCharacterGeometry.Primitive(root, type, name, position, scale, rotation, Materials[materialIndex]);
        }
    }
}
