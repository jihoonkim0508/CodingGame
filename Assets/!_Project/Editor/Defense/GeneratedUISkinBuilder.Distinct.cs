using System.Collections.Generic;
using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace CodingGame.Defense.Editor {
 public static partial class GeneratedUISkinBuilder {
  static readonly Dictionary<string,Sprite> distinct=new Dictionary<string,Sprite>();
  static void LoadDistinct(){
   distinct.Clear();
   ImportDistinct("JellySurface",new Rect(8,53,1758,798),new Vector4(150,150,150,150));
   ImportDistinct("DialogueCardTallName",new Rect(16,34,2144,674),new Vector4(960,0,300,0));
   ImportDistinct("PaletteTray",new Rect(20,31,848,1722),new Vector4(95,110,80,280));
   ImportDistinct("CodeWorkspace",new Rect(16,13,1504,999),new Vector4(70,50,50,70));
   ImportDistinct("StatsCard",new Rect(68,58,952,1337),new Vector4(80,170,60,210));
   ImportDistinct("GoalBanner",new Rect(16,167,2139,406),new Vector4(440,0,160,0));
   ImportDistinct("StudyHudBand",new Rect(30,282,2112,162),new Vector4(70,16,40,16));
   ImportDistinct("DictionaryListPanel",new Rect(20,24,848,1692),new Vector4(48,48,48,180));
   ImportDistinct("PlacementDockPanel",new Rect(24,216,2128,236),new Vector4(64,48,64,48));
   // Leave only the plain strip between the top band and side accent in the stretchable rows.
   ImportDistinct("RobotFormationDockPlain",new Rect(12,204,2148,264),new Vector4(160,148,160,108));
   ImportDistinct("RobotRoleTileV2",new Rect(34,64,1518,860),new Vector4(510,240,400,200));
   ImportDistinct("FormationStartV2",new Rect(48,52,2004,640),new Vector4(130,90,130,90));
   ImportDistinct("FormationStartWarm",new Rect(48,52,2004,640),new Vector4(130,90,130,90));
   ImportDistinct("RobotStatusHeaderRounded",new Rect(0,0,1325,107),new Vector4(66,36,66,36));
   ImportDistinct("WaveHeadingBlue",new Rect(0,0,1944,435),new Vector4(124,124,124,124));
   ImportDistinct("PreviewMonitor",new Rect(41,56,1004,1354),new Vector4(135,140,135,125));
   ImportDistinct("RewardCardV2",new Rect(25,76,1400,955),new Vector4(64,120,64,160));
   ImportDistinct("EnemyInfoCard",new Rect(40,94,2092,545),new Vector4(64,64,64,180));
   ImportDistinct("CoreStatusFrame",new Rect(28,148,2116,426),new Vector4(100,90,100,100));
   ImportDistinct("CoreStatusFill",new Rect(40,302,2092,118),new Vector4(32,16,32,16));
   ImportDistinct("WebtoonResultPanel",new Rect(12,80,1424,952),new Vector4(128,120,128,240));
   ImportDistinct("WebtoonGoalPanel",new Rect(36,244,2100,260),new Vector4(350,0,140,0));
   ImportDistinct("WebtoonClaimButton",new Rect(48,176,2080,400),new Vector4(160,0,160,0));
   ImportDistinct("SkyResultPanel",new Rect(12,80,1424,952),new Vector4(128,120,128,240));
   ImportDistinct("SkyClaimButton",new Rect(48,176,2080,400),new Vector4(160,0,160,0));
  }
  static void ImportDistinct(string name,Rect crop,Vector4 border){
   string path=Root+name+".png";AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
   importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
#pragma warning disable CS0618
   importer.spritesheet=new[]{new SpriteMetaData{name=name,rect=crop,border=border,pivot=new Vector2(.5f,.5f),alignment=0}};
#pragma warning restore CS0618
   importer.SaveAndReimport();distinct[name]=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
  }
  static void Specific(Transform root,string path,string key,Color? tint=null,float scale=6){
   var target=root.Find(path);if(!target)return;var image=target.GetComponent<Image>();if(!image)image=target.gameObject.AddComponent<Image>();
   Skin(image,distinct[key],tint??Color.white);image.pixelsPerUnitMultiplier=scale;
  }
  static RectTransform Child(Transform parent,string name){var t=parent.Find(name) as RectTransform;if(t)return t;var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return go.GetComponent<RectTransform>();}
  static TMP_Text TextLabel(Transform parent,string name,string value,float size,Color color){
   var rect=Child(parent,name);var text=rect.GetComponent<TextMeshProUGUI>();if(!text)text=rect.gameObject.AddComponent<TextMeshProUGUI>();
   text.font=parent.GetComponentInParent<Canvas>(true).GetComponentsInChildren<TMP_Text>(true).First(t=>t.font).font;
   text.text=value;text.fontSize=size;text.color=color;text.fontStyle=FontStyles.Bold;text.raycastTarget=false;text.alignment=TextAlignmentOptions.Left;return text;
  }
  static Image Icon(Transform parent,string name,Sprite sprite,Vector2 anchor,Vector2 position,float size){
   var r=Child(parent,name);SetRect(r,anchor,position,Vector2.one*size,new Vector2(.5f,.5f));var im=r.GetComponent<Image>();if(!im)im=r.gameObject.AddComponent<Image>();im.sprite=sprite;im.preserveAspect=true;im.raycastTarget=false;im.color=Color.white;return im;
  }
  static void StyleDistinct(Transform root){
   if(root.Find("DialoguePanel")){
    var dialog=(RectTransform)root.Find("DialoguePanel");Specific(root,"DialoguePanel","DialogueCardTallName",null,2.41f);dialog.sizeDelta=new Vector2(dialog.sizeDelta.x,264);
    var portrait = (RectTransform)dialog.Find("RobotPortrait");
    SetRect(portrait, new Vector2(0, .5f), new Vector2(107, -2), new Vector2(160, 176), new Vector2(.5f, .5f));
    var speaker=dialog.Find("SpeakerName").GetComponent<TMP_Text>();SetRect(speaker.rectTransform,new Vector2(0,1),new Vector2(110,-5),new Vector2(245,44),new Vector2(0,1));speaker.color=new Color(.08f,.16f,.22f);speaker.text="디버";speaker.fontSize=23;speaker.alignment=TextAlignmentOptions.Center;speaker.margin=Vector4.zero;
    var body=dialog.Find("Dialogue").GetComponent<TMP_Text>();body.rectTransform.offsetMin=new Vector2(210,104);body.rectTransform.offsetMax=new Vector2(-48,-48);body.alignment=TextAlignmentOptions.MidlineLeft;body.margin=Vector4.zero;body.lineSpacing=6;
    var advanceHint=TextLabel(dialog,"AdvanceHint","클릭 또는 Space로 계속",14,new Color(.62f,.8f,.9f,.8f));SetRect(advanceHint.rectTransform,new Vector2(0,0),new Vector2(212,60),new Vector2(310,24),Vector2.zero);advanceHint.fontStyle=FontStyles.Normal;advanceHint.alignment=TextAlignmentOptions.MidlineLeft;advanceHint.margin=Vector4.zero;
    dialog.Find("StageWave").gameObject.SetActive(false);
    SetRect((RectTransform)dialog.Find("NextText"),new Vector2(1,0),new Vector2(-60,60),new Vector2(144,42),new Vector2(1,0));
    SetRect((RectTransform)dialog.Find("PreviousText"),new Vector2(1,0),new Vector2(-218,60),new Vector2(144,42),new Vector2(1,0));
    foreach(var button in new[]{"PreviousText","NextText"}){var text=dialog.Find(button).GetComponentInChildren<TMP_Text>(true);text.fontSize=21;text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=21;text.alignment=TextAlignmentOptions.Center;}
    TriangleDialogueNavigation(dialog);
    return;
   }
   if(!root.Find("Library"))return;
   var bg=root.GetComponent<Image>();if(bg){bg.sprite=null;bg.color=new Color(.075f,.105f,.15f);}
   Specific(root,"Editor","CodeWorkspace",Color.white,4);
   Specific(root,"Editor/TitleBar/FunctionName","RobotStatusHeaderRounded",Color.white);
   Specific(root,"Python","CodeWorkspace",Color.white,4);
   var previewHint=root.Find("CodePreview/PreviewHint")?.GetComponent<TMP_Text>();
   if(previewHint){SetRect(previewHint.rectTransform,new Vector2(.5f,1),new Vector2(0,-15),new Vector2(340,26),new Vector2(.5f,1));previewHint.fontSize=15;previewHint.alignment=TextAlignmentOptions.Center;previewHint.margin=Vector4.zero;}
   var previewStatus=root.Find("CodePreview/Status")?.GetComponent<TMP_Text>();
   if(previewStatus){SetRect(previewStatus.rectTransform,new Vector2(.5f,0),new Vector2(0,14),new Vector2(340,24),new Vector2(.5f,0));previewStatus.fontSize=15;previewStatus.alignment=TextAlignmentOptions.MidlineLeft;previewStatus.margin=Vector4.zero;}
   Specific(root,"HintPopup","CodeWorkspace",Color.white,4);
   foreach(string path in new[]{"Library/Viewport","Editor/Workspace/Viewport","Python/Viewport"}){
    var image=root.Find(path)?.GetComponent<Image>();if(image){
     image.sprite=null;
     // The stencil mask needs opaque alpha even when its own graphic is hidden.
     var mask=image.GetComponent<Mask>();image.color=mask?Color.white:Color.clear;if(mask)mask.showMaskGraphic=false;
    }
   }
   var title=root.Find("Title")?.GetComponent<TMP_Text>();if(title){title.fontSize=24;title.color=new Color(.6f,.86f,.91f);}
   var subtitle=root.Find("Subtitle")?.GetComponent<TMP_Text>();if(subtitle){subtitle.text="블록 목록";subtitle.fontSize=24;subtitle.color=new Color(.64f,.88f,.96f);}
   var categories=root.Find("Library/Categories") as RectTransform;if(categories)Stretch(categories,new Vector2(0,1),Vector2.one,new Vector2(14,-52),new Vector2(-14,-14));
   var libraryView=root.Find("Library/Viewport") as RectTransform;if(libraryView)Stretch(libraryView,Vector2.zero,Vector2.one,new Vector2(14,14),new Vector2(-14,-68));
   var editor=root.Find("Editor");var caption=TextLabel(editor,"WorkspaceLabel","코딩 작업 공간",18,new Color(.65f,.77f,.83f));
   SetRect(caption.rectTransform,new Vector2(0,1),new Vector2(22,-12),new Vector2(500,27),new Vector2(0,1));
   var titlebar=(RectTransform)editor.Find("TitleBar");titlebar.anchorMin=new Vector2(0,1);titlebar.anchorMax=Vector2.one;titlebar.pivot=new Vector2(.5f,1);titlebar.sizeDelta=new Vector2(-32,40);titlebar.anchoredPosition=new Vector2(0,-45);
   var workspace=(RectTransform)editor.Find("Workspace");workspace.anchorMin=Vector2.zero;workspace.anchorMax=Vector2.one;workspace.offsetMin=new Vector2(12,18);workspace.offsetMax=new Vector2(-12,-99);
   var hint=editor.Find("Workspace/Viewport/EmptyHint")?.GetComponent<TMP_Text>();if(hint){hint.fontSize=22;hint.color=new Color(.57f,.67f,.73f);}
   foreach(var block in root.GetComponentsInChildren<CommandBlockView>(true)){
    foreach(var input in block.GetComponentsInChildren<TMP_InputField>(true))if(input.TryGetComponent<Image>(out var image)){
     FlatSlot(image,new Color(.16f,.19f,.27f,.8f));input.textComponent.alignment=TextAlignmentOptions.Center;input.textComponent.margin=Vector4.zero;
    }
    foreach(var zone in block.GetComponentsInChildren<CommandDropZone>(true))if(zone.TryGetComponent<Image>(out var image)){
     FlatSlot(image,zone.Slot==BlockSlotKind.Statement?new Color(.055f,.14f,.2f,.3f):new Color(.1f,.21f,.29f,.7f));
    }
   }
   BuildStats(root);
   RefineEditor(root);
   RefineBlockPickers(root);
  }
  static void FlatSlot(Image image,Color color){
   image.sprite=null;image.type=Image.Type.Simple;image.color=color;
   var shape=image.GetComponent<LearningCardShape>();if(!shape)shape=image.gameObject.AddComponent<LearningCardShape>();shape.Configure(8,0,color,false);
  }
  static void BuildStats(Transform root){
   var card=root.Find("RobotStats");if(!card)return;var old=card.Find("Text")?.GetComponent<TMP_Text>();if(old)old.enabled=false;
   var oldcoins=card.Find("Coins")?.GetComponent<TMP_Text>();if(oldcoins)oldcoins.enabled=false;
   var t=TextLabel(card,"StatTitle","로봇 능력치",19,new Color(.91f,.98f,1));SetRect(t.rectTransform,new Vector2(0,1),new Vector2(24,-7),new Vector2(320,32),new Vector2(0,1));t.alignment=TextAlignmentOptions.MidlineLeft;t.margin=Vector4.zero;
   string[] names={"공격력","사거리","공격 간격","체력"};var values=new TMP_Text[4];
   for(int i=0;i<4;i++){
    float y=-82-i*42;
    var label=TextLabel(card,"StatName"+i,names[i],17,new Color(.72f,.84f,.86f));SetRect(label.rectTransform,new Vector2(0,1),new Vector2(24,y),new Vector2(125,28),new Vector2(0,1));
    var value=TextLabel(card,"StatValue"+i,"0",20,new Color(.96f,.99f,1));SetRect(value.rectTransform,new Vector2(1,1),new Vector2(-24,y),new Vector2(195,28),new Vector2(1,1));value.alignment=TextAlignmentOptions.Right;values[i]=value;
   }
   var extra=TextLabel(card,"StatExtra","",15,new Color(.74f,.85f,.88f));SetRect(extra.rectTransform,new Vector2(0,1),new Vector2(24,-252),new Vector2(340,60),new Vector2(0,1));
   for(int i=0;i<3;i++){var line=Child(card,"StatDivider"+i);var image=line.GetComponent<Image>();if(!image)image=line.gameObject.AddComponent<Image>();image.sprite=null;image.color=new Color(.62f,.84f,.87f,.12f);image.raycastTarget=false;}
   var coins=TextLabel(card,"StatCoins","보유 코인",21,new Color(1,.88f,.56f));SetRect(coins.rectTransform,new Vector2(0,0),new Vector2(24,11),new Vector2(314,30),Vector2.zero);
   var view=card.GetComponent<DefenseRobotStatsCard>();if(!view)view=card.gameObject.AddComponent<DefenseRobotStatsCard>();view.Configure(t,values[0],values[1],values[2],values[3],extra,coins);
  }
  static void BindStats(){
   foreach(var preview in Object.FindObjectsByType<DefenseAttackPreview>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
    var so=new SerializedObject(preview);var old=(TMP_Text)so.FindProperty("stats").objectReferenceValue;var card=old?old.GetComponentInParent<DefenseRobotStatsCard>(true):null;
    if(card){so.FindProperty("statsCard").objectReferenceValue=card;so.ApplyModifiedPropertiesWithoutUndo();}
   }
  }
  static void HUDDistinct(Transform canvas){
   Specific(canvas,"Header","StudyHudBand",new Color(.76f,.91f,1),4);Specific(canvas,"Deck","StudyHudBand",new Color(.7f,.87f,1),4);Specific(canvas,"Guide","GoalBanner",null,6.8f);
   var guide=canvas.Find("Guide");if(guide){var text=guide.GetComponentInChildren<TMP_Text>(true);text.rectTransform.offsetMin=new Vector2(64,4);text.rectTransform.offsetMax=new Vector2(-26,-4);Icon(guide,"GoalIcon",roles[2],new Vector2(0,.5f),new Vector2(31,0),34);}
   Specific(canvas,"Reward/Card","RewardCardV2",null,2);Specific(canvas,"Inventory/Card","PaletteTray",new Color(.85f,.9f,1f),4);Specific(canvas,"Tooltip","EnemyInfoCard",null,4);Specific(canvas,"PlacementToast","StatsCard",new Color(.65f,.38f,.4f),4);
   PolishHUD(canvas);
   RefinePlacementDock(canvas);
   RefineWaveNotice(canvas);
   RefineStatusHeader(canvas);
   RefineCombatControls(canvas);
  }
 }
}
