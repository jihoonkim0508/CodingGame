using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodingGame.TitleScreen;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    // 13차 optimizations (Docs/MDA_고도화.md 124~141), one commit each. Each Apply bakes its change into scenes, prefabs or
    // import settings ahead of time (what the Inspector would do); Verify checks every item applied so far.
    //   Unity.exe -batchmode -projectPath . -executeMethod CodingGame.Defense.Editor.Optimize13.Verify   (exits 0 or 1)
    public static class Optimize13
    {
        const string TitleScene = "Assets/!_Project/Scenes/Title.unity", Log = "TestResults/Editor/optimize.log";

        // Opens a scene beside whatever is open (the user's scene stays as it is), edits it, saves it and closes it again.
        static void EditScene(string path, Action<Scene> edit)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            else if (scene.isDirty) throw new InvalidOperationException(path + " has unsaved changes; save or revert them first.");
            try { edit(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        static IEnumerable<T> All<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

        // 124: the title's embers re-mesh every frame; on their own canvas only they rebuild, not the whole title canvas.
        // The title draws no meshes, so its camera needs no depth or colour copy and no shadows.
        [MenuItem("Tools/Optimize/124 Title")]
        public static void Title124() => EditScene(TitleScene, scene =>
        {
            foreach (var embers in All<TitleAtmosphere>(scene))
                if (!embers.GetComponent<Canvas>()) embers.gameObject.AddComponent<Canvas>();
            foreach (var camera in All<Camera>(scene)) Lean(camera);
        });

        // 125: the stage map's UI (island cards, locks, seals, the "여기야!" pointer, the DEV line and 디버's box) saved in
        // Stage.unity instead of built at load; the islands are the active renderers named "Stage", left to right, as at run time.
        const string StageScene = "Assets/!_Project/Scenes/Stage.unity";
        [MenuItem("Tools/Optimize/125 Stage Map")]
        public static void StageMap125() => EditScene(StageScene, scene =>
        {
            foreach (var old in All<StageMap.StageMapHUD>(scene).ToArray()) Object.DestroyImmediate(old.gameObject);
            var islands = All<Renderer>(scene).Where(r => r.gameObject.name == "Stage" && r.gameObject.activeInHierarchy).OrderBy(r => r.transform.position.x)
                .Select((r, i) => (i + 1, r.GetComponent<Art.StageSelection.StageIslandView>() is var view && view ? view.stageTitle : $"스테이지 {i + 1}")).ToArray();
            if (islands.Length == 0) throw new InvalidOperationException("Stage.unity has no renderer named Stage");
            var host = new GameObject("StageMapUI");
            SceneManager.MoveGameObjectToScene(host, scene);
            var hud = host.AddComponent<StageMap.StageMapHUD>();
            hud.Build(islands);
            // 디버's box as a link to its prefab, not a copy.
            var so = new SerializedObject(hud);
            var copy = (GameObject)so.FindProperty("guideDialogue").objectReferenceValue;
            if (copy) Object.DestroyImmediate(copy);
            var guide = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/!_Project/Resources/UI/GuideDialogue.prefab"), host.transform);
            guide.name = "GuideDialogue";
            so.FindProperty("guideDialogue").objectReferenceValue = guide;
            so.ApplyModifiedPropertiesWithoutUndo();
        });

        // 126: the battle turned about 195 body texts into Pretendard Bold when it started (UITheme.BoldBody over the scene);
        // the scene now saves them that way and the battle no longer re-meshes them at load.
        const string DefenseScene = "Assets/!_Project/Scenes/Defense.unity";
        [MenuItem("Tools/Optimize/126 Battle Body Font")]
        public static void BodyFont126() => EditScene(DefenseScene, scene =>
        {
            foreach (var text in ToBold(scene))
            {
                text.font = UITheme.Bold; text.fontStyle &= ~TMPro.FontStyles.Bold;
                EditorUtility.SetDirty(text);
                if (PrefabUtility.IsPartOfPrefabInstance(text)) PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            }
        });
        // The texts UITheme.BoldBody would change.
        static TMPro.TMP_Text[] ToBold(Scene scene) => All<TMPro.TMP_Text>(scene).Where(text => text.font && text.font != UITheme.Bold &&
            text.font != UITheme.Display && text.font != UITheme.Lines && text.fontSharedMaterial == text.font.material).ToArray();

        // 132: the goal banner bobs every frame (DefensePlayerHUD.AnimateGoal); on its own canvas only it is rebuilt.
        [MenuItem("Tools/Optimize/132 HUD Canvases")]
        public static void HudCanvases132() => EditScene(DefenseScene, scene =>
        {
            var goal = GoalBanner(scene);
            if (!goal.GetComponent<Canvas>()) goal.gameObject.AddComponent<Canvas>();
        });
        static Transform GoalBanner(Scene scene)
        {
            var hud = All<DefensePlayerHUD>(scene).First();
            return ((Component)new SerializedObject(hud).FindProperty("guide").objectReferenceValue).transform.parent;
        }

        // 127/128: what the battle built or restyled when it started (UIRuntimeDiff: the core bar, the status line, next-wave label,
        // goal banner and robot photos, the dock's layout, the code editor's top row, the preview frame, the tutorial pointer)
        // is built here once and saved with the scene; at run time those components skip it.
        [MenuItem("Tools/Optimize/128 Bake Battle UI")]
        public static void BattleUI128() => EditScene(DefenseScene, scene =>
        {
            // The core bar is PR #21's graphic, drawn by itself; it is placed over the scene's core for the scene's camera.
            var lens = Camera.main.GetComponent<DefenseCameraLensOffset>();
            if (lens) lens.Apply();
            var core = All<Transform>(scene).First(t => t.name == "CodeBase" && t.parent && t.parent.name == "Stage01_Workshop_Art");
            foreach (var bar in All<DefenseCoreHealthBar>(scene)) bar.Place(Camera.main, core);
            Camera.main.ResetProjectionMatrix();
            var battle = All<DefenseBattle>(scene).First();
            foreach (var hud in All<DefensePlayerHUD>(scene)) hud.Bake(battle.EditorCampaign.robots);
            foreach (var dock in All<DefensePlacementDock>(scene)) dock.Layout();
            foreach (var editor in All<DefenseCodeEditor>(scene)) editor.Bake();
            foreach (var preview in All<DefenseAttackPreview>(scene)) preview.Bake();
            foreach (var pointer in All<TutorialPointer>(scene)) pointer.Bake();
            // Changes on prefab instances (the code editor, the tutorial overlay) are kept as overrides.
            foreach (var component in All<Component>(scene).Where(c => c && PrefabUtility.IsPartOfPrefabInstance(c)))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        });

        // A camera that needs neither the depth nor the opaque texture nor shadows (no shader samples them).
        static void Lean(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.requiresDepthOption = CameraOverrideOption.Off; data.requiresColorOption = CameraOverrideOption.Off;
            data.renderShadows = false;
            EditorUtility.SetDirty(data);
        }

        [MenuItem("Tools/Develop/Verify Optimizations (13차)")]
        public static void Verify()
        {
            var failed = new List<string>(); int passed = 0;
            void Check(bool ok, string what) { if (ok) passed++; else failed.Add(what); }

            var title = EditorSceneManager.OpenScene(TitleScene, OpenSceneMode.Additive);
            try
            {
                var embers = All<TitleAtmosphere>(title).ToArray();
                Check(embers.Length > 0 && embers.All(e => e.GetComponent<Canvas>()), "124 the title's embers on their own canvas");
                Check(All<Camera>(title).All(c => { var d = c.GetUniversalAdditionalCameraData();
                    return d.requiresDepthOption == CameraOverrideOption.Off && d.requiresColorOption == CameraOverrideOption.Off && !d.renderShadows; }),
                    "124 the title camera makes no depth or colour copy and no shadows");
                Check(!File.ReadAllText("Assets/!_Project/Scripts/Title/TitlePresentation.cs").Contains("text.font = UITheme.Display"),
                    "124 no font loop when the title opens (the scene is saved in Jalnan)");
            }
            finally { if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(title, true); }

            var stage = EditorSceneManager.OpenScene(StageScene, OpenSceneMode.Additive);
            try
            {
                var huds = All<StageMap.StageMapHUD>(stage).ToArray();
                Check(huds.Length == 1 && huds[0].Baked, "125 Stage.unity saves the map UI (canvas, cards, pointer)");
                var hud = huds.FirstOrDefault();
                var so = hud ? new SerializedObject(hud) : null;
                int islands = All<Renderer>(stage).Count(r => r.gameObject.name == "Stage" && r.gameObject.activeInHierarchy);
                Check(so != null && so.FindProperty("cards").arraySize == islands && islands > 0, $"125 one saved card per island ({islands})");
                var guide = so?.FindProperty("guideDialogue").objectReferenceValue as GameObject;
                Check(guide && PrefabUtility.IsPartOfPrefabInstance(guide) && guide.scene == stage, "125 디버's box is a GuideDialogue prefab instance in the scene");
                Check(!File.ReadAllText("Assets/!_Project/Scripts/Stage/StageMapController.cs").Contains("OnGUI"), "125 no IMGUI on the stage map");
            }
            finally { if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(stage, true); }

            var defense = SceneManager.GetSceneByPath(DefenseScene);
            bool openedDefense = !defense.isLoaded;
            if (openedDefense) defense = EditorSceneManager.OpenScene(DefenseScene, OpenSceneMode.Additive);
            try
            {
                var left = ToBold(defense);
                Check(left.Length == 0, $"126 the battle's body texts are saved in Pretendard Bold ({left.Length} left)");
                Check(!File.ReadAllText("Assets/!_Project/Scripts/Defense/DefenseBattle.cs").Contains("UITheme.BoldBody"), "126 no body-font pass when the battle starts");
                Check(GoalBanner(defense).GetComponent<Canvas>(), "132 the goal banner has its own canvas");
                var unbaked = All<DefensePlayerHUD>(defense).Where(c => !c.Baked).Select(c => c.name)
                    .Concat(All<DefenseCodeEditor>(defense).Where(c => !c.Baked).Select(c => c.name))
                    .Concat(All<DefenseAttackPreview>(defense).Where(c => !c.Baked).Select(c => c.name))
                    .Concat(All<TutorialPointer>(defense).Where(c => !c.Baked).Select(c => c.name)).ToArray();
                Check(unbaked.Length == 0, "128 the battle's start-up UI is saved in the scene" + (unbaked.Length > 0 ? " (not: " + string.Join(", ", unbaked) + ")" : ""));
                var hudCode = File.ReadAllText("Assets/!_Project/Scripts/Defense/DefensePlayerHUD.cs");
                Check(!hudCode.Contains("button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => battle.PlayRobot(id))"), "132 Play listeners wired once, not every frame");
            }
            finally { if (openedDefense && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(defense, true); }

            Directory.CreateDirectory(Path.GetDirectoryName(Log));
            string result = (failed.Count == 0 ? "PASS " : "FAIL ") + passed + " passed" + string.Concat(failed.Select(f => "\n  failed: " + f));
            File.WriteAllText(Log, result + "\n");
            if (failed.Count == 0) Debug.Log("[Optimize13] " + result); else Debug.LogError("[Optimize13] " + result);
            if (Application.isBatchMode) EditorApplication.Exit(failed.Count == 0 ? 0 : 1);
        }
    }
}
