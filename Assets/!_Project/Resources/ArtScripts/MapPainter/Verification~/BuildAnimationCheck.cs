using System;
using System.IO;
using System.Linq;
using CodingGame.Art.MapPainter;
using CodingGame.Art.MapPainter.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BuildAnimationCheck
{
    const string Active="MapBuildCheck.Active",Phase="MapBuildCheck.Phase";
    const string Menu=MapPainterAssets.Generated+"/BuildMenu.unity";
    const string Prefab=MapPainterAssets.Generated+"/BuildRuntime.prefab";
    static double phaseTime;
    static MapBuildSequence sequence;
    static int completions;
    static BuildAnimationCheck(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    static void Assert(bool okay,string text){if(!okay)throw new Exception("Build check: "+text);}
    static void Log(string text){File.AppendAllText(MapPainterAssets.Root+"/Reports/BuildRuntimeValidation.txt",text+"\n");}
    public static void Run()
    {
        MapBuildVerification.Run();
        Assert(File.ReadAllText(MapPainterAssets.Root+"/Reports/BuildValidation.txt").Contains("RESULT: PASS"),"sampler validation passes");
        var menu=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.SaveScene(menu,Menu);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=MapPainterEditing.CreateRoot(scene,MapPainterAssets.Setup(),false);
        MapPainterEditing.Paint(root,from x in Enumerable.Range(-1,3) from z in Enumerable.Range(-1,3) select new GridCell(x,z),false,false,false);
        MapPainterEditing.Paint(root,new[]{new GridCell(-1,0),new GridCell(0,0),new GridCell(0,1),new GridCell(1,1)},true,false,false);
        root.GetComponent<MapBuildSequence>().duration=.7f;
        var go=new GameObject("Main Camera");go.tag="MainCamera";var camera=go.AddComponent<Camera>();MapPainterEditing.FitCamera(camera,MapPainterEditing.MapBounds(root),55,false);BlueprintBackdropAssets.Ensure(root,camera,false);
        MapPainterAssets.EnsureFolder("Assets/!_Project/Scenes");EditorSceneManager.SaveScene(scene,MapPainterAssets.ArtScene);
        File.WriteAllText(MapPainterAssets.Root+"/Reports/BuildRuntimeValidation.txt","Unity runtime and editor preview validation\n");
        var markers=root.GetComponentsInChildren<MapTileMarker>();var positions=markers.Select(m=>m.transform.localPosition).ToArray();
        for(int repeat=0;repeat<3;repeat++)
        {
            Assert(!scene.isDirty,"clean before preview");MapBuildPreview.Start(root.GetComponent<MapBuildSequence>());
            Assert(MapBuildPreview.IsRunning&&AnimationMode.InAnimationMode(),"editor preview actually starts");
            root.GetComponent<MapBuildSequence>().SamplePreview(.4f);MapBuildPreview.Stop();
            Assert(!AnimationMode.InAnimationMode()&&!scene.isDirty,"editor stop does not dirty clean scene");
            for(int i=0;i<markers.Length;i++)Assert(markers[i].transform.localPosition==positions[i],"editor transform restoration");
        }
        MapBuildPreview.Start(root.GetComponent<MapBuildSequence>());root.GetComponent<MapBuildSequence>().SamplePreview(.4f);EditorSceneManager.SaveScene(scene);
        Assert(!MapBuildPreview.IsRunning&&!AnimationMode.InAnimationMode(),"save stops preview");
        for(int i=0;i<markers.Length;i++)Assert(markers[i].transform.localPosition==positions[i],"saved final pose restored");
        Log("PASS: real editor AnimationMode repeated preview/stop, clean scene preserved and save-time restoration.");
        PrefabUtility.SaveAsPrefabAsset(root.gameObject,Prefab);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(MapPainterAssets.ArtScene,true),new EditorBuildSettingsScene(Menu,true)};
        EditorSettings.enterPlayModeOptionsEnabled=false;
        SessionState.SetBool(Active,true);SessionState.SetInt(Phase,0);SessionState.SetFloat("MapBuildCheck.Deadline",(float)EditorApplication.timeSinceStartup+90);
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Active,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            sequence=UnityEngine.Object.FindFirstObjectByType<MapBuildSequence>();phaseTime=EditorApplication.timeSinceStartup;completions=0;
            if(sequence)sequence.Completed+=()=>completions++;
        }
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            int stage=SessionState.GetInt(Phase,0);
            if(stage==7)
            {
                EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
                SessionState.SetInt(Phase,8);EditorApplication.delayCall+=()=>EditorApplication.EnterPlaymode();
            }
            else if(stage==9){Log("RESULT: PASS");SessionState.SetBool(Active,false);EditorApplication.Exit(0);}
            else if(stage<0){SessionState.SetBool(Active,false);EditorApplication.Exit(1);}
        }
    }
    static void Next(int phase){SessionState.SetInt(Phase,phase);phaseTime=EditorApplication.timeSinceStartup;}
    static void Tick()
    {
        if(!SessionState.GetBool(Active,false))return;
        try
        {
            Assert(EditorApplication.timeSinceStartup<SessionState.GetFloat("MapBuildCheck.Deadline",0),"runtime timeout");
            if(!EditorApplication.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isPlaying)return;
            int phase=SessionState.GetInt(Phase,0);double elapsed=EditorApplication.timeSinceStartup-phaseTime;
            switch(phase)
            {
                case 0:
                    if(elapsed<.15)break;
                    Log("Initial state: sequence="+(sequence?sequence.name:"null")+"; anim="+(sequence&&sequence.IsAnimating)+"; enabled="+(sequence&&sequence.isActiveAndEnabled)+"; auto="+(sequence&&sequence.playOnStart)+"; playing="+Application.isPlaying+"; paused="+EditorApplication.isPaused+"; scene="+SceneManager.GetActiveScene().path+"; maps="+UnityEngine.Object.FindObjectsByType<MapBuildSequence>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
                    Assert(sequence&&sequence.HasPlayedThisEntry&&sequence.StartedCount==1,"automatic first entry animation");
                    sequence.PlayStageEntry();Time.timeScale=0;Next(1);break;
                case 1:
                    if(elapsed<1)break;
                    Assert(!sequence.IsAnimating&&sequence.StartedCount==2,"unscaled time completes once while timeScale is zero");Time.timeScale=1;
                    Assert(!sequence.TryPlayOnce(),"duplicate invocation skipped");sequence.gameObject.SetActive(false);sequence.gameObject.SetActive(true);
                    Assert(!sequence.IsAnimating,"retry activation skipped");
                    var clone=PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab)) as GameObject;
                    Assert(!clone.GetComponent<MapBuildSequence>().IsAnimating,"reloaded same map prefab skipped for retry");UnityEngine.Object.Destroy(clone);
                    Log("PASS: automatic stage entry, unscaled completion, duplicate/re-enable/prefab retry suppression.");
                    sequence.PlayStageEntry();Assert(sequence.IsAnimating,"fresh selection of same stage replays");Next(2);break;
                case 2:
                    if(elapsed<1)break;
                    Assert(!sequence.IsAnimating&&sequence.StartedCount==3,"fresh entry completed");
                    Log("Before retry: scene="+SceneManager.GetActiveScene().path+"; lastScene="+typeof(MapBuildSequence).GetField("lastScene",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null));
                    SceneManager.LoadScene(MapPainterAssets.ArtScene);Next(3);break;
                case 3:
                    if(elapsed<.2)break;
                    sequence=UnityEngine.Object.FindFirstObjectByType<MapBuildSequence>();Log("After retry: starts="+(sequence?sequence.StartedCount:-1)+"; key="+(sequence?sequence.PlaybackKey:"null"));Assert(sequence&&!sequence.IsAnimating&&sequence.StartedCount==0,"same scene retry skips animation");
                    Log("PASS: selecting the same stage again replays, same-scene reload retry skips.");
                    SceneManager.LoadScene(Menu);Next(4);break;
                case 4:
                    if(elapsed<.2)break;SceneManager.LoadScene(MapPainterAssets.ArtScene);Next(5);break;
                case 5:
                    if(elapsed<.1)break;
                    sequence=UnityEngine.Object.FindFirstObjectByType<MapBuildSequence>();Assert(sequence&&sequence.HasPlayedThisEntry&&sequence.StartedCount==1,"return from stage selection scene replays");Next(6);break;
                case 6:
                    if(elapsed<1)break;Assert(!sequence.IsAnimating,"return entry completes");
                    Log("PASS: selection scene return replays stage entry.");Next(7);EditorApplication.ExitPlaymode();break;
                case 8:
                    if(elapsed<1)break;
                    Assert(sequence&&!sequence.IsAnimating&&sequence.StartedCount==1,"new play with domain/scene reload disabled replays once");
                    Log("PASS: new Play resets entry state with domain reload and scene reload disabled.");Next(9);EditorApplication.ExitPlaymode();break;
            }
        }
        catch(Exception ex)
        {
            Log(ex+"\nRESULT: FAIL");SessionState.SetInt(Phase,-1);
            if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();else{SessionState.SetBool(Active,false);EditorApplication.Exit(1);}
        }
    }
}
