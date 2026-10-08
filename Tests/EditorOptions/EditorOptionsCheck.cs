using System;
using System.Linq;
using System.Numerics;
using CodingGame.Defense;

static class EditorOptionsCheck
{
    static void Require(bool ok,string reason) { if(!ok)throw new Exception(reason); }
    static DefenseSimulation Make()
    {
        return new DefenseSimulation(new BattleSetup {
            Min=new Vector2(-20,-20),Max=new Vector2(20,20),
            Routes=new[]{new Route(new[]{new Vector2(0,0),new Vector2(10,0)},1)},
            ActionProfiles=Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(r=>new RobotSpec{role=r}).ToArray(),
            Waves=new[]{new[]{new SpawnGroup{Enemy=new EnemySpec{speed=1,health=100},Count=3,Interval=1}}}
        });
    }
    static void Main()
    {
        var sim=Make();sim.Start();sim.Options.enemySpawn=false;sim.Advance(4);
        Require(sim.Spawned==0&&sim.RemainingEnemies==3,"disabled spawn must retain pending enemies");
        sim.Options.enemySpawn=true;sim.Advance(DefenseSimulation.TickDuration);
        Require(sim.Spawned==1,"resume must not burst overdue spawns");
        sim.Options.enemyMovement=false;var point=sim.Enemies[0].Position;sim.Advance(.5);
        Require(sim.Enemies[0].Position==point,"movement off");
        sim.TogglePause();double time=sim.Time;sim.StepOnce();
        Require(sim.Phase==BattlePhase.Paused&&Math.Abs(sim.Time-time-DefenseSimulation.TickDuration)<1e-8,"one paused tick");
        sim.Options.attackDamage=false;sim.TogglePause();
        var bot=sim.Place(new RobotSpec{role=RobotRole.Shooter,range=10},0,new Vector2(0,3),out var error);
        Require(bot!=null,error);sim.Advance(1.1);var target=sim.Enemies[0];float hp=target.Health;
        sim.RequestAction(bot.Id,RobotAction.Attack,target.Id);Require(target.Health==hp,"damage off keeps enemy health");
        var changed=Make().Setup;changed.BaseHealth=25;changed.ActionProfiles[(int)RobotRole.Shooter].damage=42;
        var liveRobots=changed.ActionProfiles.Select(p=>p.Copy()).ToArray();liveRobots[0]=bot.Spec.Copy();liveRobots[0].damage=42;
        var newEnemy=changed.Waves[0][0].Enemy;newEnemy.health=200;
        var phase=sim.Phase;var elapsed=sim.Time;var enemyPosition=target.Position;var spawned=sim.Spawned;
        sim.PendingDrops.Add(CodingGame.BlockCoding.BlockKind.Shot);sim.Projectiles.Add(new ProjectileState());
        sim.ApplyBalance(changed,liveRobots,new[]{newEnemy});
        Require(sim.BaseHealth==25&&bot.Spec.damage==42&&target.Health==200&&sim.Projectiles.Count==0,"live stats reset health and remove projectiles");
        Require(sim.Phase==phase&&sim.Time==elapsed&&sim.Spawned==spawned&&target.Position==enemyPosition&&sim.PendingDrops.Count==1,"live stats preserve progress and pending rewards");
        var flow=Make().Setup;flow.PlayerFlow=true;flow.Stage=1;flow.RobotLimit=1;
        flow.Progression=new DefenseProgression{lessons=Array.Empty<WaveLesson>(),robots=new[]{new RobotStock{role=RobotRole.Shooter,count=1}}};
        sim=new DefenseSimulation(flow);sim.Options.unlockAll=true;
        Require(sim.RobotUnlocked(RobotRole.Warrior)&&sim.RobotAvailable(RobotRole.Warrior)==0,"unlock not unlimited stock");
        sim.Options.inventoryLimit=false;Require(sim.RobotAvailable(RobotRole.Warrior)==int.MaxValue,"separate inventory bypass");
        sim.Options.unlockAll=false;Require(sim.RobotAvailable(RobotRole.Warrior)==0&&sim.Available(CodingGame.BlockCoding.BlockKind.While)==0,"inventory bypass must retain locks");sim.Options.unlockAll=true;
        sim.GrantAll();sim.GrantAll();Require(sim.RobotInventory[RobotRole.Warrior]==20,"additive robot grant");
        sim.Inventory[CodingGame.BlockCoding.BlockKind.Shot]=int.MaxValue;
        int robots=sim.RobotInventory[RobotRole.Warrior];try{sim.GrantAll();throw new Exception("overflow expected");}catch(OverflowException){}
        Require(sim.RobotInventory[RobotRole.Warrior]==robots,"overflow must not partially grant");
        var original=flow.Progression;var copy=original.Copy();copy.robots[0].count=20;Require(original.robots[0].count==1,"deep progression copy");
        var rules=new DefenseBalanceRules();var e=new EnemySpec{health=10,speed=2};var scaled=rules.Scale(e,2,1);
        Require(Math.Abs(scaled.health-13.24f)<.001f&&e.health==10,"preserved growth formula and source");
        Console.WriteLine("PASS: spawn clock, movement, one tick, damage, unlock/stock, additive/atomic grants, copy, growth");
    }
}
