using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    // Lines spoken by "시스템" show here: a red alarm banner (outlined message on a hazard-striped bar over a pulsing red
    // vignette) that plays the warning sound and fades out after a fixed time. With cards (1-1 A01) three warning cards
    // slam in above the bar first. The visuals are built at run time inside the scene's boot overlay (panel + CanvasGroup).
    public sealed class TutorialSystemAlert : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] CanvasGroup fade;
        [SerializeField] TMP_Text message;
        [SerializeField] AudioSource warning;
        [SerializeField, Min(.1f)] float seconds = 1.5f, fadeSeconds = .6f;
        // ponytail: the card wording is fixed here (1-1 A01 only); move it to the sequence if another alert needs cards.
        static readonly (string title, string body)[] Cards =
        {
            ("비정상 코드 감지", "개발툴 내부에서 비정상적인\n코드가 감지되었습니다."),
            ("무단 접근 시도", "허용되지 않은 동작이\n감지되었습니다."),
            ("시스템 이상", "개발 환경의 이상 행위가\n발견되었습니다."),
        };
        static readonly Color Red = new Color(1, .16f, .2f), DarkRed = new Color(.32f, .02f, .04f);
        float clock, duration;
        bool withCards;
        RectTransform bar, stripes, shake;
        Image vignette;
        readonly List<(RectTransform rect, CanvasGroup group)> cards = new List<(RectTransform, CanvasGroup)>();
        Material glow;
        public bool IsShowing => panel.activeSelf;

        void Awake()
        {
            if (!panel || !fade || !message || !warning) throw new InvalidOperationException(name + ": 시스템 경고 UI 참조 누락");
            Build();
            panel.SetActive(false);
        }

        // spoken: the length of the line's recording; the banner stays until it has been read out.
        public void Show(string text, bool showCards = false, float spoken = 0)
        {
            message.text = text ?? ""; withCards = showCards;
            // New glyphs join the dynamic font atlas while the alert is still dark, not when a card lands: adding them mid
            // sequence stalled a frame (about 1 s in the Editor, which re-imports the font asset) and broke the beat.
            message.font.TryAddCharacters(message.text + (showCards ? string.Concat(Cards.Select(c => c.title + c.body)) : ""));
            clock = 0; duration = Mathf.Max(seconds + (showCards ? Land(Cards.Length) : 0), spoken + .3f);
            foreach (var card in cards) card.rect.gameObject.SetActive(showCards);
            // The bar sits under the cards, or in the middle on its own.
            bar.anchoredPosition = new Vector2(0, showCards ? -40 : 0);
            panel.SetActive(true);
            Animate();
            if (warning.clip) warning.Play();
        }
        public void Hide() => panel.SetActive(false);

        void Update()
        {
            if (!IsShowing) return;
            // Stalled frames do not count: the first frames after the scene opens (1-1 A01) freeze for over a second, and a card
            // landing just before one read as "탁 … 탁 탁" (play test). Slow but running frames still count (a 1/30 cap slowed
            // every beat at 20 fps).
            float step = Time.unscaledDeltaTime;
            if (step < .25f) clock += step;
            if (clock >= duration) { Hide(); return; }
            Animate();
        }

        void Animate()
        {
            float t = clock;
            fade.alpha = Mathf.Min(Mathf.Clamp01(t / .12f), Mathf.Clamp01((duration - t) / fadeSeconds));
            vignette.color = new Color(Red.r, Red.g, Red.b, .24f + .16f * Mathf.Sin(t * 7));
            // Cards slam in one after another (big and clear, then settle).
            for (int i = 0; i < cards.Count; i++)
            {
                float k = Mathf.Clamp01((t - Land(i)) / .22f);
                cards[i].group.alpha = k;
                cards[i].rect.localScale = Vector3.one * Mathf.Lerp(1.35f, 1, 1 - (1 - k) * (1 - k));
            }
            // The bar opens sideways one beat after the last card; the message flickers on like a failing screen.
            float barStart = withCards ? Land(cards.Count) : 0;
            float open = Mathf.Clamp01((t - barStart) / .25f);
            bar.localScale = new Vector3(1 - (1 - open) * (1 - open), 1, 1);
            float lit = t - barStart - .2f;
            message.alpha = lit < 0 ? 0 : lit < .35f ? (Mathf.Sin(lit * 90) > -.2f ? 1 : .15f) : .93f + .07f * Mathf.Sin(t * 9);
            stripes.anchoredPosition = new Vector2(-Mathf.Repeat(t * 70, 40), 0);
            // A short jolt when the cards land.
            float jolt = Mathf.Clamp01(1 - (withCards ? t - barStart : t) / .25f);
            if (withCards) for (int i = 0; i < cards.Count; i++) jolt = Mathf.Max(jolt, Mathf.Clamp01(1 - Mathf.Abs(t - Land(i) - .2f) / .25f));
            shake.anchoredPosition = new Vector2(Mathf.Sin(t * 83) * 9, Mathf.Cos(t * 71) * 6) * jolt;
        }

        // The three cards and then the bar come at one even beat (planner); the bar then stays as long as a bar-only alert.
        const float Beat = .5f;
        static float Land(int panel) => .1f + panel * Beat;

        // ---- run-time visuals ----
        void Build()
        {
            var root = (RectTransform)panel.transform;
            if (panel.TryGetComponent<Image>(out var background)) background.color = Color.black;
            vignette = NewImage("Alarm vignette", root, Vignette(), Color.clear); Stretch(vignette);
            shake = Stretch(New("Alarm content", root));
            // Jalnan like every other heading, so the alarm reads as part of the game rather than a terminal (play feedback:
            // the square red frames looked stiff). Panels and stripes are rounded a little (ui_fill), framed thicker, on a shadow.
            if (UITheme.Display) message.font = UITheme.Display;
            // Light letters with a dark maroon outline and a hard dark drop, the banner look in the alarm's colours (Jalnan's
            // material has no glow pass; the glow stays for a font that has one).
            glow = new Material(message.fontSharedMaterial);
            glow.EnableKeyword(ShaderUtilities.Keyword_Glow);
            glow.SetColor(ShaderUtilities.ID_GlowColor, new Color(1, .05f, .1f, .8f));
            glow.SetFloat(ShaderUtilities.ID_GlowOuter, .55f); glow.SetFloat(ShaderUtilities.ID_GlowPower, .75f);
            glow.EnableKeyword(ShaderUtilities.Keyword_Outline);
            glow.SetFloat(ShaderUtilities.ID_OutlineWidth, .25f); glow.SetColor(ShaderUtilities.ID_OutlineColor, new Color(.12f, 0, .02f));
            glow.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            glow.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(.08f, 0, .01f, .9f)); glow.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.45f);
            glow.SetFloat(ShaderUtilities.ID_UnderlayDilate, .25f); glow.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0);

            bar = New("Alarm bar", shake); bar.sizeDelta = new Vector2(1560, 92);
            Panel(bar, 40, 16, new Color(.36f, 0, .04f, .92f));
            var mask = Stretch(New("Stripes mask", bar)); mask.gameObject.AddComponent<RectMask2D>();
            stripes = New("Stripes", mask); stripes.anchorMin = stripes.anchorMax = new Vector2(.5f, .5f); stripes.sizeDelta = new Vector2(1560, 92);
            // Four slanted stripes at each end, scrolling; the middle stays clear for the message.
            foreach (float side in new[] { -1f, 1f })
                for (int i = 0; i < 6; i++)
                {
                    var stripe = Rounded(NewImage("Stripe", stripes, null, new Color(1, .1f, .15f, .85f - i * .1f)), 7).rectTransform;
                    stripe.sizeDelta = new Vector2(18, 70); stripe.localEulerAngles = new Vector3(0, 0, -25);
                    stripe.anchoredPosition = new Vector2(side * (600 + i * 40), 0);
                }
            message.transform.SetParent(bar, false);
            var text = message.rectTransform;
            text.anchorMin = text.anchorMax = text.pivot = new Vector2(.5f, .5f);
            text.sizeDelta = new Vector2(1000, 90); text.anchoredPosition = Vector2.zero;
            message.textWrappingMode = TextWrappingModes.NoWrap; message.alignment = TextAlignmentOptions.Center;
            message.fontSize = 42; message.fontStyle = UITheme.Display ? FontStyles.Normal : FontStyles.Bold; message.color = Color.white;
            message.enableAutoSizing = true; message.fontSizeMax = 42; message.fontSizeMin = 30; // longer lines stay inside the stripes
            message.fontMaterial = glow;

            var circle = Circle(); var triangle = Triangle();
            for (int i = 0; i < Cards.Length; i++)
            {
                var card = New("Warning card", shake); card.sizeDelta = new Vector2(350, 182);
                card.anchoredPosition = new Vector2((i - 1) * 382, 150);
                var group = card.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false;
                Panel(card, 0, 12, new Color(.16f, .01f, .03f, .97f));
                var ring = NewImage("Icon ring", card, circle, new Color(.95f, .1f, .15f, 1)).rectTransform;
                ring.sizeDelta = new Vector2(90, 90); ring.anchoredPosition = new Vector2(0, 91);
                var disc = NewImage("Icon", ring, circle, DarkRed).rectTransform; disc.sizeDelta = new Vector2(82, 82);
                var sign = NewImage("Sign", disc, triangle, Red).rectTransform; sign.sizeDelta = new Vector2(52, 46); sign.anchoredPosition = new Vector2(0, 2);
                Label(sign, "!", 30, Color.white, new Vector2(0, -6), new Vector2(40, 40));
                Label(card, Cards[i].title, 30, new Color(1, .9f, .9f), new Vector2(0, 12), new Vector2(330, 44)).fontStyle = UITheme.Display ? FontStyles.Normal : FontStyles.Bold;
                // Sentences in Pretendard Bold without the glow (the glow material is Jalnan's).
                var body = Label(card, Cards[i].body, 21, new Color(1, .76f, .76f), new Vector2(0, -48), new Vector2(330, 64));
                if (UITheme.Bold) { body.font = UITheme.Bold; body.fontSharedMaterial = UITheme.Bold.material; }
                cards.Add((card, group));
            }
        }

        TMP_Text Label(RectTransform parent, string text, float size, Color color, Vector2 position, Vector2 box)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false); label.rectTransform.sizeDelta = box; label.rectTransform.anchoredPosition = position;
            label.font = message.font; label.fontSharedMaterial = glow; label.fontSize = size; label.color = color;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; label.text = text;
            return label;
        }
        // A rounded panel behind parent's content: an ink shadow 6 px below, a 3 px red frame, then the dark fill. wide: how far
        // the fill reaches past parent's sides; radius: the fill's corner radius in px.
        static void Panel(RectTransform parent, float wide, float radius, Color fill)
        {
            var shadow = Stretch(Rounded(NewImage("Shadow", parent, null, new Color(0, 0, 0, .45f)), radius + 3));
            shadow.offsetMin = new Vector2(-wide - 3, -9); shadow.offsetMax = new Vector2(wide + 3, -3);
            var frame = Stretch(Rounded(NewImage("Frame", parent, null, new Color(1, .16f, .2f, .9f)), radius + 3));
            frame.offsetMin = new Vector2(-wide - 3, -3); frame.offsetMax = new Vector2(wide + 3, 3);
            var inside = Stretch(Rounded(NewImage("Fill", parent, null, fill), radius));
            inside.offsetMin = new Vector2(-wide, 0); inside.offsetMax = new Vector2(wide, 0);
        }
        // ui_fill is a white rounded square with a 16 px radius; the multiplier scales the corners to radius.
        static Image Rounded(Image image, float radius)
        {
            image.sprite = UITheme.Fill; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 16f / radius;
            return image;
        }
        static RectTransform New(string name, RectTransform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); return rect;
        }
        static Image NewImage(string name, RectTransform parent, Sprite sprite, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false); image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }
        static RectTransform Stretch(Component c)
        {
            var rect = (RectTransform)c.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        static Sprite Draw(int size, Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(alpha((x + .5f) / size, (y + .5f) / size))));
            texture.SetPixels32(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }
        // Antialiased disc, triangle and a dark-centred red edge glow.
        static Sprite Circle() => Draw(128, (u, v) => (.5f - new Vector2(u - .5f, v - .5f).magnitude) * 128);
        static Sprite Triangle() => Draw(128, (u, v) => Mathf.Min(Mathf.Min(v - .08f, (.92f - v) * .5f - Mathf.Abs(u - .5f) + .02f), 1) * 128);
        static Sprite Vignette() => Draw(128, (u, v) => Mathf.Pow(Mathf.Clamp01(new Vector2((u - .5f) * 1.25f, v - .5f).magnitude * 1.6f - .35f), 1.6f));
    }
}
