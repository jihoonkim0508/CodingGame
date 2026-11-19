using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodingGame.BlockCoding;
using UnityEditor;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // The 예제 힌트 pictures (play feedback: text code was hard to read): every lesson's hint code built as blocks in the real
    // code editor and photographed, Resources/TutorialHints/hint_<code hash>.png (DefenseCodeEditor.HintPicture). Rerun after a
    // hint code changes; until then that hint shows as text. Plays the Defense scene, so in batchmode without -nographics:
    //   Unity.exe -batchmode -projectPath . -executeMethod CodingGame.Defense.Editor.TutorialHintPictures.Make -logFile hints.log
    public static class TutorialHintPictures
    {
        const string Folder = "Assets/!_Project/Resources/TutorialHints";
        const int Width = 1920, Height = 1080, Margin = 14;
        static readonly Stack<IEnumerator> run = new Stack<IEnumerator>();
        static readonly List<string> made = new List<string>();

        [MenuItem("Tools/Develop/예제 힌트 그림 만들기")]
        public static void Make()
        {
            var codes = DevTest.Lessons.SelectMany(l => l.steps).Select(s => s.hintCode).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToArray();
            Directory.CreateDirectory(Folder);
            made.Clear(); run.Clear(); run.Push(Shoot(codes));
            EditorApplication.update += Tick;
        }
        static void Tick()
        {
            try
            {
                if (run.Peek().MoveNext()) { if (run.Peek().Current is IEnumerator inner) run.Push(inner); }
                else run.Pop();
            }
            catch (System.Exception error) { Debug.LogError("[TutorialHintPictures] " + error); run.Clear(); }
            if (run.Count > 0) return;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged += Finish;
            EditorApplication.isPlaying = false;
        }
        static void Finish(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= Finish;
            foreach (var old in Directory.GetFiles(Folder, "*.png").Select(p => p.Replace('\\', '/')).Where(p => !made.Contains(p))) AssetDatabase.DeleteAsset(old);
            AssetDatabase.Refresh();
            foreach (var path in made)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            }
            Debug.Log($"[TutorialHintPictures] {made.Count} pictures in {Folder}");
            if (Application.isBatchMode) EditorApplication.delayCall += () => { FileUtil.DeleteFileOrDirectory("Temp/__Backupscenes"); EditorApplication.Exit(0); };
        }

        static IEnumerator Shoot(string[] codes)
        {
            // Every block unlocked and none counted: a hint may use blocks the stage has not handed out.
            DevTest.PlayDefense(2, 1, true, new DefenseTestOptions { tutorial = false, tutorialInput = false, unlockAll = true, inventoryLimit = false });
            for (double end = EditorApplication.timeSinceStartup + 60; !DevTest.Ready && EditorApplication.timeSinceStartup < end;) yield return null;
            var battle = DevTest.Battle;
            try { DevTest.AutoDeploy(); } catch (System.FormatException) { } // runs out of blocks part-way; one robot is enough
            battle.CodeEditor.Open(battle.Simulation.Robots[0].Id);
            var panel = battle.CodeEditor.Panel;
            var target = new RenderTexture(Width, Height, 24);
            var camera = Camera.main;
            camera.targetTexture = target;
            // The UI drawn by the scene camera into the texture (an overlay canvas never reaches a camera).
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay))
            { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .05f; }
            var program = panel.Program.Rect;
            var mask = program.GetComponentInParent<UnityEngine.UI.RectMask2D>();
            var view = mask ? (RectTransform)mask.transform : (RectTransform)program.parent;
            Vector3 home = program.localPosition;
            for (double end = EditorApplication.timeSinceStartup + 1.5; EditorApplication.timeSinceStartup < end;) yield return null; // the editor opens
            foreach (var code in codes)
            {
                program.localScale = Vector3.one; program.localPosition = home;
                panel.LoadProgram(panel.FunctionName, TutorialAnswers.Parse(code));
                for (double end = EditorApplication.timeSinceStartup + .3; EditorApplication.timeSinceStartup < end;) yield return null;
                Canvas.ForceUpdateCanvases();
                var chain = panel.Program.StartChain();
                if (chain.Count == 0) { Debug.LogError("[TutorialHintPictures] no blocks for: " + code); continue; }
                // A program taller or wider than the code area is drawn smaller, about its top-left corner, so all of it shows.
                var bounds = Bounds(chain, camera); var area = Bounds(new[] { view }, camera);
                float scale = Mathf.Min(1, (bounds.yMax - area.yMin - Margin) / bounds.height, (area.xMax - bounds.xMin - Margin) / bounds.width);
                if (scale < 1)
                {
                    var corner = program.TransformPoint(new Vector3(program.rect.xMin, program.rect.yMax));
                    program.localScale = Vector3.one * scale;
                    program.position += corner - program.TransformPoint(new Vector3(program.rect.xMin, program.rect.yMax));
                    Canvas.ForceUpdateCanvases();
                    bounds = Bounds(chain, camera);
                }
                var crop = Rect.MinMaxRect(Mathf.Max(0, Mathf.Floor(bounds.xMin) - Margin), Mathf.Max(0, Mathf.Floor(bounds.yMin) - Margin),
                    Mathf.Min(Width, Mathf.Ceil(bounds.xMax) + Margin), Mathf.Min(Height, Mathf.Ceil(bounds.yMax) + Margin));
                camera.Render();
                var prior = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D((int)crop.width, (int)crop.height, TextureFormat.RGB24, false);
                image.ReadPixels(crop, 0, 0); image.Apply();
                RenderTexture.active = prior;
                string path = "Assets/!_Project/Resources/" + DefenseCodeEditor.HintPicture(code) + ".png";
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                made.Add(path);
                Debug.Log($"[TutorialHintPictures] {path} {crop.width}x{crop.height} at {scale:0.00}: {code.Replace("\n", " / ")}");
            }
            program.localScale = Vector3.one; program.localPosition = home;
        }
        // Screen-pixel bounds of rects drawn by camera.
        static Rect Bounds(IEnumerable<Component> rects, Camera camera)
        {
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            var corners = new Vector3[4];
            foreach (RectTransform rect in rects.Select(r => r.transform))
            {
                rect.GetWorldCorners(corners);
                foreach (var corner in corners) { var p = RectTransformUtility.WorldToScreenPoint(camera, corner); min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
