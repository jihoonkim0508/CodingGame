// Builds the lesson tutorial sequences (1-1 to 1-5, 2-1, 2-2) from Docs/flow_stage1/tutorial_stage1_대본.md,
// applies wave settings and the lesson table (DefenseCurriculum), and wires the sequences into Defense.unity.
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
            var sequence = Write("1-1", OneOne());
            Debug.Log("Beginner tutorial 1-1 applied: " + sequence.steps.Length + " steps; " + Campaign(true));
            AssetDatabase.SaveAssets();
        }

        const string Folder = "Assets/!_Project/Defense/Tutorial/Stage1";
        [MenuItem("Tools/Develop/Apply Beginner Lessons 1-1 to 1-3")]
        public static void ApplyFirstThree()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 적용하세요.");
            lines = ParseScript(Path.Combine(Directory.GetCurrentDirectory(), Script));
            var updated = new[] { Write("1-1", OneOne()), Write("1-2", OneTwo()), Write("1-3", OneThree()) };
            string campaign = Campaign(true);
            AssetDatabase.SaveAssets();
            Debug.Log("Beginner lessons applied: " + string.Join(", ", updated.Select(s => s.sequenceId + "=" + s.steps.Length)) + "; " + campaign);
        }
        const string Script = "Docs/flow_stage1/tutorial_stage1_대본.md";
        const string Start = TutorialKeys.Start, Develop = TutorialKeys.Develop, Drop = TutorialKeys.DropZone, Palette = TutorialKeys.Palette,
            Hint = TutorialKeys.Hint, Close = TutorialKeys.Close, Route = TutorialKeys.Route, Core = TutorialKeys.CoreHealth, Preview = TutorialKeys.Preview;
        static readonly string Unit0 = TutorialKeys.Unit(0), Range0 = TutorialKeys.Range(0), Shooter = TutorialKeys.RobotButton((int)RobotRole.Shooter);

        // Lesson answers; the hint of the free-play wave after each lesson shows the same code.
        const string Code11 = "while True:\n    slash()";
        const string Code13 = "while True:\n    enemy = get_nearest_enemy()\n    attack(enemy)";
        const string Code21 = "while True:\n    enemy = get_nearest_enemy()\n    enemy_distance = enemy.distance(get_my_pos())\n    if enemy_distance <= 3:\n        attack(enemy)";

        sealed class Line { public string Speaker, Text; }

        // One step per script line; options shape the step (see 튜토리얼_아키텍처.md §6).
        sealed class S
        {
            public string Id, Text, Speaker;
            public TutorialStepKind Kind = TutorialStepKind.Dialogue;
            public string[] Targets = Array.Empty<string>(), Focus = Array.Empty<string>();
            public TutorialCompletionMode Mode;
            public string Signal, Trigger, Expected, Pointer, HintCode, AutoSlot, RequiredSlotBlock;
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
            public bool DistanceExample, BaselineExample, RestartPreview, Spot;
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
        // A guided edit; a block placement also previews its path from the palette block to the slot (the last code focus key).
        static Action<S> Edit(string signal, string expected, string pointer, params string[] focus) =>
            Signal(signal, new[] { Drop, Palette }, With(s => {
                s.Expected = expected; s.Pointer = pointer;
                string slot = focus.LastOrDefault(key => key.StartsWith(TutorialKeys.CodePrefix, StringComparison.Ordinal));
                if ((signal == TutorialSignals.CodeLinePlaced || signal == TutorialSignals.SlotFilled) && slot != null &&
                    pointer.StartsWith(TutorialKeys.PalettePrefix, StringComparison.Ordinal)) s.Demo = pointer + ">" + slot;
            }, Focus(focus)));
        // Places palette block `kind` into `slot`, which is selected on entry so a click on the palette block lands there.
        static Action<S> Put(BlockKind kind, string slot, string landing = null) =>
            With(Edit(TutorialSignals.SlotFilled, slot, "Editor.Palette." + kind, "Editor.Palette." + kind, landing ?? slot),
                s => { s.AutoSlot = slot; s.RequiredSlotBlock = slot + ":" + kind; });
        static Action<S> Focus(params string[] keys) => s => s.Focus = keys;
        static Action<S> Highlight(params string[] keys) => s => s.Highlight = keys;
        static Action<S> With(params Action<S>[] parts) => s => { foreach (var part in parts) part?.Invoke(s); };
        static Action<S> Ed => s => s.Preview = true;
        static Action<S> Fight => s => s.Battle = true;
        static Action<S> Claim => Click("Battle.ClaimRewards", s => { s.Pointer = "Battle.ClaimRewards"; s.Top = true; });
        static Action<S> Watch(bool restart, float hold) => Signal(TutorialSignals.PreviewHit, new[] { Preview },
            With(s => { s.Preview = true; s.RestartPreview = restart; s.Hold = hold; }, Focus(Preview)));
        static Action<S> PlaceShooter => Click(Shooter, s => s.Dim = true);
        // The robot goes on the one marked cell by the road (Battle.Spot), so its range and 2-1's distance condition reach the enemies.
        static Action<S> Placed => Signal(TutorialSignals.RobotPlaced, new[] { TutorialKeys.Spot },
            With(s => { s.Dim = true; s.Spot = true; s.Pointer = TutorialKeys.Spot; }, Focus(TutorialKeys.Spot)));
        static Action<S> OpenEditor => Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; });

        // 1-1 loops: one slash() runs once a wave, so it goes inside while True.
        static List<S> OneOne() => new List<S> {
            L("1-1.A01", s => s.Cards = true), L("1-1.A02", s => s.Intro = true), L("1-1.A03", s => s.Intro = true), L("1-1.A04", s => s.Intro = true), L("1-1.A04a", Focus(Route)), L("1-1.A04b", Focus(Core)), L("1-1.A04c", Focus(Core)), L("1-1.A05"), L("1-1.A06"),
            L("1-1.B01", Click(TutorialKeys.RobotButton((int)RobotRole.Warrior), s => s.Dim = true)),
            L("1-1.B02", Placed),
            L("1-1.B03", Click(Unit0, s => s.Pointer = Unit0)),
            L("1-1.B04", Highlight(Range0)), L("1-1.B05", Focus(Range0)),
            L("1-1.C01", OpenEditor),
            L("1-1.C02", Focus(Drop)), L("1-1.C03", Focus(Preview)), L("1-1.C04", Focus("Editor.Palette.Slash")),
            L("1-1.C05", Edit(TutorialSignals.CodeLinePlaced, "1:Slash", "Editor.Palette.Slash", "Editor.Palette.Slash", "Editor.Code.Start")),
            // Restarted so the single swing comes right away instead of after the demo's next loop.
            L("1-1.C06", Watch(true, 1.5f)),
            L("1-1.C07", With(Ed, Focus(Preview))), L("1-1.C08"),
            L("1-1.D01", s => s.ClearProgram = true),
            L("1-1.D02", Edit(TutorialSignals.CodeLinePlaced, "1:While", "Editor.Palette.While", "Editor.Palette.While", "Editor.Code.Start")),
            L("1-1.D03", Focus("Editor.Code.Line.1")),
            L("1-1.D05", Code(Code11, With(s => { s.AutoSlot = "Editor.Code.Body.1"; s.Pointer = "Editor.Palette.Slash";
                s.RequiredSlotBlock = "Editor.Code.Body.1:Slash"; s.Demo = "Editor.Palette.Slash>Editor.Code.Body.1"; },
                Focus("Editor.Palette.Slash", "Editor.Code.Body.1")))),
            L("1-1.D06", Focus("Editor.Code.Line.1", "Editor.Code.Body.1")),
            L("1-1.D07", Watch(true, 2)),
            L("1-1.D08", With(Ed, Focus(Preview))), L("1-1.D09", s => s.CloseEditor = true),
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
            L("1-1.E09", Claim),
        };

        // Free play: the trigger hides the guide and releases input until the wave ends. Keep all player code;
        // no exact answer, compulsory editor visit or forced clear. With `place`, the lines ending in "a" and "b" pick the new
        // robot and put it on the marked cell first, so a far-off robot can't miss every enemy (2-2's distance condition).
        static List<S> FreePlay(string id, string hint, bool cards = false, RobotRole? place = null)
        {
            var steps = lines.Keys.Where(key => key.StartsWith(id + ".", StringComparison.Ordinal)).OrderBy(key => key, StringComparer.Ordinal).ToList();
            var result = new List<S>();
            foreach (var key in steps)
            {
                string step = key.Substring(id.Length + 1);
                if (step == "A01" && cards) result.Add(L(key, s => s.Cards = true));
                else if (place != null && step.EndsWith("a", StringComparison.Ordinal)) result.Add(L(key, Click(TutorialKeys.RobotButton((int)place), s => s.Dim = true)));
                else if (place != null && step.EndsWith("b", StringComparison.Ordinal)) result.Add(L(key, Placed));
                else if (step == "B01") result.Add(L(key, s => { s.Trigger = TutorialSignals.WaveCleared; s.HintCode = hint; s.Battle = true; }));
                else if (key == steps.Last()) result.Add(L(key, Claim));
                else result.Add(L(key));
            }
            return result;
        }
        static List<S> OneTwo() => FreePlay("1-2", Code11, place: RobotRole.Warrior);

        // 1-3 variables: the shooter's attack(enemy) needs a target, found and stored inside the loop.
        static List<S> OneThree() => new List<S> {
            L("1-3.A01"), L("1-3.A02"),
            L("1-3.B01", PlaceShooter), L("1-3.B02", Placed),
            L("1-3.B03", Click(Unit0, s => s.Pointer = Unit0)),
            L("1-3.B04", OpenEditor),
            L("1-3.C01", Edit(TutorialSignals.CodeLinePlaced, "1:While", "Editor.Palette.While", "Editor.Palette.While", "Editor.Code.Start")),
            L("1-3.C02", Put(BlockKind.DeclareVariable, "Editor.Code.Body.1")),
            L("1-3.C03", Focus("Editor.Code.Name.1>1")),
            L("1-3.C04", Put(BlockKind.NearestEnemy, "Editor.Code.Arg.1>1.0")),
            L("1-3.C05", Focus("Editor.Code.Arg.1>1.0", "Editor.Code.Name.1>1")),
            L("1-3.C06", Focus("Editor.Code.Line.1>1")),
            // The body stays selected, so the attack block appends under the enemy line.
            L("1-3.D01", With(Edit(TutorialSignals.CodeLinePlaced, "1>2:Shot", "Editor.Palette.Shot", "Editor.Palette.Shot", "Editor.Code.After.1>1"),
                s => s.AutoSlot = "Editor.Code.Body.1")),
            L("1-3.D02", Put(BlockKind.Variable, "Editor.Code.Arg.1>2.0")),
            L("1-3.D03", Code(Code13, With(s => { s.Pointer = "Editor.Code.Name.1>2.0";
                s.RequiredSlotBlock = "Editor.Code.Arg.1>2.0:Variable"; }, Focus("Editor.Code.Name.1>2.0")))),
            L("1-3.D04", Focus("Editor.Code.Name.1>1", "Editor.Code.Name.1>2.0")),
            L("1-3.D05", Watch(true, 1.5f)),
            L("1-3.D06", With(Ed, Focus("Editor.Code.Line.1>1"))),
            L("1-3.D07", s => s.CloseEditor = true),
            L("1-3.E01", Signal(TutorialSignals.BattleStarted, new[] { Start }, s => s.Top = true)),
            L("1-3.E02", With(Fight, s => s.Trigger = TutorialSignals.FirstAttack)),
            L("1-3.E03", s => s.Trigger = TutorialSignals.WaveCleared), L("1-3.E04"),
            L("1-3.E05", Focus(TutorialKeys.RewardPanel)),
            L("1-3.E06", Claim),
        };

        static List<S> OneFour() => FreePlay("1-4", Code13, cards: true, place: RobotRole.Shooter);
        static List<S> OneFive() => FreePlay("1-5", null, cards: true);

        // 2-1 conditions: the shooter hits harder up close, so it fires only when the enemy is within 3.
        static List<S> TwoOne() => new List<S> {
            L("2-1.A01"), L("2-1.A02"), L("2-1.A03"), L("2-1.A04"),
            L("2-1.B01", PlaceShooter), L("2-1.B02", Placed),
            L("2-1.B03", Click(Unit0, s => s.Pointer = Unit0)),
            L("2-1.B04", OpenEditor),
            L("2-1.B05", With(Watch(false, 2), s => s.BaselineExample = true)),
            L("2-1.C01", With(s => s.DistanceExample = true, Focus("Editor.Code.Line.1>1"))),
            L("2-1.C02", Focus("Editor.Code.Line.1>2")),
            L("2-1.C03"), L("2-1.C04"),
            L("2-1.D01", With(Edit(TutorialSignals.CodeLinePlaced, "1>3:If", "Editor.Palette.If", "Editor.Palette.If", "Editor.Code.After.1>2"),
                s => s.AutoSlot = "Editor.Code.Body.1")),
            L("2-1.D02", Put(BlockKind.Comparison, "Editor.Code.Arg.1>3.0")),
            L("2-1.D03", Edit(TutorialSignals.DraftValueMatches, "1>3.0|<=", "Editor.Code.Option.1>3.0", "Editor.Code.Option.1>3.0")),
            L("2-1.D04", Put(BlockKind.Variable, "Editor.Code.Arg.1>3.0.0")),
            L("2-1.D05", Edit(TutorialSignals.DraftValueMatches, "1>3.0.0|enemy_distance", "Editor.Code.Name.1>3.0.0", "Editor.Code.Name.1>3.0.0")),
            L("2-1.D06", Put(BlockKind.Number, "Editor.Code.Arg.1>3.0.1")),
            L("2-1.D07", Edit(TutorialSignals.DraftValueMatches, "1>3.0.1|3", "Editor.Code.Name.1>3.0.1", "Editor.Code.Name.1>3.0.1")),
            L("2-1.D08", Focus("Editor.Code.Arg.1>3.0")),
            L("2-1.E01", Put(BlockKind.Shot, "Editor.Code.Body.1>3")),
            L("2-1.E02", Put(BlockKind.Variable, "Editor.Code.Arg.1>3>1.0")),
            L("2-1.E03", Code(Code21, With(s => { s.Pointer = "Editor.Code.Name.1>3>1.0";
                s.RequiredSlotBlock = "Editor.Code.Arg.1>3>1.0:Variable"; }, Focus("Editor.Code.Name.1>3>1.0")))),
            L("2-1.E04", Watch(true, 2)),
            L("2-1.E05", With(Ed, Focus("Editor.Code.Body.1>3"))),
            L("2-1.F01", s => s.CloseEditor = true),
            L("2-1.F02", Signal(TutorialSignals.BattleStarted, new[] { Start }, s => s.Top = true)),
            L("2-1.F03", With(Fight, s => s.Trigger = TutorialSignals.FirstAttack)),
            L("2-1.F04", s => s.Trigger = TutorialSignals.WaveCleared), L("2-1.F05"),
            L("2-1.F06", Claim),
        };
        static List<S> TwoTwo() => FreePlay("2-2", Code21, place: RobotRole.Shooter);

        // Script code names in the code colour (lines that already have backticks are left as written).
        static readonly Regex CodeNames = new Regex(@"(?<![A-Za-z0-9_.`])(" + string.Join("|", new[] {
            "enemy.distance(get_my_pos())", ".distance(get_my_pos())", "enemy.distance()", "get_nearest_enemy()", "get_my_pos()", "my_attack_range()",
            "enemy_distance", "attack(enemy)", "slash()", "attack", ".hp", "enemy", "while True", "while", "for", "if", "<=" }.Select(Regex.Escape)) + @")(?![A-Za-z0-9_(`])");

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

        // id is "stage-wave"; Stage 2's sequences go to Tutorial/Stage2.
        static TutorialSequence Write(string id, List<S> steps)
        {
            int stage = id[0] - '0', wave = id[2] - '0';
            string folder = stage == 1 ? Folder : "Assets/!_Project/Defense/Tutorial/Stage" + stage;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/!_Project/Defense/Tutorial", "Stage" + stage);
            string path = $"{folder}/Tutorial_{id}.asset";
            var sequence = AssetDatabase.LoadAssetAtPath<TutorialSequence>(path);
            if (!sequence) { sequence = ScriptableObject.CreateInstance<TutorialSequence>(); AssetDatabase.CreateAsset(sequence, path); }
            sequence.sequenceId = id; sequence.enabled = true; sequence.stage = stage; sequence.wave = wave;
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
                    requiredSlotBlock = s.RequiredSlotBlock,
                    skipIf = s.SkipIf, pauseBattle = s.HoldBattle || !battle, slowBattle = s.SlowBattle,
                    // 1-1 keeps the demo running while blocks are placed, up to the line that closes the editor.
                    allowPreview = s.Preview || id == "1-1" && !s.CloseEditor &&
                        (s.Id.StartsWith("C", StringComparison.Ordinal) || s.Id.StartsWith("D", StringComparison.Ordinal)),
                    highlightPadding = s.Padding, trigger = s.Trigger, expectedCode = s.Expected,
                    pointerKey = s.Pointer, autoSelectSlot = s.AutoSlot, hintCode = s.HintCode, pointerDelay = s.PointerDelay,
                    closeEditor = s.CloseEditor, clearProgram = s.ClearProgram, openEditor = s.OpenEditor, robotIndex = s.Robot,
                    prepareDistanceExample = s.DistanceExample, prepareBaselineExample = s.BaselineExample, restartPreview = s.RestartPreview,
                    introEntrance = s.Intro, undimmed = s.Undimmed, alertCards = s.Cards, autoAdvance = s.Auto, dismissable = s.Dismissable, holdSeconds = s.Hold, dragDemo = s.Demo,
                    placeSpot = s.Spot };
            }).ToArray();
            EditorUtility.SetDirty(sequence);
            return sequence;
        }

        // The lesson table (DefenseCurriculum), the 1-1 strong enemy, the 1-4 swarm and the 1-5 boss.
        static string Campaign(bool firstThreeOnly = false)
        {
            var campaign = AssetDatabase.LoadAssetAtPath<DefenseCampaignDefinition>($"{Folder}/StageOneCampaign.asset");
            var so = new SerializedObject(campaign);
            Lessons(so);
            SetupIntroEncounter(so);
            var waves = so.FindProperty("stages").GetArrayElementAtIndex(0).FindPropertyRelative("waves");
            // 1-3: two basic enemies two seconds apart, like 1-2.
            waves.GetArrayElementAtIndex(2).FindPropertyRelative("groups").GetArrayElementAtIndex(0).FindPropertyRelative("interval").floatValue = 2;
            if (firstThreeOnly)
            {
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(campaign);
                return "campaign: lesson table, 1-1 and 1-3 encounters";
            }
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
            // 2-1: one shooter needs two close shots (2.4s) per enemy, so the five enemies come 3s apart, not 2s.
            // 2-2: eight enemies 2.2s apart (was 1.7s); at 1.7s the front shooter fell behind and one outlasted the 35s wave.
            var stageTwo = so.FindProperty("stages").GetArrayElementAtIndex(1).FindPropertyRelative("waves");
            stageTwo.GetArrayElementAtIndex(0).FindPropertyRelative("groups").GetArrayElementAtIndex(0).FindPropertyRelative("interval").floatValue = 3;
            stageTwo.GetArrayElementAtIndex(1).FindPropertyRelative("groups").GetArrayElementAtIndex(0).FindPropertyRelative("interval").floatValue = 2.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(campaign);
            return "campaign: lesson table, 1-1 to 1-5 encounters, 1-4 swarm, 1-5 basic-enemy boss";
        }

        // The campaign asset keeps its own copy of the lesson table; DefenseCurriculum is the one source.
        static void Lessons(SerializedObject so)
        {
            var lessons = so.FindProperty("progression.lessons");
            var source = DefenseCurriculum.Create();
            lessons.arraySize = source.Length;
            for (int i = 0; i < source.Length; i++)
            {
                var lesson = lessons.GetArrayElementAtIndex(i);
                lesson.FindPropertyRelative("stage").intValue = source[i].stage;
                lesson.FindPropertyRelative("wave").intValue = source[i].wave;
                lesson.FindPropertyRelative("topic").stringValue = source[i].topic;
                var robots = lesson.FindPropertyRelative("robots");
                robots.arraySize = source[i].robots.Length;
                for (int r = 0; r < source[i].robots.Length; r++)
                {
                    robots.GetArrayElementAtIndex(r).FindPropertyRelative("role").intValue = (int)source[i].robots[r].role;
                    robots.GetArrayElementAtIndex(r).FindPropertyRelative("count").intValue = source[i].robots[r].count;
                }
                Stock(lesson.FindPropertyRelative("blocks"), source[i].blocks);
                Stock(lesson.FindPropertyRelative("rewards"), source[i].rewards);
            }
        }
        static void Stock(SerializedProperty list, BlockStock[] stock)
        {
            list.arraySize = stock.Length;
            for (int b = 0; b < stock.Length; b++)
            {
                list.GetArrayElementAtIndex(b).FindPropertyRelative("kind").intValue = (int)stock[b].kind;
                list.GetArrayElementAtIndex(b).FindPropertyRelative("count").intValue = stock[b].count;
            }
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
            var sequences = new[] { Write("1-1", OneOne()), Write("1-2", OneTwo()), Write("1-3", OneThree()), Write("1-4", OneFour()), Write("1-5", OneFive()),
                Write("2-1", TwoOne()), Write("2-2", TwoTwo()) };
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
