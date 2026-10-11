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
    public static class RoadShadingVerification
    {
        private static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException("Road shading validation: "+message);}
        public static void RunBatch()
        {
            var palette=MapPainterAssets.Setup();Validate(palette);
            MapPainterVerification.Run(palette);RoadBranchUpgrade.Validate(palette);
        }
        public static void Validate(ModularMapPalette palette)
        {
            var log=new List<string>{"Road normal preservation; Unity "+Application.unityVersion};
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                foreach(var rule in palette.roads.Where(r=>r.connections==3||r.connections==5||r.connections==11))
                {
                    var source=AssetDatabase.LoadAssetAtPath<GameObject>(MapPainterAssets.RoadFolder+"/"+rule.prefab.name+".fbx");
                    var model=(GameObject)PrefabUtility.InstantiatePrefab(rule.prefab,scene);
                    try
                    {
                        var inputs=source.GetComponentsInChildren<MeshFilter>(true);var outputs=model.GetComponentsInChildren<MeshFilter>(true);
                        Check(inputs.Length==outputs.Length,"mesh count retained");int tinyFaces=0;
                        for(int k=0;k<inputs.Length;k++)
                        {
                            var input=inputs[k].sharedMesh;var output=outputs[k].sharedMesh;
                            var inputVertices=input.vertices;var inputNormals=input.normals;
                            var outputVertices=output.vertices;var outputNormals=output.normals;
                            var inputUV=new List<Vector4>();var outputUV=new List<Vector4>();input.GetUVs(0,inputUV);output.GetUVs(0,outputUV);
                            int vertex=0;
                            for(int slot=0;slot<input.subMeshCount;slot++)
                            {
                                var indices=input.GetTriangles(slot);
                                for(int i=0;i<indices.Length;i+=3)
                                {
                                    var a=inputVertices[indices[i]];var b=inputVertices[indices[i+1]];var c=inputVertices[indices[i+2]];
                                    float area=Vector3.Cross(b-a,c-a).magnitude;if(area>1e-10f&&area<1e-5f)tinyFaces++;
                                    for(int j=0;j<3;j++,vertex++)
                                    {
                                        int index=indices[i+j];
                                        Check(Vector3.Distance(inputVertices[index],outputVertices[vertex])<1e-7f,"vertex position retained");
                                        Check(Mathf.Abs(outputNormals[vertex].sqrMagnitude-1)<.001f,"nonzero unit normal on every vertex");
                                        if(inputNormals.Length==inputVertices.Length&&inputNormals[index].sqrMagnitude>1e-12f)
                                            Check(Vector3.Dot(inputNormals[index].normalized,outputNormals[vertex])>.9999f,"authored normal preserved");
                                        if(inputUV.Count==inputVertices.Length)Check(Vector4.Distance(inputUV[index],outputUV[vertex])<1e-7f,"UV retained");
                                    }
                                }
                            }
                            Check(vertex==output.vertexCount,"all triangles retained");
                            Check(output.subMeshCount==2&&output.GetIndexCount(0)>0&&output.GetIndexCount(1)>0,"inner/border color slots retained");
                        }
                        log.Add("PASS: "+source.name+" original geometry/UV/normals retained, two color slots, zero invalid normals; "+tinyFaces+" tiny triangles checked.");
                    }
                    finally{Object.DestroyImmediate(model);}
                }
                Render(scene,palette,false);Render(scene,palette,true);
                log.Add("PASS: corner close-ups rendered with directional shadows disabled/enabled; shaders compile.");
                log.Add("RESULT: PASS");
            }
            catch(Exception ex){log.Add(ex.ToString());log.Add("RESULT: FAIL");throw;}
            finally{EditorSceneManager.ClosePreviewScene(scene);File.WriteAllLines(MapPainterAssets.Root+"/Reports/ShadingValidation.txt",log);}
        }
        private static void Render(Scene scene,ModularMapPalette palette,bool shadows)
        {
            var root=MapPainterEditing.CreateRoot(scene,palette,false);root.cellSize=2;
            var floors=new List<GridCell>();for(int x=-1;x<=2;x++)for(int z=-1;z<=2;z++)floors.Add(new GridCell(x,z));
            MapPainterEditing.Paint(root,floors,false,false,false);
            MapPainterEditing.Paint(root,new[]{new GridCell(-1,0),new GridCell(0,0),new GridCell(1,0),new GridCell(1,1),new GridCell(1,2),new GridCell(0,-1)},true,false,false);
            var cameraGO=new GameObject("Corner Shading Check");SceneManager.MoveGameObjectToScene(cameraGO,scene);
            var camera=cameraGO.AddComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
            camera.orthographic=true;camera.orthographicSize=.55f;camera.aspect=1;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.transform.rotation=Quaternion.Euler(55,-20,0);camera.transform.position=new Vector3(1,0,1)-camera.transform.forward*20;
            var lightGO=new GameObject("Corner Check Light");SceneManager.MoveGameObjectToScene(lightGO,scene);
            var light=lightGO.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.shadows=shadows?LightShadows.Soft:LightShadows.None;
            light.shadowBias=.05f;light.shadowNormalBias=.4f;lightGO.transform.rotation=Quaternion.Euler(50,-30,0);
            var texture=new RenderTexture(960,960,24);var image=new Texture2D(960,960,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                Check(!ShaderUtil.ShaderHasError(Shader.Find("CodingGame/Art/Stylized Map Surface")),"shader compiles");
                camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,960,960),0,0);image.Apply();
                Check(image.GetPixels32().Count(p=>p.r>120&&p.g>80&&p.b<100)>1000,"road color rendered");
                File.WriteAllBytes(MapPainterAssets.Root+"/Reports/CornerShading"+(shadows?"WithShadows":"After")+".png",image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;texture.Release();Object.DestroyImmediate(texture);Object.DestroyImmediate(image);
                Object.DestroyImmediate(root.gameObject);Object.DestroyImmediate(cameraGO);Object.DestroyImmediate(lightGO);
            }
        }
    }
}
