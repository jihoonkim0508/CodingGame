using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CodingGame.StageMap
{
    // The map's code error, the user's pick 시안/스테이지 맵 글자 글리치/시안_N2_글리치_버스트.gif. Until its stage compiles,
    // a model (an island, a bridge) is glitched like text on a screen:
    // - cyan and magenta copies either side of its outline;
    // - an island, now and then and on its own beat, bursts into slices shoved sideways with glitch bars round it.
    // StageTextGlitch on the map camera draws it from the models listed here, from their opaque parts. The glass domes and
    // see-through glows stay as they are. Play only.
    public sealed class StageGlitchVisual : MonoBehaviour
    {
        public static readonly List<StageGlitchVisual> All = new List<StageGlitchVisual>();
        public readonly List<Renderer> Parts = new List<Renderer>();
        public bool Bursts { get; private set; }
        float shown = -1;
        public float Strength => Mathf.Max(0, shown);

        // sources: the model's renderers; bursts: an island (it bursts), not a bridge (its outline copies only).
        public void Build(IEnumerable<Renderer> sources, bool bursts)
        {
            if (!Application.isPlaying || Parts.Count > 0) return;
            foreach (var source in sources)
                if (source && source.sharedMaterials.Length > 0 && System.Array.TrueForAll(source.sharedMaterials, m => m && m.renderQueue <= (int)RenderQueue.GeometryLast))
                    Parts.Add(source);
            Bursts = bursts;
            if (shown < 0) shown = 1;
        }

        // 1: the error at full; 0: compiled, gone. In between it fades (the clear).
        public void SetStrength(float strength) => shown = Mathf.Clamp01(strength);
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);
    }
}
