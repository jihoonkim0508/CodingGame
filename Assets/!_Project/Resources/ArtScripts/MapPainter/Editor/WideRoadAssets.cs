using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace CodingGame.Art.MapPainter.Editor
{
    // Wider paths cannot be made by scaling individual square modules: the ends
    // would overlap at bends. Bake one union, then partition its triangles among
    // the existing road tiles so prefab export and the build sequence still work.
    public static class WideRoadAssets
    {
        const int Resolution=20;
        const float Border=.012f;
        const string Folder=MapPainterAssets.Generated+"/WideRoads";

        public static void Apply(ModularMapRoot map,bool undo=true)
        {
            if(map.roadWidthScale<=1.0001f)
            {
                foreach(var wide in map.GetComponentsInChildren<WideRoadTile>(true))
                {
                    for(int i=0;i<wide.originalRenderers.Length;i++)if(wide.originalRenderers[i])
                    {
                        var renderer=wide.originalRenderers[i];if(undo)Undo.RecordObject(renderer,"Restore original road");
                        renderer.enabled=wide.originalEnabled[i];PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                    if(undo)Undo.RecordObject(wide.gameObject,"Restore original road");
                    wide.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(wide.gameObject);
                }
                return;
            }
            if(map.roadCells.Count==0)return;
            if(!map.palette||!map.roadGroup)throw new InvalidOperationException("Road palette/group missing.");
            var roads=map.roadCells.Distinct().OrderBy(c=>c.z).ThenBy(c=>c.x).ToArray();
            var floors=new HashSet<GridCell>(map.floorCells);
            float width=Mathf.Round(Mathf.Clamp(map.roadWidthScale,1,1.75f)*1000)/1000;
            var reference=map.palette.roads.FirstOrDefault(r=>r.connections==5)?.prefab;
            if(!reference)throw new InvalidOperationException("Register a straight road module first.");
            var bounds=MapPainterAssets.BoundsOf(MapPainterAssets.UsedVertices(reference));
            float innerY=ModuleMeshUtility.TopAt(reference,Vector2.zero);
            float topY=Mathf.Max(bounds.max.y,innerY+.001f), bottomY=bounds.min.y;
            string identity="WideRoad-v4/"+width.ToString("R",CultureInfo.InvariantCulture)+"/"+innerY.ToString("R",CultureInfo.InvariantCulture)+"/"+topY.ToString("R",CultureInfo.InvariantCulture);
            identity+="/R"+string.Join(";",roads.Select(c=>c.x+","+c.z));
            identity+="/F"+string.Join(";",floors.OrderBy(c=>c.z).ThenBy(c=>c.x).Select(c=>c.x+","+c.z));
            string hash;
            using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-","").Substring(0,24);
            // Keep the main mesh name equal to its filename as Unity expects.
            // The containing directory identifies the width/topology cache.
            string directory=Folder+"/"+hash;
            string path=directory+"/"+Name(roads[0])+".asset";
            var meshes=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToDictionary(m=>m.name);
            if(meshes.Count==0)
            {
                MapPainterAssets.EnsureFolder(directory);
                var field=new Field(roads,floors,(width-1)*.5f);
                var builders=roads.ToDictionary(c=>c,c=>new Builder(c));
                Bake(field,builders,innerY,topY,bottomY);
                foreach(var cell in roads)
                {
                    var mesh=builders[cell].Finish();
                    if(mesh.vertexCount==0){Object.DestroyImmediate(mesh);continue;}
                    if(meshes.Count==0)AssetDatabase.CreateAsset(mesh,path);else AssetDatabase.AddObjectToAsset(mesh,path);
                    // CreateAsset names its main object after the file. Keep the
                    // per-cell name even for that first mesh, including reloads.
                    mesh.name=Name(cell);EditorUtility.SetDirty(mesh);meshes.Add(mesh.name,mesh);
                }
                AssetDatabase.SaveAssets();
            }
            foreach(var tile in map.roadGroup.GetComponentsInChildren<MapTileMarker>(true))
            {
                if(!tile.road||!map.IsTile(tile)||!meshes.TryGetValue(Name(tile.cell),out var mesh))continue;
                var wide=tile.GetComponentInChildren<WideRoadTile>(true);
                if(!wide)
                {
                    var originals=tile.GetComponentsInChildren<Renderer>(true);
                    var model=new GameObject("Wide Road");model.transform.SetParent(tile.transform,false);
                    model.AddComponent<MeshFilter>();model.AddComponent<MeshRenderer>();wide=model.AddComponent<WideRoadTile>();
                    wide.originalRenderers=originals;wide.originalEnabled=originals.Select(r=>r.enabled).ToArray();
                    model.transform.localScale=map.ModuleScale(true);
                    if(undo)Undo.RegisterCreatedObjectUndo(model,"Widen connected roads");
                }
                if(!wide.gameObject.activeSelf)
                {if(undo)Undo.RecordObject(wide.gameObject,"Show wide road");wide.gameObject.SetActive(true);PrefabUtility.RecordPrefabInstancePropertyModifications(wide.gameObject);}
                var filter=wide.GetComponent<MeshFilter>();var target=wide.GetComponent<MeshRenderer>();
                if(undo){Undo.RecordObject(filter,"Change road width");Undo.RecordObject(target,"Change road width");}
                filter.sharedMesh=mesh;target.sharedMaterials=MapPainterAssets.TwoToneMaterials();
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                foreach(var renderer in wide.originalRenderers)if(renderer)
                {
                    if(undo)Undo.RecordObject(renderer,"Hide narrow road preview");renderer.enabled=false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
            }
            EditorUtility.SetDirty(map);
        }
        static string Name(GridCell c)=>"Road_"+c.x+"_"+c.z;

        sealed class Field
        {
            public readonly GridCell[] roads;
            readonly HashSet<GridCell> roadSet,floors;
            readonly List<(Vector2 a,Vector2 b)> floorEdges=new List<(Vector2,Vector2)>();
            readonly float dilation;
            readonly int[] masks;
            public Field(GridCell[] roads,HashSet<GridCell> floors,float dilation)
            {
                this.roads=roads;this.floors=floors;this.dilation=dilation;roadSet=new HashSet<GridCell>(roads);
                masks=roads.Select(c=>GridTopology.Connections(roadSet,c)).ToArray();
                foreach(var cell in floors)for(int i=0;i<4;i++)if(!floors.Contains(cell+GridTopology.Directions[i]))
                {
                    var d=GridTopology.Directions[i]; var normal=new Vector2(d.x,d.z);var tangent=new Vector2(-normal.y,normal.x)*.5f;
                    var center=new Vector2(cell.x,cell.z)+normal*.5f;floorEdges.Add((center-tangent,center+tangent));
                }
            }
            static float Box(Vector2 p)
            {
                var q=new Vector2(Mathf.Abs(p.x)-.5f,Mathf.Abs(p.y)-.5f);
                return new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0);
            }
            public float At(Vector2 p)
            {
                float value=float.PositiveInfinity;
                for(int i=0;i<roads.Length;i++)
                {
                    Vector2 local=p-new Vector2(roads[i].x,roads[i].z);
                    // Only nearby rectangles can be the closest. The expanded
                    // box has round exterior corners; the union removes seams.
                    if(Mathf.Abs(local.x)>2||Mathf.Abs(local.y)>2)continue;
                    float distance=Box(local)-dilation;
                    int mask=masks[i];
                    if(mask==0)distance=Mathf.Max(distance,Mathf.Abs(local.y)-.5f);
                    else if((mask&(mask-1))==0)
                    {
                        var d=GridTopology.Directions[mask==1?0:mask==2?1:mask==4?2:3];
                        distance=Mathf.Max(distance,-local.x*d.x-local.y*d.z-.5f);
                    }
                    value=Mathf.Min(value,distance);
                }
                float edge=float.PositiveInfinity;
                foreach(var segment in floorEdges)
                {
                    Vector2 ab=segment.b-segment.a;
                    edge=Mathf.Min(edge,(p-segment.a-ab*Mathf.Clamp01(Vector2.Dot(p-segment.a,ab)/ab.sqrMagnitude)).sqrMagnitude);
                }
                bool inside=floors.Contains(new GridCell(Mathf.FloorToInt(p.x+.5f),Mathf.FloorToInt(p.y+.5f)));
                // The floor's authored edge bevel begins .025 of a cell inwards.
                return Mathf.Max(value,(inside?-1:1)*Mathf.Sqrt(edge)+.025f);
            }
            public Vector2 Normal(Vector2 p)
            {
                const float e=.0005f;
                return new Vector2(At(p+Vector2.right*e)-At(p-Vector2.right*e),At(p+Vector2.up*e)-At(p-Vector2.up*e)).normalized;
            }
            public GridCell Owner(Vector2 p)
            {
                float best=float.PositiveInfinity;GridCell owner=roads[0];
                foreach(var cell in roads){float d=(p-new Vector2(cell.x,cell.z)).sqrMagnitude;if(d<best){best=d;owner=cell;}}
                return owner;
            }
        }
        readonly struct Sample
        {
            public readonly Vector2 p;public readonly float d;
            public Sample(Vector2 p,float d){this.p=p;this.d=d;}
        }
        static List<Sample> Clip(List<Sample> input,float threshold,bool below)
        {
            var result=new List<Sample>(); if(input.Count==0)return result;
            Sample a=input[input.Count-1];bool aIn=below?a.d<=threshold:a.d>=threshold;
            foreach(var b in input)
            {
                bool bIn=below?b.d<=threshold:b.d>=threshold;
                if(aIn!=bIn){float t=(threshold-a.d)/(b.d-a.d);result.Add(new Sample(Vector2.LerpUnclamped(a.p,b.p,t),threshold));}
                if(bIn)result.Add(b);a=b;aIn=bIn;
            }
            return result;
        }
        static void Bake(Field field,Dictionary<GridCell,Builder> builders,float innerY,float topY,float bottomY)
        {
            int minX=(field.roads.Min(c=>c.x)-1)*Resolution,maxX=(field.roads.Max(c=>c.x)+1)*Resolution;
            int minZ=(field.roads.Min(c=>c.z)-1)*Resolution,maxZ=(field.roads.Max(c=>c.z)+1)*Resolution;
            var samples=new Sample[maxX-minX+1,maxZ-minZ+1];
            for(int z=minZ;z<=maxZ;z++)for(int x=minX;x<=maxX;x++)
            {var p=new Vector2((float)x/Resolution,(float)z/Resolution);samples[x-minX,z-minZ]=new Sample(p,field.At(p));}
            for(int z=minZ;z<maxZ;z++)for(int x=minX;x<maxX;x++)
            {
                var a=samples[x-minX,z-minZ];var b=samples[x-minX+1,z-minZ];
                var c=samples[x-minX+1,z-minZ+1];var d=samples[x-minX,z-minZ+1];
                // Merge uninterrupted flat spans, retaining separate tile owners
                // for the build sequence. Only the curved edge needs fine cuts.
                if(Inside(x,z))
                {
                    var owner=field.Owner((a.p+c.p)*.5f);int end=x+1;
                    while(end<maxX&&Inside(end,z)&&field.Owner(new Vector2((end+.5f)/Resolution,(z+.5f)/Resolution)).Equals(owner))end++;
                    var right=samples[end-minX,z-minZ].p;var far=samples[end-minX,z-minZ+1].p;
                    Quad(builders[owner],a.p,right,far,d.p,innerY,Vector3.up,0);
                    Quad(builders[owner],a.p,right,far,d.p,bottomY,Vector3.down,1);
                    x=end-1;continue;
                }
                Triangle(a,b,c);Triangle(a,c,d);
            }
            bool Inside(int x,int z)=>samples[x-minX,z-minZ].d<-Border-.000001f&&samples[x-minX+1,z-minZ].d<-Border-.000001f
                &&samples[x-minX+1,z-minZ+1].d<-Border-.000001f&&samples[x-minX,z-minZ+1].d<-Border-.000001f;
            void Quad(Builder builder,Vector2 a,Vector2 b,Vector2 c,Vector2 d,float y,Vector3 normal,int slot)
            {
                var aa=new Vector3(a.x,y,a.y);var bb=new Vector3(b.x,y,b.y);var cc=new Vector3(c.x,y,c.y);var dd=new Vector3(d.x,y,d.y);
                builder.Tri(aa,bb,cc,normal,slot);builder.Tri(aa,cc,dd,normal,slot);
            }
            void Triangle(Sample a,Sample b,Sample c)
            {
                if(a.d>0&&b.d>0&&c.d>0)return;
                var triangle=new List<Sample>{a,b,c};
                Surface(Clip(triangle,-Border,true),innerY,0,Vector3.up);
                Surface(Clip(Clip(triangle,0,true),-Border,false),topY,1,Vector3.up);
                Surface(Clip(triangle,0,true),bottomY,1,Vector3.down);
                Wall(triangle,0,bottomY,topY,false);Wall(triangle,-Border,innerY,topY,true);
            }
            void Surface(List<Sample> polygon,float y,int slot,Vector3 normal)
            {
                for(int i=1;i+1<polygon.Count;i++)
                {
                    Vector2 a=polygon[0].p,b=polygon[i].p,c=polygon[i+1].p;
                    var owner=field.Owner((a+b+c)/3);builders[owner].Tri(new Vector3(a.x,y,a.y),new Vector3(b.x,y,b.y),new Vector3(c.x,y,c.y),normal,slot);
                }
            }
            void Wall(List<Sample> triangle,float threshold,float low,float high,bool inward)
            {
                var points=new List<Vector2>();
                for(int i=0;i<3;i++)
                {
                    var a=triangle[i];var b=triangle[(i+1)%3];
                    if((a.d<threshold)==(b.d<threshold))continue;
                    var p=Vector2.LerpUnclamped(a.p,b.p,(threshold-a.d)/(b.d-a.d));
                    if(points.All(q=>(q-p).sqrMagnitude>1e-12f))points.Add(p);
                }
                if(points.Count!=2)return;
                var p0=points[0];var p1=points[1];var middle=(p0+p1)*.5f;
                var n=field.Normal(middle)*(inward?-1:1);var normal=new Vector3(n.x,0,n.y);
                var a0=new Vector3(p0.x,low,p0.y);var a1=new Vector3(p0.x,high,p0.y);
                var b0=new Vector3(p1.x,low,p1.y);var b1=new Vector3(p1.x,high,p1.y);
                var owner=field.Owner(middle);builders[owner].Tri(a0,a1,b1,normal,1);builders[owner].Tri(a0,b1,b0,normal,1);
            }
        }
        sealed class Builder
        {
            readonly GridCell cell;
            readonly List<Vector3> positions=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();
            readonly List<int>[] triangles={new List<int>(),new List<int>()};
            readonly Dictionary<(Vector3Int,Vector3Int),int> vertices=new Dictionary<(Vector3Int,Vector3Int),int>();
            public Builder(GridCell cell){this.cell=cell;}
            public void Tri(Vector3 a,Vector3 b,Vector3 c,Vector3 normal,int slot)
            {
                Vector3 cross=Vector3.Cross(b-a,c-a);if(cross.sqrMagnitude<1e-18f)return;
                if(Vector3.Dot(cross,normal)<0){var temp=b;b=c;c=temp;}
                int ia=Vertex(a,normal),ib=Vertex(b,normal),ic=Vertex(c,normal);
                if(ia==ib||ib==ic||ia==ic)return;
                triangles[slot].Add(ia);triangles[slot].Add(ib);triangles[slot].Add(ic);
            }
            int Vertex(Vector3 p,Vector3 normal)
            {
                var key=(Vector3Int.RoundToInt(p*1000000),Vector3Int.RoundToInt(normal*10000));
                if(vertices.TryGetValue(key,out int index))return index;
                index=positions.Count;vertices.Add(key,index);positions.Add(p-new Vector3(cell.x,0,cell.z));normals.Add(normal);uv.Add(new Vector2(p.x,p.z));return index;
            }
            public Mesh Finish()
            {
                var mesh=new Mesh{name=Name(cell),indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.subMeshCount=2;
                for(int slot=0;slot<2;slot++)mesh.SetTriangles(triangles[slot],slot);
                mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
