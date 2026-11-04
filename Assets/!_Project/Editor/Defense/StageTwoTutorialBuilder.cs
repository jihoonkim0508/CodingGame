using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodingGame.BlockCoding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    public static partial class TutorialSequenceBuilder
    {
        const string Loop3 = "for i in range(3):\n    enemy = get_nearest_enemy()\n    slash(enemy)";
        const string Loop5 = "for i in range(5):\n    enemy = get_nearest_enemy()\n    slash(enemy)";
        const string ConditionalShot5 = "for i in range(5):\n    enemy = get_nearest_enemy()\n    enemy_distance = enemy.distance(get_my_pos())\n    if enemy_distance <= 3:\n        attack(enemy)";
        const string Shot5 = "for i in range(5):\n    enemy = get_nearest_enemy()\n    attack(enemy)";
        static S Two(string suffix, Action<S> configure = null) => L("2-" + suffix, configure);
        static Action<S> Observe(string signal, int mode = 0, bool restart = false) => Signal(signal, new[] { Preview }, s => {
            s.Preview = true; s.PreviewLesson = mode; s.RestartPreview = restart; s.Hold = 1;
            s.Focus = new[] { Preview };
        });
        static List<S> TwoOne() => new List<S> {
            Two("1.A01"), Two("1.A02"), Two("1.A03", Click(TutorialKeys.RobotButton(4))),
            Two("1.A04"), Two("1.A05", Signal(TutorialSignals.RobotPlaced, new[] { TutorialKeys.Deployment }, s => s.Focus = new[] { TutorialKeys.Floor })),
            Two("1.B01", Click(Unit0, s => s.Pointer = Unit0)),
            Two("1.B01", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Id = "B01a"; s.Pointer = Develop; })),
            Two("1.B02", s => { s.RangedExample = true; s.PreviewLesson = 3; s.Focus = new[] { Drop }; }),
            Two("1.B03", Signal(TutorialSignals.CodeLinePlaced, new[] { Drop, Palette }, s => {
                s.Expected = "1.1:Shot"; s.Pointer = "Editor.Palette.Shot"; s.Focus = new[] { "Editor.Palette.Shot", "Editor.Code.BodyAfter.1.0" };
                s.Demo = "Editor.Palette.Shot>Editor.Code.BodyAfter.1.0";
            })),
            Two("1.B04", Signal(TutorialSignals.SlotFilled, new[] { Drop, Palette }, s => {
                s.Expected = "Editor.Code.BodyArg.1.1.0"; s.AutoSlot = s.Expected; s.RequiredSlotBlock = s.Expected + ":Variable";
                s.Pointer = "Editor.Palette.Variable"; s.Focus = new[] { "Editor.Palette.Variable", "Editor.Code.BodyArg.1.1.0" };
            })),
            Two("1.B05", Code(Shot5, s => { s.Preview = false; s.Focus = new[] { "Editor.Code.BodyName.1.1.0" }; })),
            Two("1.C01", Observe(TutorialSignals.PreviewHit, restart:true)),
            Two("1.C02", Focus(Preview)), Two("1.C03", Focus(Preview)),
            Two("1.D01", Click(Close, s => { s.Pointer = Close; s.Undimmed = true; })),
            Two("1.D01", Signal(TutorialSignals.BattleStarted, new[] { Start }, s => { s.Id = "D01a"; s.Top = true; s.Pointer = Start; })),
            Two("1.D02", s => s.Trigger = TutorialSignals.WaveCleared),
            Two("1.D03", Click("Battle.ClaimRewards", s => { s.Top = true; s.Pointer = "Battle.ClaimRewards"; }))
        };
        static List<S> TwoTwo() => new List<S> {
            Two("2.A01"), Two("2.A02"), Two("2.A03"),
            Two("2.B01", s => { s.Trigger = TutorialSignals.WaveCleared; s.Battle = true; s.HintCode = "원거리 사격은 attack이에요. 적 찾기와 사격을 for 안에 넣으세요.\n\n" + Shot5; }),
            Two("2.B02"), Two("2.B03"), Two("2.B04", Click("Battle.ClaimRewards", s => { s.Top = true; s.Pointer = "Battle.ClaimRewards"; }))
        };
        static List<S> TwoThree()
        {
            Action<S> EditSignal(string signal, string expected, string pointer, params string[] focus) =>
                Signal(signal, new[] { Drop, Palette }, With(s => {
                    s.Expected = expected; s.Pointer = pointer;
                    // Every guided block placement previews its path from the palette to the actual slot.
                    if (signal == TutorialSignals.CodeLinePlaced || signal == TutorialSignals.SlotFilled)
                    {
                        string slot = signal == TutorialSignals.SlotFilled ? expected :
                            focus.LastOrDefault(key => key.StartsWith(TutorialKeys.CodePrefix, StringComparison.Ordinal));
                        if (!string.IsNullOrEmpty(slot)) s.Demo = pointer + ">" + slot;
                    }
                }, Focus(focus)));
            return new List<S> {
                L("2-3.A01"), L("2-3.A02"),
                L("2-3.P01", Signal(TutorialSignals.RobotPlaced, new[] { TutorialKeys.RobotButton(4), TutorialKeys.Deployment }, s => s.Focus = new[] { TutorialKeys.Floor })),
                L("2-3.A03", Click(Unit0, s => s.Pointer = Unit0)),
                L("2-3.A04", Signal(TutorialSignals.EditorOpened, new[] { Develop }, s => { s.Pointer = Develop; s.Preview = true; })),
                L("2-3.A05", Signal(TutorialSignals.PreviewSingleHit, new[] { Preview }, With(
                    s => { s.BaselineExample = true; s.Preview = true; s.Hold = 1.5f; }, Focus(Preview)))),
                L("2-3.B01", With(s => s.DistanceExample = true, Focus("Editor.Code.Line.1"))),
                L("2-3.B02", Focus("Editor.Code.Line.2")),
                L("2-3.B03", Focus("Editor.Palette.Number")),
                L("2-3.B04"), L("2-3.B05"),
                L("2-3.C01", EditSignal(TutorialSignals.CodeLinePlaced, "3:If", "Editor.Palette.If", "Editor.Palette.If", "Editor.Code.After.2")),
                L("2-3.C03", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg.3.0", "Editor.Palette.Comparison", "Editor.Palette.Comparison", "Editor.Code.Arg.3.0"), s => s.AutoSlot = "Editor.Code.Arg.3.0")),
                L("2-3.C04", EditSignal(TutorialSignals.DraftValueMatches, "3.0|<=", "Editor.Code.Option.3.0", "Editor.Code.Option.3.0")),
                L("2-3.C06", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg.3.0.0", "Editor.Palette.Variable", "Editor.Palette.Variable", "Editor.Code.Arg.3.0.0"), s => s.AutoSlot = "Editor.Code.Arg.3.0.0")),
                L("2-3.C07", EditSignal(TutorialSignals.DraftValueMatches, "3.0.0|enemy_distance", "Editor.Code.Name.3.0.0", "Editor.Code.Name.3.0.0")),
                L("2-3.C09", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Arg.3.0.1", "Editor.Palette.Number", "Editor.Palette.Number", "Editor.Code.Arg.3.0.1"), s => s.AutoSlot = "Editor.Code.Arg.3.0.1")),
                L("2-3.C09a", EditSignal(TutorialSignals.DraftValueMatches, "3.0.1|3", "Editor.Code.Name.3.0.1", "Editor.Code.Name.3.0.1")),
                L("2-3.C10", Focus("Editor.Code.Arg.3.0")),
                L("2-3.D02", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.Body.3", "Editor.Palette.Shot", "Editor.Palette.Shot", "Editor.Code.Body.3"), s => s.AutoSlot = "Editor.Code.Body.3")),
                L("2-3.D04", With(EditSignal(TutorialSignals.SlotFilled, "Editor.Code.BodyArg.3.0.0", "Editor.Palette.Variable", "Editor.Palette.Variable", "Editor.Code.BodyArg.3.0.0"), s => s.AutoSlot = "Editor.Code.BodyArg.3.0.0")),
                L("2-3.D05", EditSignal(TutorialSignals.ProgramMatches, Code13, "Editor.Code.BodyName.3.0.0", "Editor.Code.BodyName.3.0.0")),
                L("2-3.D06", Signal(TutorialSignals.PreviewHit, new[] { Preview }, With(s => { s.Preview = true; s.RestartPreview = true; s.Hold = 2; }, Focus(Preview)))),
                L("2-3.D07", With(Ed, Focus("Editor.Code.Body.3"))),
                L("2-3.E01", s => s.CloseEditor = true),
                L("2-3.E02", Signal(TutorialSignals.BattleStarted, new[] { Start }, s => s.Top = true)),
                L("2-3.E03", With(Fight, s => s.Trigger = TutorialSignals.FirstAttack)),
                L("2-3.E04", s => s.Trigger = TutorialSignals.WaveCleared), L("2-3.E05"),
                L("2-3.E06", Click("Battle.ClaimRewards", s => { s.Pointer = "Battle.ClaimRewards"; s.Top = true; })),
            };
        }

        static List<S> TwoFour() => new List<S> {
            Two("4.A01"), Two("4.A02"), Two("4.A03"),
            Two("4.B01", s => { s.Trigger = TutorialSignals.WaveCleared; s.Battle = true; s.HintCode = "원거리 사격은 attack이에요. 적 찾기와 사격을 for 안에 넣으세요.\n\n" + ConditionalShot5; }),
            Two("4.B02"), Two("4.B03"), Two("4.B04", Click("Battle.ClaimRewards", s => { s.Top = true; s.Pointer = "Battle.ClaimRewards"; }))
        };
        static List<S> TwoFive() => new List<S> {
            Two("5.A01"), Two("5.A02"), Two("5.A03"), Two("5.A04"), Two("5.A05"),
            Two("5.B01", s => { s.Trigger = TutorialSignals.WaveCleared; s.Battle = true;
                s.HintCode = "프로그램 종료: 횟수를 확인하세요. 공격이 닿지 않으면 사거리와 대상을 확인하세요.\n전사\n" + Loop5 + "\n\n원거리\n" + ConditionalShot5; }),
            Two("5.B02"), Two("5.B03"), Two("5.B04", Click("Battle.ClaimRewards", s => { s.Top = true; s.Pointer = "Battle.ClaimRewards"; }))
        };

        [MenuItem("Tools/Develop/Apply Stage 2 Tutorials 2-1 to 2-5")]
        public static void ApplyStageTwo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 적용하세요.");
            EditorSceneManager.OpenScene("Assets/!_Project/Scenes/Defense.unity", OpenSceneMode.Single);
            const string stageFolder = "Assets/!_Project/Defense/Tutorial/Stage2";
            if (!AssetDatabase.IsValidFolder(stageFolder)) AssetDatabase.CreateFolder("Assets/!_Project/Defense/Tutorial", "Stage2");
            lines = ParseScript(Path.Combine(Directory.GetCurrentDirectory(), "Docs/flow_reordered/stage2.md"));
            var sequences = new[] { Write("2-1", 1, TwoOne(), 2), Write("2-2", 2, TwoTwo(), 2),
                Write("2-3", 3, TwoThree(), 2), Write("2-4", 4, TwoFour(), 2), Write("2-5", 5, TwoFive(), 2) };
            var portrait = AssetDatabase.LoadAssetAtPath<TutorialSequence>($"{Folder}/Tutorial_1-1.asset").speakerPortrait;
            foreach (var sequence in sequences) { sequence.speakerPortrait = portrait; EditorUtility.SetDirty(sequence); }
            var campaign = AssetDatabase.LoadAssetAtPath<DefenseCampaignDefinition>($"{Folder}/StageOneCampaign.asset");
            campaign.progression.lessons = campaign.progression.lessons.Where(lesson => lesson.stage != 2)
                .Concat(DefenseCurriculum.Create().Where(lesson => lesson.stage == 2)).OrderBy(lesson => lesson.stage).ThenBy(lesson => lesson.wave).ToArray();
            var basic = AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{Folder}/TutorialError.asset");
            var stage = campaign.stages[1]; stage.waveSeconds = 120;
            DefenseBattle.SpawnEntry Group(int count, float health, bool boss = false) => new DefenseBattle.SpawnEntry {
                enemy = basic, route = 0, count = count, delay = 0, interval = .45f,
                healthOverride = health, speedOverride = 1.25f, leakDamageOverride = boss ? 12 : 1, boss = boss
            };
            // Short continuous encounters: no hidden retries, infinite loops or long boss delay.
            stage.waves = new[] {
                new DefenseBattle.Wave { groups = new[] { Group(3, 16) } },
                new DefenseBattle.Wave { groups = new[] { Group(5, 18) } },
                new DefenseBattle.Wave { groups = new[] { Group(1, 3) } },
                new DefenseBattle.Wave { groups = new[] { Group(5, 24) } },
                new DefenseBattle.Wave { groups = new[] { Group(3, 20), Group(1, 70, true) } }
            };
            campaign.ValidateContent(); EditorUtility.SetDirty(campaign);
            var director = UnityEngine.Object.FindObjectsByType<TutorialDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var all = director.Sequences.Where(sequence => sequence && sequence.stage != 2).Concat(sequences).ToArray();
            var so = new SerializedObject(director); var list = so.FindProperty("sequences"); list.arraySize = all.Length;
            for (int i = 0; i < all.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
            so.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(director.gameObject.scene);
            Debug.Log("Stage 2 applied: " + string.Join(", ", sequences.Select(sequence => sequence.sequenceId + "=" + sequence.steps.Length)));
        }
    }
}
