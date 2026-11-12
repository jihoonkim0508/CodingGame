using UnityEngine;
using UnityEngine.Rendering;

namespace CodingGame.StageMap
{
    // Like the Defense range/boss rings: a local mesh, emissive outline and unscaled pulse.
    // Kept separate from the island art so effects never enlarge camera framing or click bounds.
    [DisallowMultipleComponent]
    public sealed class StageLockVisual : MonoBehaviour
    {
        // Built once in the editor and saved (Tools/Optimize/Stage Visuals: the mesh and materials as assets); the barrier's
        // pulse, rejection and release go through a property block, so its saved material is never changed.
        [SerializeField, HideInInspector] Material barrierMaterial, lineMaterial;
        [SerializeField, HideInInspector] Mesh wallMesh;
        [SerializeField, HideInInspector] LineRenderer lowerRing, upperRing;
        [SerializeField, HideInInspector] MeshRenderer wall;
        [SerializeField, HideInInspector] bool baked;
        public bool Baked => baked;
        MaterialPropertyBlock propertyBlock;
        MaterialPropertyBlock block => propertyBlock ??= new MaterialPropertyBlock();
        static readonly int ReleaseId = Shader.PropertyToID("_Release"), PulseId = Shader.PropertyToID("_Pulse"), RejectId = Shader.PropertyToID("_Reject");
        float rejectedAt = -10f;
        int stageNumber;
        Camera presentationCamera;
        static readonly Color Amber = new Color(1f, .64f, .19f);
        // The barrier's box while it shows (the lock badge centres on the island and its barrier together).
        public Bounds? WallBounds => wall && wall.enabled && wall.gameObject.activeInHierarchy ? wall.bounds : (Bounds?)null;
        public bool IsLocked { get; private set; }
        public bool IsReleasing { get; private set; }
        public float ReleaseProgress { get; private set; }
        public float Rejection => Mathf.Clamp01(1f - (GameClock.Now - rejectedAt) / .65f);

        public void Configure(Bounds bounds, int number)
        {
            stageNumber = number;
            presentationCamera=Camera.main;
            if (wall) return;
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
            const int sides = 64;
            var vertices = new Vector3[(sides + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[sides * 6];
            var lower = new Vector3[sides];
            var upper = new Vector3[sides];
            for (int i = 0; i <= sides; i++)
            {
                float angle = Mathf.PI * 2 * (i % sides) / sides;
                float cosine=Mathf.Cos(angle),sine=Mathf.Sin(angle);
                // A rounded rectangular contour fits the island without eight hard corners.
                var p = new Vector3(center.x + Mathf.Sign(cosine)*Mathf.Sqrt(Mathf.Abs(cosine))*x, bottom,
                    center.z + Mathf.Sign(sine)*Mathf.Sqrt(Mathf.Abs(sine))*z);
                vertices[i * 2] = p; vertices[i * 2 + 1] = p + Vector3.up * height;
                uv[i * 2] = new Vector2(i / (float)sides, 0); uv[i * 2 + 1] = new Vector2(i / (float)sides, 1);
                if (i == sides) continue;
                lower[i] = p; upper[i] = p + Vector3.up * height;
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            wallMesh = new Mesh { name = "Rounded stage quarantine" };
            wallMesh.vertices = vertices; wallMesh.uv = uv; wallMesh.triangles = triangles; wallMesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = wallMesh;
            var renderer = wall = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = barrierMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            lowerRing = Ring("Sealed perimeter", lower, .065f);
            upperRing = Ring("Hologram rim", upper, .055f);
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
            if (wall) { block.SetFloat(ReleaseId, 0); wall.SetPropertyBlock(block); }
            gameObject.SetActive(locked);
        }

        public void Release(float progress)
        {
            IsReleasing = true; ReleaseProgress = Mathf.Clamp01(progress);
            block.SetFloat(ReleaseId, ReleaseProgress); wall.SetPropertyBlock(block);
        }

        public void Reject() => rejectedAt = GameClock.Now;

        void Update()
        {
            float pulse = 1f + .12f * Mathf.Sin(GameClock.Now * 2 + stageNumber);
            Color color = Color.Lerp(Amber, new Color(1, .5f, .24f), Rejection);
            color = Color.Lerp(color,new Color(1,.77f,.38f),ReleaseProgress);
            color.a = 1-ReleaseProgress;
            lowerRing.startColor = lowerRing.endColor = color * (pulse + Rejection);
            upperRing.startColor = upperRing.endColor = new Color(color.r, color.g, color.b, (.65f + .35f * Rejection)*(1-ReleaseProgress));
            block.SetFloat(PulseId, pulse); block.SetFloat(RejectId, Rejection); wall.SetPropertyBlock(block);
            if(presentationCamera)
            {
                float depth=Vector3.Dot(upperRing.bounds.center-presentationCamera.transform.position,presentationCamera.transform.forward);
                float perPixel=2*Mathf.Max(.1f,depth)*Mathf.Tan(presentationCamera.fieldOfView*Mathf.Deg2Rad*.5f)/Mathf.Max(1,presentationCamera.pixelHeight);
                upperRing.widthMultiplier=Mathf.Max(.055f,perPixel*1.5f);
                lowerRing.widthMultiplier=Mathf.Max(.065f,perPixel*1.8f);
            }
        }

#if UNITY_EDITOR
        public void MarkBaked() => baked = true;
#endif
        void OnDestroy()
        {
            if (baked) return; // saved assets
            if (wallMesh) Destroy(wallMesh);
            if (barrierMaterial) Destroy(barrierMaterial);
            if (lineMaterial) Destroy(lineMaterial);
        }
    }
}
