using System;
using System.Collections;
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
            // Mirror the authored bottom layout; retain width, height and edge spacing at any resolution.
            bool top = position == TutorialDialoguePosition.Top;
            panelRect.anchorMin = new Vector2(bottomAnchorMin.x, top ? 1 - bottomAnchorMax.y : bottomAnchorMin.y);
            panelRect.anchorMax = new Vector2(bottomAnchorMax.x, top ? 1 - bottomAnchorMin.y : bottomAnchorMax.y);
            panelRect.pivot = new Vector2(bottomPivot.x, top ? 1 - bottomPivot.y : bottomPivot.y);
            panelRect.anchoredPosition = new Vector2(bottomPosition.x, top ? -bottomPosition.y - topInset : bottomPosition.y);
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

        public void SetNavigation(bool canPrevious, bool canNext)
        { previousText.interactable = canPrevious; nextText.interactable = canNext; }

        public void Hide()
        {
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
        public void SetCentered(bool on)
        {
            var parent = (RectTransform)panelRect.parent;
            Vector3 home = panelRect.localPosition;   // where Show just put it
            float scale = Mathf.Min(CenterScale, parent.rect.width * .9f / panelRect.rect.width);
            // Where the pivot sits when the panel's middle is the screen's middle, at that scale.
            Vector3 middle = parent.rect.center + Vector2.Scale(panelRect.pivot - new Vector2(.5f, .5f), panelRect.rect.size) * scale;
            if (entrance != null) StopCoroutine(entrance);
            entrance = null;
            if (on) { entrance = StartCoroutine(centered ? Hold(middle, scale) : PunchIn(middle, scale)); SetGrey(1); }
            else if (centered) entrance = StartCoroutine(Shrink(middle, scale, home));
            else { panelRect.localScale = Vector3.one; SetGrey(0); }
            centered = on;
        }
        const float CenterScale = 1.6f, PunchSeconds = .35f, ShrinkSeconds = .6f;
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
                SetGrey(1 - k);
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
        void OnDestroy() { if (grey) { Destroy(grey.profile); Destroy(grey.gameObject); } }
    }
}
