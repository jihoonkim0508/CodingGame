using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    // The Defense scene's six-robot lists cut to the three robots (2026-11-25): Buffer, Warrior and Shooter (old indices 0, 1, 4)
    // stay as Support, Melee and Ranged (0, 1, 2); Tank, Bomber and Utility (2, 3, 5) leave every six-long robot list on the
    // battle, HUD, inventory and dock, and their cards are deleted. Dock drags that pointed at 4 now point at 2. A scene already
    // cut (no six-long list) is left alone.
    public static class RobotRoster
    {
        const string Scene = "Assets/!_Project/Scenes/Defense.unity";
        static readonly int[] Dropped = { 5, 3, 2 }; // old indices, last first
        static readonly string[] Lists = { "robots", "robotButtons", "robotLabels", "developerRobotLabels", "robotIcons" };
        static readonly Type[] Owners = { typeof(DefenseBattle), typeof(DefensePlayerHUD), typeof(DefenseInventoryUI), typeof(DefensePlacementDock) };

        // The 2026-11-25 change in one batchmode run: the Reflector asset, the scene's robot lists, the palette's and, my_hp()
        // and help entries, then GameData into the campaign.
        public static void ApplyAll()
        {
            SupportEnemies.Create(); Apply(); PaletteBlocks.Add();
            Debug.Log(TutorialSequenceBuilder.ApplyGameData());
        }
        // The dock cards' own role labels (the inventory and developer lists read the robots' names at run time).
        static string Rename()
        {
            string log = "";
            foreach (var label in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "RoleName" && t.transform.parent))
            {
                string name = label.transform.parent.name switch { "Buffer" => "유틸", "Tank" => "탱커", "Warrior" => "근접", "Shooter" => "원거리", _ => null };
                if (name == null || label.text == name) continue;
                Undo.RecordObject(label, "rename"); label.text = name; EditorUtility.SetDirty(label);
                log += "card " + label.transform.parent.name + " → " + name + "; ";
            }
            return log;
        }
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 실행하세요.");
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var removed = new List<Object>(); string log = "";
            foreach (var type in Owners)
                foreach (MonoBehaviour owner in Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var so = new SerializedObject(owner);
                    foreach (var path in Lists)
                    {
                        var list = so.FindProperty(path);
                        if (list == null || !list.isArray || list.arraySize != 6) continue;
                        foreach (int i in Dropped)
                        {
                            var element = list.GetArrayElementAtIndex(i);
                            if (element.objectReferenceValue) removed.Add(element.objectReferenceValue);
                            element.objectReferenceValue = null; list.DeleteArrayElementAtIndex(i);
                        }
                        log += type.Name + "." + path + " 6→" + list.arraySize + "; ";
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            // The dropped cards: their buttons' objects (their labels, icons, hover and drag scripts go with them).
            foreach (var button in removed.OfType<UnityEngine.UI.Button>().Where(b => b)) { log += "deleted " + button.name + "; "; Object.DestroyImmediate(button.gameObject); }
            foreach (var left in removed.Where(o => o && !(o is ScriptableObject))) log += "left " + left.name + " (" + left.GetType().Name + "); ";
            foreach (var drag in Object.FindObjectsByType<DefenseRobotDrag>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(drag); var index = so.FindProperty("definitionIndex");
                if (index.intValue == 4) { index.intValue = 2; so.ApplyModifiedPropertiesWithoutUndo(); log += "drag " + drag.name + " 4→2; "; }
                else if (index.intValue > 2) log += "drag " + drag.name + " still " + index.intValue + "; ";
            }
            log += Rename();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[RobotRoster] " + log + "scene saved " + EditorSceneManager.SaveScene(scene));
        }
    }
}
