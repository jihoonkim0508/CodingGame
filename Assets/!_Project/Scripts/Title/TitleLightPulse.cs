using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.TitleScreen
{
    [RequireComponent(typeof(Image))]
    public sealed class TitleLightPulse : MonoBehaviour
    {
        public Image image;
        public Material sourceMaterial;
        [Min(1)] public float period = 3.5f;
        [Range(0, .15f)] public float baseStrength = .055f;
        [Range(0, .08f)] public float amplitude = .02f;
        public float phase;
        public float MotionTime { get; private set; }
        public float CurrentStrength { get; private set; }
        Material instance;
        static readonly int Strength = Shader.PropertyToID("_Strength");

        void OnEnable()
        {
            if (!Application.isPlaying || !image || !sourceMaterial) return;
            instance = new Material(sourceMaterial) { name = sourceMaterial.name + " (runtime)", hideFlags = HideFlags.DontSave };
            image.material = instance;
            Evaluate(0);
        }
        void Update()
        {
            MotionTime += Time.unscaledDeltaTime;
            Evaluate(MotionTime);
        }
        public void Evaluate(float time)
        {
            CurrentStrength = Mathf.Max(0, baseStrength + Mathf.Sin(time * Mathf.PI * 2 / Mathf.Max(1, period) + phase) * amplitude);
            if (instance) instance.SetFloat(Strength, CurrentStrength);
        }
        void OnDisable()
        {
            if (image) image.material = sourceMaterial;
            if (instance) Destroy(instance);
            instance = null;
        }
    }
}
