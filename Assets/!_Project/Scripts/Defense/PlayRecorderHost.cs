#if UNITY_EDITOR
using System;
using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CodingGame.Defense
{
    // Editor-only frame grabber for PlayRecorder (Editor/Defense): takes the finished frame, overlay UI included, and hands
    // its pixels over with how many output frames it stands for. In the runtime assembly because a component from the
    // Editor assembly cannot be added in Play Mode.
    public sealed class PlayRecorderHost : MonoBehaviour
    {
        RenderTexture screen, scaled;
        Action<NativeArray<byte>, int> sink;
        int fps, written;
        double start;
        public double Elapsed => Time.realtimeSinceStartupAsDouble - start;

        public void Begin(int width, int height, int framesPerSecond, Action<NativeArray<byte>, int> frames)
        {
            fps = framesPerSecond; sink = frames;
            scaled = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            start = Time.realtimeSinceStartupAsDouble;
            StartCoroutine(Capture());
        }
        public void End() { StopAllCoroutines(); AsyncGPUReadback.WaitAllRequests(); sink = null; }

        IEnumerator Capture()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                // Output frames owed since the last one: 0 on fast frames, several after a stall.
                int due = (int)(Elapsed * fps) + 1 - written;
                if (due <= 0) continue;
                written += due;
                if (!screen || screen.width != Screen.width || screen.height != Screen.height)
                {
                    if (screen) screen.Release();
                    screen = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                }
                ScreenCapture.CaptureScreenshotIntoRenderTexture(screen);
                Graphics.Blit(screen, scaled);
                int copies = due;
                AsyncGPUReadback.Request(scaled, 0, TextureFormat.RGBA32, request => { if (!request.hasError) sink?.Invoke(request.GetData<byte>(), copies); });
            }
        }

        void OnDestroy()
        {
            if (screen) screen.Release();
            if (scaled) scaled.Release();
        }
    }
}
#endif
