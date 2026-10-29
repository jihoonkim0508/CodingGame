using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.TitleScreen
{
    [RequireComponent(typeof(Image))]
    public sealed class TitleFlagMotion : MonoBehaviour
    {
        public Image image;
        public Material sourceMaterial;
        public float period = 2.8f;
        public float phase;
        public float MotionTime { get; private set; }
        public float WavePhase { get; private set; }
        Material instance;
        static readonly int WavePhaseId = Shader.PropertyToID("_WavePhase");
        void OnEnable()
        {
            if (!Application.isPlaying || !image || !sourceMaterial) return;
            instance = new Material(sourceMaterial) { hideFlags = HideFlags.DontSave };
            image.material = instance;
        }
        void Update()
        {
            MotionTime += Time.unscaledDeltaTime;
            WavePhase = MotionTime * Mathf.PI * 2 / Mathf.Max(1, period) + phase;
            if (instance) instance.SetFloat(WavePhaseId, WavePhase);
        }
        void OnDisable()
        {
            if (image) image.material = sourceMaterial;
            if (instance) Destroy(instance);
            instance = null;
        }
    }
}
