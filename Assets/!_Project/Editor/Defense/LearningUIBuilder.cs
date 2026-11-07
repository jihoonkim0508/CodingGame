using System;
using System.IO;
using System.Linq;
using CodingGame.Defense;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    public static class LearningUIBuilder
    {
        const string Art="Assets/!_Project/UI/Learning/";
        const string Prefab="Assets/!_Project/Defense/Tutorial/TutorialOverlay.prefab";
        const string Scene="Assets/!_Project/Scenes/Defense.unity";
        static readonly Color Ink=Hex("D0DFE7"), Blue=Hex("84C6DB"), Edge=Hex("496879"), Paper=Hex("293E4D"), Sky=Hex("344E60"), Yellow=Hex("D8B567");
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#"+value,out var c); return c; }
        [MenuItem("Tools/Learning UI/Apply Tutorial and Defense Theme")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before applying UI.");
            if(SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Preserve unsaved scene before applying UI.");
            string backup=Path.GetFullPath("Temp/LearningUIBackups/"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(backup);
            File.Copy(Scene,backup+"/Defense.unity"); File.Copy(Prefab,backup+"/TutorialOverlay.prefab");
            AssetDatabase.Refresh();
            var guide=Import("GuideRobot.png"); var surprise=Import("GuideRobotSurprised.png");
            var prefab=PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var panel=(RectTransform)prefab.transform.Find("DialoguePanel");
                Card(panel.GetComponent<Image>(),Paper,22,2.5f);
                panel.anchorMin=new Vector2(.16f,.035f); panel.anchorMax=new Vector2(.84f,.035f);
                panel.pivot=new Vector2(.5f,0); panel.sizeDelta=new Vector2(0,220); panel.anchoredPosition=Vector2.zero;
                panel.Find("Accent").gameObject.SetActive(false);
                var portrait=panel.Find("RobotPortrait").GetComponent<Image>();
                foreach(Transform child in portrait.transform) child.gameObject.SetActive(false);
                portrait.sprite=guide; portrait.color=Color.white; portrait.type=Image.Type.Simple; portrait.preserveAspect=true; portrait.raycastTarget=false;
                Fixed(portrait.rectTransform,new Vector2(0,.5f),new Vector2(106,0),new Vector2(190,204));
                var view=panel.GetComponent<TutorialDialogueView>();
                if(!view) view=prefab.GetComponentInChildren<TutorialDialogueView>(true);
                var so=new SerializedObject(view);
                so.FindProperty("defaultPortrait").objectReferenceValue=guide;
                so.FindProperty("surprisedPortrait").objectReferenceValue=surprise;
                so.FindProperty("showDebugId").boolValue=false; so.ApplyModifiedPropertiesWithoutUndo();
                var speaker=Text(panel,"SpeakerName",Blue,24); speaker.fontStyle=FontStyles.Bold;
                Fixed(speaker.rectTransform,new Vector2(0,1),new Vector2(210,-28),new Vector2(600,36),new Vector2(0,1));
                var category=Text(panel,"StageWave",Blue,17); category.gameObject.SetActive(true); category.alignment=TextAlignmentOptions.Right;
                Fixed(category.rectTransform,new Vector2(1,1),new Vector2(-28,-28),new Vector2(180,30),new Vector2(1,1)); category.text="함께 알아보기";
                var body=Text(panel,"Dialogue",Ink,26); body.lineSpacing=10; body.alignment=TextAlignmentOptions.TopLeft;
                body.textWrappingMode=TextWrappingModes.Normal; body.overflowMode=TextOverflowModes.Overflow;
                Stretch(body.rectTransform,new Vector2(210,28),new Vector2(-205,-78));
                StyleButton(panel.Find("PreviousText").GetComponent<Button>(),false,"이전");
                Fixed((RectTransform)panel.Find("PreviousText"),new Vector2(1,0),new Vector2(-28,94),new Vector2(142,42),new Vector2(1,0));
                StyleButton(panel.Find("NextText").GetComponent<Button>(),true,"다음");
                Fixed((RectTransform)panel.Find("NextText"),new Vector2(1,0),new Vector2(-28,28),new Vector2(142,54),new Vector2(1,0));
                PrefabUtility.SaveAsPrefabAsset(prefab,Prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            if(SceneManager.GetActiveScene().path!=Scene) EditorSceneManager.OpenScene(Scene);
            var canvas=Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(c=>c.name=="PlayerCanvas").transform;
            foreach(var image in canvas.GetComponentsInChildren<Image>(true))
            {
                if(image.name=="Icon") { image.color=Blue; continue; }
                if(image.name=="Reward" || image.name=="Inventory") continue; // retain modal dimmers
                if(image.sprite && !GeneratedUISkinBuilder.ThemePanel(image.sprite)) continue; // preserve content images
                Card(image, image.name=="Header"?Sky:Paper, image.name=="Header"||image.name=="Deck"?0:14,1.5f);
            }
            foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true)) text.color=Ink;
            foreach(var button in canvas.GetComponentsInChildren<Button>(true))
                StyleButton(button,button.name=="Start"||button.name=="Claim",null);
            var brand=Text(canvas,"Header/Brand",Blue,26); brand.text="코딩 디펜스"; brand.fontStyle=FontStyles.Bold;
            canvas.Find("Header/Developer").gameObject.SetActive(false);
            var counters=Text(canvas,"Header/Counters",Ink,22);
            counters.rectTransform.anchorMin=new Vector2(.22f,0); counters.rectTransform.anchorMax=new Vector2(.92f,1);
            var notice=Text(canvas,"Deck/Notice",Blue,21); notice.fontStyle=FontStyles.Bold;
            Text(canvas,"Deck/DeploymentCount",Ink,20);
            Text(canvas,"Upcoming",Color.white,20).fontStyle=FontStyles.Bold;
            // A small readable tab above the wave cards; no machinery or ornament.
            var upcoming=canvas.Find("Upcoming").GetComponent<TMP_Text>();
            var tab=canvas.Find("UpcomingTab") as RectTransform;
            if(!tab) tab=new GameObject("UpcomingTab",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();
            tab.SetParent(canvas,false); tab.SetAsLastSibling(); tab.SetSiblingIndex(upcoming.transform.GetSiblingIndex());
            Fixed(tab,new Vector2(0,1),new Vector2(99,-114),new Vector2(154,32));
            Card(tab.GetComponent<Image>(),Blue,12,0,false);
            tab.GetComponent<Image>().raycastTarget=false;
            var hud=new SerializedObject(Object.FindFirstObjectByType<DefensePlayerHUD>());
            hud.FindProperty("upcomingTab").objectReferenceValue=tab.gameObject; hud.ApplyModifiedPropertiesWithoutUndo();
            Fixed(upcoming.rectTransform,new Vector2(0,1),new Vector2(99,-114),new Vector2(154,32)); upcoming.alignment=TextAlignmentOptions.Center;
            var toast=canvas.Find("PlacementToast").GetComponent<Image>(); Card(toast,Hex("FFF0EB"),16,2);
            Text(canvas,"PlacementToast/Message",Hex("A23E2E"),23);
            foreach(var image in canvas.Find("UpcomingCards").GetComponentsInChildren<Image>(true))
                Card(image,Sky,14,1.5f);
            // Preserve the deck's target rectangles, so tutorial spotlight and deployment hit areas remain aligned.
            foreach(var button in canvas.Find("Deck/Robots").GetComponentsInChildren<Button>(true)) StyleButton(button,false,null);
            Decorate(canvas);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("Learning UI applied. Backup: "+backup);
        }
        static Sprite Import(string name)
        {
            string path=Art+name; AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.maxTextureSize=512;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static void Card(Image image,Color fill,float radius,float border,bool shadow=true)
        {
            image.sprite=null; image.type=Image.Type.Simple; image.color=fill;
            var shape=image.GetComponent<LearningCardShape>(); if(!shape) shape=image.gameObject.AddComponent<LearningCardShape>();
            shape.Configure(radius,border,Edge,shadow);
        }
        static void StyleButton(Button button,bool primary,string label)
        {
            var image=button.GetComponent<Image>(); if(!image)return;
            Card(image,primary?Yellow:Sky,12,1.5f);
            if(primary) image.GetComponent<LearningCardShape>().Configure(12,2,Hex("D5AD48"));
            var colors=button.colors; colors.normalColor=Color.white; colors.highlightedColor=Hex("E2F2FF");
            colors.pressedColor=Hex("B9D7ED"); colors.selectedColor=Color.white; colors.disabledColor=new Color(.8f,.84f,.87f,.6f); colors.fadeDuration=.12f; button.colors=colors;
            foreach(var t in button.GetComponentsInChildren<TMP_Text>(true)) {t.color=primary?Hex("283B49"):Ink; t.fontStyle=FontStyles.Bold;}
            if(label!=null)
            {
                var t=button.transform.Find("Label").GetComponent<TMP_Text>(); t.gameObject.SetActive(true); t.text=label; t.fontSize=22;
                Stretch(t.rectTransform,new Vector2(8,4),new Vector2(-8,-4)); t.alignment=TextAlignmentOptions.Center;
                var icon=button.transform.Find("Icon"); if(icon)icon.gameObject.SetActive(false);
            }
            else {var icon=button.transform.Find("Icon"); if(icon)icon.GetComponent<Image>().color=Ink;}
        }
        static TMP_Text Text(Transform root,string path,Color color,float size)
        {var text=root.Find(path).GetComponent<TMP_Text>(); text.color=color; text.fontSize=size; return text;}
        static void Fixed(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size,Vector2? pivot=null)
        {rect.anchorMin=rect.anchorMax=anchor; rect.pivot=pivot??new Vector2(.5f,.5f); rect.sizeDelta=size; rect.anchoredPosition=position;}
        static void Stretch(RectTransform rect,Vector2 min,Vector2 max)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=min;rect.offsetMax=max;}
        static RectTransform New(Transform parent,string name) {
            var old=parent.Find(name) as RectTransform;if(old)return old;
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;
        }
        static TMP_Text Label(Transform parent,string name,string text,float size,Color color) {
            var r=New(parent,name);var t=r.GetComponent<TextMeshProUGUI>();if(!t)t=r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font=parent.GetComponentInParent<Canvas>().GetComponentsInChildren<TMP_Text>(true).First(x=>x.font).font;
            t.text=text;t.fontSize=size;t.color=color;t.raycastTarget=false;t.alignment=TextAlignmentOptions.Left;t.fontStyle=FontStyles.Bold;return t;
        }
        static void Symbol(Transform parent,LearningGlyph glyph,Vector2 position,float size,Color tint) {
            var r=New(parent,"Symbol");Fixed(r,new Vector2(0,.5f),position,Vector2.one*size);
            if(!r.GetComponent<CanvasRenderer>())r.gameObject.AddComponent<CanvasRenderer>();
            var icon=r.GetComponent<LearningSymbol>();if(!icon)icon=r.gameObject.AddComponent<LearningSymbol>();icon.Set(glyph,tint);
        }
        static void Decorate(Transform canvas) {
            Card(canvas.Find("Header").GetComponent<Image>(),Hex("1D303D"),0,0,false);
            Card(canvas.Find("Deck").GetComponent<Image>(),Hex("20333F"),0,0,false);
            var header=canvas.Find("Header");var info=(RectTransform)header.Find("Info");Fixed(info,new Vector2(0,.5f),new Vector2(260,0),new Vector2(42,42));
            var state=header.Find("Counters").GetComponent<TMP_Text>();Fixed(state.rectTransform,new Vector2(0,.5f),new Vector2(320,0),new Vector2(500,56),new Vector2(0,.5f));state.alignment=TextAlignmentOptions.Left;
            var hud=new SerializedObject(Object.FindFirstObjectByType<DefensePlayerHUD>());
            var glyphs=new[]{LearningGlyph.Wave,LearningGlyph.Bug,LearningGlyph.Clock,LearningGlyph.Coin};
            var names=new[]{"waveCounter","enemyCounter","timeCounter","coinCounter"};var titles=new[]{"웨이브","남은 적","시간","코인"};
            for(int i=0;i<4;i++) {
                var r=New(header,"Stat"+i);Fixed(r,new Vector2(1,.5f),new Vector2(-672+i*184,0),new Vector2(172,64));
                var im=r.GetComponent<Image>();if(!im)im=r.gameObject.AddComponent<Image>();Card(im,Paper,14,1,false);im.raycastTarget=false;
                Symbol(r,glyphs[i],new Vector2(30,0),38,i==3?Yellow:Blue);
                var caption=Label(r,"Caption",titles[i],15,Hex("9CB1C0"));Fixed(caption.rectTransform,new Vector2(0,1),new Vector2(60,-7),new Vector2(108,20),new Vector2(0,1));
                var number=Label(r,"Value","0",25,Ink);Fixed(number.rectTransform,new Vector2(0,0),new Vector2(60,7),new Vector2(108,31),new Vector2(0,0));hud.FindProperty(names[i]).objectReferenceValue=number;
            }
            hud.ApplyModifiedPropertiesWithoutUndo();
            var glyphByName=new System.Collections.Generic.Dictionary<string,LearningGlyph>{{"Warrior",LearningGlyph.Sword},{"Tank",LearningGlyph.Shield},{"Shooter",LearningGlyph.Target},{"Bomber",LearningGlyph.Bomb},{"Buffer",LearningGlyph.Support},{"Utility",LearningGlyph.Signal}};
            var colors=new[]{Hex("8DD1CD"),Hex("E8BD79"),Hex("91BCEC"),Hex("D9A2B6"),Hex("B7ACDB"),Hex("A8CDAB")};int n=0;
            var deck=canvas.Find("Deck/Robots");
            Fixed((RectTransform)deck,new Vector2(0,0),new Vector2(24,14),new Vector2(1240,100),new Vector2(0,0));
            foreach(var b in deck.GetComponentsInChildren<Button>(true)) {
                var tint=colors[n++%colors.Length];var image=b.GetComponent<Image>();Card(image,Paper,14,2);
                image.GetComponent<LearningCardShape>().Configure(14,2,Edge);
                var badge=b.GetComponent<LearningRobotBadge>();if(!badge)badge=b.gameObject.AddComponent<LearningRobotBadge>();badge.Setup(image);
                Symbol(b.transform,glyphByName[b.name],new Vector2(43,8),52,tint);
                var label=b.transform.Find("Label").GetComponent<TMP_Text>();Fixed(label.rectTransform,new Vector2(1,.5f),new Vector2(-27,8),new Vector2(42,36));label.fontSize=30;label.alignment=TextAlignmentOptions.Center;
                var title=Label(b.transform,"RoleName", b.name=="Warrior"?"전사":b.name=="Tank"?"탱커":b.name=="Shooter"?"원거리":b.name=="Bomber"?"폭탄":b.name=="Buffer"?"지원":"유틸",16,Hex("A9BFCD"));
                Fixed(title.rectTransform,new Vector2(.5f,0),new Vector2(0,7),new Vector2(140,22),new Vector2(.5f,0));title.alignment=TextAlignmentOptions.Center;
            }
            // The existing world-space bar keeps its tutorial target and damage-trail behavior.
            var health=Object.FindFirstObjectByType<DefenseCoreHealthBar>();
            if(health) {
                var so=new SerializedObject(health);var fill=(RectTransform)so.FindProperty("fill").objectReferenceValue;var text=(TMP_Text)so.FindProperty("label").objectReferenceValue;
                var bg=fill.parent.GetComponent<Image>();if(bg)Card(bg,Hex("203B50"),8,1.5f,false);
                var f=fill.GetComponent<Image>();if(f) {f.color=Hex("429DAD");f.sprite=null;}
                text.color=Hex("E0EBF0");text.fontStyle=FontStyles.Bold;
                var shield=New(text.transform.parent,"BaseShield");Fixed(shield,new Vector2(0,.5f),new Vector2(-17,0),new Vector2(28,28));
                var icon=shield.GetComponent<LearningSymbol>();if(!icon)icon=shield.gameObject.AddComponent<LearningSymbol>();icon.Set(LearningGlyph.Shield,Blue);
            }
        }
    }
}
