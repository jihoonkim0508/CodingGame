using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // Edits the in-game block dictionary (DefenseBlockHelp in Defense.unity): drag rows to change the list order,
    // edit title/description/example in place. Entry order and the buttons' sibling order are kept in sync.
    public sealed class BlockDictionaryWindow : EditorWindow
    {
        const string ScenePath = "Assets/!_Project/Scenes/Defense.unity";
        DefenseBlockHelp help;
        SerializedObject data;
        ReorderableList list;
        Vector2 scroll;

        [MenuItem("Tools/Develop/blockDict")]
        static void Open() => GetWindow<BlockDictionaryWindow>("블록 도감");

        void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorGUILayout.HelpBox("Play 중에는 편집할 수 없습니다. Play를 종료하세요.", MessageType.Info); return; }
            if (!help) Bind();
            if (!help)
            {
                EditorGUILayout.HelpBox("Defense 씬이 열려 있지 않습니다.", MessageType.Info);
                if (GUILayout.Button("Defense 씬 열기") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            data.Update();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            list.DoLayoutList();
            EditorGUILayout.EndScrollView();
            data.ApplyModifiedProperties();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!help.gameObject.scene.isDirty))
                    if (GUILayout.Button("씬 저장", GUILayout.Width(100))) EditorSceneManager.SaveScene(help.gameObject.scene);
            }
        }

        void Bind()
        {
            help = FindObjectsByType<DefenseBlockHelp>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (!help) return;
            data = new SerializedObject(help);
            var entries = data.FindProperty("entries");
            list = new ReorderableList(data, entries, true, true, false, false)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "순서 = 게임 도감 목록 순서 (드래그로 변경)"),
                elementHeightCallback = i => Height(entries.GetArrayElementAtIndex(i)),
                drawElementCallback = (rect, i, active, focused) => Draw(rect, i, entries.GetArrayElementAtIndex(i)),
                onReorderCallback = _ => SyncSiblings(entries),
            };
        }

        static float Line => EditorGUIUtility.singleLineHeight + 2;
        static float Area(SerializedProperty p) => EditorGUI.GetPropertyHeight(p, true) + 2;
        float Height(SerializedProperty e) =>
            e.isExpanded ? Line + Area(e.FindPropertyRelative("description")) + Area(e.FindPropertyRelative("example")) + 6 : Line + 2;

        void Draw(Rect rect, int index, SerializedProperty e)
        {
            var title = e.FindPropertyRelative("title");
            var row = new Rect(rect.x + 10, rect.y + 1, rect.width - 10, EditorGUIUtility.singleLineHeight);
            var kind = e.FindPropertyRelative("kind");
            e.isExpanded = EditorGUI.Foldout(new Rect(row.x, row.y, 150, row.height), e.isExpanded, $"{index + 1}. {kind.enumDisplayNames[kind.enumValueIndex]}", true);
            EditorGUI.PropertyField(new Rect(row.x + 155, row.y, row.width - 155, row.height), title, GUIContent.none);
            if (!e.isExpanded) return;
            foreach (var name in new[] { "description", "example" })
            {
                var p = e.FindPropertyRelative(name); row.y += Line;
                row.height = EditorGUI.GetPropertyHeight(p, true);
                EditorGUI.PropertyField(row, p, new GUIContent(name == "description" ? "설명" : "예시"), true);
                row.y += row.height - EditorGUIUtility.singleLineHeight;
                row.height = EditorGUIUtility.singleLineHeight;
            }
        }

        // The list UI follows the buttons' sibling order, so move the buttons into the slots the entries now occupy.
        void SyncSiblings(SerializedProperty entries)
        {
            data.ApplyModifiedProperties();
            var buttons = Enumerable.Range(0, entries.arraySize)
                .Select(i => ((UnityEngine.UI.Button)entries.GetArrayElementAtIndex(i).FindPropertyRelative("button").objectReferenceValue).transform).ToArray();
            var slots = buttons.Select(b => b.GetSiblingIndex()).OrderBy(s => s).ToArray();
            for (int i = 0; i < buttons.Length; i++) Undo.SetSiblingIndex(buttons[i], slots[i], "Reorder block dictionary");
            EditorSceneManager.MarkSceneDirty(help.gameObject.scene);
        }
    }
}
