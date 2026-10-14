// Builds Tutorial_1-1 ~ Tutorial_1-4 from Docs/flow_stage1/tutorial_stage1_대본.md (dialogue stays verbatim),
// sets the Stage 1 grants/topics/1-4 enemy count (명세서 §2-1) and wires the sequences into Defense.unity.
// Run from Tools/Develop after editing the script document; Defense.unity must be the open scene.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    public static class TutorialSequenceBuilder
    {
        [MenuItem("Tools/Develop/Stage 1 튜토리얼 asset 다시 만들기")]
        public static void Menu() => Debug.Log(Run());

        const string Folder = "Assets/!_Project/Defense/Tutorial/Stage1";
        const string Script = "Docs/flow_stage1/tutorial_stage1_대본.md";
        const string Start = TutorialKeys.Start, Develop = TutorialKeys.Develop, Drop = TutorialKeys.DropZone, Palette = TutorialKeys.Palette,
            Hint = TutorialKeys.Hint, Close = TutorialKeys.Close, Route = TutorialKeys.Route, Core = TutorialKeys.CoreHealth, Preview = TutorialKeys.Preview;
        static readonly string Unit0 = TutorialKeys.Unit(0), Range0 = TutorialKeys.Range(0), Range1 = TutorialKeys.Range(1);

        const string Code11 = "enemy = get_nearest_enemy()\nslash(enemy)";
        const string Code13Distance = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nslash(enemy)";
        const string Code13 = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nif enemy_distance <= my_attack_range():\n    slash(enemy)";
        const string Code14Middle = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nattack_range = my_attack_range()\nif enemy_distance <= my_attack_range():\n    slash(enemy)";
        const string Code14 = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nattack_range = my_attack_range()\n\nif enemy_distance <= attack_range:\n    slash(enemy)";

        sealed class Line { public string Id, Speaker, Text; }

        // One step per script line; options shape the step (see 튜토리얼_아키텍처.md §6).
        sealed class S
        {
            public string Id, Text, Speaker;
            public TutorialStepKind Kind = TutorialStepKind.Dialogue;
            public string[] Targets = Array.Empty<string>(), Focus = Array.Empty<string>();
            public TutorialCompletionMode Mode;
            public string Signal, Trigger, Expected, Pointer, HintCode;
            public float PointerDelay;
            public bool Dim, CloseEditor, ClearProgram, OpenEditor, Preview, Battle;
            public int Robot;
        }

        static Dictionary<string, Line> lines;
        static S L(string id, Action<S> configure = null)
        {
            var line = lines[id];
            var step = new S { Id = id.Substring(id.IndexOf('.') + 1), Text = line.Text, Speaker = line.Speaker };
            configure?.Invoke(step);
            return step;
        }
        static Action<S> Act(string[] targets, TutorialCompletionMode mode, string signal = null, Action<S> more = null) => s =>
        { s.Kind = TutorialStepKind.ActionGuide; s.Targets = targets; s.Mode = mode; s.Signal = signal; more?.Invoke(s); };
        static Action<S> Click(string target, Action<S> more = null) => Act(new[] { target }, TutorialCompletionMode.TargetClick, null, more);
        static Action<S> Signal(string signal, string[] targets, Action<S> more = null) => Act(targets, TutorialCompletionMode.ExternalSignal, signal, more);
        static Action<S> Code(string expected, Action<S> more = null) => Signal(TutorialSignals.ProgramMatches, new[] { Drop, Palette }, s => { s.Expected = expected; s.Preview = true; more?.Invoke(s); });
        static Action<S> Focus(params string[] keys) => s => s.Focus = keys;
        static Action<S> With(params Action<S>[] parts) => s => { foreach (var part in parts) part?.Invoke(s); };
        static Action<S> Ed => s => s.Preview = true;
        static Action<S> Fight => s => s.Battle = true;
        // Same line split in two: click the robot, then the develop button (the second does not retype).
        static IEnumerable<S> OpenEditor(string id, string robot = null)
        {
            robot = robot ?? Unit0;
            yield return L(id, Click(robot, s => { s.Id += "a"; s.Pointer = robot; }));
            yield return L(id, Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Id += "b"; s.Pointer = Develop; s.Preview = true; }));
        }

        static List<S> OneOne() => new List<S> {
            L("1-1.A01"), L("1-1.A02"), L("1-1.A03"), L("1-1.A04"), L("1-1.A04a", Focus(Route)), L("1-1.A04b", Focus(Core)), L("1-1.A04c", Focus(Core)), L("1-1.A05"), L("1-1.A06"),
            L("1-1.B01", Click(TutorialKeys.RobotButton(1), s => s.Dim = true)),
            L("1-1.B02", Signal(TutorialSignals.RobotPlaced, new[] { TutorialKeys.Deployment }, With(s => s.Dim = true, Focus(Route)))),
            L("1-1.B03", Click(Unit0, s => s.Pointer = Unit0)),
            L("1-1.B04"), L("1-1.B05"), L("1-1.B06"),
            L("1-1.C01", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; })),
            L("1-1.C02", Ed), L("1-1.C03", With(Ed, Focus(Drop, Preview))), L("1-1.C04", Ed), L("1-1.C05", Ed), L("1-1.C06", Ed), L("1-1.C07", Ed),
            L("1-1.C08", Ed), L("1-1.C09", Ed), L("1-1.C10", Ed), L("1-1.C11", With(Ed, Focus("Editor.Palette.NearestEnemy"))),
            L("1-1.C12", With(Ed, Focus("Editor.Palette.DeclareVariable"))), L("1-1.C13", Ed), L("1-1.C14", Ed), L("1-1.C15", Ed),
            L("1-1.C16", Code("enemy = get_nearest_enemy()", With(s => s.Pointer = "Editor.Code.Start", Focus("Editor.Code.Start")))),
            L("1-1.C17", Ed), L("1-1.C18", Ed), L("1-1.D01", Ed), L("1-1.D02", Ed), L("1-1.D03", Ed),
            L("1-1.D04", Code(Code11, With(s => s.Pointer = "Editor.Code.Arg.2.0|Editor.Code.After.1", Focus("Editor.Palette.Slash")))),
            L("1-1.D05", Ed),
            L("1-1.D06", Signal(TutorialSignals.PreviewHit, new[] { Preview }, s => s.Preview = true)),
            L("1-1.D07", Ed), L("1-1.D08", Ed), L("1-1.D09", Ed), L("1-1.D10", Ed),
            L("1-1.D11", s => s.CloseEditor = true),
            L("1-1.E01"), L("1-1.E02"),
            L("1-1.E03", Signal(TutorialSignals.BattleStarted, new[] { Start }, Focus(Route, Range0, Core))),
            L("1-1.E04", With(Fight, s => s.Trigger = TutorialSignals.FirstKill)),
            L("1-1.E05", With(Fight, s => s.Trigger = TutorialSignals.Leak)),
            L("1-1.E06", s => s.Trigger = TutorialSignals.WaveCleared), L("1-1.E07"), L("1-1.E08", Focus(Core)),
        };

        static List<S> OneTwo()
        {
            var steps = new List<S> { L("1-2.A01"), L("1-2.A02"), L("1-2.A03"), L("1-2.A04") };
            steps.AddRange(OpenEditor("1-2.A05"));
            steps.Add(L("1-2.B01", With(Ed, Focus(Drop, Preview))));
            steps.AddRange(new[] { "B02", "B03", "B04", "B05", "B06", "B07", "B08", "B09" }.Select(id => L("1-2." + id, Ed)));
            steps.AddRange(new[] {
                L("1-2.C01", Ed), L("1-2.C02", Ed), L("1-2.C03", Ed), L("1-2.C04", Ed),
                L("1-2.C05", With(Ed, Focus(Hint), s => { s.ClearProgram = true; s.HintCode = Code11; })),
                L("1-2.C06", Signal(TutorialSignals.ProgramMatches, new[] { Drop, Palette, Hint }, s =>
                    { s.Expected = Code11; s.HintCode = Code11; s.Pointer = Hint; s.PointerDelay = 60; s.Preview = true; })),
                L("1-2.C06", Signal(TutorialSignals.EditorClosed, new[] { Close }, s => { s.Id += "c"; s.Preview = true; })),
                L("1-2.D01", Signal(TutorialSignals.BattleStarted, new[] { Start })),
                L("1-2.D02", s => s.Trigger = TutorialSignals.WaveCleared), L("1-2.D03"), L("1-2.D04"),
            });
            return steps;
        }

        static List<S> OneThree()
        {
            var steps = new List<S> { L("1-3.A01"), L("1-3.A02", Focus(Route)), L("1-3.A03", Focus(Unit0)), L("1-3.A04") };
            steps.AddRange(OpenEditor("1-3.A05"));
            steps.AddRange(new[] {
                L("1-3.B01", Ed),
                L("1-3.B02", Code(Code13Distance, s => s.Pointer = "Editor.Code.After.1")),
                L("1-3.B03", With(Ed, Focus("Editor.Code.Line.1", "Editor.Code.Arg.2.0"))), L("1-3.B04", With(Ed, Focus("Editor.Code.Line.2"))),
                L("1-3.C01", With(Ed, Focus("Editor.Palette.MyAttackRange"))), L("1-3.C02", Ed),
                L("1-3.C03", Signal(TutorialSignals.ConditionBuilt, new[] { Drop, Palette }, s =>
                    { s.Expected = "enemy_distance <= my_attack_range()"; s.Pointer = "Editor.Palette.Comparison"; s.Preview = true; })),
                L("1-3.C04", With(Ed, Focus("Editor.Code.Loose"))), L("1-3.C05", With(Ed, Focus("Editor.Code.Loose"))), L("1-3.C06", With(Ed, Focus("Editor.Code.Loose"))),
                L("1-3.D01", Ed),
                L("1-3.D02", Code(Code13, With(s => s.Pointer = "Editor.Code.Body.3|Editor.Code.After.2", Focus("Editor.Code.Body.3")))),
                L("1-3.D03", Ed), L("1-3.D04", Ed),
                L("1-3.E01", s => s.CloseEditor = true),
                L("1-3.E02", Signal(TutorialSignals.BattleStarted, new[] { Start }, Focus(Route, Range0, Core))),
                L("1-3.E03", Fight),
                L("1-3.E04", With(Fight, Focus(Range0), s => s.Trigger = TutorialSignals.FirstAttack)),
                L("1-3.E05", s => s.Trigger = TutorialSignals.WaveCleared), L("1-3.E06"),
            });
            return steps;
        }

        static List<S> OneFour()
        {
            // The second warrior is placed by the player before 1-4 starts talking; the lines are 1-1 B01·B02 verbatim.
            var steps = new List<S> {
                L("1-1.B01", Click(TutorialKeys.RobotButton(1), s => { s.Id = "P01"; s.Dim = true; })),
                L("1-1.B02", Signal(TutorialSignals.RobotPlaced, new[] { TutorialKeys.Deployment }, With(s => { s.Id = "P02"; s.Dim = true; }, Focus(Route)))),
                L("1-4.A01"), L("1-4.A02"), L("1-4.A03"), L("1-4.A04"), L("1-4.A05", Focus(Unit0)), L("1-4.A06", Focus(Route)), L("1-4.A07", Focus(Range0)),
            };
            steps.AddRange(OpenEditor("1-4.B01"));
            steps.AddRange(new[] {
                L("1-4.B02", Ed), L("1-4.B03", Ed), L("1-4.B04", Ed),
                L("1-4.C01", With(Ed, Focus("Editor.Palette.DeclareVariable"))), L("1-4.C02", Ed), L("1-4.C03", With(Ed, Focus("Editor.Palette.MyAttackRange"))),
                L("1-4.C04", Code(Code14Middle, s => s.Pointer = "Editor.Code.After.2")),
                L("1-4.C05", Ed), L("1-4.C06", With(Ed, Focus("Editor.Code.Arg.4.0.1"))),
                L("1-4.C07", Code(Code14, With(s => s.Pointer = "Editor.Code.Arg.4.0.1", Focus("Editor.Code.Arg.4.0.1")))),
                L("1-4.C08", Ed), L("1-4.C09", Ed), L("1-4.C10", Ed), L("1-4.C11", Ed),
                L("1-4.D01", s => { s.OpenEditor = true; s.Robot = 1; s.HintCode = Code14; s.Preview = true; }),
                L("1-4.D02", s => { s.Robot = 1; s.HintCode = Code14; s.Preview = true; }),
                L("1-4.D03", With(Focus(Hint), s => { s.Robot = 1; s.HintCode = Code14; s.Preview = true; })),
                L("1-4.D04", Signal(TutorialSignals.ProgramMatches, new[] { Drop, Palette, Hint }, s =>
                    { s.Robot = 1; s.Expected = Code14; s.HintCode = Code14; s.Pointer = Hint; s.PointerDelay = 60; s.Preview = true; })),
                L("1-4.D04", Signal(TutorialSignals.EditorClosed, new[] { Close }, s => { s.Id += "c"; s.Robot = 1; s.Preview = true; })),
                L("1-4.D05"),
                L("1-4.E01"), L("1-4.E02"), L("1-4.E03"),
                L("1-4.E04", Signal(TutorialSignals.BattleStarted, new[] { Start }, Focus(Route, Range0, Range1, Core))),
                L("1-4.E05", With(Fight, s => s.Trigger = TutorialSignals.FirstAttack)),
                L("1-4.E06", With(Fight, Focus(Range0, Range1))),
                L("1-4.F01", s => s.Trigger = TutorialSignals.WaveCleared), L("1-4.F02"), L("1-4.F03"), L("1-4.F04"),
            });
            return steps;
        }

        // Script code names in the code colour (1-4 already has backticks).
        static readonly Regex CodeNames = new Regex(@"(?<![A-Za-z0-9_.`])(" + string.Join("|", new[] {
            "enemy.distance(get_my_pos())", ".distance(get_my_pos())", "enemy.distance()", "get_nearest_enemy()", "get_my_pos()", "my_attack_range()",
            "enemy_distance", "slash(enemy)", "slash()", ".hp", "enemy", "if", "<=" }.Select(Regex.Escape)) + @")(?![A-Za-z0-9_(`])");

        static Dictionary<string, Line> ParseScript(string path)
        {
            var result = new Dictionary<string, Line>();
            var pattern = new Regex(@"^\[(\d-\d\.[A-Z]\d\d[a-z]?)\] (?:\((?:ui|UI)로 표시\)\s*)?(\S+) : (.*)$");
            foreach (var raw in File.ReadAllLines(path))
            {
                var m = pattern.Match(raw);
                if (!m.Success) continue;
                string text = m.Groups[3].Value.Trim().Replace("\\n", "\n").Replace(" \n ", "\n");
                if (!text.Contains("`")) text = CodeNames.Replace(text, "`$1`");
                result.Add(m.Groups[1].Value, new Line { Id = m.Groups[1].Value, Speaker = m.Groups[2].Value, Text = text });
            }
            return result;
        }

        static TutorialSequence Write(string id, int wave, List<S> steps)
        {
            string path = $"{Folder}/Tutorial_{id}.asset";
            var sequence = AssetDatabase.LoadAssetAtPath<TutorialSequence>(path);
            if (!sequence) { sequence = ScriptableObject.CreateInstance<TutorialSequence>(); AssetDatabase.CreateAsset(sequence, path); }
            sequence.sequenceId = id; sequence.enabled = true; sequence.stage = 1; sequence.wave = wave;
            sequence.entryPhase = TutorialEntryPhase.Preparation; sequence.typeSpeed = 15;
            bool battle = false;
            sequence.steps = steps.Select(s =>
            {
                battle |= s.Battle || s.Trigger != null;
                return new TutorialStep {
                    stepId = s.Id, kind = s.Kind, dialoguePosition = TutorialDialoguePosition.Bottom,
                    texts = new[] { new TutorialText { textId = "t1", speaker = s.Speaker, text = s.Text, focusKeys = s.Focus } },
                    targetKeys = s.Targets, highlightKeys = Array.Empty<string>(), dim = s.Dim, blockInput = true, outline = true,
                    completionMode = s.Kind == TutorialStepKind.ActionGuide ? s.Mode : TutorialCompletionMode.Unconfigured, completionSignal = s.Signal,
                    pauseBattle = !battle, allowPreview = s.Preview, highlightPadding = 10, trigger = s.Trigger, expectedCode = s.Expected,
                    pointerKey = s.Pointer, hintCode = s.HintCode, pointerDelay = s.PointerDelay,
                    closeEditor = s.CloseEditor, clearProgram = s.ClearProgram, openEditor = s.OpenEditor, robotIndex = s.Robot };
            }).ToArray();
            EditorUtility.SetDirty(sequence);
            return sequence;
        }

        // Grants per wave (명세서 §2-1), topics, and the 1-4 enemy count.
        static string Campaign()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<DefenseCampaignDefinition>($"{Folder}/StageOneCampaign.asset");
            var so = new SerializedObject(campaign);
            var lessons = so.FindProperty("progression.lessons");
            void Lesson(int wave, string topic, int warriors, params (BlockKind kind, int count)[] blocks)
            {
                for (int i = 0; i < lessons.arraySize; i++)
                {
                    var lesson = lessons.GetArrayElementAtIndex(i);
                    if (lesson.FindPropertyRelative("stage").intValue != 1 || lesson.FindPropertyRelative("wave").intValue != wave) continue;
                    lesson.FindPropertyRelative("topic").stringValue = topic;
                    var robots = lesson.FindPropertyRelative("robots");
                    robots.arraySize = warriors > 0 ? 1 : 0;
                    if (warriors > 0) { robots.GetArrayElementAtIndex(0).FindPropertyRelative("role").intValue = (int)RobotRole.Warrior; robots.GetArrayElementAtIndex(0).FindPropertyRelative("count").intValue = warriors; }
                    var stock = lesson.FindPropertyRelative("blocks");
                    stock.arraySize = blocks.Length;
                    for (int b = 0; b < blocks.Length; b++)
                    {
                        stock.GetArrayElementAtIndex(b).FindPropertyRelative("kind").intValue = (int)blocks[b].kind;
                        stock.GetArrayElementAtIndex(b).FindPropertyRelative("count").intValue = blocks[b].count;
                    }
                    return;
                }
                throw new InvalidOperationException("lesson 1-" + wave + " not found");
            }
            Lesson(1, "변수: 적 정보 저장하기", 1, (BlockKind.DeclareVariable, 1), (BlockKind.NearestEnemy, 1), (BlockKind.Variable, 1), (BlockKind.Slash, 1));
            Lesson(2, "점(.)으로 적 정보 확인하기", 0);
            Lesson(3, "조건: 사거리 안의 적만 공격하기", 0, (BlockKind.DeclareVariable, 1), (BlockKind.Variable, 2), (BlockKind.MyPos, 1),
                (BlockKind.If, 1), (BlockKind.Comparison, 1), (BlockKind.MyAttackRange, 1));
            Lesson(4, "변수에 사거리 저장하기", 1, (BlockKind.DeclareVariable, 4), (BlockKind.NearestEnemy, 1), (BlockKind.Variable, 5), (BlockKind.Slash, 1),
                (BlockKind.MyPos, 1), (BlockKind.If, 1), (BlockKind.Comparison, 1), (BlockKind.MyAttackRange, 2));
            var group = so.FindProperty("stages").GetArrayElementAtIndex(0).FindPropertyRelative("waves").GetArrayElementAtIndex(3)
                .FindPropertyRelative("groups").GetArrayElementAtIndex(0);
            group.FindPropertyRelative("count").intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(campaign);
            return "campaign: grants, topics, 1-4 enemies 4";
        }

        public static string Run()
        {
            var log = new System.Text.StringBuilder();
            lines = ParseScript(Path.Combine(Directory.GetCurrentDirectory(), Script));
            log.AppendLine("lines " + lines.Count);
            var sequences = new[] { Write("1-1", 1, OneOne()), Write("1-2", 2, OneTwo()), Write("1-3", 3, OneThree()), Write("1-4", 4, OneFour()) };
            foreach (var s in sequences) log.AppendLine(s.sequenceId + ": " + s.steps.Length + " steps");
            log.AppendLine(Campaign());
            AssetDatabase.SaveAssets();
            var director = UnityEngine.Object.FindObjectsByType<TutorialDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var so = new SerializedObject(director);
            var list = so.FindProperty("sequences");
            list.arraySize = sequences.Length;
            for (int i = 0; i < sequences.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = sequences[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            log.AppendLine("scene saved " + EditorSceneManager.SaveScene(director.gameObject.scene));
            return log.ToString();
        }
    }
}
