using System;
using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    // Adds the palette blocks the Defense scene's code editor lacks, as copies of blocks already there (the scene's editor
    // instance holds the palette): the calculation `( ) + ( )` in 값 from the comparison (its picker gets + - * / at run time).
    // Blocks already in the palette are left alone. is_dead() has no block of its own: it is typed after `enemy.`.
    public static class PaletteBlocks
    {
        const string Scene = "Assets/!_Project/Scenes/Defense.unity";

        public static void Add()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 실행하세요.");
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var panel = Object.FindObjectsByType<CommandCodingPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var so = new SerializedObject(panel);
            var palette = so.FindProperty("palette");
            CommandBlockView View(int i) => (CommandBlockView)palette.GetArrayElementAtIndex(i).FindPropertyRelative("view").objectReferenceValue;
            CommandBlockView Find(BlockKind kind) => Enumerable.Range(0, palette.arraySize).Select(View).FirstOrDefault(v => v && v.BaseKind == kind);
            string log = "";
            void Append(BlockKind kind, BlockKind from, int category, Action<CommandBlockView> shape)
            {
                if (Find(kind)) { log += kind + " kept; "; return; }
                var source = Find(from) ?? throw new InvalidOperationException("팔레트에 " + from + " 블록이 없습니다.");
                var copy = Object.Instantiate(source.gameObject, source.transform.parent);
                copy.name = kind.ToString();
                copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
                var view = copy.GetComponent<CommandBlockView>();
                var fields = new SerializedObject(view);
                fields.FindProperty("kind").intValue = (int)kind;
                fields.ApplyModifiedPropertiesWithoutUndo();
                shape(view);
                palette.arraySize++;
                var item = palette.GetArrayElementAtIndex(palette.arraySize - 1);
                item.FindPropertyRelative("view").objectReferenceValue = view;
                item.FindPropertyRelative("category").intValue = category;
                log += kind + " added; ";
            }
            Append(BlockKind.Arithmetic, BlockKind.Comparison, 2, view => { });
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[PaletteBlocks] " + log + "scene saved " + EditorSceneManager.SaveScene(scene));
        }
    }
}
