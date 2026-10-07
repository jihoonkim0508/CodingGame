// Run through Unity MCP eval_file. Uses the actual compiler and combat simulation.
var failures = new System.Collections.Generic.List<string>();
System.Action<bool, string> check = (ok, name) => { if (!ok) failures.Add(name); };
var kinds = new[] { CodingGame.BlockCoding.BlockKind.Slash, CodingGame.BlockCoding.BlockKind.Block,
    CodingGame.BlockCoding.BlockKind.Boom, CodingGame.BlockCoding.BlockKind.Shot, CodingGame.BlockCoding.BlockKind.Slow };
foreach (var kind in kinds)
{
    var source = CodingGame.BlockCoding.PythonTreeCompiler.Compile("robot", new[] { new CodingGame.BlockCoding.CodeBlock(kind) });
    check(source.Contains("(enemy)"), "Default enemy: " + kind);
    var explicitSource = CodingGame.BlockCoding.PythonTreeCompiler.Compile("robot", new[] {
        new CodingGame.BlockCoding.CodeBlock(kind, "", new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.NearestEnemy)) });
    check(explicitSource.Contains("get_nearest_enemy()"), "Explicit target: " + kind);
    bool rejected = false;
    try { CodingGame.BlockCoding.PythonTreeCompiler.Compile("robot", new[] {
        new CodingGame.BlockCoding.CodeBlock(kind, "", new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Number, "5")) }); }
    catch (System.FormatException) { rejected = true; }
    check(rejected, "Reject numeric target: " + kind);
}
System.Func<int, CodingGame.Defense.DefenseSimulation> make = limit => {
    var sim = new CodingGame.Defense.DefenseSimulation(new CodingGame.Defense.BattleSetup {
        FunctionRepeatLimit = limit, Min = new System.Numerics.Vector2(-20,-20), Max = new System.Numerics.Vector2(20,20),
        ActionProfiles = System.Enum.GetValues(typeof(CodingGame.Defense.RobotRole)).Cast<CodingGame.Defense.RobotRole>()
            .Select(role => new CodingGame.Defense.RobotSpec { role=role, range=3, damage=10, interval=.1f }).ToArray(),
        Routes = new[] { new CodingGame.Defense.Route(new[] { new System.Numerics.Vector2(-10,0), new System.Numerics.Vector2(10,0) }, 1) },
        Waves = new[] { new[] { new CodingGame.Defense.SpawnGroup { Enemy = new CodingGame.Defense.EnemySpec(), Count = 1, Delay = 100, Interval = 1 } } }
    });
    sim.Robots.Add(new CodingGame.Defense.RobotState { Id = 100, Position = System.Numerics.Vector2.Zero,
        Spec = new CodingGame.Defense.RobotSpec { role = CodingGame.Defense.RobotRole.Warrior, range = 3, damage = 10, interval = .1f }, RangeSetting = 3 });
    sim.Start(); return sim;
};
var battle = make(5);
var points = new[] { new System.Numerics.Vector2(2,0), new System.Numerics.Vector2(1,1), new System.Numerics.Vector2(0,3),
    new System.Numerics.Vector2(-1,0), new System.Numerics.Vector2(4,0) };
for (int i=0;i<points.Length;i++) battle.Enemies.Add(new CodingGame.Defense.EnemyState { Id=200+i, Position=points[i], Health=100, Spec=new CodingGame.Defense.EnemySpec() });
check(battle.RequestAction(100,CodingGame.Defense.RobotAction.Slash,200)==CodingGame.Defense.ActionResult.Executed,"Slash executed");
check(battle.Enemies.Take(3).All(e=>e.Health==90),"Same damage in front semicircle, including boundary");
check(battle.Enemies.Skip(3).All(e=>e.Health==100),"Rear and outside range untouched");
check(battle.Events.Count(e=>e.Kind=="slash")==1 && battle.Events.Count(e=>e.Kind=="hit")==3,"One slash visual, one hit per victim");
check(battle.Events.Single(e=>e.Kind=="slash").Origin==System.Numerics.Vector2.Zero,"Robot-centered slash origin");
foreach (int limit in new[]{5,1})
{
    var sim=make(limit); var bot=sim.Robots[0];
    sim.Enemies.Add(new CodingGame.Defense.EnemyState { Id=200,Position=new System.Numerics.Vector2(2,0),Health=10000,Spec=new CodingGame.Defense.EnemySpec() });
    bot.Program=new CodingGame.Defense.DefenseProgram("robot",new[]{new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Slash)});
    for(int i=0;i<600;i++) { bot.NextAction=0; bot.Program.Tick(sim,bot); }
    check(bot.Executions==limit && bot.Program.CompletedRuns==limit,"Function execution limit "+limit);
}
var unlock=make(5);unlock.Setup.PlayerFlow=true;
check(unlock.FunctionRepeatLimit==5,"Before loop unlock");unlock.GrantBlock(CodingGame.BlockCoding.BlockKind.For,1);
check(unlock.FunctionRepeatLimit==1,"After loop unlock");
var loopSim=make(1);var loopBot=loopSim.Robots[0];
loopSim.Enemies.Add(new CodingGame.Defense.EnemyState { Id=200,Position=new System.Numerics.Vector2(2,0),Health=10000,Spec=new CodingGame.Defense.EnemySpec() });
var loop=new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.For,"",new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Number,"10"));
loop.Body.Add(new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Slash));
loopBot.Program=new CodingGame.Defense.DefenseProgram("robot",new[]{loop});
for(int i=0;i<600;i++){loopBot.NextAction=0;loopBot.Program.Tick(loopSim,loopBot);}
check(loopBot.Executions==10 && loopBot.Program.CompletedRuns==1,"User loop runs ten attacks in one function invocation");
var turned=make(5);turned.Robots[0].Position=new System.Numerics.Vector2(4,5);
turned.Enemies.Add(new CodingGame.Defense.EnemyState { Id=200,Position=new System.Numerics.Vector2(4,3),Health=100,Spec=new CodingGame.Defense.EnemySpec() });
turned.Enemies.Add(new CodingGame.Defense.EnemyState { Id=201,Position=new System.Numerics.Vector2(4,7),Health=100,Spec=new CodingGame.Defense.EnemySpec() });
turned.RequestAction(100,CodingGame.Defense.RobotAction.Slash,200);
check(turned.Enemies[0].Health==90 && turned.Enemies[1].Health==100,"Semicircle rotates toward designated enemy");
check(turned.Events.Single(e=>e.Kind=="slash").Origin==turned.Robots[0].Position,"Moved robot remains slash pivot");
if(failures.Count>0)throw new System.Exception(string.Join("; ",failures));
return "PASS: five target signatures/type checks, 180-degree equal-damage splash, visual event, 5/1 function limits";
