using UnityEngine;

namespace CodingGame
{
    // Frame pacing, set before the first scene.
    // 140: as PC games ship it, VSync on (QualitySettings, PC level) and a frame cap at the display's refresh rate for when the
    // driver forces VSync off; a build in the background drops to 30 frames a second. The Editor gets the cap only.
    public static class GraphicsOptions
    {
        const int BackgroundFps = 30;
        // The refresh rate of the display the game is on (60 when the system does not say).
        public static int DisplayHz
        {
            get { double hz = Screen.currentResolution.refreshRateRatio.value; return hz >= 30 ? Mathf.RoundToInt((float)hz) : 60; }
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
        }
    }
}
