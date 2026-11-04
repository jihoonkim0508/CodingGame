using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.Defense.Editor;

static class Check
{
    static int checks;
    static void Need(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    const string Slash = "enemy = get_nearest_enemy()\nslash(enemy)";
    const string Shot = "enemy = get_nearest_enemy()\nattack(enemy)";
    const string Condition = "enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nif enemy_distance <= 3:\n    attack(enemy)";
    static string Repeat(string code, int count = 5) => "for i in range(" + count + "):\n    " + code.Replace("\n", "\n    ");
    static RobotSpec Spec(RobotRole role) => new RobotSpec { role = role, range = role == RobotRole.Shooter ? 5.5f : 3.2f,
        damage = role == RobotRole.Shooter ? 10.78f : 6.86f, interval = role == RobotRole.Shooter ? 1.2f : .8f, projectileSpeed = 15 };
    static BattleSetup Setup(int stage, int wave) => new BattleSetup {
        Stage = stage, InitialWave = wave, PlayerFlow = true, Min = new Vector2(-12,-8), Max = new Vector2(12,8),
        ActionProfiles = Enum.GetValues(typeof(RobotRole)).Cast<RobotRole>().Select(Spec).ToArray(),
        Routes = new[] { new Route(new[] { new Vector2(7,0), new Vector2(-10,0) }, 1.2f) },
        Waves = Enumerable.Range(0,5).Select(_ => new[] { new SpawnGroup { Enemy = new EnemySpec { health = 1000, speed = 1.2f }, Count = 1, Interval = 1 } }).ToArray()
    };
    static RobotState Add(DefenseSimulation sim, RobotRole role, float x = 0) {
        var bot = sim.Place(Spec(role), (int)role, new Vector2(x,1.8f), out var error);
        Need(bot != null, "place: " + error); return bot;
    }
    static void Apply(DefenseSimulation sim, RobotState bot, string code) {
        sim.ApplyProgram(bot.Id, "robot", TutorialAnswers.Parse(code)); Need(bot.Program.Fault == null, "valid code");
    }
    static float Damage(bool condition) {
        var setup = Setup(0,0); setup.PlayerFlow = false;
        var sim = new DefenseSimulation(setup); var bot = Add(sim,RobotRole.Shooter);
        Apply(sim,bot,condition ? Condition : Shot); sim.Start(); bool falseSeen = false;
        for (int i=0;i<1200;i++) {
            sim.Advance(1.0/60); falseSeen |= bot.Program.LastCondition == false;
            var hit = sim.Events.FirstOrDefault(e=>(e.Kind=="hit" || e.Kind=="projectile-hit"));
            if (hit != null) { Need(bot.AttacksThisWave == 1,"one shot per comparison"); Need(!condition || falseSeen,"condition visibly skips distant shots"); return hit.Amount; }
        }
        throw new Exception("No preview hit");
    }
    static void Encounters() {
        string campaign=File.ReadAllText("Assets/!_Project/Defense/Tutorial/Stage1/StageOneCampaign.asset");
        var stages=Regex.Split(campaign.Substring(campaign.IndexOf("  stages:")),@"(?m)^  - waves:\r?$");
        float Field(string text,string name,float fallback=0) {
            var m=Regex.Match(text,@"(?m)^        "+name+@": ([\d.]+)");
            return m.Success?float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture):fallback;
        }
        for(int stage=1;stage<=2;stage++) {
            var waves=Regex.Split(stages[stage],@"(?m)^    - groups:\r?$").Skip(1).Select(w=>
                Regex.Split(w,@"(?m)^      - enemy:.*$").Skip(1).Select(g=>new SpawnGroup {
                    RouteIndex=(int)Field(g,"route"), Count=(int)Field(g,"count"), Delay=Field(g,"delay"), Interval=Field(g,"interval",1), Boss=Field(g,"boss")==1,
                    Enemy=new EnemySpec { health=Field(g,"healthOverride")>0?Field(g,"healthOverride"):20,
                        speed=Field(g,"speedOverride")>0?Field(g,"speedOverride"):1.4f,
                        leakDamage=(int)(Field(g,"leakDamageOverride")>0?Field(g,"leakDamageOverride"):1) }
                }).ToArray()).ToArray();
            Need(waves.Length==5,"five authored encounters");
            for(int wave=0;wave<5;wave++) {
                var setup=Setup(stage,wave); setup.Waves=waves; setup.WaveSeconds=120; setup.RobotLimit=6;
                setup.Routes=new[] { new Route(new[] {new Vector2(8,0),new Vector2(-10,0)},1), new Route(new[] {new Vector2(8,4),new Vector2(-10,4)},1) };
                var sim=new DefenseSimulation(setup);
                foreach(var role in new[]{RobotRole.Warrior,RobotRole.Shooter}) {
                    int count=sim.RobotAvailable(role);
                    for(int i=0;i<count;i++) {
                        var bot=sim.Place(Spec(role),(int)role,new Vector2(5-i*5,role==RobotRole.Warrior?1.8f:-1.8f),out var error);
                        Need(bot!=null,"authored encounter placement "+error);
                        string code=role==RobotRole.Warrior ? (stage==1&&wave<2?Slash:Repeat(Slash)) :
                            wave<2?Repeat(Shot):wave==2?(i==count-1?Condition:Repeat(Shot)):(i==count-1?Repeat(Condition):Repeat(Shot));
                        Apply(sim,bot,code);
                    }
                }
                Need(sim.StartError()==null,$"authored start {stage}-{wave+1}: "+sim.StartError());sim.Start();
                for(int i=0;i<8000&&sim.Phase==BattlePhase.Running;i++)sim.Advance(1.0/60);
                Need(sim.Phase==BattlePhase.Reward&&!sim.LastWaveTimedOut,$"encounter {stage}-{wave+1}: phase={sim.Phase}, kills={sim.Kills}, leaks={sim.Leaks}");
                Console.WriteLine($"Encounter {stage}-{wave+1}: kills={sim.Kills}, leaks={sim.Leaks}");
            }
        }
    }
    static void Main() {
        foreach(var spec in new[]{("Warrior",6.86f),("Shooter",10.78f),("Bomber",3.92f),("Tank",0.98f),("Utility",1.47f),("Buffer",0f)}) {
            string data=File.ReadAllText($"Assets/!_Project/Defense/Data/{spec.Item1}.asset");
            float value=float.Parse(Regex.Match(data,@"(?m)^    damage: ([\d.]+)").Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);
            Need(Math.Abs(value-spec.Item2)<.0001f,"30 percent damage reduction: "+spec.Item1);
        }
        PerEnemyCheck.Run();
        var lessons=DefenseCurriculum.Create();
        Need(lessons.Length==15,"15 lessons");
        var firstFor=lessons.First(l=>l.blocks.Any(b=>b.kind==BlockKind.For));
        var firstIf=lessons.First(l=>l.blocks.Any(b=>b.kind==BlockKind.If));
        Need(firstFor.stage==1 && firstFor.wave==3,"loops introduced at 1-3");
        Need(firstIf.stage==2 && firstIf.wave==3,"conditions introduced with ranged at 2-3");
        foreach(var l in lessons.Where(l=>l.stage==1)) Need(!l.blocks.Concat(l.rewards).Any(b=>b.kind==BlockKind.If || b.kind==BlockKind.Comparison),"no early conditions");
        // Use actual cumulative supplies, including previous robot reservations.
        var one=new DefenseSimulation(Setup(1,2)); var a=Add(one,RobotRole.Warrior,-2); var b=Add(one,RobotRole.Warrior,2);
        Apply(one,a,Repeat(Slash,3)); Apply(one,b,Slash); Need(one.StartError()==null,"1-3 can start with loop lesson");
        var four=new DefenseSimulation(Setup(1,3)); a=Add(four,RobotRole.Warrior,-3); b=Add(four,RobotRole.Warrior,0); var c=Add(four,RobotRole.Warrior,3);
        Apply(four,a,Repeat(Slash)); Apply(four,b,Slash); Apply(four,c,Repeat(Slash)); Need(four.StartError()==null,"1-4 requires available repeat programs, not conditions");
        for(int wave=0;wave<5;wave++) {
            var sim=new DefenseSimulation(Setup(2,wave)); var shooter=Add(sim,RobotRole.Shooter);
            string code=wave<2 ? Repeat(Shot) : wave==2 ? Condition : Repeat(Condition);
            Apply(sim,shooter,code); Need(sim.StartError()==null,"Stage 2 lesson start " + (wave+1));
            Need(sim.FunctionRepeatLimit==1,"single program invocation preserved");
            if(wave>=2) Need(DefenseSimulation.HasRangedCondition(shooter.Program),"ranged condition recognized inside or outside loop");
        }
        float baseline=Damage(false), guarded=Damage(true);
        Need(guarded>baseline*1.5f,"closer conditional shot materially increases damage");
        Console.WriteLine($"Comparison: unguarded={baseline:0.00}, distance <=3={guarded:0.00}");
        // All live sequence references must still be present in their stage scenes.
        for(int stage=1;stage<=2;stage++) for(int wave=1;wave<=5;wave++) {
            string path=$"Assets/!_Project/Defense/Tutorial/Stage{stage}/Tutorial_{stage}-{wave}.asset";
            string asset=File.ReadAllText(path); var ids=Regex.Matches(asset,@"(?m)^  - stepId: (.+)$").Select(m=>m.Groups[1].Value).ToArray();
            Need(ids.Distinct().Count()==ids.Length,"unique steps "+path);
            string guid=Regex.Match(File.ReadAllText(path+".meta"),@"guid: (\w+)").Groups[1].Value;
            Need(File.ReadAllText($"Assets/!_Project/Scenes/stage{stage}.unity").Contains(guid),"scene keeps sequence reference");
            Need(asset.Contains($"  stage: {stage}")&&asset.Contains($"  wave: {wave}"),"correct sequence location");
        }
        string conditionAsset=File.ReadAllText("Assets/!_Project/Defense/Tutorial/Stage2/Tutorial_2-3.asset");
        Need(conditionAsset.Contains("3.0.1|3") && conditionAsset.Contains("completionSignal: DraftValueMatches"),"numeric condition has input completion step");
        Need(!conditionAsset.Contains("Editor.Palette.Slash") && conditionAsset.Contains("Editor.Palette.Shot"),"condition lesson targets ranged attack palette");
        Encounters();
        Console.WriteLine("PASS curriculum order, real inventory, start gates, ranged damage and scene wiring: "+checks);
    }
}
