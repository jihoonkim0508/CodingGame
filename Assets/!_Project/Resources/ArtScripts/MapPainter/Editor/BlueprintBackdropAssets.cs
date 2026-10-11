using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodingGame.Art.MapPainter.Editor
{
    public static class BlueprintBackdropAssets
    {
        public static BlueprintBackdrop Ensure(ModularMapRoot map,Camera camera,bool undo=true)
        {
            var backdrop=map.GetComponentInChildren<BlueprintBackdrop>(true);
            if(!backdrop)
            {
                const string path=MapPainterAssets.Generated+"/BlueprintGrid.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material)
                {
                    MapPainterAssets.EnsureFolder(MapPainterAssets.Generated);
                    var shader=Shader.Find("CodingGame/Art/Blueprint Grid");
                    if(!shader)throw new System.InvalidOperationException("Blueprint Grid shader not imported.");
                    material=new Material(shader) {name="BlueprintGrid"}; AssetDatabase.CreateAsset(material,path);
                }
                var go=GameObject.CreatePrimitive(PrimitiveType.Quad); go.name="Blueprint Background";
                SceneManager.MoveGameObjectToScene(go,map.gameObject.scene); go.transform.SetParent(map.transform,false);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<MeshRenderer>().sharedMaterial=material;
                backdrop=go.AddComponent<BlueprintBackdrop>();
                if(undo)Undo.RegisterCreatedObjectUndo(go,"Create blueprint background");
            }
            backdrop.targetCamera=camera; backdrop.Refresh(); return backdrop;
        }
        public static void ApplyToOpenScene()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=MapPainterAssets.ArtScene)throw new System.InvalidOperationException("Open ArtTestScene first.");
            bool wasDirty=scene.isDirty; var camera=MapPainterEditing.FindCamera(scene);
            foreach(var root in scene.GetRootGameObjects())foreach(var map in root.GetComponentsInChildren<ModularMapRoot>(true))
            {
                Undo.RecordObject(map,"Enable blueprint background"); map.blueprintBackground=true; Ensure(map,camera);
                ModularMapPainterWindow.Open().SetMap(map);
            }
            EditorSceneManager.MarkSceneDirty(scene); if(!wasDirty)EditorSceneManager.SaveScene(scene);
            if(camera)CaptureScene(camera);
            MapPainterVerification.Run(MapPainterAssets.Setup());
            System.IO.File.WriteAllText(MapPainterAssets.Root+"/Reports/BlueprintSetup.txt",wasDirty?"Blueprint background applied. Scene had unsaved changes: save with Ctrl+S.":"Blueprint background applied and ArtTestScene saved.");
        }
        private static void CaptureScene(Camera camera)
        {
            float aspect=camera.aspect;
            int height=Mathf.Clamp(Mathf.RoundToInt(1280/Mathf.Max(.1f,aspect)),360,1280);
            var texture=new RenderTexture(1280,height,24); var image=new Texture2D(1280,height,TextureFormat.RGB24,false);
            var original=camera.targetTexture; var active=RenderTexture.active;
            try
            {
                camera.targetTexture=texture;
                foreach(var go in camera.gameObject.scene.GetRootGameObjects())
                    foreach(var backdrop in go.GetComponentsInChildren<BlueprintBackdrop>(true))backdrop.Refresh();
                camera.Render(); RenderTexture.active=texture;
                image.ReadPixels(new Rect(0,0,1280,height),0,0); image.Apply();
                System.IO.File.WriteAllBytes(MapPainterAssets.Root+"/Reports/BlueprintScenePreview.png",image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=original; RenderTexture.active=active;
                texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(image);
            }
        }
    }
}
