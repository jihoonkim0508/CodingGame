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
    [InitializeOnLoad]
    public static class WideRoadUpgrade
    {
        const string Request="Logs/MapPainter/ApplyWideRoads.request";
        static double next;
        static WideRoadUpgrade(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            next=EditorApplication.timeSinceStartup+1;
            if(!File.Exists(Request))return;File.Delete(Request);
            try{Apply();}catch(Exception e){File.WriteAllText(MapPainterAssets.Report("WideRoadUpgrade.txt"),e+"\nRESULT: FAIL");Debug.LogException(e);}
        }
        [MenuItem("Tools/Art/Modular Map/Widen Existing Roads to 150 Percent")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode before changing map assets.");
            MapBuildPreview.Stop();var report=new List<string>();
            foreach(string name in new[]{"Stage01 Workshop","Stage01_v2"})
            {
                string path=MapPainterAssets.MapPrefabs+"/"+name+".prefab";
                if(!AssetDatabase.LoadAssetAtPath<GameObject>(path))continue;
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var map in root.GetComponentsInChildren<ModularMapRoot>(true))Update(map,false);
                    PrefabUtility.SaveAsPrefabAsset(root,path);report.Add("Updated prefab, source models retained: "+path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            Scene active=SceneManager.GetActiveScene();
            foreach(string path in new[]{"Assets/!_Project/Scenes/Defense.unity",MapPainterAssets.ArtScene})
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                bool wasDirty=scene.isDirty;
                try
                {
                    int count=0;
                    foreach(var go in scene.GetRootGameObjects())foreach(var map in go.GetComponentsInChildren<ModularMapRoot>(true))
                    {Update(map,!opened);count++;}
                    if(count==0)continue;
                    EditorSceneManager.MarkSceneDirty(scene);
                    if(!wasDirty){EditorSceneManager.SaveScene(scene);report.Add("Saved scene (no prior unsaved edits): "+path);}
                    else report.Add("Updated open scene in memory; prior unsaved edits retained. Save with Ctrl+S: "+path);
                }
                finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
            }
            if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);
            // Retain earlier cached meshes for existing scene/Undo references,
            // but normalize their editor-only main-object labels.
            string cache=MapPainterAssets.Generated+"/WideRoads";
            foreach(string guid in AssetDatabase.FindAssets("t:Mesh",new[]{cache}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var mesh=AssetDatabase.LoadMainAssetAtPath(path) as Mesh;
                string expected=Path.GetFileNameWithoutExtension(path);
                if(mesh&&mesh.name!=expected){mesh.name=expected;EditorUtility.SetDirty(mesh);}
            }
            AssetDatabase.SaveAssets();SceneView.RepaintAll();
            report.Add("Road width x1.5. Grid, floor transforms, route coordinates, camera and prop transforms unchanged. Original FBX/prefab connections retained.");
            report.Add("RESULT: PASS");File.WriteAllLines(MapPainterAssets.Report("WideRoadUpgrade.txt"),report);
        }
        static void Update(ModularMapRoot map,bool undo)
        {
            int size=map.cellSize;var position=map.transform.position;var scale=map.transform.localScale;
            var points=map.routes.SelectMany(r=>map.Waypoints(r)).ToArray();
            if(undo)Undo.RecordObject(map,"Widen roads only");map.roadWidthScale=1.5f;
            WideRoadAssets.Apply(map,undo);map.ApplyFloorColors();EditorUtility.SetDirty(map);
            PrefabUtility.RecordPrefabInstancePropertyModifications(map);
            if(map.cellSize!=size||map.transform.position!=position||map.transform.localScale!=scale||!points.SequenceEqual(map.routes.SelectMany(r=>map.Waypoints(r))))
                throw new InvalidOperationException("Road-only upgrade changed map spacing or route centers.");
        }
    }
}
