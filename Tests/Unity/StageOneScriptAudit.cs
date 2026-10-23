using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CodingGame.Defense;
using UnityEditor;

public static class StageOneScriptAudit
{
    static readonly Dictionary<string,(string speaker,string text)> source = new Dictionary<string,(string,string)>();
    static readonly List<string> log = new List<string>();
    static int checks;
    static void Check(bool ok,string message) { checks++; if(!ok) throw new Exception(message); }
    static string Plain(string s) => (s??"").Replace("`","").Trim().Replace("\\n","\n").Replace(" \n ","\n");
    static TutorialSequence Sequence(int wave)=>AssetDatabase.LoadAssetAtPath<TutorialSequence>($"Assets/!_Project/Defense/Tutorial/Stage1/Tutorial_1-{wave}.asset");
    static TutorialStep Step(int wave,string id)=>Sequence(wave).steps.Single(s=>s.stepId==id);
    public static string Run()
    {
        source.Clear(); log.Clear(); checks=0;
        var regex=new Regex(@"^\[(1-\d\.[A-Z]\d\d[a-z]?)\] (?:\((?:ui|UI)로 표시\)\s*)?(\S+) : (.*)$");
        foreach(string line in File.ReadAllLines("Docs/flow_stage1/tutorial_stage1_대본.md")) {
            var m=regex.Match(line); if(m.Success) source.Add(m.Groups[1].Value,(m.Groups[2].Value,Plain(m.Groups[3].Value)));
        }
        for(int wave=1;wave<=5;wave++) {
            var seq=Sequence(wave); Check(seq&&seq.enabled,"Missing sequence "+wave);
            var covered=new HashSet<string>();
            Check(seq.steps.Select(s=>s.stepId).Distinct().Count()==seq.steps.Length,"Duplicate step IDs "+wave);
            foreach(var step in seq.steps.Concat(seq.failureSteps)) {
                string id="1-"+wave+"."+step.stepId;
                if(step.stepId=="P01"&&wave==4) id="1-1.B01";
                else if(step.stepId=="P02"&&wave==4) id="1-1.B02";
                else if(!source.ContainsKey(id)) {
                    id=Regex.Replace(id,@"r\d$","");
                    if(!source.ContainsKey(id)) id=Regex.Replace(id,@"[a-z]$","");
                }
                Check(source.ContainsKey(id),"Step has no script source: "+wave+"/"+step.stepId);
                covered.Add(id);
                foreach(var text in step.texts) {
                    Check(text.speaker==source[id].speaker&&Plain(text.text)==source[id].text,"Dialogue mismatch: "+id);
                    if(text.speaker==TutorialSignals.SystemSpeaker) Check(step.kind==TutorialStepKind.Dialogue,"System line must be banner: "+id);
                }
                if(step.kind==TutorialStepKind.ActionGuide) Check(step.completionMode!=TutorialCompletionMode.Unconfigured,"Missing completion condition: "+id);
            }
            if(wave==5) { Check(Plain(seq.retryText)==source["1-5.H01"].text,"Retry line mismatch"); covered.Add("1-5.H01"); }
            var missing=source.Keys.Where(id=>id.StartsWith("1-"+wave+".")&&!covered.Contains(id)).ToArray();
            if(wave==1) missing=missing.Where(id=>id!="1-1.E04").ToArray(); // explicitly removed in the approved temporary notes
            Check(missing.Length==0,"Missing script lines: "+string.Join(",",missing));
            log.Add($"1-{wave}: {seq.steps.Length} steps, {seq.failureSteps.Length} failure steps; all dialogue IDs covered (1-1 E04 approved omission).");
        }
        foreach(var pair in new[]{(2,"C06"),(4,"D04"),(5,"B06r0"),(5,"B06r1")}) {
            var s=Step(pair.Item1,pair.Item2);
            Check(s.completionSignal==TutorialSignals.ProgramMatches&&s.dismissable&&s.pointerDelay==60,"Practice/hint policy "+pair);
            Check(!s.targetKeys.Contains(TutorialKeys.Close)&&s.targetKeys.Contains(TutorialKeys.Hint),"Practice close/hint lock "+pair);
        }
        Check(Step(2,"C05").clearProgram,"1-2 code reset");
        Check(Step(2,"C06c").completionSignal==TutorialSignals.EditorClosed,"1-2 close after answer");
        Check(Step(3,"B02b").expectedCode.Contains("enemy.distance(get_my_pos())"),"1-3 distance practice");
        Check(Step(3,"C03").completionSignal==TutorialSignals.ConditionBuilt,"1-3 loose condition");
        foreach(string id in new[]{"C04","C05","C06"}) Check(Step(3,id).showCondition,"1-3 True/False "+id);
        Check(Step(4,"D01").openEditor&&Step(4,"D01").robotIndex==1,"1-4 second robot transition");
        Check(Step(4,"D04").robotIndex==1,"1-4 second robot answer");
        Check(Step(5,"E01").trigger==TutorialSignals.BossSpawned,"Boss entrance trigger");
        Check(Step(5,"E05").trigger==TutorialSignals.BossAttacked,"Boss attack trigger");
        Check(Sequence(5).steps.SkipWhile(s=>s.stepId!="E01").All(s=>!s.pauseBattle),"Boss fight must not pause");
        Check(Sequence(5).failureSteps.Last().targetKeys.SequenceEqual(new[]{TutorialKeys.RetryFinale,TutorialKeys.Quit}),"Failure choices");
        string result=$"PASS: {checks} script/asset checks\n"+string.Join("\n",log);
        Directory.CreateDirectory("TestResults"); File.WriteAllText("TestResults/stage-one-script-audit.txt",result); return result;
    }
}
