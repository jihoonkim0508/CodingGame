using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    // The boss's entrance warning (12차 120), shown over the map while the camera holds on the boss. Five drafts to choose
    // from; Style picks the one the game plays. Built at run time on its own overlay canvas, under the tutorial's (80).
    public sealed class DefenseBossWarning : MonoBehaviour
    {
        public static readonly string[] Drafts = { "hazard_bands", "glitch_title", "terminal_window", "boss_reticle", "comic_burst" };
        // ponytail: draft 0 until the team picks one; then delete the other four builders.
        public static int Style = 0;
        public const float Seconds = 2.9f;

        static readonly Color Red = new Color(1, .18f, .22f), Yellow = new Color(1, .82f, .18f), Ink = new Color(.06f, .07f, .1f);
        RectTransform root;
        Action<float> animate;
        float startedAt = -1;
        TMP_FontAsset font;

        public void Play(int style)
        {
            if (!root) Build();
            foreach (Transform child in root) Destroy(child.gameObject);
            animate = style switch { 1 => Glitch(), 2 => Terminal(), 3 => Reticle(), 4 => Burst(), _ => Bands() };
            startedAt = Time.unscaledTime;
            root.gameObject.SetActive(true);
            animate(0);
        }

        public void Hide() { if (root) root.gameObject.SetActive(false); startedAt = -1; }

        void Update()
        {
            if (startedAt < 0) return;
            float t = Time.unscaledTime - startedAt;
            if (t >= Seconds) { Hide(); return; }
            animate(t);
            // Everything fades out over the last .3 s.
            root.GetComponent<CanvasGroup>().alpha = Mathf.Clamp01((Seconds - t) / .3f);
        }

        void Build()
        {
            var canvasObject = new GameObject("BossWarningCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 70;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var group = canvasObject.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
            root = (RectTransform)canvasObject.transform;
            font = UITheme.Display;
            root.gameObject.SetActive(false);
        }

        // ---- the five drafts ----

        // 0: yellow-black hazard bands slide in top and bottom; a red band across the middle reads WARNING, blinking.
        Action<float> Bands()
        {
            var top = Stripes("TopBand", new Vector2(0, 1), 96); var bottom = Stripes("BottomBand", new Vector2(0, 0), 96);
            var band = Box("MiddleBand", new Color(.55f, .02f, .05f, .82f), Vector2.zero, new Vector2(1920, 190));
            var title = Label("Warning", band, "WARNING", 104, Yellow, new Vector2(0, 22));
            Label("Sub", band, "거대한 오류 코드 출현!", 40, Color.white, new Vector2(0, -52));
            return t =>
            {
                float slide = Ease(t / .3f);
                top.rect.anchoredPosition = new Vector2(0, Mathf.Lerp(96, 0, slide)); bottom.rect.anchoredPosition = new Vector2(0, Mathf.Lerp(-96, 0, slide));
                top.raw.uvRect = bottom.raw.uvRect = new Rect(t * 1.5f, 0, 1920f / 64, 96f / 64);
                band.localScale = new Vector3(1, Ease(t / .25f), 1);
                title.alpha = .55f + .45f * Mathf.Abs(Mathf.Cos(t * Mathf.PI * 2.5f));
            };
        }

        // 1: the screen darkens; BOSS splits into red and cyan copies that jitter, with scan lines running down.
        Action<float> Glitch()
        {
            var shade = Box("Shade", new Color(0, 0, 0, .55f), Vector2.zero, new Vector2(1920, 1080));
            var cyan = Label("Cyan", shade, "BOSS", 240, new Color(.2f, 1, 1, .8f), Vector2.zero);
            var red = Label("Red", shade, "BOSS", 240, new Color(1, .1f, .25f, .8f), Vector2.zero);
            var white = Label("White", shade, "BOSS", 240, Color.white, Vector2.zero);
            Label("Sub", shade, "ERROR_CODE.exe 가 깨어났어", 42, new Color(1, .55f, .6f), new Vector2(0, -170));
            var lines = new RectTransform[6];
            for (int i = 0; i < lines.Length; i++) lines[i] = Box("Scan" + i, new Color(1, 1, 1, .06f), Vector2.zero, new Vector2(1920, 10), shade);
            var noise = new System.Random(7);
            return t =>
            {
                bool burst = (int)(t * 12) % 5 == 0;
                float j = burst ? 14 : 4;
                cyan.rectTransform.anchoredPosition = new Vector2(-j + (float)noise.NextDouble() * 3, (float)noise.NextDouble() * 4 - 2);
                red.rectTransform.anchoredPosition = new Vector2(j - (float)noise.NextDouble() * 3, (float)noise.NextDouble() * 4 - 2);
                white.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1, Ease(t / .2f));
                for (int i = 0; i < lines.Length; i++) lines[i].anchoredPosition = new Vector2(0, 540 - Mathf.Repeat(t * 700 + i * 190, 1080));
            };
        }

        // 2: a red system window pops up and types its report line by line.
        Action<float> Terminal()
        {
            var window = Box("Window", new Color(.05f, .06f, .09f, .95f), new Vector2(0, 40), new Vector2(980, 380));
            var bar = Box("TitleBar", Red, new Vector2(0, 165), new Vector2(980, 50), window);
            Label("Title", bar, "⚠ SYSTEM ALERT", 30, Color.white, new Vector2(-300, 0)).alignment = TextAlignmentOptions.Left;
            var body = Label("Body", window, "", 38, new Color(1, .45f, .45f), new Vector2(0, -25));
            body.rectTransform.sizeDelta = new Vector2(880, 280); body.alignment = TextAlignmentOptions.TopLeft;
            const string report = "> 거대한 오류 코드 감지\n> 체력: ████████  보통의 4배\n> 목표: 코어\n> 모든 로봇, 전투 준비!";
            return t =>
            {
                window.localScale = Vector3.one * Mathf.Lerp(.6f, 1, Ease(t / .18f));
                int shown = Mathf.Clamp(Mathf.FloorToInt((t - .15f) * 38), 0, report.Length);
                body.text = report.Substring(0, shown) + ((int)(t * 4) % 2 == 0 ? "_" : "");
            };
        }

        // 3: the camera holds the boss in the middle; red edges close in and a reticle locks onto it.
        Action<float> Reticle()
        {
            var edges = new[] {
                Box("EdgeTop", new Color(.8f, .02f, .05f, .45f), new Vector2(0, 500), new Vector2(1920, 80)),
                Box("EdgeBottom", new Color(.8f, .02f, .05f, .45f), new Vector2(0, -500), new Vector2(1920, 80)),
                Box("EdgeLeft", new Color(.8f, .02f, .05f, .35f), new Vector2(-930, 0), new Vector2(60, 1080)),
                Box("EdgeRight", new Color(.8f, .02f, .05f, .35f), new Vector2(930, 0), new Vector2(60, 1080)) };
            var outer = Ring("Outer", 360, 10); var inner = Ring("Inner", 230, 6);
            var pill = Box("Tag", Red, new Vector2(0, 230), new Vector2(240, 64));
            Label("BossTag", pill, "BOSS", 44, Color.white, Vector2.zero);
            var caption = Box("Caption", new Color(.05f, .06f, .09f, .85f), new Vector2(0, 420), new Vector2(760, 76));
            Label("CaptionText", caption, "거대한 오류 코드를 조준했어!", 38, Yellow, Vector2.zero);
            return t =>
            {
                float lockOn = Ease(t / .45f);
                outer.localScale = Vector3.one * Mathf.Lerp(2.2f, 1, lockOn); outer.localEulerAngles = new Vector3(0, 0, t * 60);
                inner.localScale = Vector3.one * Mathf.Lerp(.3f, 1, lockOn); inner.localEulerAngles = new Vector3(0, 0, -t * 90);
                foreach (var edge in edges) edge.GetComponent<Image>().color = new Color(.8f, .02f, .05f, .3f + .2f * Mathf.Abs(Mathf.Sin(t * 6)));
                pill.localScale = Vector3.one * (t < .45f ? 0 : 1 + .08f * Mathf.Sin(t * 10));
            };
        }

        // 4: a comic burst slams in with 디버 shouting from the corner.
        Action<float> Burst()
        {
            var star = new GameObject("Burst", typeof(RectTransform), typeof(StarBurst)).GetComponent<StarBurst>();
            star.rectTransform.SetParent(root, false); star.rectTransform.sizeDelta = new Vector2(980, 560); star.color = Yellow;
            var back = new GameObject("BurstInk", typeof(RectTransform), typeof(StarBurst)).GetComponent<StarBurst>();
            back.rectTransform.SetParent(root, false); back.rectTransform.sizeDelta = new Vector2(1040, 610); back.color = Ink; back.transform.SetSiblingIndex(0);
            var title = Label("Title", star.rectTransform, "보스 등장!!", 120, Ink, new Vector2(0, 10));
            var guide = new GameObject("Guide", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            guide.rectTransform.SetParent(root, false); guide.rectTransform.anchoredPosition = new Vector2(-720, -330); guide.rectTransform.sizeDelta = new Vector2(260, 280);
            guide.sprite = Resources.Load<Sprite>("Guide/Guide_surprised") ?? Resources.Load<Sprite>("Guide/Guide_normal"); guide.preserveAspect = true;
            var shout = Box("Shout", Color.white, new Vector2(-470, -230), new Vector2(300, 90));
            Label("ShoutText", shout, "조심해!", 46, Red, Vector2.zero);
            return t =>
            {
                float pop = t < .18f ? Mathf.Lerp(0, 1.25f, t / .18f) : Mathf.Lerp(1.25f, 1, Ease((t - .18f) / .15f));
                star.rectTransform.localScale = back.rectTransform.localScale = Vector3.one * pop * (1 + .03f * Mathf.Sin(t * 18));
                star.rectTransform.localEulerAngles = back.rectTransform.localEulerAngles = new Vector3(0, 0, -4 + 2 * Mathf.Sin(t * 5));
                title.rectTransform.localEulerAngles = Vector3.zero;
                guide.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-1100, -720, Ease((t - .2f) / .3f)), -330);
                shout.localScale = Vector3.one * Ease((t - .45f) / .15f);
            };
        }

        // ---- small builders ----

        static float Ease(float x) { x = Mathf.Clamp01(x); return 1 - (1 - x) * (1 - x) * (1 - x); }

        RectTransform Box(string name, Color color, Vector2 position, Vector2 size, Transform parent = null)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent ? parent : root, false);
            image.rectTransform.anchoredPosition = position; image.rectTransform.sizeDelta = size;
            image.color = color; image.raycastTarget = false;
            return image.rectTransform;
        }

        TMP_Text Label(string name, Transform parent, string text, float size, Color color, Vector2 position)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false); label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = new Vector2(1600, size * 1.4f);
            if (font) label.font = font;
            label.text = text; label.fontSize = size; label.color = color; label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false; label.raycastTarget = false;
            return label;
        }

        (RectTransform rect, RawImage raw) Stripes(string name, Vector2 anchor, float height)
        {
            var raw = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            raw.rectTransform.SetParent(root, false);
            raw.rectTransform.anchorMin = anchor; raw.rectTransform.anchorMax = new Vector2(1, anchor.y); raw.rectTransform.pivot = anchor;
            raw.rectTransform.sizeDelta = new Vector2(0, height); raw.texture = HazardTexture(); raw.raycastTarget = false;
            return (raw.rectTransform, raw);
        }

        RectTransform Ring(string name, float size, float width)
        {
            var raw = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            raw.rectTransform.SetParent(root, false); raw.rectTransform.sizeDelta = new Vector2(size, size);
            raw.texture = RingTexture(width / size); raw.color = Red; raw.raycastTarget = false;
            return raw.rectTransform;
        }

        static Texture2D hazard, ring;
        static Texture2D HazardTexture()
        {
            if (hazard) return hazard;
            hazard = new Texture2D(64, 64) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) hazard.SetPixel(x, y, (x + y) % 64 < 32 ? Yellow : Ink);
            hazard.Apply();
            return hazard;
        }
        // A dashed ring: eight arcs with gaps, the band `width` of the radius.
        static Texture2D RingTexture(float width)
        {
            if (ring) return ring;
            const int n = 256; ring = new Texture2D(n, n) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x + .5f) / n * 2 - 1, dy = (y + .5f) / n * 2 - 1, r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Repeat(Mathf.Atan2(dy, dx) / (Mathf.PI * 2) * 8, 1);
                bool on = r <= 1 && r >= 1 - 2 * width && angle < .72f;
                ring.SetPixel(x, y, on ? Color.white : Color.clear);
            }
            ring.Apply();
            return ring;
        }

        // A sixteen-point comic starburst.
        sealed class StarBurst : Graphic
        {
            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                var r = rectTransform.rect; Vector2 c = r.center;
                vh.AddVert(c, color, Vector2.zero);
                const int points = 16;
                for (int i = 0; i <= points * 2; i++)
                {
                    float a = i * Mathf.PI / points, k = i % 2 == 0 ? 1 : .72f + .08f * Mathf.Sin(i * 1.7f);
                    vh.AddVert(c + new Vector2(Mathf.Cos(a) * r.width * .5f * k, Mathf.Sin(a) * r.height * .5f * k), color, Vector2.zero);
                    if (i > 0) vh.AddTriangle(0, i, i + 1);
                }
            }
        }
    }
}
