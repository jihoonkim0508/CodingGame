using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CodingGame.Art.MapPainter.Editor
{
    // Work on a copy only. Colour slots are independent of UV coordinates.
    public static class FloorColorMesh
    {
        public const float GrooveY = -.2f;
        public static Mesh Split(Mesh source, Matrix4x4 meshToModule)
        {
            if(source.subMeshCount==2) return UnityEngine.Object.Instantiate(source);
            var positions=source.vertices.ToList();
            var normals=source.normals.ToList(); bool hasNormals=normals.Count==positions.Count;
            var tangents=source.tangents.ToList(); bool hasTangents=tangents.Count==positions.Count;
            var colors=source.colors.ToList(); bool hasColors=colors.Count==positions.Count;
            var uv=new List<Vector4>[8];
            for(int channel=0;channel<8;channel++) { uv[channel]=new List<Vector4>(); source.GetUVs(channel,uv[channel]); }
            var output=new[]{new List<int>(),new List<int>()};
            float Height(int index)=>meshToModule.MultiplyPoint3x4(positions[index]).y;
            int Intersection(int a,int b)
            {
                float t=(GrooveY-Height(a))/(Height(b)-Height(a)); int index=positions.Count;
                positions.Add(Vector3.LerpUnclamped(positions[a],positions[b],t));
                if(hasNormals) normals.Add(Vector3.LerpUnclamped(normals[a],normals[b],t).normalized);
                if(hasTangents) tangents.Add(Vector4.LerpUnclamped(tangents[a],tangents[b],t));
                if(hasColors) colors.Add(Color.LerpUnclamped(colors[a],colors[b],t));
                for(int channel=0;channel<8;channel++)if(uv[channel].Count>0)uv[channel].Add(Vector4.LerpUnclamped(uv[channel][a],uv[channel][b],t));
                return index;
            }
            List<int> Clip(int[] triangle,bool upper)
            {
                var polygon=new List<int>();
                for(int i=0;i<3;i++)
                {
                    int a=triangle[i],b=triangle[(i+1)%3];
                    bool insideA=upper?Height(a)>=GrooveY:Height(a)<=GrooveY;
                    bool insideB=upper?Height(b)>=GrooveY:Height(b)<=GrooveY;
                    if(insideA)polygon.Add(a);
                    if(insideA!=insideB)polygon.Add(Intersection(a,b));
                }
                return polygon;
            }
            void AddPolygon(List<int> polygon,int slot)
            {
                for(int i=1;i<polygon.Count-1;i++)
                {
                    int a=polygon[0],b=polygon[i],c=polygon[i+1];
                    if(Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]).sqrMagnitude<1e-15f)continue;
                    output[slot].Add(a); output[slot].Add(b); output[slot].Add(c);
                }
            }
            for(int submesh=0;submesh<source.subMeshCount;submesh++)
            {
                if(source.GetTopology(submesh)!=MeshTopology.Triangles)throw new InvalidOperationException("Floor mesh must use triangles.");
                var indices=source.GetTriangles(submesh);
                for(int i=0;i<indices.Length;i+=3)
                {
                    int[] triangle={indices[i],indices[i+1],indices[i+2]};
                    float min=triangle.Min(Height),max=triangle.Max(Height);
                    if(min>=GrooveY)output[0].AddRange(triangle);
                    else if(max<=GrooveY)output[1].AddRange(triangle);
                    else { AddPolygon(Clip(triangle,true),0); AddPolygon(Clip(triangle,false),1); }
                }
            }
            // Remove unreferenced exporter vertices so bounds describe visible geometry.
            var used=output.SelectMany(list=>list).Distinct().OrderBy(i=>i).ToArray();
            var remap=new Dictionary<int,int>(); for(int i=0;i<used.Length;i++)remap.Add(used[i],i);
            var mesh=new Mesh { name=source.name+"_TwoTone", indexFormat=used.Length>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(used.Select(i=>positions[i]).ToList());
            if(hasNormals)mesh.SetNormals(used.Select(i=>normals[i]).ToList());
            if(hasTangents)mesh.SetTangents(used.Select(i=>tangents[i]).ToList());
            if(hasColors)mesh.SetColors(used.Select(i=>colors[i]).ToList());
            for(int channel=0;channel<8;channel++)if(uv[channel].Count>0)mesh.SetUVs(channel,used.Select(i=>uv[channel][i]).ToList());
            mesh.subMeshCount=2;
            for(int slot=0;slot<2;slot++)mesh.SetTriangles(output[slot].Select(i=>remap[i]).ToArray(),slot);
            if(!hasNormals)mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
