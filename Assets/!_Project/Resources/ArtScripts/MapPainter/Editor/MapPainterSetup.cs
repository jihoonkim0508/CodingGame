using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodingGame.Art.MapPainter.Editor
{
    // A narrow, local setup/validation request. It never executes arbitrary code or commands.
    [InitializeOnLoad]
    public static class MapPainterSetup
    {
        private const string Request=MapPainterAssets.Root+"/Setup.request.txt";
        private const string UpgradeRequest=MapPainterAssets.Root+"/Upgrade.request.txt";
        private const string SurfaceRequest=MapPainterAssets.Root+"/SurfaceV2.request.txt";
        private const string BufferRequest=MapPainterAssets.Root+"/BufferRefresh.request.txt";
        private const string BlueprintRequest=MapPainterAssets.Root+"/Blueprint.request.txt";
        private const string BuildRequest=MapPainterAssets.Root+"/BuildAnimation.request.txt";
        private const string BranchRequest=MapPainterAssets.Root+"/RoadBranch.request.txt";
        private const string ShadingRequest=MapPainterAssets.Root+"/RoadShading.request.txt";
        private static double nextCheck;
        static MapPainterSetup() { nextCheck=EditorApplication.timeSinceStartup+3; EditorApplication.update+=Poll; }
        private static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextCheck)return;
            nextCheck=EditorApplication.timeSinceStartup+.5;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            string path=File.Exists(ShadingRequest)?ShadingRequest:File.Exists(BranchRequest)?BranchRequest:File.Exists(BuildRequest)?BuildRequest:File.Exists(BlueprintRequest)?BlueprintRequest:File.Exists(BufferRequest)?BufferRequest:File.Exists(SurfaceRequest)?SurfaceRequest:File.Exists(UpgradeRequest)?UpgradeRequest:Request;
            if(!File.Exists(path))return;
            if(!AssetDatabase.IsValidFolder("Assets"))
            {
                if(Directory.Exists(MapPainterAssets.Root+"/Reports"))File.WriteAllText(MapPainterAssets.Root+"/Reports/EditorState.txt","Asset database not ready; dataPath="+Application.dataPath+"; cwd="+Directory.GetCurrentDirectory()+"; scene="+SceneManager.GetActiveScene().path);
                nextCheck=EditorApplication.timeSinceStartup+10; return;
            }
            string command=File.ReadAllText(path).Trim();
            if(command!="setup"&&command!="upgrade"&&command!="verify"&&command!="surface"&&command!="buffers"&&command!="blueprint"&&command!="build"&&command!="branch"&&command!="shading")return;
            File.Delete(path);
            if(command=="setup") SetupScene();
            else if(command=="upgrade") UpgradeEditor();
            else if(command=="verify")MapPainterVerification.Run(MapPainterAssets.Setup());
            else if(command=="surface")UpgradeSurface();
            else if(command=="branch")RoadBranchUpgrade.ApplyToOpenScene();
            else if(command=="shading")
            {
                try
                {
                    if(SceneManager.GetActiveScene().path!=MapPainterAssets.ArtScene)throw new InvalidOperationException("Open ArtTestScene first.");
                    RoadShadingVerification.Validate(MapPainterAssets.Setup());
                    RoadBranchUpgrade.ApplyToOpenScene("ShadingSetup.txt");
                }
                catch(Exception ex){Debug.LogException(ex);File.WriteAllText(MapPainterAssets.Root+"/Reports/ShadingSetup.txt",ex+"\nRESULT: FAIL");}
            }
            else if(command=="build")
            {
                try {MapBuildAssets.ApplyToOpenScene();SceneView.RepaintAll();}
                catch(Exception ex){Debug.LogException(ex);File.WriteAllText(MapPainterAssets.Root+"/Reports/BuildSetup.txt",ex+"\nRESULT: FAIL");}
            }
            else if(command=="blueprint")
            {
                try { BlueprintBackdropAssets.ApplyToOpenScene(); SceneView.RepaintAll(); }
                catch(Exception ex) { Debug.LogException(ex); File.WriteAllText(MapPainterAssets.Root+"/Reports/BlueprintSetup.txt",ex+"\nRESULT: FAIL"); }
            }
            else if(command=="buffers")
            {
                MapPainterAssets.RefreshMeshBuffers();
                MapPainterVerification.Run(MapPainterAssets.Setup()); SceneView.RepaintAll();
                File.WriteAllText(MapPainterAssets.Root+"/Reports/BufferRefresh.txt","Generated meshes rebuilt through Mesh setters and uploaded to GPU. Original FBX unchanged.");
            }
        }
        public static void UpgradeSurface()
        {
            var log=new List<string>();
            try
            {
                var palette=MapPainterAssets.Setup(); var scene=SceneManager.GetActiveScene();
                if(scene.path==MapPainterAssets.ArtScene)
                {
                    bool wasDirty=scene.isDirty;
                    foreach(var map in scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<ModularMapRoot>(true)))
                    {
                        Undo.RecordObject(map,"Upgrade map surface and size"); map.cellSize=2;
                        MapPainterEditing.RebuildAll(map); map.ApplyFloorColors();
                        ModularMapPainterWindow.Open().SetMap(map);
                        var camera=MapPainterEditing.FindCamera(scene); if(camera)MapPainterEditing.FitCamera(camera,MapPainterEditing.MapBounds(map));
                        log.Add(map.name+": cell size=2; bounds="+MapPainterEditing.MapBounds(map).size+"; colors/cells retained.");
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    if(!wasDirty)EditorSceneManager.SaveScene(scene);
                    log.Add(wasDirty?"Scene had unsaved changes; changes are in memory. Save with Ctrl+S.":"ArtTestScene updated and saved.");
                }
                MapPainterVerification.Run(palette); log.Add("RESULT: PASS");
            }
            catch(Exception ex) { log.Add(ex.ToString()); log.Add("RESULT: FAIL"); Debug.LogException(ex); }
            finally { File.WriteAllLines(MapPainterAssets.Root+"/Reports/SurfaceUpgrade.txt",log); }
        }
        [MenuItem("Tools/Art/Setup Modular Map Painter")]
        public static void SetupScene()
        {
            var log=new List<string> { "Map Painter setup; Unity "+Application.unityVersion };
            try
            {
                var palette=MapPainterAssets.Setup();
                var scene=SceneManager.GetActiveScene();
                if(scene.path!=MapPainterAssets.ArtScene)throw new InvalidOperationException("Open ArtTestScene first. No other scene will be changed.");
                bool wasDirty=scene.isDirty;
                var root=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<ModularMapRoot>(true)).FirstOrDefault();
                if(!root)root=MapPainterEditing.CreateRoot(scene,palette);
                var camera=MapPainterEditing.FindCamera(scene);
                if(!camera)
                {
                    var cameraGO=new GameObject("Art Map Camera"); SceneManager.MoveGameObjectToScene(cameraGO,scene);
                    camera=cameraGO.AddComponent<Camera>(); Undo.RegisterCreatedObjectUndo(cameraGO,"Create art camera");
                }
                MapPainterEditing.FitCamera(camera,MapPainterEditing.MapBounds(root));
                MapPainterEditing.FitSceneView(root);
                EditorSceneManager.MarkSceneDirty(scene);
                if(!wasDirty)
                {
                    EditorSceneManager.SaveScene(scene);
                    log.Add("ArtTestScene camera and empty painter root saved (scene had no pre-existing unsaved changes).");
                }
                else log.Add("ArtTestScene camera and painter root updated in memory. Save with Ctrl+S: pre-existing unsaved changes were preserved.");
                ModularMapPainterWindow.Open().SetMap(root);
                log.Add("Window: Tools > Art > Modular Map Painter");
                log.Add("Prefab output: "+MapPainterAssets.MapPrefabs);
                log.Add("Camera: orthographic, pitch=55, yaw=0, size="+camera.orthographicSize);
                log.Add("Source floor FBX and importer settings preserved.");
                log.Add("RESULT: PASS");
                MapPainterVerification.Run(palette);
            }
            catch(Exception ex) { log.Add(ex.ToString()); log.Add("RESULT: FAIL"); Debug.LogException(ex); }
            finally { File.WriteAllLines(MapPainterAssets.Root+"/Reports/Setup.txt",log); }
        }
        public static void UpgradeEditor()
        {
            var log=new List<string> { "General map editor naming and two-color upgrade" };
            try
            {
                var palette=MapPainterAssets.Setup();
                foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{MapPainterAssets.MapPrefabs}))
                {
                    string path=AssetDatabase.GUIDToAssetPath(guid); var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if(!prefab || !prefab.GetComponent<ModularMapRoot>())continue;
                    var contents=PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        if(contents.name=="Stage01 Modular Map") { contents.name="Modular Map"; PrefabUtility.SaveAsPrefabAsset(contents,path); }
                    }
                    finally { PrefabUtility.UnloadPrefabContents(contents); }
                    string name=Path.GetFileNameWithoutExtension(path);
                    if(name.StartsWith("Stage01Map",StringComparison.Ordinal))
                    {
                        string target=AssetDatabase.GenerateUniqueAssetPath(MapPainterAssets.MapPrefabs+"/ModularMap"+name.Substring("Stage01Map".Length)+".prefab");
                        string error=AssetDatabase.MoveAsset(path,target); if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
                    }
                }
                var scene=SceneManager.GetActiveScene();
                if(scene.path==MapPainterAssets.ArtScene)
                {
                    bool wasDirty=scene.isDirty;
                    var maps=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<ModularMapRoot>(true)).ToArray();
                    foreach(var map in maps)
                    {
                        if(map.name=="Stage01 Modular Map") { Undo.RecordObject(map.gameObject,"Rename modular map"); map.name="Modular Map"; }
                        map.ApplyFloorColors();
                    }
                    if(maps.Length>0)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        if(!wasDirty)EditorSceneManager.SaveScene(scene);
                        ModularMapPainterWindow.Open().SetMap(maps[0]);
                    }
                    log.Add(wasDirty?"Scene had unsaved changes: names/colors updated in memory; save with Ctrl+S.":"ArtTestScene map names saved; camera and map layout retained.");
                }
                log.Add("Original floor FBX and UV data are unchanged.");
                MapPainterVerification.Run(palette); log.Add("RESULT: PASS");
            }
            catch(Exception ex) { log.Add(ex.ToString()); log.Add("RESULT: FAIL"); Debug.LogException(ex); }
            finally { File.WriteAllLines(MapPainterAssets.Root+"/Reports/Upgrade.txt",log); }
        }
    }
}
