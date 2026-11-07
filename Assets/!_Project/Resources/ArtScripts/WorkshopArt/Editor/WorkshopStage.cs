using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CodingGame.Art.MapPainter;
using CodingGame.Art.MapPainter.Editor;
using Object=UnityEngine.Object;

namespace CodingGame.Art.Workshop
{
 [InitializeOnLoad]
 public static class WorkshopStage
 {
  public const string ScriptRoot="Assets/!_Project/Resources/ArtScripts/WorkshopArt";
  public const string Output="Assets/!_Project/Resources/ArtPrefabs/Stage01_Workshop.prefab";
  const string Request=ScriptRoot+"/ApplyWorkshop.request.txt";
  const string VerifyRequest=ScriptRoot+"/VerifyWorkshop.request.txt";
  const string RepairRequest=ScriptRoot+"/RepairWorkshop.request.txt";
  static double next;
  static WorkshopStage(){next=EditorApplication.timeSinceStartup+4;EditorApplication.update+=Poll;}
  static void Poll()
  {
   if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   next=EditorApplication.timeSinceStartup+1;
   if(File.Exists(RepairRequest))
   {
    File.Delete(RepairRequest);if(File.Exists(VerifyRequest))File.Delete(VerifyRequest);
    try{RepairRendering();VerifyLive();}catch(Exception e){Report("LiveValidation.txt",e+"\nRESULT: FAIL");Debug.LogException(e);}return;
   }
   if(File.Exists(VerifyRequest))
   {
    File.Delete(VerifyRequest);try{VerifyLive();}catch(Exception e){Report("LiveValidation.txt",e+"\nRESULT: FAIL");Debug.LogException(e);}return;
   }
   if(!File.Exists(Request))return;File.Delete(Request);
   try{Apply();}catch(Exception e){Report("SceneApply.txt",e+"\nRESULT: FAIL");Debug.LogException(e);}
  }
  static void Report(string file,string text){Directory.CreateDirectory(ScriptRoot+"/Reports");File.WriteAllText(ScriptRoot+"/Reports/"+file,text);}
  [MenuItem("Tools/Art/Workshop/Apply Stage 1 Art to ArtTestScene")]
  public static void Apply()
  {
   var scene=SceneManager.GetActiveScene();if(scene.path!=MapPainterAssets.ArtScene)throw new InvalidOperationException("Open ArtTestScene first. Other scenes are not modified.");
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
   MapBuildPreview.Stop();bool dirty=scene.isDirty;
   var map=MapPainterEditing.FindMap(scene);if(!map)throw new InvalidOperationException("No map found.");
   if(!AssetDatabase.LoadAssetAtPath<GameObject>(WorkshopModels.Root+"/Prefabs/Monitor.prefab"))WorkshopModels.Bake();
   Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Stage 1 workshop art");
   if(PrefabUtility.IsPartOfPrefabInstance(map))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(map.gameObject),PrefabUnpackMode.OutermostRoot,InteractionMode.UserAction);
   Undo.RegisterFullObjectHierarchyUndo(map.gameObject,"Stage 1 workshop art");
   Decorate(map,true);var camera=ActiveCamera(scene);
   if(camera){Undo.RecordObject(camera,"Frame workshop");Undo.RecordObject(camera.transform,"Frame workshop");Frame(camera,map);}
   var sun=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).FirstOrDefault(l=>l.type==LightType.Directional);
   if(sun){Undo.RecordObject(sun,"Warm workshop light");Undo.RecordObject(sun.transform,"Warm workshop light");sun.color=new Color(1,.94f,.82f);sun.intensity=1;sun.shadows=LightShadows.Soft;sun.shadowBias=.06f;sun.shadowNormalBias=.35f;sun.transform.rotation=Quaternion.Euler(48,-35,0);}
   // Hide the scene's user-created grey layout markers; retain the objects for Undo.
   foreach(var root in scene.GetRootGameObjects())if(root.name=="Cube"||root.name.StartsWith("Cube (",StringComparison.Ordinal))
   {Undo.RecordObject(root,"Hide layout guide");root.SetActive(false);}
   PrefabUtility.SaveAsPrefabAsset(map.gameObject,Output);AssetDatabase.SaveAssets();
   Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);if(!dirty)EditorSceneManager.SaveScene(scene);
   ModularMapPainterWindow.Open().SetMap(map);Selection.activeGameObject=map.gameObject;SceneView.RepaintAll();
   Report("SceneApply.txt","Stage 1 workshop art applied. Original Stage01 prefab retained.\nOutput: "+Output+"\n"+(dirty?"Scene had unsaved changes; save with Ctrl+S.":"ArtTestScene saved.")+"\nRESULT: PASS");
  }
  public static void Decorate(ModularMapRoot map,bool undo=false)
  {
   var old=map.transform.Find("Stage01_Workshop_Art");if(old){if(undo)Undo.DestroyObjectImmediate(old.gameObject);else Object.DestroyImmediate(old.gameObject);}
   map.name="Stage01 Workshop";map.upperColor=ColorHex("D4A76E");map.lowerColor=ColorHex("9E704C");map.roadInnerColor=ColorHex("F5DFB5");map.roadBorderColor=ColorHex("C29A62");
   map.lightSoftness=.78f;map.shadowStrength=.55f;map.bodyGradient=.17f;map.bevelHighlight=.12f;
   map.blueprintBlue=ColorHex("182F42");map.blueprintMinorOpacity=.07f;map.blueprintMajorOpacity=.18f;map.blueprintSpacing=2;
   foreach(var renderer in map.floorGroup.GetComponentsInChildren<MeshRenderer>(true))renderer.sharedMaterials=new[]{WorkshopModels.Material("DeskWood"),WorkshopModels.Material("DeskEdge")};
   foreach(var renderer in map.roadGroup.GetComponentsInChildren<MeshRenderer>(true))renderer.sharedMaterials=new[]{WorkshopModels.Material("RoadCream"),WorkshopModels.Material("RoadEdge")};
   map.ApplyFloorColors();var art=new GameObject("Stage01_Workshop_Art");art.transform.SetParent(map.transform,false);if(undo)Undo.RegisterCreatedObjectUndo(art,"Workshop decoration");
   Place(map,art.transform,"Monitor",-3.6f,5.65f,0);Place(map,art.transform,"Keyboard",-3.6f,5,0);Place(map,art.transform,"Mouse",-.8f,5,0);
   Place(map,art.transform,"Speaker",-6.25f,5.5f,0);Place(map,art.transform,"Plant",-.9f,6,0,.9f);Place(map,art.transform,"Mug",-5.85f,4.4f,15);
   Place(map,art.transform,"Books",-7.2f,-.9f,-12);Place(map,art.transform,"Mug",-8.7f,.05f,10);Place(map,art.transform,"Plant",-8.5f,2.9f,0);
   Place(map,art.transform,"Notes",-3.25f,-.6f,-5);Place(map,art.transform,"Plant",-3.1f,2.3f,0,.8f);
   Place(map,art.transform,"Board",3.6f,3.8f,0);Place(map,art.transform,"Books",7.5f,1.05f,18);Place(map,art.transform,"Lamp",6.6f,.35f,15);Place(map,art.transform,"Plant",9.0f,.2f,0);
   Place(map,art.transform,"Books",3.1f,-4.0f,8);Place(map,art.transform,"Plant",-.4f,-4.3f,0,1.05f);Place(map,art.transform,"Notes",8.7f,-3.2f,20);
   foreach(var p in new[]{new GridCell(-7,0),new GridCell(-6,3),new GridCell(-3,1),new GridCell(0,0),new GridCell(3,0),new GridCell(5,-3),new GridCell(8,-3)})
    if(map.floorCells.Contains(p)&&!map.roadCells.Contains(p))Place(map,art.transform,"BuildPad",p.x,p.z,0);
   var roads=new HashSet<GridCell>(map.roadCells);var ends=roads.Where(c=>GridTopology.Directions.Count(d=>roads.Contains(c+d))==1).OrderBy(c=>c.x).ToArray();
   if(ends.Length>=2)
   {
    var first=ends[0];var last=ends[ends.Length-1];
    Place(map,art.transform,"ErrorGate",first.x-.9f,first.z,-90,1,true);
    Place(map,art.transform,"CodeBase",last.x+1.1f,last.z,90,1,true);
   }
   var sequence=map.GetComponent<MapBuildSequence>();if(sequence){var so=new SerializedObject(sequence);so.FindProperty("playbackKey").stringValue=Guid.NewGuid().ToString("N");so.ApplyModifiedPropertiesWithoutUndo();}
   EditorUtility.SetDirty(map);
  }
  static Color ColorHex(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var c);return c;}
  static Camera ActiveCamera(Scene scene)
  {
   var cameras=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).ToArray();
   return cameras.FirstOrDefault(c=>c.isActiveAndEnabled&&c.CompareTag("MainCamera"))??cameras.FirstOrDefault(c=>c.isActiveAndEnabled)??cameras.FirstOrDefault();
  }
  static void RepairRendering()
  {
   var scene=SceneManager.GetActiveScene();if(scene.path!=MapPainterAssets.ArtScene)throw new Exception("Open ArtTestScene.");
   string path=WorkshopModels.Root+"/Shaders/WorkshopSurface.shader";
   AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
   var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Workshop shader not ready.");
   foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{WorkshopModels.Root+"/Materials"}))
   {
    var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
    material.shader=material.name=="Screen"||material.name.EndsWith("Glow")?Shader.Find("Universal Render Pipeline/Unlit"):shader;
    EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
   }
   var map=MapPainterEditing.FindMap(scene);var camera=ActiveCamera(scene);
   Undo.RecordObject(camera,"Frame active workshop camera");Undo.RecordObject(camera.transform,"Frame active workshop camera");Frame(camera,map);
   PrefabUtility.SaveAsPrefabAsset(map.gameObject,Output);EditorSceneManager.MarkSceneDirty(scene);
  }
  [MenuItem("Tools/Art/Workshop/Capture and Verify ArtTestScene")]
  public static void VerifyLive()
  {
   var scene=SceneManager.GetActiveScene();if(scene.path!=MapPainterAssets.ArtScene)throw new Exception("Open ArtTestScene.");
   if(MapBuildPreview.IsRunning)throw new Exception("Stop the build preview before capture.");
   var map=MapPainterEditing.FindMap(scene);var art=map.transform.Find("Stage01_Workshop_Art");var camera=ActiveCamera(scene);
   if(!art||!camera)throw new Exception("Workshop art or camera missing.");
   foreach(var r in art.GetComponentsInChildren<MeshRenderer>())if(!r.sharedMaterial||!r.sharedMaterial.shader||ShaderUtil.ShaderHasError(r.sharedMaterial.shader))throw new Exception("Missing or invalid material on "+r.name);
   foreach(var bg in map.GetComponentsInChildren<BlueprintBackdrop>()){bg.targetCamera=camera;bg.Refresh();}
   if(camera.clearFlags!=CameraClearFlags.SolidColor||camera.backgroundColor!=map.blueprintBlue||map.blueprintBlue!=ColorHex("182F42"))throw new Exception("Background and solid color mismatch.");
   if(!AssetDatabase.LoadAssetAtPath<GameObject>(Output))throw new Exception("Saved map missing.");
   Render(camera,"Stage01_Workshop_LiveOverview.png",1920,1080);
   Report("LiveValidation.txt","ArtTestScene live capture.\nCamera: "+camera.name+"; active="+camera.isActiveAndEnabled+"; viewport="+camera.rect+"\nProps: "+art.childCount+"; material references and shader compilation: PASS\nBlueprint and camera Solid Color: #182F42\nSaved map: "+Output+"\nScene dirty: "+scene.isDirty+"\nRESULT: PASS");
  }
  static GameObject Place(ModularMapRoot map,Transform parent,string name,float x,float z,float yaw,float scale=1,bool gateway=false)
  {
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(WorkshopModels.Root+"/Prefabs/"+name+".prefab");
   var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.transform.localPosition=new Vector3(x*map.cellSize,.025f,z*map.cellSize);go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
   if(!gateway)
   {
    var desired=go.transform.localPosition;bool found=false;
    foreach(var step in new[]{Vector2.zero,new Vector2(0,.3f),new Vector2(0,-.3f),new Vector2(.3f,0),new Vector2(-.3f,0),new Vector2(.5f,.5f),new Vector2(-.5f,.5f),new Vector2(0,1),new Vector2(1,0),new Vector2(-1,0)})
    {
     go.transform.localPosition=desired+new Vector3(step.x*map.cellSize,0,step.y*map.cellSize);
     if(ClearOfRoads(map,go)){found=true;break;}
    }
    if(!found){Object.DestroyImmediate(go);throw new InvalidOperationException(name+" cannot be placed without covering the road at "+x+","+z);}
   }
   return go;
  }
  static Bounds BoundsOf(GameObject go)
  {var renderers=go.GetComponentsInChildren<MeshRenderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;}
  static bool ClearOfRoads(ModularMapRoot map,GameObject prop)
  {
   Bounds b=BoundsOf(prop);float half=map.cellSize*.49f;
   foreach(var c in map.roadCells){var p=map.CellPosition(c);if(b.min.x<p.x+half&&b.max.x>p.x-half&&b.min.z<p.z+half&&b.max.z>p.z-half)return false;}
   return true;
  }
  public static void Frame(Camera camera,ModularMapRoot map)
  {
   MapPainterEditing.FitCamera(camera,MapPainterEditing.MapBounds(map),50,false);
   // Slight side view adds depth while the road stays readable.
   var bounds=MapPainterEditing.MapBounds(map);var focus=bounds.center;
   // Orthographic framing is independent of distance. Keep the stage inside the
   // project's 50-unit shadow range rather than moving the camera 100+ units away.
   float distance=Mathf.Max(25,bounds.size.y+15);
   camera.transform.rotation=Quaternion.Euler(50,12,0);camera.transform.position=focus-camera.transform.forward*distance;camera.orthographicSize*=1.06f;
   foreach(var backdrop in map.GetComponentsInChildren<BlueprintBackdrop>()){backdrop.targetCamera=camera;backdrop.Refresh();}
  }
  public static void RunBatch()
  {
   Directory.CreateDirectory(ScriptRoot+"/Reports");WorkshopModels.Bake();var scene=EditorSceneManager.NewPreviewScene();var log=new List<string>();
   try
   {
    var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/!_Project/Resources/ArtPrefabs/Stage01.prefab");
    var go=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
    var map=go.GetComponent<ModularMapRoot>();var roads=map.roadCells.ToArray();var floors=map.floorCells.ToArray();Decorate(map);
    if(!map.roadCells.SequenceEqual(roads)||!map.floorCells.SequenceEqual(floors))throw new Exception("Map topology changed.");
    foreach(var renderer in map.transform.Find("Stage01_Workshop_Art").GetComponentsInChildren<MeshRenderer>())
    {if(!renderer.sharedMaterial||ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader))throw new Exception("Invalid prop material.");var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;if(mesh.normals.Any(n=>float.IsNaN(n.x)))throw new Exception("Invalid normals.");}
    PrefabUtility.SaveAsPrefabAsset(go,Output);
    var cameraGO=new GameObject("Workshop Camera");SceneManager.MoveGameObjectToScene(cameraGO,scene);var camera=cameraGO.AddComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.aspect=16f/9;Frame(camera,map);
    var lightGO=new GameObject("Workshop Sun");SceneManager.MoveGameObjectToScene(lightGO,scene);var light=lightGO.AddComponent<Light>();light.type=LightType.Directional;light.color=new Color(1,.94f,.82f);light.intensity=1;light.shadows=LightShadows.Soft;light.shadowBias=.06f;light.shadowNormalBias=.35f;lightGO.transform.rotation=Quaternion.Euler(48,-35,0);
    Render(camera,"Stage01_Workshop_Overview.png",1920,1080);
    var focus=map.transform.TransformPoint(new Vector3(-6,1.5f,10));camera.orthographicSize=6;camera.transform.position=focus-camera.transform.forward*25;Render(camera,"Stage01_Workshop_Desk.png",1280,960);
    focus=map.transform.TransformPoint(new Vector3(22,2.3f,-2));camera.orthographicSize=5;camera.transform.rotation=Quaternion.Euler(32,40,0);camera.transform.position=focus-camera.transform.forward*22;Render(camera,"Stage01_Workshop_Base.png",1280,960);
    focus=map.transform.TransformPoint(new Vector3(-19.8f,1.4f,2));camera.orthographicSize=3.7f;camera.transform.rotation=Quaternion.Euler(32,-45,0);camera.transform.position=focus-camera.transform.forward*22;Render(camera,"Stage01_Workshop_Spawn.png",1280,960);
    log.Add("PASS: map cells and road cells preserved; decoration footprints clear of roads except intended endpoint structures.");
    log.Add("PASS: 13 reusable prop prefabs, persistent combined meshes/materials, full map prefab and rendered overview/detail.");
    var monster=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/!_Project/Resources/Models/MonsterBase/MonsterBaseFix.fbx");
    if(monster){var mb=MapPainterAssets.BoundsOf(MapPainterAssets.UsedVertices(monster));float clearHeight=WorkshopModels.DoorHeight-.30f;log.Add("MonsterBaseFix at 0.65: "+mb.size*.65f+"; clear entrance "+WorkshopModels.DoorWidth+" x "+clearHeight);if(Mathf.Max(mb.size.x,mb.size.z)*.65f>WorkshopModels.DoorWidth||mb.size.y*.65f>clearHeight)throw new Exception("Monster does not fit entrance.");CheckDoor(mb.size*.65f);}
    log.Add("RESULT: PASS");
   }
   catch(Exception e){log.Add(e.ToString());log.Add("RESULT: FAIL");throw;}
   finally{EditorSceneManager.ClosePreviewScene(scene);Report("Validation.txt",string.Join("\n",log));}
  }
  static void CheckDoor(Vector3 monsterSize)
  {
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(WorkshopModels.Root+"/Prefabs/CodeBase.prefab");
   foreach(float x in new[]{-.6f,0,.6f})foreach(float y in new[]{.35f,.35f+monsterSize.y*.5f,.35f+monsterSize.y})
   foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>())
   {
    var mesh=filter.sharedMesh;var v=mesh.vertices;var indices=mesh.triangles;
    for(int i=0;i<indices.Length;i+=3)
    {
     Vector3 a=v[indices[i]],b=v[indices[i+1]],c=v[indices[i+2]];
     float det=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(det)<1e-8f)continue;
     float u=((b.y-c.y)*(x-c.x)+(c.x-b.x)*(y-c.y))/det;
     float w=((c.y-a.y)*(x-c.x)+(a.x-c.x)*(y-c.y))/det;
     if(u<0||w<0||u+w>1)continue;float z=u*a.z+w*b.z+(1-u-w)*c.z;
     if(z> -2.8f&&z<-.6f)throw new Exception("Entrance passage blocked at "+x+","+y+","+z);
    }
   }
  }
  static void Render(Camera camera,string name,int width,int height)
  {
   var target=new RenderTexture(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGB24,false);var previous=RenderTexture.active;var oldTarget=camera.targetTexture;float oldAspect=camera.aspect;
   try
   {
    camera.aspect=(float)width/height*camera.rect.width/camera.rect.height;camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
    File.WriteAllBytes(ScriptRoot+"/Reports/"+name,image.EncodeToPNG());
    if(name.EndsWith("Overview.png"))
    {
     var pixels=image.GetPixels32();
     if(pixels.Count(p=>p.g>p.r*1.15f&&p.g>p.b*1.1f)<300)throw new Exception("Green prop materials not rendered.");
     if(pixels.Count(p=>p.b>110&&p.b>p.r*1.3f&&p.g>70)<300)throw new Exception("Blue base materials not rendered.");
    }
    File.WriteAllBytes(ScriptRoot+"/Reports/"+name,image.EncodeToPNG());
   }
   finally{RenderTexture.active=previous;camera.targetTexture=oldTarget;camera.aspect=oldAspect;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);}
  }
 }
}
