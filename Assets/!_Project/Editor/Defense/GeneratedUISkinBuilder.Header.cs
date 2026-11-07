using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense.Editor
{
    public static partial class GeneratedUISkinBuilder
    {
        static void AlignRosterNames(Transform canvas)
        {
            var roster=canvas.Find("Deck/Robots");if(!roster)return;
            foreach(var button in roster.GetComponentsInChildren<Button>(true))
            {
                var role=button.transform.Find("RoleName").GetComponent<TMP_Text>();
                Stretch(role.rectTransform,Vector2.zero,new Vector2(1,0),new Vector2(8,3),new Vector2(-8,25));
                role.fontSize=16;role.enableAutoSizing=true;role.fontSizeMin=12;role.fontSizeMax=16;role.alignment=TextAlignmentOptions.Midline;role.margin=Vector4.zero;role.raycastTarget=false;
            }
        }
        static void EnsureStockForeground(Transform canvas)
        {
            var roster=canvas.Find("Deck/Robots");if(!roster)return;
            foreach(var button in roster.GetComponentsInChildren<Button>(true))
            {
                var box=button.transform.Find("StockBox");var label=button.transform.Find("Label")?.GetComponent<TMP_Text>();
                if(!box||!label)continue;
                // Absolute ends are stable on repeated application; inserting at the label's old index reverses order.
                box.SetAsFirstSibling();label.transform.SetAsLastSibling();
                label.gameObject.SetActive(true);label.enabled=true;label.color=new Color(.96f,.99f,1);label.raycastTarget=false;
            }
        }

        static void RefineStatusHeader(Transform canvas)
        {
            var header=canvas.Find("Header");if(!header)return;
            Specific(canvas,"Header","RobotStatusHeader",Color.white,3);
            var image=header.GetComponent<Image>();image.raycastTarget=false;
            Stretch((RectTransform)header,new Vector2(0,1),Vector2.one,new Vector2(20,-86),new Vector2(-20,-8));
            var logo=header.Find("TitleLogo") as RectTransform;
            if(logo)SetRect(logo,new Vector2(0,.5f),new Vector2(112,0),new Vector2(180,58),new Vector2(.5f,.5f));
            var info=header.Find("Info");
            if(info){SetRect((RectTransform)info,new Vector2(0,.5f),new Vector2(238,0),new Vector2(34,34),new Vector2(.5f,.5f));RoundedControl(info.GetComponent<Image>(),new Color(.14f,.31f,.42f),7);var text=info.GetComponentInChildren<TMP_Text>(true);if(text){Stretch(text.rectTransform,Vector2.zero,Vector2.one,new Vector2(3,2),new Vector2(-3,-2));text.fontSize=22;text.alignment=TextAlignmentOptions.Midline;text.margin=Vector4.zero;}}
            var state=header.Find("Counters")?.GetComponent<TMP_Text>();
            if(state){SetRect(state.rectTransform,new Vector2(0,.5f),new Vector2(276,0),new Vector2(350,40),new Vector2(0,.5f));state.fontSize=18;state.enableAutoSizing=true;state.fontSizeMin=15;state.fontSizeMax=18;state.alignment=TextAlignmentOptions.MidlineLeft;state.margin=Vector4.zero;state.color=new Color(.83f,.93f,.97f);}
            for(int i=0;i<4;i++)
            {
                var stat=header.Find("Stat"+i);if(!stat)continue;
                SetRect((RectTransform)stat,new Vector2(1,.5f),new Vector2(-626+i*176,0),new Vector2(156,54),new Vector2(.5f,.5f));
                var frame=stat.GetComponent<Image>();if(frame)frame.enabled=false;
                var icon=stat.Find("Generated icon") as RectTransform;if(icon)SetRect(icon,new Vector2(0,.5f),new Vector2(21,0),new Vector2(38,38),new Vector2(.5f,.5f));
                var caption=stat.Find("Caption").GetComponent<TMP_Text>();SetRect(caption.rectTransform,new Vector2(0,1),new Vector2(47,-5),new Vector2(105,18),new Vector2(0,1));caption.fontSize=13;caption.alignment=TextAlignmentOptions.MidlineLeft;caption.margin=Vector4.zero;caption.color=new Color(.67f,.84f,.88f);
                var value=stat.Find("Value").GetComponent<TMP_Text>();SetRect(value.rectTransform,new Vector2(0,0),new Vector2(47,4),new Vector2(105,28),Vector2.zero);value.fontSize=24;value.enableAutoSizing=true;value.fontSizeMin=18;value.fontSizeMax=24;value.alignment=TextAlignmentOptions.MidlineLeft;value.margin=Vector4.zero;value.color=new Color(.95f,.99f,1);
            }
        }
    }
}
