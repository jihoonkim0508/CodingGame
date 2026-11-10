#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CodingGame.StageMap;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CodingGame.TitleScreen
{
    public sealed class TitleMouseMenuProbe : MonoBehaviour
    {
        const string Reports="C:/Users/user/Documents/ChatGPT/CodingGame/TitleScreenWork/MouseMenuReports/";
        readonly List<string> results=new List<string>();
        Mouse mouse;Keyboard keyboard;Gamepad gamepad;
        bool finished;
        float began;
        public static string Begin()
        {
            if(!EditorApplication.isPlaying||EditorApplication.isPaused)throw new InvalidOperationException("Running Play mode required");
            if(FindFirstObjectByType<TitleMouseMenuProbe>())return "Already running";
            DontDestroyOnLoad(new GameObject("Temporary Mouse Menu QA").AddComponent<TitleMouseMenuProbe>().gameObject);
            Application.runInBackground=true;return "Mouse-only menu and projectile render QA started";
        }
        IEnumerator Start()
        {
            began=Time.realtimeSinceStartup;Directory.CreateDirectory(Reports);
            var run=Run();
            while(true){bool more;try{more=run.MoveNext();}catch(Exception error){Finish(error.ToString());yield break;}if(!more)break;yield return run.Current;}
            Finish(null);
        }
        void Update(){if(!finished&&began>0&&Time.realtimeSinceStartup-began>45)Finish("Verification timeout");}
        void Check(bool condition,string message){if(!condition)throw new Exception(message);results.Add("PASS: "+message);}
        IEnumerator Run()
        {
            var title=FindFirstObjectByType<TitlePresentation>();
            mouse=InputSystem.AddDevice<Mouse>("TitleMouseMenuProbeMouse");
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(20,20)});
            while(!title.IsReady)yield return null;
            yield return null;yield return null;yield return null;
            var events=EventSystem.current;var module=events.GetComponent<InputSystemUIInputModule>();
            Check(title.startButton.navigation.mode==Navigation.Mode.None&&title.quitButton.navigation.mode==Navigation.Mode.None,"Both buttons disable directional navigation");
            Check(!events.sendNavigationEvents&&module.move==null&&module.submit==null&&module.cancel==null,"Title move/submit/cancel actions are disabled");
            Check(module.point!=null&&module.leftClick!=null,"Mouse pointing and left-click actions remain bound");
            Check(events.currentSelectedGameObject==null&&events.firstSelectedGameObject==null,"No automatic or initial button selection");
            keyboard=InputSystem.AddDevice<Keyboard>("TitleMouseMenuProbeKeyboard");
            foreach(var key in new[]{Key.DownArrow,Key.UpArrow,Key.LeftArrow,Key.RightArrow,Key.Tab,Key.Enter,Key.Space})
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
                Check(events.currentSelectedGameObject==null&&SceneManager.GetActiveScene().name=="Title"&&title.startButton.interactable,"Keyboard "+key+" neither selects nor activates a menu button");
            }
            gamepad=InputSystem.AddDevice<Gamepad>("TitleMouseMenuProbeGamepad");
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.DpadDown).WithButton(GamepadButton.South));yield return null;
            InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return null;yield return null;
            Check(events.currentSelectedGameObject==null&&SceneManager.GetActiveScene().name=="Title"&&title.startButton.interactable,"Gamepad direction/confirm do not activate menu");
            Vector2 point=RectTransformUtility.WorldToScreenPoint(null,title.startButton.transform.position);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});
            yield return new WaitForSecondsRealtime(.3f);
            Check(title.startButton.transform.localScale.x>1.01f,"Mouse hover animation still responds");
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(20,20)});
            yield return new WaitForSecondsRealtime(.3f);
            yield return new WaitForEndOfFrame();Save("01_MouseOnly_Menu.png");
            var effects=FindFirstObjectByType<TitleProjectileEffect>();var turret=effects.turret;
            Check(!effects.raycastTarget&&effects.transform.IsChildOf(title.battlefieldRect),"Projectile layer stays behind menu and never intercepts clicks");
            float deadline=Time.realtimeSinceStartup+10;
            while((turret.ShotPhase<.42f||turret.ShotPhase>.49f)&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return new WaitForEndOfFrame();
            Check(effects.VisibleProjectiles>=2,"Burst creates multiple travelling projectiles");
            Save("02_Projectile_Burst.png");
            deadline=Time.realtimeSinceStartup+10;
            while(effects.ImpactIntensity<.25f&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return new WaitForEndOfFrame();
            Check(effects.ImpactIntensity>0,"Projectile impact sparks are rendered");Save("03_Impact.png");
            point=RectTransformUtility.WorldToScreenPoint(null,title.startButton.transform.position);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=0});yield return null;
            deadline=Time.realtimeSinceStartup+10;
            while(SceneManager.GetActiveScene().name!="Stage"&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(SceneManager.GetActiveScene().name=="Stage","Actual mouse click still enters Stage through GameFlow");
        }
        static void Save(string name)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(Reports+name,texture.EncodeToPNG());}finally{Destroy(texture);}
        }
        void Finish(string error)
        {
            if(finished)return;finished=true;if(error!=null)results.Add("FAIL: "+error);
            results.Add("RESULT: "+(error==null?"PASS":"FAIL"));File.WriteAllLines(Reports+"EditorValidation.txt",results);
            if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(gamepad!=null)InputSystem.RemoveDevice(gamepad);
            EditorApplication.isPlaying=false;
        }
    }
}
#endif
