using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodingGame.Defense;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CodingGame.Art.MapPainter.Editor
{
    public static class WideRoadVerification
    {
        static string Reports=>Environment.GetEnvironmentVariable("CODINGGAME_ROAD_REPORTS")??"Logs/MapPainter/WideRoads";
        static readonly List<string> log=new List<string>();
        static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);log.Add("PASS: "+message);}
        public static void RunBatch()
        {
            // This method must never run against the live checkout. All scene
            // saving below is to a newly named verification scene in this copy.
            if(!Application.dataPath.Replace('\\','/').Contains("/RoadWidthVerification/UnitySandbox/"))
                throw new InvalidOperationException("Run only in the isolated RoadWidthVerification/UnitySandbox project.");
            Directory.CreateDirectory(Reports);
            try
            {
                var scene=EditorSceneManager.OpenScene("Assets/!_Project/Scenes/Defense.unity",OpenSceneMode.Single);
                var map=MapPainterEditing.FindMap(scene);Require(map&&map.palette,"Defense map and palette loaded");
                map.roadWidthScale=1;WideRoadAssets.Apply(map,false);
                var route=Object.FindObjectsByType<DefenseRoute>(FindObjectsSortMode.None).First(r=>r.Map==map);
                var floors=map.floorCells.ToArray();var roads=map.roadCells.ToArray();var points=map.Waypoints(map.routes[0]);
                var scale=map.transform.localScale;var position=map.transform.position;int size=map.cellSize;
                var sourceHandle=PrefabUtility.GetPrefabInstanceHandle(map.gameObject);
                var props=map.transform.Find("Stage01_Workshop_Art");
                var positions=props?props.Cast<Transform>().ToDictionary(t=>t,t=>t.position):new Dictionary<Transform,Vector3>();
                log.Add("Before road world width="+map.RoadHalfWidth*2+"; cell="+size+"; map scale="+scale);
                map.ApplyFloorColors();var actors=Actors(map,points);var camera=CameraFor(scene,map,points);Capture(camera,"Before.png");
                map.roadWidthScale=1.5f;WideRoadAssets.Apply(map,false);map.ApplyFloorColors();Capture(camera,"After.png");
                Require(map.cellSize==size&&map.transform.position==position&&map.transform.localScale==scale,"Map size, transform and grid spacing unchanged");
                Require(floors.SequenceEqual(map.floorCells)&&roads.SequenceEqual(map.roadCells),"Floor and road topology unchanged");
                Require(points.SequenceEqual(map.Waypoints(map.routes[0])),"Enemy waypoints unchanged");
                Require(positions.All(p=>p.Key.position==p.Value),"Prop positions unchanged");
                Require(PrefabUtility.GetPrefabInstanceHandle(map.gameObject)==sourceHandle,"Original map prefab connection preserved");
                Require(Mathf.Abs(map.RoadHalfWidth-1.2f)<.001f,"Defense full road width is 2.4 world units");
                Require(Mathf.Abs(route.Read().HalfWidth-map.RoadHalfWidth)<.001f,"Gameplay route width matches visual width");
                Meshes(map);Coverage(map);
                var sequence=map.GetComponent<MapBuildSequence>();
                if(sequence)
                {
                    var transforms=map.roadGroup.GetComponentsInChildren<Transform>().ToDictionary(t=>t,t=>(t.localPosition,t.localScale));
                    sequence.BeginPreview();sequence.SamplePreview(.5f);Capture(camera,"BuildPreview.png");sequence.Finish(false);
                    Require(transforms.All(p=>p.Key.localPosition==p.Value.localPosition&&p.Key.localScale==p.Value.localScale),"Build preview restores widened geometry transforms");
                }
                foreach(var go in actors)Object.DestroyImmediate(go);Object.DestroyImmediate(camera.gameObject);
                Require(EditorSceneManager.SaveScene(scene,"Assets/WideRoadRoundtrip.unity",true),"Saved a new verification scene copy");
                scene=EditorSceneManager.OpenScene("Assets/WideRoadRoundtrip.unity",OpenSceneMode.Single);map=MapPainterEditing.FindMap(scene);
                Require(Mathf.Abs(map.roadWidthScale-1.5f)<.001f,"Scene round-trip preserves width");Meshes(map);Cases();
                log.Add("RESULT: PASS");File.WriteAllLines(Path.Combine(Reports,"Validation.txt"),log);EditorApplication.Exit(0);
            }
            catch(Exception e){log.Add(e.ToString());log.Add("RESULT: FAIL");File.WriteAllLines(Path.Combine(Reports,"Validation.txt"),log);Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static List<GameObject> Actors(ModularMapRoot map,Vector3[] points)
        {
            var result=new List<GameObject>();
            foreach(string name in new[]{"Enemy","Warrior","Shooter"})
            {
                string path="Assets/!_Project/Defense/Data/"+name+".asset";
                var prefab=name=="Enemy"?AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path).prefab.gameObject:AssetDatabase.LoadAssetAtPath<RobotDefinition>(path).prefab.gameObject;
                var actor=(GameObject)PrefabUtility.InstantiatePrefab(prefab,map.gameObject.scene);Vector3 direction=(points[1]-points[0]).normalized;
                actor.transform.position=name=="Enemy"?points[0]+direction*1.6f:points[1]+new Vector3(name=="Warrior"?1.2f:-2,0,name=="Warrior"?-.9f:2.7f);
                actor.transform.rotation=Quaternion.LookRotation(name=="Enemy"?direction:Vector3.back);result.Add(actor);
                foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())log.Add(name+" skin world size="+renderer.bounds.size);
                if(name=="Enemy"){var second=Object.Instantiate(actor);second.transform.position+=direction*2;result.Add(second);}
            }
            return result;
        }
        static Camera CameraFor(Scene scene,ModularMapRoot map,Vector3[] points)
        {
            var go=new GameObject("Road width comparison camera");SceneManager.MoveGameObjectToScene(go,scene);var camera=go.AddComponent<Camera>();
            camera.scene=scene;camera.orthographic=true;camera.orthographicSize=7.4f;camera.aspect=16f/9;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=map.blueprintBlue;camera.nearClipPlane=.1f;camera.farClipPlane=150;camera.transform.rotation=Quaternion.Euler(50,12,0);
            camera.transform.position=(points[0]+points[1])*.5f+new Vector3(1.6f,0,0)-camera.transform.forward*32;return camera;
        }
        static void Capture(Camera camera,string name)
        {
            var rt=new RenderTexture(1600,900,24);var image=new Texture2D(1600,900,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Reports,name),image.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
        static void Meshes(ModularMapRoot map)
        {
            var wide=map.roadGroup.GetComponentsInChildren<WideRoadTile>(true);
            log.Add("Road data cells="+map.roadCells.Count+"; markers="+map.roadGroup.GetComponentsInChildren<MapTileMarker>(true).Length+"; wide patches="+wide.Length);
            foreach(var tile in map.roadGroup.GetComponentsInChildren<MapTileMarker>(true))
                if(!map.roadCells.Contains(tile.cell)||!tile.GetComponentInChildren<WideRoadTile>(true))log.Add("Road marker mismatch: "+tile.cell+"; active="+tile.gameObject.activeSelf+"; in data="+map.roadCells.Contains(tile.cell));
            Require(wide.Length==map.roadCells.Count,"Every road tile has widened geometry");
            foreach(var tile in wide)
            {
                var mesh=tile.GetComponent<MeshFilter>().sharedMesh;
                Require(mesh&&EditorUtility.IsPersistent(mesh)&&mesh.subMeshCount==2,"Persistent two-color mesh: "+mesh.name);
                Require(mesh.normals.Length==mesh.vertexCount&&mesh.normals.All(n=>float.IsFinite(n.x)&&n.sqrMagnitude>.9f),"Finite unit normals: "+mesh.name);
                Require(tile.originalRenderers.All(r=>r&&!r.enabled),"Original narrow mesh retained and hidden: "+mesh.name);
            }
        }
        static bool Surface(ModularMapRoot map,Vector3 world)
        {
            foreach(var tile in map.roadGroup.GetComponentsInChildren<WideRoadTile>())
            {
                var filter=tile.GetComponent<MeshFilter>();var p=filter.transform.InverseTransformPoint(world);var mesh=filter.sharedMesh;
                if(p.x<mesh.bounds.min.x-.001f||p.x>mesh.bounds.max.x+.001f||p.z<mesh.bounds.min.z-.001f||p.z>mesh.bounds.max.z+.001f)continue;
                var v=mesh.vertices;var indices=mesh.GetTriangles(0);
                for(int i=0;i<indices.Length;i+=3)
                {
                    var a=v[indices[i]];var b=v[indices[i+1]];var c=v[indices[i+2]];float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(det)<1e-12f)continue;
                    float u=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.z-c.z))/det,w=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.z-c.z))/det;
                    if(u>=-.0001f&&w>=-.0001f&&u+w<=1.0001f)return true;
                }
            }
            return false;
        }
        static void Coverage(ModularMapRoot map)
        {
            var set=new HashSet<GridCell>(map.roadCells);int expanded=0;
            foreach(var c in map.roadCells)
            {
                Require(Surface(map,map.CellPosition(c)),"Road center covered: "+c);int mask=GridTopology.Connections(set,c);
                foreach(var d in GridTopology.Directions.Where((d,i)=>(mask&(1<<i))!=0))Require(Surface(map,map.CellPosition(c)+map.transform.TransformVector(new Vector3(d.x,0,d.z)*map.cellSize*.5f)),"Connected seam covered: "+c+" / "+d);
                if(mask==5||mask==10)foreach(float sign in new[]{-1f,1f})if(Surface(map,map.CellPosition(c)+map.transform.TransformVector((mask==5?Vector3.right:Vector3.forward)*map.cellSize*.65f*sign)))expanded++;
            }
            Require(expanded>10,"Straight surfaces extend beyond old half-cell width");
        }
        static void Cases()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var map=MapPainterEditing.CreateRoot(scene,AssetDatabase.LoadAssetAtPath<ModularMapPalette>(MapPainterAssets.PalettePath),false);map.roadWidthScale=1.5f;
                var cells=new List<GridCell>();for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++)cells.Add(new GridCell(x,z));MapPainterEditing.Paint(map,cells,false,false,false);
                foreach(int mask in new[]{3,6,12,9,11,7,14,13})
                {
                    MapPainterEditing.Paint(map,map.roadCells.ToArray(),true,true,false);var road=new List<GridCell>{new GridCell(0,0)};
                    for(int i=0;i<4;i++)if((mask&(1<<i))!=0)road.Add(GridTopology.Directions[i]);MapPainterEditing.Paint(map,road,true,false,false);
                    foreach(var d in road.Skip(1))for(int j=0;j<=8;j++)Require(Surface(map,new Vector3(d.x,0,d.z)*j/8f),"Continuous rotated corner/T arm: "+mask+" / "+d+" / "+j);
                }
                map.roadWidthScale=1;WideRoadAssets.Apply(map,false);Require(map.GetComponentsInChildren<WideRoadTile>().Length==0,"Width 1 restores original modules");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
