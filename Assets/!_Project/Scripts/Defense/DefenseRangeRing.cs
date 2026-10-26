using System;
using UnityEngine;

namespace CodingGame.Defense
{
    public sealed class DefenseRangeRing : MonoBehaviour
    {
        [SerializeField] LineRenderer line;
        [SerializeField, Tooltip("Optional transparent vertex-colour material; tints the area inside the ring.")] Material fillMaterial;
        [SerializeField, Range(0, 1)] float fillAlpha = .18f;
        readonly Vector3[] points = new Vector3[97];
        Mesh fillMesh;
        MeshRenderer fillRenderer;
        float lineWidth, glow;
        void Awake()
        {
            if (!line || !line.sharedMaterial) throw new InvalidOperationException(name + ": 사거리 LineRenderer/Material 참조 누락");
            lineWidth = line.widthMultiplier;
            // Hidden until shown: the scene keeps the line on for editing (AttackPreviewWorld's ring showed at start).
            line.enabled = false;
            if (!fillMaterial) return;
            var fill = new GameObject("Fill", typeof(MeshFilter), typeof(MeshRenderer));
            fill.layer = gameObject.layer; fill.transform.SetParent(transform, false);
            fillMesh = new Mesh { name = "Range fill" }; fillMesh.MarkDynamic();
            fill.GetComponent<MeshFilter>().sharedMesh = fillMesh;
            fillRenderer = fill.GetComponent<MeshRenderer>(); fillRenderer.sharedMaterial = fillMaterial;
            fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; fillRenderer.receiveShadows = false; fillRenderer.enabled = false;
        }
        // glow 0..1 (tutorial highlight): a thick bright-red ring and a stronger rim; the caller pulses glow.
        public void Show(Vector3 center, float radius, Color color, float glow)
        {
            this.glow = glow;
            if (glow > 0) color = Color.Lerp(new Color(1, .12f, .12f), new Color(1, .6f, .55f), glow);
            line.widthMultiplier = lineWidth * (1 + 3 * glow);
            Show(center, radius, color);
            this.glow = 0;
        }
        public void Show(Vector3 center, float radius, Color color)
        {
            if (glow == 0) line.widthMultiplier = lineWidth;
            for (int i = 0; i < points.Length; i++)
            {
                float a = i * Mathf.PI * 2 / (points.Length - 1);
                points[i] = center + new Vector3(Mathf.Cos(a) * radius, .065f, Mathf.Sin(a) * radius);
            }
            Draw(color);
            Fill(center + Vector3.up * .06f, points.Length, color);
        }
        public void Hide() { line.enabled = false; line.widthMultiplier = lineWidth; if (fillRenderer) fillRenderer.enabled = false; }
        public void ShowSemicircle(Vector3 center, Vector3 forward, float radius, Color color)
        {
            forward.y = 0;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < points.Length - 1; i++)
            {
                float angle = -Mathf.PI * .5f + i * Mathf.PI / (points.Length - 2);
                points[i] = center + (forward * Mathf.Cos(angle) + right * Mathf.Sin(angle)) * radius;
            }
            points[points.Length - 1] = points[0];
            Draw(color);
            Fill(center, points.Length - 1, color);
        }
        void Draw(Color color)
        {
            line.useWorldSpace = true; line.positionCount = points.Length;
            line.SetPositions(points); line.startColor = line.endColor = color; line.enabled = true;
        }
        // Like a MOBA range indicator (planner reference): almost clear in the middle, glowing toward the rim. A fan from the
        // centre to an inner ring, then a band from the inner ring out to the outline points already computed (world space).
        const float Band = .78f;
        void Fill(Vector3 center, int count, Color color)
        {
            if (!fillRenderer) return;
            var vertices = new Vector3[1 + 2 * count]; var colors = new Color[vertices.Length]; var triangles = new int[(count - 1) * 9];
            Color Tint(float alpha) => new Color(color.r, color.g, color.b, alpha);
            float rim = Mathf.Lerp(fillAlpha, .6f, glow);
            vertices[0] = transform.InverseTransformPoint(center); colors[0] = Tint(rim * .12f);
            for (int i = 0; i < count; i++)
            {
                var edge = new Vector3(points[i].x, center.y, points[i].z);
                vertices[1 + i] = transform.InverseTransformPoint(Vector3.Lerp(center, edge, Band)); colors[1 + i] = Tint(rim * .25f);
                vertices[1 + count + i] = transform.InverseTransformPoint(edge); colors[1 + count + i] = Tint(rim);
                if (i == count - 1) break;
                int t = i * 9, inner = 1 + i, outer = 1 + count + i;
                triangles[t] = 0; triangles[t + 1] = inner + 1; triangles[t + 2] = inner;
                triangles[t + 3] = inner; triangles[t + 4] = inner + 1; triangles[t + 5] = outer + 1;
                triangles[t + 6] = inner; triangles[t + 7] = outer + 1; triangles[t + 8] = outer;
            }
            fillMesh.Clear(); fillMesh.vertices = vertices; fillMesh.colors = colors; fillMesh.triangles = triangles; fillMesh.RecalculateBounds();
            fillRenderer.enabled = true;
        }
        void OnDestroy() { if (fillMesh) Destroy(fillMesh); }
    }
}
