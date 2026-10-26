#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CodingGame.Art.StageSelection;
using CodingGame.StageMap;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public sealed class StageSelectionPlayProbe:MonoBehaviour
{
    readonly List<string> log=new List<string>();float started;Mouse mouse;
    string Reports=>Environment.GetEnvironmentVariable("CODINGGAME_STAGE_REPORTS");
    IEnumerator Start()
    {
        Debug.Log("Stage selection test probe Start; product="+Application.productName);
        if(Application.productName!="CodingGameStageSelectionVerification")throw new Exception("PlayerPrefs isolation missing.");
        DontDestroyOnLoad(gameObject);Application.runInBackground=true;started=Time.realtimeSinceStartup;
        var run=Run();while(true){bool more;try{more=run.MoveNext();}catch(Exception e){Finish(e.ToString());yield break;}if(!more)break;yield return run.Current;}Finish(null);
    }
    void Update(){if(started>0&&Time.realtimeSinceStartup-started>45)Finish("Play probe timed out.");}
    void Check(bool value,string message){if(!value)throw new Exception(message);log.Add("PASS: "+message);Debug.Log("Stage verification: "+message);}
    static void Refresh(StageMapController controller)=>typeof(StageMapController).GetMethod("RefreshNodes",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,null);
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(.8f);
        var nodes=FindObjectsByType<StageNode>(FindObjectsSortMode.None).OrderBy(n=>n.Number).ToArray();
        var controller=FindFirstObjectByType<StageMapController>();var mover=FindFirstObjectByType<StageCameraController>();
        Check(nodes.Length==3&&nodes.Select(n=>n.Number).SequenceEqual(new[]{1,2,3}),"Real GameFlow bootstraps exactly three stage nodes");
        StageProgress.ResetAll(3);StageProgress.SelectedStage=0;Refresh(controller);
        Check(nodes[0].State==StageState.Available&&nodes[1].State==StageState.Locked&&nodes[2].State==StageState.Locked,"Initial availability and locks");
        var roots=nodes.Select(n=>n.transform.position).ToArray();var visual=nodes[0].GetComponent<StageIslandView>().visualRoot;var pose=visual.localPosition;
        yield return new WaitForSecondsRealtime(.4f);Check(Vector3.Distance(pose,visual.localPosition)>.001f,"Floating island motion is active");
        Check(nodes.Select((n,i)=>Vector3.Distance(n.transform.position,roots[i])).Max()<.0001f,"Click anchors do not drift with floating art");
        Capture("Stage_Play_Default.png");
        mouse=InputSystem.AddDevice<Mouse>("StageSelectionTestMouse");yield return Click(nodes[1]);
        yield return new WaitForSecondsRealtime(.1f);Check(SceneManager.GetActiveScene().name=="Stage"&&StageProgress.SelectedStage==0&&!mover.IsMoving,"Mouse click on locked island does not enter a stage");
        StageProgress.MarkCleared(1);Refresh(controller);
        Check(nodes[0].State==StageState.Cleared&&nodes[1].State==StageState.Available&&nodes[2].State==StageState.Locked,"Clearing stage 1 unlocks only stage 2");
        var themes=nodes.Select(n=>n.GetComponent<Renderer>().sharedMaterial).ToArray();StageProgress.MarkCleared(2);Refresh(controller);
        Check(nodes[2].State==StageState.Available&&nodes.Select((n,i)=>n.GetComponent<Renderer>().sharedMaterial==themes[i]).All(v=>v),"Unlocks retain all three theme materials");
        typeof(StageMapController).GetMethod("DeveloperReset",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,null);
        Check(nodes[0].State==StageState.Available&&nodes[1].State==StageState.Locked,"Existing developer reset still works");
        StageProgress.MarkCleared(1);Refresh(controller);Capture("Stage_Play_Unlocked.png");yield return Click(nodes[1]);
        yield return new WaitForSecondsRealtime(.15f);Check(StageProgress.SelectedStage==2&&mover.IsMoving,"Actual mouse input selects stage 2 and starts camera approach");
        float deadline=Time.realtimeSinceStartup+12;while(SceneManager.GetActiveScene().name!="Defense"&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(SceneManager.GetActiveScene().name=="Defense"&&StageProgress.SelectedStage==2,"Existing camera/fade transition reaches the actual Defense scene with selected stage 2");
        yield return new WaitForSecondsRealtime(1);
    }
    IEnumerator Click(StageNode node)
    {
        var point=Camera.main.WorldToScreenPoint(node.transform.position+Vector3.up*.7f);
        yield return null;InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,point.y),buttons=1});
        yield return null;InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,point.y),buttons=0});yield return null;
    }
    void Capture(string name)
    {
        var camera=Camera.main;var rt=new RenderTexture(1920,1080,24);var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);var old=RenderTexture.active;var target=camera.targetTexture;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Reports,name),image.EncodeToPNG());}
        finally{camera.targetTexture=target;RenderTexture.active=old;rt.Release();Destroy(rt);Destroy(image);}
    }
    void Finish(string error){if(error!=null)log.Add(error);log.Add("RESULT: "+(error==null?"PASS":"FAIL"));File.WriteAllLines(Path.Combine(Reports,"PlayValidation.txt"),log);EditorApplication.Exit(error==null?0:1);}
}
#endif
