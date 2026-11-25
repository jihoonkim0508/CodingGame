using System.Collections;
using System.Linq;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // The 2-4 / 3-4 monsters' models and effects in real play (SupportMonsterArt): TestResults/Editor/newmonsters/ clips
    // (a_shielder.mp4: 2-4, shields and break_shield; b_healer, c_queue, d_stack: 3-4) and one still of each, 1920x1080.
    // One shooter runs the lesson's code, the rest shoot the nearest enemy (their hits on protected segments and books spark 막힘).
    // Batchmode without -nographics:
    //   Unity.exe -batchmode -projectPath . -executeMethod CodingGame.Defense.Editor.ScreenTour.RunNewMonsters -logFile new.log
    public static partial class ScreenTour
    {
        public static void RunNewMonsters() => Start("TestResults/Editor/newmonsters", NewMonsters());

        const string BreakShields = "while True:\n    e = get_nearest_enemy()\n    if e.shield > 0:\n        break_shield(e)\n    else:\n        shoot(e)";
        const string Priorities = "while True:\n    target = get_nearest_enemy()\n    for e in get_enemies():\n" +
            "        if e.type == Enemy.Healer:\n            target = e\n            break\n" +
            "        if e.type == Enemy.Stack:\n            target = e.top()\n            break\n" +
            "        if e.type == Enemy.Queue:\n            target = e.front()\n            break\n    shoot(target)";

        static IEnumerator NewMonsters()
        {
            // 2-4: shielders bubble their neighbours; the first shooter breaks shields.
            DevTest.PlayDefense(2, 4, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy();
            var sim = DevTest.Sim;
            OneShooter(sim, BreakShields);
            DevTest.Battle.StartBattle(); DevTest.Speed(3);
            yield return Until(() => sim.Enemies.Any(e => e.Active && e.Spec.type == EnemyType.Shielder) && sim.Enemies.Any(e => e.Active && e.Shield > 0), 90);
            DevTest.Speed(1);
            yield return Seconds(.4);
            yield return Shot("보호막병");
            Begin("a_shielder");
            yield return Seconds(10);
            End();

            // 3-4: healers, then the queue centipede, then the stack's pile of books.
            DevTest.PlayDefense(3, 4, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy();
            sim = DevTest.Sim;
            OneShooter(sim, Priorities);
            DevTest.Battle.StartBattle(); DevTest.Speed(2);
            yield return Until(() => sim.Enemies.Any(e => e.Active && e.Spec.type == EnemyType.Healer) && sim.Enemies.Any(e => e.Active && e.Spec.type == EnemyType.Healer && sim.Time - e.LastAbility > 1.8), 90);
            DevTest.Speed(1);
            Begin("b_healer");
            yield return Until(() => sim.Enemies.Any(e => e.Active && e.Spec.type == EnemyType.Healer && sim.Time - e.LastAbility < .15), 6);
            yield return Shot("힐러");
            yield return Seconds(6);
            End();
            DevTest.Speed(3);
            yield return Until(() => sim.Enemies.Count(e => e.Active && e.Spec.type == EnemyType.Queue) >= 4, 90);
            DevTest.Speed(1);
            Begin("c_queue");
            yield return Seconds(3);
            yield return Shot("큐 지네");
            yield return Seconds(6);
            End();
            DevTest.Speed(3);
            yield return Until(() => sim.Enemies.Count(e => e.Active && e.Spec.type == EnemyType.Stack) >= 3, 90);
            DevTest.Speed(1);
            Begin("d_stack");
            yield return Seconds(3);
            yield return Shot("스택 책더미");
            yield return Seconds(9);
            End();
            Debug.Log($"[ScreenTour] new monsters done: 3-4 {sim.Phase}, kills {sim.Kills}, leaks {sim.WaveLeaks}");
        }

        static void OneShooter(DefenseSimulation sim, string code)
        {
            var shooter = sim.Robots.FirstOrDefault(r => r.Spec.role == RobotRole.Shooter);
            if (shooter != null) Relaxed(() => sim.ApplyProgram(shooter.Id, "lesson", TutorialAnswers.Parse(code)));
        }
    }
}
