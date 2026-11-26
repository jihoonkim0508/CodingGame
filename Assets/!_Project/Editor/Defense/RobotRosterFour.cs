using System;
using System.Collections.Generic;
using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    // The four robots (2026-11-26): the Defense scene's three-long robot lists (battle, HUD, inventory, dock) get the tank as
    // the 4th, as a copy of the utility robot's card (robot 0) relabelled 탱커 with the shield emblem and its icon; the buff's
    // palette blocks, 도감 entries and inventory rows leave the scene, and the 도감 says 유틸/탱커 where it said 지원/buff.
    // A scene already done (four-long lists, no buff block) is left alone. Batchmode:
    //   -executeMethod CodingGame.Defense.Editor.RobotRosterFour.ApplyAll -quit (also writes GameData into the campaign)
    public static class RobotRosterFour
    {
        const string Scene = "Assets/!_Project/Scenes/Defense.unity";
        static readonly string[] Lists = { "robots", "robotButtons", "robotLabels", "developerRobotLabels", "robotIcons" };
        static readonly Type[] Owners = { typeof(DefenseBattle), typeof(DefensePlayerHUD), typeof(DefenseInventoryUI), typeof(DefensePlacementDock) };

        public static void ApplyAll() { Apply(); TutorialSequenceBuilder.ApplyAllFive(); }

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 실행하세요.");
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var tank = AssetDatabase.LoadAssetAtPath<RobotDefinition>("Assets/!_Project/Defense/Data/Tank.asset") ?? throw new InvalidOperationException("Tank.asset 없음");
            string log = AddTank(tank) + BindCard() + Emblems() + StripBuff();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[RobotRosterFour] " + log + "scene saved " + EditorSceneManager.SaveScene(scene));
        }

        static string AddTank(RobotDefinition tank)
        {
            string log = "";
            var clones = new Dictionary<GameObject, GameObject>();
            GameObject CloneOf(GameObject source)
            {
                if (clones.TryGetValue(source, out var copy)) return copy;
                copy = Object.Instantiate(source, source.transform.parent); copy.name = "Tank";
                copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
                clones[source] = copy; log += "copied " + AnimationUtility.CalculateTransformPath(source.transform, null) + "; ";
                return copy;
            }
            // A card's label or icon maps into that card's copy; anything else is copied on its own.
            Object Counterpart(Object o)
            {
                if (o is RobotDefinition) return tank;
                if (!(o is Component c)) return null;
                if (!(c is Button))
                    for (var t = c.transform.parent; t; t = t.parent)
                        if (clones.TryGetValue(t.gameObject, out var card))
                            return card.transform.Find(AnimationUtility.CalculateTransformPath(c.transform, t)).GetComponent(c.GetType());
                return CloneOf(c.gameObject).GetComponent(c.GetType());
            }
            foreach (bool buttons in new[] { true, false }) // buttons first, so their labels land in their copies
                foreach (var type in Owners)
                    foreach (MonoBehaviour owner in Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        var so = new SerializedObject(owner);
                        foreach (var path in Lists)
                        {
                            var list = so.FindProperty(path);
                            if (list == null || !list.isArray || list.arraySize != 3) continue;
                            var first = list.GetArrayElementAtIndex(0).objectReferenceValue;
                            if (first is Button != buttons) continue;
                            list.arraySize = 4; list.GetArrayElementAtIndex(3).objectReferenceValue = first ? Counterpart(first) : null;
                            log += type.Name + "." + path + " 3→4; ";
                        }
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
            foreach (var card in clones.Values)
            {
                foreach (var drag in card.GetComponentsInChildren<DefenseRobotDrag>(true))
                { var so = new SerializedObject(drag); so.FindProperty("definitionIndex").intValue = (int)RobotRole.Tank; so.ApplyModifiedPropertiesWithoutUndo(); }
                foreach (var symbol in card.GetComponentsInChildren<LearningSymbol>(true)) symbol.Set(LearningGlyph.Shield, new Color(.72f, .6f, .95f));
                foreach (var icon in card.GetComponentsInChildren<Image>(true).Where(i => i.name == "Icon")) icon.sprite = tank.icon;
                foreach (var c in card.GetComponentsInChildren<Component>(true)) if (c) EditorUtility.SetDirty(c);
            }
            // The cards' own role labels (the inventory and developer lists read the robots' names at run time).
            foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "RoleName" && t.transform.parent))
            {
                string name = label.transform.parent.name switch { "Buffer" => "유틸", "Tank" => "탱커", "Warrior" => "근접", "Shooter" => "원거리", _ => null };
                if (name == null || label.text == name) continue;
                label.text = name; EditorUtility.SetDirty(label); log += "card " + label.transform.parent.name + " → " + name + "; ";
            }
            return log;
        }

        // A palette block or 도감 button that came with the code editor prefab cannot be destroyed in its scene instance: it is
        // hidden (and out of its list) instead; one added in the scene is deleted.
        static void Remove(GameObject go)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(go) && !PrefabUtility.IsAddedGameObjectOverride(go)) { go.SetActive(false); EditorUtility.SetDirty(go); }
            else Object.DestroyImmediate(go);
        }
        // The tutorial's Battle.Robot.3 (2-4 A03 points at the tank's card) bound to the HUD's 4th card, after Battle.Robot.2.
        static string BindCard()
        {
            var hud = Object.FindObjectsByType<DefensePlayerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var card = (Button)new SerializedObject(hud).FindProperty("robotButtons").GetArrayElementAtIndex(3).objectReferenceValue;
            var bridge = Object.FindObjectsByType<TutorialInputBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var so = new SerializedObject(bridge); var targets = so.FindProperty("targets");
            int Index(string key) { for (int i = 0; i < targets.arraySize; i++) if (targets.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue == key) return i; return -1; }
            if (Index(TutorialKeys.RobotButton(3)) >= 0) return "";
            int at = Index(TutorialKeys.RobotButton(2));
            targets.InsertArrayElementAtIndex(at);
            var binding = targets.GetArrayElementAtIndex(at + 1);
            binding.FindPropertyRelative("key").stringValue = TutorialKeys.RobotButton(3);
            binding.FindPropertyRelative("uiTarget").objectReferenceValue = (RectTransform)card.transform;
            binding.FindPropertyRelative("worldTarget").objectReferenceValue = null;
            binding.FindPropertyRelative("allowRuntimeTarget").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return "bridge " + TutorialKeys.RobotButton(3) + " → " + card.name + "; ";
        }
        // The deck cards' role emblems (LearningSkins/RoleEmblems: 0 swords, 1 shield, 2 target, 3 bomb, 4 plus, 5 signal tower):
        // the tank's copy the shield, the utility robot the tower (the plus read as healing, the buff's).
        static string Emblems()
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/!_Project/Resources/LearningSkins/RoleEmblems.png").OfType<Sprite>().ToDictionary(s => s.name);
            string log = "";
            foreach (var icon in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(i => i.name == "Generated icon" && i.transform.parent))
            {
                string want = icon.transform.parent.name switch { "Tank" => "RoleEmblems1", "Buffer" => "RoleEmblems5", _ => null };
                if (want == null || icon.sprite && icon.sprite.name == want) continue;
                icon.sprite = sprites[want]; EditorUtility.SetDirty(icon); log += "emblem " + icon.transform.parent.name + " → " + want + "; ";
            }
            return log;
        }
        static string StripBuff()
        {
            string log = "";
            var panel = Object.FindObjectsByType<CommandCodingPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var so = new SerializedObject(panel); var palette = so.FindProperty("palette");
            for (int i = palette.arraySize - 1; i >= 0; i--)
            {
                var view = (CommandBlockView)palette.GetArrayElementAtIndex(i).FindPropertyRelative("view").objectReferenceValue;
                if (!view || !DefenseProgression.Retired(view.BaseKind)) continue;
                log += "palette " + view.BaseKind + " removed; ";
                Remove(view.gameObject); palette.DeleteArrayElementAtIndex(i);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var help = Object.FindObjectsByType<DefenseBlockHelp>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var hs = new SerializedObject(help); var entries = hs.FindProperty("entries");
            var rules = AssetDatabase.LoadAssetAtPath<DefenseCampaignDefinition>("Assets/!_Project/Defense/Tutorial/Stage1/StageOneCampaign.asset").rules;
            string penalty = $" (효과 {rules.reducedStrength:0%}, 재사용 대기 {rules.reducedIntervalMultiplier:0.#}배)";
            // Rewritten whole: what the tank and the utility robot do now.
            var rewrite = new Dictionary<BlockKind, (string title, string description, string example)> {
                [BlockKind.Block] = ("block(enemy) · 방패 치기",
                    "탱커 로봇의 전문 기술. 사거리 안의 적을 잠깐 기절시키고 피해를 조금 줍니다. 탱커 로봇의 방패 치기는 적의 보호막도 깨고, 붙잡은 적은 기절해 있는 동안 가드를 깎지 못해요.\n" +
                    "근접·원거리 로봇도 같은 힘으로 쓸 수 있어요(보호막은 그대로).\n유틸 로봇이 공격 기술을 쓰면 효과가 약해요" + penalty,
                    "while True:\n    enemy = get_nearest_enemy()\n    block(enemy)"),
                [BlockKind.Slow] = ("slow(enemy) · 느리게",
                    "유틸 로봇의 전문 기술. 사거리 안의 적에게 피해를 주고 몇 초 동안 느리게 합니다.\n공격류 로봇이 유틸 기술을 쓰면 효과가 약해요" + penalty,
                    "while True:\n    enemy = get_nearest_enemy()\n    slow(enemy)"),
                [BlockKind.GetRobots] = ("get_robots() · 로봇 목록",
                    "배치된 모든 로봇의 리스트입니다. for r in get_robots():로 하나씩 보며 r.hp(체력)와 r.role(종류)을 읽을 수 있어요.",
                    "for r in get_robots():\n    if r.hp < 30:\n        e = get_nearest_enemy()\n        slow(e)"),
                [BlockKind.Role] = ("robot.role · 로봇 종류",
                    "로봇의 종류입니다. Robot.Melee(근접), Robot.Ranged(원거리), Robot.Tank(탱커), Robot.Utility(유틸)와 비교하세요. 로봇 변수 뒤에 점을 찍고 role을 고르세요(hp도 있어요).",
                    "for r in get_robots():\n    if r.role == Robot.Tank and r.hp < 50:\n        e = get_nearest_enemy()\n        slow(e)"),
                [BlockKind.RobotRoleName] = ("Robot.Melee · 로봇 종류 이름",
                    "로봇 종류의 이름입니다. 고르는 칸에서 Melee(근접), Ranged(원거리), Tank(탱커), Utility(유틸)를 고르고 r.role과 비교할 때 씁니다.",
                    "if r.role == Robot.Utility:\n    continue"),
            };
            for (int i = entries.arraySize - 1; i >= 0; i--)
            {
                var entry = entries.GetArrayElementAtIndex(i); var kind = (BlockKind)entry.FindPropertyRelative("kind").intValue;
                if (DefenseProgression.Retired(kind))
                {
                    var button = (Button)entry.FindPropertyRelative("button").objectReferenceValue;
                    if (button) Remove(button.gameObject);
                    entries.DeleteArrayElementAtIndex(i); log += "help " + kind + " removed; ";
                    continue;
                }
                if (rewrite.TryGetValue(kind, out var text))
                {
                    entry.FindPropertyRelative("title").stringValue = text.title; entry.FindPropertyRelative("description").stringValue = text.description;
                    entry.FindPropertyRelative("example").stringValue = text.example; log += "help " + kind + " rewritten; ";
                }
                foreach (var field in new[] { "title", "description", "example" })
                {
                    var p = entry.FindPropertyRelative(field);
                    string was = p.stringValue;
                    p.stringValue = was.Replace("지원 로봇", "유틸 로봇").Replace("지원 기술", "유틸 기술").Replace("지원류", "유틸류").Replace("buff·slow", "slow");
                    if (p.stringValue != was) log += "help " + kind + " " + field + " 지원→유틸; ";
                    if (p.stringValue.Contains("buff") || p.stringValue.Contains("버프") || p.stringValue.Contains("지원")) log += "STILL " + kind + "." + field + ": " + p.stringValue.Replace("\n", " ") + "; ";
                }
            }
            hs.ApplyModifiedPropertiesWithoutUndo();

            var inventory = Object.FindObjectsByType<DefenseInventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var inv = new SerializedObject(inventory); var slots = inv.FindProperty("blockSlots");
            for (int i = slots.arraySize - 1; i >= 0; i--)
            {
                var slot = slots.GetArrayElementAtIndex(i); var kind = (BlockKind)slot.FindPropertyRelative("kind").intValue;
                if (!DefenseProgression.Retired(kind)) continue;
                var label = (TMP_Text)slot.FindPropertyRelative("label").objectReferenceValue;
                // The row is the label's parent (PaletteBlocks), unless other rows' labels sit in it too (then it is the list).
                bool shared = Enumerable.Range(0, slots.arraySize).Any(j => j != i && slots.GetArrayElementAtIndex(j).FindPropertyRelative("label").objectReferenceValue is TMP_Text other &&
                    other && other.transform.parent == label?.transform.parent);
                if (label) Remove(shared ? label.gameObject : label.transform.parent.gameObject);
                slots.DeleteArrayElementAtIndex(i); log += "inventory row " + kind + " removed; ";
            }
            inv.ApplyModifiedPropertiesWithoutUndo();
            return log;
        }
    }
}
