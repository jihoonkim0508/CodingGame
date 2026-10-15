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
            if (!fillMaterial) return;
            var fill = new GameObject("Fill", typeof(MeshFilter), typeof(MeshRenderer));
            fill.layer = gameObject.layer; fill.transform.SetParent(transform, false);
            fillMesh = new Mesh { name = "Range fill" }; fillMesh.MarkDynamic();
            fill.GetComponent<MeshFilter>().sharedMesh = fillMesh;
            fillRenderer = fill.GetComponent<MeshRenderer>(); fillRenderer.sharedMaterial = fillMaterial;
            fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; fillRenderer.receiveShadows = false; fillRenderer.enabled = false;
        }
        // glow 0..1 (tutorial highlight): a thick bright-gold ring and a strong fill; the caller pulses glow.
        public void Show(Vector3 center, float radius, Color color, float glow)
        {
            this.glow = glow;
            if (glow > 0) color = Color.Lerp(new Color(1, .78f, .12f), new Color(1, .95f, .55f), glow);
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
        // Triangle fan from the centre to the outline points already computed (world space).
        void Fill(Vector3 center, int count, Color color)
        {
            if (!fillRenderer) return;
            var vertices = new Vector3[count + 1]; var colors = new Color[count + 1]; var triangles = new int[(count - 1) * 3];
            var inside = new Color(color.r, color.g, color.b, Mathf.Lerp(fillAlpha, .55f, glow));
            vertices[0] = transform.InverseTransformPoint(center); colors[0] = inside;
            for (int i = 0; i < count; i++)
            {
                vertices[i + 1] = transform.InverseTransformPoint(new Vector3(points[i].x, center.y, points[i].z)); colors[i + 1] = inside;
                if (i < count - 1) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 2; triangles[i * 3 + 2] = i + 1; }
            }
            fillMesh.Clear(); fillMesh.vertices = vertices; fillMesh.colors = colors; fillMesh.triangles = triangles; fillMesh.RecalculateBounds();
            fillRenderer.enabled = true;
        }
        void OnDestroy() { if (fillMesh) Destroy(fillMesh); }
    }
}
