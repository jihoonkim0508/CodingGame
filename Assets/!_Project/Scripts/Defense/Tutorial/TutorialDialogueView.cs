using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    public sealed class TutorialDialogueView : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Image portrait;
        [SerializeField] TMP_Text speaker, stageWave, dialogue;
        [SerializeField] Button previousText, nextText;
        [SerializeField, Tooltip("Top position keeps this gap so the HUD header stays readable.")] float topInset = 96;
        [SerializeField, Tooltip("Bottom position sits this much higher than authored, clear of the HUD's bottom bar.")] float bottomLift = 170;
        Coroutine typing, entrance;
        RectTransform panelRect;
        Vector2 bottomAnchorMin, bottomAnchorMax, bottomPosition, bottomPivot;
        public bool IsTyping => typing != null;
        public bool IsVisible => panel && panel.activeSelf;

        public void Initialize(Action onPrevious, Action onNext)
        {
            if (!panel || !portrait || !speaker || !stageWave || !dialogue || !previousText || !nextText)
                throw new InvalidOperationException(name + ": TutorialDialogueView 참조 누락");
            panelRect = panel.transform as RectTransform;
            if (!panelRect) throw new InvalidOperationException(name + ": 대화 패널에 RectTransform이 필요합니다.");
            bottomAnchorMin = panelRect.anchorMin; bottomAnchorMax = panelRect.anchorMax;
            bottomPosition = panelRect.anchoredPosition; bottomPivot = panelRect.pivot;
            previousText.onClick.AddListener(() => onPrevious?.Invoke());
            nextText.onClick.AddListener(() => onNext?.Invoke());
            Hide();
        }

        public void Show(TutorialSequence sequence, TutorialDialoguePosition position, string text, bool animate, string speakerName)
        {
            StopTyping();
            Place(position);
            panel.SetActive(true);
            portrait.sprite = sequence.speakerPortrait;
            portrait.enabled = sequence.speakerPortrait;
            speaker.text = speakerName ?? "";
            stageWave.text = sequence.stage + "-" + sequence.wave;
            dialogue.text = text ?? string.Empty;
            dialogue.ForceMeshUpdate();
            int characterCount = dialogue.textInfo.characterCount;
            dialogue.maxVisibleCharacters = animate ? 0 : int.MaxValue;
            if (animate && characterCount > 0) typing = StartCoroutine(Type(sequence.typeSpeed, characterCount));
        }

        GameObject supplement;
        public void ShowLessonSupplement(bool compare, bool prediction, Action<bool> answered)
        {
            if (supplement) { supplement.SetActive(false); Destroy(supplement); }
            if (!compare && !prediction) return;
            supplement = new GameObject("Repeat lesson", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)supplement.transform; rect.SetParent(panelRect, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 12); rect.sizeDelta = new Vector2(760, compare ? 190 : 70);
            supplement.GetComponent<Image>().color = new Color(.02f, .08f, .1f, .98f);
            if (compare)
            {
                void Label(string name, float left, float right, float bottom, float top, string content)
                {
                    var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                    text.rectTransform.SetParent(rect, false); text.rectTransform.anchorMin = new Vector2(left,bottom); text.rectTransform.anchorMax = new Vector2(right,top);
                    text.rectTransform.offsetMin = new Vector2(12, 4); text.rectTransform.offsetMax = new Vector2(-12, -4);
                    text.font = dialogue.font; text.fontSize = 25; text.alignment = TextAlignmentOptions.TopLeft; text.raycastTarget = false;
                    text.text = content;
                }
                Label("Expanded", 0, .5f, .2f, 1, "<b>명령을 늘어놓기</b>\n적 찾기 → 베기\n적 찾기 → 베기\n적 찾기 → 베기");
                Label("Loop", .5f, 1, .2f, 1, "<b>반복문 쓰기</b>\nfor 3회\n    적 찾기 → 베기");
                Label("Same result", .1f, 1, 0, .2f, "<color=#6FFFD2>같은 명령 묶음을 세 번 실행</color>");
                return;
            }
            string[] labels = { "1번", "3번", "적이 쓰러질 때까지" };
            for (int i = 0; i < labels.Length; i++)
            {
                int choice = i;
                var button = Instantiate(nextText, rect); button.name = "Prediction " + i;
                var br = (RectTransform)button.transform; br.anchorMin = br.anchorMax = new Vector2(.5f, .5f);
                br.pivot = new Vector2(.5f, .5f); br.sizeDelta = new Vector2(230, 52); br.anchoredPosition = new Vector2((i - 1) * 245, 0);
                foreach (Transform child in button.transform) Destroy(child.gameObject);
                var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.rectTransform.SetParent(br, false); label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                label.font = dialogue.font; label.fontSize = 23; label.alignment = TextAlignmentOptions.Center; label.text = labels[i]; label.raycastTarget = false;
                button.interactable = true;
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => { supplement.SetActive(false); answered?.Invoke(choice == 1); });
            }
        }

        // Stage 2 only: annotations follow the actual editable blocks instead of a detached text example.
        sealed class RepeatMarker
        {
            public string key, title;
            public RectTransform frame, label;
            public TMP_Text text;
            public Image[] edges;
            public Color color;
            public bool below;
        }
        readonly List<RepeatMarker> repeatMarkers = new List<RepeatMarker>();
        GameObject repeatOverlay;
        Func<string, Vector3[], bool> repeatArea;
        Func<DefenseSimulation> repeatSimulation;
        bool repeatLive;
        public void ShowRepeatBlockGuide(string stepId, Func<string, Vector3[], bool> area, Func<DefenseSimulation> simulation)
        {
            if (repeatOverlay) { repeatOverlay.SetActive(false); Destroy(repeatOverlay); }
            repeatMarkers.Clear(); repeatArea = area; repeatSimulation = simulation;
            if (string.IsNullOrEmpty(stepId)) return;
            var canvas = panelRect.GetComponentInParent<Canvas>().rootCanvas;
            repeatOverlay = new GameObject("Stage 2 repeat block labels", typeof(RectTransform));
            var root = (RectTransform)repeatOverlay.transform; root.SetParent(canvas.transform, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            var gold = new Color(1, .8f, .2f); var cyan = new Color(.35f, 1, .9f);
            void Mark(string key, string title, Color color, bool below = false)
            {
                var marker = new RepeatMarker { key = key, title = title, color = color, below = below };
                marker.frame = new GameObject(key, typeof(RectTransform)).GetComponent<RectTransform>();
                marker.frame.SetParent(root, false); marker.frame.anchorMin = marker.frame.anchorMax = new Vector2(.5f,.5f);
                marker.edges = new Image[4];
                for (int i = 0; i < 4; i++)
                {
                    var edge = new GameObject("Outline " + i, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    edge.rectTransform.SetParent(marker.frame, false); edge.raycastTarget = false; edge.color = color;
                    var er = edge.rectTransform;
                    if (i < 2) { er.anchorMin = new Vector2(0,i); er.anchorMax = new Vector2(1,i); er.sizeDelta = new Vector2(0,4); }
                    else { er.anchorMin = new Vector2(i-2,0); er.anchorMax = new Vector2(i-2,1); er.sizeDelta = new Vector2(4,0); }
                    er.anchoredPosition = Vector2.zero; marker.edges[i] = edge;
                }
                marker.label = new GameObject("Label", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                marker.label.SetParent(root,false); marker.label.anchorMin = marker.label.anchorMax = new Vector2(.5f,.5f); marker.label.pivot = new Vector2(0,.5f);
                marker.label.sizeDelta = new Vector2(320,64); var image = marker.label.GetComponent<Image>(); image.raycastTarget = false; image.color = new Color(.025f,.06f,.085f,.98f);
                marker.text = new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                marker.text.rectTransform.SetParent(marker.label,false); marker.text.rectTransform.anchorMin = Vector2.zero; marker.text.rectTransform.anchorMax = Vector2.one;
                marker.text.rectTransform.offsetMin = new Vector2(10,4); marker.text.rectTransform.offsetMax = new Vector2(-10,-4);
                marker.text.font = dialogue.font; marker.text.fontSize = 21; marker.text.fontStyle = FontStyles.Bold;
                marker.text.alignment = TextAlignmentOptions.MidlineLeft; marker.text.color = color; marker.text.raycastTarget = false;
                repeatMarkers.Add(marker);
            }
            repeatLive = stepId == "D01" || stepId == "D02";
            switch (stepId)
            {
                case "C01": Mark("Editor.Palette.For", "① 이 블록을 시작 위치로", gold, true); break;
                case "C02": Mark("Editor.Code.Arg.1.0", "① 반복 횟수\n10을 3으로 바꿔요", gold); break;
                case "C03": case "C04": Mark("Editor.Code.Body.1", "② 반복할 명령 묶음\n적 찾기와 베기를 안쪽에", cyan); break;
                case "C05": case "D00": case "D01": case "D02":
                    Mark("Editor.Code.Arg.1.0", "① 이 숫자만큼 반복", gold);
                    Mark("Editor.Code.Body.1", "② 적 찾기 → 베기", cyan); break;
                case "D03": Mark("Editor.Code.Arg.1.0", "① 숫자만 바꾸면 돼요\n3 → 10", gold); break;
                case "D04": case "D05": case "D06": Mark("Editor.Code.Body.1", "② 적 찾기도 반복 안쪽\n매번 공격 대상을 다시 찾기", cyan); break;
            }
            if (repeatMarkers.Count == 0) repeatOverlay.SetActive(false);
        }
        void LateUpdate()
        {
            if (!repeatOverlay || !repeatOverlay.activeSelf || repeatArea == null) return;
            var root = (RectTransform)repeatOverlay.transform; var canvas = root.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (var marker in repeatMarkers)
            {
                var corners = new Vector3[4]; bool visible = repeatArea(marker.key,corners);
                marker.frame.gameObject.SetActive(visible); marker.label.gameObject.SetActive(visible); if (!visible) continue;
                var min = new Vector2(float.MaxValue,float.MaxValue); var max = new Vector2(float.MinValue,float.MinValue);
                foreach(var corner in corners)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(root,RectTransformUtility.WorldToScreenPoint(camera,corner),camera,out var local);
                    min = Vector2.Min(min,local); max = Vector2.Max(max,local);
                }
                marker.frame.anchoredPosition = (min+max)/2; marker.frame.sizeDelta = max-min+Vector2.one*8;
                var pos = marker.below ? new Vector2(min.x,min.y-42) : new Vector2(max.x+14,(min.y+max.y)/2);
                if (!marker.below && pos.x+320 > root.rect.xMax-12) pos.x = min.x-334;
                pos.x = Mathf.Clamp(pos.x,root.rect.xMin+12,root.rect.xMax-332);
                pos.y = Mathf.Clamp(pos.y,root.rect.yMin+40,root.rect.yMax-40); marker.label.anchoredPosition = pos;
                marker.text.text = marker.title;
                var sim = repeatSimulation?.Invoke(); var program = sim?.Robots.Count > 0 ? sim.Robots[0].Program : null;
                if(repeatLive && marker.key == "Editor.Code.Body.1" && program != null)
                    marker.text.text += program.Completed(sim) ? "\n종료 · 잠시 후 다시 재생" : program.LoopCount > 0 ? $"\n지금 {program.LoopIteration} / {program.LoopCount}번째" : "\n반복 시작 대기";
                float alpha = .75f+.25f*Mathf.Sin(Time.unscaledTime*4);
                foreach(var edge in marker.edges) edge.color = new Color(marker.color.r,marker.color.g,marker.color.b,alpha);
            }
        }

        public RectTransform Panel => panelRect;
        public bool IsCentered => centered;
        public bool AtTop { get; private set; }
        // Mirror the authored bottom layout; retain width, height and edge spacing at any resolution.
        public void Place(TutorialDialoguePosition position)
        {
            bool top = AtTop = position == TutorialDialoguePosition.Top;
            panelRect.anchorMin = new Vector2(bottomAnchorMin.x, top ? 1 - bottomAnchorMax.y : bottomAnchorMin.y);
            panelRect.anchorMax = new Vector2(bottomAnchorMax.x, top ? 1 - bottomAnchorMin.y : bottomAnchorMax.y);
            panelRect.pivot = new Vector2(bottomPivot.x, top ? 1 - bottomPivot.y : bottomPivot.y);
            panelRect.anchoredPosition = new Vector2(bottomPosition.x, top ? -bottomPosition.y - topInset : bottomPosition.y + bottomLift);
        }

        IEnumerator Type(float charactersPerSecond, int characterCount)
        {
            float elapsed = 0;
            while (dialogue.maxVisibleCharacters < characterCount)
            {
                // One slow frame (editor opening, robot spawn) would otherwise reveal many letters at once; 0.1 s keeps low frame rates at full speed.
                elapsed += Mathf.Min(Time.unscaledDeltaTime, .1f);
                int count = Mathf.Min(characterCount, Mathf.FloorToInt(elapsed * charactersPerSecond) + 1);
                dialogue.maxVisibleCharacters = count;
                yield return null;
            }
            typing = null;
        }

        public void FinishTyping()
        {
            if (typing == null) return;
            StopTyping(); dialogue.maxVisibleCharacters = int.MaxValue;
        }

        // Editor only: the line's id (e.g. "1-1 A04b") in the panel's top-right corner, to match playtest notes to the script.
        TMP_Text idLabel;
        public void SetDebugId(string id)
        {
#if UNITY_EDITOR
            if (!idLabel)
            {
                idLabel = new GameObject("Debug id", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                var rect = idLabel.rectTransform; rect.SetParent(panelRect, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one; rect.anchoredPosition = new Vector2(-14, -8); rect.sizeDelta = new Vector2(240, 28);
                idLabel.font = dialogue.font; idLabel.fontSize = 18; idLabel.alignment = TextAlignmentOptions.TopRight;
                idLabel.color = new Color(1, 1, 1, .45f); idLabel.raycastTarget = false;
            }
            idLabel.text = id;
#endif
        }

        public void SetNavigation(bool canPrevious, bool canNext)
        { previousText.interactable = canPrevious; nextText.interactable = canNext; }

        public void Hide()
        {
            if (repeatOverlay) repeatOverlay.SetActive(false);
            StopTyping(); StopEntrance();
            if (panel) panel.SetActive(false);
        }

        void StopTyping()
        {
            if (typing != null) StopCoroutine(typing);
            typing = null;
        }

        // Centred mode (the opening lines): the world turns grey and the dialogue sits large in the middle of the screen,
        // punching in when it first appears. Leaving it shrinks the dialogue back into its usual place and brings the
        // colour back. Call after Show, every time a line is shown.
        // grey: the opening lines (grey world, extra large); otherwise a plain explanation line, moderately larger.
        public void SetCentered(bool on, bool grey = true)
        {
            var parent = (RectTransform)panelRect.parent;
            Vector3 home = panelRect.localPosition;   // where Show just put it
            float scale = Mathf.Min(grey ? CenterScale : PlainScale, parent.rect.width * .9f / panelRect.rect.width);
            // Where the pivot sits when the panel's middle is the screen's middle, at that scale.
            Vector3 middle = parent.rect.center + Vector2.Scale(panelRect.pivot - new Vector2(.5f, .5f), panelRect.rect.size) * scale;
            if (entrance != null) StopCoroutine(entrance);
            entrance = null;
            if (on) { entrance = StartCoroutine(centered ? Hold(middle, scale) : PunchIn(middle, scale)); SetGrey(grey ? 1 : 0); }
            else if (centered) entrance = StartCoroutine(Shrink(middle, scale, home));
            else { panelRect.localScale = Vector3.one; SetGrey(0); }
            centered = on;
        }
        // Glides from where the panel was to where Place just put it.
        public void SlideFrom(Vector3 from)
        {
            if (entrance != null) StopCoroutine(entrance);
            entrance = StartCoroutine(Slide(from, panelRect.localPosition));
        }
        IEnumerator Slide(Vector3 from, Vector3 to)
        {
            for (float t = 0; t < SlideSeconds; t += Time.unscaledDeltaTime)
            {
                panelRect.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / SlideSeconds));
                yield return null;
            }
            panelRect.localPosition = to;
            entrance = null;
        }
        const float SlideSeconds = .3f;
        const float CenterScale = 1.6f, PlainScale = 1.25f, PunchSeconds = .35f, ShrinkSeconds = .6f;
        bool centered;
        Volume grey;
        UniversalAdditionalCameraData greyCamera;
        bool cameraHadPostProcessing;
        void StopEntrance()
        {
            if (entrance != null) StopCoroutine(entrance);
            entrance = null; centered = false;
            if (panelRect) panelRect.localScale = Vector3.one;
            SetGrey(0);
        }
        IEnumerator Hold(Vector3 middle, float scale) { panelRect.localPosition = middle; panelRect.localScale = Vector3.one * scale; yield break; }
        // Bursts in slightly too big with a shake, then settles.
        IEnumerator PunchIn(Vector3 middle, float scale)
        {
            for (float t = 0; t < PunchSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / PunchSeconds, settle = 1 - k;
                panelRect.localScale = Vector3.one * scale * (1 + .18f * settle * settle);
                panelRect.localPosition = middle + new Vector3(Mathf.Sin(t * 90) * 14 * settle, Mathf.Cos(t * 70) * 6 * settle, 0);
                yield return null;
            }
            panelRect.localPosition = middle; panelRect.localScale = Vector3.one * scale;
            entrance = null;
        }
        IEnumerator Shrink(Vector3 middle, float scale, Vector3 home)
        {
            for (float t = 0; t < ShrinkSeconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / ShrinkSeconds);
                panelRect.localPosition = Vector3.Lerp(middle, home, k);
                panelRect.localScale = Vector3.one * Mathf.Lerp(scale, 1, k);
                if (grey) SetGrey(1 - k);
                yield return null;
            }
            panelRect.localPosition = home; panelRect.localScale = Vector3.one; SetGrey(0);
            entrance = null;
        }
        // weight 1: the battle camera's picture loses its colour (UI overlay stays coloured). Post-processing is on only meanwhile.
        void SetGrey(float weight)
        {
            if (weight > 0 && !grey)
            {
                grey = new GameObject("Tutorial grey", typeof(Volume)).GetComponent<Volume>();
                grey.isGlobal = true; grey.priority = 100;
                grey.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                grey.profile.Add<ColorAdjustments>(true).saturation.Override(-100);
            }
            if (grey) grey.weight = weight;
            var camera = Camera.main ? Camera.main.GetComponent<UniversalAdditionalCameraData>() : null;
            if (weight > 0 && camera && !greyCamera) { greyCamera = camera; cameraHadPostProcessing = camera.renderPostProcessing; camera.renderPostProcessing = true; }
            if (weight <= 0 && greyCamera) { greyCamera.renderPostProcessing = cameraHadPostProcessing; greyCamera = null; }
        }
        void OnDestroy() { if (repeatOverlay) Destroy(repeatOverlay); if (grey) { Destroy(grey.profile); Destroy(grey.gameObject); } }
    }
}
