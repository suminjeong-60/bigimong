using UnityEngine;
using UnityEngine.UI;

namespace Bigimong.AR
{
    public sealed class CircularButtonGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            var rect = GetPixelAdjustedRect();
            var center = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) * .5f;
            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = center;
            helper.AddVert(vertex);
            const int segments = 40;
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                vertex.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                helper.AddVert(vertex);
                if (i > 0) helper.AddTriangle(0, i, i + 1);
            }
        }
    }
}
