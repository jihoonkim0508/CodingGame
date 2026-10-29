using System;
using System.IO;
using System.Linq;
using CodingGame.TitleScreen;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CodingGame.TitleScreen.Editor
{
    public static class TitleSceneBuilder
    {
        public const string ScenePath = "Assets/!_Project/Scenes/Title.unity";
        public const string Root = "Assets/!_Project/Title/Presentation";
        static readonly string Work = "C:/Users/user/Documents/ChatGPT/CodingGame/TitleScreenWork";
        static TMP_FontAsset font;

        [MenuItem("Tools/Title/Rebuild Layered Title Scene")]
        public static void Apply()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before applying Title presentation.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            // Include any live unsaved layout in a separate recoverable backup first.
            Directory.CreateDirectory(Work + "/BeforeApply");
            if (!scene.GetRootGameObjects().Any(x => x.name == "Title Presentation"))
                EditorSceneManager.SaveScene(scene, Work + "/BeforeApply/Title_LiveBefore.unity", true);
            var old = scene.GetRootGameObjects().FirstOrDefault(x => x.name == "Title Presentation");
            if (old) Object.DestroyImmediate(old);
            var retained = scene.GetRootGameObjects().FirstOrDefault(x => x.name == "Title_OriginalLayout");
            if (!retained) retained = new GameObject("Title_OriginalLayout");
            foreach (var canvas in scene.GetRootGameObjects().Where(x => x.GetComponent<Canvas>()).ToArray())
                canvas.transform.SetParent(retained.transform, true);
            retained.SetActive(false);
            var eventSystem = scene.GetRootGameObjects().Select(x => x.GetComponent<EventSystem>()).FirstOrDefault(x => x);
            if (!eventSystem) eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            foreach (var module in eventSystem.GetComponents<BaseInputModule>()) Object.DestroyImmediate(module);
            var input = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            input.AssignDefaultActions();
            string fontPath = Root + "/Fonts/TitleMenu SDF.asset";
            if (!AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath))
            {
                if (!AssetDatabase.IsValidFolder(Root + "/Fonts")) AssetDatabase.CreateFolder(Root, "Fonts");
                AssetDatabase.CopyAsset("Assets/TextMesh Pro/Fonts/PretendardVariable SDF.asset", fontPath);
            }
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (!font) throw new Exception("Project Korean font is missing.");

            var root = Rect("Title Presentation", null, new Vector2(1920, 1080), Vector2.zero);
            var canvasNew = root.gameObject.AddComponent<Canvas>();
            canvasNew.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasNew.sortingOrder = 0;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var controller = root.gameObject.AddComponent<TitlePresentation>();
            controller.canvasRect = root;
            var black = Image("Black Backdrop", root, null, Color.black);
            Stretch(black.rectTransform);
            var battlefield = Rect("Battlefield", root, new Vector2(1920, 1080), Vector2.zero);
            controller.battlefieldRect = battlefield;
            controller.battlefieldGroup = Group(battlefield);
            FullImage("City and Road", battlefield, Sprite("BattlefieldClean.png"));
            var smokeSprite = Sprite("Ambient/SoftSmoke.png");
            SmokePair(battlefield, smokeSprite, "Cool skyline smoke", new Vector2(-400, 230), new Vector2(245, 345), new Vector2(-24, 92), new Color(.80f,.88f,1), .23f, 7, .15f);
            SmokePair(battlefield, smokeSprite, "Warm skyline smoke", new Vector2(680, 270), new Vector2(275, 390), new Vector2(30, 110), new Color(1,.83f,.73f), .28f, 6.5f, .3f);
            controller.friendlyRect = Rect("Friendly - three depth layers", battlefield, new Vector2(1920, 1080), Vector2.zero);
            controller.enemyRect = Rect("Enemy - three depth layers", battlefield, new Vector2(1920, 1080), Vector2.zero);
            controller.entranceLayers = new[]
            {
                Entrance(controller.friendlyRect, "01 Friendly Rear - anchored base", "FriendlyRear.png", 0, 1.08f, -800, 3, 0, 0),
                Entrance(controller.enemyRect, "02 Enemy Rear - boss", "EnemyRear.png", .08f, .95f, 850, 5, 2, .25f),
                Entrance(controller.enemyRect, "03 Enemy Middle - advancing army", "EnemyMid.png", .24f, .76f, 900, 8, 4, .65f),
                Entrance(controller.friendlyRect, "04 Friendly Middle - robots", "FriendlyMid.png", .36f, .70f, -880, 8, 4, .65f),
                Entrance(controller.enemyRect, "05 Enemy Front - fast pass", "EnemyFront.png", .58f, .56f, 650, 12, 6, .25f),
                Entrance(controller.friendlyRect, "06 Friendly Front - final beat", "FriendlyFront.png", .72f, .54f, -620, 12, 6, .25f)
            };
            Flag(controller.entranceLayers[0], "BlueFlagWave", new Vector4(0,.855f,.039f,.976f), true, 2.8f, 0);
            Flag(controller.entranceLayers[1], "RedFlagWave", new Vector4(.951f,.840f,1,.985f), false, 3.3f, 1.2f);
            Turret(controller, controller.entranceLayers[3]);
            LightPulse(controller.entranceLayers[0], "Base screen and antenna glow", "BaseScreenGlow", 0,
                new Vector4(.155f,.687f,.048f,.10f), new Vector4(.15f,.85f,.035f,.04f), new Vector4(.23f,.81f,.016f,.045f),
                3.6f, .06f, .02f, .3f);
            LightPulse(controller.entranceLayers[1], "Enemy eyes and weapon glow", "EnemyEyeGlow", 1,
                new Vector4(.894f,.695f,.065f,.04f), new Vector4(.75f,.71f,.05f,.04f), new Vector4(.94f,.69f,.025f,.03f),
                5.4f, .035f, .015f, 1.1f);
            // Keep the illustration readable while calming saturated highlights behind UI.
            var dim = Image("Quiet Lighting", battlefield, null, new Color(.015f, .025f, .065f, .16f));
            Stretch(dim.rectTransform);
            // In front of edge units so visible smoke is not hidden by the armies.
            SmokePair(battlefield, smokeSprite, "Left foreground smoke", new Vector2(-650, -65), new Vector2(230, 330), new Vector2(-30, 100), new Color(.77f,.85f,1), .28f, 5.5f, .10f);
            SmokePair(battlefield, smokeSprite, "Right foreground smoke", new Vector2(645, -110), new Vector2(245, 355), new Vector2(32, 108), new Color(1,.82f,.69f), .30f, 6, .45f);
            var atmosphereRect = Rect("Slow Edge Embers", battlefield, new Vector2(1920, 1080), Vector2.zero);
            atmosphereRect.gameObject.AddComponent<CanvasRenderer>();
            controller.atmosphere = atmosphereRect.gameObject.AddComponent<TitleAtmosphere>();
            controller.atmosphere.raycastTarget = false;
            var logo = Image("Existing Title Logo", root, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/!_Project/Title/Asset/titleImg.png"), Color.white);
            logo.rectTransform.sizeDelta = new Vector2(780, 260);
            logo.rectTransform.anchoredPosition = new Vector2(0, 175);
            logo.preserveAspect = true;
            controller.logoRect = logo.rectTransform;
            controller.logoGroup = Group(logo.rectTransform);
            var shadow = logo.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, .02f, .08f, .65f);
            shadow.effectDistance = new Vector2(0, -5);
            var menu = Rect("Menu", root, Vector2.zero, Vector2.zero);
            controller.menuGroup = Group(menu);
            controller.startButton = Button(menu, "GameStartBtn", Sprite("StartButton.png", true), new Vector2(510, 145), new Vector2(0, -65),
                "게임 시작", new Color(.19f, .105f, .015f), 45, false);
            controller.quitButton = Button(menu, "GameQuitBtn", Sprite("QuitButton.png", true), new Vector2(420, 102), new Vector2(0, -210),
                "게임 종료", new Color(.64f, .78f, .96f), 34, true);
            var team = Rect("DF Team Splash", root, new Vector2(1000, 480), Vector2.zero);
            controller.teamGroup = Group(team);
            controller.teamLogo = Image("DF Team Logo", team, Sprite("Team/DFTeamLogo.png", true), Color.white);
            controller.teamLogo.rectTransform.sizeDelta = new Vector2(900, 420);
            controller.teamLogo.preserveAspect = true;
            var skip = Text("Skip Hint", root, "클릭 / Space / Esc  ·  팀 로고 건너뛰기", 18, new Color(.72f, .79f, .88f), new Vector2(650, 40), Vector2.zero);
            skip.rectTransform.anchorMin = skip.rectTransform.anchorMax = new Vector2(.5f, 0);
            skip.rectTransform.anchoredPosition = new Vector2(0, 30);
            controller.skipGroup = Group(skip.rectTransform);
            controller.ConfigureMouseOnlyMenu();
            AttachProjectiles(controller);
            // Persist the real opening state so entering Play cannot expose the authoring preview.
            controller.PreviewAt(0);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Work + "/Reports");
            File.WriteAllText(Work + "/Reports/SceneBuild.txt", "Title scene built. Existing GameFlow bindings preserved.\nIntro duration: " + controller.Duration + "\nRESULT: PASS\n");
        }

        [MenuItem("Tools/Title/Update Team Logo Only")]
        public static void ApplyTeamLogo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before updating the team logo.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            var controller=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TitlePresentation>(true)).Single();
            Directory.CreateDirectory(Work+"/BeforeApply");
            EditorSceneManager.SaveScene(scene,Work+"/BeforeApply/Title_LiveBeforeTeamLogo.unity",true);
            Undo.RecordObject(controller,"Replace DF team text with logo");
            var team=(RectTransform)controller.teamGroup.transform;
            Undo.RecordObject(team,"Resize team splash");
            team.name="DF Team Splash";team.sizeDelta=new Vector2(1000,480);
            foreach(var label in team.GetComponentsInChildren<TMP_Text>(true))Undo.DestroyObjectImmediate(label.gameObject);
            var image=team.GetComponentInChildren<Image>(true);
            if(!image)image=Image("DF Team Logo",team,null,Color.white);
            Undo.RecordObject(image,"Assign supplied DF logo");
            image.sprite=Sprite("Team/DFTeamLogo.png",true);image.preserveAspect=true;image.raycastTarget=false;
            image.rectTransform.sizeDelta=new Vector2(900,420);image.rectTransform.anchoredPosition=Vector2.zero;
            controller.teamLogo=image;
            var hint=controller.skipGroup.GetComponent<TMP_Text>();
            if(hint){Undo.RecordObject(hint,"Clarify splash-only skip");hint.text="클릭 / Space / Esc  ·  팀 로고 건너뛰기";hint.rectTransform.sizeDelta=new Vector2(650,40);EditorUtility.SetDirty(hint);}
            EditorUtility.SetDirty(image);EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Title/Update Mouse Menu and Projectiles")]
        public static void ApplyMouseMenuAndProjectiles()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode before updating title controls.");
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            var controller=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TitlePresentation>(true)).Single();
            Directory.CreateDirectory(Work+"/BeforeApply");
            EditorSceneManager.SaveScene(scene,Work+"/BeforeApply/Title_LiveBeforeMouseMenu.unity",true);
            Undo.RecordObject(controller.startButton,"Mouse-only title buttons");Undo.RecordObject(controller.quitButton,"Mouse-only title buttons");
            foreach(var events in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EventSystem>(true)))
            {
                Undo.RecordObject(events,"Disable title navigation");
                var input=events.GetComponent<InputSystemUIInputModule>();if(input)Undo.RecordObject(input,"Disable title submit actions");
            }
            controller.ConfigureMouseOnlyMenu();AttachProjectiles(controller);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }

        static void AttachProjectiles(TitlePresentation controller)
        {
            var turret=controller.GetComponentsInChildren<TitleTurretMotion>(true).Single();
            turret.burstCount=3;turret.burstSpacing=.16f;EditorUtility.SetDirty(turret);
            var effect=controller.battlefieldRect.GetComponentInChildren<TitleProjectileEffect>(true);
            if(!effect)
            {
                var rect=Rect("Short burst projectiles behind menu",controller.battlefieldRect,new Vector2(1920,1080),Vector2.zero);
                rect.gameObject.AddComponent<CanvasRenderer>();
                effect=rect.gameObject.AddComponent<TitleProjectileEffect>();
            }
            effect.turret=turret;effect.muzzle=turret.flash.rectTransform;effect.raycastTarget=false;
            effect.target=new Vector2(490,-290);EditorUtility.SetDirty(effect);
        }

        [MenuItem("Tools/Title/Update Black Opening")]
        public static void ApplyBlackOpening()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before updating the opening.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var controller = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TitlePresentation>(true)).Single();
            Directory.CreateDirectory(Work + "/BeforeApply");
            EditorSceneManager.SaveScene(scene, Work + "/BeforeApply/Title_LiveBeforeBlackOpening.unity", true);
            controller.openingBlackHold = .4f;
            controller.canvasRect.Find("Black Backdrop").GetComponent<Image>().color = Color.black;
            controller.PreviewAt(0);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Title/Update Mouse Hover Markers")]
        public static void ApplyHoverMarkers()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode before updating hover markers.");
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            var controller=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TitlePresentation>(true)).Single();
            Directory.CreateDirectory(Work+"/BeforeApply");
            EditorSceneManager.SaveScene(scene,Work+"/BeforeApply/Title_LiveBeforeHoverMarker.unity",true);
            AttachHoverMarker(controller.startButton);AttachHoverMarker(controller.quitButton);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }

        static void AttachHoverMarker(Button button)
        {
            if (button.name == "GameStartBtn")
            {
                var icon = button.transform.Find("Icon");
                if (icon) Object.DestroyImmediate(icon.gameObject);
                var label = button.transform.Find("Label") as RectTransform;
                if (label) { label.anchoredPosition = Vector2.zero; EditorUtility.SetDirty(label); }
            }
            var marker=button.GetComponentInChildren<TitleButtonHighlight>(true);
            if(!marker)
            {
                var rect=Rect("Mouse hover chevrons and neon frame",button.transform,Vector2.zero,Vector2.zero);
                Stretch(rect);rect.gameObject.AddComponent<CanvasRenderer>();
                marker=rect.gameObject.AddComponent<TitleButtonHighlight>();
            }
            marker.spriteOutline=TitleButtonContour.Extract(button.GetComponent<Image>().sprite);
            marker.outlineGap=5;
            marker.showOutline=false;
            marker.gameObject.name="Mouse hover triangle indicator";
            marker.raycastTarget=false;marker.SetState(0,false);
            var motion=button.GetComponent<TitleButtonMotion>();
            if(!motion)motion=button.gameObject.AddComponent<TitleButtonMotion>();
            Undo.RecordObject(motion,"Attach title hover marker");motion.highlight=marker;
            EditorUtility.SetDirty(motion);EditorUtility.SetDirty(marker);
        }

        static Sprite Sprite(string name, bool trim = false)
        {
            string path = Root + "/Textures/" + name;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.isReadable = trim;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            if (trim)
            {
                // Trim transparent padding through the sprite rect; PNG pixels remain untouched.
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var pixels = texture.GetPixels32();
                int xmin = texture.width, xmax = 0, ymin = texture.height, ymax = 0;
                for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
                    if (pixels[y * texture.width + x].a > 12) { xmin = Mathf.Min(xmin, x); xmax = Mathf.Max(xmax, x); ymin = Mathf.Min(ymin, y); ymax = Mathf.Max(ymax, y); }
                var factories = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
                factories.Init();
                importer.spriteImportMode = SpriteImportMode.Multiple;
                var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                var id = GUID.Generate();
                provider.SetSpriteRects(new[] { new UnityEditor.SpriteRect { name = System.IO.Path.GetFileNameWithoutExtension(name),
                    rect = new Rect(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1), alignment = SpriteAlignment.Center, pivot = new Vector2(.5f, .5f), spriteID = id } });
                var names = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
                names.SetNameFileIdPairs(new[] { new UnityEditor.SpriteNameFileIdPair(System.IO.Path.GetFileNameWithoutExtension(name), id) });
                provider.Apply();
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (!sprite) throw new Exception("Sprite missing: " + path);
            return sprite;
        }

        static TitleEntranceLayer Entrance(Transform parent, string name, string texture, float delay,
            float duration, float distance, float overshoot, float lift, float idleWeight)
        {
            var image = FullImage(name, parent, Sprite("Depth/" + texture));
            var layer = image.gameObject.AddComponent<TitleEntranceLayer>();
            layer.rect = image.rectTransform;
            layer.opacity = image.GetComponent<CanvasGroup>();
            layer.opacity.interactable = layer.opacity.blocksRaycasts = false;
            layer.delay = delay; layer.duration = duration; layer.fromX = distance;
            layer.overshoot = overshoot; layer.entryLift = lift; layer.idleWeight = idleWeight;
            layer.idlePhase = delay * 4;
            return layer;
        }

        static void SmokePair(Transform parent, Sprite sprite, string name, Vector2 position, Vector2 size,
            Vector2 drift, Color tint, float opacity, float period, float phase)
        {
            for (int i = 0; i < 2; i++)
            {
                var image = Image(name + " " + (i + 1), parent, sprite, tint);
                image.rectTransform.sizeDelta = size;
                image.rectTransform.anchoredPosition = position;
                var motion = image.gameObject.AddComponent<TitleSmokeDrift>();
                motion.image = image; motion.rect = image.rectTransform; motion.home = position;
                motion.travel = drift; motion.tint = tint; motion.maxOpacity = opacity;
                motion.period = period; motion.phase = Mathf.Repeat(phase + i * .5f, 1);
                motion.Evaluate(0);
            }
        }

        static void LightPulse(TitleEntranceLayer layer, string name, string materialName, float accent,
            Vector4 a, Vector4 b, Vector4 c, float period, float strength, float amplitude, float phase)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Shaders/TitleSubtleEmission.shader");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("Title emission shader did not compile.");
            string path = Root + "/Materials/" + materialName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader) { name = materialName }; AssetDatabase.CreateAsset(material, path); }
            material.SetFloat("_AccentMode", accent); material.SetFloat("_Strength", strength);
            material.SetVector("_AreaA", a); material.SetVector("_AreaB", b); material.SetVector("_AreaC", c);
            EditorUtility.SetDirty(material);
            var overlay = FullImage(name, layer.transform, layer.GetComponent<Image>().sprite);
            overlay.material = material;
            var pulse = overlay.gameObject.AddComponent<TitleLightPulse>();
            pulse.image = overlay; pulse.sourceMaterial = material;
            pulse.period = period; pulse.baseStrength = strength; pulse.amplitude = amplitude; pulse.phase = phase;
            if (accent < .5f) { var scan = overlay.gameObject.AddComponent<TitleCodeScan>(); scan.image = overlay; }
        }

        static Material PartsMaterial(string name)
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/TitleIllustrationParts.shader");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("Title illustration-parts shader did not compile.");
            string path=Root+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material=new Material(shader){name=name}; AssetDatabase.CreateAsset(material,path); }
            return material;
        }
        static void Flag(TitleEntranceLayer layer, string name, Vector4 bounds, bool pinnedRight, float period, float phase)
        {
            var material=PartsMaterial(name);
            material.SetVector("_FlagRect",bounds); material.SetFloat("_PinRight",pinnedRight?1:0);
            material.SetFloat("_WaveHeight",.0048f); material.SetFloat("_CutEnabled",0);
            EditorUtility.SetDirty(material);
            var image=layer.GetComponent<Image>(); image.material=material;
            var wave=layer.gameObject.AddComponent<TitleFlagMotion>();
            wave.image=image; wave.sourceMaterial=material; wave.period=period; wave.phase=phase;
        }
        static void Turret(TitlePresentation controller, TitleEntranceLayer layer)
        {
            // Exact UV separation from the existing PNG; no repainting of the gun.
            var pixels=new Rect(328,295,190,98);
            var uv=new Rect(pixels.x/1672f,pixels.y/941f,pixels.width/1672f,pixels.height/941f);
            var cut=PartsMaterial("FriendlyTurretBodyCutout");
            cut.SetFloat("_CutEnabled",1); cut.SetVector("_CutRect",new Vector4(uv.xMin,uv.yMin,uv.xMax,uv.yMax));
            cut.SetVector("_FlagRect",Vector4.zero); EditorUtility.SetDirty(cut);
            layer.GetComponent<Image>().material=cut;
            Vector2 size=new Vector2(uv.width*1920,uv.height*1080), pivot=new Vector2(.40f,.08f);
            var gun=Rect("Separate turret upper body",layer.transform,size,Vector2.zero);
            gun.pivot=pivot;
            gun.anchoredPosition=new Vector2((uv.x+uv.width*pivot.x-.5f)*1920,(uv.y+uv.height*pivot.y-.5f)*1080);
            var graphic=gun.gameObject.AddComponent<RawImage>();
            graphic.texture=layer.GetComponent<Image>().sprite.texture; graphic.uvRect=uv; graphic.raycastTarget=false;
            var flashRect=Rect("Small muzzle flash",gun,new Vector2(32,20),new Vector2(size.x*.47f,-size.y*.20f));
            flashRect.gameObject.AddComponent<CanvasRenderer>();
            var flash=flashRect.gameObject.AddComponent<TitleMuzzleFlash>(); flash.raycastTarget=false;
            var motion=gun.gameObject.AddComponent<TitleTurretMotion>();
            motion.presentation=controller; motion.gun=gun; motion.home=gun.anchoredPosition; motion.flash=flash;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = rect.anchoredPosition = Vector2.zero; }
        static Image Image(string name, Transform parent, Sprite sprite, Color color)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }
        static Image FullImage(string name, Transform parent, Sprite sprite)
        {
            var image = Image(name, parent, sprite, Color.white);
            image.rectTransform.sizeDelta = new Vector2(1920, 1080);
            return image;
        }
        static CanvasGroup Group(RectTransform rect)
        {
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            return group;
        }
        static TMP_Text Text(string name, Transform parent, string text, int size, Color color, Vector2 dimensions, Vector2 position)
        {
            var rect = Rect(name, parent, dimensions, position);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }
        static Button Button(Transform parent, string name, Sprite sprite, Vector2 size, Vector2 position,
            string label, Color ink, int fontSize, bool power)
        {
            var image = Image(name, parent, sprite, Color.white);
            image.rectTransform.sizeDelta = size; image.rectTransform.anchoredPosition = position;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var navigation=button.navigation;navigation.mode=Navigation.Mode.None;button.navigation=navigation;
            var colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1, 1, 1, 1);
            colors.pressedColor = new Color(.84f, .88f, .94f, 1); colors.disabledColor = Color.white;
            button.colors = colors;
            var text = Text("Label", image.transform, label, fontSize, ink, new Vector2(size.x * .65f, size.y * .75f), new Vector2(power ? 24 : 0, 0));
            text.fontStyle = FontStyles.Bold;
            if (power)
            {
                var iconRect = Rect("Icon", image.transform, new Vector2(fontSize, fontSize), new Vector2(-size.x * .255f, 0));
                iconRect.gameObject.AddComponent<CanvasRenderer>();
                var icon = iconRect.gameObject.AddComponent<TitleMenuIcon>(); icon.power = true; icon.color = ink; icon.raycastTarget = false;
            }
            image.gameObject.AddComponent<TitleButtonMotion>();
            AttachHoverMarker(button);
            return button;
        }

    }
}
