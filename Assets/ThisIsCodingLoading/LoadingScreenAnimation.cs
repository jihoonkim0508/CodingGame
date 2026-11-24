using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThisIsCoding.Loading
{
    [DisallowMultipleComponent]
    public sealed class LoadingScreenAnimation : MonoBehaviour
    {
        [SerializeField] private RawImage repairAnimation;
        [SerializeField] private TMP_Text loadingText;

        private const int Columns = 8;
        private const int Rows = 9;
        private const int FrameCount = 66;
        private const int Padding = 2;
        private const double FrameInterval = 0.04;

        private double startTime;
        private string previousText;
        private int previousFrame;
        private int previousVisibleCharacters;

        private void OnEnable()
        {
            startTime = Time.unscaledTimeAsDouble;
            previousFrame = -1;
            previousVisibleCharacters = -1;
            Update();
        }

        private void OnDisable()
        {
            if (loadingText != null)
                loadingText.maxVisibleCharacters = int.MaxValue;
        }

        private void Update()
        {
            double elapsed = Time.unscaledTimeAsDouble - startTime;
            int frame = (int)(elapsed / FrameInterval % FrameCount);
            if (repairAnimation != null && repairAnimation.texture != null && frame != previousFrame)
            {
                Texture texture = repairAnimation.texture;
                int cellWidth = texture.width / Columns;
                int cellHeight = texture.height / Rows;
                int x = frame % Columns * cellWidth + Padding;
                int y = (Rows - 1 - frame / Columns) * cellHeight + Padding;
                repairAnimation.uvRect = new Rect((float)x / texture.width, (float)y / texture.height,
                    (float)(cellWidth - Padding * 2) / texture.width,
                    (float)(cellHeight - Padding * 2) / texture.height);
                previousFrame = frame;
            }

            if (loadingText == null)
                return;
            string message = loadingText.text;
            int visibleCharacters = int.MaxValue;
            if (!string.IsNullOrEmpty(message) && message.EndsWith("...", StringComparison.Ordinal))
            {
                int dots = (int)(elapsed / (FrameInterval * FrameCount / 4) % 4);
                visibleCharacters = message.Length - 3 + dots;
            }
            if (visibleCharacters != previousVisibleCharacters || message != previousText)
            {
                loadingText.maxVisibleCharacters = visibleCharacters;
                previousVisibleCharacters = visibleCharacters;
                previousText = message;
            }
        }
    }
}

