using UnityEngine;

namespace CodingGame.TitleScreen
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class TitleEntranceLayer : MonoBehaviour
    {
        public RectTransform rect;
        public CanvasGroup opacity;
        [Tooltip("Seconds after the first faction begins entering")]
        [Min(0)] public float delay;
        [Min(.1f)] public float duration = .8f;
        [Tooltip("Negative enters from left; positive enters from right")]
        public float fromX = -900;
        [Range(0, 20)] public float overshoot = 6;
        [Range(0, 12)] public float entryLift = 3;
        [Range(0, 1)] public float idleWeight = .5f;
        public float idlePhase;
        public float EndTime => delay + duration;
        public bool HasEntered(float sequenceTime) => sequenceTime >= EndTime;

        public void Evaluate(float sequenceTime, float idleTime = 0, float idleStrength = 0)
        {
            float local = sequenceTime - delay;
            float t = Mathf.Clamp01(local / Mathf.Max(.1f, duration));
            // A fast approach followed by a short, small recoil into its final slot.
            const float landing = .82f;
            float x;
            if (t < landing)
            {
                float approach = 1 - Mathf.Pow(1 - t / landing, 3);
                x = Mathf.Lerp(fromX, -Mathf.Sign(fromX) * overshoot, approach);
            }
            else
            {
                float settle = Mathf.SmoothStep(0, 1, (t - landing) / (1 - landing));
                x = Mathf.Lerp(-Mathf.Sign(fromX) * overshoot, 0, settle);
            }
            opacity.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(local / .1f));
            float y = -entryLift * Mathf.Sin(t * Mathf.PI);
            if (t >= 1) y += Mathf.Sin(idleTime * .62f + idlePhase) * idleStrength * idleWeight;
            rect.anchoredPosition = new Vector2(x, y);
        }
    }
}
