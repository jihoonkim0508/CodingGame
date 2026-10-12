using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CodingGame.Art.MapPainter.Editor
{
    public static class MapPainterVerification
    {
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Verification failed: " + message); }
        [MenuItem("Tools/Art/Validate Modular Map Painter")]
        public static void RunFromMenu() { Run(MapPainterAssets.Setup()); }
        public static void Run(ModularMapPalette palette)
        {
            var log = new List<string> { "Unity integration validation: " + Application.unityVersion };
            var scene = EditorSceneManager.NewPreviewScene();
            var testPalette = Object.Instantiate(palette);
            string temporaryPrefab = MapPainterAssets.Generated + "/ValidationMap.prefab";
            try
            {
                foreach (var rule in palette.floors)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(rule.prefab, scene);
                    try
                    {
                        var points = MapPainterAssets.UsedVertices(instance);
                        Bounds bounds = MapPainterAssets.BoundsOf(points);
                        Check(Mathf.Abs(bounds.size.x - 1) < .0001f && Mathf.Abs(bounds.size.z - 1) < .0001f, rule.label + " footprint");
                        Check(Mathf.Abs(bounds.max.y) < .0001f, rule.label + " top alignment");
                        Check(MapPainterAssets.InferFloor(points).Equals(new FloorShape(rule.exposedEdges, rule.cutCorners)), rule.label + " mesh signature");
                        for(int corner=0;corner<4;corner++)if((rule.cutCorners&(1<<corner))!=0)
                        {
                            var d=GridTopology.Diagonals[corner];
                            Check(points.Any(p=>Mathf.Abs(p.x-d.x*.5f)<.0001f&&Mathf.Abs(p.z-d.z*.5f)<.0001f&&Mathf.Abs(p.y+.025f)<.0001f),rule.label+" corner bevel ends at full-width shoulder");
                            Check(ModuleMeshUtility.TopAt(instance,new Vector2(d.x,d.z)*.497f)>-.026f,rule.label+" concave corner surface coverage");
                        }
                        foreach(var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                        {
                            var mesh=filter.sharedMesh; if(!mesh)continue;
                            Check(mesh.subMeshCount==2,rule.label+" two color slots");
                            var vertices=mesh.vertices;
                            for(int slot=0;slot<2;slot++)
                                foreach(int index in mesh.GetTriangles(slot))
                                {
                                    float y=instance.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[index])).y;
                                    Check(slot==0?y>=FloorColorMesh.GrooveY-.0001f:y<=FloorColorMesh.GrooveY+.0001f,rule.label+" groove partition");
                                }
                        }
                    }
                    finally { Object.DestroyImmediate(instance); }
                }
                log.Add($"PASS: {palette.floors.Count} floor modules have 1x1 footprints, top Y=0 and correct geometric signatures.");
                log.Add("PASS: all modules have two color slots partitioned at the groove center, including generated complements.");
                log.Add("PASS: concave bevels close at the full-width shoulder; generated notches do not cut the body.");
                for (int mask=0;mask<256;mask++)
                {
                    var cells=GridTopology.Neighbourhood(mask);
                    var shape=GridTopology.Floor(cells,new GridCell(0,0));
                    Check(palette.TryFloor(shape,out _,out _),"mask "+mask+" floor coverage");
                    for(int turns=0;turns<4;turns++)
                    {
                        var rotated=new HashSet<GridCell>(cells.Select(c=>GridTopology.RotateCell(c,turns)));
                        Check(GridTopology.Floor(rotated,new GridCell(0,0)).Equals(shape.Rotate(turns)),"rotation invariance "+mask);
                    }
                }
                log.Add("PASS: all 256 neighbour masks and 1024 rotated masks resolve exactly.");
                var walk=new HashSet<GridCell>{new GridCell(0,0),new GridCell(1,0),new GridCell(1,1),new GridCell(1,2),new GridCell(0,2)};
                var found=GridTopology.ShortestPath(walk,new GridCell(0,0),new GridCell(0,2));
                Check(found!=null&&found.Count==4&&found[3].Equals(new GridCell(0,2)),"road path search");
                Check(GridTopology.ShortestPath(walk,new GridCell(0,0),new GridCell(5,5))==null,"unreachable path");
                Check(GridTopology.Corners(new GridCell(0,0),found).SequenceEqual(new[]{new GridCell(0,0),new GridCell(1,0),new GridCell(1,2),new GridCell(0,2)}),"route corners");
                var routeMap=MapPainterEditing.CreateRoot(scene,testPalette,false);
                for(int x=0;x<=4;x++)routeMap.floorCells.Add(new GridCell(x,0));
                Check(MapRouteEditing.SetBase(routeMap,new GridCell(4,0))&&!MapRouteEditing.SetBase(routeMap,new GridCell(9,9)),"base on floor only");
                var route=routeMap.routes[MapRouteEditing.AddStart(routeMap,new GridCell(0,0))];
                Check(MapRouteEditing.AddStart(routeMap,new GridCell(0,0))<0,"duplicate start rejected");
                Check(MapRouteEditing.Extend(routeMap,route,new GridCell(2,0))&&route.path.Count==2,"straight extension");
                Check(MapRouteEditing.Extend(routeMap,route,new GridCell(1,0))&&route.path.Count==1,"trim to clicked cell");
                Check(MapRouteEditing.Extend(routeMap,route,new GridCell(4,0))&&routeMap.IsComplete(route)&&routeMap.Waypoints(route).Length==2,"route reaches base");
                Check(MapRouteEditing.AddStart(routeMap,new GridCell(1,0))>=0&&MapRouteEditing.AutoRoute(routeMap,routeMap.routes[1])==false,"auto route needs road");
                Object.DestroyImmediate(routeMap.gameObject);
                log.Add("PASS: path search, corners, base/start rules, extension, trimming and completion.");
                var root=MapPainterEditing.CreateRoot(scene,testPalette,false);
                foreach(var roadRule in testPalette.roads)roadRule.prefab=null;
                var square=new List<GridCell>(); for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)square.Add(new GridCell(x,z));
                Check(MapPainterEditing.Paint(root,square,false,false,false)==9,"paint square");
                Check(root.floorGroup.childCount==9,"no duplicate modules");
                Color original=palette.floors[0].prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial.color;
                root.upperColor=new Color(.85f,.3f,.12f,1); root.lowerColor=new Color(.12f,.25f,.6f,1); root.ApplyFloorColors();
                foreach(var renderer in root.floorGroup.GetComponentsInChildren<MeshRenderer>())
                {
                    var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block,0);
                    Check(Vector4.Distance(block.GetColor("_BaseColor"),root.upperColor)<.0001f,"upper preview color");
                    renderer.GetPropertyBlock(block,1);
                    Check(Vector4.Distance(block.GetColor("_BaseColor"),root.lowerColor)<.0001f,"lower preview color");
                }
                Check(palette.floors[0].prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial.color==original,"shared material unchanged");
                var secondMap=MapPainterEditing.CreateRoot(scene,testPalette,false);
                MapPainterEditing.Paint(secondMap,new[]{new GridCell(5,5)},false,false,false);
                var secondRenderer=secondMap.floorGroup.GetComponentInChildren<MeshRenderer>(); var secondBlock=new MaterialPropertyBlock();
                secondRenderer.GetPropertyBlock(secondBlock,0);
                Check(Vector4.Distance(secondBlock.GetColor("_BaseColor"),secondMap.upperColor)<.0001f,"independent map colors");
                Object.DestroyImmediate(secondMap.gameObject);
                Check(MapPainterEditing.Paint(root,square,false,false,false)==0,"repeated painting is idempotent");
                Check(MapPainterEditing.Paint(root,new[]{new GridCell(0,0)},true,false,false)==1,"road on floor");
                Check(root.roadGroup.childCount==0,"unregistered road models do not create placeholders");
                Check(MapPainterEditing.Paint(root,new[]{new GridCell(50,50)},true,false,false)==0,"road outside floor rejected");
                MapPainterEditing.Paint(root,new[]{new GridCell(0,0)},false,true,false);
                Check(root.floorCells.Count==8&&root.roadCells.Count==0,"floor deletion removes road data");
                foreach(var tile in root.floorGroup.GetComponentsInChildren<MapTileMarker>())
                {
                    var shape=GridTopology.Floor(new HashSet<GridCell>(root.floorCells),tile.cell);
                    Check(root.palette.TryFloor(shape,out var expected,out int turns)&&tile.source==expected&&tile.turns==turns,"local neighbour update");
                }
                log.Add("PASS: painting, duplicate prevention, local edge/diagonal updates, road floor requirement and deletion cascade.");
                // Real floor prefab assets act only as test bindings for the future road models.
                for(int i=0;i<testPalette.roads.Count;i++)testPalette.roads[i].prefab=palette.floors[i].prefab;
                for(int mask=0;mask<16;mask++)
                {
                    Check(testPalette.TryRoad(mask,out _,out int turns),"road mask "+mask);
                    var rule=testPalette.roads.First(r=>Enumerable.Range(0,4).Any(t=>GridTopology.RotateMask(r.connections,t)==mask));
                    Check(GridTopology.RotateMask(rule.connections,turns)==mask,"road rotation "+mask);
                }
                MapPainterEditing.Paint(root,new[]{new GridCell(0,0)},false,false,false);
                var route=new[]{new GridCell(-1,-1),new GridCell(0,-1),new GridCell(0,0),new GridCell(1,0)};
                MapPainterEditing.Paint(root,route,true,false,false);
                Check(root.roadGroup.childCount==4,"registered model hierarchy");
                MapPainterEditing.Paint(root,new[]{new GridCell(0,0)},true,true,false);
                Check(root.floorCells.Count==9&&root.roadCells.Count==3,"road-only erasing preserves floor");
                root.palette=palette; MapPainterEditing.RebuildAll(root,false);
                root.cellSize=2; root.floorHeight=.7f; root.ApplyFloorColors();
                foreach(var marker in root.GetComponentsInChildren<MapTileMarker>())
                {
                    Check(Mathf.Abs(marker.transform.localPosition.x-marker.cell.x*2)<.0001f&&Mathf.Abs(marker.transform.localPosition.z-marker.cell.z*2)<.0001f,"scaled cell spacing");
                    Check(marker.transform.GetChild(0).localScale==new Vector3(2,marker.road?2:.7f,2),"independent floor height and road scale");
                }
                root.blueprintSpacing=1.5f;
                BlueprintBackdropAssets.Ensure(root,null,false);
                PrefabUtility.SaveAsPrefabAsset(root.gameObject,temporaryPrefab);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(temporaryPrefab);
                var restored=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                try
                {
                    var restoredMap=restored.GetComponent<ModularMapRoot>();
                    Check(restoredMap.palette==palette&&restoredMap.floorCells.Count==9&&restoredMap.roadCells.Count==3,"prefab data roundtrip");
                    Check(restoredMap.upperColor==root.upperColor&&restoredMap.lowerColor==root.lowerColor,"prefab color roundtrip");
                    Check(restoredMap.cellSize==2&&Mathf.Abs(restoredMap.floorHeight-.7f)<.0001f,"prefab size roundtrip");
                    Check(restoredMap.roadInnerColor==root.roadInnerColor&&restoredMap.roadBorderColor==root.roadBorderColor,"prefab road color roundtrip");
                    Check(restoredMap.blueprintBackground&&restoredMap.blueprintSpacing==1.5f&&restoredMap.blueprintBlue==root.blueprintBlue,"prefab blueprint settings roundtrip");
                    var savedBackdrop=restoredMap.GetComponentInChildren<BlueprintBackdrop>();
                    Check(savedBackdrop&&savedBackdrop.GetComponent<MeshFilter>().sharedMesh&&savedBackdrop.GetComponent<MeshRenderer>().sharedMaterial,"prefab persistent blueprint mesh and material");
                    MapPainterEditing.RebuildAll(restoredMap,false);
                    Check(restoredMap.floorGroup.childCount==9,"prefab re-editing");
                    var renderer=restoredMap.floorGroup.GetComponentInChildren<MeshRenderer>();var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,0);
                    Check(Vector4.Distance(block.GetColor("_BaseColor"),root.upperColor)<.0001f,"prefab color restoration");
                }
                finally { Object.DestroyImmediate(restored); }
                log.Add("PASS: all 16 road masks and rotations (test bindings only), road erasing, prefab roundtrip and re-editing.");
                log.Add("PASS: independent upper/lower previews, unchanged shared materials and prefab color restoration.");
                log.Add("PASS: cell size 2, independent floor thickness, road scaling and prefab size/color restoration.");
                foreach(var roadRule in palette.roads.Where(r=>r.connections==3||r.connections==5||r.connections==11))
                {
                    Check(roadRule.prefab,"real straight/corner/T road registered");
                    var model=(GameObject)PrefabUtility.InstantiatePrefab(roadRule.prefab,scene);
                    try
                    {
                        var bounds=MapPainterAssets.BoundsOf(MapPainterAssets.UsedVertices(model));
                        Check(Mathf.Abs(bounds.size.x-1)<.0001f&&Mathf.Abs(bounds.size.z-1)<.0001f,"road unit footprint");
                        foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
                        {
                            Check(filter.sharedMesh.subMeshCount==2,"road inner/border slots");
                            Check(filter.sharedMesh.normals.All(n=>Mathf.Abs(n.sqrMagnitude-1)<.001f),"road normals remain valid on small corner triangles");
                        }
                        float center=ModuleMeshUtility.TopAt(model,Vector2.zero);
                        for(int side=0;side<4;side++)
                        {
                            var d=GridTopology.Directions[side]; float edge=ModuleMeshUtility.TopAt(model,new Vector2(d.x,d.z)*.49f);
                            Check(((roadRule.connections&(1<<side))!=0)==(Mathf.Abs(edge-center)<.001f),roadRule.label+" physical connection "+side);
                        }
                    }
                    finally { Object.DestroyImmediate(model); }
                }
                var surfaceShader=Shader.Find("CodingGame/Art/Stylized Map Surface");
                Check(surfaceShader&&!ShaderUtil.ShaderHasError(surfaceShader),"stylized URP shader compilation");
                log.Add("PASS: supplied straight/corner/T road geometry matches canonical connection masks; road colors have two slots; URP shader compiles.");
                foreach(var start in new[]{new GridCell(-5,-2),new GridCell(4,-3),new GridCell(0,0)})
                    foreach(var end in new[]{new GridCell(6,3),new GridCell(-2,4),new GridCell(0,0)})
                    {
                        var line=GridTopology.Stroke(start,end).ToArray();
                        Check(line[0].Equals(start)&&line[line.Length-1].Equals(end),"stroke endpoints");
                        Check(line.Length==Math.Abs(end.x-start.x)+Math.Abs(end.z-start.z)+1,"stroke length");
                        for(int i=1;i<line.Length;i++)Check(Math.Abs(line[i].x-line[i-1].x)+Math.Abs(line[i].z-line[i-1].z)==1,"continuous stroke");
                    }
                log.Add("PASS: continuous orthogonal strokes with negative and positive coordinates.");
                Object.DestroyImmediate(root.gameObject);
                RenderPreview(scene,palette,log);
                log.Add("LIMIT: endpoints use open straight tiles; dedicated end caps and cross models are not supplied.");
                log.Add("LIMIT: mouse gestures and interactive Undo/Redo still need an editor interaction check.");
                log.Add("RESULT: PASS");
            }
            catch(Exception ex) { log.Add(ex.ToString()); log.Add("RESULT: FAIL"); Debug.LogException(ex); }
            finally
            {
                if(AssetDatabase.LoadAssetAtPath<GameObject>(temporaryPrefab))AssetDatabase.DeleteAsset(temporaryPrefab);
                Object.DestroyImmediate(testPalette); EditorSceneManager.ClosePreviewScene(scene);
                File.WriteAllLines(MapPainterAssets.Report("Validation.txt"),log);
            }
        }
        private static void RenderPreview(Scene scene,ModularMapPalette palette,List<string> log)
        {
            var root=MapPainterEditing.CreateRoot(scene,palette,false);
            var cells=new List<GridCell>();
            for(int z=-2;z<=2;z++)for(int x=-7;x<=7;x++)
            {
                if((z==2&&(x<-4||x>4))||(z==-2&&(x==-6||x==-1||x==3||x==6))||(z==1&&(x==-2||x==3)))continue;
                cells.Add(new GridCell(x,z));
            }
            MapPainterEditing.Paint(root,cells,false,false,false);
            root.cellSize=2; root.ApplyFloorColors();
            var route=new[]{new GridCell(-6,0),new GridCell(-5,0),new GridCell(-4,0),new GridCell(-3,0),new GridCell(-3,-1),new GridCell(-2,-1),new GridCell(-1,-1),new GridCell(0,-1),new GridCell(1,-1),new GridCell(1,0),new GridCell(1,1),new GridCell(2,1)};
            MapPainterEditing.Paint(root,route,true,false,false);
            foreach(var material in root.GetComponentsInChildren<MeshRenderer>().Select(r=>r.sharedMaterial).Where(m=>m).Distinct())
                log.Add("Preview material: "+material.name+" / "+material.shader.name+" / color="+material.color);
            var cameraGO=new GameObject("Validation Camera"); SceneManager.MoveGameObjectToScene(cameraGO,scene);
            var camera=cameraGO.AddComponent<Camera>(); camera.scene=scene;
            camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
            var texture=new RenderTexture(1280,800,24); var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=texture; camera.aspect=1280f/800f;
                MapPainterEditing.FitCamera(camera,MapPainterEditing.MapBounds(root),55,false);
                Bounds mapBounds=MapPainterEditing.MapBounds(root);
                var backdrop=BlueprintBackdropAssets.Ensure(root,camera,false);
                Check(Vector3.Distance(mapBounds.size,MapPainterEditing.MapBounds(root).size)<.001f,"blueprint excluded from map bounds");
                Check(backdrop.transform.position.y<root.transform.position.y-root.floorHeight,"blueprint below map bottom");
                var blueprintShader=backdrop.GetComponent<MeshRenderer>().sharedMaterial.shader;
                Check(!ShaderUtil.ShaderHasError(blueprintShader),"blueprint URP shader compiles");
                var plane=new Plane(Vector3.up,backdrop.transform.position);
                for(int i=0;i<4;i++)
                {
                    var ray=camera.ViewportPointToRay(new Vector3((i&1)==0?0:1,(i&2)==0?0:1,0));
                    Check(plane.Raycast(ray,out float distance),"blueprint camera corner intersection");
                    Vector3 local=backdrop.transform.InverseTransformPoint(ray.GetPoint(distance));
                    Check(Mathf.Abs(local.x)<.5f&&Mathf.Abs(local.y)<.5f,"blueprint covers camera viewport");
                }
                root.blueprintBackground=false; backdrop.Refresh();
                Check(!backdrop.GetComponent<MeshRenderer>().enabled,"blueprint toggle off");
                root.blueprintBackground=true; backdrop.Refresh();
                Check(backdrop.GetComponent<MeshRenderer>().enabled&&camera.clearFlags==CameraClearFlags.SolidColor&&camera.backgroundColor==root.blueprintBlue,"blueprint toggle and camera solid blue");
                var lightGO=new GameObject("Validation Light"); SceneManager.MoveGameObjectToScene(lightGO,scene);
                var light=lightGO.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=.65f; lightGO.transform.rotation=Quaternion.Euler(50,-25,0);
                camera.Render(); var previous=RenderTexture.active;
                try { RenderTexture.active=texture; image.ReadPixels(new Rect(0,0,1280,800),0,0); image.Apply(); }
                finally { RenderTexture.active=previous; }
                File.WriteAllBytes(MapPainterAssets.Report("FloorPreview.png"),image.EncodeToPNG());
                File.WriteAllBytes(MapPainterAssets.Report("BlueprintPreview.png"),image.EncodeToPNG());
                var pixels=image.GetPixels32();
                Check(pixels.Count(p=>p.r>220&&p.b>220&&p.g<60)<pixels.Length/100,"render contains no unsupported pink shader");
                Check(pixels.Count(p=>p.r>120&&p.g>80&&p.b<100)>100,"real road inner color rendered");
                Check(pixels.Count(p=>p.b>p.g+20&&p.g>p.r+15)>pixels.Length/5,"blueprint blue paper visible");
                Check(!ShaderUtil.ShaderHasError(blueprintShader),"blueprint shader rendered without errors");
                foreach(var cell in cells)
                {
                    Vector3 p=camera.WorldToViewportPoint(root.CellPosition(cell));
                    Check(p.x>0&&p.x<1&&p.y>0&&p.y<1&&p.z>0,"camera framing");
                }
                log.Add("PASS: isolated floor preview rendered at 1280x800; camera pitch 55 degrees, yaw 0, orthographic; all cells in frame.");
                log.Add("PASS: blueprint viewport coverage, background below map, solid blue camera, visibility toggle, bounds exclusion, prefab mesh/material/settings restoration and real URP rendering.");
            }
            finally { camera.targetTexture=null; texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(image); }
        }
    }
}
