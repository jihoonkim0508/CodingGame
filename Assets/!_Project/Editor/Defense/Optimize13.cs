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

            Directory.CreateDirectory(Path.GetDirectoryName(Log));
            string result = (failed.Count == 0 ? "PASS " : "FAIL ") + passed + " passed" + string.Concat(failed.Select(f => "\n  failed: " + f));
            File.WriteAllText(Log, result + "\n");
            if (failed.Count == 0) Debug.Log("[Optimize13] " + result); else Debug.LogError("[Optimize13] " + result);
            if (Application.isBatchMode) EditorApplication.Exit(failed.Count == 0 ? 0 : 1);
        }
    }
}
