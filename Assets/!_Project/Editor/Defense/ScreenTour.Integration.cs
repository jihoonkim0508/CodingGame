using System.Collections;
using System.Linq;
using CodingGame.BlockCoding;
using UnityEngine;
using Point = System.Numerics.Vector2;

namespace CodingGame.Defense.Editor
{
    // The 2026-11-26 integration as a player meets it (the support-monster models, the battle presentation, the three robots
    // and team play together): TestResults/Editor/integration/ stills and clips a_team (4-1: the board's editor, the fight with
    // job cards, holograms and the rift), b_stack (3-4: the crate pile), c_crowd (2-5, melee robots at one bend: one
    // rolled-up damage number per enemy, the slash under the bloom). Windowed Editor, like RunTeam:
    //   Unity.exe -projectPath . -executeMethod CodingGame.Defense.Editor.ScreenTour.RunIntegration -logFile integration.log
    public static partial class ScreenTour
    {
        public static void RunIntegration() => Start("TestResults/Editor/integration", Integration());

        static IEnumerator Integration()
        {
            DefenseSelectionCamera.InputLocked = true; // the real pointer may rest on the Game view's edge
            // a: 4-1, the board as RunTeam sets it up; its editor first (no robot stats card beside the board).
            DevTest.PlayDefense(4, 1, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(1);
            var battle = DevTest.Battle; var sim = battle.Simulation;
            Deploy();
            Relaxed(() =>
            {
                sim.ApplyBoard(TutorialAnswers.Parse("jobs = []\npriority = {Enemy.Healer: 0, Enemy.Shielder: 1, Enemy.Normal: 2}\n"));
                foreach (var bot in sim.Robots.Where(r => r.Spec.role == RobotRole.Support)) sim.ApplyProgram(bot.Id, "scout", TutorialAnswers.Parse(TeamSupport));
                foreach (var bot in sim.Robots.Where(r => r.Spec.role == RobotRole.Ranged)) sim.ApplyProgram(bot.Id, "sniper_" + bot.Id, TutorialAnswers.Parse(TeamRanged));
            });
            Relaxed(() => battle.CodeEditor.OpenBoard()); battle.CodeEditor.Panel.RefreshPalette();
            yield return Seconds(1.2);
            yield return Shot("team_board_editor");
            battle.CodeEditor.Close();
            yield return Seconds(.3);
            battle.StartBattle(); DevTest.Speed(2);
            yield return Until(() => sim.Board.Variables.TryGetValue("jobs", out var jobs) && jobs.Items.Count >= 1 && sim.Robots.Any(r => r.Job != 0), 60);
            DevTest.Speed(1);
            Begin("a_team");
            yield return Seconds(2.5);
            // Right after a robot acts, while its code chip is up.
            int acted = sim.Robots.Sum(r => r.ActionsThisWave);
            yield return Until(() => sim.Robots.Sum(r => r.ActionsThisWave) > acted, 8);
            yield return Seconds(.25);
            yield return Shot("team_battle");
            yield return Seconds(17.5);
            End();

            // b: 3-4, every robot on the nearest enemy, so the stack's pile grows (their hits spark on its bottom crate).
            DevTest.PlayDefense(3, 4, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy(); sim = DevTest.Sim;
            DevTest.Battle.StartBattle(); DevTest.Speed(3);
            yield return Until(() => sim.Enemies.Count(e => e.Active && e.Spec.type == EnemyType.Stack) >= 4, 120);
            DevTest.Speed(1);
            var pile = sim.Enemies.FirstOrDefault(e => e.Active && e.Chain != null && e.Chain.Stack);
            if (pile != null) PlayerCamera.ZoomAt(View.WorldToScreenPoint(new Vector3(pile.Position.X, 0, pile.Position.Y)), .62f);
            Begin("b_stack");
            yield return Seconds(1.5);
            yield return Shot("stack_crates");
            yield return Seconds(14);
            End();
            Debug.Log("[ScreenTour] stack pile: " + sim.Enemies.Count(e => e.Active && e.Spec.type == EnemyType.Stack) + " layers");

            // c: 2-5's tank column into the melee robots round one bend (no shields, so every hit counts), the camera in close.
            DevTest.PlayDefense(2, 5, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            battle = DevTest.Battle; sim = battle.Simulation;
            DevTest.GrantAll();
            var road = sim.Setup.Routes[0].Points.ToArray();
            var bend = road[Mathf.Clamp(road.Length / 2, 1, road.Length - 1)];
            foreach (var role in new[] { RobotRole.Melee, RobotRole.Melee, RobotRole.Melee, RobotRole.Melee, RobotRole.Ranged, RobotRole.Ranged })
            {
                int index = Enumerable.Range(0, battle.RobotDefinitions.Count).First(i => battle.DefinitionRole(i) == role);
                var spec = battle.RobotDefinition(index).stats;
                var spot = Enumerable.Range(0, 81).Select(i => new Point(bend.X + (i % 9 - 4) * .9f, bend.Y + (i / 9 - 4) * .9f))
                    .Where(p => sim.PlacementError(spec, p) == null).OrderBy(p => Point.Distance(p, bend)).FirstOrDefault();
                if (spot != default) battle.PlaceRobot(index, new Vector3(spot.X, 0, spot.Y));
            }
            Relaxed(() =>
            {
                foreach (var bot in sim.Robots)
                    sim.ApplyProgram(bot.Id, "robot_" + bot.Id, TutorialAnswers.Parse(bot.Spec.role == RobotRole.Melee
                        ? "while True:\n    enemy = get_nearest_enemy()\n    if enemy.distance(get_my_pos()) <= my_attack_range():\n        slash()"
                        : "while True:\n    shoot(get_nearest_enemy())"));
            });
            Debug.Log("[ScreenTour] crowd: " + string.Join(", ", sim.Robots.Select(r => $"{r.Spec.role} at {r.Position.X:0.0},{r.Position.Y:0.0}")));
            battle.StartBattle(); DevTest.Speed(3);
            yield return Until(() => sim.Robots.Count(r => r.ActionsThisWave > 0) >= 3, 60);
            DevTest.Speed(1);
            PlayerCamera.ZoomAt(View.WorldToScreenPoint(new Vector3(bend.X, 0, bend.Y)), .6f);
            Begin("c_crowd");
            yield return Seconds(1.2);
            int Slashes() => sim.Robots.Where(r => r.Spec.role == RobotRole.Melee).Sum(r => r.ActionsThisWave);
            int slashes = Slashes();
            yield return Until(() => Slashes() > slashes, 6);
            yield return Seconds(.3); // the slash's ring is fullest as its strike lands
            yield return Shot("crowd_numbers_slash");
            yield return Seconds(1.4);
            yield return Shot("crowd_numbers_later");
            yield return Seconds(14);
            End();
            Debug.Log($"[ScreenTour] crowd: {sim.Phase}, kills {sim.Kills}, leaks {sim.WaveLeaks}");
            DefenseSelectionCamera.InputLocked = false;
        }
    }
}
