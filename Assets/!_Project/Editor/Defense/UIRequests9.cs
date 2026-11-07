using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodingGame.Art.MapPainter;
using CodingGame.BlockCoding;
using CodingGame.StageMap;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    // The requests after PR #19's UI (Docs/MDA_고도화.md 9차 98~108, 10차 109~114): the scene re-skinned by PR #19's builders while
    // the robot cards keep this branch's look, Jalnan for the title and the speaker, A2Z (에이투지체) for 디버's lines, no portrait
    // badge; then no logo in the top bar and the block guide's i under the bar's right end.
    //   Unity.exe -batchmode -projectPath . -executeMethod CodingGame.Defense.Editor.UIRequests9.Apply -quit
    //   Unity.exe -batchmode -projectPath . -executeMethod CodingGame.Defense.Editor.UIRequests9.Verify   (exits 0 or 1)
    public static class UIRequests9
    {
        const string Scene = "Assets/!_Project/Scenes/Defense.unity", TitleScene = "Assets/!_Project/Scenes/Title.unity",
            Overlay = "Assets/!_Project/Defense/Tutorial/TutorialOverlay.prefab", Jalnan = "Assets/!_Project/Resources/UI/Jalnan2 SDF.asset",
            A2ZFile = "Assets/!_Project/Resources/UI/A2Z-Regular.ttf", A2Z = "Assets/!_Project/Resources/UI/A2Z SDF.asset",
            Pretendard = "Assets/TextMesh Pro/Fonts/PretendardVariable SDF.asset", Log = "TestResults/Editor/ui-requests.log";
        // The dialogue text starts this far in (GeneratedUISkinBuilder.Distinct); 디버's picture sits in the middle of that column.
        const float PortraitColumn = 210;

        [MenuItem("Tools/Learning UI/Apply 9차 Requests")]
        public static void Apply()
        {
            var a2z = A2ZAsset();
            var jalnan = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Jalnan);
            EditorSceneManager.OpenScene(Scene);
            var restore = Preserve(Canvas("PlayerCanvas").Find("Deck/Robots"));
            LearningUIBuilder.Apply();
            GeneratedUISkinBuilder.Apply();
            restore();
            Dialogue(Canvas("TutorialOverlay"), jalnan, a2z);
            TopBar(Canvas("PlayerCanvas"));
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            var prefab = PrefabUtility.LoadPrefabContents(Overlay);
            try { Dialogue(prefab.transform, jalnan, a2z); PrefabUtility.SaveAsPrefabAsset(prefab, Overlay); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.OpenScene(TitleScene);
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { text.font = jalnan; text.fontSharedMaterial = jalnan.material; text.fontStyle &= ~FontStyles.Bold; EditorUtility.SetDirty(text); }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[UIRequests9] applied");
        }

        [MenuItem("Tools/Learning UI/Apply 10차 Requests")]
        public static void Apply10()
        {
            EditorSceneManager.OpenScene(Scene);
            TopBar(Canvas("PlayerCanvas"));
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[UIRequests10] applied");
        }

        // 112: no 'This is Coding' logo, the stage line starts at the bar's left. 113: the i sits just under the bar at its right
        // end, as tall as the pause and speed buttons, which move left to make room.
        const float InfoSize = 46, BarGap = 20, ControlsRight = 24, InfoGap = 10;
        static void TopBar(Transform canvas)
        {
            var header = canvas.Find("Header");
            var logo = header.Find("TitleLogo");
            if (logo) Object.DestroyImmediate(logo.gameObject);
            var counters = (RectTransform)header.Find("Counters");
            counters.anchoredPosition = new Vector2(24, counters.anchoredPosition.y);
            var info = (RectTransform)header.Find("Info");
            info.anchorMin = info.anchorMax = new Vector2(1, 0); info.pivot = new Vector2(1, 1);
            // The header ends 20 in from the screen's right, the controls 24.
            info.anchoredPosition = new Vector2(20 - ControlsRight, -BarGap); info.sizeDelta = new Vector2(InfoSize, InfoSize);
            var hud = Object.FindFirstObjectByType<DefensePlayerHUD>(FindObjectsInactive.Include);
            var controls = (RectTransform)((GameObject)new SerializedObject(hud).FindProperty("combatControls").objectReferenceValue).transform;
            controls.anchoredPosition = new Vector2(-(ControlsRight + InfoSize + InfoGap), controls.anchoredPosition.y);
            foreach (var changed in new Object[] { counters, info, controls }) EditorUtility.SetDirty(changed);
        }

        // 11차 116, 117: the stage map's 디버 talks in the battle's dialogue box. A copy of the Defense scene's DialoguePanel (the
        // speech bubble, 디버's picture, Jalnan name, A2Z line) on its own overlay canvas at the top middle, without the battle's
        // view script, page arrows or hint; nothing on it takes clicks, so the map under it still does.
        const string GuidePrefab = "Assets/!_Project/Resources/UI/GuideDialogue.prefab";
        [MenuItem("Tools/Learning UI/Build Stage Guide Dialogue")]
        public static void BuildGuideDialogue()
        {
            var defense = SceneManager.GetSceneByPath(Scene);
            bool opened = !defense.isLoaded;
            if (opened) defense = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Additive);
            try
            {
                var overlay = defense.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).First(c => c.name == "TutorialOverlay");
                var source = overlay.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "DialoguePanel");
                var preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    var root = new GameObject("GuideDialogue", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
                    SceneManager.MoveGameObjectToScene(root, preview);
                    var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 25;
                    EditorUtility.CopySerialized(overlay.GetComponent<CanvasScaler>(), root.GetComponent<CanvasScaler>());
                    var group = root.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
                    var panel = (RectTransform)Object.Instantiate(source.gameObject, root.transform).transform;
                    panel.name = "DialoguePanel"; panel.gameObject.SetActive(true);
                    foreach (var name in new[] { "Accent", "StageWave", "PreviousText", "NextText", "AdvanceHint" })
                    { var extra = panel.Find(name); if (extra) Object.DestroyImmediate(extra.gameObject); }
                    foreach (var behaviour in panel.GetComponentsInChildren<MonoBehaviour>(true).Where(b => !(b is Graphic)).ToArray())
                        Object.DestroyImmediate(behaviour);
                    foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                    // At the top middle, as wide as in the battle.
                    panel.anchorMin = new Vector2(source.anchorMin.x, 1); panel.anchorMax = new Vector2(source.anchorMax.x, 1);
                    panel.pivot = new Vector2(.5f, 1); panel.anchoredPosition = new Vector2(0, -GuideTop); panel.sizeDelta = new Vector2(0, source.rect.height);
                    var jalnan = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Jalnan);
                    var speaker = panel.Find("SpeakerName").GetComponent<TMP_Text>();
                    speaker.text = StageMap.StageMapController.GuideSpeaker; speaker.font = jalnan; speaker.fontSharedMaterial = jalnan.material; speaker.fontStyle = FontStyles.Normal;
                    panel.Find("Dialogue").GetComponent<TMP_Text>().text = "오류 코드가 몰려오고 있어! 빛나는 섬을 눌러 줘!";
                    PrefabUtility.SaveAsPrefabAsset(root, GuidePrefab);
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            finally { if (opened) EditorSceneManager.CloseScene(defense, true); }
            Debug.Log("[UIRequests11] " + GuidePrefab);
        }
        const float GuideTop = 16;

        // Where a rect sits on its canvas, in canvas units.
        static Rect Box(RectTransform rect, RectTransform canvas)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = canvas.InverseTransformPoint(corners[0]), max = canvas.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // A dynamic TMP font made from the OFL font file once; 디버's lines fill its atlas as they show.
        static TMP_FontAsset A2ZAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(A2Z);
            if (asset) return asset;
            AssetDatabase.ImportAsset(A2ZFile);
            asset = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(A2ZFile), 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            asset.name = "A2Z SDF"; asset.atlasTextures[0].name = "A2Z SDF Atlas"; asset.material.name = "A2Z SDF Material";
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Pretendard);
            if (fallback) asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            AssetDatabase.CreateAsset(asset, A2Z);
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset); AssetDatabase.AddObjectToAsset(asset.material, asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static void Dialogue(Transform root, TMP_FontAsset jalnan, TMP_FontAsset a2z)
        {
            var panel = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "DialoguePanel");
            var badge = panel.Find("PortraitBadge");
            if (badge && !PrefabUtility.IsPartOfPrefabInstance(badge)) Object.DestroyImmediate(badge.gameObject);
            var portrait = (RectTransform)panel.Find("RobotPortrait");
            portrait.anchorMin = portrait.anchorMax = new Vector2(0, .5f); portrait.pivot = new Vector2(.5f, .5f);
            portrait.anchoredPosition = new Vector2(PortraitColumn / 2, 0);
            portrait.GetComponent<Image>().preserveAspect = true;
            var speaker = panel.Find("SpeakerName").GetComponent<TMP_Text>();
            speaker.font = jalnan; speaker.fontSharedMaterial = jalnan.material; speaker.fontStyle = FontStyles.Normal;
            var body = panel.Find("Dialogue").GetComponent<TMP_Text>();
            body.font = a2z; body.fontSharedMaterial = a2z.material; body.fontStyle = FontStyles.Normal;
            foreach (var changed in new Object[] { portrait, portrait.GetComponent<Image>(), speaker, body })
            {
                EditorUtility.SetDirty(changed);
                if (PrefabUtility.IsPartOfPrefabInstance(changed)) PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
            }
        }

        // Snapshot a subtree (each component's fields, each object's active flag) and return what puts it back: the builders'
        // new children and components go, the fields come back, and references to these objects stay valid.
        static Action Preserve(Transform root)
        {
            var kept = root.GetComponentsInChildren<Transform>(true).ToDictionary(t => t,
                t => (t.gameObject.activeSelf, t.GetComponents<Component>().Select(c => (c, EditorJsonUtility.ToJson(c))).ToArray()));
            return () =>
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => !kept.ContainsKey(t)).ToArray())
                    if (t) Object.DestroyImmediate(t.gameObject);
                foreach (var (t, (active, components)) in kept)
                {
                    foreach (var extra in t.GetComponents<Component>().Where(c => components.All(k => k.c != c)).Reverse().ToArray())
                        Object.DestroyImmediate(extra);
                    foreach (var (component, json) in components) { EditorJsonUtility.FromJsonOverwrite(json, component); EditorUtility.SetDirty(component); }
                    t.gameObject.SetActive(active);
                }
            };
        }

        static Transform Canvas(string name) =>
            Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(c => c.name == name).transform;

        [MenuItem("Tools/Develop/Verify UI Requests (9차)")]
        public static void Verify()
        {
            var failed = new List<string>(); int passed = 0;
            void Check(bool ok, string what) { if (ok) passed++; else failed.Add(what); }
            var jalnan = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Jalnan);
            var a2z = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(A2Z);
            Check(a2z && a2z.sourceFontFile, "105 A2Z SDF font asset made from A2Z-Regular.ttf");

            var prefab = PrefabUtility.LoadPrefabContents(Overlay);
            try
            {
                var panel = prefab.GetComponentsInChildren<Transform>(true).First(t => t.name == "DialoguePanel");
                Check(!panel.Find("PortraitBadge"), "107 PortraitBadge removed");
                var portrait = (RectTransform)panel.Find("RobotPortrait");
                Check(portrait.pivot == new Vector2(.5f, .5f) && portrait.anchorMin == portrait.anchorMax &&
                    Mathf.Abs(portrait.anchoredPosition.x - PortraitColumn / 2) < .5f && Mathf.Abs(portrait.anchoredPosition.y) < .5f &&
                    portrait.GetComponent<Image>().preserveAspect, "107 RobotPortrait in the middle of its column");
                var speaker = panel.Find("SpeakerName").GetComponent<TMP_Text>();
                Check(speaker.font == jalnan && (speaker.fontStyle & FontStyles.Bold) == 0, "106 speaker in Jalnan, not bold");
                Check(panel.Find("Dialogue").GetComponent<TMP_Text>().font == a2z, "105 dialogue lines in A2Z");
                // The battle turns body text into Pretendard Bold when it starts (UITheme.BoldBody); 디버's lines stay A2Z.
                UITheme.BoldBody(panel);
                Check(panel.Find("Dialogue").GetComponent<TMP_Text>().font == a2z && speaker.font == jalnan, "105 A2Z and Jalnan survive the battle's body-font pass");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            EditorSceneManager.OpenScene(TitleScene);
            var titles = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(titles.Length > 0 && titles.All(t => t.font == jalnan && (t.fontStyle & FontStyles.Bold) == 0), "104 title texts in Jalnan, not bold");
            var titleButtons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(titleButtons.Length >= 2 && titleButtons.All(b => b.GetComponentsInChildren<TMP_Text>(true).Length > 0 &&
                b.GetComponentsInChildren<TMP_Text>(true).All(t => t.font == jalnan && t.fontSharedMaterial == jalnan.material)),
                "109 title buttons (게임 시작, 게임 종료) labelled in Jalnan");
            // Saved with another font, the title still opens in Jalnan (TitlePresentation.Awake).
            var presentation = Object.FindFirstObjectByType<TitleScreen.TitlePresentation>(FindObjectsInactive.Include);
            var pretendard = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Pretendard);
            foreach (var t in titles) t.font = pretendard;
            typeof(TitleScreen.TitlePresentation).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(presentation, null);
            var shown = presentation.canvasRect.GetComponentsInChildren<TMP_Text>(true);
            Check(UITheme.Display == jalnan && shown.Length >= 3 && shown.All(t => t.font == jalnan), "109 the title sets Jalnan again when it opens, whatever the scene was saved with");
            EditorSceneManager.OpenScene(TitleScene); // drop the test's changes

            // 110: a clear and its stars count for this run only; nothing is written to PlayerPrefs.
            const string ClearKey = "CodingGame.StageMap.Cleared.stage-1", StarsKey = "CodingGame.StageMap.Stars.stage-1";
            PlayerPrefs.DeleteKey(ClearKey); PlayerPrefs.DeleteKey(StarsKey);
            StageProgress.ResetAll(3); StageProgress.MarkCleared(1); StageProgress.RecordStars(1, 3);
            Check(StageProgress.GetState(2) == StageState.Available && StageProgress.BestStars(1) == 3 && !PlayerPrefs.HasKey(ClearKey) && !PlayerPrefs.HasKey(StarsKey),
                "110 a clear opens the next island and keeps its stars without a save file");
            StageProgress.ResetAll(3);
            Check(StageProgress.GetState(2) == StageState.Locked && StageProgress.BestStars(1) == 0, "110 a new run starts from stage 1");

            EditorSceneManager.OpenScene(Scene);
            var robots = Canvas("PlayerCanvas").Find("Deck/Robots").GetComponentsInChildren<Button>(true);
            Check(robots.Length == 6 && robots.All(b => !b.GetComponent<LearningRobotBadge>() && !b.transform.Find("Generated icon") &&
                !b.transform.Find("StockBox") && !b.transform.Find("RoleName") && b.image.sprite && b.image.sprite.name.StartsWith("ui_")),
                "99 robot cards keep this branch's look (no PR badge, emblem, stock box or role name)");
            Check(Canvas("TutorialOverlay").GetComponentsInChildren<Transform>(true).All(t => t.name != "PortraitBadge"), "107 no PortraitBadge in the scene's overlay");
            // Shapes load PR #19's surfaces in OnEnable; the editor's blocks are inactive in the saved scene, so check the type and
            // the surfaces it loads.
            Check(typeof(CommandBlockShape).GetField("generatedSurface", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null &&
                Resources.Load<Texture2D>("LearningSkins/JellySurface") && Resources.Load<Texture2D>("LearningSkins/ControlOpenFrameV2"),
                "98 blocks drawn with PR #19's surfaces (JellySurface, ControlOpenFrameV2)");

            // 100: an enemy's bar, lifted, clears the top of its body.
            foreach (var name in new[] { "Enemy", "Runner", "Heavy" })
            {
                var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/!_Project/Defense/Prefabs/" + name + ".prefab");
                var view = enemy ? enemy.GetComponent<DefenseActorView>() : null;
                if (!view) { failed.Add("100 enemy prefab " + name); continue; }
                var bar = (Transform)new SerializedObject(view).FindProperty("healthRoot").objectReferenceValue;
                float top = enemy.GetComponentsInChildren<Renderer>(true).Where(r => !r.transform.IsChildOf(bar)).Max(r => r.bounds.max.y);
                Check(bar.position.y + DefenseActorView.HealthLift - top >= .15f, $"100 {name}'s bar clears its head (bar {bar.position.y + DefenseActorView.HealthLift:0.00}, head {top:0.00})");
            }
            Check(DefenseCoreHealthBar.Lift >= .5f, "100 core bar lifted");
            // 111: PR #19's (art to develop) battle-map backdrop, navy with its grid showing.
            bool ArtBackdrop(ModularMapRoot map) => map && map.blueprintBackground && ColorUtility.ToHtmlStringRGB(map.blueprintBlue) == "182F42" &&
                Mathf.Abs(map.blueprintMinorOpacity - .07f) < .001f && Mathf.Abs(map.blueprintMajorOpacity - .18f) < .001f;
            var workshop = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/!_Project/Resources/ArtPrefabs/Stage01 Workshop.prefab");
            Check(ArtBackdrop(workshop ? workshop.GetComponent<ModularMapRoot>() : null), "111 workshop map (stages 2, 3): navy #182F42 with the grid at .07 and .18");
            var sceneMaps = Object.FindObjectsByType<ModularMapRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(sceneMaps.Length > 0 && sceneMaps.All(ArtBackdrop), "111 stage 1's map in the scene: the same navy and grid");
            Check(File.ReadAllText("Assets/!_Project/Resources/ArtScripts/WorkshopArt/Editor/WorkshopStage.cs").Contains(
                "map.blueprintBlue=ColorHex(\"182F42\");map.blueprintMinorOpacity=.07f;map.blueprintMajorOpacity=.18f;"), "111 the workshop builder makes the same backdrop");

            // 112, 113: the top bar.
            var player = (RectTransform)Canvas("PlayerCanvas");
            var header = (RectTransform)player.Find("Header");
            Check(!header.Find("TitleLogo"), "112 no logo at the top left");
            var topBar = Box(header, player); var counters = Box((RectTransform)header.Find("Counters"), player);
            Check(counters.xMin - topBar.xMin <= 40, $"112 the stage line starts at the bar's left ({counters.xMin - topBar.xMin:0} in)");
            var info = Box((RectTransform)header.Find("Info"), player);
            var hud = Object.FindFirstObjectByType<DefensePlayerHUD>(FindObjectsInactive.Include);
            var controls = Box((RectTransform)((GameObject)new SerializedObject(hud).FindProperty("combatControls").objectReferenceValue).transform, player);
            Check(info.yMax < topBar.yMin && topBar.yMin - info.yMax <= 30, $"113 i just under the top bar ({topBar.yMin - info.yMax:0} below)");
            Check(player.rect.xMax - info.xMax <= 30 && info.xMin > player.rect.center.x, $"113 i at the right end ({player.rect.xMax - info.xMax:0} from the edge)");
            Check(!info.Overlaps(controls) && !info.Overlaps(topBar), "113 i clear of the pause and speed buttons and of the bar");
            // 116, 117: the stage map's 디버 in the battle's dialogue box, speaker 디버.
            var guide = AssetDatabase.LoadAssetAtPath<GameObject>(GuidePrefab);
            var guidePanel = guide ? guide.transform.Find("DialoguePanel") : null;
            Check(guidePanel && guidePanel.GetComponent<Image>().sprite && guidePanel.GetComponent<Image>().sprite.name == "DialogueCard" &&
                guidePanel.Find("RobotPortrait").GetComponent<Image>().sprite?.name == "GuideRobot" &&
                guidePanel.Find("Dialogue").GetComponent<TMP_Text>().font == a2z, "116 GuideDialogue is the battle's box: speech bubble, 디버's picture, A2Z line");
            Check(guidePanel && !guide.GetComponentInChildren<TutorialDialogueView>(true) && !guide.GetComponentInChildren<Selectable>(true) &&
                guide.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget) && !guidePanel.Find("NextText") && !guidePanel.Find("AdvanceHint"),
                "116 no page arrows, hint or clickable parts on the map's box");
            Check(guidePanel && ((RectTransform)guidePanel).anchorMin.y == 1 && ((RectTransform)guidePanel).pivot.y == 1, "116 the box hangs from the top of the screen");
            Check(typeof(StageMap.StageMapController).GetMethod("DrawGuide", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) == null,
                "116 the old paper card (IMGUI) is gone");
            var guideSpeaker = guidePanel ? guidePanel.Find("SpeakerName").GetComponent<TMP_Text>() : null;
            Check(guideSpeaker && guideSpeaker.text == "디버" && StageMap.StageMapController.GuideSpeaker == "디버" && guideSpeaker.font == jalnan,
                "117 speaker fixed to 디버 in Jalnan (prefab and the map's code)");
            Check(typeof(DefenseBattle).GetMethod("SetPlacementTint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) == null,
                "108, 114 placing a robot leaves the map's colours alone (no white floor, red road)");

            Directory.CreateDirectory(Path.GetDirectoryName(Log));
            string result = (failed.Count == 0 ? "PASS " : "FAIL ") + passed + " passed" + string.Concat(failed.Select(f => "\n  failed: " + f));
            File.WriteAllText(Log, result + "\n");
            if (failed.Count == 0) Debug.Log("[UIRequests9] " + result); else Debug.LogError("[UIRequests9] " + result);
            if (Application.isBatchMode) EditorApplication.Exit(failed.Count == 0 ? 0 : 1);
        }
    }
}
