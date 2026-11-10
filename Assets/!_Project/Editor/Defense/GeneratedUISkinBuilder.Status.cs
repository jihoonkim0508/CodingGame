using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace CodingGame.Defense.Editor {
 public static partial class GeneratedUISkinBuilder {
  static void StatusHUD(Transform canvas){
   Specific(canvas,"Guide","WebtoonGoalPanel",Color.white,3.4f);
   var goal=canvas.Find("Guide");var text=goal.GetComponentInChildren<TMP_Text>(true);text.rectTransform.offsetMin=new Vector2(108,8);text.rectTransform.offsetMax=new Vector2(-32,-8);text.fontSize=20;text.enableAutoSizing=true;text.fontSizeMin=17;text.fontSizeMax=20;text.alignment=TextAlignmentOptions.Center;text.color=new Color(.84f,.95f,1);
   var icon=goal.Find("GoalIcon") as RectTransform;if(icon)SetRect(icon,new Vector2(0,.5f),new Vector2(24,0),new Vector2(23,23),new Vector2(.5f,.5f));
   var caption=TextLabel(goal,"StatusGoalLabel","목표",18,new Color(.67f,.9f,1));SetRect(caption.rectTransform,new Vector2(0,.5f),new Vector2(56,0),new Vector2(40,28),new Vector2(.5f,.5f));caption.alignment=TextAlignmentOptions.Center;caption.margin=Vector4.zero;
   Specific(canvas,"Reward/Card","SkyResultPanel",Color.white,2.2f);
   var card=canvas.Find("Reward/Card");
   var tag=card.Find("StatusTag");if(tag)tag.gameObject.SetActive(false);
   var heading=card.Find("Title").GetComponent<TMP_Text>();Stretch(heading.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(40,-62),new Vector2(-40,-22));heading.fontSize=28;heading.color=new Color(.035f,.12f,.2f);heading.alignment=TextAlignmentOptions.Center;
   Stretch((RectTransform)card.Find("RewardScroll"),Vector2.zero,Vector2.one,new Vector2(42,112),new Vector2(-42,-110));
   var claim=card.Find("Claim");Stretch((RectTransform)claim,new Vector2(0,0),new Vector2(1,0),new Vector2(48,29),new Vector2(-48,69));
   var image=claim.GetComponent<Image>();Skin(image,distinct["SkyClaimButton"],Color.white);image.pixelsPerUnitMultiplier=8;
   var label=claim.GetComponentInChildren<TMP_Text>(true);Stretch(label.rectTransform,Vector2.zero,Vector2.one,new Vector2(18,3),new Vector2(-18,-3));label.color=new Color(.025f,.1f,.18f);label.fontSize=20;label.enableVertexGradient=false;label.alignment=TextAlignmentOptions.Center;label.margin=Vector4.zero;label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=20;
   RewardViewport(card);
  }
 }
}
