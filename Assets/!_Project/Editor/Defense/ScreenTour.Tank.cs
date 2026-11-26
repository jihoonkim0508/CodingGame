using System.Collections;
using System.Linq;
using CodingGame.BlockCoding;
using UnityEditor;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // 로봇 4종 (2026-11-26): the deck with the four robots (3-1's preparation), the 2-4 lesson where the tank comes (its card,
    // then the guard line once it stands by the road), the tank's code editor (blocks and Python), a 2-4 battle with the tank
    // holding a line of enemies under its guard bar, and the moment it is knocked back. TestResults/Editor/tank/NN_*.png,
    // 1920x1080. Batchmode without -nographics:
    //   Unity.exe -batchmode -projectPath . -executeMethod CodingGame.Defense.Editor.ScreenTour.RunTank -logFile tank.log
    public static partial class ScreenTour
    {
        public static void RunTank() => Start("TestResults/Editor/tank", TankTour());

        static int Held(DefenseSimulation sim, RobotState tank) => sim.Enemies.Count(e => e.Active && e.BlockedBy == tank.Id);
        // A point `side` units left of the road (of its walking direction), `along` of the way along it.
        static System.Numerics.Vector2 Beside(Route road, float along, float side)
        {
            var p = road.Points; float total = 0;
            for (int i = 1; i < p.Length; i++) total += System.Numerics.Vector2.Distance(p[i - 1], p[i]);
            float left = total * along;
            for (int i = 1; i < p.Length; i++)
            {
                float length = System.Numerics.Vector2.Distance(p[i - 1], p[i]);
                if (left > length && i < p.Length - 1) { left -= length; continue; }
                var dir = System.Numerics.Vector2.Normalize(p[i] - p[i - 1]);
                return p[i - 1] + dir * System.Math.Min(left, length) + new System.Numerics.Vector2(-dir.Y, dir.X) * side;
            }
            return p[0];
        }
        // The robot moved to the free spot nearest to `want` (a quarter-unit search).
        static void MoveNear(DefenseSimulation sim, RobotState bot, System.Numerics.Vector2 want)
        {
            for (float r = 0; r < 4; r += .25f)
                for (int k = 0; k < 16; k++)
                {
                    var at = want + r * new System.Numerics.Vector2(Mathf.Cos(k * Mathf.PI / 8), Mathf.Sin(k * Mathf.PI / 8));
                    if (sim.MoveRobot(bot.Id, at, out _)) return;
                    if (r == 0) break;
                }
            Debug.LogWarning("[ScreenTour] no spot for " + bot.Spec.role + " near " + want);
        }
        // The lesson's dialogue lines up to the step `id`, each typed out and passed as a player would.
        static IEnumerator LessonTo(TutorialDirector director, string id)
        {
            for (double end = EditorApplication.timeSinceStartup + 60; EditorApplication.timeSinceStartup < end;)
            {
                var step = director.EditorSynced ? director.EditorStep : null;
                if (step != null && step.stepId == id) { yield return Seconds(1.2); director.EditorFinishTyping(); yield return Seconds(.3); yield break; }
                if (step != null && step.kind != TutorialStepKind.ActionGuide) { director.EditorFinishTyping(); director.EditorNext(); yield return Seconds(.4); continue; }
                yield return null;
            }
            Debug.LogWarning("[ScreenTour] lesson never reached " + id + " (at " + director.EditorStep?.stepId + ")");
        }

        static IEnumerator TankTour()
        {
            // The deck: 3-1 starts with every robot 2-10's clear and the stage before gave (the utility robot among them).
            DevTest.PlayDefense(3, 1, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(1.5);
            var sim = DevTest.Sim;
            Debug.Log("[ScreenTour] 3-1 deck: " + string.Join(", ", System.Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => r + " " + sim.RobotAvailable(r))));
            yield return Shot("deck");

            // The 2-4 lesson: the tank's card, then the guard line once it stands by the road.
            DevTest.PlayDefense(2, 4, true, new DefenseTestOptions { tutorial = true, tutorialInput = true });
            yield return Until(() => DevTest.Ready);
            var battle = DevTest.Battle; var director = battle.Tutorial;
            yield return LessonTo(director, "A03");
            yield return Shot("lesson_card");
            battle.ChooseRobot((int)RobotRole.Tank);
            yield return Until(() => director.EditorStep?.stepId == "A04", 10);
            yield return Seconds(1);
            DevTest.PlaceTeachingRobot(battle, RobotRole.Tank);
            yield return LessonTo(director, "A06");
            yield return Shot("lesson_guard");

            // The tank's code: block(enemy) in a loop, in the blocks and in Python, the preview playing it.
            DevTest.PlayDefense(2, 4, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy();
            battle = DevTest.Battle; sim = battle.Simulation;
            var tank = sim.Robots.Single(r => r.Spec.role == RobotRole.Tank);
            Debug.Log("[ScreenTour] 2-4 robots: " + string.Join(", ", sim.Robots.Select(r => $"{r.Spec.role} at {r.Position.X:0.0},{r.Position.Y:0.0}")));
            battle.SelectRobot(tank.Id); battle.DevelopSelected();
            yield return Seconds(.6);
            Program(TutorialAnswers.Parse(TutorialSequenceBuilder.Code24));
            yield return Seconds(2.5);
            Debug.Log("[ScreenTour] tank code: " + battle.CodeEditor.Panel.AppliedSource);
            yield return Shot("tank_code_blocks");
            battle.CodeEditor.Panel.SetMode(CodingMode.Python);
            yield return Seconds(1);
            yield return Shot("tank_code_python");
            battle.CodeEditor.Panel.SetMode(CodingMode.Block);
            battle.CodeEditor.Close();
            yield return Seconds(.3);
            // For the battle shots the three stand by the open middle of the road, not at the spawn where the models hide the line.
            var road = sim.Setup.Routes[0];
            MoveNear(sim, tank, Beside(road, .45f, 1.6f));
            foreach (var bot in sim.Robots.Where(r => r != tank))
                MoveNear(sim, bot, tank.Position + (bot.Spec.role == RobotRole.Melee ? new System.Numerics.Vector2(-2.2f, -1.6f) : new System.Numerics.Vector2(-3.5f, 1.2f)));
            Debug.Log("[ScreenTour] moved: " + string.Join(", ", sim.Robots.Select(r => $"{r.Spec.role} at {r.Position.X:0.0},{r.Position.Y:0.0}")));
            yield return Seconds(.5);

            // The battle: the tank holds the road, a line forms behind the first enemy, the guard bar wears down. The others hold
            // their fire (a long cooldown, their code as it is) until the line is three long, then open fire on it.
            var others = sim.Robots.Where(r => r != tank).ToArray();
            void Hold(bool on) { foreach (var bot in others) bot.NextAction = on ? sim.Time + 999 : sim.Time; }
            battle.StartBattle(); Hold(true); DevTest.Speed(2);
            yield return Until(() => Held(sim, tank) >= 3 || sim.Phase != BattlePhase.Running, 60);
            DevTest.Speed(1);
            yield return Seconds(.3);
            Debug.Log($"[ScreenTour] held {Held(sim, tank)}, guard {tank.Guard:0.0}/{tank.MaxGuard:0}, phase {sim.Phase}");
            yield return Shot("tank_holding");
            Hold(false);
            yield return Seconds(1.2);
            yield return Shot("tank_holding_fire");

            // The knock-back: the others hold their fire again so the line wears the guard out.
            Hold(true);
            DevTest.Speed(2);
            yield return Until(() => !tank.Holding(sim.Time) || sim.Phase != BattlePhase.Running, 60);
            DevTest.Speed(1);
            yield return Seconds(.45);
            Debug.Log($"[ScreenTour] knocked back: {!tank.Holding(sim.Time)} until {tank.KnockedUntil:0.0} (time {sim.Time:0.0}), guard {tank.Guard:0.0}");
            yield return Shot("tank_knocked");
            yield return Seconds(1.6);
            yield return Shot("tank_knocked_later");
            DevTest.Speed(3);
            yield return Until(() => sim.Phase != BattlePhase.Running, 120);
            Debug.Log($"[ScreenTour] 2-4: {sim.Phase}, core {sim.BaseHealth}, leaks {sim.WaveLeaks}, held {tank.HeldThisWave}, knocked {tank.KnockedThisWave}");
            yield return Seconds(1.5);
            yield return Shot("debrief");
        }
    }
}
