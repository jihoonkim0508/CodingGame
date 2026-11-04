using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CodingGame.BlockCoding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Point = System.Numerics.Vector2;

namespace CodingGame.Defense.Editor
{
    public static class StageTwoVerification
    {
        static readonly List<string> report = new List<string>();
        static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); report.Add("PASS " + label); }
        static CodeBlock[] Repeat(RobotRole role, int count = 5, string name = "target")
        {
            var loop = new CodeBlock(BlockKind.For, "0", new CodeBlock(BlockKind.Number, count.ToString()));
            loop.Body.Add(new CodeBlock(BlockKind.DeclareVariable, name, new CodeBlock(BlockKind.NearestEnemy)));
            loop.Body.Add(new CodeBlock(role == RobotRole.Warrior ? BlockKind.Slash : BlockKind.Shot, "0", new CodeBlock(BlockKind.Variable, name)));
            return new[] { loop };
        }
        public static void Run()
        {
            try
            {
                report.Clear(); TutorialSequenceBuilder.ApplyStageTwo();
                var director = UnityEngine.Object.FindObjectsByType<TutorialDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
                Check(director.Sequences.Count(s => s && s.stage == 1) == 5, "Stage 1 sequences retained");
                Check(director.Sequences.Count(s => s && s.stage == 2) == 5, "Five Stage 2 sequences connected");
                foreach (var sequence in director.Sequences.Where(s => s && s.stage == 2))
                {
                    Check(sequence.enabled && sequence.steps.Select(s => s.stepId).Distinct().Count() == sequence.steps.Length, sequence.sequenceId + " enabled and unique steps");
                    foreach (var step in sequence.steps)
                        Check(step.texts.Length > 0 && step.texts.All(t => !string.IsNullOrWhiteSpace(t.text)), sequence.sequenceId + "/" + step.stepId + " dialogue");
                }
                // Structural matching must retain scope, order and arbitrary consistent variable names.
                var repeat = new DefenseProgram("test", Repeat(RobotRole.Warrior, 3));
                Check(TutorialWatcher.Matches(repeat, "for i in range(3):\n    enemy = get_nearest_enemy()\n    slash(enemy)"), "Renamed three-run loop matches");
                var outside = Repeat(RobotRole.Warrior, 3)[0]; var finder = outside.Body[0]; outside.Body.RemoveAt(0);
                Check(!TutorialWatcher.HasRepeatAttack(new DefenseProgram("outside", new[] { finder, outside }), RobotRole.Warrior), "Outside search rejected");
                Check(!TutorialWatcher.Matches(new DefenseProgram("outside", new[] { finder, outside }), "for i in range(3):\n    enemy = get_nearest_enemy()\n    slash(enemy)"), "Guided scope mismatch rejected");
                Check(!TutorialWatcher.HasRepeatAttack(new DefenseProgram("zero", Repeat(RobotRole.Warrior, 0)), RobotRole.Warrior), "Zero iterations rejected");
                Check(TutorialWatcher.HasRepeatAttack(new DefenseProgram("renamed", Repeat(RobotRole.Shooter, 7, "foe")), RobotRole.Shooter), "Other positive count and variable accepted");
                var battle = UnityEngine.Object.FindObjectsByType<DefenseBattle>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
                var campaign = AssetDatabase.LoadAssetAtPath<DefenseCampaignDefinition>("Assets/!_Project/Defense/Tutorial/Stage1/StageOneCampaign.asset");
                var make = typeof(DefenseBattle).GetMethod("MakeSetup", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int strategy = 0; strategy < 3; strategy++)
                {
                    var setup = (BattleSetup)make.Invoke(battle, new object[] { campaign, 2 });
                    setup.PreviousStageRewards = false;
                    var sim = new DefenseSimulation(setup);
                    var samples = setup.Routes[0].Points.Zip(setup.Routes[0].Points.Skip(1), (a, b) =>
                        Enumerable.Range(0, 30).Select(i => Point.Lerp(a, b, i / 30f))).SelectMany(x => x).ToArray();
                    for (int wave = 1; wave <= 5; wave++)
                    {
                        if (wave < 5)
                        {
                            var role = wave < 3 ? RobotRole.Warrior : RobotRole.Shooter;
                            int definition = Array.FindIndex(campaign.robots, r => r.stats.role == role);
                            var spec = campaign.robots[definition].stats;
                            var cells = new List<Point>();
                            for (float x = setup.Min.X + .4f; x < setup.Max.X; x += .8f)
                            for (float y = setup.Min.Y + .4f; y < setup.Max.Y; y += .8f)
                            { var p = new Point(x, y); if (sim.PlacementError(spec, p) == null) cells.Add(p); }
                            var choices = cells.OrderByDescending(p => samples.Count(q => Point.Distance(p, q) <= spec.range)).ToArray();
                            Check(choices.Length > strategy * 3, "Legal placements " + strategy + "/" + wave);
                            var robot = sim.Place(spec, definition, choices[strategy * 3], out var error);
                            Check(robot != null, "Placed " + role + " " + error);
                            sim.ApplyProgram(robot.Id, "robot_" + robot.Id, Repeat(role));
                        }
                        Check(sim.StartError() == null, "Start gate " + strategy + "/" + wave + ": " + sim.StartError());
                        sim.Start();
                        float bossHealth = -1;
                        for (int i = 0; i < 60 * 125 && sim.Phase == BattlePhase.Running; i++)
                        {
                            sim.Advance(1.0 / 60);
                            var boss = sim.Enemies.FirstOrDefault(e => e.Boss); if (boss != null) bossHealth = boss.Health;
                        }
                        report.Add("DETAIL bossHealth=" + bossHealth + " robots=" + string.Join(";", sim.Robots.Select(r => r.Spec.role + "@" + r.Position + " attacks=" + r.AttacksThisWave + " runs=" + r.Program?.CompletedRuns)));
                        report.Add($"RESULT strategy={strategy} wave={wave} phase={sim.Phase} hp={sim.BaseHealth} kills={sim.Kills} survivors={sim.LastSurvivors} timeout={sim.LastWaveTimedOut}");
                        Check(sim.Phase == BattlePhase.Reward && !sim.LastWaveTimedOut, "Continuous encounter cleared " + strategy + "/" + wave);
                        Check(sim.Robots.All(robot => robot.Program == null || robot.Program.Fault == null), "No program faults " + strategy + "/" + wave);
                        Check(sim.ClaimRewards(), "Claim " + strategy + "/" + wave);
                    }
                }
                var path = "TestResults/Stage2/verification.md"; Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "# Stage 2 verification\n\n" + string.Join("\n", report.Select(line => "- " + line)));
                Debug.Log("STAGE2_VERIFICATION_PASS " + report.Count);
            }
            catch (Exception error)
            {
                Directory.CreateDirectory("TestResults/Stage2");
                File.WriteAllText("TestResults/Stage2/verification.md", string.Join("\n", report) + "\nFAIL " + error);
                Debug.LogException(error); throw;
            }
        }
    }
}
