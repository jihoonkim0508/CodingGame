using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense.Editor
{
    public static partial class GeneratedUISkinBuilder
    {
        static void RefineStatusHeader(Transform canvas)
        {
            var header = canvas.Find("Header") as RectTransform;
            if (!header) return;
            Specific(canvas, "Header", "RobotStatusHeaderRounded", Color.white, 3);
            header.GetComponent<Image>().raycastTarget = false;
            Stretch(header, new Vector2(0, 1), Vector2.one, new Vector2(20, -86), new Vector2(-20, -8));
            LayoutStatusHeader(header);
        }

        internal static void LayoutStatusHeader(RectTransform header)
        {
            foreach (var name in new[] { "Brand", "TitleLogo" })
            {
                var title = header.Find(name);
                if (title) title.gameObject.SetActive(false);
            }

            var info = (RectTransform)header.Find("Info");
            // Leave a gap between the info button and the start of the combat controls.
            SetRect(info, new Vector2(1, 0), new Vector2(-358, -43), new Vector2(36, 36), new Vector2(.5f, .5f));
            RoundedControl(info.GetComponent<Image>(), new Color(.14f, .31f, .42f), 18);
            var infoLabel = info.GetComponentInChildren<TMP_Text>(true);
            Stretch(infoLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(3, 2), new Vector2(-3, -2));
            infoLabel.fontSize = 22;
            infoLabel.alignment = TextAlignmentOptions.Midline;
            infoLabel.margin = Vector4.zero;

            var state = header.Find("Counters").GetComponent<TMP_Text>();
            SetRect(state.rectTransform, new Vector2(0, .5f), new Vector2(24, 0), new Vector2(350, 40), new Vector2(0, .5f));
            state.fontSize = 18;
            state.enableAutoSizing = true;
            state.fontSizeMin = 15;
            state.fontSizeMax = 18;
            state.alignment = TextAlignmentOptions.MidlineLeft;
            state.margin = Vector4.zero;
            state.color = new Color(.83f, .93f, .97f);

            var captionWidths = new[] { 61f, 74f, 42f, 42f };
            var centerLeft = new Vector2(0, .5f);
            for (int i = 0; i < captionWidths.Length; i++)
            {
                var stat = header.Find("Stat" + i);
                if (!stat) continue;
                SetRect((RectTransform)stat, new Vector2(1, .5f), new Vector2(-626 + i * 176, 0),
                    new Vector2(156, 54), new Vector2(.5f, .5f));
                var frame = stat.GetComponent<Image>();
                if (frame) frame.enabled = false;
                foreach (var name in new[] { "Generated icon", "Symbol" })
                {
                    var icon = stat.Find(name);
                    if (icon) icon.gameObject.SetActive(false);
                }

                var caption = stat.Find("Caption").GetComponent<TMP_Text>();
                float captionWidth = captionWidths[i];
                SetRect(caption.rectTransform, centerLeft, Vector2.zero, new Vector2(captionWidth, 36), centerLeft);
                caption.fontSize = 20;
                caption.enableAutoSizing = false;
                caption.textWrappingMode = TextWrappingModes.NoWrap;
                caption.alignment = TextAlignmentOptions.MidlineLeft;
                caption.margin = Vector4.zero;
                caption.color = new Color(.67f, .84f, .88f);

                var value = stat.Find("Value").GetComponent<TMP_Text>();
                float valueOffset = captionWidth + 8;
                SetRect(value.rectTransform, centerLeft, new Vector2(valueOffset, 0), new Vector2(156 - valueOffset, 36), centerLeft);
                value.fontSize = 24;
                value.enableAutoSizing = true;
                value.fontSizeMin = 13;
                value.fontSizeMax = 24;
                value.textWrappingMode = TextWrappingModes.NoWrap;
                value.alignment = TextAlignmentOptions.MidlineLeft;
                value.margin = Vector4.zero;
                value.color = new Color(.95f, .99f, 1);
            }
        }
    }
}
