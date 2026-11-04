using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame
{
    // The toy-workshop look: cream paper cards and buttons with a dark ink outline, Jalnan for headings. The sprites
    // (Resources/UI, drawn by Editor/UI~/ui_sprites.py) are white with the outline and shadow baked in, so Image.color
    // picks the fill. Tools/Develop/UI 스타일 적용 (UIRestyle) puts this on the scenes and prefabs; runtime-built UI uses
    // the same pieces from here.
    // Colour roles: Primary (a beige gold) fills the one main action on a screen and marks what is chosen;
    // other buttons are paper; Coral is for recall, leaving and errors only; Mint stays 디버's own colour.
    public static class UITheme
    {
        public static readonly Color Ink = Hex(0x262A3E), Paper = Hex(0xFFF8EC), PaperDeep = Hex(0xF1E7D3), GridPaper = Hex(0xFBF7EE),
            Muted = Hex(0x6B7085), Sunny = Hex(0xFFC531), Mint = Hex(0x3DD6A3), Sky = Hex(0x56B8FF), Coral = Hex(0xFF6B6B),
            Grape = Hex(0xB08CFF), Orange = Hex(0xFFA34D), Night = Hex(0x1B2133),
            Primary = Hex(0xEFCB84), World = Hex(0x4B8BBE); // Primary: a beige gold, softer than Sunny; World: the stage map's sky, behind full-screen panels
        // Rich-text colours that read on paper.
        public const string InkHex = "#262A3E", MutedHex = "#6B7085", CodeHex = "#1971C2", GoodHex = "#2B8A3E", WarnHex = "#E8590C",
            DangerHex = "#E03131", GoldHex = "#C77700", KeywordHex = "#D6336C", CalmHex = "#0C8A73";

        public static Sprite Card => Load("ui_card");
        public static Sprite FlatCard => Load("ui_card_flat");
        public static Sprite Button => Load("ui_button");
        public static Sprite Pill => Load("ui_pill");
        public static Sprite Chip => Load("ui_chip");
        public static Sprite Circle => Load("ui_circle");
        public static Sprite Fill => Load("ui_fill"); // no outline: the coloured part inside a sticker
        static Sprite Load(string name) => Resources.Load<Sprite>("UI/" + name);

        static TMP_FontAsset display;
        // Jalnan 2 for headings, buttons and banners (Resources/UI/Jalnan2 SDF, made by UIRestyle); null before that runs.
        public static TMP_FontAsset Display => display ? display : display = Resources.Load<TMP_FontAsset>("UI/Jalnan2 SDF");
        static TMP_FontAsset bold;
        // Pretendard Bold for body text that has to read clearly (Resources/UI/Pretendard Bold SDF, made by UIRestyle).
        public static TMP_FontAsset Bold => bold ? bold : bold = Resources.Load<TMP_FontAsset>("UI/Pretendard Bold SDF");

        // Sliced sticker art on an Image; the corners shrink to fit small rects.
        public static Image Style(Image image, Sprite sprite, Color fill)
        {
            if (!image) return image;
            image.sprite = sprite; image.type = sprite && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.color = fill;
            if (sprite)
            {
                var size = image.rectTransform.rect.size; var b = sprite.border;
                float fit = Mathf.Max(1, (b.y + b.w) / Mathf.Max(1, size.y * .9f), (b.x + b.z) / Mathf.Max(1, size.x * .9f));
                image.pixelsPerUnitMultiplier = size.y > 0 ? fit : 1;
            }
            return image;
        }

        public static Color Hex(int rgb) => new Color((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f);
    }
}
