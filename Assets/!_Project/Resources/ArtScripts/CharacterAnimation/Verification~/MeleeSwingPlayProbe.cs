#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEditor;
using UnityEngine;
public class MeleeSwingPlayProbe:MonoBehaviour
{
 const string Root="Assets/!_Project/Resources/Models/CharacterAnimation";
 float started;
 IEnumerator Start()
 {
  started=Time.realtimeSinceStartup;var routine=Run();
  while(true){bool more;try{more=routine.MoveNext();}catch(Exception e){Finish(e.ToString());yield break;}if(!more)break;yield return routine.Current;}
  Finish(null);
 }
 void Update(){if(started>0&&Time.realtimeSinceStartup-started>25)Finish("Playback timed out.");}
 Vector3[] Pose(SkinnedMeshRenderer skin){var mesh=new Mesh();skin.BakeMesh(mesh);var p=mesh.vertices;Destroy(mesh);return p;}
 IEnumerator Run()
 {
  Application.runInBackground=true;
  var actor=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/AttackRobot.prefab"));var animator=actor.GetComponent<Animator>();var skin=actor.GetComponentInChildren<SkinnedMeshRenderer>();
  yield return null;yield return null;
  var original=Pose(skin);animator.SetTrigger("Attack");yield return new WaitForSeconds(.48f);
  if(!animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))throw new Exception("Attack state not playing.");
  float displacement=original.Zip(Pose(skin),Vector3.Distance).Max();if(displacement<.8f)throw new Exception("Large sword swing not playing in actual Play mode: "+displacement);
  yield return new WaitForSeconds(1.2f);
  if(!animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"))throw new Exception("Did not return to Idle.");
  if(actor.transform.localPosition.sqrMagnitude>.0001f)throw new Exception("Actor root drift.");
  if(!skin.sharedMaterial.GetTexture("_BaseMap"))throw new Exception("Original texture lost.");
 }
 void Finish(string error){File.WriteAllText(Root+"/Reports~/UnityMeleeSwingPlay.txt",error==null?"PASS: actual Play mode Attack trigger, large sword sweep, Idle return, stationary actor root and original material.\nRESULT: PASS":error+"\nRESULT: FAIL");EditorApplication.Exit(error==null?0:1);}
}
#endif
