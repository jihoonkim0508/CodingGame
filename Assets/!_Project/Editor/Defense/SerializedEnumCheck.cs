using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    // Scenes and assets keep BlockKind / EnemyType as ints, so an enum member put anywhere but the end (or two branches that
    // both appended) silently turns one block or enemy into another. This reads them back by what they show:
    // - each palette block's header text holds its kind's Python (`shoot(`, `while`, `or` ...), and a block or inventory row
    //   PaletteBlocks named after its kind is that kind;
    // - each 도감 entry's title names its kind;
    // - each EnemyDefinition asset's type is the one its file name says;
    // - each campaign lesson's clear reward is GameData/Award.md's row for that wave.
    // Batchmode: -executeMethod CodingGame.Defense.Editor.SerializedEnumCheck.Run (TestResults/Editor/serialized-enums.log).
    public static class SerializedEnumCheck
    {
        // Text a block of that kind shows (nested holes aside). Kinds without words of their own (a number, the pickers'
        // comparison and arithmetic) are checked by their picker instead.
        static readonly Dictionary<BlockKind, string> Shows = new Dictionary<BlockKind, string> {
            [BlockKind.Shot] = "shoot(", [BlockKind.Attack] = "shoot(", [BlockKind.Wait] = "wait(", [BlockKind.While] = "while", [BlockKind.If] = "if",
            [BlockKind.Elif] = "elif", [BlockKind.Else] = "else", [BlockKind.NearestEnemy] = "get_nearest_enemy()", [BlockKind.PositionX] = "get_pos_x()",
            [BlockKind.PositionY] = "get_pos_y()", [BlockKind.True] = "True", [BlockKind.False] = "False", [BlockKind.DeclareVariable] = "=",
            [BlockKind.For] = "for", [BlockKind.Break] = "break", [BlockKind.Continue] = "continue", [BlockKind.Slash] = "slash()",
            [BlockKind.Block] = "block(", [BlockKind.Boom] = "boom(", [BlockKind.Slow] = "slow(", [BlockKind.Buff] = "buff(",
            [BlockKind.MyAttackRange] = "my_attack_range()", [BlockKind.MyPos] = "get_my_pos()", [BlockKind.EnemyTypeName] = "Enemy.",
            [BlockKind.ForEach] = "in", [BlockKind.GetEnemies] = "get_enemies()", [BlockKind.BreakShield] = "break_shield(",
            [BlockKind.And] = "and", [BlockKind.MyHp] = "my_hp()", [BlockKind.WhileCondition] = "while", [BlockKind.Or] = "or",
            [BlockKind.EmptyList] = "[]", [BlockKind.DictLiteral] = "{", [BlockKind.Append] = ".append(", [BlockKind.Len] = "len(",
            [BlockKind.Not] = "not", [BlockKind.Index] = "[", [BlockKind.GetRobots] = "get_robots()", [BlockKind.RobotRoleName] = "Robot.",
            [BlockKind.BuffRobot] = "buff(" };
        static readonly BlockKind[] Pickers = { BlockKind.Comparison, BlockKind.Arithmetic, BlockKind.EnemyTypeName, BlockKind.In, BlockKind.RobotRoleName };
        // A 도감 title holds this (its kind's Python as the guide writes it).
        static readonly Dictionary<BlockKind, string> Titled = new Dictionary<BlockKind, string> {
            [BlockKind.ForEach] = "for e in", [BlockKind.GetEnemies] = "get_enemies()", [BlockKind.Shield] = "shield", [BlockKind.BreakShield] = "break_shield",
            [BlockKind.Front] = "front()", [BlockKind.Top] = "top()", [BlockKind.Reflecting] = "reflecting", [BlockKind.And] = "and",
            [BlockKind.MyHp] = "my_hp()", [BlockKind.Left] = "left", [BlockKind.Right] = "right", [BlockKind.Or] = "or", [BlockKind.WhileCondition] = "while",
            [BlockKind.EmptyList] = "[]", [BlockKind.DictLiteral] = "{", [BlockKind.Append] = "append", [BlockKind.Pop] = "pop(0)", [BlockKind.Len] = "len(",
            [BlockKind.In] = " in ", [BlockKind.Not] = "not", [BlockKind.Index] = "[", [BlockKind.GetRobots] = "get_robots()", [BlockKind.Role] = "role",
            [BlockKind.RobotRoleName] = "Robot.", [BlockKind.BuffRobot] = "buff(r)", [BlockKind.WeakPoint] = "weak_point()",
            [BlockKind.Shot] = "shoot", [BlockKind.Slash] = "slash", [BlockKind.Buff] = "buff", [BlockKind.Slow] = "slow", [BlockKind.Block] = "block",
            [BlockKind.Boom] = "boom", [BlockKind.While] = "while", [BlockKind.If] = "if", [BlockKind.IsDead] = "is_dead" };
        // EnemyDefinition file name → its kind.
        static readonly Dictionary<string, EnemyType> Enemies = new Dictionary<string, EnemyType> {
            ["Enemy"] = EnemyType.Normal, ["Runner"] = EnemyType.Fast, ["Heavy"] = EnemyType.Tank, ["Healer"] = EnemyType.Healer, ["Shielder"] = EnemyType.Shielder,
            ["QueueCentipede"] = EnemyType.Queue, ["StackCentipede"] = EnemyType.Stack, ["Reflector"] = EnemyType.Reflector, ["TreeLegion"] = EnemyType.Tree };

        public static void Run()
        {
            var failures = new List<string>(); var log = new System.Text.StringBuilder();
            try { Check(failures, log); }
            catch (Exception error) { failures.Add("exception: " + error); }
            string result = (failures.Count == 0 ? "PASS" : "FAIL " + failures.Count + "\n" + string.Join("\n", failures)) + "\n" + log;
            Directory.CreateDirectory("TestResults/Editor");
            File.WriteAllText("TestResults/Editor/serialized-enums.log", result);
            Debug.Log("[SerializedEnumCheck] " + result);
            if (Application.isBatchMode) EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
        }
        static string Texts(CommandBlockView view) => string.Join(" ", view.transform.Find("Header").GetComponentsInChildren<TMP_Text>(true)
            .Where(t => t.GetComponentInParent<CommandBlockView>(true) == view && !t.GetComponentInParent<TMP_Dropdown>(true)).Select(t => t.text));
        // A name PaletteBlocks gave a copy (its kind's name); not a number, which Enum.TryParse would also read.
        static bool Named(string name, out BlockKind kind) { kind = 0; return name.Length > 0 && char.IsLetter(name[0]) && Enum.TryParse(name, out kind); }
        static void Check(List<string> failures, System.Text.StringBuilder log)
        {
            EditorSceneManager.OpenScene("Assets/!_Project/Scenes/Defense.unity", OpenSceneMode.Single);
            var panel = Object.FindObjectsByType<CommandCodingPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var seen = new HashSet<BlockKind>();
            foreach (var item in panel.Palette)
            {
                var view = item.view; if (!view) { failures.Add("palette: missing view"); continue; }
                var kind = view.BaseKind; string shown = Texts(view);
                log.AppendLine($"palette {(int)kind,2} {kind,-15} '{view.name}': {shown}");
                if (!seen.Add(kind)) failures.Add($"palette: {kind} twice");
                if (Named(view.name, out var named) && named != kind) failures.Add($"palette: '{view.name}' holds kind {kind} ({(int)kind})");
                if (Shows.TryGetValue(kind, out var token) && !shown.Contains(token)) failures.Add($"palette: {kind} ({(int)kind}) shows '{shown}', not '{token}'");
                bool picker = view.GetComponentsInChildren<TMP_Dropdown>(true).Any(d => d.transform.IsChildOf(view.transform.Find("Header")) && d.GetComponentInParent<CommandBlockView>(true) == view);
                if (Pickers.Contains(kind) && !picker) failures.Add($"palette: {kind} has no picker");
                if (!Pickers.Contains(kind) && picker && kind != BlockKind.Variable) failures.Add($"palette: {kind} ({(int)kind}) has a picker it should not have");
                int holes = view.Arguments.Count;
                int wanted = PythonTreeCompiler.ArgumentCount(kind);
                if (holes != wanted) failures.Add($"palette: {kind} ({(int)kind}) has {holes} holes, its kind takes {wanted}");
            }
            var help = Object.FindObjectsByType<DefenseBlockHelp>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var entries = new SerializedObject(help).FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var kind = (BlockKind)entry.FindPropertyRelative("kind").intValue; string title = entry.FindPropertyRelative("title").stringValue;
                var button = entry.FindPropertyRelative("button").objectReferenceValue as Component;
                log.AppendLine($"help {(int)kind,2} {kind,-15} {title}");
                if (!Enum.IsDefined(typeof(BlockKind), kind)) failures.Add($"help: '{title}' has no kind ({(int)kind})");
                if (Titled.TryGetValue(kind, out var token) && !title.Contains(token)) failures.Add($"help: {kind} ({(int)kind}) is titled '{title}'");
                if (button && Named(button.name, out var named) && named != kind) failures.Add($"help: button '{button.name}' holds {kind}");
            }
            var inventory = Object.FindObjectsByType<DefenseInventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var slots = new SerializedObject(inventory).FindProperty("blockSlots");
            var rows = Enumerable.Range(0, slots.arraySize).Select(i => slots.GetArrayElementAtIndex(i)).ToList();
            var rowKinds = rows.Select(r => (BlockKind)r.FindPropertyRelative("kind").intValue).ToList();
            foreach (var missing in DefenseProgression.ItemKinds.Except(rowKinds)) failures.Add("inventory: no row for " + missing);
            foreach (var twice in rowKinds.GroupBy(k => k).Where(g => g.Count() > 1)) failures.Add("inventory: two rows for " + twice.Key);
            foreach (var row in rows)
            {
                var kind = (BlockKind)row.FindPropertyRelative("kind").intValue;
                var label = row.FindPropertyRelative("label").objectReferenceValue as Component;
                if (label && Named(label.transform.parent.name, out var named) && named != kind) failures.Add($"inventory: row '{label.transform.parent.name}' holds {kind}");
            }
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
                string name = Path.GetFileNameWithoutExtension(path);
                log.AppendLine($"enemy {name}: {enemy.stats.type} ({(int)enemy.stats.type})");
                if (!Enemies.TryGetValue(name, out var type)) failures.Add($"enemy: {name} is not in the check's table (add its kind)");
                else if (enemy.stats.type != type) failures.Add($"enemy: {name} has type {enemy.stats.type} ({(int)enemy.stats.type}), not {type}");
            }
            var campaign = AssetDatabase.LoadAssetAtPath<DefenseCampaignDefinition>("Assets/!_Project/Defense/Tutorial/Stage1/StageOneCampaign.asset");
            var data = GameDataApply.Load(campaign.rules.upgradeMultiplier);
            foreach (var lesson in campaign.progression.lessons)
            {
                var award = data.Awards.FirstOrDefault(a => a.Stage == lesson.stage && a.Wave == lesson.wave);
                string Of(IEnumerable<(BlockKind kind, int count)> items) => string.Join(", ", items.OrderBy(i => i.kind).Select(i => i.kind + "×" + i.count));
                string asset = Of(lesson.rewards.Select(b => (b.kind, b.count))), table = Of(award?.Blocks ?? new List<(BlockKind, int)>());
                if (asset != table) failures.Add($"campaign {lesson.stage}-{lesson.wave}: rewards {asset} but Award.md says {table}");
            }
            // The tutorial's bindings as TutorialInputBridge.Initialize takes them: a bad one turns the whole tutorial off.
            foreach (var bridge in Object.FindObjectsByType<TutorialInputBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var targets = new SerializedObject(bridge).FindProperty("targets"); var keys = new HashSet<string>();
                for (int i = 0; i < targets.arraySize; i++)
                {
                    var t = targets.GetArrayElementAtIndex(i); string key = t.FindPropertyRelative("key").stringValue;
                    bool ui = t.FindPropertyRelative("uiTarget").objectReferenceValue, world = t.FindPropertyRelative("worldTarget").objectReferenceValue;
                    if (string.IsNullOrWhiteSpace(key) || !t.FindPropertyRelative("allowRuntimeTarget").boolValue && !ui && !world || ui && world || !keys.Add(key))
                        failures.Add($"tutorial binding '{key}' has no target, two, or a key used twice");
                }
            }
            log.AppendLine($"campaign lessons {campaign.progression.lessons.Length}, stages {campaign.stages.Length}");
        }
    }
}
