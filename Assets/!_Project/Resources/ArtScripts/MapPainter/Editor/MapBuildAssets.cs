using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodingGame.Art.MapPainter.Editor
{
    public static class MapBuildAssets
    {
        public static MapBuildSequence Ensure(ModularMapRoot map,bool undo=true)
        {
            var sequence=map.GetComponent<MapBuildSequence>();
            if(!sequence)sequence=undo?Undo.AddComponent<MapBuildSequence>(map.gameObject):map.gameObject.AddComponent<MapBuildSequence>();
            if(!sequence.outlineMaterial)
            {
                const string path=MapPainterAssets.Generated+"/MapBuildOutline.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material)
                {
                    MapPainterAssets.EnsureFolder(MapPainterAssets.Generated);
                    var shader=Shader.Find("CodingGame/Art/Map Build Outline");
                    if(!shader)throw new System.InvalidOperationException("Map Build Outline shader not imported.");
                    material=new Material(shader){name="MapBuildOutline"};AssetDatabase.CreateAsset(material,path);
                }
                if(undo)Undo.RecordObject(sequence,"Configure map build animation");
                sequence.outlineMaterial=material;
            }
            sequence.EnsureIdentity();EditorUtility.SetDirty(sequence);return sequence;
        }
        public static void ApplyToOpenScene()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=MapPainterAssets.ArtScene)throw new System.InvalidOperationException("Open ArtTestScene first.");
            MapBuildPreview.Stop();bool wasDirty=scene.isDirty;int count=0;
            foreach(var go in scene.GetRootGameObjects())foreach(var map in go.GetComponentsInChildren<ModularMapRoot>(true))
            {Ensure(map);ModularMapPainterWindow.Open().SetMap(map);count++;}
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{MapPainterAssets.MapPrefabs}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                if(!AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ModularMapRoot>())continue;
                var contents=PrefabUtility.LoadPrefabContents(path);
                try {Ensure(contents.GetComponent<ModularMapRoot>(),false);PrefabUtility.SaveAsPrefabAsset(contents,path);}
                finally {PrefabUtility.UnloadPrefabContents(contents);}
            }
            EditorSceneManager.MarkSceneDirty(scene);if(!wasDirty)EditorSceneManager.SaveScene(scene);
            MapBuildVerification.Run();
            File.WriteAllText(MapPainterAssets.Root+"/Reports/BuildSetup.txt",$"Map build animation added to {count} scene maps and saved map prefabs.\n"+(wasDirty?"Scene had unsaved changes; save with Ctrl+S.":"ArtTestScene saved.")+"\nSee BuildValidation.txt for validation.");
        }
    }
}
