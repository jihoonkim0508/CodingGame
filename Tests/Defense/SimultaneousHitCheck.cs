using System;
using System.Linq;
using System.Numerics;
using CodingGame.BlockCoding;
using CodingGame.Defense;

// Robots hitting one enemy at the same moment (2026-11-19 question "데미지가 씹히는 경우"): every hit on a living enemy lands
// with its own event, the second reads the health the first left, and a hit after the kill is the only one dropped.
static class SimultaneousHitCheck
{
    static int checks;
    static void Assert(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
    static DefenseSimulation Make(float health) => new DefenseSimulation(new BattleSetup {
        Progression = FixtureProgression.Create(), Min = new Vector2(-20, -10), Max = new Vector2(20, 10),
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role = r, damage = 7, interval = 1.6f, range = 3, projectileSpeed = 8 }).ToArray(),
        Routes = new[] { new Route(new[] { new Vector2(-10, 0), new Vector2(10, 0) }, 1) },
        Waves = new[] { new[] { new SpawnGroup { Enemy = new EnemySpec { health = health, speed = .01f }, Count = 1, Interval = 1 } } } });
    static RobotState Robot(DefenseSimulation sim, RobotRole role, float x)
    {
        var bot = sim.Place(new RobotSpec { role = role, range = 3, damage = 7, interval = 1.6f }, (int)role, new Vector2(x, 2), out var error);
        Assert(bot != null, "placed: " + error);
        sim.ApplyProgram(bot.Id, "idle_" + bot.Id, new[] { new CodeBlock(BlockKind.Wait, "0", new CodeBlock(BlockKind.Number, "100")) });
        return bot;
    }
    static EnemyState Spawned(DefenseSimulation sim)
    {
        sim.Start();
        while (sim.Enemies.Count == 0 && sim.Time < 5) sim.Advance(1.0 / 60);
        sim.Events.Clear();
        return sim.Enemies[0];
    }
    static void Until(DefenseSimulation sim, Func<bool> done) { while (!done() && sim.Time < 30) sim.Advance(1.0 / 60); }

    public static void Run()
    {
        // Two warriors swing in the same tick, so both strikes land in the same tick: 20 - 7 - 7 = 6, two hit events.
        var sim = Make(20);
        var a = Robot(sim, RobotRole.Warrior, -10); var b = Robot(sim, RobotRole.Warrior, -9);
        var enemy = Spawned(sim);
        Assert(sim.RequestAction(a.Id, RobotAction.Slash) == ActionResult.Executed && sim.RequestAction(b.Id, RobotAction.Slash) == ActionResult.Executed, "both swing");
        Until(sim, () => sim.Events.Count(e => e.Kind == "hit") >= 2);
        sim.Advance(.5);
        var hits = sim.Events.Where(e => e.Kind == "hit").ToArray();
        Assert(Math.Abs(enemy.Health - 6) < 1e-4 && hits.Length == 2, $"two same-tick strikes: hp {enemy.Health}, hits {hits.Length}");
        Assert(hits.Select(h => h.Source).Distinct().Count() == 2 && hits.All(h => h.Amount == 7 && h.AppliedAmount == 7), "one 7 from each warrior");

        // Three warriors on 14 HP in the same tick: two hits kill it, the third (on a dead enemy) is the only one dropped.
        sim = Make(14);
        var bots = new[] { Robot(sim, RobotRole.Warrior, -11), Robot(sim, RobotRole.Warrior, -10), Robot(sim, RobotRole.Warrior, -9) };
        enemy = Spawned(sim);
        foreach (var bot in bots) Assert(sim.RequestAction(bot.Id, RobotAction.Slash) == ActionResult.Executed, "swing");
        Until(sim, () => !enemy.Active);
        sim.Advance(.5);
        hits = sim.Events.Where(e => e.Kind == "hit").ToArray();
        Assert(!enemy.Active && enemy.Health == 0 && sim.Kills == 1 && sim.Events.Count(e => e.Kind == "killed") == 1, "14 HP dies to two of three strikes");
        Assert(hits.Length == 2 && hits.Sum(h => h.AppliedAmount) == 14 && bots.Sum(r => r.DamageThisWave) == 14 && bots.Sum(r => r.KillsThisWave) == 1,
            $"events and robot totals agree with the health lost: hits {hits.Length} applied {hits.Sum(h => h.AppliedAmount)}");

        // A shooter's shot and a warrior's strike: the shot fired 0-59 ticks after the swing, so in some runs both land in the
        // same tick. Every run: health lost = sum of the hit events = the robots' totals, one event per landed hit.
        int together = 0;
        for (int k = 0; k < 60; k++)
        {
            sim = Make(1000);
            var shooter = Robot(sim, RobotRole.Shooter, -11); var warrior = Robot(sim, RobotRole.Warrior, -9);
            enemy = Spawned(sim);
            Assert(sim.RequestAction(warrior.Id, RobotAction.Slash) == ActionResult.Executed, "warrior swings");
            for (int i = 0; i < k; i++) sim.Advance(DefenseSimulation.TickDuration);
            Assert(sim.RequestAction(shooter.Id, RobotAction.Attack, enemy.Id) == ActionResult.Executed, "shooter fires");
            for (int i = 0; i < 120; i++)
            {
                int before = sim.Events.Count;
                sim.Advance(DefenseSimulation.TickDuration);
                var tick = sim.Events.Skip(before).ToArray();
                if (tick.Any(e => e.Kind == "hit") && tick.Any(e => e.Kind == "projectile-hit")) together++;
            }
            var landed = sim.Events.Where(e => e.Kind == "hit" || e.Kind == "projectile-hit").ToArray();
            Assert(landed.Length == 2 && landed.All(e => e.Amount == 7 && e.AppliedAmount == 7) && Math.Abs(enemy.Health - 986) < 1e-3 &&
                shooter.DamageThisWave == 7 && warrior.DamageThisWave == 7, $"shot {k} ticks after the swing: hits {landed.Length}, hp {enemy.Health}");
        }
        Assert(together >= 1, "some run lands the shot and the strike in the same tick");

        // Two shots landing together on 10 HP: the first takes 7, the second the 3 left (applied), then the kill.
        sim = Make(10);
        var s1 = Robot(sim, RobotRole.Shooter, -11); var s2 = Robot(sim, RobotRole.Shooter, -9);
        enemy = Spawned(sim);
        sim.RequestAction(s1.Id, RobotAction.Attack, enemy.Id); sim.RequestAction(s2.Id, RobotAction.Attack, enemy.Id);
        sim.Advance(1);
        hits = sim.Events.Where(e => e.Kind == "projectile-hit").ToArray();
        Assert(!enemy.Active && hits.Length == 2 && hits.Sum(h => h.AppliedAmount) == 10 && sim.Kills == 1, $"two shots: hits {hits.Length} applied {hits.Sum(h => h.AppliedAmount)}");
        Console.WriteLine($"Simultaneous hit checks passed: {checks}");
    }
}
