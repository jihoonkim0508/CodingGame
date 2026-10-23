using System;
using System.Collections.Generic;
using System.Linq;
using CodingGame.BlockCoding;
using UnityEditor;

namespace CodingGame.Defense.Editor
{
    public static partial class TutorialSequenceBuilder
    {
        [MenuItem("Tools/Develop/Stage 1~2 튜토리얼 asset 다시 만들기")]
        public static void RebuildTutorials() => Menu();

        static string LoopCode(int count, bool condition) => "for i in range(" + count + "):\n" +
            string.Join("\n", (condition ? Code14 : Code11).Split('\n').Where(l => l.Length > 0).Select(l => "    " + l));

        static S Practice(string id, string answer, int robot = 0) => L(id,
            Signal(TutorialSignals.ProgramMatches, new[] { Drop, Palette, Hint }, s => {
                s.Expected = answer; s.HintCode = answer; s.Robot = robot;
                s.Preview = true; s.Dismissable = true; s.Pointer = Hint; s.PointerDelay = 60;
            }));

        static void FinishWave(List<S> steps, string prefix)
        {
            steps.Add(L(prefix + ".D01", Signal(TutorialSignals.EditorClosed, new[] { Close }, s => {
                s.Pointer = Close; s.Preview = true; s.Undimmed = true;
            })));
            steps.Add(L(prefix + ".E01", Signal(TutorialSignals.BattleStarted, new[] { Start }, s => {
                s.Pointer = Start; s.Top = true;
            })));
            steps.Add(L(prefix + ".E02", With(Fight, s => s.Trigger = TutorialSignals.FirstAttack)));
            steps.Add(L(prefix + ".F01", s => s.Trigger = TutorialSignals.WaveCleared));
            steps.Add(L(prefix + ".F02", Click("Battle.ClaimRewards", s => {
                s.Pointer = "Battle.ClaimRewards"; s.Top = true;
            })));
        }

        static List<S> Finale()
        {
            var steps = new List<S> { L("1-5.A01"), L("1-5.A02", Focus(Route)) };
            steps.AddRange(OpenEditor("1-5.B01"));
            steps.Add(Practice("1-5.C01", Code14));
            steps.Add(L("1-5.C02", s => { s.OpenEditor = true; s.Robot = 1; s.Preview = true; s.HintCode = Code14; }));
            steps.Add(Practice("1-5.C03", Code14, 1));
            FinishWave(steps, "1-5");
            return steps;
        }

        static List<S> StageTwo(int wave)
        {
            string p = "2-" + wave;
            string answer = wave == 1 ? Code14 : LoopCode(wave == 2 ? 5 : 10, wave >= 4);
            var steps = new List<S> { L(p + ".A01"), L(p + ".A02") };
            if (wave == 1 || wave == 5)
            {
                steps.Add(L(p + ".P01", Click(TutorialKeys.RobotButton(1))));
                steps.Add(L(p + ".P02", Signal(TutorialSignals.RobotPlaced, new[] { TutorialKeys.Deployment }, Focus(TutorialKeys.Floor))));
            }
            steps.AddRange(OpenEditor(p + ".B01"));
            steps.Add(L(p + ".B02", With(Ed, Focus(wave == 1 ? Drop : "Editor.Palette.For"), s => s.HintCode = answer)));
            steps.Add(L(p + ".B03", With(Ed, s => s.HintCode = answer)));
            if (wave == 2)
                steps.Add(L(p + ".B04", Code(answer, s => {
                    s.Pointer = "Editor.Code.Body.1|Editor.Code.Start";
                    s.HintCode = answer; s.Targets = new[] { Drop, Palette, Hint };
                    s.Demo = "Editor.Palette.For>Editor.Code.Start";
                })));
            if (wave >= 4)
                steps.Add(L(p + ".B04", With(Ed, s => s.HintCode = answer)));
            steps.Add(Practice(p + ".C01", answer));
            if (wave >= 2)
                steps.Add(L(p + ".C04", With(Ed, Focus("Editor.Code.Body.1"))));
            if (wave == 5)
            {
                steps.Add(L(p + ".C02", s => { s.OpenEditor = true; s.Robot = 1; s.Preview = true; s.HintCode = answer; }));
                steps.Add(Practice(p + ".C03", answer, 1));
            }
            FinishWave(steps, p);
            return steps;
        }

        // Only the continuation lessons change. Stage 1-1~4 and Stage 3 keep their existing content.
        static void ConfigureContinuation()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<DefenseCampaignDefinition>(Folder + "/StageOneCampaign.asset");
            BlockStock B(BlockKind kind, int count = 1) => new BlockStock { kind = kind, count = count };
            void Lesson(int stage, int wave, string topic, int robots, params BlockStock[] blocks)
            {
                var lesson = campaign.progression.lessons.Single(l => l.stage == stage && l.wave == wave);
                lesson.topic = topic;
                lesson.robots = robots == 0 ? Array.Empty<RobotStock>() : new[] { new RobotStock { role = RobotRole.Warrior, count = robots } };
                lesson.blocks = blocks;
                lesson.rewards = new[] { B(BlockKind.Number) };
            }
            BlockStock[] Kit() => new[] { B(BlockKind.DeclareVariable, 3), B(BlockKind.NearestEnemy), B(BlockKind.Variable, 4),
                B(BlockKind.MyPos), B(BlockKind.MyAttackRange), B(BlockKind.If), B(BlockKind.Comparison), B(BlockKind.Slash) };
            Lesson(1, 5, "변수와 조건: 보스 종합 실습", 0);
            Lesson(2, 1, "조건 복습: 사거리 안의 적 공격하기", 1, Kit());
            Lesson(2, 2, "반복: for로 다섯 번 실행하기", 0, B(BlockKind.For), B(BlockKind.Number));
            Lesson(2, 3, "반복: 횟수와 대상 갱신", 0);
            Lesson(2, 4, "반복과 조건: 매번 거리 확인하기", 0);
            Lesson(2, 5, "반복과 조건: 두 로봇 종합 실습", 1, Kit().Concat(new[] { B(BlockKind.For), B(BlockKind.Number) }).ToArray());
            // Introductory enemies give room to observe finite loops; keep the established map and simulation rules.
            var enemy = campaign.stages[0].waves[0].groups[0].enemy;
            var stageTwo = campaign.stages[1];
            stageTwo.waveSeconds = 120;
            int[] counts = { 2, 3, 4, 4, 6 };
            for (int w = 0; w < 5; w++)
                stageTwo.waves[w].groups = new[] { new DefenseBattle.SpawnEntry { enemy = enemy, route = 0, count = counts[w], interval = .4f, delay = 0 } };
            EditorUtility.SetDirty(campaign);
        }
    }
}
