using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodingGame.Art.MapPainter.Editor;
using CodingGame.StageMap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CodingGame.Art.StageSelection.Editor
{
    [InitializeOnLoad]
    public static class StageSelectionScene
    {
        public const string Path="Assets/!_Project/Scenes/Stage.unity";
        public const string WorldPrefab="Assets/!_Project/Resources/ArtPrefabs/StageSelectionWorld.prefab";
        const string WorldName="Stage Selection World";
        const string Request="Logs/StageSelection/Apply.request";
        static double next;
        static StageSelectionScene(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request))return;File.Delete(Request);
            try{Apply();}catch(Exception e){Report(e+"\nRESULT: FAIL");Debug.LogException(e);}
        }
        static void Report(string text){Directory.CreateDirectory("Logs/StageSelection");File.WriteAllText("Logs/StageSelection/Apply.txt",text);}
        [MenuItem("Tools/Art/Stage Selection/Build Three Theme Islands")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode before rebuilding the stage map.");
            var active=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(Path);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(Path,OpenSceneMode.Additive);
            bool dirty=scene.isDirty;
            try
            {
                SceneManager.SetActiveScene(scene);
                Build(scene);
                if(!dirty)EditorSceneManager.SaveScene(scene);
                Report("Stage scene: "+Path+"\nExactly three islands: "+string.Join(" -> ",StageSelectionModels.Titles)+"\nModels: "+StageSelectionModels.Root+"\nWorld prefab: "+WorldPrefab+"\nOriginal greybox retained under inactive StageSelection_OriginalLayout.\n"+(dirty?"Stage had unsaved edits; changes retained in memory. Save with Ctrl+S.":"Stage scene saved.")+"\nRESULT: PASS");
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}
            SceneView.RepaintAll();
        }
        public static GameObject Build(Scene scene)
        {
            if(scene.path!=Path)throw new InvalidOperationException("Only the Stage scene may be edited.");
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.52f,.62f,.77f);
            RenderSettings.ambientEquatorColor=new Color(.27f,.34f,.43f);RenderSettings.ambientGroundColor=new Color(.12f,.16f,.23f);
            StageSelectionModels.Bake();
            var roots=scene.GetRootGameObjects();
            var old=roots.FirstOrDefault(g=>g.name==WorldName);if(old)Undo.DestroyObjectImmediate(old);
            var retained=roots.FirstOrDefault(g=>g.name=="StageSelection_OriginalLayout");
            if(!retained){retained=new GameObject("StageSelection_OriginalLayout");SceneManager.MoveGameObjectToScene(retained,scene);Undo.RegisterCreatedObjectUndo(retained,"Preserve original stage layout");}
            foreach(var go in roots.Where(g=>g&&(g.name=="StageGround"||g.name=="Road"||g.name=="Stage")&&!g.GetComponent<StageIslandView>()))Undo.SetTransformParent(go.transform,retained.transform,"Preserve original stage model");
            Undo.RecordObject(retained,"Hide original stage greybox");retained.SetActive(false);
            var world=new GameObject(WorldName);SceneManager.MoveGameObjectToScene(world,scene);Undo.RegisterCreatedObjectUndo(world,"Create three theme islands");
            var positions=new[]{new Vector3(-7.2f,.85f,0),new Vector3(0,.85f,.8f),new Vector3(7.2f,.85f,0)};
            for(int i=0;i<3;i++)
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(StageSelectionModels.Root+"/Prefabs/"+StageSelectionModels.Names[i]+".prefab");
                var island=(GameObject)PrefabUtility.InstantiatePrefab(source,world.transform);island.name="Stage";island.transform.localPosition=positions[i];
            }
            var bridges=new GameObject("Connecting Circuit Walkways");bridges.transform.SetParent(world.transform,false);
            var bridgePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(StageSelectionModels.Root+"/Prefabs/CircuitBridge.prefab");
            for(int bridge=0;bridge<2;bridge++)
            {
                Vector3 from=positions[bridge]+new Vector3(2.55f,0,-1.15f),to=positions[bridge+1]+new Vector3(-2.55f,0,-1.15f);
                // A single clear connection between adjacent islands. Overlap the
                // landing ports slightly so the bobbing never opens a visible gap.
                var center=(from+to)*.5f;var delta=to-from;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(bridgePrefab,bridges.transform);go.name="Bridge "+(bridge+1)+" to "+(bridge+2);
                go.transform.localPosition=center;go.transform.localRotation=Quaternion.Euler(0,-Mathf.Atan2(delta.z,delta.x)*Mathf.Rad2Deg,0);go.transform.localScale=new Vector3(delta.magnitude+.18f,1,1);
            }
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).FirstOrDefault(c=>c.CompareTag("MainCamera"));
            if(!camera)throw new InvalidOperationException("Stage scene's Main Camera is missing.");
            Undo.RecordObject(camera,"Frame island stage selection");Undo.RecordObject(camera.transform,"Frame island stage selection");Frame(camera,world);
            var sun=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).FirstOrDefault(l=>l.type==LightType.Directional);
            if(sun){Undo.RecordObject(sun,"Light stage islands");Undo.RecordObject(sun.transform,"Light stage islands");sun.transform.rotation=Quaternion.Euler(43,-32,0);sun.color=new Color(1,.95f,.85f);sun.intensity=1.25f;sun.shadows=LightShadows.Soft;sun.shadowBias=.04f;sun.shadowNormalBias=.25f;}
            var atmosphere=new GameObject("Island Atmosphere");atmosphere.transform.SetParent(world.transform,false);
            var volume=atmosphere.AddComponent<Volume>();volume.isGlobal=true;volume.priority=1;volume.sharedProfile=Profile();
            var additional=camera.GetUniversalAdditionalCameraData();additional.renderPostProcessing=true;
            PrefabUtility.SaveAsPrefabAsset(world,WorldPrefab);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            return world;
        }
        static VolumeProfile Profile()
        {
            string path=StageSelectionModels.Root+"/Materials/IslandAtmosphere.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(profile)return profile;
            profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="IslandAtmosphere";AssetDatabase.CreateAsset(profile,path);
            var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.22f);bloom.threshold.Override(1.1f);bloom.scatter.Override(.55f);
            AssetDatabase.AddObjectToAsset(bloom,profile);EditorUtility.SetDirty(profile);return profile;
        }
        public static void Frame(Camera camera,GameObject world)
        {
            camera.orthographic=false;camera.fieldOfView=40;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=StageSelectionModels.Hex("0D2033");
            camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.rect=new Rect(0,0,1,1);
            var views=world.GetComponentsInChildren<StageIslandView>();var bounds=views[0].VisualBounds;foreach(var view in views)bounds.Encapsulate(view.VisualBounds);
            var rotation=Quaternion.Euler(42,0,0);camera.transform.SetPositionAndRotation(bounds.center-rotation*Vector3.forward*StageCameraController.FrameDistance(camera,bounds,rotation),rotation);
        }
        public static void Capture(Camera camera,string path,int width=1920,int height=1080)
        {
            var target=new RenderTexture(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGB24,false);var previous=RenderTexture.active;var old=camera.targetTexture;float aspect=camera.aspect;
            try{camera.aspect=(float)width/height;camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=old;camera.aspect=aspect;RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);}
        }
    }
}
