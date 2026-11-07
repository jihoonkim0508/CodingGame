using System;
using System.Collections.Generic;
using System.Linq;
using CodingGame.BlockCoding;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    public static partial class DevTest
    {
        static void RestoreStageTwoWork(DefenseBattle battle, int wave, int step = 0, TutorialSequence preview = null)
        {
            var sequence = preview ? preview : TutorialSequenceFor(2, wave);
            if (!sequence) throw new InvalidOperationException("Stage 2 튜토리얼 asset을 먼저 생성하세요.");
            var sim = battle.Simulation;
            RobotState EnsureRobot(int placedWave, RobotRole role)
            {
                var bot = sim.Robots.FirstOrDefault(r => r.PlacedWave == placedWave && r.Spec.role == role);
                if (bot != null) return bot;
                bot = PlaceTeachingRobot(battle, role); bot.PlacedWave = placedWave; return bot;
            }
            void Loop(RobotState bot)
            {
                if (StageTwoLearning.Matches(bot.Program?.CopyBlocks(), bot.Spec.role)) return;
                sim.ApplyProgram(bot.Id, "robot_" + bot.Id, new[] { StageTwoLearning.Loop(bot.Spec.role, bot.PlacedWave == 0 || bot.PlacedWave == 2 ? 3 : 10) });
            }
            for (int priorWave = 0; priorWave < Math.Min(wave - 1, 4); priorWave++)
                Loop(EnsureRobot(priorWave, priorWave < 2 ? RobotRole.Warrior : RobotRole.Shooter));
            if (step == 0 || wave == 5) return;
            var current = sequence.steps[step];
            bool result = current.trigger == TutorialSignals.WaveCleared || current.targetKeys.Contains("Battle.ClaimRewards");
            bool placed = sequence.steps.Take(step).Any(s => s.completionSignal == TutorialSignals.RobotPlaced);
            if (!placed && !result) return;
            var robot = EnsureRobot(wave - 1, wave <= 2 ? RobotRole.Warrior : RobotRole.Shooter);
            if (wave == 2 || wave == 4 || result) { Loop(robot); return; }

            int Index(string id) => Array.FindIndex(sequence.steps, s => s.stepId == id);
            IReadOnlyList<CodeBlock> program = Array.Empty<CodeBlock>();
            if (wave == 1)
            {
                var body = StageTwoLearning.Loop(RobotRole.Warrior).Body;
                if (step > Index("C03")) program = new[] { StageTwoLearning.Loop(RobotRole.Warrior) };
                else if (step > Index("C02")) program = new[] { StageTwoLearning.Loop(RobotRole.Warrior, 10) };
                else if (step > Index("C01")) program = new[] { new CodeBlock(BlockKind.For, "i", new CodeBlock(BlockKind.Number, "10")), body[0], body[1] };
                else if (step >= Index("B02")) program = body;
            }
            else if (wave == 3 && step >= Index("B02"))
            {
                var loop = StageTwoLearning.Loop(RobotRole.Shooter, attack: step > Index("B04"));
                if (step == Index("B04")) loop.Body.Add(new CodeBlock(BlockKind.Shot));
                program = new[] { loop };
            }
            // Loading the draft also restores deliberately incomplete target sockets before the guided action.
            battle.CodeEditor.Open(robot.Id);
            battle.CodeEditor.Panel.LoadProgram("robot_" + robot.Id, program);
        }

        static void PrepareTutorialTargets(DefenseBattle battle, TutorialSequence sequence, int index)
        {
            var step = sequence.steps[index];
            var robot = sequence.stage == 2 ? battle.Tutorial.StageTwoRobot : TutorialWatcher.Robot(battle.Simulation, step.robotIndex);
            var keys = step.targetKeys.Concat(step.texts.SelectMany(t => t.focusKeys ?? Array.Empty<string>()));
            if (robot != null) battle.SelectRobot(robot.Id);
            bool editor = keys.Any(k => k.StartsWith("Editor.", StringComparison.Ordinal)) || step.stageTwoExample != 0 || step.prepareBaselineExample || step.prepareDistanceExample;
            if (editor && robot != null) battle.CodeEditor.Open(robot.Id);
            else battle.CodeEditor.Close(false);
            if (step.completionSignal == TutorialSignals.RobotPlaced)
            {
                var role = sequence.stage == 2 && sequence.wave == 3 ? RobotRole.Shooter : RobotRole.Warrior;
                int definition = Enumerable.Range(0, battle.Simulation.Setup.ActionProfiles.Length).First(i => battle.DefinitionRole(i) == role);
                battle.ChooseRobot(definition);
            }
            if (sequence.stage == 2 && step.completionSignal == TutorialDirector.StageTwoObservation && !step.observePreview)
                battle.AttackPreview.ObserveOnce();
            // Result steps need a real completed wave, not a fabricated tutorial success signal.
            if (step.trigger == TutorialSignals.WaveCleared || step.targetKeys.Contains("Battle.ClaimRewards"))
            {
                var sim = battle.Simulation;
                sim.Start();
                if (sim.Phase == BattlePhase.Ready) throw new InvalidOperationException(sim.StartError());
                for (int i = 0; i < 36000 && sim.Phase == BattlePhase.Running; i++) sim.Advance(DefenseSimulation.TickDuration);
                if (sim.Phase != BattlePhase.Reward && sim.Phase != BattlePhase.Victory)
                    throw new InvalidOperationException("결과 구간을 준비하는 전투가 완료되지 않았습니다. 앞 구간에서 배치·코드를 확인하세요.");
            }
            battle.EditorRefreshView();
        }
    }
}
