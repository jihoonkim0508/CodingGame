using UnityEngine;
using UnityEngine.Rendering;

namespace CodingGame.StageMap
{
    // Draws StageGlitchVisual's code error for the map camera. Each frame:
    // 1. The glitched models are drawn flat into a mask from this camera's view: coverage, which model, its strength.
    // 2. A full-screen quad (Shaders/StageTextGlitch) reads the mask with the opaque texture.
    // Every island bursts for .3 s in each 1.1 s, each on its own beat, 0.5 s apart. The burst's slices and bars change
    // every .1 s. Sizes are the mockup's, in pixels of a 1080-high screen.
    [RequireComponent(typeof(Camera))]
    public sealed class StageTextGlitch : MonoBehaviour
    {
        const string Shaders = "ArtScripts/StageSelection/Shaders/";
        const int MaxModels = 8, MaxSlices = 32, MaxBars = 32, SlicesPerBurst = 7, BarsPerBurst = 8;
        static readonly int MaskValue = Shader.PropertyToID("_GlitchMaskValue");
        static readonly Color[] BarColors = { Color.cyan, Color.magenta, Color.white };
        Camera view;
        RenderTexture mask;
        Material flat, glitch;
        CommandBuffer commands;
        GameObject screen;
        readonly Vector4[] rects = new Vector4[MaxModels], slices = new Vector4[MaxSlices], bars = new Vector4[MaxBars], barColors = new Vector4[MaxBars];

        void OnEnable()
        {
            view = GetComponent<Camera>();
            flat = new Material(Resources.Load<Shader>(Shaders + "StageGlitchMask"));
            glitch = new Material(Resources.Load<Shader>(Shaders + "StageTextGlitch"));
            commands = new CommandBuffer { name = "Stage glitch mask" };
            var quad = new Mesh { name = "Stage glitch screen", vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(.5f, .5f), new Vector3(-.5f, .5f) },
                triangles = new[] { 0, 2, 1, 0, 3, 2 } };
            quad.bounds = new Bounds(Vector3.zero, Vector3.one * 100000); // never culled: the shader puts it over the whole screen
            screen = new GameObject("Stage glitch", typeof(MeshFilter), typeof(MeshRenderer));
            screen.transform.SetParent(transform, false); screen.transform.localPosition = Vector3.forward * (view.nearClipPlane + 1);
            screen.GetComponent<MeshFilter>().sharedMesh = quad;
            var renderer = screen.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = glitch; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            RenderPipelineManager.beginCameraRendering += Draw;
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Draw;
            if (screen) { Destroy(screen.GetComponent<MeshFilter>().sharedMesh); Destroy(screen); }
            Destroy(flat); Destroy(glitch); commands?.Release();
            if (mask) { mask.Release(); Destroy(mask); }
        }

        void Draw(ScriptableRenderContext context, Camera camera)
        {
            if (camera != view) return;
            int width = camera.pixelWidth, height = camera.pixelHeight;
            if (!mask || mask.width != width || mask.height != height)
            {
                if (mask) { mask.Release(); Destroy(mask); }
                mask = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = "Stage glitch mask", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            }
            commands.Clear();
            commands.SetRenderTarget(mask);
            commands.ClearRenderTarget(false, true, Color.clear);
            commands.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
            // A pixel of a 1080-high screen, in viewport units.
            float unitX = height / 1080f / width, unitY = 1 / 1080f;
            int frame = Mathf.FloorToInt(GameClock.Now * 10), sliceCount = 0, barCount = 0, island = 0;
            bool any = false, bursting = false;
            var models = StageGlitchVisual.All;
            for (int i = 0; i < models.Count && i < MaxModels; i++)
            {
                var model = models[i]; float strength = model.Strength; int beat = model.Bursts ? island++ : -1;
                rects[i] = Vector4.zero;
                if (strength <= 0) continue;
                commands.SetGlobalVector(MaskValue, new Vector4(1, i / 8f, strength, 1));
                var box = new Bounds(); bool boxed = false;
                foreach (var part in model.Parts)
                {
                    if (!part || !part.enabled || !part.gameObject.activeInHierarchy) continue;
                    for (int m = 0; m < part.sharedMaterials.Length; m++) commands.DrawRenderer(part, flat, m);
                    if (boxed) box.Encapsulate(part.bounds); else { box = part.bounds; boxed = true; }
                }
                if (!boxed) continue;
                any = true;
                var rect = rects[i] = ScreenRect(camera, box);
                if (beat < 0 || strength < .25f || (frame + beat * 5) % 11 >= 3) continue;
                bursting = true;
                var random = new System.Random(frame * 7 + beat * 101);
                float Range(float from, float to) => from + (float)random.NextDouble() * (to - from);
                for (int k = 0; k < SlicesPerBurst && sliceCount < MaxSlices; k++)
                {
                    float tall = Range(5, 26) * unitY, bottom = Range(rect.y, Mathf.Max(rect.y, rect.w - tall));
                    slices[sliceCount++] = new Vector4(bottom, bottom + tall, (random.Next(2) * 2 - 1) * Range(15, 70) * unitX, i);
                }
                for (int k = 0; k < BarsPerBurst && barCount < MaxBars; k++)
                {
                    float y = Range(rect.y, rect.w), tall = Range(3, 8) * unitY, wide = Range(20, 150) * unitX;
                    int place = random.Next(3);
                    float x = place == 0 ? rect.x - wide / 2 : place == 1 ? rect.z - wide / 2 : Range(rect.x, rect.z);
                    bars[barCount] = new Vector4(x, y, x + wide, y + tall);
                    barColors[barCount++] = BarColors[random.Next(BarColors.Length)].linear;
                }
            }
            screen.SetActive(any);
            if (!any) return;
            context.ExecuteCommandBuffer(commands);
            commands.Clear();
            glitch.SetTexture("_GlitchMask", mask);
            glitch.SetVectorArray("_Rects", rects); glitch.SetVectorArray("_Slices", slices);
            glitch.SetVectorArray("_Bars", bars); glitch.SetVectorArray("_BarColors", barColors);
            glitch.SetFloat("_SliceCount", sliceCount); glitch.SetFloat("_BarCount", barCount);
            glitch.SetFloat("_GhostX", (bursting ? 16 : 5) * unitX); glitch.SetFloat("_GhostAlpha", .9f);
            glitch.SetColor("_Sky", camera.backgroundColor);
        }

        static Vector4 ScreenRect(Camera camera, Bounds box)
        {
            Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = camera.WorldToViewportPoint(new Vector3((corner & 1) == 0 ? box.min.x : box.max.x,
                    (corner & 2) == 0 ? box.min.y : box.max.y, (corner & 4) == 0 ? box.min.z : box.max.z));
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return new Vector4(min.x, min.y, max.x, max.y);
        }
    }
}
