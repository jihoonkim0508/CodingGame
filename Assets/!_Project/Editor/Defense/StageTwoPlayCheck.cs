using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodingGame.BlockCoding;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Point = System.Numerics.Vector2;

namespace CodingGame.Defense.Editor
{
    // Scene-level smoke test: uses real director/editor/preview; block loading replaces only manual dragging.
    [InitializeOnLoad]
    public static class StageTwoPlayCheck
    {
        const string Key = "CodingGame.StageTwoSmoke";
        static readonly List<string> report = new List<string>();
        static string last;
        static double changedAt, started;
        static int stageFrames;
        static StageTwoPlayCheck() { EditorApplication.update += Tick; }
        public static void Run()
        {
            SessionState.SetBool(Key, true); SessionState.SetFloat(Key + ".start", (float)EditorApplication.timeSinceStartup);
            DevTest.PlayDefense(2, 1, true, false);
        }
        static void End(bool ok, string reason)
        {
            SessionState.SetBool(Key, false); Directory.CreateDirectory("TestResults/Stage2");
            File.WriteAllText("TestResults/Stage2/playmode.md", "# Stage 2 Play Mode smoke\n\n" + string.Join("\n", report.Select(line => "- " + line)) + "\n\n" + (ok ? "PASS " : "FAIL ") + reason);
            Debug.Log((ok ? "STAGE2_PLAY_PASS " : "STAGE2_PLAY_FAIL ") + reason);
            EditorApplication.Exit(ok ? 0 : 1);
        }
        static CodeBlock[] Blocks(int count, RobotRole role = RobotRole.Warrior)
        {
            var loop = new CodeBlock(BlockKind.For, "0", new CodeBlock(BlockKind.Number, count.ToString()));
            loop.Body.Add(new CodeBlock(BlockKind.DeclareVariable, "enemy", new CodeBlock(BlockKind.NearestEnemy)));
            loop.Body.Add(new CodeBlock(role == RobotRole.Warrior ? BlockKind.Slash : BlockKind.Shot, "0", new CodeBlock(BlockKind.Variable, "enemy")));
            return new[] { loop };
        }
        static void Capture(string name)
        {
            var camera = Camera.main;
            if (!camera) return;
            var canvasStates = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(canvas => canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                .Select(canvas => (canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance)).ToArray();
            var previous = camera.targetTexture; var active = RenderTexture.active;
            var target = new RenderTexture(1920,1080,24);
            try
            {
                foreach(var state in canvasStates)
                { state.canvas.renderMode=RenderMode.ScreenSpaceCamera; state.canvas.worldCamera=camera; state.canvas.planeDistance=camera.nearClipPlane+1; }
                Canvas.ForceUpdateCanvases(); camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply();
                Directory.CreateDirectory("TestResults/Stage2"); File.WriteAllBytes("TestResults/Stage2/"+name+".png",texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            }
            finally
            {
                camera.targetTexture=previous; RenderTexture.active=active;
                foreach(var state in canvasStates)
                { state.canvas.renderMode=state.renderMode;state.canvas.worldCamera=state.worldCamera;state.canvas.planeDistance=state.planeDistance; }
                target.Release();UnityEngine.Object.DestroyImmediate(target);Canvas.ForceUpdateCanvases();
            }
        }
        static void Load(DefenseCodeEditor editor, CodeBlock[] blocks) => editor.Panel.LoadProgram(editor.Panel.FunctionName, blocks);
        static void Place(DefenseBattle battle, int definition)
        {
            var sim = battle.Simulation; var spec = battle.RobotDefinition(definition).stats;
            var samples = sim.Setup.Routes[0].Points.Zip(sim.Setup.Routes[0].Points.Skip(1), (a,b) => Enumerable.Range(0,20).Select(i=>Point.Lerp(a,b,i/20f))).SelectMany(x=>x).ToArray();
            var cells = new List<Point>();
            for(float x=sim.Setup.Min.X+.4f;x<sim.Setup.Max.X;x+=.8f)
            for(float y=sim.Setup.Min.Y+.4f;y<sim.Setup.Max.Y;y+=.8f)
            { var p=new Point(x,y);if(sim.PlacementError(spec,p)==null)cells.Add(p); }
            var best=cells.OrderByDescending(p=>samples.Count(q=>Point.Distance(p,q)<=spec.range)).First();
            battle.ChooseRobot(definition); if(!battle.PlaceRobot(definition,new Vector3(best.X,0,best.Y)))throw new Exception("Placement failed: "+battle.StatusMessage);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            try
            {
                if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".start",0)>240){End(false,"Timeout " + last);return;}
                if(!EditorApplication.isPlaying || !DevTest.Ready)return;
                var battle=DevTest.Battle;var director=battle.Tutorial;var sim=battle.Simulation;
                if(!director.EditorSynced)return;
                if(sim.Phase==BattlePhase.Defeat){End(false,"Battle failed at "+last);return;}
                if(sim.Phase==BattlePhase.Victory){End(true,"All five tutorials reached victory");return;}
                var step=director.EditorStep;if(step==null)return;
                string key=(sim.WaveIndex+1)+"/"+step.stepId;
                if(last!=key)
                {
                    last=key;stageFrames=0;changedAt=EditorApplication.timeSinceStartup;
                    report.Add("ENTER "+key+" phase="+sim.Phase+" editor="+battle.CodeEditor.IsOpen);
                    Directory.CreateDirectory("TestResults/Stage2"); File.WriteAllText("TestResults/Stage2/play-progress.md", string.Join("\n",report));
                }
                stageFrames++;if(stageFrames<3)return;
                if(EditorApplication.timeSinceStartup-changedAt>35){End(false,"Stuck "+last);return;}
                director.EditorFinishTyping();
                if(!string.IsNullOrEmpty(step.trigger))
                {
                    if(sim.Phase==BattlePhase.Ready)
                    {
                        if(sim.WaveIndex==1 || sim.WaveIndex==3)
                        {
                            var role=sim.WaveIndex==1?RobotRole.Warrior:RobotRole.Shooter;
                            if(!sim.Robots.Any(bot=>sim.IsNewRobotThisWave(bot)&&bot.Spec.role==role))
                            {
                                Place(battle,role==RobotRole.Warrior?1:4);
                                var bot=sim.Robots.Single(r=>sim.IsNewRobotThisWave(r)&&r.Spec.role==role);
                                sim.ApplyProgram(bot.Id,"free_"+bot.Id,Blocks(5,role));
                            }
                        }
                        battle.StartBattle();
                    }
                    if(sim.Phase==BattlePhase.Running)sim.Advance(1);
                    if(sim.Phase==BattlePhase.Reward)director.EditorNext();
                    return;
                }
                if(step.kind!=TutorialStepKind.ActionGuide)
                {
                    if(step.compareRepeats && stageFrames==3)Capture("comparison");
                    director.EditorNext();return;
                }
                var editor=battle.CodeEditor;
                if(step.prediction)
                {
                    Capture("prediction");
                    var button=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="Prediction 0");
                    button.onClick.Invoke();return;
                }
                var robotChoice = step.targetKeys.FirstOrDefault(target => target.StartsWith(TutorialKeys.RobotButtonPrefix));
                if(step.completionMode==TutorialCompletionMode.TargetClick && robotChoice!=null)
                { battle.ChooseRobot(int.Parse(robotChoice.Substring(TutorialKeys.RobotButtonPrefix.Length))); return; }
                if(step.completionSignal==TutorialSignals.RobotPlaced){Place(battle,sim.WaveIndex==2?4:1);return;}
                if(step.targetKeys.Contains(TutorialKeys.Unit(0))){battle.SelectRobot(TutorialWatcher.Robot(sim,0).Id);return;}
                if(step.completionSignal==TutorialSignals.EditorOpened){battle.DevelopSelected();return;}
                if(step.targetKeys.Contains(TutorialKeys.Close)){editor.Close();return;}
                if(step.completionSignal==TutorialSignals.BattleStarted){battle.StartBattle();return;}
                if(step.targetKeys.Contains("Battle.ClaimRewards")){battle.ClaimRewards();return;}
                if(step.completionSignal==TutorialSignals.ProgramMatches){Load(editor,TutorialAnswers.Parse(step.expectedCode));return;}
                if(sim.WaveIndex==0 && step.stepId=="C01")
                {
                    var loop=Blocks(5)[0];var body=loop.Body.ToArray();loop.Body.Clear();Load(editor,new[]{loop}.Concat(body).ToArray());return;
                }
                if(sim.WaveIndex==0 && step.stepId=="C02")
                {
                    var loop=Blocks(3)[0];var body=loop.Body.ToArray();loop.Body.Clear();Load(editor,new[]{loop}.Concat(body).ToArray());return;
                }
                if(sim.WaveIndex==0 && step.stepId=="C03"){Load(editor,Blocks(3));return;}
                if(sim.WaveIndex==2 && step.stepId=="B03")
                {
                    var blocks=Blocks(5,RobotRole.Shooter);blocks[0].Body[1].Arguments.Clear();Load(editor,blocks);return;
                }
                if(sim.WaveIndex==2 && step.stepId=="B04"){Load(editor,Blocks(5,RobotRole.Shooter));return;}
                // Preview completion is deliberately not injected: real attacks and program termination must report it.
            }
            catch(Exception error){End(false,last+" "+error);}
        }
    }
}
