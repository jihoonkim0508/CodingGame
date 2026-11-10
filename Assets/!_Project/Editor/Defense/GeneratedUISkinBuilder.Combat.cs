using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense.Editor
{
    public static partial class GeneratedUISkinBuilder
    {
        static void RefineCombatControls(Transform canvas)
        {
            var hud=Object.FindFirstObjectByType<DefensePlayerHUD>(FindObjectsInactive.Include);if(!hud)return;
            var so=new SerializedObject(hud);if(so.FindProperty("canvasRoot").objectReferenceValue!=canvas.gameObject)return;
            var root=((GameObject)so.FindProperty("combatControls").objectReferenceValue).transform;
            SetRect((RectTransform)root,Vector2.one,new Vector2(-24,-106),new Vector2(324,46),Vector2.one);
            var pause=root.Find("Pause");var speed=root.Find("Speed");
            foreach(var item in new[]{pause,speed})
            {
                bool isPause=item==pause;
                SetRect((RectTransform)item,new Vector2(0,1),new Vector2(isPause?0:174,0),new Vector2(isPause?164:150,46),new Vector2(0,1));
                var image=item.GetComponent<Image>();RoundedControl(image,new Color(.11f,.23f,.34f),8);image.GetComponent<LearningCardShape>().Configure(8,1.2f,new Color(.36f,.62f,.71f),false);image.raycastTarget=true;
                var button=item.GetComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.ColorTint;var colors=button.colors;colors.normalColor=colors.selectedColor=Color.white;colors.highlightedColor=new Color(.8f,.95f,1);colors.pressedColor=new Color(.58f,.79f,.87f);colors.disabledColor=new Color(.5f,.59f,.65f,.65f);button.colors=colors;
                var text=item.Find("Label").GetComponent<TMP_Text>();Stretch(text.rectTransform,Vector2.zero,Vector2.one,new Vector2(49,4),new Vector2(isPause?-10:-30,-4));text.fontSize=isPause?20:23;text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=isPause?20:23;text.alignment=TextAlignmentOptions.Midline;text.margin=Vector4.zero;text.color=new Color(.91f,.97f,1);text.raycastTarget=false;
                var icon=item.Find("Icon").GetComponent<Image>();SetRect(icon.rectTransform,new Vector2(0,.5f),new Vector2(25,0),new Vector2(23,23),new Vector2(.5f,.5f));icon.color=new Color(.79f,.93f,.97f);icon.preserveAspect=true;icon.raycastTarget=false;
            }
            var shortcut=speed.Find("Shortcut").GetComponent<TMP_Text>();SetRect(shortcut.rectTransform,new Vector2(1,.5f),new Vector2(-8,0),new Vector2(16,22),new Vector2(1,.5f));shortcut.fontSize=12;shortcut.alignment=TextAlignmentOptions.Midline;shortcut.margin=Vector4.zero;shortcut.color=new Color(.55f,.73f,.79f);shortcut.raycastTarget=false;
            var resume=Child(pause,"ResumeIcon");SetRect(resume,new Vector2(0,.5f),new Vector2(25,0),new Vector2(16,20),new Vector2(.5f,.5f));if(!resume.GetComponent<CanvasRenderer>())resume.gameObject.AddComponent<CanvasRenderer>();var triangle=resume.GetComponent<DialogueNavArrow>();if(!triangle)triangle=resume.gameObject.AddComponent<DialogueNavArrow>();triangle.Configure(false);triangle.color=new Color(.69f,.98f,.9f);resume.gameObject.SetActive(false);
            var view=root.GetComponent<DefenseCombatControls>();if(!view)view=root.gameObject.AddComponent<DefenseCombatControls>();view.Configure((DefenseBattle)so.FindProperty("battle").objectReferenceValue,pause.GetComponent<Image>(),pause.Find("Icon").gameObject,resume.gameObject,speed.Find("Label").GetComponent<TMP_Text>());EditorUtility.SetDirty(view);
        }
    }
}
