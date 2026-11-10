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
        Vector3 restScale;
        float shake;
        // Where the frame stands: over the core of the map in use (PR #21 fixed it to the screen's top right, and Stage 1's
        // camera puts its core elsewhere). Moved only when that spot changes on the screen or while a hit shakes it.
        public const float AboveCore = .45f; // world units over the core model's top
        Vector2 home;
        bool homeKnown, moved;
        Transform placedModel;
        Vector3 coreTop;

        public RectTransform BarRect => (RectTransform)transform.parent;
        public override Texture mainTexture => fillTexture ? fillTexture : base.mainTexture;

        void LateUpdate()
        {
            if (!Application.isPlaying || !battle || battle.Simulation == null) return;
            FollowCore();
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

        void FollowCore()
        {
            if (!homeKnown) { home = BarRect.anchoredPosition; homeKnown = true; }
            var model = battle.CoreModel;
            var camera = battle.BattleCamera;
            if (!model || !camera) return;
            if (model != placedModel) { placedModel = model; coreTop = Top(model); }
            var spot = Spot(camera, coreTop);
            if ((spot - home).sqrMagnitude > .25f) { home = spot; moved = true; }
        }
        // The frame's anchored position that puts its bottom middle over the core on the screen, kept on the screen.
        Vector2 Spot(Camera camera, Vector3 top)
        {
            var frame = BarRect;
            var parent = (RectTransform)frame.parent;
            Vector2 screen = camera.WorldToScreenPoint(top + Vector3.up * AboveCore);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var local);
            var area = parent.rect;
            var size = frame.rect.size;
            float halfWidth = size.x * .5f + 16;
            local.x = Mathf.Clamp(local.x, area.xMin + halfWidth, area.xMax - halfWidth);
            local.y = Mathf.Clamp(local.y, area.yMin + 16, area.yMax - size.y - 96); // under the top bar
            var pivot = local + new Vector2((frame.pivot.x - .5f) * size.x, frame.pivot.y * size.y);
            var anchor = new Vector2(Mathf.Lerp(area.xMin, area.xMax, frame.anchorMin.x), Mathf.Lerp(area.yMin, area.yMax, frame.anchorMin.y));
            return pivot - anchor;
        }
        // The top middle of the core model's meshes.
        static Vector3 Top(Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return model.position;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }
        // The editor places the frame for the scene's own camera and core (the workshop's), so the scene shows it where
        // the game does; the battle then follows the core of the stage it plays.
        public void Place(Camera camera, Transform model) => BarRect.anchoredPosition = Spot(camera, Top(model));

        void UpdateHitAnimation()
        {
            var frame = BarRect;
            if (trail.JustHit)
            {
                if (shake <= 0) restScale = frame.localScale;
                shake = ShakeSeconds;
            }
            if (shake <= 0)
            {
                if (moved) { frame.anchoredPosition = home; moved = false; }
                return;
            }
            shake = Mathf.Max(0, shake - Time.unscaledDeltaTime);
            float fade = shake / ShakeSeconds;
            frame.anchoredPosition = home + Vector2.right * (Mathf.Sin(shake * 70) * 9 * fade);
            frame.localScale = restScale * (1 + .14f * fade * fade);
            moved = true; // back to home once the shake ends
        }

        protected override void OnDisable()
        {
            if (shake > 0 && BarRect)
            {
                BarRect.anchoredPosition = home;
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
