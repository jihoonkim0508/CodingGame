using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense.Editor
{
    public static partial class GeneratedUISkinBuilder
    {
        static void AlignCategoryTabs(Transform root)
        {
            var categories=root.Find("Library/Categories");if(!categories)return;
            Stretch((RectTransform)categories,new Vector2(0,1),Vector2.one,new Vector2(20,-50),new Vector2(-20,-14));
            var layout=categories.GetComponent<HorizontalLayoutGroup>();
            if(layout){layout.padding=new RectOffset();layout.spacing=6;layout.childAlignment=TextAnchor.MiddleCenter;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=true;}
            foreach(var button in categories.GetComponentsInChildren<Button>(true))
            {
                RoundedControl(button.GetComponent<Image>(),new Color(.13f,.23f,.34f),6);
                var le=button.GetComponent<LayoutElement>();if(!le)le=button.gameObject.AddComponent<LayoutElement>();le.minWidth=0;le.preferredWidth=-1;le.flexibleWidth=1;le.minHeight=le.preferredHeight=34;le.flexibleHeight=1;
                var text=button.GetComponentInChildren<TMP_Text>(true);Stretch(text.rectTransform,Vector2.zero,Vector2.one,new Vector2(5,3),new Vector2(-5,-3));text.margin=Vector4.zero;text.fontSize=17;text.enableAutoSizing=true;text.fontSizeMin=14;text.fontSizeMax=17;text.alignment=TextAlignmentOptions.Midline;text.color=new Color(.88f,.95f,1);text.raycastTarget=false;
                var colors=button.colors;colors.normalColor=colors.selectedColor=Color.white;colors.highlightedColor=new Color(.82f,.95f,1);colors.pressedColor=new Color(.65f,.87f,1);button.colors=colors;
            }
        }
        static void StylePreviewArena()
        {
            foreach(var preview in Object.FindObjectsByType<DefenseAttackPreview>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var so=new SerializedObject(preview);var arena=so.FindProperty("arena").objectReferenceValue as Transform;var camera=so.FindProperty("previewCamera").objectReferenceValue as Camera;
                var floor=arena?arena.Find("Floor")?.GetComponent<Renderer>():null;
                if(floor&&floor.sharedMaterial){string path=Root+"PreviewStudioFloor.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!material){material=new Material(floor.sharedMaterial);AssetDatabase.CreateAsset(material,path);}var color=new Color(.2f,.32f,.42f);if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);else material.color=color;if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.12f);EditorUtility.SetDirty(material);floor.sharedMaterial=material;}
                if(camera)camera.backgroundColor=new Color(.07f,.13f,.2f);
            }
        }
    }
}
