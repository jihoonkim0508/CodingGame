using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CodingGame.StageMap
{
    // The map's code error: until its stage compiles, a model (an island, a bridge) is covered in glitch cells, the Object
    // Glitch Shader's band (Resources/ArtScripts/StageSelection/StageGlitch, a copy of BandGlitchLit that overwrites the
    // colour instead of base + glitch - 1, which turned plain colours black) made 14 wide so the cells cover over 90% of each
    // island (the user: at least 80%; 1.2 wide covered under 10%), its core still running across. The cells are drawn by copies
    // of the model's meshes over it (not clipped); the screens and glows stay on top. Built in play only.
    public sealed class StageGlitchVisual : MonoBehaviour
    {
        const string MaterialPath = "ArtScripts/StageSelection/StageGlitch";
        static Material material;
        static readonly int Center = Shader.PropertyToID("_BandCenterLength"), Travel = Shader.PropertyToID("_BandAxisLength"),
            Axis = Shader.PropertyToID("_BandAxis"), Width = Shader.PropertyToID("_BandTransitionLength"), Deform = Shader.PropertyToID("_DeformationStrength"),
            BaseColor = Shader.PropertyToID("_BaseColor");
        readonly List<Renderer> overlays = new List<Renderer>(), models = new List<Renderer>();
        MaterialPropertyBlock read;
        readonly List<Vector3> bands = new List<Vector3>(); // per overlay: start, travel, axis index
        MaterialPropertyBlock block;
        float shown = -1;
        public float Strength => Mathf.Max(0, shown);
        public static bool IsOverlay(Renderer renderer) => material && renderer.sharedMaterial == material;

        // sources: the model's renderers; along: the world direction the band runs (across an island, along a bridge).
        public void Build(IEnumerable<Renderer> sources, Vector3 along)
        {
            if (!Application.isPlaying || overlays.Count > 0) return;
            if (!material) material = Resources.Load<Material>(MaterialPath);
            if (!material) return;
            var list = new List<Renderer>();
            var whole = new Bounds(); bool any = false;
            foreach (var source in sources)
            {
                // Screens and glows (unlit, some see-through) stay as they are: the lit overlay darkened them.
                if (!source || source.sharedMaterial == null || source.sharedMaterial.renderQueue >= (int)RenderQueue.Transparent ||
                    source.sharedMaterial.shader.name.Contains("Unlit") || IsOverlay(source)) continue;
                if (!source.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh) continue;
                list.Add(source);
                if (any) whole.Encapsulate(source.bounds); else { whole = source.bounds; any = true; }
            }
            foreach (var source in list)
            {
                var copy = new GameObject("Glitch overlay", typeof(MeshFilter), typeof(MeshRenderer));
                copy.transform.SetParent(source.transform, false);
                copy.GetComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                var renderer = copy.GetComponent<MeshRenderer>();
                var shared = new Material[source.sharedMaterials.Length];
                for (int i = 0; i < shared.Length; i++) shared[i] = material;
                renderer.sharedMaterials = shared;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                // The band runs from the model's far end to its near one along the mesh axis closest to along, the whole model's
                // extent in this mesh's own space (the shader works there), so every part of it shows the same band.
                var t = source.transform; Vector3 local = t.InverseTransformDirection(along);
                int axis = Mathf.Abs(local.x) >= Mathf.Abs(local.y) && Mathf.Abs(local.x) >= Mathf.Abs(local.z) ? 0 : Mathf.Abs(local.y) >= Mathf.Abs(local.z) ? 1 : 2;
                float a = t.InverseTransformPoint(whole.min)[axis], b = t.InverseTransformPoint(whole.max)[axis];
                bands.Add(new Vector3(Mathf.Max(a, b), Mathf.Abs(b - a), axis));
                overlays.Add(renderer); models.Add(source);
            }
            block = new MaterialPropertyBlock(); read = new MaterialPropertyBlock();
            float strength = shown; shown = -1; SetStrength(strength < 0 ? 1 : strength);
        }

        // 1: the error at full; 0: compiled, the overlays off. In between the band narrows and settles (the clear).
        // Also called when the model's colours change (the island's look), which the overlays take up.
        public void SetStrength(float strength)
        {
            shown = strength = Mathf.Clamp01(strength);
            if (!material) return;
            float width = material.GetFloat(Width) * strength, deform = material.GetFloat(Deform) * strength;
            for (int i = 0; i < overlays.Count; i++)
            {
                var renderer = overlays[i];
                renderer.enabled = strength > 0;
                if (strength <= 0) continue;
                var band = bands[i]; var axis = Vector4.zero; axis[(int)band.z] = 1;
                block.Clear();
                block.SetFloat(Center, band.x); block.SetFloat(Travel, band.y); block.SetVector(Axis, axis);
                block.SetFloat(Width, width); block.SetFloat(Deform, deform);
                // The band's soft edge shows the overlay's own colour between the cells: the model's, so the edge does not stain it.
                block.SetColor(BaseColor, ColorOf(models[i]));
                renderer.SetPropertyBlock(block);
            }
        }
        Color ColorOf(Renderer source)
        {
            var color = source.sharedMaterial && source.sharedMaterial.HasProperty(BaseColor) ? source.sharedMaterial.GetColor(BaseColor) : Color.gray;
            if (source.HasPropertyBlock()) { source.GetPropertyBlock(read); if (read.HasColor(BaseColor)) color = read.GetColor(BaseColor); }
            color.a = 1; return color;
        }
    }
}
