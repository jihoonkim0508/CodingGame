using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodingGame.Art.MapPainter.Editor
{
    public static class MapPainterSetup
    {
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
                var root=MapPainterEditing.FindMap(scene);
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
                log.Add("RESULT: PASS");
                MapPainterVerification.Run(palette);
            }
            catch(Exception ex) { log.Add(ex.ToString()); log.Add("RESULT: FAIL"); Debug.LogException(ex); }
            finally { File.WriteAllLines(MapPainterAssets.Report("Setup.txt"),log); }
        }
    }
}
