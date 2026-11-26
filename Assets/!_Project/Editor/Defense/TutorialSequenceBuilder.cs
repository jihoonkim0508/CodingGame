// Builds the tutorial sequences from Docs/flow_stage1/tutorial_stage1_대본.md, applies the campaign's waves and rewards from GameData/,
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
            Campaign();
            AssetDatabase.SaveAssets();
            Debug.Log("Beginner tutorial 1-1 applied: " + sequence.steps.Length + " steps and the campaign from GameData");
        }

        const string Folder = "Assets/!_Project/Defense/Tutorial/Stage1";
        public static void ApplyFirstThree()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 적용하세요.");
            lines = ParseScript(Path.Combine(Directory.GetCurrentDirectory(), Script));
            var updated = new[] { Write("1-1", 1, OneOne()), Write("1-2", 2, OneTwo()), Write("1-3", 3, OneThree()) };
            Campaign();
            AssetDatabase.SaveAssets();
            Debug.Log("Beginner lessons applied: " + string.Join(", ", updated.Select(s => s.sequenceId + "=" + s.steps.Length)) + " (campaign from GameData)");
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
        // 2-1: the found enemy kept in a variable; 2-3: the warrior slashes only once the nearest enemy is within its range,
        // instead of slashing air and resting 1.6 s while an enemy walks in.
        internal const string Code21 = "while True:\n    enemy = get_nearest_enemy()\n    shoot(enemy)";
        internal const string Code23 = "while True:\n    enemy = get_nearest_enemy()\n    if enemy.distance(get_my_pos()) <= my_attack_range():\n        slash()";
        // 2-5: the enemy kept until is_dead() says it fell; 2-6: a loop in the loop that break leaves once its enemy is out of
        // range; 2-7's hint also leaves it once the enemy is down; 2-8: continue skips every nearest enemy that is not a tank.
        internal const string Code25 = "enemy = get_nearest_enemy()\nwhile True:\n    if enemy.is_dead():\n        enemy = get_nearest_enemy()\n    shoot(enemy)";
        internal const string Code26 = "while True:\n    enemy = get_nearest_enemy()\n    while True:\n        if enemy.distance(get_my_pos()) > my_attack_range():\n            break\n        shoot(enemy)";
        internal const string Code27 = "while True:\n    enemy = get_nearest_enemy()\n    while True:\n        if enemy.is_dead():\n            break\n        if enemy.distance(get_my_pos()) > my_attack_range():\n            break\n        shoot(enemy)";
        internal const string Code28 = "while True:\n    enemy = get_nearest_enemy()\n    if enemy.type != Enemy.Tank:\n        continue\n    while True:\n        if enemy.is_dead():\n            break\n        if enemy.distance(get_my_pos()) > my_attack_range():\n            break\n        shoot(enemy)";
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
            public bool Dim, ClearProgram, OpenEditor, Preview, Battle;
            public int Robot;
            public float Padding = 10;
            public string[] Highlight = Array.Empty<string>();
            public bool Intro, Cards, Undimmed, Dismissable, Round, Rush;
            public float Auto;
            public string SkipIf;
            public string Demo;
            public bool Top;
            public float Hold, TriggerDelay;
            public bool DistanceExample, BaselineExample, RestartPreview, PlaceShooter, PlaceWarrior;
            public string Example;
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
        // The player closes the editor with its 닫기 (no step closes it: the user, 2026-11-19); the code is applied as it is built.
        static Action<S> CloseClick => Click(Close, s => { s.Pointer = Close; s.Preview = true; });
        static Action<S> Fight => s => s.Battle = true;
        // 디버's entrance lines: the HUD dims with the greyed map, so the dialogue is the one bright thing (focus report 1-3).
        static Action<S> Intro => s => { s.Intro = true; s.Dim = true; };
        static List<S> OneOne() => new List<S> {
            L("1-1.A01", s => s.Cards = true), L("1-1.A02", Intro), L("1-1.A03", Intro), L("1-1.A04a", Focus(Route)), L("1-1.A04b", Focus(Core)), L("1-1.A05"),
            L("1-1.B01", Click(TutorialKeys.RobotButton(1), s => s.Dim = true)),
            L("1-1.B02", Signal(TutorialSignals.RobotPlaced, new[] { TutorialKeys.Deployment }, With(s => { s.Dim = true; s.Round = true; }, Focus(TutorialKeys.Floor)))),
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
                s => { s.Preview = true; s.RestartPreview = true; s.Hold = 3; }, Focus(Preview)))),
            // "베었다!" over the preview: it plays the swing again (slash() runs once per demo and stood still for this line).
            L("1-1.D09", With(Ed, With(s => { s.Preview = true; s.RestartPreview = true; }, Focus(Preview)))),
            // Before the first workspace closes: the bottom line is where the code's problems show (debugging).
            L("1-1.D09a", With(Ed, Focus(TutorialKeys.Status))),
            L("1-1.D09b", CloseClick),
            L("1-1.D10"),
            L("1-1.E01", Focus(TutorialKeys.NextWave)),
            L("1-1.E03", Signal(TutorialSignals.BattleStarted, new[] { Start }, s => s.Top = true)),
            // Battle start called the function once and it ended (a swing, likely at nobody); then the first enemy in range is
            // the moment for the robot's Play button, which does nothing before (TutorialDirector.CanPerformAction).
            // The swing-paced lines (E03a, E03c, E04) come when the robot's swing has played out, so they follow its attack
            // speed instead of fixed delays (the user: '공속 가져와서, 하드코딩하지 말고').
            L("1-1.E03a", s => s.Trigger = TutorialSignals.FunctionEnded),
            L("1-1.E03b", Click(TutorialKeys.Play(0), s => { s.Trigger = TutorialSignals.EnemyInRange; s.Pointer = TutorialKeys.Play(0); s.Top = true; })),
            // A basic enemy takes two swings (the warrior's 7 against 12.6): once the pressed call has ended and its swing has
            // landed and played out, at normal speed (FunctionEnded came at the battle start's call), the second press.
            L("1-1.E03c", Click(TutorialKeys.Play(0), s => { s.Trigger = TutorialSignals.PlayEnded; s.Pointer = TutorialKeys.Play(0); s.Top = true; })),
            // After the kill the battle runs on at 1x until the killing swing is over, then stands still under the line.
            L("1-1.E04", s => s.Trigger = TutorialSignals.KillSwingEnded),
            // Pause and speed are only told about in 1-2 (the user: '1-1 보상 이후로, 클릭하지 말고 말로만').
            L("1-1.E04d"),
            // A basic enemy at the core first: only that the core lost health (once); the tank's arrival, first or later,
            // the line about the tough one. Meanwhile the tank, once past the warrior, walks the rest fast (rushTank).
            L("1-1.E05a", With(Fight, Focus(Core), s => { s.Trigger = TutorialSignals.Leak; s.SkipIf = TutorialSignals.LeakTank; s.Rush = true; })),
            L("1-1.E05", With(Fight, Focus(Core), s => { s.Trigger = TutorialSignals.LeakTank; s.Rush = true; })),
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
                // The robot's 레벨업 and 회수, told about first (no press: 회수 would take the warrior away); both open from 1-2.
                L("1-2.A00", Click(Unit0, s => s.Pointer = Unit0)),
                L("1-2.A00a", With(Focus("Battle.Upgrade"), s => s.Pointer = "Battle.Upgrade")),
                L("1-2.A00b", With(Focus("Battle.Recall"), s => s.Pointer = "Battle.Recall")),
                L("1-2.A01"), L("1-2.A02"), L("1-2.A03"),
                // The battle's controls show from its start: told about there, under a held battle, no click asked (both work
                // from 1-2 on, DefenseBattle.SpeedUnlocked).
                // A04 waits, hidden, through the free setup: the hint (slash()) is there already.
                L("1-2.A04", s => { s.Trigger = TutorialSignals.BattleStarted; s.Focus = new[] { TutorialKeys.Pause }; s.HintCode = Code11; }),
                L("1-2.A05", With(Focus(TutorialKeys.Speed), s => s.HintCode = Code11)),
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
            L("1-3.D01a", CloseClick),
            L("1-3.D02"),
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
                    // A ':Kind' after the slot says what must be there (2-3 C07: get_my_pos()); a demo needs a palette block to drag.
                    string slot = !pointer.StartsWith(TutorialKeys.PalettePrefix, StringComparison.Ordinal) ? null :
                        signal == TutorialSignals.SlotFilled ? expected.Split(':')[0] :
                        focus.LastOrDefault(key => key.StartsWith(TutorialKeys.CodePrefix, StringComparison.Ordinal));
                    if (!string.IsNullOrEmpty(slot)) s.Demo = pointer + ">" + slot;
                }
            }, Focus(focus)));

        // 2-1 (play feedback 4: variables open in Stage 2): a shooter stood by the road; while True, then enemy = get_nearest_enemy()
        // inside it, then shoot(enemy) under that.
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
            L("2-1.C02a", CloseClick),
            // 1-5's clear brought a warrior (GameData/Award.md): the lesson stands it by the road after the shooter (2-1's balance
            // assumes that spot, and installation order keeps the shooter Unit 0 and the warrior the newest for 2-3), then the
            // player gives it 1-3's loop (the stand-in code PlaceLessonRobot gives is cleared as the arrow points at it).
            L("2-1.C03", s => s.PlaceWarrior = true),
            L("2-1.C04", Click(Newest, s => { s.Pointer = Newest; s.Robot = -1; s.ClearProgram = true; })),
            L("2-1.C05", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; s.Robot = -1; })),
            L("2-1.C06", With(EditSignal(TutorialSignals.CodeLinePlaced, "1:While", "Editor.Palette.While", "Editor.Palette.While", "Editor.Code.Start"), s => s.Robot = -1)),
            L("2-1.C07", Code(Code13, With(
                s => { s.Pointer = "Editor.Palette.Slash"; s.AutoSlot = "Editor.Code.Body.1"; s.Demo = "Editor.Palette.Slash>Editor.Code.Body.1"; s.Robot = -1; },
                Focus("Editor.Palette.Slash", "Editor.Code.Body.1")))),
            L("2-1.C08", CloseClick),
            L("2-1.D01"),
            L("2-1.D02"),
            L("2-1.E01", s => { s.Trigger = TutorialSignals.WaveCleared; s.HintCode = Code21; s.Battle = true; }),
            L("2-1.E02", Click("Battle.ClaimRewards", s => { s.Pointer = "Battle.ClaimRewards"; s.Top = true; })),
        };

        // 2-3 (게임 논리 검토, 3e52879): the warrior 2-1 stood by the road (the newest robot, Robot.Unit.-1; no warrior is left in
        // stock since 1-5's clear brings it) first shows slash() on and on, resting after each swing at air; then its loop finds
        // the nearest enemy and the player builds the if on its distance round slash().
        static readonly string Newest = TutorialKeys.Unit(-1);
        static List<S> TwoThree() => new List<S> {
            L("2-3.A01"),
            L("2-3.A03", Click(Newest, s => s.Pointer = Newest)),
            L("2-3.A04", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; })),
            // Held past the first swing until the enemy has walked through while the warrior rests.
            L("2-3.A05", Signal(TutorialSignals.PreviewHit, new[] { Preview }, With(
                s => { s.BaselineExample = true; s.Preview = true; s.RestartPreview = true; s.Hold = 6; }, Focus(Preview)))),
            L("2-3.B01", With(Ed, Focus(Preview))),
            L("2-3.B02", With(Ed, s => s.DistanceExample = true, Focus("Editor.Code.Line.1/1"))),
            L("2-3.B04"), L("2-3.B05"),
            // The if becomes the loop's second line ("1/2"), a click adds it at the body's end.
            L("2-3.C01", With(EditSignal(TutorialSignals.CodeLinePlaced, "1/2:If", "Editor.Palette.If", "Editor.Palette.If", "Editor.Code.After.1/1"),
                s => { s.AutoSlot = "Editor.Code.Body.1"; s.Demo = "Editor.Palette.If>Editor.Code.After.1/1"; })),
            L("2-3.C03", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg.1/2.0", "Editor.Palette.Comparison", "Editor.Palette.Comparison", "Editor.Code.Arg.1/2.0"), s => s.AutoSlot = "Editor.Code.Arg.1/2.0")),
            L("2-3.C04", EditSignal(TutorialSignals.DraftValueMatches, "1/2.0|<=", "Editor.Code.Option.1/2.0", "Editor.Code.Option.1/2.0")),
            // enemy.distance() is the variable block named enemy. with distance( chosen after the dot (no block of its own).
            // `enemy.` typed up to the dot, then distance( from the picker the dot opens: two steps (the user, 2026-11-19: players
            // typed ".distance" on and skipped the step; the field now takes nothing after the dot).
            L("2-3.C06", EnemyIn("1/2.0.0", "enemy.")),
            L("2-3.C06a", Member("1/2.0.0", nameof(BlockKind.Distance))),
            L("2-3.C07", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg.1/2.0.0.0:MyPos", "Editor.Palette.MyPos", "Editor.Palette.MyPos", "Editor.Code.Arg.1/2.0.0.0"), s => s.AutoSlot = "Editor.Code.Arg.1/2.0.0.0")),
            L("2-3.C09", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg.1/2.0.1:MyAttackRange", "Editor.Palette.MyAttackRange", "Editor.Palette.MyAttackRange", "Editor.Code.Arg.1/2.0.1"), s => s.AutoSlot = "Editor.Code.Arg.1/2.0.1")),
            L("2-3.C10", With(Ed, Focus("Editor.Code.Arg.1/2.0"))),
            L("2-3.D02", With(EditSignal(TutorialSignals.ProgramMatches, Code23, "Editor.Palette.Slash", "Editor.Palette.Slash", "Editor.Code.Body.1/2"),
                s => { s.AutoSlot = "Editor.Code.Body.1/2"; s.Demo = "Editor.Palette.Slash>Editor.Code.Body.1/2"; })),
            L("2-3.D06", Signal(TutorialSignals.PreviewHit, new[] { Preview }, With(s => { s.Preview = true; s.RestartPreview = true; s.Hold = 3; }, Focus(Preview)))),
            L("2-3.D07", With(Ed, Focus("Editor.Code.Body.1/2"))),
            L("2-3.D07a", CloseClick),
            L("2-3.E01"),
            L("2-3.E02", Signal(TutorialSignals.BattleStarted, new[] { Start }, s => s.Top = true)),
            // A beat after the warrior's first swing, so the player sees it before '벴다!'.
            L("2-3.E03", With(Fight, s => { s.Trigger = TutorialSignals.FirstAttack; s.TriggerDelay = 1.2f; })),
            L("2-3.E04", s => { s.Trigger = TutorialSignals.WaveCleared; s.HintCode = Code23; }),
            L("2-3.E06", Click("Battle.ClaimRewards", s => { s.Pointer = "Battle.ClaimRewards"; s.Top = true; })),
        }.Select(s => { s.Robot = -1; return s; }).ToList();

        static Action<S> Claim => Click("Battle.ClaimRewards", s => { s.Pointer = "Battle.ClaimRewards"; s.Top = true; });
        // 닫기, the start button, then the wave (2-5, 2-6, 2-8 E00 to E03).
        static S[] Battle(string wave, string hint) => new[] {
            L(wave + ".E00", CloseClick),
            L(wave + ".E01", Signal(TutorialSignals.BattleStarted, new[] { Start }, s => { s.Top = true; s.HintCode = hint; })),
            L(wave + ".E02", With(Fight, s => { s.Trigger = TutorialSignals.WaveCleared; s.HintCode = hint; })),
            L(wave + ".E03", Claim) };
        // A variable block put in the slot and named, said in one line: enemy, or `enemy.` up to the dot that opens its member picker.
        static Action<S> EnemyIn(string path, string name = "enemy") => With(EditSignal(TutorialSignals.DraftValueMatches, path + "|" + name,
            "Editor.Code.Name." + path + "|Editor.Palette.Variable", "Editor.Palette.Variable", "Editor.Code.Arg." + path),
            s => { s.AutoSlot = "Editor.Code.Arg." + path; s.Demo = "Editor.Palette.Variable>Editor.Code.Arg." + path; });
        // The member chosen from the picker `enemy.` opened (distance(, is_dead(), type), the picker in the spotlight.
        static Action<S> Member(string path, string kind) => EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg." + path + ":" + kind,
            "Editor.Code.Option." + path, "Editor.Code.Option." + path);

        // 2-5 (원거리_집중공격_설계 4-5): the enemy found once before the loop is shot until it falls, then the shooter stands
        // still; the player adds the if that finds a new one once enemy.is_dead().
        static List<S> TwoFive() => new List<S> {
            L("2-5.A01", s => s.Cards = true), L("2-5.A02"),
            L("2-5.A03", Click(Unit0, s => s.Pointer = Unit0)),
            L("2-5.A04", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; })),
            // Held past the first hit until the first enemy is down (two shots, 2.4 s apart) and the shooter has stood still.
            L("2-5.A05", Signal(TutorialSignals.PreviewHit, new[] { Preview }, With(
                s => { s.Example = "2-5"; s.RestartPreview = true; s.Preview = true; s.Hold = 10; }, Focus("Editor.Code.Line.1", Preview)))),
            L("2-5.B01", With(Ed, Focus("Editor.Code.Line.1", Preview))), L("2-5.B02", With(Ed, Focus("Editor.Code.Line.1"))),
            // In front of shoot(enemy), the loop's first line: dragged there, or clicked (a click goes where the demo drops it).
            L("2-5.C01", With(EditSignal(TutorialSignals.CodeLinePlaced, "2/1:If", "Editor.Palette.If", "Editor.Palette.If", "Editor.Code.Before.2/1"),
                s => s.Demo = "Editor.Palette.If>Editor.Code.Before.2/1")),
            L("2-5.C02", EnemyIn("2/1.0", "enemy.")),
            L("2-5.C03", Member("2/1.0", nameof(BlockKind.IsDead))),
            L("2-5.C04", With(Ed, Focus("Editor.Code.Arg.2/1.0"))),
            L("2-5.D01", With(EditSignal(TutorialSignals.CodeLinePlaced, "2/1/1:DeclareVariable", "Editor.Palette.DeclareVariable", "Editor.Palette.DeclareVariable", "Editor.Code.Body.2/1"),
                s => s.AutoSlot = "Editor.Code.Body.2/1")),
            L("2-5.D02", With(EditSignal(TutorialSignals.ProgramMatches, Code25, "Editor.Palette.NearestEnemy", "Editor.Palette.NearestEnemy", "Editor.Code.Arg.2/1/1.0"),
                s => { s.AutoSlot = "Editor.Code.Arg.2/1/1.0"; s.Demo = "Editor.Palette.NearestEnemy>Editor.Code.Arg.2/1/1.0"; })),
            L("2-5.D03", Signal(TutorialSignals.PreviewHit, new[] { Preview }, With(s => { s.Preview = true; s.RestartPreview = true; s.Hold = 10; }, Focus(Preview)))),
            L("2-5.D04", With(Ed, Focus("Editor.Code.Line.2/1"))),
        }.Concat(Battle("2-5", Code25)).ToList();

        // 2-6 (게임 논리 검토): the loop in the loop keeps shooting its enemy after it ran out of range; break leaves the inner
        // loop then, and the outer one finds the nearest again. Then a second shooter gets the code by copy and paste.
        static List<S> TwoSix() => new List<S> {
            L("2-6.A01", s => s.Cards = true), L("2-6.A02"),
            L("2-6.A03", Click(Unit0, s => s.Pointer = Unit0)),
            L("2-6.A04", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; })),
            // No line of its own for the demo: B01's '봤지?' waits on the preview until the second fast one has walked past.
            L("2-6.B01", Signal(TutorialSignals.PreviewHit, new[] { Preview }, With(
                s => { s.Example = "2-6"; s.RestartPreview = true; s.Preview = true; s.Hold = 11; }, Focus("Editor.Code.Line.1/2", Preview)))),
            L("2-6.B02", With(Ed, Focus("Editor.Code.Line.1/2"))), L("2-6.B03", With(Ed, Focus("Editor.Code.Line.1/2"))),
            L("2-6.C01", With(EditSignal(TutorialSignals.CodeLinePlaced, "1/2/1:If", "Editor.Palette.If", "Editor.Palette.If", "Editor.Code.Before.1/2/1"),
                s => s.Demo = "Editor.Palette.If>Editor.Code.Before.1/2/1")),
            L("2-6.C02", With(EditSignal(TutorialSignals.DraftValueMatches, "1/2/1.0|>", "Editor.Code.Option.1/2/1.0|Editor.Palette.Comparison", "Editor.Palette.Comparison", "Editor.Code.Arg.1/2/1.0"),
                s => { s.AutoSlot = "Editor.Code.Arg.1/2/1.0"; s.Demo = "Editor.Palette.Comparison>Editor.Code.Arg.1/2/1.0"; })),
            // `enemy.` and distance( from its picker, then get_my_pos() in its brackets and my_attack_range() on the right.
            L("2-6.C03", EnemyIn("1/2/1.0.0", "enemy.")),
            L("2-6.C03a", Member("1/2/1.0.0", nameof(BlockKind.Distance))),
            L("2-6.C03b", Signal(TutorialSignals.ConditionBuilt, new[] { Drop, Palette }, With(s => {
                s.Expected = "enemy.distance(get_my_pos()) > my_attack_range()"; s.AutoSlot = "Editor.Code.Arg.1/2/1.0.0";
                // get_my_pos() first, then my_attack_range(): the arrow and the drag demo move on once the bracket is filled.
                s.Pointer = "Editor.Palette.MyPos|Editor.Palette.MyAttackRange";
                s.Demo = "Editor.Palette.MyPos>Editor.Code.Arg.1/2/1.0.0.0|Editor.Palette.MyAttackRange>Editor.Code.Arg.1/2/1.0.1"; },
                Focus("Editor.Code.Arg.1/2/1.0.0", "Editor.Code.Arg.1/2/1.0.1")))),
            L("2-6.C04", With(EditSignal(TutorialSignals.ProgramMatches, Code26, "Editor.Palette.Break", "Editor.Palette.Break", "Editor.Code.Body.1/2/1"),
                s => { s.AutoSlot = "Editor.Code.Body.1/2/1"; s.Demo = "Editor.Palette.Break>Editor.Code.Body.1/2/1"; })),
            L("2-6.D01", Signal(TutorialSignals.PreviewHit, new[] { Preview }, With(s => { s.Preview = true; s.RestartPreview = true; s.Hold = 10; }, Focus(Preview)))),
            L("2-6.D02", With(Ed, Focus("Editor.Code.Body.1/2/1"))),
            // The second shooter gets this code by copy and paste (the user, 2026-11-19: the right-click 복사 where a second robot
            // needs the first one's code). The right-click on the top line, while True, copies the loop with every line in it.
            L("2-6.F01", With(Ed, Focus("Editor.Code.Line.1"))),
            // The arrow and the outline on while True's own row (Editor.Code.Head.1): round the whole loop they left the line no
            // place clear of the row to right-click (2-8 F01, the longer code, put the line over it).
            L("2-6.F02", Signal(TutorialSignals.CodeCopied, new[] { Drop }, With(s => { s.Pointer = "Editor.Code.Head.1"; s.Preview = true; }, Focus("Editor.Code.Head.1")))),
            L("2-6.F02a", CloseClick),
            L("2-6.F03", Click(TutorialKeys.RobotButton((int)RobotRole.Ranged), s => s.Dim = true)),
            // Beside the road, so no road lit (play test 2026-11-20: the lit road drew the click onto it, where only a tank may
            // stand) and no dim: the whole map shows, and the placement ring greys over the road.
            L("2-6.F04", Signal(TutorialSignals.RobotPlaced, new[] { TutorialKeys.Deployment }, s => s.Undimmed = true)),
            L("2-6.F05", Click(Newest, s => { s.Pointer = Newest; s.Robot = -1; })),
            L("2-6.F06", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; s.Robot = -1; })),
            // Pasted where it was clicked; near the start slot it snaps there.
            L("2-6.F07", Signal(TutorialSignals.ProgramMatches, new[] { Drop }, With(s => { s.Expected = Code26; s.Pointer = "Editor.Code.Start"; s.Preview = true; s.Robot = -1; },
                Focus("Editor.Code.Start")))),
            L("2-6.F08", With(Ed, s => s.Robot = -1, Focus(Palette))),
        }.Concat(Battle("2-6", Code26)).ToList();

        // 2-8: 2-7's hint, then an if under enemy = get_nearest_enemy() that continues while the nearest enemy is no tank; a
        // warrior stands by the road from the start for the small ones.
        static List<S> TwoEight() => new List<S> {
            L("2-8.A01", s => { s.Cards = true; s.PlaceWarrior = true; }), L("2-8.A02"),
            L("2-8.A03", Click(Unit0, s => s.Pointer = Unit0)),
            L("2-8.A04", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; })),
            L("2-8.B01", With(Ed, s => s.Example = "2-8", Focus("Editor.Code.Line.1/1"))),
            L("2-8.B02", With(Ed, Focus("Editor.Code.Line.1/2"))), L("2-8.B03", With(Ed, Focus("Editor.Code.Line.1"))),
            L("2-8.C01", With(EditSignal(TutorialSignals.CodeLinePlaced, "1/2:If", "Editor.Palette.If", "Editor.Palette.If", "Editor.Code.After.1/1"),
                s => s.Demo = "Editor.Palette.If>Editor.Code.After.1/1")),
            // The comparison put in the if and its sign turned to != (one line: the block, then its picker).
            L("2-8.C02", With(EditSignal(TutorialSignals.DraftValueMatches, "1/2.0|!=", "Editor.Code.Option.1/2.0|Editor.Palette.Comparison", "Editor.Palette.Comparison", "Editor.Code.Arg.1/2.0"),
                s => { s.AutoSlot = "Editor.Code.Arg.1/2.0"; s.Demo = "Editor.Palette.Comparison>Editor.Code.Arg.1/2.0"; })),
            L("2-8.C03", EnemyIn("1/2.0.0", "enemy.")),
            L("2-8.C03a", Member("1/2.0.0", nameof(BlockKind.EnemyTypeOf))),
            L("2-8.C04", With(EditSignal(TutorialSignals.DraftValueMatches, "1/2.0.1|Tank", "Editor.Code.Option.1/2.0.1|Editor.Palette.EnemyTypeName", "Editor.Palette.EnemyTypeName", "Editor.Code.Arg.1/2.0.1"),
                s => { s.AutoSlot = "Editor.Code.Arg.1/2.0.1"; s.Demo = "Editor.Palette.EnemyTypeName>Editor.Code.Arg.1/2.0.1"; })),
            L("2-8.C05", With(EditSignal(TutorialSignals.ProgramMatches, Code28, "Editor.Palette.Continue", "Editor.Palette.Continue", "Editor.Code.Body.1/2"),
                s => { s.AutoSlot = "Editor.Code.Body.1/2"; s.Demo = "Editor.Palette.Continue>Editor.Code.Body.1/2"; })),
            L("2-8.D01", Signal(TutorialSignals.PreviewHit, new[] { Preview }, With(s => { s.Preview = true; s.RestartPreview = true; s.Hold = 10; }, Focus(Preview)))),
            L("2-8.D02", With(Ed, Focus("Editor.Code.Line.1/2"))),
            // The second shooter (2-6's, the newest robot) still shoots the small ones with 2-6's or 2-7's code: it gets this one
            // by copy and paste as in 2-6, its old code cleared first (2-7's clear holds a second set of the new blocks).
            L("2-8.F01", Signal(TutorialSignals.CodeCopied, new[] { Drop }, With(s => { s.Pointer = "Editor.Code.Head.1"; s.Preview = true; }, Focus("Editor.Code.Head.1")))),
            L("2-8.F02", CloseClick),
            L("2-8.F03", Click(Newest, s => { s.Pointer = Newest; s.Robot = -1; })),
            L("2-8.F04", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; s.Robot = -1; })),
            L("2-8.F05", With(Ed, s => { s.ClearProgram = true; s.Robot = -1; }, Focus(Drop))),
            L("2-8.F06", Signal(TutorialSignals.ProgramMatches, new[] { Drop }, With(s => { s.Expected = Code28; s.Pointer = "Editor.Code.Start"; s.Preview = true; s.Robot = -1; },
                Focus("Editor.Code.Start")))),
            L("2-8.F07", With(Ed, s => s.Robot = -1, Focus("Editor.Code.Line.1/2"))),
        }.Concat(Battle("2-8", Code28)).ToList();

        // 2-2, 2-4, 2-7, 2-9, 2-10: lines before free play (as 1-4, 1-5), the hint holding the code to build on. 2-2 had no line
        // and 2-4 none either (play test 2026-11-20: '뭘 하라는 거지?'); each says what differs from the wave before and what
        // to do, and the goal bar, where every wave's task is written, is lit on the line that says it.
        static List<S> TwoTwo() => new List<S> {
            L("2-2.A01", s => s.Cards = true), L("2-2.A02"), L("2-2.A03"), L("2-2.A04", Focus(TutorialKeys.Goal)),
            L("2-2.B01", s => { s.Trigger = TutorialSignals.WaveCleared; s.HintCode = Code21; s.Battle = true; }),
            L("2-2.B02", Claim),
        };
        static List<S> TwoFour() => new List<S> {
            L("2-4.A01", s => s.Cards = true), L("2-4.A02"), L("2-4.A03", Focus(TutorialKeys.Goal)), L("2-4.A04"),
            L("2-4.B01", s => { s.Trigger = TutorialSignals.WaveCleared; s.HintCode = Code23; s.Battle = true; }),
            L("2-4.B02", Claim),
        };
        // 2-7: the one new thing is the is_dead() break (2-6's code lets a fallen enemy go already: its distance reads as
        // infinite), so the lines and the goal name that line and where it goes, not '두 가지를 합쳐' (play test 2026-11-20).
        static List<S> TwoSeven() => new List<S> {
            L("2-7.A01", s => s.Cards = true), L("2-7.A02"), L("2-7.A03"), L("2-7.A04", Focus(TutorialKeys.Goal)),
            L("2-7.B01", s => { s.Trigger = TutorialSignals.WaveCleared; s.HintCode = Code27; s.Battle = true; }),
            L("2-7.B02", Claim),
        };
        static List<S> TwoNine() => new List<S> {
            L("2-9.A01", s => s.Cards = true), L("2-9.A02"), L("2-9.A03"), L("2-9.A04"),
            L("2-9.B01", s => { s.Trigger = TutorialSignals.WaveCleared; s.HintCode = Code28; s.Battle = true; }),
            L("2-9.B02"), L("2-9.B03", Claim),
        };
        static List<S> TwoTen() => new List<S> {
            L("2-10.A01", s => s.Cards = true), L("2-10.A02"), L("2-10.A03"), L("2-10.A04"),
            L("2-10.B01", s => { s.Trigger = TutorialSignals.WaveCleared; s.Battle = true; }),
            L("2-10.B02"), L("2-10.B03", Claim),
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
            L("3-1.B01", Click(TutorialKeys.RobotButton((int)RobotRole.Melee), s => s.Dim = true)),
            L("3-1.B02", Signal(TutorialSignals.RobotPlaced, new[] { TutorialKeys.Deployment }, With(s => s.Dim = true, Focus(Route)))),
            L("3-1.B03"), L("3-1.B04"),
            // The praise only for a wave held without a leak; otherwise E01a (shown on a leak) says what to change.
            L("3-1.E01", s => { s.Trigger = TutorialSignals.WaveCleared; s.HintCode = Code31; s.Battle = true; s.SkipIf = TutorialSignals.Leak; }),
            L("3-1.E01a", s => s.Trigger = TutorialSignals.Leak),
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
            "enemy.is_dead()", "is_dead()", "enemy.type", "Enemy.Tank", "Enemy.", "Tank", "type", "break", "continue",
            "enemy_distance", "while True", "slash()", "block()", "shoot()", ".hp", "enemy", "while", "if", "<=", "!=" }.Select(Regex.Escape)) + @")(?![A-Za-z0-9_(`])");

        static Dictionary<string, List<Line>> ParseScript(string path)
        {
            var result = new Dictionary<string, List<Line>>();
            var pattern = new Regex(@"^\[(\d-\d+\.[A-Z]\d\d[a-z]?)\] (?:\((?:ui|UI)로 표시\)\s*)?(\S+) : (.*)$");
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
            sequence.steps = steps.Select(s =>
            {
                return new TutorialStep {
                    stepId = s.Id, kind = s.Kind, dialoguePosition = s.Top ? TutorialDialoguePosition.Top : TutorialDialoguePosition.Bottom,
                    texts = s.Lines.Select((line, i) => new TutorialText { textId = "t" + (i + 1), speaker = line.Speaker, text = line.Text, focusKeys = s.Focus, highlightKeys = s.Highlight }).ToArray(),
                    targetKeys = s.Targets, dim = s.Dim, blockInput = true, outline = true, roundFocus = s.Round, rushTank = s.Rush,
                    completionMode = s.Kind == TutorialStepKind.ActionGuide ? s.Mode : TutorialCompletionMode.Unconfigured, completionSignal = s.Signal,
                    requiredSlotBlock = s.RequiredSlotBlock,
                    // A line on screen holds the battle, a hidden step's wait lets it run (the user: '대사 나올 때에는 0.2배속이 아닌 일시정지').
                    skipIf = s.SkipIf, pauseBattle = true, slowBattle = false,
                    allowPreview = s.Preview || id == "1-1" &&
                        ((s.Id.StartsWith("C", StringComparison.Ordinal) || s.Id.StartsWith("D", StringComparison.Ordinal)) && s.Id != "D10"),
                    highlightPadding = s.Padding, trigger = s.Trigger, expectedCode = s.Expected,
                    pointerKey = s.Pointer, autoSelectSlot = s.AutoSlot, hintCode = s.HintCode, pointerDelay = s.PointerDelay,
                    closeEditor = false, clearProgram = s.ClearProgram, openEditor = s.OpenEditor, robotIndex = s.Robot,
                    prepareDistanceExample = s.DistanceExample, prepareBaselineExample = s.BaselineExample, prepareExample = s.Example, restartPreview = s.RestartPreview, placeShooter = s.PlaceShooter, placeWarrior = s.PlaceWarrior,
                    introEntrance = s.Intro, undimmed = s.Undimmed, alertCards = s.Cards, autoAdvance = s.Auto, dismissable = s.Dismissable, holdSeconds = s.Hold, triggerDelay = s.TriggerDelay, dragDemo = s.Demo };
            }).ToArray();
            EditorUtility.SetDirty(sequence);
            return sequence;
        }

        // GameData/Award.md and Balance.md (GameDataApply): every wave's rewards on its clear, 1-1's start stock (the only
        // grant at a wave's start), coins, enemy groups and the robots' level-1 numbers; here only the lessons' topics and goals.
        public static string ApplyGameData()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play 종료 후 적용하세요.");
            string log = Campaign();
            AssetDatabase.SaveAssets();
            return log;
        }
        static string Campaign()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<DefenseCampaignDefinition>($"{Folder}/StageOneCampaign.asset");
            var data = GameDataApply.Load(campaign.rules.upgradeMultiplier);
            var so = new SerializedObject(campaign);
            var lessons = so.FindProperty("progression.lessons");
            SerializedProperty Lesson(int stage, int wave)
            {
                for (int i = 0; i < lessons.arraySize; i++)
                {
                    var lesson = lessons.GetArrayElementAtIndex(i);
                    if (lesson.FindPropertyRelative("stage").intValue == stage && lesson.FindPropertyRelative("wave").intValue == wave) return lesson;
                }
                lessons.arraySize++;
                var added = lessons.GetArrayElementAtIndex(lessons.arraySize - 1);
                added.FindPropertyRelative("stage").intValue = stage; added.FindPropertyRelative("wave").intValue = wave;
                added.FindPropertyRelative("topic").stringValue = stage + "-" + wave; added.FindPropertyRelative("goal").stringValue = "";
                return added;
            }
            void Topic(int stage, int wave, string topic, string goal = "")
            { var lesson = Lesson(stage, wave); lesson.FindPropertyRelative("topic").stringValue = topic; lesson.FindPropertyRelative("goal").stringValue = goal; }
            // 1-1 the swing and the Play button, 1-2 free, 1-3 the loop, 1-4 free, 1-5 the boss; the shooter and variables at 2-1,
            // conditions at 2-3, is_dead() 2-5, break 2-6, continue 2-8, the boss 2-10; the tank at 3-1 (13차 179).
            Topic(1, 1, "베기와 실행 버튼", "전사를 배치하고 slash()와 '실행'으로 적 막기");
            Topic(1, 2, "베기 자유 플레이", "새 전사를 배치하고 '실행'으로 베기");
            Topic(1, 3, "반복문", "반복문으로 slash()를 여러 번 실행해 적 8마리 막기");
            Topic(1, 4, "반복 자유 플레이", "새 전사와 반복문으로 몰려오는 적 막기");
            Topic(1, 5, "반복 보스 실습", "반복문으로 작은 적을 모두 막고 보스 쓰러뜨리기");
            Topic(2, 1, "변수: 찾은 적을 상자에 담기(원거리)", "찾은 적을 변수에 담아 원거리 로봇으로 공격하기");
            Topic(2, 2, "변수 자유 플레이", "2-1에서 만든 코드로 원거리 로봇과 근접 로봇이 함께 막기 · 빠른 적 첫 등장");
            Topic(2, 3, "조건문: 벨 때 고르기(근접 로봇)", "적이 공격 범위에 들어왔을 때만 베는 조건문으로 막기");
            Topic(2, 4, "조건 자유 플레이", "빠른 적이 먼저 와요 · 조건문 근접 로봇과 원거리 로봇이 함께 막기");
            Topic(2, 5, "is_dead(): 한 적을 끝까지", "쓰러진 적이면 새로 찾는 is_dead()로 한 적씩 끝내기");
            Topic(2, 6, "break: 사거리를 벗어나면 놓아주기", "빠른 적 등장 · 사거리 밖으로 나간 적은 break로 놓아주기 · 새 원거리 로봇엔 코드 복사");
            Topic(2, 7, "자유 실습: 쓰러지면 break", "원거리 로봇 안쪽 반복 맨 위에 if enemy.is_dead(): break 넣기");
            Topic(2, 8, "continue와 enemy.type", "단단한 적 등장 · 원거리 로봇은 continue로 단단한 적만, 작은 적은 근접 로봇이");
            Topic(2, 9, "자유 종합", "모든 종류의 적 · is_dead(), break, continue로 로봇마다 맡을 적 나누기");
            Topic(2, 10, "보스", "보스 등장 · 작은 적을 모두 막고 보스를 쓰러뜨리기");
            Topic(3, 1, "block() 소개", "근접 로봇의 block()으로 적을 멈추고 다른 로봇으로 공격하기");
            Topic(3, 2, "자유 플레이", "적이 두 번에 나눠 와요 · 끝까지 막기");
            Topic(3, 3, "반사병: continue로 건너뛰기", "반사병 등장 · e.reflecting이면 continue로 건너뛰고 다른 적을 쏘기");
            Topic(3, 4, "보스", "마지막 보스 · 작은 적을 모두 막고 보스를 쓰러뜨리기");
            // Stage 4 (팀 플레이): the team board's shared names, each robot keeping its own code.
            Topic(4, 1, "팀 보드: 함께 쓰는 jobs 큐", "팀 보드에 jobs = [] · 지원 로봇이 힐러를 jobs.append(e), 원거리 로봇이 jobs.pop(0)으로 꺼내 처치");
            Topic(4, 2, "사전: 우선순위 표", "팀 보드에 priority = {Enemy.Healer: 0, ...} · priority[e.type]이 가장 작은 적부터 쏘기");
            Topic(4, 3, "팀 종합: weak_point()와 get_robots()", "e.weak_point()로 지네·트리의 약점을, get_robots()와 buff(r)로 다친 로봇을 돌보기");
            void Stock(SerializedProperty list, string field, IList<(int value, int count)> items)
            {
                list.arraySize = items.Count;
                for (int i = 0; i < items.Count; i++)
                {
                    list.GetArrayElementAtIndex(i).FindPropertyRelative(field).intValue = items[i].value;
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("count").intValue = items[i].count;
                }
            }
            var summary = new System.Text.StringBuilder("GameData 적용\n");
            var start = data.Awards.FirstOrDefault(a => a.Stage == 0) ?? new GameDataTables.Award();
            string Items(GameDataTables.Award a)
            {
                string text = string.Join(", ", a.Robots.Select(r => r.role + " ×" + r.count).Concat(a.Blocks.Select(b => b.kind + " ×" + b.count)));
                return text.Length > 0 ? text : "-";
            }
            summary.AppendLine("start: " + Items(start));
            var waves = data.Groups.Select(g => (g.Stage, g.Wave)).Concat(data.Awards.Where(a => a.Stage > 0).Select(a => (a.Stage, a.Wave))).Distinct().OrderBy(w => w).ToArray();
            foreach (var (stage, wave) in waves)
            {
                var lesson = Lesson(stage, wave);
                var award = data.Awards.FirstOrDefault(a => a.Stage == stage && a.Wave == wave) ?? new GameDataTables.Award();
                // No grants at a wave's start but 1-1's start stock; a wave's row is what its clear gives.
                var first = stage == 1 && wave == 1 ? start : new GameDataTables.Award();
                Stock(lesson.FindPropertyRelative("robots"), "role", first.Robots.Select(r => ((int)r.role, r.count)).ToList());
                Stock(lesson.FindPropertyRelative("blocks"), "kind", first.Blocks.Select(b => ((int)b.kind, b.count)).ToList());
                Stock(lesson.FindPropertyRelative("rewardRobots"), "role", award.Robots.Select(r => ((int)r.role, r.count)).ToList());
                Stock(lesson.FindPropertyRelative("rewards"), "kind", award.Blocks.Select(b => ((int)b.kind, b.count)).ToList());
                var coins = data.CoinRows.FirstOrDefault(c => c.Stage == stage && c.Wave == wave);
                lesson.FindPropertyRelative("startCoins").intValue = coins?.Start ?? -1;
                lesson.FindPropertyRelative("coinsPerKill").intValue = coins?.PerKill ?? -1;
                summary.AppendLine($"{stage}-{wave} 클리어 보상: {Items(award)}" + (coins != null && coins.Start >= 0 ? $" · 시작 코인 {coins.Start}" : ""));
            }
            for (int i = lessons.arraySize - 1; i >= 0; i--)
            {
                var lesson = lessons.GetArrayElementAtIndex(i);
                if (!waves.Contains((lesson.FindPropertyRelative("stage").intValue, lesson.FindPropertyRelative("wave").intValue))) lessons.DeleteArrayElementAtIndex(i);
            }
            // Balance.md: a boss comes once every minion is down (its delay unused) and takes 12 core health on reaching it; the
            // others the enemy's own. Roads stay as they were (1-5's minions walk the second one).
            var stages = so.FindProperty("stages");
            var enemyList = so.FindProperty("enemies");
            EnemyDefinition Data(string name)
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"Assets/!_Project/Defense/Data/{name}.asset") ??
                    throw new InvalidOperationException(name + " enemy asset not found");
                // A kind the campaign did not have yet (Healer, Shielder) joins its enemy list.
                if (!Enumerable.Range(0, enemyList.arraySize).Any(i => enemyList.GetArrayElementAtIndex(i).objectReferenceValue == enemy))
                { enemyList.arraySize++; enemyList.GetArrayElementAtIndex(enemyList.arraySize - 1).objectReferenceValue = enemy; }
                return enemy;
            }
            foreach (var stageGroups in data.Groups.GroupBy(g => g.Stage))
            {
                // A stage the tables add (4) starts as a copy of the last one (its roads), its waves rewritten below.
                if (stageGroups.Key == stages.arraySize + 1) stages.arraySize++;
                if (stageGroups.Key > stages.arraySize) throw new InvalidOperationException($"캠페인에 {stageGroups.Key}스테이지가 없습니다.");
                var waveList = stages.GetArrayElementAtIndex(stageGroups.Key - 1).FindPropertyRelative("waves");
                waveList.arraySize = stageGroups.Max(g => g.Wave);
                foreach (var waveGroups in stageGroups.GroupBy(g => g.Wave))
                {
                    var groups = waveList.GetArrayElementAtIndex(waveGroups.Key - 1).FindPropertyRelative("groups");
                    var routes = Enumerable.Range(0, groups.arraySize).Select(i => groups.GetArrayElementAtIndex(i).FindPropertyRelative("route").intValue).ToArray();
                    var list = waveGroups.ToArray();
                    groups.arraySize = list.Length;
                    for (int i = 0; i < list.Length; i++)
                    {
                        var g = groups.GetArrayElementAtIndex(i); var x = list[i];
                        g.FindPropertyRelative("enemy").objectReferenceValue = Data(x.Enemy);
                        g.FindPropertyRelative("route").intValue = i < routes.Length ? routes[i] : 0;
                        g.FindPropertyRelative("count").intValue = x.Count;
                        g.FindPropertyRelative("delay").floatValue = x.Delay;
                        g.FindPropertyRelative("interval").floatValue = x.Interval;
                        g.FindPropertyRelative("healthOverride").floatValue = x.Health;
                        g.FindPropertyRelative("speedOverride").floatValue = x.Speed;
                        g.FindPropertyRelative("leakDamageOverride").intValue = x.Boss ? 12 : 0;
                        g.FindPropertyRelative("boss").boolValue = x.Boss;
                    }
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(campaign);
            foreach (var stats in data.Robots)
            {
                var robot = campaign.robots.FirstOrDefault(r => r && r.stats.role == stats.Role) ?? throw new InvalidOperationException(stats.Role + " 로봇 asset이 캠페인에 없습니다.");
                var r = new SerializedObject(robot);
                r.FindProperty("stats.damage").floatValue = stats.Damage;
                r.FindProperty("stats.interval").floatValue = stats.Interval;
                r.FindProperty("stats.range").floatValue = stats.Range;
                r.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(robot);
            }
            GameDataApply.MarkApplied();
            summary.Append($"적 그룹 {data.Groups.Count}개 · 로봇 {data.Robots.Count}종 · 경고 {data.Warnings.Count}개");
            return summary.ToString();
        }

        public static string Run()
        {
            var log = new System.Text.StringBuilder();
            lines = ParseScript(Path.Combine(Directory.GetCurrentDirectory(), Script));
            log.AppendLine("lines " + lines.Count);
            var sequences = new[] { Write("1-1", 1, OneOne()), Write("1-2", 2, OneTwo()), Write("1-3", 3, OneThree()), Write("1-4", 4, OneFour()), Write("1-5", 5, OneFive()),
                Write("2-1", 1, TwoOne(), 2), Write("2-3", 3, TwoThree(), 2), Write("2-5", 5, TwoFive(), 2), Write("2-6", 6, TwoSix(), 2),
                Write("2-7", 7, TwoSeven(), 2), Write("2-8", 8, TwoEight(), 2), Write("2-9", 9, TwoNine(), 2), Write("2-10", 10, TwoTen(), 2),
                Write("3-1", 1, ThreeOne(), 3), Write("2-2", 2, TwoTwo(), 2), Write("2-4", 4, TwoFour(), 2) };
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
