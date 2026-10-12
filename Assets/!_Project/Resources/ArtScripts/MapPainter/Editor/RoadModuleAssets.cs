using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace CodingGame.Art.MapPainter.Editor
{
    public static class RoadModuleAssets
    {
        public static void Setup(ModularMapPalette palette,bool force=false)
        {
            if(!AssetDatabase.IsValidFolder(MapPainterAssets.RoadFolder))return;
            if(palette.roadModelVersion>=3&&!force)return;
            var log=new List<string>();
            foreach(string guid in AssetDatabase.FindAssets("t:GameObject",new[]{MapPainterAssets.RoadFolder}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid); if(!path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase))continue;
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                string prefabPath=MapPainterAssets.Generated+"/Modules/"+source.name+".prefab";
                MapPainterAssets.NormalizeModule(source,false,prefabPath,out var sourceBounds);
                var contents=PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    float insideY=ModuleMeshUtility.TopAt(contents,Vector2.zero);
                    if(float.IsInfinity(insideY))throw new InvalidOperationException(source.name+": no road surface at the cell center.");
                    var bounds=MapPainterAssets.BoundsOf(MapPainterAssets.UsedVertices(contents));
                    int connections=0;
                    for(int i=0;i<4;i++)
                    {
                        var d=GridTopology.Directions[i]; float y=ModuleMeshUtility.TopAt(contents,new Vector2(d.x,d.z)*.49f);
                        if(!float.IsInfinity(y)&&y<=insideY+Mathf.Max(.001f,bounds.size.y*.05f))connections|=1<<i;
                    }
                    // Determine topology from the open edges, not the FBX name (RoadCorner2 is a T).
                    int count=Enumerable.Range(0,4).Count(i=>(connections&(1<<i))!=0);
                    int desired=count==3?11:count==4?15:count==2?
                        (connections==5||connections==10?5:3):-1;
                    if(desired<0)throw new InvalidOperationException(source.name+": expected two, three or four open road edges; detected mask "+connections+".");
                    int turns=Enumerable.Range(0,4).FirstOrDefault(t=>GridTopology.RotateMask(connections,t)==desired);
                    if(GridTopology.RotateMask(connections,turns)!=desired)
                        throw new InvalidOperationException(source.name+": road connection mask "+connections+" cannot be normalized; check raised border geometry.");
                    int meshIndex=0;
                    foreach(var filter in contents.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var renderer=filter.GetComponent<MeshRenderer>(); if(!filter.sharedMesh||!renderer)continue;
                        var copy=SplitRoad(filter.sharedMesh,contents.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix,insideY);
                        string meshPath=MapPainterAssets.Generated+"/Modules/"+source.name+"_TwoTone"+meshIndex+".asset";
                        MapPainterAssets.SaveMesh(copy,meshPath);
                        filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                        renderer.sharedMaterials=new[]{MapPainterSurfaceAssets.StylizedMaterial(),MapPainterSurfaceAssets.StylizedMaterial()}; meshIndex++;
                    }
                    foreach(Transform child in contents.transform) { child.localPosition=Quaternion.Euler(0,90*turns,0)*child.localPosition; child.localRotation=Quaternion.Euler(0,90*turns,0)*child.localRotation; }
                    var prefab=PrefabUtility.SaveAsPrefabAsset(contents,prefabPath);
                    var rule=palette.roads.First(r=>r.connections==desired);
                    rule.prefab=prefab; rule.rotationOffset=0;
                    if(desired==5)
                        foreach(var endRule in palette.roads.Where(r=>r.connections==0||r.connections==1)) { endRule.prefab=prefab; endRule.rotationOffset=0; }
                    log.Add(source.name+": source="+sourceBounds.size+"; connections="+connections+"; normalized="+desired+"; rotation="+turns*90+"; inner Y="+insideY+"; two color slots.");
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            palette.roadModelVersion=3; EditorUtility.SetDirty(palette); AssetDatabase.SaveAssets();
            log.Add("Endpoints use the open-ended straight module. T registered="+palette.roads.Any(r=>r.connections==11&&r.prefab)+"; cross registered="+palette.roads.Any(r=>r.connections==15&&r.prefab)+".");
            File.WriteAllLines(MapPainterAssets.Root+"/Reports/RoadModels.txt",log);
        }
        private static Mesh SplitRoad(Mesh source,Matrix4x4 toModule,float insideY)
        {
            var vertices=source.vertices; var sourceNormals=source.normals;
            var positions=new List<Vector3>(); var normals=new List<Vector3>();
            var uv=new List<Vector4>(); var inputUV=new List<Vector4>(); source.GetUVs(0,inputUV);
            var triangles=new[]{new List<int>(),new List<int>()};
            for(int slot=0;slot<source.subMeshCount;slot++)
            {
                var indices=source.GetTriangles(slot);
                for(int i=0;i<indices.Length;i+=3)
                {
                    Vector3 a=vertices[indices[i]],b=vertices[indices[i+1]],c=vertices[indices[i+2]];
                    Vector3 ma=toModule.MultiplyPoint3x4(a),mb=toModule.MultiplyPoint3x4(b),mc=toModule.MultiplyPoint3x4(c);
                    // Vector3.normalized returns zero for small cross products. Fillet triangles
                    // are tiny in the imported FBX's local units, but are visible after scaling.
                    Vector3 normal=UnitNormal(Vector3.Cross(b-a,c-a));
                    Vector3 moduleNormal=UnitNormal(Vector3.Cross(mb-ma,mc-ma));
                    bool inner=moduleNormal.y>.99f&&Mathf.Abs(ma.y-insideY)<.0001f&&Mathf.Abs(mb.y-insideY)<.0001f&&Mathf.Abs(mc.y-insideY)<.0001f;
                    for(int j=0;j<3;j++)
                    {
                        int index=indices[i+j];
                        triangles[inner?0:1].Add(positions.Count); positions.Add(vertices[index]);
                        // Keep the authored smoothing across the rounded corner and hard borders.
                        normals.Add(sourceNormals.Length==vertices.Length&&sourceNormals[index].sqrMagnitude>1e-12f?UnitNormal(sourceNormals[index]):normal);
                        if(inputUV.Count==vertices.Length)uv.Add(inputUV[index]);
                    }
                }
            }
            if(triangles[0].Count==0||triangles[1].Count==0)throw new InvalidOperationException(source.name+": road inner/border split is empty.");
            var mesh=new Mesh {name=source.name+"_TwoTone",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(positions); mesh.SetNormals(normals); if(uv.Count>0)mesh.SetUVs(0,uv);
            mesh.subMeshCount=2; for(int s=0;s<2;s++)mesh.SetTriangles(triangles[s],s); mesh.RecalculateBounds(); return mesh;
        }
        private static Vector3 UnitNormal(Vector3 value)
        {
            float lengthSquared=value.sqrMagnitude;
            return lengthSquared>1e-20f?value/Mathf.Sqrt(lengthSquared):Vector3.up;
        }
    }
}
