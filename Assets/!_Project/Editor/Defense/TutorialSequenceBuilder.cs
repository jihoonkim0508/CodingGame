// Builds the tutorial sequences from Docs/flow_stage1/tutorial_stage1_대본.md, applies the campaign's waves and lessons,
// and wires the sequences into Defense.unity. Stages (13차 179): 1-1 melee, 1-2 free, 1-3 loops, 1-4 free, 1-5 boss;
// 2-1 variables and the shooter, 2-2 free, 2-3 conditions, 2-4 and 2-5 free, 2-6 boss; 3-1 the tank, 3-2 and 3-3 free, 3-4 boss.
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
        public static void Menu() => Debug.Log(Run());

        [MenuItem("Tools/Develop/Apply Stage 1 Tutorials 1-1 to 1-5")]
        public static void ApplyAllFive()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 적용하세요.");
            EditorSceneManager.OpenScene("Assets/!_Project/Scenes/Defense.unity", OpenSceneMode.Single);
            Debug.Log(Run());
        }

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

        // The function runs when called: battle start calls it once, the robot's Play button again (play feedback 4).
        // 1-1: one swing a call; 1-3: the loop, while True, swings on and on from one call (1-4 free play, the same with a third
        // warrior); 3-1: the tank's loop. No for for the player (12차 121): every loop is while True.
        const string Code11 = "slash()";
        const string Code13 = "while True:\n    slash()";
        const string Code31 = "while True:\n    enemy = get_nearest_enemy()\n    block(enemy)";
        // 2-1: the found enemy kept in a variable; 2-3: the shooter waits until the enemy is within 3, where one shot fells it
        // (32 x 7/distance, at most 3x), instead of firing on entry at 7 and reloading 6 s while the enemy walks past.
        const string Code21 = "while True:\n    enemy = get_nearest_enemy()\n    attack(enemy)";
        const string Code23 = "while True:\n    enemy = get_nearest_enemy()\n    enemy_distance = enemy.distance(get_my_pos())\n    if enemy_distance <= 3:\n        attack(enemy)";
        const string StagesFolder = "Assets/!_Project/Defense/Tutorial/Stages23";

        sealed class Line { public string Speaker, Text; }

        // One step per script ID; options shape the step (see 튜토리얼_아키텍처.md §6). An ID written on several lines gives
        // the step several texts in order; an action step waits on its last text, so that line carries the instruction.
        sealed class S
        {
            public string Id;
            public Line[] Lines;
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
            public float Hold, TriggerDelay;
            public bool DistanceExample, BaselineExample, RestartPreview, PlaceShooter;
        }

        static Dictionary<string, List<Line>> lines;
        static S L(string id, Action<S> configure = null)
        {
            var step = new S { Id = id.Substring(id.IndexOf('.') + 1), Lines = lines[id].ToArray() };
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
        // 디버's entrance lines: the HUD dims with the greyed map, so the dialogue is the one bright thing (focus report 1-3).
        static Action<S> Intro => s => { s.Intro = true; s.Dim = true; };
        static List<S> OneOne() => new List<S> {
            L("1-1.A01", s => s.Cards = true), L("1-1.A02", Intro), L("1-1.A03", Intro), L("1-1.A04a", Focus(Route)), L("1-1.A04b", Focus(Core)), L("1-1.A05"),
            L("1-1.B01", Click(TutorialKeys.RobotButton(1), s => s.Dim = true)),
            L("1-1.B02", Signal(TutorialSignals.RobotPlaced, new[] { TutorialKeys.Deployment }, With(s => s.Dim = true, Focus(TutorialKeys.Floor)))),
            L("1-1.B03", Click(Unit0, s => s.Pointer = Unit0)),
            // The range itself is the highlight (tinted inside), not a dimmed screen with a round hole: seen in perspective
            // the hole never lined up with the ring (play test).
            L("1-1.B04", Highlight(Range0)),
            L("1-1.C01", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; })),
            L("1-1.C02", Focus(Drop)), L("1-1.C03", Focus(Preview)),
            L("1-1.C05", Code(Code11, With(
                s => { s.Pointer = "Editor.Palette.Slash"; s.Demo = "Editor.Palette.Slash>Editor.Code.Start"; },
                Focus("Editor.Palette.Slash", "Editor.Code.Start")))),
            L("1-1.C09", Focus("Editor.Code.Line.1")), L("1-1.C10", Focus("Editor.Code.Line.1")),
            // The line is about when the function is called (the preview calls it again whenever it ends).
            L("1-1.D06", Focus("Editor.Code.Line.1", Preview)),
            L("1-1.D08", Signal(TutorialSignals.PreviewHit, new[] { Preview }, With(
                s => { s.Preview = true; s.Hold = 3; }, Focus(Preview)))),
            L("1-1.D09", With(Ed, Focus(Preview))),
            // Before the first workspace closes: the bottom line is where the code's problems show (debugging).
            L("1-1.D09a", With(Ed, Focus(TutorialKeys.Status))),
            L("1-1.D10", s => s.CloseEditor = true),
            L("1-1.E01", Focus(TutorialKeys.NextWave)),
            L("1-1.E03", Signal(TutorialSignals.BattleStarted, new[] { Start }, s => s.Top = true)),
            // Battle start called the function once and it ended (a swing, likely at nobody); then, slowed, the first enemy
            // in range is the moment for the robot's Play button.
            L("1-1.E03a", s => { s.Trigger = TutorialSignals.FunctionEnded; s.TriggerDelay = .2f; s.HoldBattle = true; }),
            L("1-1.E03b", Click(TutorialKeys.Play(0), s => { s.Trigger = TutorialSignals.EnemyInRange; s.Pointer = TutorialKeys.Play(0); s.SlowBattle = true; s.Top = true; })),
            // A basic enemy takes two swings (the warrior's 7 against 12.6): once the first call ends, the second press.
            L("1-1.E03c", Click(TutorialKeys.Play(0), s => { s.Trigger = TutorialSignals.FunctionEnded; s.TriggerDelay = .3f; s.Pointer = TutorialKeys.Play(0); s.SlowBattle = true; s.Top = true; })),
            L("1-1.E04", s => { s.Trigger = TutorialSignals.FirstKill; s.TriggerDelay = .8f; s.SlowBattle = true; }),
            L("1-1.E04a", Click(TutorialKeys.Pause, s => { s.Pointer = TutorialKeys.Pause; s.SlowBattle = true; })),
            L("1-1.E04b", Click(TutorialKeys.Pause, s => { s.Pointer = TutorialKeys.Pause; s.HoldBattle = true; })),
            L("1-1.E04c", Click(TutorialKeys.Speed, s => s.Pointer = TutorialKeys.Speed)),
            L("1-1.E04d"),
            L("1-1.E05", With(Fight, Focus(Core), s => s.Trigger = TutorialSignals.Leak)),
            L("1-1.E06", s => s.Trigger = TutorialSignals.WaveCleared),
            L("1-1.E07"),
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

        // 1-3: the loop lesson moved from 1-1. The warrior from 1-1 gets a while True with the swing inside; the code is cleared first.
        static List<S> OneThree() => new List<S> {
            L("1-3.A01"),
            L("1-3.A03", Click(Unit0, s => s.Pointer = Unit0)),
            L("1-3.A04", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; })),
            // The slash() lines are seen as the step clears them (13차 176; the line before it was cut for length).
            L("1-3.C01a", With(Ed, s => s.ClearProgram = true, Focus(Drop))),
            // The player opens the 제어 tab, where the loop is (the editor opens on 명령).
            L("1-3.C04", Click(TutorialKeys.PaletteTab(0), With(Ed, s => s.Pointer = TutorialKeys.PaletteTab(0), Focus(TutorialKeys.PaletteTab(0))))),
            L("1-3.C05", Signal(TutorialSignals.CodeLinePlaced, new[] { Drop, Palette }, With(
                s => { s.Expected = "1:While"; s.Pointer = "Editor.Palette.While"; s.Preview = true;
                    s.Demo = "Editor.Palette.While>Editor.Code.Start"; },
                Focus("Editor.Palette.While", "Editor.Code.Start")))),
            L("1-3.C06", With(Ed, Focus("Editor.Code.Line.1"))),
            L("1-3.C08", Code(Code13, With(
                s => { s.Pointer = "Editor.Palette.Slash"; s.AutoSlot = "Editor.Code.Body.1";
                    s.Demo = "Editor.Palette.Slash>Editor.Code.Body.1"; },
                Focus("Editor.Palette.Slash", "Editor.Code.Body.1")))),
            L("1-3.C10", With(Ed, Focus("Editor.Code.Line.1"))),
            L("1-3.D01", With(Ed, Focus("Editor.Code.Line.1", Preview))),
            L("1-3.D02", s => s.CloseEditor = true),
            L("1-3.E01"),
            L("1-3.B01", s => { s.Trigger = TutorialSignals.WaveCleared; s.HintCode = Code13; s.Battle = true; }),
            L("1-3.B03", Click("Battle.ClaimRewards", s => { s.Pointer = "Battle.ClaimRewards"; s.Top = true; })),
        };

        // A guided block placement: the step finishes on signal (expected says what), with the pointer on the palette block and
        // the drag preview from it to the code slot it fills.
        static Action<S> EditSignal(string signal, string expected, string pointer, params string[] focus) =>
            Signal(signal, new[] { Drop, Palette }, With(s => {
                s.Expected = expected; s.Pointer = pointer;
                if (signal == TutorialSignals.CodeLinePlaced || signal == TutorialSignals.SlotFilled)
                {
                    string slot = signal == TutorialSignals.SlotFilled ? expected :
                        focus.LastOrDefault(key => key.StartsWith(TutorialKeys.CodePrefix, StringComparison.Ordinal));
                    if (!string.IsNullOrEmpty(slot)) s.Demo = pointer + ">" + slot;
                }
            }, Focus(focus)));

        // 2-1 (play feedback 4: variables open in Stage 2): a shooter stood by the road; while True, then enemy = get_nearest_enemy()
        // inside it, then attack(enemy) under that.
        static List<S> TwoOne() => new List<S> {
            L("2-1.A01", s => s.Cards = true),
            L("2-1.A03", s => s.PlaceShooter = true),
            L("2-1.A04", Click(Unit0, s => s.Pointer = Unit0)),
            L("2-1.A05", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; })),
            L("2-1.B01", EditSignal(TutorialSignals.CodeLinePlaced, "1:While", "Editor.Palette.While", "Editor.Palette.While", "Editor.Code.Start")),
            L("2-1.B02", With(EditSignal(TutorialSignals.CodeLinePlaced, "1/1:DeclareVariable", "Editor.Palette.DeclareVariable", "Editor.Palette.DeclareVariable", "Editor.Code.Body.1"),
                s => s.AutoSlot = "Editor.Code.Body.1")),
            L("2-1.B03", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg.1/1.0", "Editor.Palette.NearestEnemy", "Editor.Palette.NearestEnemy", "Editor.Code.Arg.1/1.0"),
                s => s.AutoSlot = "Editor.Code.Arg.1/1.0")),
            L("2-1.B04", With(Ed, Focus("Editor.Code.Line.1/1"))),
            L("2-1.B05", With(EditSignal(TutorialSignals.CodeLinePlaced, "1/2:Shot", "Editor.Palette.Shot", "Editor.Palette.Shot", "Editor.Code.After.1/1"),
                s => { s.AutoSlot = "Editor.Code.Body.1"; s.Demo = "Editor.Palette.Shot>Editor.Code.After.1/1"; })),
            L("2-1.B06", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg.1/2.0", "Editor.Palette.Variable", "Editor.Palette.Variable", "Editor.Code.Arg.1/2.0"),
                s => s.AutoSlot = "Editor.Code.Arg.1/2.0")),
            L("2-1.B07", EditSignal(TutorialSignals.ProgramMatches, Code21, "Editor.Code.Name.1/2.0", "Editor.Code.Name.1/2.0")),
            L("2-1.C02", Signal(TutorialSignals.PreviewHit, new[] { Preview }, With(s => { s.Preview = true; s.RestartPreview = true; s.Hold = 2; }, Focus(Preview)))),
            L("2-1.D01", s => s.CloseEditor = true),
            L("2-1.D02"),
            L("2-1.E01", s => { s.Trigger = TutorialSignals.WaveCleared; s.HintCode = Code21; s.Battle = true; }),
            L("2-1.E02", Click("Battle.ClaimRewards", s => { s.Pointer = "Battle.ClaimRewards"; s.Top = true; })),
        };

        // 2-3: the old 1-3 lesson. The shooter from 2-1 (Robot.Unit.0) first shows the plain loop firing on entry; then its code
        // gets the distance line and the player builds the if round attack(enemy).
        static List<S> TwoThree() => new List<S> {
            L("2-3.A01"),
            L("2-3.A03", Click(Unit0, s => s.Pointer = Unit0)),
            L("2-3.A04", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; })),
            // Held 6 s past the first (far) hit: the shooter reloads while the enemy walks right past it.
            L("2-3.A05", Signal(TutorialSignals.PreviewHit, new[] { Preview }, With(
                s => { s.BaselineExample = true; s.Preview = true; s.Hold = 6; }, Focus(Preview)))),
            L("2-3.B01", With(s => s.DistanceExample = true, Focus("Editor.Code.Line.1/1"))),
            L("2-3.B02", Focus("Editor.Code.Line.1/2")),
            L("2-3.B03"),
            L("2-3.B04"), L("2-3.B05"),
            // Everything sits inside the for (line 1); the if becomes its third line ("1/3").
            L("2-3.C01", With(EditSignal(TutorialSignals.CodeLinePlaced, "1/3:If", "Editor.Palette.If", "Editor.Palette.If", "Editor.Code.After.1/2"),
                s => { s.AutoSlot = "Editor.Code.Body.1"; s.Demo = "Editor.Palette.If>Editor.Code.After.1/2"; })),
            L("2-3.C03", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg.1/3.0", "Editor.Palette.Comparison", "Editor.Palette.Comparison", "Editor.Code.Arg.1/3.0"), s => s.AutoSlot = "Editor.Code.Arg.1/3.0")),
            L("2-3.C04", EditSignal(TutorialSignals.DraftValueMatches, "1/3.0|<=", "Editor.Code.Option.1/3.0", "Editor.Code.Option.1/3.0")),
            L("2-3.C06", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg.1/3.0.0", "Editor.Palette.Variable", "Editor.Palette.Variable", "Editor.Code.Arg.1/3.0.0"), s => s.AutoSlot = "Editor.Code.Arg.1/3.0.0")),
            L("2-3.C07", EditSignal(TutorialSignals.DraftValueMatches, "1/3.0.0|enemy_distance", "Editor.Code.Name.1/3.0.0", "Editor.Code.Name.1/3.0.0")),
            L("2-3.C09", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg.1/3.0.1", "Editor.Palette.Number", "Editor.Palette.Number", "Editor.Code.Arg.1/3.0.1"), s => s.AutoSlot = "Editor.Code.Arg.1/3.0.1")),
            L("2-3.C09a", EditSignal(TutorialSignals.DraftValueMatches, "1/3.0.1|3", "Editor.Code.Name.1/3.0.1", "Editor.Code.Name.1/3.0.1")),
            L("2-3.C10", Focus("Editor.Code.Arg.1/3.0")),
            // attack() goes in the if, then the enemy it shoots goes in its brackets (a variable renamed enemy finishes the code).
            L("2-3.D02", With(EditSignal(TutorialSignals.CodeLinePlaced, "1/3/1:Shot", "Editor.Palette.Shot", "Editor.Palette.Shot", "Editor.Code.Body.1/3"),
                s => { s.AutoSlot = "Editor.Code.Body.1/3"; s.Demo = "Editor.Palette.Shot>Editor.Code.Body.1/3"; })),
            L("2-3.D03", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg.1/3/1.0", "Editor.Palette.Variable", "Editor.Palette.Variable", "Editor.Code.Arg.1/3/1.0"), s => s.AutoSlot = "Editor.Code.Arg.1/3/1.0")),
            L("2-3.D04", EditSignal(TutorialSignals.ProgramMatches, Code23, "Editor.Code.Name.1/3/1.0", "Editor.Code.Name.1/3/1.0")),
            L("2-3.D06", Signal(TutorialSignals.PreviewHit, new[] { Preview }, With(s => { s.Preview = true; s.RestartPreview = true; s.Hold = 3; }, Focus(Preview)))),
            L("2-3.D07", With(Ed, Focus("Editor.Code.Body.1/3"))),
            L("2-3.E01", s => s.CloseEditor = true),
            L("2-3.E02", Signal(TutorialSignals.BattleStarted, new[] { Start }, s => s.Top = true)),
            // A beat after the first shot, so the player sees it before '쐈다!'.
            L("2-3.E03", With(Fight, s => { s.Trigger = TutorialSignals.FirstAttack; s.TriggerDelay = 1.2f; })),
            L("2-3.E04", s => { s.Trigger = TutorialSignals.WaveCleared; s.HintCode = Code23; }),
            L("2-3.E06", Click("Battle.ClaimRewards", s => { s.Pointer = "Battle.ClaimRewards"; s.Top = true; })),
        };

        static List<S> OneFour() => new List<S> {
            L("1-4.A01", s => s.Cards = true), L("1-4.A02"), L("1-4.A03"), L("1-4.A04"),
            L("1-4.B01", s => { s.Trigger = TutorialSignals.WaveCleared; s.HintCode = Code13; s.Battle = true; }),
            L("1-4.B03"),
            L("1-4.B04", Click("Battle.ClaimRewards", s => { s.Pointer = "Battle.ClaimRewards"; s.Top = true; })),
        };

        // 3-1 (13차 179): 디버 brings the tank, the player picks it and stands it on the road; its code is the player's (the
        // hint holds the loop with block(enemy)), then the battle.
        static List<S> ThreeOne() => new List<S> {
            L("3-1.A01", s => s.Cards = true), L("3-1.A02"), L("3-1.A03"),
            L("3-1.B01", Click(TutorialKeys.RobotButton((int)RobotRole.Tank), s => s.Dim = true)),
            L("3-1.B02", Signal(TutorialSignals.RobotPlaced, new[] { TutorialKeys.Deployment }, With(s => s.Dim = true, Focus(Route)))),
            L("3-1.B03"), L("3-1.B04"),
            L("3-1.E01", s => { s.Trigger = TutorialSignals.WaveCleared; s.HintCode = Code31; s.Battle = true; }),
            L("3-1.E02", Click("Battle.ClaimRewards", s => { s.Pointer = "Battle.ClaimRewards"; s.Top = true; })),
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
            "enemy_distance", "while True", "slash()", "block()", "attack()", ".hp", "enemy", "while", "if", "<=" }.Select(Regex.Escape)) + @")(?![A-Za-z0-9_(`])");

        static Dictionary<string, List<Line>> ParseScript(string path)
        {
            var result = new Dictionary<string, List<Line>>();
            var pattern = new Regex(@"^\[(\d-\d\.[A-Z]\d\d[a-z]?)\] (?:\((?:ui|UI)로 표시\)\s*)?(\S+) : (.*)$");
            foreach (var raw in File.ReadAllLines(path))
            {
                var m = pattern.Match(raw);
                if (!m.Success) continue;
                string text = m.Groups[3].Value.Trim().Replace("\\n", "\n").Replace(" \n ", "\n");
                if (!text.Contains("`")) text = CodeNames.Replace(text, "`$1`");
                if (!result.TryGetValue(m.Groups[1].Value, out var said)) result.Add(m.Groups[1].Value, said = new List<Line>());
                said.Add(new Line { Speaker = m.Groups[2].Value, Text = text });
            }
            return result;
        }

        static TutorialSequence Write(string id, int wave, List<S> steps, int stage = 1)
        {
            string path = $"{(stage == 1 ? Folder : StagesFolder)}/Tutorial_{id}.asset";
            var sequence = AssetDatabase.LoadAssetAtPath<TutorialSequence>(path);
            if (!sequence) { sequence = ScriptableObject.CreateInstance<TutorialSequence>(); AssetDatabase.CreateAsset(sequence, path); }
            sequence.sequenceId = id; sequence.enabled = true; sequence.stage = stage; sequence.wave = wave;
            sequence.entryPhase = TutorialEntryPhase.Preparation; sequence.typeSpeed = 40;
            bool battle = false;
            sequence.steps = steps.Select(s =>
            {
                battle |= s.Battle || s.Trigger != null;
                return new TutorialStep {
                    stepId = s.Id, kind = s.Kind, dialoguePosition = s.Top ? TutorialDialoguePosition.Top : TutorialDialoguePosition.Bottom,
                    texts = s.Lines.Select((line, i) => new TutorialText { textId = "t" + (i + 1), speaker = line.Speaker, text = line.Text, focusKeys = s.Focus, highlightKeys = s.Highlight }).ToArray(),
                    targetKeys = s.Targets, dim = s.Dim, blockInput = true, outline = true,
                    completionMode = s.Kind == TutorialStepKind.ActionGuide ? s.Mode : TutorialCompletionMode.Unconfigured, completionSignal = s.Signal,
                    requiredSlotBlock = s.RequiredSlotBlock,
                    skipIf = s.SkipIf, pauseBattle = s.HoldBattle || !battle, slowBattle = s.SlowBattle,
                    allowPreview = s.Preview || id == "1-1" &&
                        ((s.Id.StartsWith("C", StringComparison.Ordinal) || s.Id.StartsWith("D", StringComparison.Ordinal)) && s.Id != "D10"),
                    highlightPadding = s.Padding, trigger = s.Trigger, expectedCode = s.Expected,
                    pointerKey = s.Pointer, autoSelectSlot = s.AutoSlot, hintCode = s.HintCode, pointerDelay = s.PointerDelay,
                    closeEditor = s.CloseEditor, clearProgram = s.ClearProgram, openEditor = s.OpenEditor, robotIndex = s.Robot,
                    prepareDistanceExample = s.DistanceExample, prepareBaselineExample = s.BaselineExample, restartPreview = s.RestartPreview, placeShooter = s.PlaceShooter,
                    introEntrance = s.Intro, undimmed = s.Undimmed, alertCards = s.Cards, autoAdvance = s.Auto, dismissable = s.Dismissable, holdSeconds = s.Hold, triggerDelay = s.TriggerDelay, dragDemo = s.Demo };
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
            void Lesson(int wave, string topic, int warriors, params (BlockKind kind, int count)[] blocks) => StageLesson(1, wave, topic, RobotRole.Warrior, warriors, blocks);
            void StageLesson(int stage, int wave, string topic, RobotRole role, int robotCount, params (BlockKind kind, int count)[] blocks)
            {
                for (int i = 0; i < lessons.arraySize; i++)
                {
                    var lesson = lessons.GetArrayElementAtIndex(i);
                    if (lesson.FindPropertyRelative("stage").intValue != stage || lesson.FindPropertyRelative("wave").intValue != wave) continue;
                    lesson.FindPropertyRelative("topic").stringValue = topic;
                    var robots = lesson.FindPropertyRelative("robots");
                    robots.arraySize = robotCount > 0 ? 1 : 0;
                    if (robotCount > 0) { robots.GetArrayElementAtIndex(0).FindPropertyRelative("role").intValue = (int)role; robots.GetArrayElementAtIndex(0).FindPropertyRelative("count").intValue = robotCount; }
                    var stock = lesson.FindPropertyRelative("blocks");
                    stock.arraySize = blocks.Length;
                    for (int b = 0; b < blocks.Length; b++)
                    {
                        stock.GetArrayElementAtIndex(b).FindPropertyRelative("kind").intValue = (int)blocks[b].kind;
                        stock.GetArrayElementAtIndex(b).FindPropertyRelative("count").intValue = blocks[b].count;
                    }
                    return;
                }
                // A wave without a lesson yet (2-6): a new entry, no rewards.
                lessons.arraySize++;
                var added = lessons.GetArrayElementAtIndex(lessons.arraySize - 1);
                added.FindPropertyRelative("stage").intValue = stage; added.FindPropertyRelative("wave").intValue = wave;
                added.FindPropertyRelative("rewards").arraySize = 0; added.FindPropertyRelative("goal").stringValue = "";
                StageLesson(stage, wave, topic, role, robotCount, blocks);
            }
            void Rewards(int stage, int wave, params (BlockKind kind, int count)[] blocks)
            {
                for (int i = 0; i < lessons.arraySize; i++)
                {
                    var lesson = lessons.GetArrayElementAtIndex(i);
                    if (lesson.FindPropertyRelative("stage").intValue != stage || lesson.FindPropertyRelative("wave").intValue != wave) continue;
                    var stock = lesson.FindPropertyRelative("rewards");
                    stock.arraySize = blocks.Length;
                    for (int b = 0; b < blocks.Length; b++)
                    {
                        stock.GetArrayElementAtIndex(b).FindPropertyRelative("kind").intValue = (int)blocks[b].kind;
                        stock.GetArrayElementAtIndex(b).FindPropertyRelative("count").intValue = blocks[b].count;
                    }
                }
            }
            void FreeLesson(int stage, int wave, string topic, string goal) { StageLesson(stage, wave, topic, RobotRole.Warrior, 0); Goal(stage, wave, goal); }
            void Goal(int stage, int wave, string goal)
            {
                for (int i = 0; i < lessons.arraySize; i++)
                {
                    var lesson = lessons.GetArrayElementAtIndex(i);
                    if (lesson.FindPropertyRelative("stage").intValue == stage && lesson.FindPropertyRelative("wave").intValue == wave)
                        lesson.FindPropertyRelative("goal").stringValue = goal;
                }
            }
            void DropLesson(int stage, int wave)
            {
                for (int i = lessons.arraySize - 1; i >= 0; i--)
                {
                    var lesson = lessons.GetArrayElementAtIndex(i);
                    if (lesson.FindPropertyRelative("stage").intValue == stage && lesson.FindPropertyRelative("wave").intValue == wave) lessons.DeleteArrayElementAtIndex(i);
                }
            }
            // 1-1 the swing and the Play button, 1-2 free, 1-3 the loop, 1-4 free with a third warrior, 1-5 the boss; the shooter
            // and variables open at 2-1, conditions at 2-3, the tank at 3-1 (13차 179).
            Lesson(1, "베기와 실행 버튼", 1, (BlockKind.Slash, 1));
            // Grants and rewards give what the robots' code needs and no more (play feedback: eight slash() for three warriors,
            // value blocks with nothing to use them on): one slash() and, from the loop on, one while per warrior, plus 1-2's
            // second swing a call (slash() twice, before loops; that warrior may keep it). A reward is the next wave's need
            // (1-1's slash() for 1-2's warrior, 1-2's slash() and 1-3's while for 1-4's), else nothing.
            Lesson(2, "베기 자유 플레이", 1, (BlockKind.Slash, 1));
            Lesson(3, "반복문", 0, (BlockKind.While, 2));
            Rewards(1, 1, (BlockKind.Slash, 1)); Rewards(1, 2, (BlockKind.Slash, 1)); Rewards(1, 3, (BlockKind.While, 1));
            SetupIntroEncounter(so);
            // Balance (play feedback): from 1-2 a basic enemy takes three of the warrior's swings (17.5 health: about 3 s from
            // the first hit to the kill) and walks at about 2 per second, so one warrior lets some through and every robot the
            // lessons give has work; Stage 2's Heavies are the shooter's (45 health, 1.5 speed: two shots at the edge of its
            // range, one from within about 4.8). Health and speed are written before the stage's scaling, which brings them to
            // those numbers.
            // 1-3: eight basic enemies 1.2 s apart, three swings each: the two warriors' while True loops hold the road.
            var practiceGroup = so.FindProperty("stages").GetArrayElementAtIndex(0).FindPropertyRelative("waves").GetArrayElementAtIndex(2)
                .FindPropertyRelative("groups").GetArrayElementAtIndex(0);
            practiceGroup.FindPropertyRelative("count").intValue = 8;
            practiceGroup.FindPropertyRelative("interval").floatValue = 1.2f;
            // The regular monster with the numbers the tutorial monster had (one monster per kind; F2 tunes it everywhere).
            practiceGroup.FindPropertyRelative("healthOverride").floatValue = 15.2f;
            practiceGroup.FindPropertyRelative("speedOverride").floatValue = 1.81f;
            if (firstThreeOnly)
            {
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(campaign);
                return "campaign: lessons 1-1 to 1-3 only";
            }
            Lesson(4, "반복 자유 플레이", 1);
            Lesson(5, "반복 보스 실습", 0);
            Rewards(1, 4); Rewards(1, 5); Rewards(2, 1); Rewards(2, 2);
            // Stage 2: variables at 2-1 (two shooters), conditions at 2-3 (with the utility robot's slow() to use them on).
            StageLesson(2, 1, "변수: 찾은 적을 상자에 담기(원거리)", RobotRole.Shooter, 2, (BlockKind.While, 2), (BlockKind.Shot, 2),
                (BlockKind.DeclareVariable, 2), (BlockKind.Variable, 2), (BlockKind.NearestEnemy, 2));
            StageLesson(2, 3, "조건문: 쏠 때 고르기", RobotRole.Utility, 1, (BlockKind.If, 2), (BlockKind.Comparison, 2), (BlockKind.Number, 2),
                (BlockKind.MyPos, 2), (BlockKind.DeclareVariable, 2), (BlockKind.Variable, 3), (BlockKind.MyAttackRange, 1), (BlockKind.Slow, 1));
            // Free waves bring nothing new: 2-1 and 2-3 already give two of each block their codes use.
            FreeLesson(2, 2, "변수 자유 플레이", "찾은 적을 변수에 담아 두 원거리형으로 무리 막기");
            FreeLesson(2, 4, "조건 자유 플레이", "적이 가까이 왔을 때 쏘는 조건문으로 무리 막기");
            FreeLesson(2, 5, "자유 플레이", "변수와 조건문을 모두 써서 더 큰 무리 막기");
            FreeLesson(2, 6, "보스", "보스 등장 · 작은 적을 모두 막고 보스를 쓰러뜨리기");
            StageLesson(3, 1, "탱커형 소개", RobotRole.Tank, 1, (BlockKind.While, 1), (BlockKind.Block, 1));
            Goal(3, 1, "탱커형으로 길을 막고 다른 로봇으로 공격하기");
            FreeLesson(3, 2, "자유 플레이", "적이 두 번에 나눠 와요 · 탱커형과 함께 끝까지 막기");
            FreeLesson(3, 3, "자유 플레이", "탱커병 뒤로 적이 줄지어 와요 · 길을 막고 한꺼번에 공격하기");
            FreeLesson(3, 4, "보스", "마지막 보스 · 작은 적을 모두 막고 보스를 쓰러뜨리기");
            DropLesson(3, 5);
            // Stage 2 gains its boss wave (2-6); Stage 3 ends at its boss (3-4, the old last wave with a boss behind it).
            var stages = so.FindProperty("stages");
            EnemyDefinition Data(string name) => AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"Assets/!_Project/Defense/Data/{name}.asset") ??
                throw new InvalidOperationException(name + " enemy asset not found");
            void Groups(int stage, int wave, params (string enemy, int count, float delay, float interval, float health, float speed, int leak, bool boss)[] list)
            {
                var waveList = stages.GetArrayElementAtIndex(stage - 1).FindPropertyRelative("waves");
                if (waveList.arraySize < wave) waveList.arraySize = wave;
                var groups = waveList.GetArrayElementAtIndex(wave - 1).FindPropertyRelative("groups");
                groups.arraySize = list.Length;
                for (int i = 0; i < list.Length; i++)
                {
                    var g = groups.GetArrayElementAtIndex(i); var x = list[i];
                    g.FindPropertyRelative("enemy").objectReferenceValue = Data(x.enemy);
                    g.FindPropertyRelative("route").intValue = 0;
                    g.FindPropertyRelative("count").intValue = x.count;
                    g.FindPropertyRelative("delay").floatValue = x.delay;
                    g.FindPropertyRelative("interval").floatValue = x.interval;
                    g.FindPropertyRelative("healthOverride").floatValue = x.health;
                    g.FindPropertyRelative("speedOverride").floatValue = x.speed;
                    g.FindPropertyRelative("leakDamageOverride").intValue = x.leak;
                    g.FindPropertyRelative("boss").boolValue = x.boss;
                }
            }
            // A boss is a Heavy about four times as tough as the wave's own (base health before the stage's scaling), coming
            // once every minion is down; reaching the core ends the stage, as 1-5's does.
            Groups(1, 2, ("Enemy", 4, 0, 2, 16.7f, 1.95f, 0, false));
            // 2-1 and 2-2: the two shooters alone hold them (2-2's goal; a few get through 2-2), the warriors from Stage 1
            // placed as well make them easy.
            Groups(2, 1, ("Enemy", 6, 0, 2.5f, 15.4f, 1.95f, 0, false), ("Heavy", 2, 6, 4, 26.4f, 1.95f, 0, false));
            Groups(2, 2, ("Enemy", 8, 0, 2, 13.2f, 1.88f, 0, false), ("Heavy", 2, 5, 5, 22.7f, 1.83f, 0, false), ("Runner", 3, 12, 2, 13.2f, 1.84f, 0, false));
            Groups(2, 6, ("Enemy", 6, 0, 1.4f, 0, 0, 0, false), ("Runner", 4, 3, 1.4f, 0, 0, 0, false), ("Heavy", 1, 0, 2, 200, 1.3f, 12, true));
            // No tank-bodied minions before the last boss: the boss comes only once they are all down, and with them it came
            // after Stage 3's 50 s and walked in at the time-up (campaign run, 13차 179).
            Groups(3, 4, ("Runner", 4, 0, 1.3f, 0, 0, 0, false), ("Enemy", 5, 4, 1.2f, 0, 0, 0, false), ("Heavy", 1, 0, 2, 130, 1.1f, 12, true));
            stages.GetArrayElementAtIndex(2).FindPropertyRelative("waves").arraySize = 4;
            var waves = so.FindProperty("stages").GetArrayElementAtIndex(0).FindPropertyRelative("waves");
            var fourth = waves.GetArrayElementAtIndex(3).FindPropertyRelative("groups").GetArrayElementAtIndex(0);
            fourth.FindPropertyRelative("count").intValue = 8;
            fourth.FindPropertyRelative("interval").floatValue = .8f;
            fourth.FindPropertyRelative("healthOverride").floatValue = 13.3f;
            fourth.FindPropertyRelative("speedOverride").floatValue = 1.93f;
            var fifth = waves.GetArrayElementAtIndex(4).FindPropertyRelative("groups");
            var basic = Data("Enemy");
            var minions = fifth.GetArrayElementAtIndex(0);
            minions.FindPropertyRelative("enemy").objectReferenceValue = basic;
            minions.FindPropertyRelative("route").intValue = 1;
            minions.FindPropertyRelative("count").intValue = 3;
            minions.FindPropertyRelative("delay").floatValue = 0;
            minions.FindPropertyRelative("interval").floatValue = 2;
            minions.FindPropertyRelative("healthOverride").floatValue = 14.5f;
            minions.FindPropertyRelative("speedOverride").floatValue = 1.99f;
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
            return "campaign: lessons for 1-1 to 3-4, 1-4 swarm, bosses at 1-5, 2-6 and 3-4";
        }

        static void SetupIntroEncounter(SerializedObject so)
        {
            var stage = so.FindProperty("stages").GetArrayElementAtIndex(0);
            var firstGroups = stage.FindPropertyRelative("waves").GetArrayElementAtIndex(0).FindPropertyRelative("groups");
            firstGroups.arraySize = 3;
            var basicEnemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/!_Project/Defense/Data/Enemy.asset");
            // Two swings (the warrior's 7 each) fell a basic enemy here (14 x 0.9 = 12.6 for wave 1): two Play presses a kill.
            firstGroups.GetArrayElementAtIndex(0).FindPropertyRelative("enemy").objectReferenceValue = basicEnemy;
            firstGroups.GetArrayElementAtIndex(0).FindPropertyRelative("healthOverride").floatValue = 14;
            firstGroups.GetArrayElementAtIndex(0).FindPropertyRelative("speedOverride").floatValue = 1.4f;
            var tankEnemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/!_Project/Defense/Data/Heavy.asset");
            if (!basicEnemy || !tankEnemy) throw new InvalidOperationException("1-1의 기본·단단한 적 asset이 없습니다.");
            // While 디버 slows, pauses and resumes the battle (E04 to E04c) one more walks in from 4s, so there is something to
            // watch (play feedback 4); it was a bunch of four, cut to one so basic enemies come one at a time (13차 175).
            var crowd = firstGroups.GetArrayElementAtIndex(1);
            crowd.FindPropertyRelative("enemy").objectReferenceValue = basicEnemy;
            crowd.FindPropertyRelative("route").intValue = 0;
            crowd.FindPropertyRelative("count").intValue = 1;
            crowd.FindPropertyRelative("delay").floatValue = 4;
            crowd.FindPropertyRelative("interval").floatValue = .6f;
            crowd.FindPropertyRelative("healthOverride").floatValue = 14;
            crowd.FindPropertyRelative("speedOverride").floatValue = 1.4f;
            crowd.FindPropertyRelative("leakDamageOverride").intValue = 0;
            crowd.FindPropertyRelative("boss").boolValue = false;
            // The one that reaches the core has the tank's body (play feedback 4: it looked like the others); the tank rule
            // slows it to 0.75x, so it leaks about four seconds later than the basic body did.
            var strongGroup = firstGroups.GetArrayElementAtIndex(2);
            strongGroup.FindPropertyRelative("enemy").objectReferenceValue = tankEnemy;
            strongGroup.FindPropertyRelative("route").intValue = 0;
            strongGroup.FindPropertyRelative("count").intValue = 1;
            strongGroup.FindPropertyRelative("delay").floatValue = 12;
            strongGroup.FindPropertyRelative("interval").floatValue = 2;
            strongGroup.FindPropertyRelative("healthOverride").floatValue = 500;
            strongGroup.FindPropertyRelative("speedOverride").floatValue = 1.6f;
            stage.FindPropertyRelative("waveSeconds").floatValue = 120;
        }

        public static string Run()
        {
            var log = new System.Text.StringBuilder();
            lines = ParseScript(Path.Combine(Directory.GetCurrentDirectory(), Script));
            log.AppendLine("lines " + lines.Count);
            var sequences = new[] { Write("1-1", 1, OneOne()), Write("1-2", 2, OneTwo()), Write("1-3", 3, OneThree()), Write("1-4", 4, OneFour()), Write("1-5", 5, OneFive()),
                Write("2-1", 1, TwoOne(), 2), Write("2-3", 3, TwoThree(), 2), Write("3-1", 1, ThreeOne(), 3) };
            foreach (var s in sequences) log.AppendLine(s.sequenceId + ": " + s.steps.Length + " steps");
            log.AppendLine(Campaign());
            AssetDatabase.SaveAssets();
            var director = UnityEngine.Object.FindObjectsByType<TutorialDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var so = new SerializedObject(director);
            var list = so.FindProperty("sequences");
            // Only these: the old hand-made intros (Stages23 Intro_2-4 if/elif/else, Intro_3-1 for and boom(), Intro_3-4 while True)
            // taught what the new layout no longer does there (13차 179).
            var wired = sequences;
            list.arraySize = wired.Length;
            for (int i = 0; i < wired.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = wired[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            log.AppendLine("scene saved " + EditorSceneManager.SaveScene(director.gameObject.scene));
            return log.ToString();
        }
    }
}
