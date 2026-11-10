using System;
using System.Linq;
using System.Numerics;
using CodingGame.Defense;

// Placement is a little looser (9차 102): a robot may stand nearer the road's edge, a neighbour, the map's edge and an
// obstacle than before, while the road itself, a robot's spot and the map outside stay closed.
static class PlacementCheck
{
    static int checks;
    static void Assert(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
    static RobotSpec Spec(RobotRole role, float radius = .75f) => new RobotSpec { role = role, range = 3, damage = 10, interval = 1, radius = radius };
    static DefenseSimulation Make() => new DefenseSimulation(new BattleSetup {
        Progression = FixtureProgression.Create(), Min = new Vector2(-12, -8), Max = new Vector2(12, 8),
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => Spec(r)).ToArray(),
        Routes = new[] { new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1) },
        Obstacles = new[] { new PlacementObstacle { Min = new Vector2(5.5f, 4.5f), Max = new Vector2(6.5f, 5.5f) } },
        Waves = new[] { new[] { new SpawnGroup { Enemy = new EnemySpec(), Count = 1, Interval = 1 } } } });

    public static void Run()
    {
        var sim = Make();
        var warrior = Spec(RobotRole.Warrior);
        // Road: half-width 1, radius .75. Before, the body had to clear the road by .25 (1 + .75 + .25 = 2.0).
        Assert(sim.PlacementError(warrior, new Vector2(-5, 1.8f)) == null, "a warrior 1.8 from the road's middle now fits (was 2.0)");
        Assert(sim.PlacementError(warrior, new Vector2(-5, 1.3f)) == "적 경로에는 탱커만 설치할 수 있습니다.", "a warrior over the road is still turned away");
        // A tank may hang a little over the road's edge (before: its whole body inside the half-width).
        Assert(sim.PlacementError(Spec(RobotRole.Tank, .5f), new Vector2(-5, .6f)) == null, "a tank whose body hangs a bit over the road's edge fits");
        Assert(sim.PlacementError(Spec(RobotRole.Tank, .5f), new Vector2(-5, 1.2f)) == "탱커는 몸체가 경로 안에 들어가도록 설치하세요.", "a tank beside the road is still turned away");
        // Neighbours: two radii .75 needed 1.62 apart; now 1.35 is enough, the same spot is not.
        var first = sim.Place(warrior, 1, new Vector2(-5, 4), out var error);
        Assert(first != null, "first warrior placed: " + error);
        Assert(sim.PlacementError(warrior, new Vector2(-3.6f, 4)) == null, "a warrior 1.4 from another fits (was 1.62)");
        Assert(sim.PlacementError(warrior, new Vector2(-4.5f, 4)) == "다른 로봇과 겹칩니다.", "a warrior on top of another is turned away");
        // The map's edge: the body may hang a little past it, the centre may not.
        Assert(sim.PlacementError(warrior, new Vector2(-11.5f, 6)) == null, "a warrior .5 from the map's edge fits (body .75)");
        Assert(sim.PlacementError(warrior, new Vector2(-12.2f, 6)) == "설치 허용 영역 밖입니다.", "a warrior past the map's edge is turned away");
        // Obstacles: a body may touch the box's corner but not stand on it.
        Assert(sim.PlacementError(warrior, new Vector2(6, 6.1f)) == null, "a warrior .6 past an obstacle's edge fits (body .75)");
        Assert(sim.PlacementError(warrior, new Vector2(6, 5.5f)) == "장애물 또는 목표 시설과 겹칩니다.", "a warrior on an obstacle is turned away");
        Console.WriteLine($"PlacementCheck: {checks} checks passed");
    }
}
