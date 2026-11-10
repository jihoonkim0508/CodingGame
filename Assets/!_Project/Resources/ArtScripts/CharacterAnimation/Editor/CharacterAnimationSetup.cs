using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace CodingGame.Art.CharacterAnimation
{
 [InitializeOnLoad]
 public static class CharacterAnimationSetup
 {
  public const string Root="Assets/!_Project/Resources/Models/CharacterAnimation";
  const string Request="Assets/!_Project/Resources/ArtScripts/CharacterAnimation/SetupCharacterAnimations.request.txt";
  static double next;
  static CharacterAnimationSetup(){next=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Poll;}
  static void Poll()
  {
   if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request))return;File.Delete(Request);
   try{Setup();}catch(Exception e){File.WriteAllText(Root+"/Reports~/UnitySetup.txt",e+"\nRESULT: FAIL");Debug.LogException(e);}
  }
  static void Folder(string path)
  {
   if(AssetDatabase.IsValidFolder(path))return;
   if(Directory.Exists(path)){AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);return;}
   string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
  }
  [MenuItem("Tools/Art/Characters/Build and Validate Animated Prefabs")]
  public static void Setup()
  {
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
   foreach(string folder in new[]{"Materials","Clips","Controllers","Prefabs"})Folder(Root+"/"+folder);
   var log=new List<string>();
   string shaderPath=Root+"/Shaders/CharacterSurface.shader";
   AssetDatabase.ImportAsset(shaderPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
   var shader=AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
   Check(shader&&!ShaderUtil.ShaderHasError(shader),"Character Surface shader import");
   // The monster now dies through a shader effect, not an animation state.
   AssetDatabase.DeleteAsset(Root+"/Clips/Monster_Death.anim");
   foreach(string name in new[]{"Monster","MeleeRobot","RangedRobot"})
   {
    string texturePath=Root+"/Textures/"+name+"_Albedo.tga";
    var textureImporter=(TextureImporter)AssetImporter.GetAtPath(texturePath);
    textureImporter.textureType=TextureImporterType.Default;textureImporter.sRGBTexture=true;textureImporter.alphaSource=TextureImporterAlphaSource.None;textureImporter.mipmapEnabled=true;textureImporter.maxTextureSize=2048;textureImporter.SaveAndReimport();
    string materialPath=Root+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
    if(!material){material=new Material(shader){name=name};AssetDatabase.CreateAsset(material,materialPath);}
    material.shader=shader;material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
    material.SetColor("_AccentColor",name=="Monster"?new Color(1,.12f,.14f):name=="MeleeRobot"?new Color(.08f,.65f,1):new Color(.28f,1,.35f));
    material.SetFloat("_Smoothness",name=="Monster"?.38f:.28f);material.SetFloat("_SpecularStrength",name=="Monster"?.17f:.13f);
    material.SetFloat("_LightSoftness",.65f);material.SetFloat("_ShadowStrength",.65f);material.SetFloat("_RimStrength",.08f);material.SetFloat("_AccentGlow",.035f);material.SetFloat("_TextureSaturation",.88f);material.SetFloat("_BuildGlow",0);EditorUtility.SetDirty(material);
    material.SetFloat("_Dissolve",0);material.SetFloat("_DissolveScale",12);material.SetFloat("_DissolveEdgeWidth",.045f);material.SetColor("_DissolveEdgeColor",name=="Monster"?new Color(3,.28f,.04f):material.GetColor("_AccentColor")*2);
    string fbx=Root+"/FBX/"+name+"_Animated.fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(fbx);
    importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=true;
    importer.importNormals=ModelImporterNormals.Import;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.isReadable=true;importer.globalScale=1;
    importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),name+"_Albedo"),material);
    importer.SaveAndReimport();
    var definitions=importer.defaultClipAnimations;
    if(definitions.Length!=(name=="Monster"?1:2))throw new Exception(name+": unexpected FBX take count "+definitions.Length);
    foreach(var clip in definitions)
    {
     clip.name=clip.takeName.Split('|').Last();clip.loopTime=clip.name.EndsWith("Idle")||clip.name.EndsWith("Walk");clip.loopPose=clip.loopTime;
     clip.keepOriginalOrientation=true;clip.keepOriginalPositionXZ=true;clip.keepOriginalPositionY=true;clip.lockRootRotation=true;clip.lockRootPositionXZ=true;clip.lockRootHeightY=true;
    }
    importer.clipAnimations=definitions;importer.SaveAndReimport();
    var embedded=AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
    foreach(var clip in embedded)
    {
     string path=Root+"/Clips/"+clip.name+".anim";var target=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
     if(target){EditorUtility.CopySerialized(clip,target);target.name=clip.name;EditorUtility.SetDirty(target);}
     else{target=Object.Instantiate(clip);target.name=clip.name;AssetDatabase.CreateAsset(target,path);}
    }
    string prefix=name=="Monster"?"Monster":name=="MeleeRobot"?"Melee":"Ranged";
    var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/"+prefix+(name=="Monster"?"_Walk":"_Idle")+".anim");
    var action=name=="Monster"?null:AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/"+prefix+"_Attack.anim");
    if(!idle||(name!="Monster"&&!action))throw new Exception(name+": extracted clips missing.");
    var controller=Controller(name,idle,action);
    var model=AssetDatabase.LoadAssetAtPath<GameObject>(fbx);var go=(GameObject)PrefabUtility.InstantiatePrefab(model);
    try
    {
     PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);go.name=name+"_Animated";
     var animator=go.GetComponent<Animator>();if(!animator)animator=go.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
     foreach(var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>()){renderer.sharedMaterials=new[]{material};renderer.updateWhenOffscreen=true;}
     if(name=="Monster")go.transform.localScale=Vector3.one*.65f;
     PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+name+"_Animated.prefab");
    }
    finally{Object.DestroyImmediate(go);}
    log.Add(name+": Generic rig; "+idle.name+"="+idle.length.ToString("0.###")+"s; "+(action?action.name+"="+action.length.ToString("0.###")+"s":"death clip removed; dissolve shader ready")+"; original albedo texture remapped.");
   }
   AssetDatabase.SaveAssets();Validate();log.Add("RESULT: PASS");File.WriteAllLines(Root+"/Reports~/UnitySetup.txt",log);
  }
  static AnimatorController Controller(string name,AnimationClip idle,AnimationClip action)
  {
   string path=Root+"/Controllers/"+name+".controller";var existing=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
   // Preserve artist-edited controllers on subsequent model reimports.
   if(existing)
   {
    if(name=="Monster")
    {
     var states=existing.layers[0].stateMachine;
     foreach(var state in states.states.Where(s=>s.state.name=="Death").ToArray())states.RemoveState(state.state);
     for(int i=existing.parameters.Length-1;i>=0;i--)if(existing.parameters[i].name=="Die")existing.RemoveParameter(i);
     EditorUtility.SetDirty(existing);
    }
    return existing;
   }
   var controller=AnimatorController.CreateAnimatorControllerAtPath(path);var machine=controller.layers[0].stateMachine;
   var rest=machine.AddState(name=="Monster"?"Walk":"Idle");rest.motion=idle;
   machine.defaultState=rest;rest.writeDefaultValues=false;if(name=="Monster")return controller;
   var active=machine.AddState(name=="Monster"?"Death":"Attack");active.motion=action;active.writeDefaultValues=false;rest.writeDefaultValues=false;machine.defaultState=rest;
   string trigger=name=="Monster"?"Die":"Attack";controller.AddParameter(trigger,AnimatorControllerParameterType.Trigger);
   var enter=rest.AddTransition(active);enter.hasExitTime=false;enter.duration=.06f;enter.AddCondition(AnimatorConditionMode.If,0,trigger);
   if(name!="Monster"){var exit=active.AddTransition(rest);exit.hasExitTime=true;exit.exitTime=1;exit.duration=.08f;}
   return controller;
  }
  static void Check(bool condition,string message){if(!condition)throw new Exception("Character validation: "+message);}
  public static void Validate()
  {
   var scene=EditorSceneManager.NewPreviewScene();var log=new List<string>();
   try
   {
    foreach(string name in new[]{"Monster","MeleeRobot","RangedRobot"})
    {
     string prefix=name=="Monster"?"Monster":name=="MeleeRobot"?"Melee":"Ranged";
     var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+name+"_Animated.prefab");var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
     try
     {
      var skin=go.GetComponentsInChildren<SkinnedMeshRenderer>();Check(skin.Length==1,name+" skin count");Check(skin[0].bones.Length==(name=="Monster"?10:name=="MeleeRobot"?7:6),name+" bone count");
      Check(skin[0].sharedMesh.boneWeights.All(w=>Mathf.Abs(w.weight0-1)<.0001f&&w.weight1==0),name+" rigid weights");
      Check(skin[0].sharedMaterial.GetTexture("_BaseMap"),name+" texture assignment");Check(skin[0].sharedMaterial.shader==AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/CharacterSurface.shader"),name+" custom shader assignment");Check(!ShaderUtil.ShaderHasError(skin[0].sharedMaterial.shader),name+" URP shader");
      var animator=go.GetComponent<Animator>();animator.enabled=false;
      foreach(string suffix in name=="Monster"?new[]{"Walk"}:new[]{"Idle","Attack"})
      {
       var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/"+prefix+"_"+suffix+".anim");Check(clip&&clip.length>.5f,name+" clip duration");
       var bindings=AnimationUtility.GetCurveBindings(clip);Check(bindings.Length>0,name+" animation bindings");
       Check(bindings.All(b=>string.IsNullOrEmpty(b.path)||go.transform.Find(b.path)),name+" animation paths");
       var samples=new List<Vector3[]>();var mesh=new Mesh();
       try
       {
        foreach(float time in new[]{0,clip.length*.44f,clip.length})
        {clip.SampleAnimation(go,time);skin[0].BakeMesh(mesh);samples.Add(mesh.vertices.Select(v=>go.transform.InverseTransformPoint(skin[0].transform.TransformPoint(v))).ToArray());}
       }
       finally{Object.DestroyImmediate(mesh);}
       float motion=samples[0].Zip(samples[1],Vector3.Distance).Max();Check(motion>.005f,name+"/"+suffix+" visible motion");
       float seam=samples[0].Zip(samples[2],Vector3.Distance).Max();if(suffix=="Walk"||suffix=="Idle")Check(seam<.0005f,name+"/"+suffix+" loop seam");
       Check(go.transform.localPosition.sqrMagnitude<.0001f,name+" root stays in place");
       log.Add("PASS "+clip.name+": "+clip.length.ToString("0.###")+"s; motion="+motion.ToString("0.###")+"; seam="+seam.ToString("0.######")+"; bindings="+bindings.Length);
       clip.SampleAnimation(go,clip.length*(suffix=="Attack"&&name=="RangedRobot"?.28f:.44f));
       Render(go,scene,clip.name);
      }
      animator.enabled=true;animator.Rebind();animator.Update(0);
      if(name!="Monster")
      {
       animator.SetTrigger("Attack");animator.Update(.02f);animator.Update(.15f);
       Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"),name+" trigger transition");
       animator.Update(3);Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),name+" final state");
      }
      else
      {
       Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"),"Monster default Walk");
       Check(!animator.parameters.Any(p=>p.name=="Die"),"Monster death trigger removed");
       Check(!AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/Monster_Death.anim"),"Monster death clip removed");
       Check(skin[0].sharedMaterial.HasProperty("_Dissolve"),"Monster dissolve control");
      }
      log.Add("PASS "+name+": skin/texture, controller and in-place root.");
     }
     finally{Object.DestroyImmediate(go);}
    }
    log.Add("RESULT: PASS");
   }
   catch(Exception e){log.Add(e.ToString());log.Add("RESULT: FAIL");throw;}
   finally{EditorSceneManager.ClosePreviewScene(scene);File.WriteAllLines(Root+"/Reports~/UnityValidation.txt",log);}
  }
  static void Render(GameObject actor,UnityEngine.SceneManagement.Scene scene,string name)
  {
   // PreviewScene does not run a normal player skinning update. Render the exact
   // CPU-skinned pose used by validation rather than a stale GPU rest pose.
   var skin=actor.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();skin.BakeMesh(baked);
   var snapshot=new GameObject("Skinned pose snapshot");snapshot.transform.SetParent(skin.transform,false);snapshot.AddComponent<MeshFilter>().sharedMesh=baked;snapshot.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
   bool visible=skin.enabled;skin.enabled=false;
   var cameraGO=new GameObject("Animation Validation Camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGO,scene);var camera=cameraGO.AddComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
   var renderer=actor.GetComponentInChildren<SkinnedMeshRenderer>();var bounds=renderer.bounds;
   camera.orthographic=true;camera.orthographicSize=actor.name.StartsWith("Monster")?.85f:1.65f;camera.aspect=1;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.11f,.16f);
   camera.transform.position=bounds.center+new Vector3(3,2.6f,5).normalized*8;camera.transform.LookAt(bounds.center);
   var lightGO=new GameObject("Validation Light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGO,scene);var light=lightGO.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;lightGO.transform.rotation=Quaternion.Euler(35,-30,0);
   var target=new RenderTexture(600,600,24);var image=new Texture2D(600,600,TextureFormat.RGB24,false);var previous=RenderTexture.active;
   try
   {
    camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,600,600),0,0);image.Apply();var pixels=image.GetPixels32();
    Check(pixels.Count(p=>p.r>240&&p.b>240&&p.g<20)<200,"no pink shader");
    File.WriteAllBytes(Root+"/Reports~/Unity_"+name+".png",image.EncodeToPNG());
   }
   finally{RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);Object.DestroyImmediate(cameraGO);Object.DestroyImmediate(lightGO);Object.DestroyImmediate(snapshot);Object.DestroyImmediate(baked);skin.enabled=visible;}
  }
 }
}
