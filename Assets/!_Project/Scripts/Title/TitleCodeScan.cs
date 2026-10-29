using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.TitleScreen
{
    [RequireComponent(typeof(Image))]
    public sealed class TitleCodeScan : MonoBehaviour
    {
        public Image image;
        public float period = 4.5f;
        [Range(0, .8f)] public float intensity = .42f;
        public float MotionTime { get; private set; }
        public float ScanPosition { get; private set; }
        TitleLightPulse pulse;
        float appliedPosition = -1, appliedIntensity = -1;
        static readonly int PositionId = Shader.PropertyToID("_ScanPosition");
        static readonly int StrengthId = Shader.PropertyToID("_ScanStrength");
        void Awake() { if (image) pulse = image.GetComponent<TitleLightPulse>(); }
        void Update()
        {
            MotionTime += Time.unscaledDeltaTime;
            float phase = Mathf.Repeat(MotionTime / Mathf.Max(2, period), 1);
            ScanPosition = Mathf.Floor(phase * 6) / 5f;
            // TitleLightPulse owns the runtime material instance.
            if (Application.isPlaying && image && pulse && image.material != pulse.sourceMaterial &&
                (ScanPosition != appliedPosition || intensity != appliedIntensity))
            {
                image.material.SetFloat(PositionId, ScanPosition);
                image.material.SetFloat(StrengthId, intensity);
                appliedPosition = ScanPosition; appliedIntensity = intensity;
            }
        }
        void OnDisable() { appliedPosition = appliedIntensity = -1; }
    }
}
