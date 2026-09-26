// Run with Unity MCP eval_file while Defense.unity is playing. Resets this test run.
if (!UnityEditor.EditorApplication.isPlaying) return "Play Mode required";
var battle = UnityEngine.GameObject.Find("DefenseBattle").GetComponent<CodingGame.Defense.DefenseBattle>();
var editor = battle.GetComponent<CodingGame.Defense.DefenseCodeEditor>();
var preview = battle.GetComponent<CodingGame.Defense.DefenseAttackPreview>();
var label = (TMPro.TMP_Text)new UnityEditor.SerializedObject(preview).FindProperty("status").objectReferenceValue;
int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new System.Exception(message); checks++; }
string Stock() => string.Join(",", battle.Simulation.Inventory.OrderBy(p=>p.Key).Select(p=>p.Key+":"+p.Value));
void Show(params CodingGame.BlockCoding.CodeBlock[] blocks)
{
    if (preview.IsShowing) preview.TogglePreview();
    editor.Panel.LoadProgram("guard",blocks);
    preview.TogglePreview();
}
void Status() => preview.SendMessage("RefreshStatus");
void Advance(float seconds) => preview.SendMessage("AdvancePreview",seconds);
try
{
    battle.Restart();
    Check(battle.PlaceRobot(2,new UnityEngine.Vector3(-8,0,4)),"place authored tank");
    battle.SelectRobot(battle.SelectedId);
    battle.Simulation.GrantBlock(CodingGame.BlockCoding.BlockKind.Buff,1);
    string stock = Stock(); var time = battle.Simulation.Time; var baseHealth = battle.Simulation.BaseHealth;
    var actualTank = battle.Simulation.Robots.Single(); var health = actualTank.Health;
    Show(); Status();
    Check(label.text.Contains("코드 없음")&&label.text.Contains("저지 비활성"),"empty tank status");
    Show(new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Block));
    preview.PreviewSimulation.Advance(2.5); Status();
    Check(label.text.Contains("저지 3/3")&&label.text.Contains("한도 도달"),"occupied capacity status");
    Check(label.text.Contains("재사용 대기"),"cooldown distinct from body blocking");
    Check(preview.PreviewSimulation.Enemies.All(e=>e.BlockedBy!=0&&e.Position.X>0),"authored preview enemies intercepted");
    UnityEngine.Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate();
    Check(label.preferredHeight<=label.rectTransform.rect.height&&label.preferredWidth<=label.rectTransform.rect.width,"status fits authored panel");
    preview.SendMessage("ResetPreview"); Status();
    Check(preview.PreviewSimulation.Robots.Count==1&&preview.PreviewSimulation.Enemies.Count==0&&label.text.Contains("저지 비활성"),"loop resets stance and actors");
    preview.PreviewSimulation.Advance(2.5); Status();
    Check(label.text.Contains("저지 3/3"),"loop restores blocking through code execution");
    var held=preview.PreviewSimulation;
    for(int i=0;i<100;i++) Advance(.1f);
    Check(preview.PreviewSimulation==held&&held.Time>12&&held.Enemies.Count==3,"living blocked enemies are not reset after eight seconds");
    Show(new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Wait,"0",new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Number,"10")),
        new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Block));
    var waiting=preview.PreviewSimulation;
    for(int i=0;i<102;i++) Advance(.1f);
    Check(preview.PreviewSimulation==waiting&&waiting.Robots.Single().BlockingEnabled,"ten-second wait reaches next command without forced reset");
    Show();var empty=preview.PreviewSimulation;
    Advance(.01f);
    Check(preview.PreviewSimulation==empty&&empty.Spawned==0,"pending spawns do not start restart timer");
    for(int i=0;i<300&&empty.RemainingEnemies>0;i++) Advance(.1f);
    Check(empty.RemainingEnemies==0&&empty.Leaks==3&&preview.PreviewSimulation==empty,"last escaped enemy starts delay without immediate reset");
    Advance(.99f); Status();
    Check(preview.PreviewSimulation==empty&&label.text.Contains("후 다시 시작"),"preview remains through first 0.99 seconds after enemies disappear");
    Advance(.01f);
    Check(preview.PreviewSimulation!=empty&&preview.PreviewSimulation.RemainingEnemies==3&&preview.PreviewSimulation.Robots.Count==1,"preview restarts at one second with one robot");
    Show(new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Block));
    var killed=preview.PreviewSimulation;killed.Robots.Single().Spec.damage=10000;
    for(int i=0;i<150&&killed.RemainingEnemies>0;i++) Advance(.1f);
    Check(killed.Kills==3&&preview.PreviewSimulation==killed,"last kill also waits before reset");
    Advance(.5f);preview.TogglePreview();preview.TogglePreview();var reopened=preview.PreviewSimulation;Advance(.6f);
    Check(preview.PreviewSimulation==reopened&&reopened.RemainingEnemies>0,"reopening clears previous restart countdown");
    Show(new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Wait,"0",new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Number,"5")),
        new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Block));
    preview.PreviewSimulation.Advance(.25); Status();
    Check(label.text.Contains("wait")&&label.text.Contains("저지 비활성"),"wait before block reports inactive stance");
    Show(new CodingGame.BlockCoding.CodeBlock(CodingGame.BlockCoding.BlockKind.Buff));
    preview.PreviewSimulation.Advance(.25); Status();
    Check(label.text.Contains("호환되지 않는 명령"),"incompatible command explained");
    Check(Stock()==stock&&battle.Simulation.Time==time&&battle.Simulation.BaseHealth==baseHealth&&actualTank.Health==health,
        "preview does not consume actual inventory, time or health");
    preview.Close();
    Check(!preview.IsShowing&&preview.PreviewSimulation==null,"closing clears preview simulation");
    return $"Defense preview checks passed: {checks}";
}
finally { battle.Restart(); }
