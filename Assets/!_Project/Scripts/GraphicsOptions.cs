using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CodingGame
{
    // Frame pacing and anti-aliasing, set before the first scene.
    // 140: as PC games ship it, VSync on (QualitySettings, PC level) and a frame cap at the display's refresh rate for when the
    // driver forces VSync off; a build in the background drops to 30 frames a second. The Editor gets the cap only.
    // 164: MSAA off, 2x or 4x from the Esc menu, saved like the volume (the pipeline's 4x until changed).
    public static class GraphicsOptions
    {
        const int BackgroundFps = 30;
        // The refresh rate of the display the game is on (60 when the system does not say).
        public static int DisplayHz
        {
            get { double hz = Screen.currentResolution.refreshRateRatio.value; return hz >= 30 ? Mathf.RoundToInt((float)hz) : 60; }
        }

        public static readonly int[] MsaaLevels = { 1, 2, 4 };
        const string MsaaKey = "Graphics.MSAA";
        static int pipelineMsaa = 4;
        static UniversalRenderPipelineAsset Pipeline => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        public static int Msaa
        {
            get => PlayerPrefs.GetInt(MsaaKey, pipelineMsaa);
            set { PlayerPrefs.SetInt(MsaaKey, value); PlayerPrefs.Save(); if (Pipeline) Pipeline.msaaSampleCount = value; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            Application.targetFrameRate = DisplayHz;
#if !UNITY_EDITOR
            Application.focusChanged += focused =>
            {
                QualitySettings.vSyncCount = focused ? 1 : 0; // a frame cap applies only with VSync off
                Application.targetFrameRate = focused ? DisplayHz : BackgroundFps;
            };
#endif
            if (!Pipeline) return;
            pipelineMsaa = Pipeline.msaaSampleCount;
            Pipeline.msaaSampleCount = Msaa;
#if UNITY_EDITOR
            // The pipeline asset is the project's own file in the Editor: leave it as it was.
            Application.quitting += () => { if (Pipeline) Pipeline.msaaSampleCount = pipelineMsaa; };
#endif
        }
    }
}
