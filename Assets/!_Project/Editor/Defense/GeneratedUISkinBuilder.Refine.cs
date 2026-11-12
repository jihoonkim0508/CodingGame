using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace CodingGame.Defense.Editor {
 public static partial class GeneratedUISkinBuilder {
  static void RoundedControl(Image image,Color color,float radius=7){Plain(image,color);var shape=image.gameObject.AddComponent<LearningCardShape>();shape.Configure(radius,1,new Color(.27f,.46f,.59f),false);}
  static void RefineEditor(Transform root){
   var title=root.Find("Title").GetComponent<TMP_Text>();Stretch(title.rectTransform,new Vector2(0,1),new Vector2(.25f,1),new Vector2(24,-44),new Vector2(-12,-12));title.fontSize=22;title.alignment=TextAlignmentOptions.MidlineLeft;title.margin=Vector4.zero;
   var subtitle=root.Find("Subtitle").GetComponent<TMP_Text>();SetRect(subtitle.rectTransform,new Vector2(.25f,1),new Vector2(16,-12),new Vector2(310,32),new Vector2(0,1));subtitle.fontSize=22;subtitle.alignment=TextAlignmentOptions.MidlineLeft;subtitle.margin=Vector4.zero;
   string[] names={"CloseEditor","Automatic","Hint","Info","RightToggle","LeftToggle"};float[] widths={88,104,96,112,100,100};string[] labels={"닫기","비우기","예제 힌트","블록 도감","우측 미리보기","좌측 미리보기"};
   var buttons=new System.Collections.Generic.List<Button>();
   for(int i=0;i<names.Length;i++){var t=root.Find(names[i]);if(!t)continue;var button=t.GetComponent<Button>();if(!button)continue;buttons.Add(button);SetRect((RectTransform)t,Vector2.one,new Vector2(0,-14),new Vector2(widths[i],34),Vector2.one);RoundedControl(t.GetComponent<Image>(),new Color(.08f,.15f,.21f));
    foreach(var icon in t.GetComponentsInChildren<Image>(true))if(icon.transform!=t)icon.gameObject.SetActive(false);
    var text=t.GetComponentInChildren<TMP_Text>(true);if(text){text.gameObject.SetActive(true);text.text=labels[i];Stretch(text.rectTransform,Vector2.zero,Vector2.one,new Vector2(7,2),new Vector2(-7,-2));text.fontSize=17;text.enableAutoSizing=true;text.fontSizeMin=13;text.fontSizeMax=17;text.color=new Color(.85f,.94f,1);text.alignment=TextAlignmentOptions.Center;text.margin=Vector4.zero;}
   }
   var toolbar=root.GetComponent<DefenseEditorToolbar>();if(!toolbar)toolbar=root.gameObject.AddComponent<DefenseEditorToolbar>();toolbar.Configure(buttons.ToArray());EditorUtility.SetDirty(toolbar);if(PrefabUtility.IsPartOfPrefabInstance(toolbar))PrefabUtility.RecordPrefabInstancePropertyModifications(toolbar);
   var preview=root.Find("CodePreview");if(preview){foreach(var raw in preview.GetComponentsInChildren<RawImage>(true))Stretch(raw.rectTransform,Vector2.zero,Vector2.one,new Vector2(14,44),new Vector2(-14,-58));
    var status=preview.Find("Status")?.GetComponent<TMP_Text>();if(status){Stretch(status.rectTransform,new Vector2(0,0),new Vector2(1,0),new Vector2(18,8),new Vector2(-18,30));status.fontSize=15;status.alignment=TextAlignmentOptions.MidlineLeft;status.margin=Vector4.zero;}
    var hint=preview.Find("PreviewHint")?.GetComponent<TMP_Text>();if(hint){Stretch(hint.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(14,-38),new Vector2(-14,-12));hint.fontSize=14;hint.alignment=TextAlignmentOptions.Center;hint.margin=Vector4.zero;}
   }
   RefineStudioPanels(root);
   AlignCategoryTabs(root);
   for(int i=0;i<4;i++){var name=root.Find("RobotStats/StatName"+i)?.GetComponent<TMP_Text>();var value=root.Find("RobotStats/StatValue"+i)?.GetComponent<TMP_Text>();if(name){name.alignment=TextAlignmentOptions.MidlineLeft;name.margin=Vector4.zero;}if(value){value.alignment=TextAlignmentOptions.MidlineRight;value.fontSize=19;value.margin=Vector4.zero;}}
   var coins=root.Find("RobotStats/StatCoins")?.GetComponent<TMP_Text>();if(coins){coins.fontSize=19;coins.alignment=TextAlignmentOptions.MidlineLeft;coins.margin=Vector4.zero;}
   var marker=root.Find("DragLayer/SnapMarker")?.GetComponent<Image>();if(marker){Plain(marker,new Color(.18f,.94f,.78f,.08f));var shape=marker.gameObject.AddComponent<LearningCardShape>();shape.Configure(7,1.5f,new Color(.36f,1,.84f,.9f),false,false);marker.raycastTarget=false;}
  }
  internal static void RefineStudioPanels(Transform root)
  {
   var backdrop = new Color(.075f, .105f, .15f);
   Plain(root.GetComponent<Image>(), backdrop);
   Plain(root.Find("Background").GetComponent<Image>(), backdrop);
   // Editor/PreviewPanel keeps its StudioPreviewFrame skin (the flat panel lost the preview's visible frame; rolled back 2026-11-12).
   foreach (string path in new[] { "Library", "CodePreview", "RobotStats" })
   {
    var panel = root.Find(path);
    if (!panel || !panel.TryGetComponent<Image>(out var image)) continue;
    image.sprite = null;
    image.type = Image.Type.Simple;
    image.color = new Color(.106f, .164f, .23f);
    var shape = image.GetComponent<LearningCardShape>();
    if (!shape) shape = image.gameObject.AddComponent<LearningCardShape>();
    shape.Configure(12, 1, new Color(.24f, .36f, .47f), false);
    var hint = panel.Find("PreviewHint")?.GetComponent<TMP_Text>();
    if (hint) hint.color = new Color(.74f, .85f, .92f);
   }

   SetStudioSideInsets(root.Find("CodePreview") as RectTransform, 24, -8);
   SetStudioSideInsets(root.Find("LeftPreview") as RectTransform, 24, -8);
   var stats = root.Find("RobotStats") as RectTransform;
   SetStudioSideInsets(stats, 24, -8);
   var library = root.Find("Library") as RectTransform;
   if (stats && library) stats.offsetMin = new Vector2(stats.offsetMin.x, library.offsetMin.y);
   SetStudioSideInsets(root.Find("Editor") as RectTransform, 338, -26);
   SetStudioSideInsets(root.Find("Title") as RectTransform, 24, -12);
   var python = (RectTransform)root.Find("Python");
   python.anchoredPosition = new Vector2(-26, python.anchoredPosition.y);
  }

  static void SetStudioSideInsets(RectTransform rect, float left, float right)
  {
   if (!rect) return;
   rect.offsetMin = new Vector2(left, rect.offsetMin.y);
   rect.offsetMax = new Vector2(right, rect.offsetMax.y);
  }
  static void RefineHelp(Transform card){
   var list=card.Find("BlockList");var viewport=(RectTransform)list.Find("Viewport");Stretch(viewport,Vector2.zero,Vector2.one,new Vector2(18,18),new Vector2(-44,-68));
   var caption=TextLabel(list,"ListCaption","보유 블록",18,new Color(.86f,.91f,1));SetRect(caption.rectTransform,new Vector2(0,1),new Vector2(24,-6),new Vector2(280,34),new Vector2(0,1));caption.alignment=TextAlignmentOptions.MidlineLeft;caption.margin=Vector4.zero;
   var content=viewport.Find("Content");var layout=content.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(0,0,8,8);layout.spacing=8;layout.childControlWidth=true;layout.childForceExpandWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childAlignment=TextAnchor.UpperLeft;
   foreach(var b in content.GetComponentsInChildren<Button>(true)){RoundedControl(b.GetComponent<Image>(),new Color(.15f,.25f,.34f),8);var le=b.GetComponent<LayoutElement>();if(!le)le=b.gameObject.AddComponent<LayoutElement>();le.minWidth=0;le.flexibleWidth=1;le.minHeight=le.preferredHeight=50;var text=b.GetComponentInChildren<TMP_Text>(true);Stretch(text.rectTransform,Vector2.zero,Vector2.one,new Vector2(14,4),new Vector2(-12,-4));text.margin=Vector4.zero;text.alignment=TextAlignmentOptions.MidlineLeft;}
   var close=card.Find("Close")?.GetComponent<Image>();if(close)RoundedControl(close,new Color(.1f,.22f,.31f),7);
   InsetScrollbar(list.GetComponent<ScrollRect>(),68,18);
   var detail=card.Find("Detail");
   var name=detail.Find("Name").GetComponent<TMP_Text>();Stretch(name.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(28,-44),new Vector2(-28,-2));name.margin=Vector4.zero;name.fontSize=24;
   var stock=detail.Find("Stock").GetComponent<TMP_Text>();Stretch(stock.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(28,-98),new Vector2(-28,-68));stock.margin=Vector4.zero;
   var description=detail.Find("Description").GetComponent<TMP_Text>();description.rectTransform.offsetMax=new Vector2(-28,-124);description.margin=Vector4.zero;
  }
  static void InsetScrollbar(ScrollRect scroll,float top=12,float bottom=12){
   if(!scroll||!scroll.verticalScrollbar)return;var bar=scroll.verticalScrollbar;var rect=(RectTransform)bar.transform;
   Stretch(rect,new Vector2(1,0),Vector2.one,new Vector2(-28,bottom),new Vector2(-14,-top));scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
   var track=bar.GetComponent<Image>();if(track)RoundedControl(track,new Color(.035f,.085f,.14f,.9f),5);
   var handle=bar.handleRect;var area=handle.parent as RectTransform;
   if(area==rect){area=Child(rect,"SlidingArea");handle.SetParent(area,false);}
   Stretch(area,Vector2.zero,Vector2.one,new Vector2(3,3),new Vector2(-3,-3));
   handle.offsetMin=handle.offsetMax=Vector2.zero;var image=handle.GetComponent<Image>();RoundedControl(image,new Color(.32f,.67f,.83f),4);bar.targetGraphic=image;
   var colors=bar.colors;colors.normalColor=new Color(.55f,.85f,1);colors.highlightedColor=new Color(.75f,.95f,1);colors.pressedColor=Color.white;colors.disabledColor=new Color(.3f,.43f,.55f,.6f);bar.colors=colors;
   bar.navigation=new Navigation{mode=Navigation.Mode.None};
  }
  static void RewardViewport(Transform card){var image=card.Find("RewardScroll/Viewport").GetComponent<Image>();image.sprite=null;image.color=Color.white;var shape=image.GetComponent<LearningCardShape>();if(shape)Object.DestroyImmediate(shape);var mask=image.GetComponent<Mask>();if(mask)mask.showMaskGraphic=false;}
 }
}
