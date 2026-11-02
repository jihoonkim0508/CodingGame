using UnityEngine;
using UnityEngine.Rendering;

namespace CodingGame.StageMap
{
    // Like the Defense range/boss rings: a local mesh, emissive outline and unscaled pulse.
    // Kept separate from the island art so effects never enlarge camera framing or click bounds.
    [DisallowMultipleComponent]
    public sealed class StageLockVisual : MonoBehaviour
    {
        Material barrierMaterial, lineMaterial;
        Mesh wallMesh;
        LineRenderer lowerRing, upperRing;
        float rejectedAt = -10f;
        int stageNumber;
        static readonly Color Amber = new Color(1f, .27f, .09f);
        public bool IsLocked { get; private set; }
        public bool IsReleasing { get; private set; }
        public float ReleaseProgress { get; private set; }
        public float Rejection => Mathf.Clamp01(1f - (Time.unscaledTime - rejectedAt) / .65f);

        public void Configure(Bounds bounds, int number)
        {
            stageNumber = number;
            var center = transform.InverseTransformPoint(bounds.center);
            var size = bounds.size;
            float x = size.x * .52f, z = size.z * .52f;
            float bottom = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)).y + size.y * .25f;
            float height = Mathf.Max(1.5f, size.y * .95f);
            var shader = Resources.Load<Shader>("ArtScripts/StageSelection/Shaders/StageLockBarrier");
            if (!shader) throw new System.InvalidOperationException("스테이지 잠금 장벽 셰이더가 없습니다.");
            barrierMaterial = new Material(shader) { name = "Stage lock hologram" };
            barrierMaterial.SetColor("_BaseColor", Amber);
            lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Stage lock outline" };
            const int sides = 8;
            var vertices = new Vector3[(sides + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[sides * 6];
            var lower = new Vector3[sides];
            var upper = new Vector3[sides];
            for (int i = 0; i <= sides; i++)
            {
                float angle = Mathf.PI * 2 * i / sides + Mathf.PI / 8;
                var p = new Vector3(center.x + Mathf.Cos(angle) * x, bottom, center.z + Mathf.Sin(angle) * z);
                vertices[i * 2] = p; vertices[i * 2 + 1] = p + Vector3.up * height;
                uv[i * 2] = new Vector2(i / (float)sides, 0); uv[i * 2 + 1] = new Vector2(i / (float)sides, 1);
                if (i == sides) continue;
                lower[i] = p; upper[i] = p + Vector3.up * height;
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            wallMesh = new Mesh { name = "Octagonal stage quarantine" };
            wallMesh.vertices = vertices; wallMesh.uv = uv; wallMesh.triangles = triangles; wallMesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = wallMesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = barrierMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            lowerRing = Ring("Sealed perimeter", lower, .045f);
            upperRing = Ring("Hologram rim", upper, .022f);
        }

        LineRenderer Ring(string label, Vector3[] points, float width)
        {
            var go = new GameObject(label); go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial; line.useWorldSpace = false; line.loop = true;
            line.positionCount = points.Length; line.SetPositions(points); line.widthMultiplier = width;
            line.startColor = line.endColor = Amber;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }

        public void SetLocked(bool locked)
        {
            IsReleasing = false; ReleaseProgress = 0;
            IsLocked = locked;
            if(barrierMaterial) barrierMaterial.SetFloat("_Release",0);
            gameObject.SetActive(locked);
        }

        public void Release(float progress)
        {
            IsReleasing = true; ReleaseProgress = Mathf.Clamp01(progress);
            barrierMaterial.SetFloat("_Release",ReleaseProgress);
        }

        public void Reject() => rejectedAt = Time.unscaledTime;

        void Update()
        {
            float pulse = 1f + .12f * Mathf.Sin(Time.unscaledTime * 2 + stageNumber);
            Color color = Color.Lerp(Amber, new Color(1, .5f, .24f), Rejection);
            color = Color.Lerp(color,new Color(1,.77f,.38f),ReleaseProgress);
            color.a = 1-ReleaseProgress;
            lowerRing.startColor = lowerRing.endColor = color * (pulse + Rejection);
            upperRing.startColor = upperRing.endColor = new Color(color.r, color.g, color.b, (.65f + .35f * Rejection)*(1-ReleaseProgress));
            barrierMaterial.SetFloat("_Pulse", pulse);
            barrierMaterial.SetFloat("_Reject", Rejection);
        }

        void OnDestroy()
        {
            if (wallMesh) Destroy(wallMesh);
            if (barrierMaterial) Destroy(barrierMaterial);
            if (lineMaterial) Destroy(lineMaterial);
        }
    }
}
