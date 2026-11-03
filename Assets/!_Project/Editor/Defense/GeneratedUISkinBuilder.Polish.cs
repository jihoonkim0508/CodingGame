using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace CodingGame.Defense.Editor {
 public static partial class GeneratedUISkinBuilder {
  static void Stretch(RectTransform rect,Vector2 min,Vector2 max,Vector2 insetMin,Vector2 insetMax){rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=insetMin;rect.offsetMax=insetMax;}
  static void Plain(Image image,Color color){image.sprite=null;image.color=color;var old=image.GetComponent<LearningCardShape>();if(old)Object.DestroyImmediate(old);}
  static void PolishHUD(Transform canvas){
   var cards=canvas.Find("UpcomingCards") as RectTransform;
   if(cards){SetRect(cards,new Vector2(0,1),new Vector2(182,-205),new Vector2(324,124),new Vector2(.5f,.5f));
    foreach(var raw in cards.GetComponentsInChildren<RawImage>(true)){
     SetRect(raw.rectTransform,new Vector2(.5f,1),new Vector2(0,-47),new Vector2(60,78),new Vector2(.5f,.5f));raw.texture=null;raw.color=Color.white;
     var image=raw.transform.parent.GetComponent<Image>();Plain(image,new Color(.075f,.13f,.2f));var shape=image.gameObject.AddComponent<LearningCardShape>();shape.Configure(8,1.5f,new Color(.3f,.57f,.72f),false);
     var count=raw.transform.parent.Find("Count").GetComponent<TMP_Text>();SetRect(count.rectTransform,new Vector2(.5f,0),new Vector2(0,4),new Vector2(88,26),new Vector2(.5f,0));count.fontSize=19;count.alignment=TextAlignmentOptions.Center;count.margin=Vector4.zero;
    }
   }
   var tooltip=canvas.Find("Tooltip") as RectTransform;
   if(tooltip){tooltip.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,420);
    var title=tooltip.Find("Title").GetComponent<TMP_Text>();SetRect(title.rectTransform,new Vector2(0,1),new Vector2(24,-4),new Vector2(372,28),new Vector2(0,1));title.fontSize=22;title.alignment=TextAlignmentOptions.MidlineLeft;title.margin=Vector4.zero;
    var description=tooltip.Find("Description").GetComponent<TMP_Text>();Stretch(description.rectTransform,Vector2.zero,Vector2.one,new Vector2(24,16),new Vector2(-24,-58));description.fontSize=20;description.alignment=TextAlignmentOptions.TopLeft;description.margin=Vector4.zero;
   }
   var reward=canvas.Find("Reward");if(!reward)return;Plain(reward.GetComponent<Image>(),new Color(.015f,.025f,.045f,.8f));
   var card=(RectTransform)reward.Find("Card");SetRect(card,new Vector2(.5f,.5f),new Vector2(0,18),new Vector2(760,480),new Vector2(.5f,.5f));
   var heading=card.Find("Title").GetComponent<TMP_Text>();Stretch(heading.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(32,-74),new Vector2(-32,-14));heading.fontSize=31;heading.color=new Color(.08f,.16f,.23f);heading.alignment=TextAlignmentOptions.Center;heading.margin=Vector4.zero;
   var scroll=card.Find("RewardScroll") as RectTransform;Stretch(scroll,Vector2.zero,Vector2.one,new Vector2(38,112),new Vector2(-38,-100));
   scroll.GetComponent<ScrollRect>().verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
   var claim=card.Find("Claim") as RectTransform;Stretch(claim,new Vector2(0,0),new Vector2(1,0),new Vector2(40,28),new Vector2(-40,84));
   var items=scroll.Find("Viewport/Items").GetComponent<TMP_Text>();items.fontSize=23;items.color=new Color(.88f,.94f,1f);
   StatusHUD(canvas);
  }
  static void PolishBlockHelp(Transform canvas){
   var window=canvas.Find("BlockHelpWindow");if(!window)return;Plain(window.GetComponent<Image>(),new Color(.015f,.025f,.045f,.82f));
   Specific(window,"Card","CodeWorkspace",Color.white,4);
   var card=window.Find("Card");Specific(card,"BlockList","DictionaryListPanel",Color.white,4);Specific(card,"Detail","EnemyInfoCard",new Color(.8f,.87f,.94f),4);Specific(card,"Detail/ExampleBox","CodeWorkspace",Color.white,4);
   var title=card.Find("Title").GetComponent<TMP_Text>();title.color=new Color(.7f,.94f,1);title.fontSize=29;Stretch(title.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(78,-76),new Vector2(-90,-20));
   Icon(card,"BookIcon",hud[5],new Vector2(0,1),new Vector2(42,-48),34);
   var list=card.Find("BlockList");var viewport=list.Find("Viewport");var mask=viewport.GetComponent<Mask>();var image=viewport.GetComponent<Image>();if(image){image.sprite=null;image.color=mask?Color.white:Color.clear;}if(mask)mask.showMaskGraphic=false;
   var content=viewport.Find("Content");var layout=content.GetComponent<VerticalLayoutGroup>();if(layout){layout.padding=new RectOffset(14,14,14,14);layout.spacing=8;}
   foreach(var button in content.GetComponentsInChildren<Button>(true)){
    Skin(button.GetComponent<Image>(),distinct["JellySurface"],Color.white);button.GetComponent<Image>().type=Image.Type.Simple;
    var le=button.GetComponent<LayoutElement>();if(le){le.minHeight=54;le.preferredHeight=54;}
    var text=button.GetComponentInChildren<TMP_Text>(true);text.fontSize=18;text.alignment=TextAlignmentOptions.MidlineLeft;text.margin=new Vector4(12,2,10,2);text.enableAutoSizing=true;text.fontSizeMin=15;text.fontSizeMax=18;
   }
   var detail=card.Find("Detail");
   var name=detail.Find("Name").GetComponent<TMP_Text>();Stretch(name.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(28,-40),new Vector2(-28,-6));name.fontSize=26;name.alignment=TextAlignmentOptions.MidlineLeft;name.color=new Color(.91f,.98f,1f);
   var stock=detail.Find("Stock").GetComponent<TMP_Text>();Stretch(stock.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(28,-107),new Vector2(-28,-75));stock.fontSize=18;stock.color=new Color(.6f,.86f,.81f);stock.alignment=TextAlignmentOptions.MidlineLeft;
   var description=detail.Find("Description").GetComponent<TMP_Text>();Stretch(description.rectTransform,new Vector2(0,.4f),new Vector2(1,1),new Vector2(28,14),new Vector2(-28,-132));description.fontSize=21;description.enableAutoSizing=true;description.fontSizeMin=18;description.fontSizeMax=21;description.alignment=TextAlignmentOptions.TopLeft;
   var exampleTitle=detail.Find("ExampleTitle").GetComponent<TMP_Text>();Stretch(exampleTitle.rectTransform,new Vector2(0,.33f),new Vector2(1,.33f),new Vector2(28,8),new Vector2(-28,40));exampleTitle.fontSize=20;exampleTitle.color=new Color(.74f,.8f,1f);
   Stretch((RectTransform)detail.Find("ExampleBox"),Vector2.zero,new Vector2(1,.33f),new Vector2(20,20),new Vector2(-20,-4));
   var code=detail.Find("ExampleBox/Code").GetComponent<TMP_Text>();code.fontSize=20;code.enableAutoSizing=true;code.fontSizeMin=14;code.fontSizeMax=20;code.alignment=TextAlignmentOptions.TopLeft;code.color=new Color(.81f,.91f,.97f);
   RefineHelp(card);
  }
 }
}
