using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using CodingGame.Defense.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class StageOneGuidanceCheck
{
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    static void Check(bool ok,string label) { if(!ok) throw new Exception(label); }
    static DefenseBattle B=>DevTest.Battle;
    static void Load(string code) { var p=B.CodeEditor.Panel;p.LoadProgram(p.FunctionName,TutorialAnswers.Parse(code)); }
    static Vector2 Point(RectTransform rect,float u=.5f) {
        var camera=rect.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(new Vector3(Mathf.Lerp(rect.rect.xMin,rect.rect.xMax,u),rect.rect.center.y)));
    }
    static Vector2 Target(string key) {
        var corners=new Vector3[4]; Check(B.CodeEditor.TryGetTutorialCorners(key,corners),"Missing "+key);
        var world=(corners[0]+corners[2])*.5f;
        var camera=B.CodeEditor.Panel.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(camera,world);
    }
    static async Task Drag(BlockKind kind,string destination) {
        B.CodeEditor.RevealPalette(TutorialKeys.PalettePrefix+kind); await Task.Delay(100);
        var source=B.CodeEditor.TutorialPaletteItem(TutorialKeys.PalettePrefix+kind); Check(source,"Missing palette "+kind);
        Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)source.parent); Canvas.ForceUpdateCanvases();
        var drag=typeof(Playtest).GetMethod("Drag",BindingFlags.Static|BindingFlags.NonPublic);
        var from=Point(source,.60f); var to=Target(destination);
        if(destination.StartsWith("Editor.Code.After.")) {
            var corners=new Vector3[4]; B.CodeEditor.TryGetTutorialCorners(destination,corners);
            var camera=source.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
            var sourceTop=RectTransformUtility.WorldToScreenPoint(camera,source.TransformPoint(new Vector3(source.rect.xMin,source.rect.yMax)));
            to=RectTransformUtility.WorldToScreenPoint(camera,corners[1])+(from-sourceTop);
        }
        var result=drag.Invoke(null,new object[]{from,to});
        File.AppendAllText("TestResults/stage-one-drag-trace.txt",kind+" -> "+destination+" pressed="+result+" from="+from+" to="+to+"\n");
        await Task.Delay(160);
    }
    static void Name(CommandBlockView block,string name)=>Field<TMP_InputField>(block,"input").text=name;
    static async Task Setup(int wave,string step,string code) {
        DevTest.PlayDefense(1,wave,true,true);
        await Task.Delay(600);
        Check(B.Simulation.WaveIndex==wave-1,"Wave setup");
        var robot=B.Simulation.Robots.OrderBy(r=>r.Id).First();
        B.CodeEditor.Open(robot.Id); Load(code);
        var seq=B.Tutorial.Sequences.Single(s=>s.stage==1&&s.wave==wave);
        B.Tutorial.EditorRun(seq,Array.FindIndex(seq.steps,s=>s.stepId==step));
        B.BeginCodeView(robot); await Task.Delay(300);
    }
    public static string Start() {
        Application.runInBackground=true;
        UnityEditor.EditorApplication.delayCall+=async()=>{
            try { await Run(); File.WriteAllText("TestResults/stage-one-guidance.txt","PASS: real EventSystem drags for 1-3 distance declaration/variable/get_my_pos and 1-4 range declaration/value; adaptive arrows avoid old slash/if slots; True/False use live preview distances; no info/code overlap."); }
            catch(Exception e) { File.WriteAllText("TestResults/stage-one-guidance.txt","FAIL "+e); Debug.LogException(e); }
        }; return "Scheduled UI guidance checks";
    }
    static async Task Run() {
        Application.runInBackground=true;
        await Setup(3,"B02b","enemy = get_nearest_enemy()\nslash(enemy)");
        Check(!B.Tutorial.EditorBridge.TryGetArea("Editor.Code.DistanceValue",out _),"Do not point into the old slash");
        await Drag(BlockKind.DeclareVariable,"Editor.Code.After.1");
        var declare=B.CodeEditor.Panel.Program.StartChain()[1];
        Check(declare.BaseKind==BlockKind.DeclareVariable,"Declaration inserted before slash");
        Name(declare,"enemy_distance");
        Check(B.Tutorial.EditorBridge.TryGetArea("Editor.Code.DistanceValue",out _),"Arrow follows new declaration");
        await Drag(BlockKind.Variable,"Editor.Code.DistanceValue");
        var value=declare.Arguments[0].Blocks.Single(); Name(value,"enemy.");
        Field<TMP_Dropdown>(value,"dropdown").value=1;
        await Task.Delay(100);
        await Drag(BlockKind.MyPos,"Editor.Code.DistancePosition");
        await Task.Delay(300);
        Check(B.Tutorial.EditorStep?.stepId=="B03","Distance code completion advances dialogue");
        // The read-only values sit above code. Verify both comparison outcomes from actual preview positions.
        var seq=B.Tutorial.Sequences.Single(s=>s.stage==1&&s.wave==3);
        B.Tutorial.EditorRun(seq,Array.FindIndex(seq.steps,s=>s.stepId=="C05"));
        var preview=B.PreviewSimulation; preview.Options.enemyMovement=false; preview.Options.robotCode=false; preview.Options.enemySpawn=false;
        preview.Advance(1d/60); var enemy=preview.Enemies.First(); var robot=preview.Robots.First();
        foreach(var target in preview.Enemies) target.Position=robot.Position+new System.Numerics.Vector2(robot.Range+1,0);
        await Task.Delay(200);
        var info=Field<TMP_Text>(B.CodeEditor,"tutorialInfo");
        Check(info.text.Contains("False"),"Far enemy gives False");
        enemy.Position=robot.Position+new System.Numerics.Vector2(robot.Range-1,0);
        await Task.Delay(200); Check(info.text.Contains("True"),"Near enemy gives True");
        info.ForceMeshUpdate(); Check(!info.isTextOverflowing,"Information fits");
        var bridge=B.Tutorial.EditorBridge;
        Check(bridge.TryGetArea("Editor.Code.EnemyInfo",out var area)&&bridge.TryGetArea("Editor.Code.Line.1",out var line)&&!area.Overlaps(line),"Information does not cover code");
        B.Tutorial.EditorFinishTyping(); ScreenCapture.CaptureScreenshot("TestResults/1-3.condition-final.png");
        await Task.Delay(120);
        await Setup(4,"C04","enemy = get_nearest_enemy()\nenemy_distance = enemy.distance(get_my_pos())\nif enemy_distance <= my_attack_range():\n    slash(enemy)");
        Check(!B.Tutorial.EditorBridge.TryGetArea("Editor.Code.RangeValue",out _),"Do not point into old if");
        await Drag(BlockKind.DeclareVariable,"Editor.Code.After.2");
        declare=B.CodeEditor.Panel.Program.StartChain()[2]; Name(declare,"attack_range");
        await Drag(BlockKind.MyAttackRange,"Editor.Code.RangeValue");
        await Task.Delay(300);
        Check(B.Tutorial.EditorStep?.stepId=="C05","Range code completion advances dialogue");
    }
}
