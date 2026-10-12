using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CodingGame.Art.MapPainter.Editor
{
    public static class MapBuildVerification
    {
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        [MenuItem("Tools/Art/Validate Map Build Animation")]
        public static void Run()
        {
            var scene=EditorSceneManager.NewPreviewScene();var log=new List<string>();
            try
            {
                var root=MapPainterEditing.CreateRoot(scene,MapPainterAssets.Setup(),false);root.cellSize=2;
                var cells=new List<GridCell>();for(int x=-4;x<=4;x++)for(int z=-2;z<=2;z++)cells.Add(new GridCell(x,z));
                MapPainterEditing.Paint(root,cells,false,false,false);
                var route=new[]{new GridCell(-3,0),new GridCell(-2,0),new GridCell(-1,0),new GridCell(-1,1),new GridCell(0,1),new GridCell(1,1),new GridCell(1,0),new GridCell(1,-1),new GridCell(2,-1),new GridCell(3,-1)};
                MapPainterEditing.Paint(root,route,true,false,false);
                var sequence=root.GetComponent<MapBuildSequence>();Check(sequence&&sequence.outlineMaterial,"build component and persistent outline material");
                var positions=root.GetComponentsInChildren<MapTileMarker>().ToDictionary(m=>m,m=>m.transform.localPosition);
                var scales=positions.Keys.ToDictionary(m=>m,m=>m.transform.localScale);
                var renderers=root.GetComponentsInChildren<MeshRenderer>();
                var visible=renderers.ToDictionary(r=>r,r=>r.enabled);
                var blocks=renderers.ToDictionary(r=>r,r=>{var b=new MaterialPropertyBlock();r.GetPropertyBlock(b,0);return b.GetColor("_BaseColor");});
                var roadOrder=MapBuildPlayback.RoadOrder(new HashSet<GridCell>(route));
                Check(roadOrder[route[0]]==0&&roadOrder[route[route.Length-1]]==1,"road order follows endpoint to endpoint");
                for(int i=1;i<route.Length;i++)Check(roadOrder[route[i]]>roadOrder[route[i-1]],"road continuous reveal order");
                var cameraGO=new GameObject("Build Camera");SceneManager.MoveGameObjectToScene(cameraGO,scene);
                var camera=cameraGO.AddComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
                var target=new RenderTexture(960,600,24);camera.targetTexture=target;camera.aspect=1.6f;
                MapPainterEditing.FitCamera(camera,MapPainterEditing.MapBounds(root),55,false);BlueprintBackdropAssets.Ensure(root,camera,false);
                var lightGO=new GameObject("Build Light");SceneManager.MoveGameObjectToScene(lightGO,scene);
                var light=lightGO.AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(50,-25,0);
                try
                {
                    sequence.BeginPreview();
                    Check(renderers.All(r=>!r.enabled),"initial map hidden");
                    Capture(camera,target,"Build_00.png");
                    sequence.SamplePreview(.11f);Capture(camera,target,"Build_01_Outline.png");
                    sequence.SamplePreview(.32f);
                    Check(root.floorGroup.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled)&&root.floorGroup.GetComponentsInChildren<MeshRenderer>().Any(r=>!r.enabled),"floor sweep partly visible");
                    Check(root.roadGroup.GetComponentsInChildren<MeshRenderer>().All(r=>!r.enabled),"roads wait for floor");
                    Capture(camera,target,"Build_02_Floor.png");
                    sequence.SamplePreview(.72f);
                    Check(root.floorGroup.GetComponentsInChildren<MeshRenderer>().All(r=>r.enabled),"floor completed before road");
                    Check(root.roadGroup.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled)&&root.roadGroup.GetComponentsInChildren<MeshRenderer>().Any(r=>!r.enabled),"road progressive reveal");
                    Capture(camera,target,"Build_03_Road.png");
                    sequence.SamplePreview(1);Capture(camera,target,"Build_04_Complete.png");sequence.Finish(false);
                    Check(!sequence.IsAnimating,"completion restores state");
                    for(int repeat=0;repeat<3;repeat++){sequence.BeginPreview();sequence.SamplePreview(.43f);sequence.Finish(false);}
                    foreach(var marker in positions.Keys)
                    {
                        Check(marker.transform.localPosition==positions[marker]&&marker.transform.localScale==scales[marker],"original transform restored");
                    }
                    foreach(var renderer in renderers)
                    {
                        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,0);
                        Check(renderer.enabled==visible[renderer]&&block.GetColor("_BaseColor")==blocks[renderer]&&block.GetFloat("_BuildGlow")==0,"visibility and property blocks restored");
                    }
                    Check(scene.GetRootGameObjects().All(go=>go.name!="Map Build Preview Effects"),"temporary effects cleaned after repeated stops");
                    Check(!ShaderUtil.ShaderHasError(sequence.outlineMaterial.shader),"outline shader real render compile");
                    log.Add("PASS: floor sweep, route connection order, stage timing, repeated preview/stop, transform/color/visibility restoration, temporary effect cleanup and real URP renders at five times.");
                }
                finally {sequence.Finish(false);camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);}
                string path=MapPainterAssets.Generated+"/BuildValidation.prefab";
                try
                {
                    PrefabUtility.SaveAsPrefabAsset(root.gameObject,path);
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                    var saved=instance.GetComponent<MapBuildSequence>();
                    Check(saved&&saved.playOnStart&&saved.outlineMaterial==sequence.outlineMaterial&&saved.PlaybackKey==sequence.PlaybackKey,"prefab build configuration roundtrip");
                    Object.DestroyImmediate(instance);log.Add("PASS: prefab duration, identity and outline material persist for game loading.");
                }
                finally {AssetDatabase.DeleteAsset(path);}
                log.Add("RESULT: PASS");
            }
            catch(Exception ex){log.Add(ex.ToString());log.Add("RESULT: FAIL");Debug.LogException(ex);}
            finally {EditorSceneManager.ClosePreviewScene(scene);File.WriteAllLines(MapPainterAssets.Report("BuildValidation.txt"),log);}
        }
        private static void Capture(Camera camera,RenderTexture target,string name)
        {
            camera.Render();var before=RenderTexture.active;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
                Check(image.GetPixels32().Count(p=>p.r>220&&p.b>220&&p.g<60)<target.width*target.height/100,"no pink shader pixels");
                File.WriteAllBytes(MapPainterAssets.Report(name),image.EncodeToPNG());
            }
            finally {RenderTexture.active=before;Object.DestroyImmediate(image);}
        }
    }
}
