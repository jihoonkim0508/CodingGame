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
    // instance holds the palette): the calculation `( ) + ( )` in 값 from the comparison (its picker gets + - * / at run time),
    // and the enemy kind `Enemy. [Tank]` in 값 from the comparison's picker alone (Normal / Fast / Tank at run time).
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
            // `Enemy. [Tank]`: the comparison's picker (kinds filled at run time) without its two holes, after get_nearest_enemy().
            Append(BlockKind.EnemyTypeName, BlockKind.Comparison, 2, view =>
            {
                var fields = new SerializedObject(view);
                fields.FindProperty("arguments").arraySize = 0;
                fields.ApplyModifiedPropertiesWithoutUndo();
                var header = view.transform.Find("Header");
                foreach (var hole in new[] { "Slot0", "Slot1" }) Object.DestroyImmediate(header.Find(hole).gameObject);
                var picker = header.Find("Operator"); picker.name = "Kind";
                picker.Find("Caption").GetComponent<TMP_Text>().text = "Tank";
                var label = Object.Instantiate(Find(BlockKind.NearestEnemy).transform.Find("Header").GetComponentInChildren<TMP_Text>(true).gameObject, header);
                label.name = "Label"; label.transform.SetSiblingIndex(0);
                label.GetComponent<TMP_Text>().text = "Enemy.";
                view.transform.SetSiblingIndex(Find(BlockKind.NearestEnemy).transform.GetSiblingIndex() + 1);
            });
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[PaletteBlocks] " + log + "scene saved " + EditorSceneManager.SaveScene(scene));
        }
    }
}
