#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace CodingGame.TitleScreen
{
    // Explicit editor-only QA. Temporary object is never saved into a scene.
    public sealed class TitleTeamLogoProbe : MonoBehaviour
    {
        const string Reports="C:/Users/user/Documents/ChatGPT/CodingGame/TitleScreenWork/TeamLogoReports/";
        readonly List<string> results=new List<string>();
        Keyboard keyboard;
        Mouse mouse;
        float began;
        bool finished;
        public static string Begin()
        {
            if(!EditorApplication.isPlaying||EditorApplication.isPaused)throw new InvalidOperationException("Running Play mode required");
            if(FindFirstObjectByType<TitleTeamLogoProbe>())return "Already running";
            DontDestroyOnLoad(new GameObject("Temporary Team Logo QA").AddComponent<TitleTeamLogoProbe>().gameObject);
            Application.runInBackground=true;
            return "Team logo / scoped skip render verification started";
        }
        IEnumerator Start()
        {
            began=Time.realtimeSinceStartup;
            Directory.CreateDirectory(Reports);
            var run=Run();
            while(true)
            {
                bool more;
                try{more=run.MoveNext();}catch(Exception error){Finish(error.ToString());yield break;}
                if(!more)break;
                yield return run.Current;
            }
            Finish(null);
        }
        void Update(){if(!finished&&began>0&&Time.realtimeSinceStartup-began>45)Finish("Verification timeout");}
        void Check(bool condition,string description)
        {
            if(!condition)throw new Exception(description);
            results.Add("PASS: "+description);
        }
        IEnumerator Run()
        {
            var title=FindFirstObjectByType<TitlePresentation>();
            Check(title&&title.teamLogo&&title.teamLogo.sprite.name=="DFTeamLogo"&&title.teamLogo.preserveAspect,"Supplied DF logo sprite assigned with aspect preserved");
            Check(!title.teamGroup.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(),"Temporary team text removed");
            while(title.Elapsed<title.openingBlackHold+title.teamFadeIn+.1f)yield return null;
            Check(title.teamGroup.alpha>.99f&&title.battlefieldGroup.alpha==0,"Team logo appears alone on black");
            yield return new WaitForEndOfFrame();Save("01_Team_Logo.png");
            keyboard=InputSystem.AddDevice<Keyboard>("TeamLogoProbeKeyboard");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));
            yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            yield return null;yield return null;
            Check(title.Elapsed>=title.TeamEnd&&!title.IsReady&&title.menuGroup.alpha==0&&title.logoGroup.alpha==0,
                "Actual Space skips team splash only; title and menu remain hidden");
            Check(!title.CanSkipTeamLogo&&title.skipGroup.alpha==0,"Skip hint disappears after team splash");
            while(title.FactionElapsed<.2f)yield return null;
            float before=title.Elapsed;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));
            yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            yield return null;yield return null;
            Check(!title.IsReady&&title.Elapsed-before<.25f&&title.menuGroup.alpha==0,"Repeated Space cannot bypass the battlefield entrance");
            Check(title.entranceLayers[5].opacity.alpha==0,"Final foreground layer still waits for its own entrance beat");
            yield return new WaitForEndOfFrame();Save("02_Entrance_After_Skip.png");
            while(title.FactionElapsed<.55f)yield return null;
            Check(title.entranceLayers.Take(4).All(l=>l.opacity.alpha>0)&&title.entranceLayers[5].opacity.alpha==0,
                "Middle layers enter before the final foreground after splash skip");
            yield return new WaitForEndOfFrame();Save("03_Stagger_After_Skip.png");
            while(!title.IsReady)yield return null;
            yield return null;yield return null;yield return null;
            Check(title.startButton.interactable&&title.quitButton.interactable,"Menu enables only after natural background/title sequence completion");
            yield return new WaitForEndOfFrame();Save("04_Menu_After_Skip.png");

            SceneManager.LoadScene("Title");yield return null;yield return null;
            title=FindFirstObjectByType<TitlePresentation>();
            while(title.Elapsed<title.openingBlackHold+title.teamFadeIn+.1f)yield return null;
            mouse=InputSystem.AddDevice<Mouse>("TeamLogoProbeMouse");
            Vector2 point=new Vector2(Screen.width*.5f,Screen.height*.5f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});
            yield return null;InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=0});
            yield return null;yield return null;
            Check(title.Elapsed>=title.TeamEnd&&!title.IsReady&&title.menuGroup.alpha==0,"Actual mouse click also skips only the team logo");
            before=title.Elapsed;
            title.SkipIntro();title.SkipIntro();
            Check(Mathf.Abs(title.Elapsed-before)<.001f,"Repeated skip calls after splash do not jump the timeline");
            while(!title.IsReady)yield return null;
            yield return null;yield return null;yield return null;
            Check(SceneManager.GetActiveScene().name=="Title"&&title.startButton.interactable,"Splash click does not start gameplay and menu remains usable");
        }
        static void Save(string name)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try{File.WriteAllBytes(Reports+name,texture.EncodeToPNG());}finally{Destroy(texture);}
        }
        void Finish(string error)
        {
            if(finished)return;finished=true;
            if(error!=null)results.Add("FAIL: "+error);
            results.Add("RESULT: "+(error==null?"PASS":"FAIL"));
            File.WriteAllLines(Reports+"EditorValidation.txt",results);
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            if(mouse!=null)InputSystem.RemoveDevice(mouse);
            EditorApplication.isPlaying=false;
        }
    }
}
#endif
