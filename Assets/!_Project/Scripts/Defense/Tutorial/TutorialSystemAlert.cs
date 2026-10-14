using System;
using TMPro;
using UnityEngine;

namespace CodingGame.Defense
{
    // Lines spoken by "시스템" show here: a banner that plays the warning sound and fades out after a fixed time.
    // Uses the scene's boot overlay objects (panel + CanvasGroup + AudioSource).
    public sealed class TutorialSystemAlert : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] CanvasGroup fade;
        [SerializeField] TMP_Text message;
        [SerializeField] AudioSource warning;
        [SerializeField, Min(.1f)] float seconds = 1.5f, fadeSeconds = .6f;
        float until;
        public bool IsShowing => panel.activeSelf;
        void Awake()
        {
            if (!panel || !fade || !message || !warning) throw new InvalidOperationException(name + ": 시스템 경고 UI 참조 누락");
            panel.SetActive(false);
        }
        public void Show(string text)
        {
            message.text = text ?? ""; fade.alpha = 1; until = Time.unscaledTime + seconds;
            panel.SetActive(true);
            if (warning.clip) warning.Play();
        }
        public void Hide() => panel.SetActive(false);
        void Update()
        {
            if (!IsShowing) return;
            fade.alpha = Mathf.Clamp01((until - Time.unscaledTime) / fadeSeconds);
            if (Time.unscaledTime >= until) Hide();
        }
    }
}
