// Run through Unity MCP eval_file. Uses the actual compiler and combat simulation.
var failures = new System.Collections.Generic.List<string>();
System.Action<bool, string> check = (ok, name) => { if (!ok) failures.Add(name); };
var kinds = new[] { CodingGame.BlockCoding.BlockKind.Block, CodingGame.BlockCoding.BlockKind.Boom,
    CodingGame.BlockCoding.BlockKind.Shot, CodingGame.BlockCoding.BlockKind.Slow };
foreach (var kind in kinds)
{
    bool empty = false;
    try { CodingGame.BlockCoding.PythonTreeCompiler.Compile("robot", new[] { new CodingGame.BlockCoding.CodeBlock(kind) }); }
    catch (System.FormatException) { empty = true; }
    check(empty, "Empty target is a compile error: " + kind);
    var explicitSource = CodingGame.BlockCoding.PythonTreeCompiler.Compile("robot", new[] {
        new CodingGame.BlockCoding.CodeBlock(kind, "", new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.NearestEnemy)) });
    check(explicitSource.Contains("get_nearest_enemy()"), "Explicit target: " + kind);
    bool rejected = false;
    try { CodingGame.BlockCoding.PythonTreeCompiler.Compile("robot", new[] {
        new CodingGame.BlockCoding.CodeBlock(kind, "", new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Number, "5")) }); }
    catch (System.FormatException) { rejected = true; }
    check(rejected, "Reject numeric target: " + kind);
}
// slash() takes no target.
check(CodingGame.BlockCoding.PythonTreeCompiler.Compile("robot", new[] { new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Slash) }).Contains("slash()"), "slash() compiles without a target");
bool targetRejected = false;
try { CodingGame.BlockCoding.PythonTreeCompiler.Compile("robot", new[] { new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Slash, "",
    new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.NearestEnemy)) }); }
catch (System.FormatException) { targetRejected = true; }
check(targetRejected, "slash() rejects a target");
System.Func<int, CodingGame.Defense.DefenseSimulation> make = limit => {
    var sim = new CodingGame.Defense.DefenseSimulation(new CodingGame.Defense.BattleSetup {
        FunctionRepeatLimit = limit, Min = new System.Numerics.Vector2(-20,-20), Max = new System.Numerics.Vector2(20,20),
        ActionProfiles = System.Enum.GetValues(typeof(CodingGame.Defense.RobotRole)).Cast<CodingGame.Defense.RobotRole>()
            .Select(role => new CodingGame.Defense.RobotSpec { role=role, range=3, damage=10, interval=.1f }).ToArray(),
        Routes = new[] { new CodingGame.Defense.Route(new[] { new System.Numerics.Vector2(-10,0), new System.Numerics.Vector2(10,0) }, 1) },
        Waves = new[] { new[] { new CodingGame.Defense.SpawnGroup { Enemy = new CodingGame.Defense.EnemySpec(), Count = 1, Delay = 100, Interval = 1 } } }
    });
    sim.Robots.Add(new CodingGame.Defense.RobotState { Id = 100, Position = System.Numerics.Vector2.Zero,
        Spec = new CodingGame.Defense.RobotSpec { role = CodingGame.Defense.RobotRole.Warrior, range = 3, damage = 10, interval = .1f } });
    sim.Start(); return sim;
};
var battle = make(5);
var points = new[] { new System.Numerics.Vector2(2,0), new System.Numerics.Vector2(1,1), new System.Numerics.Vector2(0,3),
    new System.Numerics.Vector2(-1,0), new System.Numerics.Vector2(0,-2), new System.Numerics.Vector2(4,0) };
for (int i=0;i<points.Length;i++) battle.Enemies.Add(new CodingGame.Defense.EnemyState { Id=200+i, Position=points[i], Health=100, Spec=new CodingGame.Defense.EnemySpec { speed = 0 } });
check(battle.RequestAction(100,CodingGame.Defense.RobotAction.Slash)==CodingGame.Defense.ActionResult.Executed,"Slash executed without a target");
battle.Advance(.1); // the sword gets there after the wind-up
check(battle.Enemies.Take(5).All(e=>e.Health==90),"Same damage all around within range, including the boundary and behind");
check(battle.Enemies.Skip(5).All(e=>e.Health==100),"Outside range untouched");
check(battle.Events.Count(e=>e.Kind=="slash")==1 && battle.Events.Count(e=>e.Kind=="hit")==5,"One slash visual, one hit per victim");
check(battle.Events.Single(e=>e.Kind=="slash").Origin==System.Numerics.Vector2.Zero,"Robot-centered slash origin");
foreach (int limit in new[]{5,1})
{
    var sim=make(limit); var bot=sim.Robots[0];
    sim.Enemies.Add(new CodingGame.Defense.EnemyState { Id=200,Position=new System.Numerics.Vector2(2,0),Health=10000,Spec=new CodingGame.Defense.EnemySpec() });
    bot.Program=new CodingGame.Defense.DefenseProgram("robot",new[]{new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Slash)});
    for(int i=0;i<600;i++) { bot.NextAction=0; bot.Program.Tick(sim,bot); }
    check(bot.Executions==limit && bot.Program.CompletedRuns==limit,"Function execution limit "+limit);
}
check(new CodingGame.Defense.BattleSetup().FunctionRepeatLimit==1,"The robot function runs once a wave by default");
var loopSim=make(1);var loopBot=loopSim.Robots[0];
loopSim.Enemies.Add(new CodingGame.Defense.EnemyState { Id=200,Position=new System.Numerics.Vector2(2,0),Health=10000,Spec=new CodingGame.Defense.EnemySpec() });
var loop=new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.For,"",new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Number,"10"));
loop.Body.Add(new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Slash));
loopBot.Program=new CodingGame.Defense.DefenseProgram("robot",new[]{loop});
for(int i=0;i<600;i++){loopBot.NextAction=0;loopBot.Program.Tick(loopSim,loopBot);}
check(loopBot.Executions==10 && loopBot.Program.CompletedRuns==1,"User loop runs ten attacks in one function invocation");
var moved=make(5);moved.Robots[0].Position=new System.Numerics.Vector2(4,5);
moved.Enemies.Add(new CodingGame.Defense.EnemyState { Id=200,Position=new System.Numerics.Vector2(4,3),Health=100,Spec=new CodingGame.Defense.EnemySpec { speed = 0 } });
moved.Enemies.Add(new CodingGame.Defense.EnemyState { Id=201,Position=new System.Numerics.Vector2(4,7),Health=100,Spec=new CodingGame.Defense.EnemySpec { speed = 0 } });
moved.RequestAction(100,CodingGame.Defense.RobotAction.Slash);moved.Advance(.1);
check(moved.Enemies.All(e=>e.Health==90),"Enemies on opposite sides are both hit");
check(moved.Events.Single(e=>e.Kind=="slash").Origin==moved.Robots[0].Position,"Moved robot remains slash pivot");
if(failures.Count>0)throw new System.Exception(string.Join("; ",failures));
return "PASS: target signatures/type checks, targetless slash(), 360-degree equal-damage splash, visual event, 5/1 function limits";
