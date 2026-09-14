using UnityEngine;
using UnityEngine.Rendering;

namespace Bigimong.AR
{
    public static class ProceduralCharacterGeometry
    {
        public static GameObject Primitive(Transform parent, PrimitiveType primitive, string name,
            Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(part);
            return part;
        }

        public static GameObject CreateConeMesh(Transform parent, string name, Vector3 position,
            Vector3 scale, Quaternion rotation, Material material, int segments = 10)
        {
            segments = Mathf.Max(4, segments);
            var vertices = new Vector3[segments + 2];
            vertices[0] = new Vector3(0f, .5f, 0f);
            vertices[1] = new Vector3(0f, -.5f, 0f);
            for (var index = 0; index < segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                vertices[index + 2] = new Vector3(Mathf.Cos(angle) * .5f, -.5f, Mathf.Sin(angle) * .5f);
            }

            var triangles = new int[segments * 6];
            for (var index = 0; index < segments; index++)
            {
                var next = (index + 1) % segments;
                var offset = index * 6;
                triangles[offset] = 0;
                triangles[offset + 1] = index + 2;
                triangles[offset + 2] = next + 2;
                triangles[offset + 3] = 1;
                triangles[offset + 4] = next + 2;
                triangles[offset + 5] = index + 2;
            }
            ReverseWinding(triangles);
            return MeshPart(parent, name, position, scale, rotation, material, vertices, triangles);
        }

        public static GameObject CreateWingMesh(Transform parent, string name, float side, Vector3 position,
            Vector3 scale, Quaternion rotation, Material material)
        {
            var direction = side < 0 ? -1f : 1f;
            var vertices = new[]
            {
                new Vector3(0f, .04f, 0f), new Vector3(direction, .08f, .18f),
                new Vector3(direction * .78f, -.04f, .92f), new Vector3(direction * .18f, -.1f, .62f),
                new Vector3(0f, -.04f, 0f), new Vector3(direction, 0f, .18f),
                new Vector3(direction * .78f, -.12f, .92f), new Vector3(direction * .18f, -.18f, .62f),
            };
            var triangles = new[]
            {
                0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6,
                0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2,
                2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0,
            };
            if (direction > 0f) ReverseWinding(triangles);
            return MeshPart(parent, name, position, scale, rotation, material, vertices, triangles);
        }

        private static void ReverseWinding(int[] triangles)
        {
            for (var index = 0; index < triangles.Length; index += 3)
                (triangles[index + 1], triangles[index + 2]) = (triangles[index + 2], triangles[index + 1]);
        }

        public static Material MaterialFor(string name, Color color, float smoothness = .48f, Color? emission = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission.Value);
            }
            return material;
        }

        private static GameObject MeshPart(Transform parent, string name, Vector3 position, Vector3 scale,
            Quaternion rotation, Material material, Vector3[] vertices, int[] triangles)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            var mesh = new Mesh { name = name + " Mesh", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            // Editor behavior checks inspect triangle winding before the APK build.
            mesh.UploadMeshData(!Application.isEditor);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            part.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            part.AddComponent<ProceduralGeneratedMeshOwner>().Own(mesh);
            return part;
        }

        private static void RemoveCollider(GameObject part)
        {
            var collider = part.GetComponent<Collider>();
            if (collider == null) return;
            collider.enabled = false;
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }
    }

    internal sealed class ProceduralGeneratedMeshOwner : MonoBehaviour
    {
        private Mesh mesh;

        public void Own(Mesh value) => mesh = value;

        private void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
            mesh = null;
        }
    }
}
