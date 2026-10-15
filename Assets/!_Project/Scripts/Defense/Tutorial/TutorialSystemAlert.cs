using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    // Lines spoken by "시스템" show here: a red alarm banner (glowing message on a hazard-striped bar over a pulsing red
    // vignette) that plays the warning sound and fades out after a fixed time. With cards (1-1 A01) three warning cards
    // slam in above the bar first. The visuals are built at run time inside the scene's boot overlay (panel + CanvasGroup).
    public sealed class TutorialSystemAlert : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] CanvasGroup fade;
        [SerializeField] TMP_Text message;
        [SerializeField] AudioSource warning;
        [SerializeField, Min(.1f)] float seconds = 1.5f, fadeSeconds = .6f;
        [SerializeField, Min(0), Tooltip("경고 카드가 있는 배너(1-1 A01)에 더하는 표시 시간")] float cardSeconds = 3.2f;
        // ponytail: the card wording is fixed here (1-1 A01 only); move it to the sequence if another alert needs cards.
        static readonly (string title, string body)[] Cards =
        {
            ("비정상 코드 감지", "개발툴 내부에서 비정상적인\n코드가 감지되었습니다."),
            ("무단 접근 시도", "허용되지 않은 동작이\n감지되었습니다."),
            ("시스템 이상", "개발 환경의 이상 행위가\n발견되었습니다."),
        };
        static readonly Color Red = new Color(1, .16f, .2f), DarkRed = new Color(.32f, .02f, .04f);
        float shown, until;
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

        public void Show(string text, bool showCards = false)
        {
            message.text = text ?? ""; withCards = showCards;
            shown = Time.unscaledTime; until = shown + seconds + (showCards ? cardSeconds : 0);
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
            if (Time.unscaledTime >= until) { Hide(); return; }
            Animate();
        }

        void Animate()
        {
            float t = Time.unscaledTime - shown;
            fade.alpha = Mathf.Min(Mathf.Clamp01(t / .12f), Mathf.Clamp01((until - Time.unscaledTime) / fadeSeconds));
            vignette.color = new Color(Red.r, Red.g, Red.b, .24f + .16f * Mathf.Sin(t * 7));
            // Cards slam in one after another (big and clear, then settle).
            for (int i = 0; i < cards.Count; i++)
            {
                float k = Mathf.Clamp01((t - Land(i)) / .22f);
                cards[i].group.alpha = k;
                cards[i].rect.localScale = Vector3.one * Mathf.Lerp(1.35f, 1, 1 - (1 - k) * (1 - k));
            }
            // The bar opens sideways once the cards are in; the message flickers on like a failing screen.
            float barStart = withCards ? Land(cards.Count - 1) + .5f : 0;
            float open = Mathf.Clamp01((t - barStart) / .25f);
            bar.localScale = new Vector3(1 - (1 - open) * (1 - open), 1, 1);
            float lit = t - barStart - .2f;
            message.alpha = lit < 0 ? 0 : lit < .35f ? (Mathf.Sin(lit * 90) > -.2f ? 1 : .15f) : .85f + .15f * Mathf.Sin(t * 9);
            stripes.anchoredPosition = new Vector2(-Mathf.Repeat(t * 70, 40), 0);
            // A short jolt when the cards land.
            float jolt = Mathf.Clamp01(1 - (withCards ? t - barStart : t) / .25f);
            if (withCards) for (int i = 0; i < cards.Count; i++) jolt = Mathf.Max(jolt, Mathf.Clamp01(1 - Mathf.Abs(t - Land(i) - .2f) / .25f));
            shake.anchoredPosition = new Vector2(Mathf.Sin(t * 83) * 9, Mathf.Cos(t * 71) * 6) * jolt;
        }

        // Cards come one per second (planner).
        static float Land(int card) => .1f + card * 1f;

        // ---- run-time visuals ----
        void Build()
        {
            var root = (RectTransform)panel.transform;
            if (panel.TryGetComponent<Image>(out var background)) background.color = Color.black;
            vignette = NewImage("Alarm vignette", root, Vignette(), Color.clear); Stretch(vignette);
            shake = Stretch(New("Alarm content", root));
            glow = new Material(message.fontSharedMaterial);
            glow.EnableKeyword(ShaderUtilities.Keyword_Glow);
            glow.SetColor(ShaderUtilities.ID_GlowColor, new Color(1, .05f, .1f, .8f));
            glow.SetFloat(ShaderUtilities.ID_GlowOuter, .55f); glow.SetFloat(ShaderUtilities.ID_GlowPower, .75f);

            bar = New("Alarm bar", shake); bar.sizeDelta = new Vector2(1560, 92);
            var barFill = Stretch(NewImage("Fill", bar, null, new Color(.5f, 0, .03f, .35f)));
            barFill.offsetMin = new Vector2(-40, 0); barFill.offsetMax = new Vector2(40, 0);
            var mask = Stretch(New("Stripes mask", bar)); mask.gameObject.AddComponent<RectMask2D>();
            stripes = New("Stripes", mask); stripes.anchorMin = stripes.anchorMax = new Vector2(.5f, .5f); stripes.sizeDelta = new Vector2(1560, 92);
            // Four slanted stripes at each end, scrolling; the middle stays clear for the message.
            foreach (float side in new[] { -1f, 1f })
                for (int i = 0; i < 6; i++)
                {
                    var stripe = NewImage("Stripe", stripes, null, new Color(1, .1f, .15f, .85f - i * .1f)).rectTransform;
                    stripe.sizeDelta = new Vector2(18, 70); stripe.localEulerAngles = new Vector3(0, 0, -25);
                    stripe.anchoredPosition = new Vector2(side * (600 + i * 40), 0);
                }
            message.transform.SetParent(bar, false);
            var text = message.rectTransform;
            text.anchorMin = text.anchorMax = text.pivot = new Vector2(.5f, .5f);
            text.sizeDelta = new Vector2(1000, 90); text.anchoredPosition = Vector2.zero;
            message.enableWordWrapping = false; message.alignment = TextAlignmentOptions.Center;
            message.fontSize = 42; message.fontStyle = FontStyles.Bold; message.color = new Color(1, .25f, .28f);
            message.fontMaterial = glow;

            var circle = Circle(); var triangle = Triangle();
            for (int i = 0; i < Cards.Length; i++)
            {
                var card = New("Warning card", shake); card.sizeDelta = new Vector2(350, 182);
                card.anchoredPosition = new Vector2((i - 1) * 382, 150);
                var group = card.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false;
                // A 2px red frame: a red plate with the dark fill inset on it (Outline would tint the whole fill).
                var frame = Stretch(NewImage("Frame", card, null, new Color(1, .15f, .2f, .75f))); frame.offsetMin = new Vector2(-2, -2); frame.offsetMax = new Vector2(2, 2);
                Stretch(NewImage("Fill", card, null, new Color(.13f, .005f, .015f, .96f)));
                var ring = NewImage("Icon ring", card, circle, new Color(.95f, .1f, .15f, 1)).rectTransform;
                ring.sizeDelta = new Vector2(90, 90); ring.anchoredPosition = new Vector2(0, 91);
                var disc = NewImage("Icon", ring, circle, DarkRed).rectTransform; disc.sizeDelta = new Vector2(82, 82);
                var sign = NewImage("Sign", disc, triangle, Red).rectTransform; sign.sizeDelta = new Vector2(52, 46); sign.anchoredPosition = new Vector2(0, 2);
                Label(sign, "!", 30, Color.white, new Vector2(0, -6), new Vector2(40, 40));
                Label(card, Cards[i].title, 30, new Color(1, .9f, .9f), new Vector2(0, 12), new Vector2(330, 44)).fontStyle = FontStyles.Bold;
                Label(card, Cards[i].body, 21, new Color(1, .72f, .72f), new Vector2(0, -48), new Vector2(330, 64));
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
