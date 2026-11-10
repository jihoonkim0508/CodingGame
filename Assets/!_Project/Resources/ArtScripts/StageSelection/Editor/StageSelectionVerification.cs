using System;
using System.IO;
using System.Linq;
using CodingGame.StageMap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace CodingGame.Art.StageSelection.Editor
{
    [InitializeOnLoad]
    public static class StageSelectionVerification
    {
        static string lastState;
        static StageSelectionVerification()
        {
            if(Application.productName=="CodingGameStageSelectionVerification"&&Application.dataPath.Replace('\\','/').Contains("/StageSelectionVerification/UnitySandbox/")&&!AssetDatabase.IsAssetImportWorkerProcess())
                EditorApplication.update+=Pump;
        }
        static void Pump()
        {
            string state="playing="+EditorApplication.isPlaying+"; paused="+EditorApplication.isPaused+"; changing="+EditorApplication.isPlayingOrWillChangePlaymode;
            if(state!=lastState){Directory.CreateDirectory(Reports);File.AppendAllText(System.IO.Path.Combine(Reports,"PlayStates.txt"),state+"\n");lastState=state;}
            // Batch editors have no focused Game view. Keep their isolated test
            // player loop moving and avoid error-pause from Unity's search UI.
            if(EditorApplication.isPaused)EditorApplication.isPaused=false;
            EditorApplication.QueuePlayerLoopUpdate();
        }
        public static string Reports=>Environment.GetEnvironmentVariable("CODINGGAME_STAGE_REPORTS")??"Logs/StageSelection/Verification";
        public static void BuildStandalone()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/StageSelectionVerification/UnitySandbox/")||Application.productName!="CodingGameStageSelectionVerification")
                throw new InvalidOperationException("Standalone verification build requires the isolated project.");
            try
            {
                PlayerSettings.runInBackground=true;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
                var scene=EditorSceneManager.OpenScene(StageSelectionScene.Path);
                foreach(var oldProbe in scene.GetRootGameObjects().Where(g=>g.name=="Stage selection integration probe"))Object.DestroyImmediate(oldProbe);
                var probe=Type.GetType("StageSelectionPlayProbe, Assembly-CSharp");if(probe==null)throw new Exception("Standalone probe missing.");
                new GameObject("Stage selection integration probe").AddComponent(probe);EditorSceneManager.SaveScene(scene);
                string output=System.IO.Path.GetFullPath(System.IO.Path.Combine(Reports,"../Player/StageSelectionCheck.exe"));Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
                var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{StageSelectionScene.Path,"Assets/!_Project/Scenes/Defense.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                File.WriteAllText(System.IO.Path.Combine(Reports,"StandaloneBuild.txt"),"Result: "+build.summary.result+"\nErrors: "+build.summary.totalErrors+"\nOutput: "+output);
                EditorApplication.Exit(build.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded?0:1);
            }
            catch(Exception e){File.WriteAllText(System.IO.Path.Combine(Reports,"StandaloneBuild.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void RunBatch()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/StageSelectionVerification/UnitySandbox/")||Application.productName!="CodingGameStageSelectionVerification")
                throw new InvalidOperationException("This probe requires the isolated stage-selection test project and isolated PlayerPrefs product name.");
            Directory.CreateDirectory(Reports);
            try
            {
                PlayerSettings.runInBackground=true;Application.runInBackground=true;EditorApplication.isPaused=false;
                var scene=EditorSceneManager.OpenScene(StageSelectionScene.Path);
                foreach(var oldProbe in scene.GetRootGameObjects().Where(g=>g.name=="Stage selection integration probe"))Object.DestroyImmediate(oldProbe);
                var world=StageSelectionScene.Build(scene);var camera=Camera.main;camera.aspect=16f/9;StageSelectionScene.Frame(camera,world);
                var views=world.GetComponentsInChildren<StageIslandView>().OrderBy(v=>v.stageNumber).ToArray();
                if(views.Length!=3||!views.Select(v=>v.stageTitle).SequenceEqual(StageSelectionModels.Titles))throw new Exception("Expected exactly the three requested stage themes.");
                var selectors=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.name=="Stage").ToArray();
                if(selectors.Length!=3)throw new Exception("Legacy greybox is still discoverable as a stage.");
                int triangles=0;
                foreach(var filter in world.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=filter.sharedMesh;
                    if(!mesh||!EditorUtility.IsPersistent(mesh)||mesh.normals.Any(n=>!float.IsFinite(n.x)||n.sqrMagnitude<.5f))throw new Exception("Invalid or nonpersistent mesh: "+filter.name);
                    triangles+=mesh.triangles.Length/3;
                }
                foreach(var renderer in world.GetComponentsInChildren<MeshRenderer>())foreach(var material in renderer.sharedMaterials)
                    if(!material||!material.shader||ShaderUtil.ShaderHasError(material.shader))throw new Exception("Invalid stage material on "+renderer.name);
                Physics.SyncTransforms();
                foreach(var view in views)
                {
                    var ray=camera.ScreenPointToRay(camera.WorldToScreenPoint(view.transform.position+Vector3.up*.7f));
                    if(!Physics.Raycast(ray,out var hit)||hit.collider.GetComponentInParent<StageIslandView>()!=view)throw new Exception("Island click collider cannot be hit: "+view.stageTitle);
                }
                foreach(float aspect in new[]{16f/9,16f/10,4f/3})
                {
                    camera.aspect=aspect;StageSelectionScene.Frame(camera,world);
                    foreach(var view in views)
                    {
                        var b=view.VisualBounds;
                        for(int i=0;i<8;i++){var p=camera.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(p.z<=0||p.x<0||p.x>1||p.y<0||p.y>1)throw new Exception("Island clipped at aspect "+aspect);}
                    }
                }
                camera.aspect=16f/9;StageSelectionScene.Frame(camera,world);StageSelectionScene.Capture(camera,System.IO.Path.Combine(Reports,"Stage_Overview.png"));
                var originalPosition=camera.transform.position;var originalRotation=camera.transform.rotation;
                for(int i=0;i<3;i++)
                {
                    var bounds=views[i].VisualBounds;camera.aspect=4f/3;camera.fieldOfView=35;var rotation=Quaternion.Euler(32,-18,0);
                    camera.transform.SetPositionAndRotation(bounds.center-rotation*Vector3.forward*StageCameraController.FrameDistance(camera,bounds,rotation),rotation);
                    StageSelectionScene.Capture(camera,System.IO.Path.Combine(Reports,"Stage_0"+(i+1)+".png"),1200,900);
                }
                camera.fieldOfView=40;camera.aspect=16f/9;camera.transform.SetPositionAndRotation(originalPosition,originalRotation);
                EditorSceneManager.SaveScene(scene);
                scene=EditorSceneManager.OpenScene(StageSelectionScene.Path);
                if(Object.FindObjectsByType<StageIslandView>(FindObjectsSortMode.None).Length!=3)throw new Exception("Scene round-trip lost an island.");
                File.WriteAllText(System.IO.Path.Combine(Reports,"StaticValidation.txt"),"PASS: exactly three themes in order; persistent models and materials; original layout inactive; collider raycasts; 16:9, 16:10 and 4:3 framing; saved scene reload.\nTriangles: "+triangles+"\nRESULT: PASS");
                var probe=Type.GetType("StageSelectionPlayProbe, Assembly-CSharp");if(probe==null)throw new Exception("Test-only runtime probe missing.");
                new GameObject("Stage selection integration probe").AddComponent(probe);EditorSceneManager.SaveScene(scene);
                // Enter after startup delay callbacks (including Unity's search
                // indexer), rather than competing with them in executeMethod.
                double after=EditorApplication.timeSinceStartup+2;
                void Enter()
                {
                    if(EditorApplication.timeSinceStartup<after||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                    EditorApplication.update-=Enter;File.WriteAllText(System.IO.Path.Combine(Reports,"PlayRequest.txt"),"Entering Play; isPlaying="+EditorApplication.isPlaying+"\n");EditorApplication.EnterPlaymode();
                }
                EditorApplication.update+=Enter;
            }
            catch(Exception e){File.WriteAllText(System.IO.Path.Combine(Reports,"Failure.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
