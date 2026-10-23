using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.Defense.Editor;
using TMPro;
using UnityEngine;

// Drives the real tutorial, with normal inventory/input limits. Program loading replaces repetitive mouse assembly;
// actual drop/slot guidance is checked separately through target geometry and the editor's loose-expression API.
public static class StageOneScriptCheck
{
    static readonly List<string> log = new List<string>();
    static readonly HashSet<string> seen = new HashSet<string>();
    static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    static void Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o,args);
    static void Require(bool ok, string label) { if (!ok) throw new Exception(label); }
    static void Save(string text) { Directory.CreateDirectory("TestResults"); File.WriteAllText("TestResults/stage-one-script.txt",text); }
    public static string Start()
    {
        Application.runInBackground = true;
        UnityEditor.EditorApplication.delayCall += async () => {
            try { await Run(); Save("PASS\n"+string.Join("\n",log)); }
            catch(Exception e) { Save("FAIL "+e+"\n"+string.Join("\n",log)); Debug.LogException(e); }
            finally { Time.timeScale=1; }
        };
        return "Scheduled: TestResults/stage-one-script.txt";
    }
    static void Load(DefenseBattle b,string code)
    {
        var panel=b.CodeEditor.Panel;
        panel.LoadProgram(panel.FunctionName,TutorialAnswers.Parse(code));
        Require(panel.IsApplied,"Program rejected: "+code);
    }
    static void Loose(DefenseBattle b)
    {
        var panel=b.CodeEditor.Panel;
        var source=panel.Palette.First(p=>p.view.BaseKind==BlockKind.Comparison).view;
        panel.Program.PlaceFree(source,new Vector2(300,-260));
        var comparison=panel.Program.LooseRoots().First();
        Call(comparison.Arguments[0],"Load",(IReadOnlyList<CodeBlock>)new[]{new CodeBlock(BlockKind.Variable,"enemy_distance")});
        Call(comparison.Arguments[1],"Load",(IReadOnlyList<CodeBlock>)new[]{new CodeBlock(BlockKind.MyAttackRange)});
        Field<TMP_Dropdown>(comparison,"dropdown").value=Array.IndexOf(PythonTreeCompiler.Comparisons,"<=");
        Call(panel,"Changed");
    }
    static void Place(DefenseBattle b)
    {
        // During the RobotPlaced step placement is already allowed, so no lock change is needed.
        var sim=b.Simulation; var spec=b.RobotDefinition(1).stats;
        var cells=new List<System.Numerics.Vector2>();
        for(float x=sim.Setup.Min.X+.5f;x<sim.Setup.Max.X;x++)
            for(float y=sim.Setup.Min.Y+.5f;y<sim.Setup.Max.Y;y++) {
                var p=new System.Numerics.Vector2(x,y); if(sim.PlacementError(spec,p)==null) cells.Add(p);
            }
        float Score(System.Numerics.Vector2 p) {
            float sum=0;
            foreach(var route in sim.Setup.Routes) for(int i=1;i<route.Points.Length;i++)
                for(float t=0;t<1;t+=.05f) if(System.Numerics.Vector2.Distance(p,System.Numerics.Vector2.Lerp(route.Points[i-1],route.Points[i],t))<=spec.range) sum++;
            return sum;
        }
        var best=cells.OrderByDescending(Score).First();
        Require(b.PlaceRobot(1,new Vector3(best.X,0,best.Y)),"Placement rejected: "+b.StatusMessage);
    }
    public static async Task Run()
    {
        log.Clear(); seen.Clear(); string last=""; float entered=Time.realtimeSinceStartup;
        bool acted=false; bool entranceSeen=false, entranceChecked=false; double entranceTime=0; float entranceAt=0; Vector3 home=Vector3.zero;
        for(int tick=0;tick<16000;tick++)
        {
            await Task.Delay(40);
            Require(UnityEditor.EditorApplication.isPlaying,"Play mode stopped");
            var b=DevTest.Battle; if(!b||b.Simulation==null) continue;
            var sim=b.Simulation; var d=b.Tutorial;
            Require(sim.Phase!=BattlePhase.Defeat,"Unexpected defeat: "+last+" hp="+sim.BaseHealth+" kills="+sim.Kills);
            if(sim.Phase==BattlePhase.Victory) {
                Require(b.Stage==1,"Wrong stage");
                Require(entranceSeen&&entranceChecked,"Boss entrance verification missing");
                log.Add("COMPLETE hp="+sim.BaseHealth+" kills="+sim.Kills+" leaks="+sim.Leaks+" steps="+seen.Count);
                return;
            }
            var camera=Field<Camera>(b,"battleCamera");
            if(b.IsTutorialFinale && sim.Phase==BattlePhase.Ready) home=camera.transform.position;
            if(b.FinaleEntrancePlaying) {
                Require(!d.PauseBattle,"Boss entrance must not pause battle");
                if(!entranceSeen) { entranceSeen=true; entranceTime=sim.Time; entranceAt=Time.realtimeSinceStartup;
                    ScreenCapture.CaptureScreenshot("TestResults/1-5.boss-entrance.png"); }
            }
            if(entranceSeen && !entranceChecked && Time.realtimeSinceStartup-entranceAt>2) {
                Require(!b.FinaleEntrancePlaying && sim.Time>entranceTime,"Boss entrance returns automatically while fight advances");
                Require(Vector3.Distance(camera.transform.position,home)<.1f,"Boss camera returns to previous viewpoint");
                entranceChecked=true; log.Add("Boss entrance: live simulation, no click wait, camera restored.");
            }
            Time.timeScale=sim.Phase==BattlePhase.Running?(b.FinaleEntrancePlaying?1:8):1;
            var seq=Field<TutorialSequence>(d,"active");
            if(!seq) { if(sim.Phase==BattlePhase.Reward) b.ClaimRewards(); continue; }
            var step=d.EditorStep;
            if(step==null) continue;
            string key=seq.sequenceId+"."+step.stepId;
            if(key!=last) {
                last=key; entered=Time.realtimeSinceStartup; acted=false;
                if(seen.Add(key)) { log.Add(key); Save("RUNNING "+key+"\n"+string.Join("\n",log)); }
            }
            Require(Time.realtimeSinceStartup-entered<40,"Stuck: "+last);
            if(!d.IsActive||Time.realtimeSinceStartup-entered<.3f) continue;
            var view=Field<TutorialDialogueView>(d,"view");
            if(!view.IsVisible) continue; // banners advance themselves
            d.EditorFinishTyping();
            var text=view.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Dialogue");
            text.ForceMeshUpdate(); Require(!text.isTextOverflowing,"Dialogue overflows: "+key);
            if(step.showEnemyInfo) {
                Require(d.EditorBridge.TryGetArea("Editor.Code.EnemyInfo",out _),"Missing enemy information: "+key);
            }
            if(seen.Count>0 && !acted && new[]{"1-2.C06","1-3.D02","1-4.D04","1-5.B06r1","1-3.C05","1-5.E02","1-5.F03"}.Contains(key)) {
                string shot="TestResults/"+key+".png"; ScreenCapture.CaptureScreenshot(shot);
            }
            if(step.kind==TutorialStepKind.Dialogue) { acted=true; await Task.Delay(70); d.EditorNext(); continue; }
            if(acted) continue;
            acted=true; await Task.Delay(70);
            if(!string.IsNullOrWhiteSpace(step.pointerKey)&&step.pointerDelay==0) {
                Require(d.EditorBridge.TryGetArea(step.pointerKey,out _),"Missing action arrow target: "+key+" "+step.pointerKey);
            }
            switch(step.completionSignal)
            {
                case TutorialSignals.ProgramMatches:
                    b.CodeEditor.Close(); Require(b.CodeEditor.IsOpen,"Premature close accepted: "+key);
                    if(step.pointerDelay==60) {
                        Field<UnityEngine.UI.Button>(b.CodeEditor,"hintButton").onClick.Invoke();
                        Require(b.CodeEditor.HintOpen,"Hint not open: "+key);
                        Field<UnityEngine.UI.Button>(b.CodeEditor,"hintButton").onClick.Invoke();
                        Require(!b.CodeEditor.HintOpen,"Hint not closed: "+key);
                        Call(d,"NextText");
                    }
                    Load(b,step.expectedCode); break;
                case TutorialSignals.ConditionBuilt: Loose(b); break;
                case TutorialSignals.SlotFilled:
                    b.CodeEditor.Panel.LoadProgram(b.CodeEditor.Panel.FunctionName,TutorialAnswers.Parse("enemy = get_nearest_enemy()\nslash(variable)")); break;
                case TutorialSignals.EditorOpened: b.DevelopSelected(); break;
                case TutorialSignals.EditorClosed: b.CodeEditor.Close(); break;
                case TutorialSignals.BattleStarted: b.StartBattle(); break;
                case TutorialSignals.RobotPlaced: Place(b); break;
                case TutorialSignals.PreviewHit: acted=false; break;
                default:
                    string target=step.targetKeys.FirstOrDefault()??"";
                    if(target.StartsWith(TutorialKeys.RobotButtonPrefix)) b.ChooseRobot(int.Parse(target.Substring(TutorialKeys.RobotButtonPrefix.Length)));
                    else if(target.StartsWith("Robot.Unit.")) b.SelectRobot(sim.Robots.OrderBy(r=>r.Id).ElementAt(int.Parse(target.Substring(11))).Id);
                    else if(target=="Battle.ClaimRewards") b.ClaimRewards();
                    else throw new Exception("Unhandled action: "+key);
                    break;
            }
        }
        throw new Exception("Timeout "+last);
    }
}
