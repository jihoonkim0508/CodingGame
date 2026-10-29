using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace CodingGame.Art.CharacterAnimation
{
 [InitializeOnLoad]
 public static class MeleeSwingRevision
 {
  const string Root="Assets/!_Project/Resources/Models/CharacterAnimation";
  const string Request="Assets/!_Project/Resources/ArtScripts/CharacterAnimation/UpdateMeleeSwing.request.txt";
  static double next;
  static MeleeSwingRevision(){next=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Poll;}
  static void Poll()
  {
   if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request))return;File.Delete(Request);
   try{Refresh();}catch(Exception e){Directory.CreateDirectory(Root+"/Reports~");File.WriteAllText(Root+"/Reports~/UnityMeleeSwing.txt",e+"\nRESULT: FAIL");Debug.LogException(e);}
  }
  static void Check(bool value,string message){if(!value)throw new Exception("Melee swing: "+message);}
  [MenuItem("Tools/Art/Characters/Update Melee Swing Only")]
  public static void Refresh()
  {
   // Keep the user's renamed prefab/controller and all material settings.
   string fbx=Root+"/FBX/AttackRobot.fbx";
   AssetDatabase.ImportAsset(fbx,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
   var embedded=AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Single(c=>c.name=="Melee_Attack");
   var target=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/Melee_Attack.anim");
   Check(target,"Existing attack clip missing");
   EditorUtility.CopySerialized(embedded,target);target.name="Melee_Attack";EditorUtility.SetDirty(target);AssetDatabase.SaveAssets();
   Validate();
  }
  public static void Validate()
  {
   Directory.CreateDirectory(Root+"/Reports~");Directory.CreateDirectory(Root+"/Previews~");
   var log=new List<string>();var scene=EditorSceneManager.NewPreviewScene();
   try
   {
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/AttackRobot.prefab");Check(prefab,"User AttackRobot prefab missing");
    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
    var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();var animator=go.GetComponent<Animator>();
    Check(skin&&skin.sharedMesh&&skin.bones.All(b=>b),"Skin binding");Check(skin.bones.Length==7,"Skeleton preserved");
    var attack=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/Melee_Attack.anim");
    var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/Melee_Idle.anim");
    Check(Mathf.Abs(attack.length-1)<.01f,"Attack duration");
    Check(animator.runtimeAnimatorController.animationClips.Contains(attack),"Controller still references the existing attack clip");
    Check(AnimationUtility.GetCurveBindings(attack).All(b=>string.IsNullOrEmpty(b.path)||go.transform.Find(b.path)),"Animation paths");
    animator.enabled=false;var baked=new Mesh();
    Vector3[] Pose(AnimationClip clip,float time)
    {clip.SampleAnimation(go,time);skin.BakeMesh(baked);return baked.vertices;}
    var original=Pose(idle,0);var start=Pose(attack,0);var end=Pose(attack,attack.length);
    float startError=original.Zip(start,Vector3.Distance).Max(),endError=start.Zip(end,Vector3.Distance).Max();
    Check(startError<.002f&&endError<.002f,"Return to Idle without a pose jump");
    float motion=start.Zip(Pose(attack,.5f),Vector3.Distance).Max();Check(motion>.8f,"Large blade arc");
    foreach(float t in new[]{0,.27f,.40f,.50f,.63f,1})
    {Pose(attack,t);Render(go,skin,scene,"MeleeSwing_"+Mathf.RoundToInt(t*100).ToString("000"));}
    Object.DestroyImmediate(baked);
    animator.enabled=true;animator.Rebind();animator.Update(0);animator.SetTrigger("Attack");animator.Update(.02f);animator.Update(.18f);
    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"),"Attack trigger");animator.Update(2);
    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),"Return to Idle");
    Check(go.transform.localPosition.sqrMagnitude<.0001f,"Stationary pedestal/root");
    log.Add("PASS: existing AttackRobot prefab/controller and original clip GUID retained.");
    log.Add("PASS: "+skin.sharedMesh.vertexCount+" imported vertices, 7 bones, curve paths, large sword sweep ("+motion.ToString("0.###")+" units), stationary actor root.");
    log.Add("PASS: 1 second attack, Idle start error="+startError.ToString("0.######")+", return error="+endError.ToString("0.######")+", Attack trigger and Idle return.");
    log.Add("RESULT: PASS");
   }
   catch(Exception e){log.Add(e.ToString());log.Add("RESULT: FAIL");throw;}
   finally{EditorSceneManager.ClosePreviewScene(scene);File.WriteAllLines(Root+"/Reports~/UnityMeleeSwing.txt",log);}
  }
  static void Render(GameObject actor,SkinnedMeshRenderer skin,UnityEngine.SceneManagement.Scene scene,string name)
  {
   var mesh=new Mesh();skin.BakeMesh(mesh);var pose=new GameObject("Pose snapshot");pose.transform.SetParent(skin.transform,false);
   pose.AddComponent<MeshFilter>().sharedMesh=mesh;pose.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
   bool visible=skin.enabled;skin.enabled=false;
   var camera=new GameObject("Swing preview camera").AddComponent<Camera>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject,scene);
   camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
   camera.orthographic=true;camera.orthographicSize=1.9f;camera.aspect=1;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.11f,.16f);
   // Fixed framing makes the blade's full sweep easy to compare.
   var center=actor.transform.TransformPoint(new Vector3(0,1.05f,0));camera.transform.position=center+new Vector3(3,2.6f,5).normalized*8;camera.transform.LookAt(center);
   var light=new GameObject("Swing preview light").AddComponent<Light>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,scene);
   light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(35,-30,0);
   var target=new RenderTexture(600,600,24);var image=new Texture2D(600,600,TextureFormat.RGB24,false);var previous=RenderTexture.active;
   try
   {
    camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,600,600),0,0);image.Apply();
    Check(image.GetPixels32().Count(p=>p.r>240&&p.b>240&&p.g<20)<100,"No shader errors");
    File.WriteAllBytes(Root+"/Previews~/"+name+".png",image.EncodeToPNG());
   }
   finally
   {
    RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);
    Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(light.gameObject);Object.DestroyImmediate(pose);Object.DestroyImmediate(mesh);skin.enabled=visible;
   }
  }
 }
}
