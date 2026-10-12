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
    public static class RoadBranchUpgrade
    {
        private static void Check(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("Road branch validation: "+message);}

        public static void ApplyToOpenScene(string reportName="BranchSetup.txt")
        {
            var log=new List<string>();
            try
            {
                var scene=SceneManager.GetActiveScene();
                if(scene.path!=MapPainterAssets.ArtScene)throw new InvalidOperationException("Open ArtTestScene first.");
                MapBuildPreview.Stop();bool wasDirty=scene.isDirty;
                var palette=MapPainterAssets.Setup();
                Validate(palette);
                foreach(var go in scene.GetRootGameObjects())foreach(var map in go.GetComponentsInChildren<ModularMapRoot>(true))
                {
                    MapPainterEditing.RebuildAll(map);ModularMapPainterWindow.Open().SetMap(map);
                    log.Add(map.name+": existing cells, size and colors retained; road modules refreshed.");
                }
                foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{MapPainterAssets.MapPrefabs}))
                {
                    string path=AssetDatabase.GUIDToAssetPath(guid);
                    if(!AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ModularMapRoot>())continue;
                    var contents=PrefabUtility.LoadPrefabContents(path);
                    try {MapPainterEditing.RebuildAll(contents.GetComponent<ModularMapRoot>(),false);PrefabUtility.SaveAsPrefabAsset(contents,path);log.Add("Updated "+path);}
                    finally {PrefabUtility.UnloadPrefabContents(contents);}
                }
                EditorSceneManager.MarkSceneDirty(scene);if(!wasDirty)EditorSceneManager.SaveScene(scene);
                log.Add(wasDirty?"Scene had unsaved changes; save with Ctrl+S.":"ArtTestScene saved.");
                SceneView.RepaintAll();log.Add("RESULT: PASS");
            }
            catch(Exception ex){log.Add(ex.ToString());log.Add("RESULT: FAIL");Debug.LogException(ex);}
            finally {File.WriteAllLines(MapPainterAssets.Root+"/Reports/"+reportName,log);}
        }

        public static void RunBatch()
        {
            var palette=MapPainterAssets.Setup();
            MapPainterVerification.Run(palette);
            Validate(palette);
        }

        public static void Validate(ModularMapPalette palette)
        {
            var log=new List<string>{"Real T-road model validation; Unity "+Application.unityVersion};
            var scene=EditorSceneManager.NewPreviewScene();
            string savedPath=MapPainterAssets.Generated+"/ValidationBranch.prefab";
            try
            {
                Check(palette.TryRoad(11,out var branchPrefab,out _),"T model registered");
                log.Add("T prefab: "+AssetDatabase.GetAssetPath(branchPrefab));
                var canonical=new[]{new GridCell(-2,0),new GridCell(-1,0),new GridCell(0,0),new GridCell(1,0),new GridCell(2,0),new GridCell(0,1),new GridCell(0,2)};
                for(int turns=0;turns<4;turns++)
                {
                    var root=MapPainterEditing.CreateRoot(scene,palette,false);root.cellSize=2;
                    try
                    {
                        var floors=new List<GridCell>();for(int z=-2;z<=2;z++)for(int x=-2;x<=2;x++)floors.Add(new GridCell(x,z));
                        MapPainterEditing.Paint(root,floors,false,false,false);
                        var route=canonical.Select(c=>GridTopology.RotateCell(c,turns)).ToArray();
                        MapPainterEditing.Paint(root,route.Take(5),true,false,false);
                        Check(Center(root).source!=branchPrefab,"center initially straight");
                        MapPainterEditing.Paint(root,route.Skip(5),true,false,false);
                        var center=Center(root);
                        Check(center.source==branchPrefab&&center.turns==turns,"T selection and rotation "+turns);
                        Check(root.roadGroup.childCount==route.Length,"all branch tiles placed");
                        float top=ModuleMeshUtility.TopAt(center.gameObject,Vector2.zero);
                        int mask=GridTopology.RotateMask(11,turns);
                        for(int side=0;side<4;side++)
                        {
                            var d=GridTopology.Directions[side];
                            float edge=ModuleMeshUtility.TopAt(center.gameObject,new Vector2(d.x,d.z)*.98f);
                            bool open=(mask&(1<<side))!=0;
                            Check(open==(Mathf.Abs(edge-top)<.002f),"rotated physical edge "+turns+"/"+side);
                            if(!open)continue;
                            var neighbor=root.roadGroup.GetComponentsInChildren<MapTileMarker>().Single(m=>m.cell.Equals(d));
                            float adjoining=ModuleMeshUtility.TopAt(neighbor.gameObject,new Vector2(-d.x,-d.z)*.98f);
                            Check(Mathf.Abs(edge+center.transform.localPosition.y-adjoining-neighbor.transform.localPosition.y)<.002f,"T/straight surface seam height "+turns+"/"+side);
                        }
                        foreach(var renderer in center.GetComponentsInChildren<MeshRenderer>())
                        {
                            Check(renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount==2,"T has inner/border slots");
                            var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,0);
                            Check(Vector4.Distance(block.GetColor("_BaseColor"),root.roadInnerColor)<.0001f,"T inner color");
                            renderer.GetPropertyBlock(block,1);
                            Check(Vector4.Distance(block.GetColor("_BaseColor"),root.roadBorderColor)<.0001f,"T border color");
                        }
                        MapPainterEditing.Paint(root,new[]{route[5]},true,true,false);
                        Check(Center(root).source!=branchPrefab,"remove branch returns to straight");
                        MapPainterEditing.Paint(root,new[]{route[1]},true,true,false);
                        Check(Center(root).source==palette.roads.First(r=>r.connections==1).prefab,"remove second connection returns to endpoint");
                        MapPainterEditing.Paint(root,new[]{route[5]},true,false,false);
                        Check(Center(root).source==palette.roads.First(r=>r.connections==3).prefab,"two adjacent connections return to corner");
                        MapPainterEditing.Paint(root,new[]{route[1]},true,false,false);
                        Check(Center(root).source==branchPrefab,"repaint restores T");
                        Check(MapPainterEditing.Paint(root,route,true,false,false)==0,"repeated T painting is idempotent");
                        CheckBuild(root,route);
                        if(turns==0)
                        {
                            PrefabUtility.SaveAsPrefabAsset(root.gameObject,savedPath);
                            var saved=PrefabUtility.LoadPrefabContents(savedPath);
                            try
                            {
                                var restored=saved.GetComponent<ModularMapRoot>();
                                Check(restored.roadCells.Count==7&&Center(restored).source==branchPrefab,"T prefab save and reload");
                                MapPainterEditing.RebuildAll(restored,false);
                                Check(Center(restored).source==branchPrefab,"T prefab re-editing");
                            }
                            finally {PrefabUtility.UnloadPrefabContents(saved);}
                            Render(scene,root);
                        }
                        log.Add("PASS: rotation "+turns*90+"; three open seams, closed border, two colors, paint/erase/repaint and build playback.");
                    }
                    finally {Object.DestroyImmediate(root.gameObject);}
                }
                log.Add("PASS: T prefab save/reload/re-editing and rendered RoadBranchPreview.png.");log.Add("RESULT: PASS");
            }
            catch(Exception ex){log.Add(ex.ToString());log.Add("RESULT: FAIL");throw;}
            finally
            {
                if(AssetDatabase.LoadAssetAtPath<GameObject>(savedPath))AssetDatabase.DeleteAsset(savedPath);
                EditorSceneManager.ClosePreviewScene(scene);
                File.WriteAllLines(MapPainterAssets.Root+"/Reports/BranchValidation.txt",log);
            }
        }

        private static MapTileMarker Center(ModularMapRoot root)=>root.roadGroup.GetComponentsInChildren<MapTileMarker>().Single(m=>m.cell.Equals(new GridCell(0,0)));
        private static void CheckBuild(ModularMapRoot root,GridCell[] route)
        {
            var order=MapBuildPlayback.RoadOrder(new HashSet<GridCell>(route));
            Check(order.Count==route.Length&&order.Values.All(v=>v>=0&&v<=1),"all branches have build order");
            var exits=new[]{route[1],route[3],route[5]}.Where(c=>order[c]>order[route[2]]).ToArray();
            Check(exits.Length==2,"two outgoing branches build after junction");
            foreach(var exit in exits)
            {
                var end=exit+exit;
                Check(order[end]>order[exit],"outgoing branch grows outward");
            }
            var markers=root.roadGroup.GetComponentsInChildren<MapTileMarker>();
            var poses=markers.ToDictionary(m=>m,m=>m.transform.localPosition);
            var playback=new MapBuildPlayback(root,root.GetComponent<MapBuildSequence>());
            try
            {
                playback.Sample(0);Check(markers.All(m=>m.GetComponentsInChildren<MeshRenderer>().All(r=>!r.enabled)),"roads initially hidden");
                playback.Sample(.85f);Check(markers.Where(m=>exits.Contains(m.cell)).All(m=>m.GetComponentsInChildren<MeshRenderer>().All(r=>r.enabled)),"both branches visible during playback");
                playback.Sample(1);Check(markers.All(m=>m.GetComponentsInChildren<MeshRenderer>().All(r=>r.enabled)),"all branch roads visible at completion");
            }
            finally {playback.Restore();}
            Check(markers.All(m=>Vector3.Distance(m.transform.localPosition,poses[m])<.0001f),"branch poses restored");
        }
        private static void Render(Scene scene,ModularMapRoot root)
        {
            var go=new GameObject("Branch Check Camera");SceneManager.MoveGameObjectToScene(go,scene);
            var camera=go.AddComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
            var lightGO=new GameObject("Branch Check Light");SceneManager.MoveGameObjectToScene(lightGO,scene);
            var light=lightGO.AddComponent<Light>();light.type=LightType.Directional;light.intensity=.65f;lightGO.transform.rotation=Quaternion.Euler(50,-25,0);
            var texture=new RenderTexture(960,720,24);var image=new Texture2D(960,720,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=texture;camera.aspect=960f/720;
                MapPainterEditing.FitCamera(camera,MapPainterEditing.MapBounds(root),55,false);
                BlueprintBackdropAssets.Ensure(root,camera,false);camera.Render();RenderTexture.active=texture;
                image.ReadPixels(new Rect(0,0,960,720),0,0);image.Apply();
                Check(image.GetPixels32().Count(p=>p.r>120&&p.g>80&&p.b<100)>1000,"T inner color rendered");
                File.WriteAllBytes(MapPainterAssets.Root+"/Reports/RoadBranchPreview.png",image.EncodeToPNG());
            }
            finally {RenderTexture.active=previous;camera.targetTexture=null;texture.Release();Object.DestroyImmediate(texture);Object.DestroyImmediate(image);Object.DestroyImmediate(go);Object.DestroyImmediate(lightGO);}
        }
    }
}
