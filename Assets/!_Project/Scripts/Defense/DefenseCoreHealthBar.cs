using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DefenseCoreHealthBar : MaskableGraphic
    {
        [SerializeField] DefenseBattle battle;
        [SerializeField] Texture2D fillTexture;

        const int PreviewHealth = 12;
        const float ShakeSeconds = .45f;
        static readonly Color EmptyColor = new Color(.18f, .28f, .37f);
        static readonly Color TrailColor = new Color(.63f, .8f, .91f);
        static readonly Color SeparatorShade = new Color(.67f, .78f, .74f);
        readonly DefenseHealthTrail trail = new DefenseHealthTrail();
        int maxHealth = PreviewHealth;
        int health = PreviewHealth;
        float trailHealth = PreviewHealth;
        Vector2 restPosition;
        Vector3 restScale;
        float shake;

        public RectTransform BarRect => (RectTransform)transform.parent;
        public override Texture mainTexture => fillTexture ? fillTexture : base.mainTexture;

        void LateUpdate()
        {
            if (!Application.isPlaying || !battle || battle.Simulation == null) return;
            var sim = battle.Simulation;
            int maximum = sim.Setup.BaseHealth;
            int current = Mathf.Clamp(sim.BaseHealth, 0, maximum);
            float fading = trail.Step((float)current / maximum, Time.unscaledDeltaTime) * maximum;
            UpdateHitAnimation();
            if (maxHealth == maximum && health == current && trailHealth == fading) return;
            maxHealth = maximum;
            health = current;
            trailHealth = fading;
            SetVerticesDirty();
        }

        void UpdateHitAnimation()
        {
            var frame = BarRect;
            if (trail.JustHit)
            {
                if (shake <= 0)
                {
                    restPosition = frame.anchoredPosition;
                    restScale = frame.localScale;
                }
                shake = ShakeSeconds;
            }
            if (shake <= 0) return;
            shake = Mathf.Max(0, shake - Time.unscaledDeltaTime);
            float fade = shake / ShakeSeconds;
            frame.anchoredPosition = restPosition + Vector2.right * (Mathf.Sin(shake * 70) * 9 * fade);
            frame.localScale = restScale * (1 + .14f * fade * fade);
        }

        protected override void OnDisable()
        {
            if (shake > 0 && BarRect)
            {
                BarRect.anchoredPosition = restPosition;
                BarRect.localScale = restScale;
                shake = 0;
            }
            base.OnDisable();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!fillTexture) return;

            var rect = GetPixelAdjustedRect();
            int count = Application.isPlaying ? maxHealth : PreviewHealth;
            int filled = Application.isPlaying ? health : PreviewHealth;
            // Geometry and UVs share the frame's coordinates, so the end caps fit the rim.
            const float left = .018f, right = .983f, halfSeparator = .08f;
            Color previousTint = default;
            float previousEnd = left;
            for (int i = 0; i < count; i++)
            {
                float start = Mathf.Lerp(left, right, (i + (i == 0 ? 0 : halfSeparator)) / count);
                float end = Mathf.Lerp(left, right, (i + 1 - (i == count - 1 ? 0 : halfSeparator)) / count);
                Color tint = i < filled ? color : color * Color.Lerp(EmptyColor, TrailColor, Mathf.Clamp01(trailHealth - i));

                // Shade the same fill across each seam, with no gap exposing the navy frame.
                if (i > 0)
                {
                    float middle = (previousEnd + start) * .5f;
                    Color seam = Color.Lerp(previousTint, tint, .5f) * SeparatorShade;
                    AddStrip(mesh, rect, previousEnd, middle, previousTint, seam);
                    AddStrip(mesh, rect, middle, start, seam, tint);
                }
                AddStrip(mesh, rect, start, end, tint, tint);
                previousTint = tint;
                previousEnd = end;
            }
        }

        static void AddStrip(VertexHelper mesh, Rect rect, float start, float end, Color startTint, Color endTint)
        {
            float xMin = Mathf.Lerp(rect.xMin, rect.xMax, start);
            float xMax = Mathf.Lerp(rect.xMin, rect.xMax, end);
            int vertex = mesh.currentVertCount;
            mesh.AddVert(new Vector3(xMin, rect.yMin), startTint, new Vector2(start, 0));
            mesh.AddVert(new Vector3(xMin, rect.yMax), startTint, new Vector2(start, 1));
            mesh.AddVert(new Vector3(xMax, rect.yMax), endTint, new Vector2(end, 1));
            mesh.AddVert(new Vector3(xMax, rect.yMin), endTint, new Vector2(end, 0));
            mesh.AddTriangle(vertex, vertex + 1, vertex + 2);
            mesh.AddTriangle(vertex + 2, vertex + 3, vertex);
        }
    }
}
