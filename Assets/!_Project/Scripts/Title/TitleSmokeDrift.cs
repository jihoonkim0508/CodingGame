using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.TitleScreen
{
    [RequireComponent(typeof(Image))]
    public sealed class TitleSmokeDrift : MonoBehaviour
    {
        public Image image;
        public RectTransform rect;
        public Vector2 home;
        public Vector2 travel = new Vector2(14, 42);
        public Color tint = Color.white;
        [Range(0, .6f)] public float maxOpacity = .3f;
        [Min(3)] public float period = 6;
        [Range(0, 1)] public float phase;
        public float MotionTime { get; private set; }
        public float CurrentOpacity => image ? image.color.a : 0;

        void Update()
        {
            MotionTime += Time.unscaledDeltaTime;
            Evaluate(MotionTime);
        }
        public void Evaluate(float time)
        {
            float p = Mathf.Repeat(time / Mathf.Max(3, period) + phase, 1);
            // Cross-faded pairs hide the wrap back to the bottom of the plume.
            float fade = Mathf.Sin(p * Mathf.PI);
            rect.anchoredPosition = home + (p - .5f) * travel;
            rect.localScale = Vector3.one * Mathf.Lerp(.86f, 1.12f, p);
            var color = tint;
            color.a = maxOpacity * fade * fade;
            image.color = color;
        }
    }
}
