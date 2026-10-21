#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public class AnimationPlaybackProbe:MonoBehaviour
{
 const string Root="Assets/!_Project/Resources/Models/CharacterAnimation";
 readonly List<string> log=new List<string>();float started;
 IEnumerator Start()
 {
  started=Time.realtimeSinceStartup;var routine=Run();
  while(true)
  {
   bool more;try{more=routine.MoveNext();}catch(Exception e){Finish(e.ToString());yield break;}
   if(!more)break;yield return routine.Current;
  }
  Finish(null);
 }
 void Update(){if(started>0&&Time.realtimeSinceStartup-started>25)Finish("Playback test timed out.");}
 void Check(bool value,string message){if(!value)throw new Exception(message);}
 Vector3[] Pose(GameObject go)
 {var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();skin.BakeMesh(mesh);var result=mesh.vertices;Destroy(mesh);return result;}
 IEnumerator Run()
 {
  Application.runInBackground=true;
  var monster=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Monster_Animated.prefab"));
  var melee=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/MeleeRobot_Animated.prefab"));melee.transform.position=Vector3.right*4;
  var ranged=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/RangedRobot_Animated.prefab"));ranged.transform.position=Vector3.right*8;
  var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(35,-30,0);
  var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Shadow test floor";floor.transform.position=new Vector3(4,-.025f,0);floor.transform.localScale=new Vector3(1.4f,1,.8f);
  var floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMaterial.SetColor("_BaseColor",new Color(.10f,.16f,.21f));floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
  yield return null;yield return null;
  var walk=Pose(monster);yield return new WaitForSeconds(.23f);
  Check(walk.Zip(Pose(monster),Vector3.Distance).Max()>.005f,"Monster walk did not animate in Play mode.");
  var meleeRest=Pose(melee);var rangedRest=Pose(ranged);
  Check(!monster.GetComponent<Animator>().parameters.Any(p=>p.name=="Die"),"Monster death trigger still exists.");
  melee.GetComponent<Animator>().SetTrigger("Attack");ranged.GetComponent<Animator>().SetTrigger("Attack");
  yield return new WaitForSeconds(.25f);
  Check(rangedRest.Zip(Pose(ranged),Vector3.Distance).Max()>.02f,"Ranged recoil missing.");Capture(ranged,"Play_RangedAttack.png");
  yield return new WaitForSeconds(.20f);
  Check(meleeRest.Zip(Pose(melee),Vector3.Distance).Max()>.15f,"Melee slash missing.");Capture(melee,"Play_MeleeAttack.png");
  yield return new WaitForSeconds(1.3f);
  Check(monster.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Walk"),"Monster walk changed state.");
  Check(melee.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Idle"),"Melee did not return to Idle.");
  Check(ranged.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Idle"),"Ranged did not return to Idle.");
  Check(monster.transform.position.sqrMagnitude<.0001f&&Vector3.Distance(melee.transform.position,Vector3.right*4)<.0001f&&Vector3.Distance(ranged.transform.position,Vector3.right*8)<.0001f,"Actor root drift.");
  Capture(monster,"Play_MonsterWalk.png");
  var skin=monster.GetComponentInChildren<SkinnedMeshRenderer>();var block=new MaterialPropertyBlock();
  block.SetFloat("_Dissolve",.5f);skin.SetPropertyBlock(block);yield return null;Capture(monster,"Play_MonsterDissolve.png");
  block.SetFloat("_Dissolve",1);skin.SetPropertyBlock(block);yield return null;Capture(monster,"Play_MonsterDissolveComplete.png");
  block.SetFloat("_Dissolve",0);skin.SetPropertyBlock(block);
  Check(skin.sharedMaterial.GetFloat("_Dissolve")==0,"Dissolve changed shared material.");
  log.Add("PASS: actual Play mode walk, melee slash, ranged recoil, Idle return, textures, no actor-root drift, death clip removed and per-instance dissolve rendering.");
 }
 void Capture(GameObject actor,string file)
 {
  var camera=new GameObject("Playback Camera").AddComponent<Camera>();var bounds=actor.GetComponentInChildren<SkinnedMeshRenderer>().bounds;
  camera.orthographic=true;camera.orthographicSize=actor.name.StartsWith("Monster")?.85f:1.65f;camera.transform.position=bounds.center+new Vector3(3,2.6f,5).normalized*8;camera.transform.LookAt(bounds.center);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.11f,.16f);
  var target=new RenderTexture(600,600,24);var image=new Texture2D(600,600,TextureFormat.RGB24,false);var previous=RenderTexture.active;
  try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,600,600),0,0);image.Apply();Check(image.GetPixels32().Count(p=>p.r>240&&p.b>240&&p.g<20)<200,"Pink shader pixels.");Check(!ShaderUtil.ShaderHasError(actor.GetComponentInChildren<Renderer>().sharedMaterial.shader),"Character shader compile error.");File.WriteAllBytes(Root+"/Reports~/"+file,image.EncodeToPNG());}
  finally{RenderTexture.active=previous;camera.targetTexture=null;target.Release();Destroy(target);Destroy(image);Destroy(camera.gameObject);}
 }
 void Finish(string error)
 {if(error!=null)log.Add(error);log.Add("RESULT: "+(error==null?"PASS":"FAIL"));File.WriteAllLines(Root+"/Reports~/UnityPlayValidation.txt",log);EditorApplication.Exit(error==null?0:1);}
}
#endif
