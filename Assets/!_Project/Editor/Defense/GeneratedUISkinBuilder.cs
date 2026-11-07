using System;
using System.IO;
using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace CodingGame.Defense.Editor {
 public static partial class GeneratedUISkinBuilder {
  const string Root="Assets/!_Project/Resources/LearningSkins/";
  static Sprite panel,button,surface,primaryButton;
  static Sprite[] roles,hud;
  [MenuItem("Tools/Learning UI/Apply Generated Sprite Skins")]
  public static void Apply(){
   if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
   if(EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Preserve unsaved scene first");
   string backup="Temp/GeneratedUISkinBackups/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);
   File.Copy("Assets/!_Project/Scenes/Defense.unity",backup+"/Defense.unity");
   AssetDatabase.Refresh();
   panel=Single("ToolPanel",new Vector4(96,96,96,96));button=Single("ToolButton",new Vector4(150,130,150,130));surface=Single("BlockSurface",new Vector4(48,48,48,48));primaryButton=Single("PrimaryButton",new Vector4(150,130,150,130));
   roles=Atlas("RoleEmblems");hud=Atlas("HudIcons");Single("StageGuide",Vector4.zero);
   if(roles.Length!=6||hud.Length!=6)throw new InvalidOperationException($"Atlas import incomplete: roles={roles.Length} hud={hud.Length}");
   LoadDistinct();
   string[] prefabs={"Assets/!_Project/Defense/Prefabs/RobotCodeEditor.prefab","Assets/!_Project/Defense/Tutorial/TutorialOverlay.prefab"};
   foreach(var path in prefabs){File.Copy(path,backup+"/"+Path.GetFileName(path));var p=PrefabUtility.LoadPrefabContents(path);try{Style(p.transform);StyleDistinct(p.transform);RecordUIOverrides(p.transform);PrefabUtility.SaveAsPrefabAsset(p,path);}finally{PrefabUtility.UnloadPrefabContents(p);}}
   foreach(string name in new[]{"Comparison","Variable"}){string path="Assets/!_Project/UI/CommandBlocks/"+name+".prefab";File.Copy(path,backup+"/"+name+".prefab");var p=PrefabUtility.LoadPrefabContents(path);try{RefineBlockPickers(p.transform);PrefabUtility.SaveAsPrefabAsset(p,path);}finally{PrefabUtility.UnloadPrefabContents(p);}}
   if(EditorSceneManager.GetActiveScene().path!="Assets/!_Project/Scenes/Defense.unity")EditorSceneManager.OpenScene("Assets/!_Project/Scenes/Defense.unity");
   var canvases=Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None);
   foreach(var c in canvases.Where(c=>c.name=="PlayerCanvas"||c.name=="DefenseCanvas"||c.name=="BlockHelpCanvas"))Style(c.transform);
   var editor=Object.FindFirstObjectByType<DefenseCodeEditor>(FindObjectsInactive.Include);var editorSo=new SerializedObject(editor);
   var editorRoot=(GameObject)editorSo.FindProperty("editorRoot").objectReferenceValue;Style(editorRoot.transform);StyleDistinct(editorRoot.transform);
   foreach(var c in canvases.Where(c=>c.name=="PlayerCanvas")){DecorateHUD(c.transform);HUDDistinct(c.transform);}
   foreach(var c in canvases.Where(c=>c.name=="BlockHelpCanvas"))PolishBlockHelp(c.transform);
   // The tutorial instance receives prefab updates; style local overrides as well.
   foreach(var c in canvases.Where(c=>c.name=="TutorialOverlay")){Style(c.transform);StyleDistinct(c.transform);}
   BindStats();
   StylePreviewArena();
   var camera=Camera.main;
   if(camera && camera.rect!=new Rect(0,0,1,1)){
    var old=camera.rect;
    camera.fieldOfView=2*Mathf.Atan(Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)/old.height)*Mathf.Rad2Deg;
    camera.rect=new Rect(0,0,1,1);
    var offset=camera.GetComponent<DefenseCameraLensOffset>();if(!offset)offset=camera.gameObject.AddComponent<DefenseCameraLensOffset>();
    offset.Configure(old.center-new Vector2(.5f,.5f));
   }
   foreach(var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())RecordUIOverrides(root.transform);
   EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
   Debug.Log("Generated sprites applied: HUD, deck, dialogue, developer tool, blocks. Backup "+backup);
  }
  // The mda-upgrade branch's UITheme panels (UIRestyle) are replaced like the old panel_glass; icons and fills are kept.
  internal static bool ThemePanel(Sprite s)=>s.name.StartsWith("panel_glass")||new[]{"ui_card","ui_card_flat","ui_button","ui_pill","ui_bar_top","ui_bar_bottom","ui_chip","ui_tab"}.Contains(s.name);
  static Sprite Single(string name,Vector4 border){
   var path=Root+name+".png";AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
   importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spriteBorder=border;importer.npotScale=TextureImporterNPOTScale.None;
   if(name=="ToolButton"||name=="PrimaryButton") {
    importer.GetSourceTextureWidthAndHeight(out int w,out int h);importer.spriteImportMode=SpriteImportMode.Multiple;
    Rect crop=name=="ToolButton"?new Rect(w*11/1774f,h*129/887f,w*1753/1774f,h*632/887f):new Rect(w*18/1774f,h*63/887f,w*1739/1774f,h*772/887f);
#pragma warning disable CS0618
    importer.spritesheet=new[]{new SpriteMetaData{name=name,rect=crop,border=border,pivot=new Vector2(.5f,.5f),alignment=0}};
#pragma warning restore CS0618
   }
   importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.maxTextureSize=name=="StageGuide"?512:1024;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
  }
  static Sprite[] Atlas(string name){
   var path=Root+name+".png";AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
   importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.maxTextureSize=2048;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.textureCompression=TextureImporterCompression.Uncompressed;
   importer.GetSourceTextureWidthAndHeight(out int sw,out int sh);int w=sw/3,h=sh/2;
   var entries=new SpriteMetaData[6];for(int i=0;i<6;i++)entries[i]=new SpriteMetaData{name=name+i,rect=new Rect(i%3*w,(1-i/3)*h,w,h),pivot=new Vector2(.5f,.5f),alignment=0};
#pragma warning disable CS0618
   importer.spritesheet=entries;
#pragma warning restore CS0618
   importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x=>x.name).ToArray();
  }
  static void Skin(Image image,Sprite sprite,Color tint){
   var old=image.GetComponent<LearningCardShape>();if(old)Object.DestroyImmediate(old);
   image.sprite=sprite;image.type=Image.Type.Sliced;image.color=tint;image.pixelsPerUnitMultiplier=6;
  }
  static void Style(Transform root){
   foreach(var image in root.GetComponentsInChildren<Image>(true)){
    if(image.name=="RobotPortrait"||image.name=="Symbol"||image.name=="BaseShield"||image.name=="Icon"||image.name=="Core shield")continue;
    if(image.sprite && !ThemePanel(image.sprite)&&!image.GetComponent<LearningCardShape>() && image.sprite!=panel && image.sprite!=button&&image.sprite!=surface&&image.sprite!=primaryButton)continue;
    if(image.name=="Reward"||image.name=="Inventory"||image.name=="SystemBoot")continue;
    var zone=image.GetComponent<CommandDropZone>();
    if(zone&&zone.FreePlacement){image.sprite=null;image.color=Color.clear;continue;}
    bool field=image.GetComponent<TMP_InputField>()||image.GetComponent<TMP_Dropdown>()||image.GetComponentInParent<CommandBlockView>()||zone;
    bool primary=image.name=="Start"||image.name=="Claim"||image.name=="NextText";
    bool clickable=image.GetComponent<Selectable>();
    Skin(image,primary?primaryButton:clickable||field?button:panel,field?new Color(.55f,.66f,.74f):Color.white);
   }
   foreach(var text in root.GetComponentsInChildren<TMP_Text>(true)){
    var owner=text.GetComponentInParent<Button>(true);
    if(owner){
     bool gold=owner.name=="Start"||owner.name=="Claim"||owner.name=="NextText";
     text.enableVertexGradient=false;text.color=gold?new Color(.08f,.13f,.19f):new Color(.97f,.99f,1);
     text.fontStyle=FontStyles.Bold;
     continue;
    }
    if(text.GetComponentInParent<CommandBlockView>())text.color=new Color(.94f,.96f,.98f);
    else if(text.name!="Label"||!text.GetComponentInParent<Button>())text.color=new Color(.8f,.88f,.93f);
   }
   foreach(var b in root.GetComponentsInChildren<Button>(true)){
    var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.84f,.95f,1);colors.pressedColor=new Color(.62f,.8f,.9f);colors.selectedColor=Color.white;colors.disabledColor=new Color(.52f,.59f,.65f,.7f);b.colors=colors;
   }
  }
  static void SetRect(RectTransform r,Vector2 anchor,Vector2 pos,Vector2 size,Vector2 pivot){r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=pos;r.sizeDelta=size;}
  static void Picture(Transform parent,Sprite sprite,Vector2 pos,Vector2 size){
   var old=parent.Find("Symbol");if(old)Object.DestroyImmediate(old.gameObject);
   var prior=parent.Find("Generated icon");if(prior)Object.DestroyImmediate(prior.gameObject);
   var go=new GameObject("Generated icon",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;
   SetRect(image.rectTransform,new Vector2(0,.5f),pos,size,new Vector2(.5f,.5f));
  }
  static void DecorateHUD(Transform canvas){
   int[] iconOrder={2,3,0,1};for(int i=0;i<4;i++){
    var stat=canvas.Find("Header/Stat"+i);
    // Icon, caption and value sit directly on the shared header; no individual frames.
    stat.GetComponent<Image>().enabled=false;
    Picture(stat,hud[iconOrder[i]],new Vector2(30,0),new Vector2(46,46));
   }
   var deck=canvas.Find("Deck");
   // Reserve distinct rows for instruction, capacity and cards; no rectangle overlaps.
   var notice=(RectTransform)deck.Find("Notice");notice.anchorMin=new Vector2(0,.78f);notice.anchorMax=new Vector2(.66f,1);notice.offsetMin=new Vector2(24,0);notice.offsetMax=new Vector2(0,-4);
   var capacity=(RectTransform)deck.Find("DeploymentCount");capacity.anchorMin=new Vector2(0,.62f);capacity.anchorMax=new Vector2(.66f,.78f);capacity.offsetMin=new Vector2(24,0);capacity.offsetMax=Vector2.zero;
   var robots=(RectTransform)deck.Find("Robots");SetRect(robots,Vector2.zero,new Vector2(24,10),new Vector2(1240,94),Vector2.zero);
   foreach(var b in robots.GetComponentsInChildren<Button>(true)){
    int index=b.name=="Warrior"?0:b.name=="Tank"?1:b.name=="Shooter"?2:b.name=="Bomber"?3:b.name=="Buffer"?4:5;
    Picture(b.transform,roles[index],new Vector2(49,12),new Vector2(66,66));
    var title=b.transform.Find("RoleName");if(title)SetRect((RectTransform)title,new Vector2(.5f,0),new Vector2(0,6),new Vector2(140,22),new Vector2(.5f,0));
    var count=(RectTransform)b.transform.Find("Label");SetRect(count,new Vector2(1,.5f),new Vector2(-29,10),new Vector2(48,38),new Vector2(.5f,.5f));
    var badge=b.GetComponent<LearningRobotBadge>();if(badge)badge.Setup(b.GetComponent<Image>());
   }
  }
 }
}
