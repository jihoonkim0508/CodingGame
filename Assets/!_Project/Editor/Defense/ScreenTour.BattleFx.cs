using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CodingGame.Defense.Editor
{
    // The battle presentation (2026-11-25, DefenseBattleFx and the HUD's code hologram, coins and wave band) and the player's
    // battle camera (DefenseSelectionCamera), recorded into TestResults/Editor/battlefx/. Batchmode without -nographics, like
    // RunNewMonsters:
    //   Unity.exe -batchmode -projectPath . -executeMethod CodingGame.Defense.Editor.ScreenTour.RunBattleFx -logFile fx.log
    // - RunBattleFxMoment: one moment of 2-4 and of 3-4 photographed the same way before and after (the Nth kill, same robots and
    //   code: the simulation is deterministic);
    // - RunBattleFx: clips a_stage1 (1-3), b_2-4 (with the camera zooming in on the fight and back: wheel and WASD injected as
    //   input events, the camera's moves logged), c_3-4 (the boss: entrance, footsteps, 2x, its fall, the last kill),
    //   d_victory (2-5 cleared); stills of the code hologram, the zoomed fight, the healer and the segment arrow; the camera
    //   gate checked (keys do nothing with the code panel open); frame times with the effects on and off.
    public static partial class ScreenTour
    {
        public static void RunBattleFxMoment() => Start("TestResults/Editor/battlefx_moment", FxMoment());
        public static void RunBattleFx() => Start("TestResults/Editor/battlefx", BattleFx());

        static IEnumerator FxMoment()
        {
            DefenseSelectionCamera.InputLocked = true;
            foreach (var (stage, wave, kills) in new[] { (2, 4, 4), (3, 4, 7) })
            {
                DevTest.PlayDefense(stage, wave, true, Sandbox);
                yield return Until(() => DevTest.Ready);
                yield return Seconds(.5);
                Deploy();
                var sim = DevTest.Sim;
                DevTest.Battle.StartBattle(); DevTest.Speed(2);
                yield return Until(() => sim.Kills >= kills - 1, 90);
                DevTest.Speed(1);
                yield return Until(() => sim.Kills >= kills, 30);
                yield return Shot($"moment_{stage}-{wave}");
                Debug.Log($"[ScreenTour] moment {stage}-{wave} at {sim.Time:0.00} s, kills {sim.Kills}, enemies {sim.Enemies.Count(e => e.Active)}");
            }
        }

        public static void RunBattleFxVictory() => Start("TestResults/Editor/battlefx_victory", Victory());
        // d: Stage 2's last wave cleared (two of each enemy), the stage's victory with confetti.
        static IEnumerator Victory()
        {
            DefenseSelectionCamera.InputLocked = true;
            DevTest.PlayDefense(2, 10, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            var sim = DevTest.Sim;
            sim.SetWaveGroups(2, 9, sim.Setup.Waves[9].Select(g => new SpawnGroup { Enemy = g.Enemy, DefinitionIndex = g.DefinitionIndex, RouteIndex = g.RouteIndex,
                Count = System.Math.Min(g.Count, 2), Delay = g.Delay, Interval = g.Interval, Boss = g.Boss }).ToArray());
            DevTest.GrantAll(); Deploy(); DevTest.Battle.StartBattle(); DevTest.RunWave();
            Debug.Log($"[ScreenTour] 2-10 ran: {sim.Phase}");
            yield return Seconds(2.5);
            Begin("d_victory");
            yield return Seconds(.5);
            DevTest.Battle.ContinueCampaign();
            yield return Seconds(1.2);
            yield return Shot("victory");
            yield return Seconds(3.3);
            End();
            Debug.Log($"[ScreenTour] 2-10 after the claim: {DevTest.Sim.Phase}");
        }

        static DefenseSelectionCamera PlayerCamera => Object.FindFirstObjectByType<DefenseSelectionCamera>();
        static Camera View => DevTest.Battle.BattleCamera;
        // Input events as the devices would send them; every device input reaches the game view meanwhile (batchmode has no focus).
        static Mouse FakeMouse => Mouse.current ?? InputSystem.AddDevice<Mouse>();
        static Keyboard FakeKeyboard => Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        static IEnumerator Wheel(Vector2 at, int notches, double gap = .12)
        {
            for (int i = 0; i < Mathf.Abs(notches); i++)
            {
                InputSystem.QueueStateEvent(FakeMouse, new MouseState { position = at, scroll = new Vector2(0, notches > 0 ? 120 : -120) });
                yield return null;
                InputSystem.QueueStateEvent(FakeMouse, new MouseState { position = at });
                yield return Seconds(gap);
            }
        }
        static IEnumerator Hold(Key key, double seconds)
        {
            InputSystem.QueueStateEvent(FakeKeyboard, new KeyboardState(key));
            yield return Seconds(seconds);
            InputSystem.QueueStateEvent(FakeKeyboard, new KeyboardState());
            yield return Seconds(.3);
        }
        static string Pose() { var t = View.transform; return $"pos {t.position.x:0.00},{t.position.y:0.00},{t.position.z:0.00} zoom {PlayerCamera.Zoom:0.00}"; }
        static Vector2 ScreenOf(Vector3 world) => View.WorldToScreenPoint(world);

        static IEnumerator BattleFx()
        {
            var settings = InputSystem.settings;
            var open = Object.Instantiate(settings); open.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = open;
            DefenseSelectionCamera.InputLocked = true; // the real pointer may rest on the Game view's edge
            // Mouse in the middle, away from the edges.
            InputSystem.QueueStateEvent(FakeMouse, new MouseState { position = new Vector2(960, 540) });

            // a: Stage 1's straight road, its third wave from the start (the wave call, the rift, the road's flow, kills, coins).
            DevTest.PlayDefense(1, 3, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(3);
            Deploy();
            var sim = DevTest.Sim;
            yield return Seconds(.5);
            Begin("a_stage1");
            yield return Seconds(1.2);
            DevTest.Battle.StartBattle();
            yield return Seconds(9.5);
            End();

            // b: 2-4, the shielders; the camera zooms in on the fight with the wheel, pans with D, and zooms back out.
            DevTest.PlayDefense(2, 4, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy();
            sim = DevTest.Sim; var battle = DevTest.Battle;
            // The gate: with the code panel open, W does not move the camera.
            battle.SelectRobot(sim.Robots[0].Id); battle.DevelopSelected();
            yield return Seconds(.6);
            DefenseSelectionCamera.InputLocked = false;
            string before = Pose();
            yield return Hold(Key.W, .5);
            Debug.Log($"[ScreenTour] camera with the code panel open: {before} -> {Pose()} (should not move)");
            battle.CodeEditor.Close();
            yield return Seconds(.4);
            // Zoomed out, the wheel cannot go past the home view.
            before = Pose();
            yield return Wheel(new Vector2(960, 540), -3);
            yield return Seconds(.4);
            Debug.Log($"[ScreenTour] camera wheel out at home: {before} -> {Pose()} (should stay at zoom 1)");
            battle.StartBattle(); DevTest.Speed(2);
            yield return Until(() => sim.Kills >= 2, 60);
            DevTest.Speed(1);
            Begin("b_2-4");
            yield return Seconds(1.5);
            // Zoom in once an enemy is in a robot's reach.
            yield return Until(() => sim.Enemies.Any(e => e.Active && sim.Robots.Any(r => System.Numerics.Vector2.Distance(e.Position, r.Position) < 4.5f)), 15);
            Vector3 World(System.Numerics.Vector2 p) => new Vector3(p.X, 0, p.Y);
            // Between a robot and the enemy nearest to it.
            var pair = sim.Enemies.Where(e => e.Active).SelectMany(e => sim.Robots.Select(r => (e, r))).OrderBy(x => System.Numerics.Vector2.Distance(x.e.Position, x.r.Position)).FirstOrDefault();
            var fight = pair.e != null ? (World(pair.e.Position) + World(pair.r.Position)) / 2 : World(sim.Robots[0].Position);
            before = Pose();
            yield return Wheel(ScreenOf(fight), 6);
            yield return Seconds(.7);
            Debug.Log($"[ScreenTour] camera wheel in x6 at the fight: {before} -> {Pose()}");
            yield return Shot("zoomed_fight");
            before = Pose();
            yield return Hold(Key.D, .6);
            Debug.Log($"[ScreenTour] camera D 0.6 s: {before} -> {Pose()}");
            yield return Seconds(1.5);
            yield return Shot("hologram");
            yield return Hold(Key.A, .6);
            yield return Wheel(new Vector2(960, 540), -8, .08);
            yield return Seconds(.8);
            Debug.Log($"[ScreenTour] camera wheel out x8: {Pose()} (back to zoom 1)");
            DefenseSelectionCamera.InputLocked = true;
            yield return Seconds(3);
            End();

            // c: 3-4 with fewer minions so the boss comes soon: the healer and the segment arrow, the boss's entrance and
            // footsteps, a stretch at 2x, its fall with hit-stop, the last kill in slow motion and the clear.
            DevTest.PlayDefense(3, 4, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            sim = DevTest.Sim; battle = DevTest.Battle;
            var groups = sim.Setup.Waves[3].Select(g => new SpawnGroup { Enemy = g.Enemy, DefinitionIndex = g.DefinitionIndex, RouteIndex = g.RouteIndex,
                Count = g.Boss ? g.Count : System.Math.Min(g.Count, g.Enemy.type == EnemyType.Normal || g.Enemy.type == EnemyType.Fast ? 2 : 3), Delay = g.Delay, Interval = g.Interval, Boss = g.Boss }).ToArray();
            sim.SetWaveGroups(3, 3, groups);
            DevTest.GrantAll(); Deploy(); // every robot kind, so the boss falls (bomber blasts, buffs)
            OneShooter(sim, Priorities);
            battle.StartBattle(); DevTest.Speed(2);
            yield return Until(() => sim.Enemies.Any(e => e.Active && e.Spec.type == EnemyType.Healer) && sim.Enemies.Count(e => e.Active) >= 4, 60);
            DevTest.Speed(1);
            Begin("c_3-4");
            yield return Seconds(2.5);
            yield return Shot("healer");
            yield return Until(() => sim.Enemies.Any(e => e.Active && e.Chain != null && e.Chain.Hittable == e), 20);
            yield return Seconds(1.5);
            yield return Shot("segment_arrow");
            yield return Seconds(2);
            End();
            // Frame times (not recording) with everything on and with the world effects off (the HUD's parts stay), in turns.
            var fx = battle.GetComponent<DefenseBattleFx>();
            var on = new System.Collections.Generic.List<float>(); var off = new System.Collections.Generic.List<float>();
            for (int turn = 0; turn < 4; turn++)
            {
                fx.Shown = turn % 2 == 0; var into = fx.Shown ? on : off;
                yield return Seconds(.3);
                for (double end = UnityEditor.EditorApplication.timeSinceStartup + 2.5; UnityEditor.EditorApplication.timeSinceStartup < end;) { into.Add(Time.unscaledDeltaTime); yield return null; }
            }
            fx.Shown = true;
            string Stats(System.Collections.Generic.List<float> f) { var s = f.Skip(5).OrderBy(x => x).ToList(); return s.Count == 0 ? "-" : $"{s.Count} frames, mean {s.Average() * 1000:0.0} ms, p95 {s[(int)(s.Count * .95f)] * 1000:0.0} ms, max {s.Last() * 1000:0.0} ms"; }
            Debug.Log($"[ScreenTour] frame times 3-4 1x, effects on: {Stats(on)}; effects off: {Stats(off)}");
            // c2: 3-4 again with only two minions before the boss, the boss at 55% health (the recording's lineup cannot
            // always hold every minion of the full wave: a leaked one cancels the boss).
            DevTest.PlayDefense(3, 4, true, Sandbox);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            sim = DevTest.Sim; battle = DevTest.Battle;
            bool firstMinions = true;
            var bossWave = sim.Setup.Waves[3].Where(g => g.Boss || g.Enemy.type == EnemyType.Normal && firstMinions && !(firstMinions = false)).Select(g =>
            {
                var spec = JsonUtility.FromJson<EnemySpec>(JsonUtility.ToJson(g.Enemy));
                if (g.Boss) spec.health *= .55f;
                return new SpawnGroup { Enemy = spec, DefinitionIndex = g.DefinitionIndex, RouteIndex = g.RouteIndex, Count = g.Boss ? g.Count : 2, Delay = g.Boss ? g.Delay : 0, Interval = g.Interval, Boss = g.Boss };
            }).ToArray();
            sim.SetWaveGroups(3, 3, bossWave);
            DevTest.GrantAll(); Deploy();
            battle.StartBattle(); DevTest.Speed(3);
            yield return Until(() => battle.EditorBossEntered || sim.Phase != BattlePhase.Running, 120);
            DevTest.Speed(1);
            Begin("c2_boss");
            yield return Seconds(7);
            DevTest.Speed(2); // 2x: the footsteps, the dust and the road's flow keep the battle's pace
            yield return Seconds(4);
            DevTest.Speed(1);
            var bossKilled = false;
            for (double end = UnityEditor.EditorApplication.timeSinceStartup + 40; sim.Phase == BattlePhase.Running && UnityEditor.EditorApplication.timeSinceStartup < end;)
            {
                // Hurry the long middle of the fight, but show its end at 1x.
                var boss = sim.Enemies.FirstOrDefault(e => e.Active && e.Boss);
                DevTest.Speed(boss != null && boss.Health > boss.Spec.health * .25f ? 3 : 1);
                if (boss != null && boss.Health <= boss.Spec.health * .25f && !bossKilled) { bossKilled = true; Debug.Log("[ScreenTour] boss low, 1x"); }
                yield return null;
            }
            DevTest.Speed(1);
            yield return Seconds(3.5);
            End();
            Debug.Log($"[ScreenTour] 3-4: {sim.Phase}, core {sim.BaseHealth}, leaks {sim.WaveLeaks}, kills {sim.Kills}");

            yield return Victory();
            if (settings) InputSystem.settings = settings;
            DefenseSelectionCamera.InputLocked = false;
        }
    }
}
