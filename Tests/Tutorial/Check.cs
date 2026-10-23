using System;
using System.Linq;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.Defense.Editor;

static class Check
{
    static int checks;
    static void Require(bool b, string label) { if (!b) throw new Exception(label); checks++; }
    static void Main()
    {
        const string basic = "enemy = get_nearest_enemy()\nslash(enemy)";
        const string conditional = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nattack_range = my_attack_range()\nif enemy_distance <= attack_range:\n    slash(enemy)";
        string Loop(int n, string body) => $"for i in range({n}):\n" + string.Join("\n", body.Split('\n').Select(l => "    " + l));
        DefenseProgram Program(string s) => new DefenseProgram("robot", TutorialAnswers.Parse(s));
        foreach (string answer in new[] { basic, conditional, Loop(5,basic), Loop(10,basic), Loop(10,conditional) })
        {
            Require(TutorialWatcher.Matches(Program(answer),answer), "Answer roundtrip");
            Require(TutorialWatcher.Matches(Program(System.Text.RegularExpressions.Regex.Replace(answer, @"\benemy\b", "적")),answer), "Renamed variables");
            Require(TutorialWatcher.Matches(Program(answer),"\n"+answer+"\n"), "Blank lines");
        }
        Require(!TutorialWatcher.Matches(Program(Loop(5,basic)),Loop(10,basic)), "Wrong repeat count rejected");
        Require(!TutorialWatcher.Matches(Program("enemy = get_nearest_enemy()\nfor i in range(5):\n    slash(enemy)"),Loop(5,basic)), "Stale target outside loop rejected");
        string wrongScope = "for i in range(10):\n    enemy = get_nearest_enemy()\n    enemy_distance = enemy.distance(get_my_pos())\n    attack_range = my_attack_range()\n    if enemy_distance <= attack_range:\n        attack_range = my_attack_range()\n    slash(enemy)";
        Require(!TutorialWatcher.Matches(Program(wrongScope),Loop(10,conditional)), "Attack outside if rejected");
        // Same lines, different nesting: both programs compile, only one repeats its distance check.
        string outside = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nattack_range = my_attack_range()\nfor i in range(10):\n    if enemy_distance <= attack_range:\n        slash(enemy)";
        Require(!TutorialWatcher.Matches(Program(outside),Loop(10,conditional)), "Distance outside loop rejected");
        var blocks = TutorialAnswers.Parse(Loop(10,conditional));
        Require(blocks.Length == 1 && blocks[0].Body.Count == 4 && blocks[0].Body[3].Body.Single().Kind == BlockKind.Slash, "Nested block parser");
        string attackOutside = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nattack_range = my_attack_range()\nif enemy_distance <= attack_range:\n    attack_range = my_attack_range()\nslash(enemy)";
        Require(!TutorialWatcher.Matches(Program(attackOutside),conditional), "Non-loop attack outside if rejected");
        Require(!TutorialWatcher.Matches(Program("enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nattack_range = my_attack_range()\nif enemy_distance <= attack_range:\n    enemy_distance = enemy.distance(get_my_pos())\nslash(enemy)"),conditional), "Incorrect body rejected");
        FinaleRules();
        Console.WriteLine($"PASS: {checks} tutorial answer, scope and finale rules checks");
    }
    static void FinaleRules()
    {
        var profiles=Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r=>new RobotSpec {role=r}).ToArray();
        var normal=new SpawnGroup { Enemy=new EnemySpec {health=20,speed=1,leakDamage=1},Count=1,Interval=1 };
        var boss=new SpawnGroup { Enemy=new EnemySpec {health=40,speed=1,leakDamage=12},Count=1,Interval=1,WaitForClear=true };
        var setup=new BattleSetup {
            Stage=1, InitialWave=4, BaseHealth=12, PlayerFlow=true, WaveSeconds=120, PreviousStageRewards=false,
            ActionProfiles=profiles, Min=new System.Numerics.Vector2(-12,-8), Max=new System.Numerics.Vector2(12,8),
            Routes=new[]{new Route(new[]{new System.Numerics.Vector2(-10,0),new System.Numerics.Vector2(10,0)},1.1f)},
            Waves=Enumerable.Range(0,5).Select(i=>i==4?new[]{normal,boss}:new[]{normal}).ToArray(),
            Progression=new DefenseProgression {lessons=Array.Empty<WaveLesson>(), robots=new[]{new RobotStock {role=RobotRole.Warrior,count=1}}}
        };
        var sim=new DefenseSimulation(setup);
        var robot=sim.Place(profiles[1],1,new System.Numerics.Vector2(-8,2),out var error);
        Require(robot!=null,"Retry fixture robot: "+error);
        int coins=sim.Coins, owned=sim.Owned(BlockKind.Number);
        sim.Start(); sim.Advance(1d/60);
        Require(sim.FinaleBossId==0 && sim.Spawned==1,"Boss waits for normal enemies");
        var enemy=sim.Enemies.Single(); enemy.Active=false;
        sim.Advance(1d/60);
        Require(sim.Spawned==2 && sim.FinaleBossId!=0 && sim.Phase==BattlePhase.Running,"Boss releases in same tick without preparation");
        Require(sim.Enemies.Single().IsFinaleBoss,"Spawned boss marker");
        for(int i=0;i<3000 && sim.Phase==BattlePhase.Running;i++) sim.Advance(1d/60);
        Require(sim.Phase==BattlePhase.Defeat && sim.BaseHealth==0,"Boss reaching core fails encounter");
        Require(sim.RetryFinale(),"Retry accepted on failure");
        Require(sim.Phase==BattlePhase.Ready && sim.WaveIndex==4 && sim.BaseHealth==12,"Retry restores only wave 1-5");
        Require(sim.Coins==coins && sim.Owned(BlockKind.Number)==owned && sim.Spawned==0 && sim.Enemies.Count==0 && sim.PendingDrops.Count==0,"Retry has no combat leftovers or duplicate grants");
        Require(sim.Robots.Single().Id==robot.Id && sim.Robots.Single().Position==robot.Position,"Retry retains robot layout");
        Require(sim.FinaleBossId==0 && !sim.FinaleBossAttacked,"Retry resets boss signals");
        sim.Start(); sim.Advance(1d/60);
        Require(sim.Spawned==1 && sim.FinaleBossId==0,"Retry starts with normal enemies");
        sim.Enemies.Single().Active=false; sim.Advance(1d/60);
        sim.Enemies.Single().Active=false; sim.Advance(1d/60);
        Require(sim.Phase==BattlePhase.Reward,"Boss defeat reaches reward");
        Require(!sim.RetryFinale(),"Retry unavailable after success");
    }

}
