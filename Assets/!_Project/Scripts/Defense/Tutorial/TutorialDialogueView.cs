using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    public sealed class TutorialDialogueView : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Image portrait;
        [SerializeField] TMP_Text speaker, stageWave, dialogue;
        [SerializeField] Button previousText, nextText;
        Coroutine typing;
        Action previous, next;
        bool isTyping;
        public bool IsTyping => isTyping;
        public bool IsVisible => panel && panel.activeSelf;

        public void Initialize(Action onPrevious, Action onNext)
        {
            if (!panel || !portrait || !speaker || !stageWave || !dialogue || !previousText || !nextText)
                throw new InvalidOperationException(name + ": TutorialDialogueView 참조 누락");
            previous = onPrevious; next = onNext;
            previousText.onClick.AddListener(() => previous?.Invoke());
            nextText.onClick.AddListener(() => next?.Invoke());
            Hide();
        }

        public void Show(TutorialSequence sequence, int textIndex, string text, bool animate, Action completed)
        {
            StopTyping();
            panel.SetActive(true);
            portrait.sprite = sequence.speakerPortrait;
            portrait.enabled = sequence.speakerPortrait;
            speaker.text = sequence.speakerName;
            stageWave.text = sequence.stage + "-" + sequence.wave;
            dialogue.text = text ?? string.Empty;
            dialogue.ForceMeshUpdate();
            int characterCount = dialogue.textInfo.characterCount;
            dialogue.maxVisibleCharacters = animate ? 0 : int.MaxValue;
            if (animate && characterCount > 0) typing = StartCoroutine(Type(sequence.typeSpeed, characterCount, completed));
            else completed?.Invoke();
        }

        IEnumerator Type(float charactersPerSecond, int characterCount, Action completed)
        {
            isTyping = true;
            float elapsed = 0;
            while (dialogue.maxVisibleCharacters < characterCount)
            {
                elapsed += Time.unscaledDeltaTime;
                int count = Mathf.Min(characterCount, Mathf.FloorToInt(elapsed * charactersPerSecond) + 1);
                dialogue.maxVisibleCharacters = count;
                yield return null;
            }
            typing = null; isTyping = false; completed?.Invoke();
        }

        public void FinishTyping()
        {
            if (!isTyping) return;
            StopTyping(); dialogue.maxVisibleCharacters = int.MaxValue;
        }

        public void SetNavigation(bool canPrevious, bool canNext)
        { previousText.interactable = canPrevious; nextText.interactable = canNext; }

        public void Hide()
        {
            StopTyping();
            if (panel) panel.SetActive(false);
        }

        void StopTyping()
        {
            if (typing != null) StopCoroutine(typing);
            typing = null; isTyping = false;
        }
    }
}
