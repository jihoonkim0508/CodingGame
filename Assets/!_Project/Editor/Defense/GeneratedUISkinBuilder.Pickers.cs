using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense.Editor
{
    public static partial class GeneratedUISkinBuilder
    {
        static void RecordUIOverrides(Transform root)
        {
            foreach(var component in root.GetComponentsInChildren<Component>(true))
                if(component && PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
        static void RefineBlockPickers(Transform root)
        {
            foreach(var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
            {
                if(!dropdown.GetComponentInParent<CodingGame.BlockCoding.CommandBlockView>(true))continue;
                RoundedControl(dropdown.GetComponent<Image>(),new Color(.24f,.19f,.35f),6);
                var caption=dropdown.captionText;
                Stretch(caption.rectTransform,Vector2.zero,Vector2.one,new Vector2(8,2),new Vector2(-24,-2));
                caption.alignment=TextAlignmentOptions.Midline;caption.margin=Vector4.zero;caption.fontSize=18;caption.enableAutoSizing=true;caption.fontSizeMin=13;caption.fontSizeMax=18;caption.raycastTarget=false;
                var arrow=dropdown.transform.Find("Arrow")?.GetComponent<TMP_Text>();
                if(arrow){Stretch(arrow.rectTransform,new Vector2(1,0),Vector2.one,new Vector2(-22,2),new Vector2(-4,-2));arrow.text="▼";arrow.fontSize=12;arrow.alignment=TextAlignmentOptions.Midline;arrow.margin=Vector4.zero;arrow.color=new Color(.78f,.87f,1);arrow.raycastTarget=false;}
                var template=dropdown.template;
                // TMP_Dropdown handles screen-edge placement, keyboard navigation and option callbacks.
                template.anchorMin=template.anchorMax=new Vector2(.5f,0);template.pivot=new Vector2(.5f,1);template.anchoredPosition=new Vector2(0,-6);template.sizeDelta=new Vector2(164,228);
                RoundedControl(template.GetComponent<Image>(),new Color(.09f,.15f,.23f),9);
                // TMP sizes the popup to the content height; vertical viewport insets would clip the last row.
                var viewport=(RectTransform)template.Find("Viewport");Stretch(viewport,Vector2.zero,Vector2.one,new Vector2(6,0),new Vector2(-6,0));
                var mask=viewport.GetComponent<Mask>();if(mask){var image=viewport.GetComponent<Image>();Plain(image,Color.white);mask.showMaskGraphic=false;}
                var content=(RectTransform)viewport.Find("Content");content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=new Vector2(0,42);
                var item=(RectTransform)content.Find("Item");item.sizeDelta=new Vector2(0,42);
                var itemImage=item.GetComponent<Image>();RoundedControl(itemImage,new Color(.18f,.29f,.41f),5);
                var toggle=item.GetComponent<Toggle>();toggle.targetGraphic=itemImage;toggle.transition=Selectable.Transition.ColorTint;
                var colors=toggle.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.65f,.89f,1);colors.selectedColor=new Color(.65f,.89f,1);colors.pressedColor=new Color(.42f,.72f,.88f);toggle.colors=colors;
                var check=item.Find("Check") as RectTransform;if(check){SetRect(check,new Vector2(0,.5f),new Vector2(7,0),new Vector2(3,18),new Vector2(.5f,.5f));Plain(check.GetComponent<Image>(),new Color(.52f,.91f,.95f));}
                var label=dropdown.itemText;Stretch(label.rectTransform,Vector2.zero,Vector2.one,new Vector2(18,5),new Vector2(-14,-5));label.fontSize=18;label.alignment=TextAlignmentOptions.MidlineLeft;label.margin=Vector4.zero;label.color=new Color(.91f,.96f,1);label.raycastTarget=false;
            }
        }

        static void RefineWaveNotice(Transform canvas)
        {
            var tab=canvas.Find("UpcomingTab") as RectTransform;if(!tab)return;
            Specific(canvas,"UpcomingTab","WaveNoticeBanner",Color.white,4);
            SetRect(tab,new Vector2(0,1),new Vector2(24,-94),new Vector2(218,42),new Vector2(0,1));
            var icon=tab.Find("WaveIcon") as RectTransform;if(icon)SetRect(icon,new Vector2(0,.5f),new Vector2(26,0),new Vector2(27,27),new Vector2(.5f,.5f));
            var text=canvas.Find("Upcoming").GetComponent<TMP_Text>();
            SetRect(text.rectTransform,new Vector2(0,1),new Vector2(72,-94),new Vector2(150,42),new Vector2(0,1));text.fontSize=20;text.alignment=TextAlignmentOptions.MidlineLeft;text.margin=Vector4.zero;text.color=new Color(.065f,.18f,.28f);text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=20;
        }
    }
}
