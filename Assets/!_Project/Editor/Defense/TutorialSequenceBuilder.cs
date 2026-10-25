// Builds Stage 1 tutorial sequences from Docs/flow_stage1/tutorial_stage1_대본.md,
// applies wave settings, and wires the sequences into Defense.unity.
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

        [MenuItem("Tools/Develop/Apply Stage 1 Tutorials 1-1 to 1-5")]
        public static void ApplyAllFive()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 적용하세요.");
            EditorSceneManager.OpenScene("Assets/!_Project/Scenes/Defense.unity", OpenSceneMode.Single);
            Debug.Log(Run());
        }

        [MenuItem("Tools/Develop/Apply Beginner Tutorial 1-1")]
        public static void ApplyOneOne()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 적용하세요.");
            lines = ParseScript(Path.Combine(Directory.GetCurrentDirectory(), Script));
            var sequence = Write("1-1", 1, OneOne());
            var campaign = AssetDatabase.LoadAssetAtPath<DefenseCampaignDefinition>($"{Folder}/StageOneCampaign.asset");
            var so = new SerializedObject(campaign);
            SetupIntroEncounter(so);
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(campaign);
            AssetDatabase.SaveAssets();
            Debug.Log("Beginner tutorial 1-1 applied: " + sequence.steps.Length + " steps and intro encounter (other waves unchanged)");
        }

        const string Folder = "Assets/!_Project/Defense/Tutorial/Stage1";
        [MenuItem("Tools/Develop/Apply Beginner Lessons 1-1 to 1-3")]
        public static void ApplyFirstThree()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 적용하세요.");
            lines = ParseScript(Path.Combine(Directory.GetCurrentDirectory(), Script));
            var updated = new[] { Write("1-1", 1, OneOne()), Write("1-2", 2, OneTwo()), Write("1-3", 3, OneThree()) };
            Campaign(true);
            AssetDatabase.SaveAssets();
            Debug.Log("Beginner lessons applied: " + string.Join(", ", updated.Select(s => s.sequenceId + "=" + s.steps.Length)) + " (1-4 onward unchanged)");
        }
        const string Script = "Docs/flow_stage1/tutorial_stage1_대본.md";
        const string Start = TutorialKeys.Start, Develop = TutorialKeys.Develop, Drop = TutorialKeys.DropZone, Palette = TutorialKeys.Palette,
            Hint = TutorialKeys.Hint, Close = TutorialKeys.Close, Route = TutorialKeys.Route, Core = TutorialKeys.CoreHealth, Preview = TutorialKeys.Preview;
        static readonly string Unit0 = TutorialKeys.Unit(0), Range0 = TutorialKeys.Range(0), Range1 = TutorialKeys.Range(1);

        const string Code11 = "enemy = get_nearest_enemy()\nslash(enemy)";
        const string Code13Distance = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nslash(enemy)";
        const string Code13 = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nif enemy_distance <= my_attack_range():\n    slash(enemy)";

        sealed class Line { public string Speaker, Text; }

        // One step per script line; options shape the step (see 튜토리얼_아키텍처.md §6).
        sealed class S
        {
            public string Id, Text, Speaker;
            public TutorialStepKind Kind = TutorialStepKind.Dialogue;
            public string[] Targets = Array.Empty<string>(), Focus = Array.Empty<string>();
            public TutorialCompletionMode Mode;
            public string Signal, Trigger, Expected, Pointer, HintCode, AutoSlot;
            public float PointerDelay;
            public bool Dim, CloseEditor, ClearProgram, OpenEditor, Preview, Battle, HoldBattle, SlowBattle;
            public int Robot;
            public float Padding = 10;
            public string[] Highlight = Array.Empty<string>();
            public bool Intro, Cards, Undimmed, Dismissable;
            public float Auto;
            public string SkipIf;
            public string Demo;
            public bool Top;
            public float Hold;
            public bool DistanceExample, BaselineExample, RestartPreview;
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
        static Action<S> Highlight(params string[] keys) => s => s.Highlight = keys;
        static Action<S> With(params Action<S>[] parts) => s => { foreach (var part in parts) part?.Invoke(s); };
        static Action<S> Ed => s => s.Preview = true;
        static Action<S> Fight => s => s.Battle = true;
        static List<S> OneOne() => new List<S> {
            L("1-1.A01", s => s.Cards = true), L("1-1.A02", s => s.Intro = true), L("1-1.A03", s => s.Intro = true), L("1-1.A04", s => s.Intro = true), L("1-1.A04a", Focus(Route)), L("1-1.A04b", Focus(Core)), L("1-1.A04c", Focus(Core)), L("1-1.A05"), L("1-1.A06"),
            L("1-1.B01", Click(TutorialKeys.RobotButton(1), s => s.Dim = true)),
            L("1-1.B02", Signal(TutorialSignals.RobotPlaced, new[] { TutorialKeys.Deployment }, With(s => s.Dim = true, Focus(TutorialKeys.Floor)))),
            L("1-1.B03", Click(Unit0, s => s.Pointer = Unit0)),
            L("1-1.B04", Highlight(Range0)), L("1-1.B05", Focus(Range0)),
            L("1-1.C01", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; })),
            L("1-1.C02", Focus(Drop)), L("1-1.C03", Focus(Preview)), L("1-1.C04"),
            // Inspect the draft at each small action: incomplete lines cannot match compiled Python yet.
            L("1-1.C05", Signal(TutorialSignals.CodeLinePlaced, new[] { Drop, Palette }, With(
                s => { s.Expected = "1:DeclareVariable"; s.Pointer = "Editor.Palette.DeclareVariable";
                    s.Demo = "Editor.Palette.DeclareVariable>Editor.Code.Start"; },
                Focus("Editor.Palette.DeclareVariable", "Editor.Code.Start")))),
            L("1-1.C06", Focus("Editor.Code.Name.1")),
            L("1-1.C08", Code("enemy = get_nearest_enemy()", With(
                s => { s.Pointer = "Editor.Palette.NearestEnemy"; s.AutoSlot = "Editor.Code.Arg.1.0";
                    s.Demo = "Editor.Palette.NearestEnemy>Editor.Code.Arg.1.0"; },
                Focus("Editor.Palette.NearestEnemy", "Editor.Code.Arg.1.0")))),
            L("1-1.C09", Focus("Editor.Code.Arg.1.0")), L("1-1.C10", Focus("Editor.Code.Name.1")),
            L("1-1.C11", Focus("Editor.Code.Line.1")),
            L("1-1.D01", Signal(TutorialSignals.CodeLinePlaced, new[] { Drop, Palette }, With(
                s => { s.Expected = "2:Slash"; s.Pointer = "Editor.Palette.Slash";
                    s.Demo = "Editor.Palette.Slash>Editor.Code.After.1"; },
                Focus("Editor.Palette.Slash", "Editor.Code.After.1")))),
            L("1-1.D03", Signal(TutorialSignals.SlotFilled, new[] { Drop, Palette }, With(
                s => { s.Expected = "Editor.Code.Arg.2.0"; s.AutoSlot = s.Expected; s.Pointer = "Editor.Palette.Variable";
                    s.Demo = "Editor.Palette.Variable>Editor.Code.Arg.2.0"; },
                Focus("Editor.Palette.Variable", "Editor.Code.Arg.2.0")))),
            L("1-1.D04", Code(Code11, With(s => s.Pointer = "Editor.Code.Name.2.0", Focus("Editor.Code.Name.2.0")))),
            L("1-1.D05", Focus("Editor.Code.Name.1", "Editor.Code.Name.2.0")),
            L("1-1.D06", Focus("Editor.Code.Line.1")), L("1-1.D07", Focus("Editor.Code.Line.2")),
            L("1-1.D08", Signal(TutorialSignals.PreviewHit, new[] { Preview }, With(
                s => { s.Preview = true; s.Hold = 1.5f; }, Focus(Preview)))),
            L("1-1.D09", With(Ed, Focus(Preview))), L("1-1.D10", s => s.CloseEditor = true),
            L("1-1.E01", Focus(TutorialKeys.NextWave)),
            L("1-1.E03", Signal(TutorialSignals.BattleStarted, new[] { Start }, s => s.Top = true)),
            L("1-1.E04", s => { s.Trigger = TutorialSignals.FirstKill; s.SlowBattle = true; }),
            L("1-1.E04a", Click(TutorialKeys.Pause, s => { s.Pointer = TutorialKeys.Pause; s.SlowBattle = true; })),
            L("1-1.E04b", Click(TutorialKeys.Pause, s => { s.Pointer = TutorialKeys.Pause; s.HoldBattle = true; })),
            L("1-1.E04c", Click(TutorialKeys.Speed, s => s.Pointer = TutorialKeys.Speed)),
            L("1-1.E05", With(Fight, Focus(Core), s => s.Trigger = TutorialSignals.Leak)),
            L("1-1.E06", s => s.Trigger = TutorialSignals.WaveCleared),
            L("1-1.E07"), L("1-1.E08", Focus(TutorialKeys.RewardPanel)),
            // Claiming the reward starts 1-2, so it comes last.
            L("1-1.E09", Click("Battle.ClaimRewards", s => { s.Pointer = "Battle.ClaimRewards"; s.Top = true; })),
        };

        static List<S> OneTwo()
        {
            // The trigger hides the guide and releases input until the wave ends. Keep all player code.
            // Free play has no exact answer, compulsory editor visit or forced clear.
            return new List<S> {
                L("1-2.A01"), L("1-2.A02"), L("1-2.A03"),
                L("1-2.B01", s => { s.Trigger = TutorialSignals.WaveCleared; s.HintCode = Code11; s.Battle = true; }),
                L("1-2.B02"),
                L("1-2.B03", Click("Battle.ClaimRewards", s => { s.Pointer = "Battle.ClaimRewards"; s.Top = true; })),
            };
        }

        static List<S> OneThree()
        {
            Action<S> EditSignal(string signal, string expected, string pointer, params string[] focus) =>
                Signal(signal, new[] { Drop, Palette }, With(s => { s.Expected = expected; s.Pointer = pointer; }, Focus(focus)));
            return new List<S> {
                L("1-3.A01"), L("1-3.A02"),
                L("1-3.A03", Click(Unit0, s => s.Pointer = Unit0)),
                L("1-3.A04", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; })),
                L("1-3.A05", Signal(TutorialSignals.PreviewSingleHit, new[] { Preview }, With(
                    s => { s.BaselineExample = true; s.Preview = true; s.Hold = 1.5f; }, Focus(Preview)))),
                L("1-3.B01", With(s => s.DistanceExample = true, Focus("Editor.Code.Line.1"))),
                L("1-3.B02", Focus("Editor.Code.Line.2")),
                L("1-3.B03", Focus("Editor.Palette.MyAttackRange")),
                L("1-3.B04"), L("1-3.B05"),
                L("1-3.C01", EditSignal(TutorialSignals.CodeLinePlaced, "3:If", "Editor.Palette.If", "Editor.Palette.If", "Editor.Code.After.2")),
                L("1-3.C03", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg.3.0", "Editor.Palette.Comparison", "Editor.Palette.Comparison", "Editor.Code.Arg.3.0"), s => s.AutoSlot = "Editor.Code.Arg.3.0")),
                L("1-3.C04", EditSignal(TutorialSignals.DraftValueMatches, "3.0|<=", "Editor.Code.Option.3.0", "Editor.Code.Option.3.0")),
                L("1-3.C06", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg.3.0.0", "Editor.Palette.Variable", "Editor.Palette.Variable", "Editor.Code.Arg.3.0.0"), s => s.AutoSlot = "Editor.Code.Arg.3.0.0")),
                L("1-3.C07", EditSignal(TutorialSignals.DraftValueMatches, "3.0.0|enemy_distance", "Editor.Code.Name.3.0.0", "Editor.Code.Name.3.0.0")),
                L("1-3.C09", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg.3.0.1", "Editor.Palette.MyAttackRange", "Editor.Palette.MyAttackRange", "Editor.Code.Arg.3.0.1"), s => s.AutoSlot = "Editor.Code.Arg.3.0.1")),
                L("1-3.C09a", Focus("Editor.Code.Arg.3.0.1")),
                L("1-3.C10", Focus("Editor.Code.Arg.3.0")),
                L("1-3.D02", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Body.3", "Editor.Palette.Slash", "Editor.Palette.Slash", "Editor.Code.Body.3"), s => s.AutoSlot = "Editor.Code.Body.3")),
                L("1-3.D04", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.BodyArg.3.0.0", "Editor.Palette.Variable", "Editor.Palette.Variable", "Editor.Code.BodyArg.3.0.0"), s => s.AutoSlot = "Editor.Code.BodyArg.3.0.0")),
                L("1-3.D05", EditSignal(TutorialSignals.ProgramMatches, Code13, "Editor.Code.BodyName.3.0.0", "Editor.Code.BodyName.3.0.0")),
                L("1-3.D06", Signal(TutorialSignals.PreviewHit, new[] { Preview }, With(s => { s.Preview = true; s.RestartPreview = true; s.Hold = 2; }, Focus(Preview)))),
                L("1-3.D07", With(Ed, Focus("Editor.Code.Body.3"))),
                L("1-3.E01", s => s.CloseEditor = true),
                L("1-3.E02", Signal(TutorialSignals.BattleStarted, new[] { Start }, s => s.Top = true)),
                L("1-3.E03", With(Fight, s => s.Trigger = TutorialSignals.FirstAttack)),
                L("1-3.E04", s => s.Trigger = TutorialSignals.WaveCleared), L("1-3.E05"),
                L("1-3.E06", Click("Battle.ClaimRewards", s => { s.Pointer = "Battle.ClaimRewards"; s.Top = true; })),
            };
        }

        static List<S> OneFour() => new List<S> {
            L("1-4.A01", s => s.Cards = true), L("1-4.A02"), L("1-4.A03"), L("1-4.A04"),
            L("1-4.B01", s => { s.Trigger = TutorialSignals.WaveCleared; s.Battle = true; }),
            L("1-4.B02"), L("1-4.B03"),
            L("1-4.B04", Click("Battle.ClaimRewards", s => { s.Pointer = "Battle.ClaimRewards"; s.Top = true; })),
        };

        static List<S> OneFive() => new List<S> {
            L("1-5.A01", s => s.Cards = true), L("1-5.A02"), L("1-5.A03"), L("1-5.A04"),
            L("1-5.B01", s => { s.Trigger = TutorialSignals.WaveCleared; s.Battle = true; }),
            L("1-5.B02"), L("1-5.B03"),
            L("1-5.B04", Click("Battle.ClaimRewards", s => { s.Pointer = "Battle.ClaimRewards"; s.Top = true; })),
        };
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
                result.Add(m.Groups[1].Value, new Line { Speaker = m.Groups[2].Value, Text = text });
            }
            return result;
        }

        static TutorialSequence Write(string id, int wave, List<S> steps)
        {
            string path = $"{Folder}/Tutorial_{id}.asset";
            var sequence = AssetDatabase.LoadAssetAtPath<TutorialSequence>(path);
            if (!sequence) { sequence = ScriptableObject.CreateInstance<TutorialSequence>(); AssetDatabase.CreateAsset(sequence, path); }
            sequence.sequenceId = id; sequence.enabled = true; sequence.stage = 1; sequence.wave = wave;
            sequence.entryPhase = TutorialEntryPhase.Preparation; sequence.typeSpeed = 30;
            bool battle = false;
            sequence.steps = steps.Select(s =>
            {
                battle |= s.Battle || s.Trigger != null;
                return new TutorialStep {
                    stepId = s.Id, kind = s.Kind, dialoguePosition = s.Top ? TutorialDialoguePosition.Top : TutorialDialoguePosition.Bottom,
                    texts = new[] { new TutorialText { textId = "t1", speaker = s.Speaker, text = s.Text, focusKeys = s.Focus, highlightKeys = s.Highlight } },
                    targetKeys = s.Targets, dim = s.Dim, blockInput = true, outline = true,
                    completionMode = s.Kind == TutorialStepKind.ActionGuide ? s.Mode : TutorialCompletionMode.Unconfigured, completionSignal = s.Signal,
                    skipIf = s.SkipIf, pauseBattle = s.HoldBattle || !battle, slowBattle = s.SlowBattle,
                    allowPreview = s.Preview || id == "1-1" &&
                        ((s.Id.StartsWith("C", StringComparison.Ordinal) || s.Id.StartsWith("D", StringComparison.Ordinal)) && s.Id != "D10"),
                    highlightPadding = s.Padding, trigger = s.Trigger, expectedCode = s.Expected,
                    pointerKey = s.Pointer, autoSelectSlot = s.AutoSlot, hintCode = s.HintCode, pointerDelay = s.PointerDelay,
                    closeEditor = s.CloseEditor, clearProgram = s.ClearProgram, openEditor = s.OpenEditor, robotIndex = s.Robot,
                    prepareDistanceExample = s.DistanceExample, prepareBaselineExample = s.BaselineExample, restartPreview = s.RestartPreview,
                    introEntrance = s.Intro, undimmed = s.Undimmed, alertCards = s.Cards, autoAdvance = s.Auto, dismissable = s.Dismissable, holdSeconds = s.Hold, dragDemo = s.Demo };
            }).ToArray();
            EditorUtility.SetDirty(sequence);
            return sequence;
        }

        // Grants, learning topics, the 1-4 swarm, and the 1-5 boss.
        static string Campaign(bool firstThreeOnly = false)
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
            Lesson(1, "변수와 게임 플레이 방법 소개", 1, (BlockKind.DeclareVariable, 1), (BlockKind.NearestEnemy, 1), (BlockKind.Variable, 1), (BlockKind.Slash, 1));
            Lesson(2, "변수 자유 플레이", 1, (BlockKind.DeclareVariable, 1), (BlockKind.NearestEnemy, 1), (BlockKind.Variable, 1));
            Lesson(3, "조건: 내 공격 사거리 안이면 베기", 0, (BlockKind.DeclareVariable, 1), (BlockKind.Variable, 2), (BlockKind.MyPos, 1),
                (BlockKind.If, 1), (BlockKind.Comparison, 1), (BlockKind.MyAttackRange, 1));
            SetupIntroEncounter(so);
            // Keep the two-enemy preview so the range comparison is visible during movement.
            var practiceGroup = so.FindProperty("stages").GetArrayElementAtIndex(0).FindPropertyRelative("waves").GetArrayElementAtIndex(2)
                .FindPropertyRelative("groups").GetArrayElementAtIndex(0);
            practiceGroup.FindPropertyRelative("interval").floatValue = .5f;
            if (firstThreeOnly)
            {
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(campaign);
                return "campaign: lessons 1-1 to 1-3 only";
            }
            Lesson(4, "조건문 활용 자유 플레이", 1, (BlockKind.DeclareVariable, 2), (BlockKind.NearestEnemy, 1), (BlockKind.Variable, 3), (BlockKind.Slash, 1),
                (BlockKind.MyPos, 1), (BlockKind.If, 1), (BlockKind.Comparison, 1), (BlockKind.Number, 1));
            Lesson(5, "변수와 조건문: 보스 종합 실습", 0,
                (BlockKind.DeclareVariable, 3), (BlockKind.Variable, 5), (BlockKind.Number, 2),
                (BlockKind.If, 2), (BlockKind.Comparison, 2), (BlockKind.MyAttackRange, 2),
                (BlockKind.MyPos, 1), (BlockKind.NearestEnemy, 1), (BlockKind.Slash, 1));
            var waves = so.FindProperty("stages").GetArrayElementAtIndex(0).FindPropertyRelative("waves");
            var fourth = waves.GetArrayElementAtIndex(3).FindPropertyRelative("groups").GetArrayElementAtIndex(0);
            fourth.FindPropertyRelative("count").intValue = 6;
            fourth.FindPropertyRelative("interval").floatValue = .6f;
            var fifth = waves.GetArrayElementAtIndex(4).FindPropertyRelative("groups");
            var basic = AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{Folder}/TutorialError.asset");
            var minions = fifth.GetArrayElementAtIndex(0);
            minions.FindPropertyRelative("enemy").objectReferenceValue = basic;
            minions.FindPropertyRelative("route").intValue = 1;
            minions.FindPropertyRelative("count").intValue = 3;
            minions.FindPropertyRelative("delay").floatValue = 0;
            minions.FindPropertyRelative("interval").floatValue = 2;
            minions.FindPropertyRelative("healthOverride").floatValue = 0;
            minions.FindPropertyRelative("speedOverride").floatValue = 0;
            minions.FindPropertyRelative("leakDamageOverride").intValue = 0;
            minions.FindPropertyRelative("boss").boolValue = false;
            var boss = fifth.GetArrayElementAtIndex(1);
            boss.FindPropertyRelative("enemy").objectReferenceValue = basic;
            boss.FindPropertyRelative("route").intValue = 0;
            boss.FindPropertyRelative("count").intValue = 1;
            boss.FindPropertyRelative("delay").floatValue = 0;
            boss.FindPropertyRelative("interval").floatValue = 2;
            boss.FindPropertyRelative("healthOverride").floatValue = 84;
            boss.FindPropertyRelative("speedOverride").floatValue = .65f;
            boss.FindPropertyRelative("leakDamageOverride").intValue = 12;
            boss.FindPropertyRelative("boss").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(campaign);
            return "campaign: lessons 1-1 to 1-5, 1-4 swarm, 1-5 basic-enemy boss";
        }

        static void SetupIntroEncounter(SerializedObject so)
        {
            var stage = so.FindProperty("stages").GetArrayElementAtIndex(0);
            var firstGroups = stage.FindPropertyRelative("waves").GetArrayElementAtIndex(0).FindPropertyRelative("groups");
            firstGroups.arraySize = 2;
            var basicEnemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{Folder}/TutorialError.asset");
            if (!basicEnemy) throw new InvalidOperationException("1-1의 기본 적 asset이 없습니다.");
            var strongGroup = firstGroups.GetArrayElementAtIndex(1);
            strongGroup.FindPropertyRelative("enemy").objectReferenceValue = basicEnemy;
            strongGroup.FindPropertyRelative("route").intValue = 0;
            strongGroup.FindPropertyRelative("count").intValue = 1;
            strongGroup.FindPropertyRelative("delay").floatValue = 12;
            strongGroup.FindPropertyRelative("interval").floatValue = 2;
            strongGroup.FindPropertyRelative("healthOverride").floatValue = 500;
            strongGroup.FindPropertyRelative("speedOverride").floatValue = 1.6f;
            // All three enemies use the same basic prefab; only this last spawn has stronger stats.
            stage.FindPropertyRelative("waveSeconds").floatValue = 120;
        }

        public static string Run()
        {
            var log = new System.Text.StringBuilder();
            lines = ParseScript(Path.Combine(Directory.GetCurrentDirectory(), Script));
            log.AppendLine("lines " + lines.Count);
            var sequences = new[] { Write("1-1", 1, OneOne()), Write("1-2", 2, OneTwo()), Write("1-3", 3, OneThree()), Write("1-4", 4, OneFour()), Write("1-5", 5, OneFive()) };
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
