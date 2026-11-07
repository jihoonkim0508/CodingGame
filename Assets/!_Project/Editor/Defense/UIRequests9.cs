using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodingGame.BlockCoding;
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
    // The requests after PR #19's UI (Docs/MDA_고도화.md 9차 98~107): the scene re-skinned by PR #19's builders while the robot
    // cards keep this branch's look, Jalnan for the title and the speaker, A2Z (에이투지체) for 디버's lines, no portrait badge.
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
            Check(typeof(DefenseBattle).GetMethod("SetPlacementTint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) == null,
                "108 placing a robot leaves the map's colours alone (no white floor, red road)");

            Directory.CreateDirectory(Path.GetDirectoryName(Log));
            string result = (failed.Count == 0 ? "PASS " : "FAIL ") + passed + " passed" + string.Concat(failed.Select(f => "\n  failed: " + f));
            File.WriteAllText(Log, result + "\n");
            if (failed.Count == 0) Debug.Log("[UIRequests9] " + result); else Debug.LogError("[UIRequests9] " + result);
            if (Application.isBatchMode) EditorApplication.Exit(failed.Count == 0 ? 0 : 1);
        }
    }
}
