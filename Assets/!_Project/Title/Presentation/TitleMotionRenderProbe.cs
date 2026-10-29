#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodingGame.TitleScreen
{
    // Manually invoked editor-only render QA; no component is saved into the scene.
    public sealed class TitleMotionRenderProbe : MonoBehaviour
    {
        const string Reports="C:/Users/user/Documents/ChatGPT/CodingGame/TitleScreenWork/MotionReports/";
        public static string Begin()
        {
            if(!EditorApplication.isPlaying||EditorApplication.isPaused)throw new System.InvalidOperationException("Running Play mode required");
            if(FindFirstObjectByType<TitleMotionRenderProbe>())return "Render probe already running";
            new GameObject("Temporary Title Render QA").AddComponent<TitleMotionRenderProbe>();
            Application.runInBackground=true;
            return "Render probe started";
        }
        IEnumerator Start()
        {
            var title=FindFirstObjectByType<TitlePresentation>();
            while(!title.IsReady)yield return null;
            yield return new WaitForSecondsRealtime(.7f);
            var smoke=FindObjectsByType<TitleSmokeDrift>(FindObjectsSortMode.None);
            foreach(var s in smoke){s.enabled=false;s.image.enabled=false;}
            yield return null;
            yield return new WaitForEndOfFrame();
            Save("02_Smoke_Off.png");
            foreach(var s in smoke)s.image.enabled=true;
            yield return null;
            yield return new WaitForEndOfFrame();
            Save("03_Smoke_On.png");
            foreach(var s in smoke)s.enabled=true;
            var turret=FindFirstObjectByType<TitleTurretMotion>();
            float deadline=Time.realtimeSinceStartup+12;
            while(turret.flash.Intensity<.25f&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return new WaitForEndOfFrame();
            Save("05_Turret_Shot.png");
            File.WriteAllText(Reports+"RenderProbe.txt","Smoke Off/On captured after WaitForEndOfFrame.\nShot recoil="+turret.Recoil+", flash="+turret.flash.Intensity+"\nRESULT: "+(turret.flash.Intensity>.15f?"PASS":"FAIL"));
            yield return null;
            EditorApplication.isPlaying=false;
        }
        static void Save(string name)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try{File.WriteAllBytes(Reports+name,texture.EncodeToPNG());}
            finally{Destroy(texture);}
        }
    }
}
#endif
