using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using CodingGame.Defense;
using CodingGame.Defense.Editor;

// Three robots in two classes (2026-11-25): melee and ranged (공격류) do every attack action natively and differ only in
// range; support (지원류) buffs and slows; the other class's action works at reducedStrength and reducedIntervalMultiplier.
static class ClassCheck
{
    static int checks;
    static void Assert(bool ok, string name) { if (!ok) throw new Exception("ClassCheck: " + name); checks++; }
    static readonly RobotAction[] AttackActions = { RobotAction.Attack, RobotAction.Slash, RobotAction.Boom, RobotAction.Block, RobotAction.BreakShield };
    static DefenseSimulation Make() => new DefenseSimulation(new BattleSetup {
        Progression = FixtureProgression.Create(), Min = new Vector2(-20, -10), Max = new Vector2(20, 10),
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r => new RobotSpec { role = r }).ToArray(),
        Routes = new[] { new Route(new[] { new Vector2(-15, 0), new Vector2(15, 0) }, 1) },
        Waves = new[] { new[] { new SpawnGroup { Enemy = new EnemySpec { health = 100000, speed = .01f }, Count = 1, Interval = 1 } } } });
    static RobotState Place(DefenseSimulation sim, RobotRole role, float x, float range = 30) =>
        sim.Place(new RobotSpec { role = role, range = range, damage = 8, interval = 1, projectileSpeed = 1000 }, (int)role, new Vector2(x, 3), out _)
        ?? throw new Exception("ClassCheck: placed " + role);
    static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

    public static void Run()
    {
        Assert(Rules.RoleCount == 3 && (int)RobotRole.Support == 0 && (int)RobotRole.Melee == 1 && (int)RobotRole.Ranged == 2, "three roles; Support and Melee keep Buffer's and Warrior's ints");
        foreach (RobotRole role in Enum.GetValues(typeof(RobotRole)))
            foreach (RobotAction action in Enum.GetValues(typeof(RobotAction)))
                Assert(Rules.Compatibility(role, action) == (Rules.AttackClass(role) == !Rules.SupportAction(action) ? ActionCompatibility.Native : ActionCompatibility.Reduced),
                    $"class compatibility {role}/{action}");
        Assert(Rules.NativeAction(RobotRole.Melee) == RobotAction.Slash && Rules.NativeAction(RobotRole.Ranged) == RobotAction.Attack && Rules.NativeAction(RobotRole.Support) == RobotAction.Buff, "native actions");

        // The penalty numbers: 25% of the effect and twice the cooldown, both ways.
        var sim = Make(); var rules = sim.Setup.RulesConfig;
        Assert(rules.reducedStrength == .25f && rules.reducedIntervalMultiplier == 2, "penalty is 25% strength, 2x cooldown");
        var melee = Place(sim, RobotRole.Melee, -2, 3.6f); var ranged = Place(sim, RobotRole.Ranged, 0, 7); var support = Place(sim, RobotRole.Support, 2, 5);
        foreach (var action in AttackActions)
        {
            Assert(sim.ActionDamage(melee, action) == 8 && sim.ActionDamage(ranged, action) == 8 && sim.ActionInterval(melee, action) == 1 && sim.ActionInterval(ranged, action) == 1, "attack class native: " + action);
            Assert(sim.ActionDamage(support, action) == 2 && sim.ActionInterval(support, action) == 2, "support attacking: 25%, 2x: " + action);
        }
        foreach (var action in new[] { RobotAction.Buff, RobotAction.Slow })
        {
            Assert(sim.ActionInterval(support, action) == 1 && sim.ActionInterval(melee, action) == 2 && sim.ActionInterval(ranged, action) == 2, "attack class supporting: 2x cooldown " + action);
            Assert(sim.ActionDamage(melee, action) == 2, "attack class supporting: 25% " + action);
        }
        sim.Start();
        Assert(sim.RequestAction(melee.Id, RobotAction.Buff) == ActionResult.Executed && ranged.Buffs.Single().Damage == 1 + .25f * .25f && melee.NextAction == sim.Time + 2,
            "a melee robot's buff works at a quarter, then waits twice as long");
        Assert(sim.RequestAction(support.Id, RobotAction.Buff) == ActionResult.Executed && ranged.Buffs.Any(b => b.Damage == 1.25f), "the support robot's buff is full");

        // Melee and ranged do every attack action and hit as hard; only the reach differs.
        foreach (var action in AttackActions)
        {
            var a = Make(); var near = Place(a, RobotRole.Melee, -14.5f); a.Start(); for (int i = 0; i < 30; i++) a.Advance(1.0 / 60);
            var b = Make(); var far = Place(b, RobotRole.Ranged, -14.5f); b.Start(); for (int i = 0; i < 30; i++) b.Advance(1.0 / 60);
            Assert(a.RequestAction(near.Id, action) == ActionResult.Executed && b.RequestAction(far.Id, action) == ActionResult.Executed, "both do " + action);
            for (int i = 0; i < 120; i++) { a.Advance(1.0 / 60); b.Advance(1.0 / 60); }
            Assert(a.Enemies[0].Health == b.Enemies[0].Health && a.Enemies[0].Health < 100000, "the same damage: " + action);
        }
        // The game's own robots: Warrior (melee) and Shooter (ranged) assets and Balance.md's rows match but for range.
        string Stats(string file) => Regex.Match(File.ReadAllText(Path.Combine(Root, "Assets/!_Project/Defense/Data", file)), @"  stats:\n((?:    .*\n)+)").Groups[1].Value.Replace("\r", "");
        string Without(string stats, params string[] fields) => string.Join("\n", stats.Split('\n').Where(l => !fields.Any(f => l.TrimStart().StartsWith(f + ":"))));
        string warrior = Stats("Warrior.asset"), shooter = Stats("Shooter.asset");
        Assert(Regex.IsMatch(warrior, @"role: 1\n") && Regex.IsMatch(shooter, @"role: 2\n") && Regex.IsMatch(Stats("Buffer.asset"), @"role: 0\n"), "robot assets carry Melee 1, Ranged 2, Support 0");
        Assert(Without(warrior, "role", "range") == Without(shooter, "role", "range") && warrior.Contains("range: 3.6") && shooter.Contains("range: 7"), "melee and ranged assets differ only in range (and role)");
        foreach (var dropped in new[] { "Tank.asset", "Bomber.asset", "Utility.asset" }) Assert(!File.Exists(Path.Combine(Root, "Assets/!_Project/Defense/Data", dropped)), "dropped robot gone: " + dropped);
        var data = GameDataTables.Parse(File.ReadAllText(Path.Combine(Root, "GameData/Award.md")), File.ReadAllText(Path.Combine(Root, "GameData/Balance.md")), 1.2f);
        var m = data.Robots.Single(r => r.Role == RobotRole.Melee); var r2 = data.Robots.Single(r => r.Role == RobotRole.Ranged);
        Assert(m.Damage == r2.Damage && m.Interval == r2.Interval && m.Range < r2.Range && data.Robots.Count == 3, "Balance.md: melee and ranged the same but for range");
        // Every serialized role in the campaign is one of the three.
        var roles = Regex.Matches(File.ReadAllText(Path.Combine(Root, "Assets/!_Project/Defense/Tutorial/Stage1/StageOneCampaign.asset")), @"- role: (\d+)").Select(x => int.Parse(x.Groups[1].Value)).ToArray();
        Assert(roles.Length > 0 && roles.All(v => v <= 2), "campaign roles are 0..2");
        Console.WriteLine($"Class checks passed: {checks} (other class: {rules.reducedStrength:0%} strength, {rules.reducedIntervalMultiplier}x cooldown)");
    }
}
