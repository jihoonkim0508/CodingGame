using System;
using System.Linq;
using CodingGame.Defense;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.StageMap
{
    // Esc on the stage map (play feedback 2026-11-12): the battle's Esc menu without its stage buttons — resume, the sound and
    // 디버's voice volume, anti-aliasing, and back to the title. Built over the map's canvas the first time it opens.
    public sealed class StageMapMenu : MonoBehaviour
    {
        Canvas canvas;
        Action<float> voiceChanged;
        GameObject menu;
        Slider soundSlider, voiceSlider;
        TMP_Text soundPercent, voicePercent;
        Button[] msaaButtons;
        public bool Open => menu && menu.activeSelf;

        // voiceChanged: the guide's voice playing now follows the slider.
        public void Configure(Canvas mapCanvas, Action<float> onVoice) { canvas = mapCanvas; voiceChanged = onVoice; }

        public void Toggle()
        {
            if (!canvas) return;
            if (!menu) Build();
            bool open = !menu.activeSelf;
            menu.SetActive(open); menu.transform.SetAsLastSibling();
            if (!open) return;
            soundSlider.value = DefenseSounds.Volume; voiceSlider.value = TutorialDialogueView.VoiceVolume;
            ShowMsaa();
        }

        void ShowMsaa()
        {
            for (int i = 0; i < msaaButtons.Length; i++)
                UITheme.Style((Image)msaaButtons[i].targetGraphic, UITheme.Button, GraphicsOptions.MsaaLevels[i] == GraphicsOptions.Msaa ? UITheme.Primary : UITheme.PaperDeep);
        }

        // Laid out like DefensePlayerHUD.BuildMenu (shade, paper card, rows), less its restart row.
        void Build()
        {
            var shade = new GameObject("Menu", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            shade.SetParent(canvas.transform, false); shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.sizeDelta = Vector2.zero;
            shade.GetComponent<Image>().color = new Color(UITheme.Night.r, UITheme.Night.g, UITheme.Night.b, .75f);
            menu = shade.gameObject;
            // Over the map's cards and 디버's box, under the scene fade; the shade also keeps the pointer off the islands.
            var layer = menu.AddComponent<Canvas>(); layer.overrideSorting = true; layer.sortingOrder = 90; menu.AddComponent<GraphicRaycaster>();
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            card.SetParent(shade, false); card.anchorMin = card.anchorMax = new Vector2(.5f, .5f); card.sizeDelta = new Vector2(500, 540);
            UITheme.Style(card.GetComponent<Image>(), UITheme.Card, UITheme.Paper);
            var font = TMP_Settings.defaultFontAsset;
            TMP_Text Text(string text, float x, float y, float width, float size, TextAlignmentOptions align = TextAlignmentOptions.Center)
            {
                var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.rectTransform.SetParent(card, false); label.rectTransform.sizeDelta = new Vector2(width, 60); label.rectTransform.anchoredPosition = new Vector2(x, y);
                label.font = font; label.fontSize = size; label.alignment = align; label.raycastTarget = false; label.text = text;
                label.color = UITheme.Ink; label.textWrappingMode = TextWrappingModes.NoWrap;
                return label;
            }
            Button Add(string text, float x, float y, Vector2 size, Color fill, float fontSize = 28)
            {
                var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
                var r = (RectTransform)go.transform; r.SetParent(card, false);
                r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.sizeDelta = size; r.anchoredPosition = new Vector2(x, y);
                var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
                UITheme.Style((Image)button.targetGraphic, UITheme.Button, fill);
                var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.rectTransform.SetParent(r, false); label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.sizeDelta = Vector2.zero;
                label.font = UITheme.Display ? UITheme.Display : font; label.fontSize = fontSize; label.color = UITheme.Ink;
                label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; label.text = text;
                return button;
            }
            Slider Volume(string name, float y, out TMP_Text shown)
            {
                Text(name, -150, y, 150, 24, TextAlignmentOptions.MidlineLeft);
                shown = Text("0%", 180, y, 80, 22, TextAlignmentOptions.MidlineRight); shown.color = UITheme.Muted;
                var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                root.SetParent(card, false); root.sizeDelta = new Vector2(190, 30); root.anchoredPosition = new Vector2(32, y);
                RectTransform Part(string partName, Transform parent, Sprite sprite, Color color)
                {
                    var part = new GameObject(partName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                    part.SetParent(parent, false); part.anchorMin = Vector2.zero; part.anchorMax = Vector2.one; part.sizeDelta = Vector2.zero;
                    UITheme.Style(part.GetComponent<Image>(), sprite, color);
                    return part;
                }
                var track = Part("Track", root, UITheme.Pill, UITheme.PaperDeep); track.sizeDelta = new Vector2(0, -12);
                var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
                fillArea.SetParent(root, false); fillArea.anchorMin = Vector2.zero; fillArea.anchorMax = Vector2.one; fillArea.sizeDelta = new Vector2(0, -12);
                var fill = Part("Fill", fillArea, UITheme.Pill, UITheme.Primary);
                var handleArea = new GameObject("Handle Area", typeof(RectTransform)).GetComponent<RectTransform>();
                handleArea.SetParent(root, false); handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one; handleArea.sizeDelta = new Vector2(-30, 0);
                var handle = Part("Handle", handleArea, UITheme.Circle, UITheme.Paper); handle.sizeDelta = new Vector2(30, 0);
                var slider = root.gameObject.AddComponent<Slider>();
                slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
                slider.direction = Slider.Direction.LeftToRight; slider.minValue = 0; slider.maxValue = 1;
                return slider;
            }
            var title = Text("메뉴", 0, 212, 440, 46);
            if (UITheme.Display) title.font = UITheme.Display;
            var resume = Add("계속하기", 0, 132, new Vector2(380, 66), UITheme.Primary);
            soundSlider = Volume("소리", 52, out soundPercent);
            voiceSlider = Volume("디버 목소리", -26, out voicePercent);
            Text("안티에일리어싱", -137, -104, 175, 22, TextAlignmentOptions.MidlineLeft);
            msaaButtons = GraphicsOptions.MsaaLevels.Select((level, i) =>
            {
                var button = Add(level == 1 ? "끔" : level + "x", i * 90, -104, new Vector2(80, 46), UITheme.PaperDeep, 22);
                button.onClick.AddListener(() => { GraphicsOptions.Msaa = level; ShowMsaa(); });
                return button;
            }).ToArray();
            var leave = Add("타이틀로", 0, -196, new Vector2(380, 66), UITheme.Coral);
            resume.onClick.AddListener(Toggle);
            leave.onClick.AddListener(() => GameFlow.LoadScene(GameFlow.TitleScene));
            soundSlider.onValueChanged.AddListener(v => { DefenseSounds.Volume = v; soundPercent.text = Mathf.RoundToInt(v * 100) + "%"; });
            voiceSlider.onValueChanged.AddListener(v => { TutorialDialogueView.VoiceVolume = v; voicePercent.text = Mathf.RoundToInt(v * 100) + "%"; voiceChanged?.Invoke(v); });
            menu.SetActive(false);
        }
    }
}
