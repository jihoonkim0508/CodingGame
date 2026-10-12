using System.Collections.Generic;
using UnityEngine;

namespace CodingGame.Art.MapPainter.Editor
{
    public static class ModuleMeshUtility
    {
        // Generated copies only: suppress buried walls and use planar face normals.
        public static Mesh CleanFloor(Mesh source, Matrix4x4 toModule, int exposedEdges)
        {
            var positions=source.vertices; var sourceUV=new List<Vector4>[8];
            for(int c=0;c<8;c++) { sourceUV[c]=new List<Vector4>(); source.GetUVs(c,sourceUV[c]); }
            var outputPositions=new List<Vector3>(); var normals=new List<Vector3>();
            var outputUV=new List<Vector4>[8]; for(int c=0;c<8;c++)outputUV[c]=new List<Vector4>();
            var triangles=new List<int>[source.subMeshCount];
            for(int slot=0;slot<source.subMeshCount;slot++)
            {
                triangles[slot]=new List<int>(); var indices=source.GetTriangles(slot);
                for(int i=0;i<indices.Length;i+=3)
                {
                    Vector3 a=positions[indices[i]],b=positions[indices[i+1]],c=positions[indices[i+2]];
                    Vector3 ma=toModule.MultiplyPoint3x4(a),mb=toModule.MultiplyPoint3x4(b),mc=toModule.MultiplyPoint3x4(c);
                    bool buried=false;
                    for(int side=0;side<4;side++)
                    {
                        if((exposedEdges&(1<<side))!=0)continue;
                        var d=GridTopology.Directions[side];
                        float Projection(Vector3 p)=>p.x*d.x+p.z*d.z;
                        if(Projection(ma)>.488f&&Projection(mb)>.488f&&Projection(mc)>.488f)buried=true;
                    }
                    if(buried)continue;
                    Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
                    if(normal.sqrMagnitude<.5f)continue;
                    for(int j=0;j<3;j++)
                    {
                        int sourceIndex=indices[i+j]; triangles[slot].Add(outputPositions.Count);
                        outputPositions.Add(positions[sourceIndex]); normals.Add(normal);
                        for(int channel=0;channel<8;channel++)if(sourceUV[channel].Count==positions.Length)outputUV[channel].Add(sourceUV[channel][sourceIndex]);
                    }
                }
            }
            var mesh=new Mesh {name=source.name+"_Joined",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(outputPositions); mesh.SetNormals(normals);
            for(int c=0;c<8;c++)if(outputUV[c].Count>0)mesh.SetUVs(c,outputUV[c]);
            mesh.subMeshCount=triangles.Length; for(int s=0;s<triangles.Length;s++)mesh.SetTriangles(triangles[s],s);
            mesh.RecalculateBounds(); return mesh;
        }
        public static float TopAt(GameObject module, Vector2 point)
        {
            float top=float.NegativeInfinity;
            foreach(var filter in module.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh; if(!mesh)continue;
                var vertices=mesh.vertices; var matrix=module.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                var ray=new Ray(new Vector3(point.x,10,point.y),Vector3.down);
                foreach(int slot in System.Linq.Enumerable.Range(0,mesh.subMeshCount))
                {
                    var indices=mesh.GetTriangles(slot);
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        Vector3 a=matrix.MultiplyPoint3x4(vertices[indices[i]]),b=matrix.MultiplyPoint3x4(vertices[indices[i+1]]),c=matrix.MultiplyPoint3x4(vertices[indices[i+2]]);
                        Vector3 e1=b-a,e2=c-a,p=Vector3.Cross(ray.direction,e2); float det=Vector3.Dot(e1,p);
                        if(Mathf.Abs(det)<1e-9f)continue;
                        Vector3 t=ray.origin-a; float u=Vector3.Dot(t,p)/det; if(u<-.00001f||u>1.00001f)continue;
                        Vector3 q=Vector3.Cross(t,e1); float v=Vector3.Dot(ray.direction,q)/det; if(v<-.00001f||u+v>1.00001f)continue;
                        float distance=Vector3.Dot(e2,q)/det; if(distance>=0)top=Mathf.Max(top,10-distance);
                    }
                }
            }
            return top;
        }
    }
}
