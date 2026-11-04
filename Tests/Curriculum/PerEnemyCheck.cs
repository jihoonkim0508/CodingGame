using System;
using System.Linq;
using System.Numerics;
using CodingGame.Defense;
using CodingGame.BlockCoding;
using CodingGame.Defense.Editor;

static class PerEnemyCheck
{
    static void Need(bool ok,string message) { if(!ok) throw new Exception(message); }
    static DefenseSimulation Make(bool staggered=true, float health=1000) => new DefenseSimulation(new BattleSetup {
        Min=new Vector2(-12,-5), Max=new Vector2(12,5),
        ActionProfiles=Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r=>Spec(r)).ToArray(),
        Routes=new[]{new Route(new[]{new Vector2(0,0),new Vector2(-10,0)},.01f)},
        Waves=new[]{new[]{new SpawnGroup {Enemy=new EnemySpec {health=health,speed=.01f},Count=1,Interval=1},
            new SpawnGroup {Enemy=new EnemySpec {health=1000,speed=.01f},Count=1,Delay=staggered?4:0,Interval=1}}}
    });
    static RobotSpec Spec(RobotRole role) => new RobotSpec {role=role,damage=14,range=4,interval=.5f,projectileSpeed=20};
    static RobotState Add(DefenseSimulation sim,RobotRole role,string code,float x=0) {
        var bot=sim.Place(Spec(role),(int)role,new Vector2(x,2),out var error);
        Need(bot!=null,error);sim.ApplyProgram(bot.Id,"robot",TutorialAnswers.Parse(code));return bot;
    }
    static void Advance(DefenseSimulation sim,double seconds) {for(int i=0;i<seconds*60;i++)sim.Advance(1.0/60);}
    static void SingleTargetAndLock()
    {
        foreach(var role in new[]{RobotRole.Warrior,RobotRole.Bomber})
        {
            var sim=Make(false);var bot=sim.Place(Spec(role),(int)role,new Vector2(0,2),out _);
            var kind=role==RobotRole.Warrior?BlockKind.Slash:BlockKind.Boom;
            sim.ApplyProgram(bot.Id,"one",new[]{new CodeBlock(kind,"0",new CodeBlock(BlockKind.NearestEnemy))});
            sim.Start();
            for(int i=0;i<180&&!sim.Events.Any(e=>e.Kind=="hit"||e.Kind=="projectile-hit");i++)sim.Advance(1.0/60);
            Need(sim.Enemies.Count(e=>e.Health<1000)==1,"Wide visual must damage only one monster: "+role);
            Need(sim.Events.Any(e=>e.Kind==(role==RobotRole.Warrior?"slash":"blast")&&e.Radius>0),"Wide effect radius retained: "+role);
        }
        // The nearest enemy changes during the loop, but all three attacks must still use the first target.
        var locked=Make(false);var actor=Add(locked,RobotRole.Warrior,"for i in range(3):\n    enemy = get_nearest_enemy()\n    slash(enemy)");
        locked.Start();while(!locked.Events.Any(e=>e.Kind=="slash"))locked.Advance(1.0/60);
        int original=locked.Events.First(e=>e.Kind=="slash").Target;
        locked.Enemies.First(e=>e.Id==original).Position=new Vector2(2,0);
        locked.Enemies.First(e=>e.Id!=original).Position=new Vector2(0,1);
        Advance(locked,4);
        var ordered=locked.Events.Where(e=>e.Kind=="slash").Select(e=>e.Target).ToArray();
        Need(ordered.Length==6&&ordered.Take(3).All(id=>id==original)&&ordered.Skip(3).All(id=>id!=original),"Target changes only after the three-iteration loop ends");
        // An early kill must not redirect the remaining loop iterations. A wait makes the boundary observable.
        var early=Make(false,5);var robot=early.Place(Spec(RobotRole.Warrior),1,new Vector2(0,2),out _);
        var loop=TutorialAnswers.Parse("for i in range(3):\n    enemy = get_nearest_enemy()\n    slash(enemy)")[0];
        loop.Body.Add(new CodeBlock(BlockKind.Wait,"0",new CodeBlock(BlockKind.Number,"1")));
        early.ApplyProgram(robot.Id,"wait_loop",new[]{loop});early.Start();Advance(early,2.5);
        Need(early.Kills==1&&robot.AttacksThisWave==1&&robot.Program.CompletedRuns==0,"Dead target remains locked while the loop finishes");
        Advance(early,2);
        Need(robot.Program.CompletedRuns>=1&&robot.AttacksThisWave>=2,"Next monster becomes attackable after the loop finishes");
        // An explosion must not transfer its damage when its designated victim dies in flight.
        var lost=Make(false);var bomber=lost.Place(Spec(RobotRole.Bomber),3,new Vector2(0,2),out _);
        lost.ApplyProgram(bomber.Id,"bomb",new[]{new CodeBlock(BlockKind.Boom,"0",new CodeBlock(BlockKind.NearestEnemy))});
        lost.Start();while(lost.Projectiles.Count==0)lost.Advance(1.0/60);
        int victim=lost.Projectiles[0].Target;lost.Enemies.First(e=>e.Id==victim).Active=false;
        for(int i=0;i<120&&!lost.Events.Any(e=>e.Kind=="blast");i++)lost.Advance(1.0/60);
        Need(lost.Events.Any(e=>e.Kind=="blast")&&lost.Enemies.Where(e=>e.Active).All(e=>e.Health==1000),"Missing bomb target preserves explosion but causes no collateral damage");
        Console.WriteLine("PASS single-target slash/blast, visual radii, target lock, early death, delayed impacts");
    }
    public static void Run() {
        bool rejected=false;
        try { new DefenseProgram("too_many",TutorialAnswers.Parse("for i in range(6):\n    enemy = get_nearest_enemy()\n    slash(enemy)")); }
        catch(FormatException) { rejected=true; }
        Need(rejected,"Literal loop counts over five are rejected before execution");
        var dynamicSim=Make();var dynamicBot=Add(dynamicSim,RobotRole.Warrior,"count = 6\nfor i in range(count):\n    enemy = get_nearest_enemy()\n    slash(enemy)");
        dynamicSim.Start();Advance(dynamicSim,2);
        Need(dynamicBot.Program.Fault!=null&&dynamicBot.AttacksThisWave==0,"Variable loop counts over five are rejected at runtime");
        var bounded=Make();var boundedBot=bounded.Place(Spec(RobotRole.Warrior),1,new Vector2(0,2),out _);
        var whileBlock=new CodeBlock(BlockKind.While);
        whileBlock.Body.Add(new CodeBlock(BlockKind.Slash,"0",new CodeBlock(BlockKind.NearestEnemy)));
        bounded.ApplyProgram(boundedBot.Id,"bounded_while",new[]{whileBlock});bounded.Start();Advance(bounded,3.5);
        Need(boundedBot.AttacksThisWave==5&&boundedBot.Program.Completed(bounded),"While also stops at the five-iteration limit");
        SingleTargetAndLock();
        foreach(var role in new[]{RobotRole.Warrior,RobotRole.Shooter}) {
            string action=role==RobotRole.Warrior?"slash":"attack";
            var sim=Make();var bot=Add(sim,role,"enemy = get_nearest_enemy()\n"+action+"(enemy)");sim.Start();Advance(sim,3);
            Need(bot.AttacksThisWave==1 && sim.Enemies[0].Health>0,"Basic attack must stop after one hit on a surviving monster");
            Need(bot.Program.Completed(sim),"Wait after finishing the current monster");Advance(sim,4);
            Need(bot.AttacksThisWave==2 && bot.Program.CompletedRuns==2,"Later monster restarts the program exactly once");
            var hits=sim.Events.Where(e=>(e.Kind=="hit"||e.Kind=="projectile-hit")&&e.Source==bot.Id).GroupBy(e=>e.Target).ToArray();
            Need(hits.Length==2&&hits.All(g=>g.Count()==1),"Each monster receives one basic hit, including area attacks");
            sim=Make();bot=Add(sim,role,"enemy = get_nearest_enemy()\nfor i in range(3):\n    "+action+"(enemy)");
            sim.Start();Advance(sim,8);
            var attacks=sim.Events.Where(e=>e.Source==bot.Id&&e.Kind==(role==RobotRole.Warrior?"slash":"shot")).GroupBy(e=>e.Target).ToArray();
            Need(attacks.Length==2&&attacks.All(g=>g.Count()==3),"Explicit loop repeats three times per monster, including a captured target outside the loop");
        }
        var area=Make(false);var warrior=Add(area,RobotRole.Warrior,"enemy = get_nearest_enemy()\nslash(enemy)");area.Start();Advance(area,4);
        Need(warrior.AttacksThisWave==2,"Each monster needs its own swing even when their visual areas overlap");
        Need(area.Events.Where(e=>e.Kind=="hit").GroupBy(e=>e.Target).All(g=>g.Count()==1),"No double hit from area target bookkeeping");
        var team=Make();var first=Add(team,RobotRole.Shooter,"enemy = get_nearest_enemy()\nattack(enemy)",-2);
        var second=Add(team,RobotRole.Shooter,"enemy = get_nearest_enemy()\nattack(enemy)",2);team.Start();Advance(team,8);
        Need(first.AttacksThisWave==2&&second.AttacksThisWave==2,"Each robot has independent per-monster history");
        var boss=Make(true,5);var hunter=Add(boss,RobotRole.Warrior,"enemy = get_nearest_enemy()\nfor i in range(5):\n    slash(enemy)");
        boss.Start();Advance(boss,11);
        Need(boss.Kills==1&&hunter.AttacksThisWave==6,"Dead captured target must not prevent a later boss from getting its own five attacks");
        Console.WriteLine("PASS per-enemy attacks: delayed targets, surviving enemies, explicit loops, ranged/melee, area hits, independent robots, later boss");
    }
}
