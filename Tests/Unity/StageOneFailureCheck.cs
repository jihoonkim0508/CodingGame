using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodingGame.Defense;
using CodingGame.Defense.Editor;
using UnityEngine;
using UnityEngine.UI;

public static class StageOneFailureCheck
{
    static T Field<T>(object o,string key)=>(T)o.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    static void Require(bool ok,string text) { if(!ok) throw new Exception(text); }
    public static string Start()
    {
        Application.runInBackground=true;
        UnityEditor.EditorApplication.delayCall+=async()=>{
            try { var result=await Run(); File.WriteAllText("TestResults/stage-one-failure.txt",result); }
            catch(Exception e) { File.WriteAllText("TestResults/stage-one-failure.txt","FAIL "+e); Debug.LogException(e); }
        };
        return "Scheduled failure/retry check";
    }
    public static async Task<string> Run()
    {
        var b=DevTest.Battle; var sim=b.Simulation; var d=b.Tutorial;
        Require(sim.Phase==BattlePhase.Defeat,"Start from 1-5 defeat");
        Time.timeScale=1;
        for(int i=0;i<200;i++) {
            await Task.Delay(50);
            if(d.EditorStep?.stepId=="G04") break;
            d.EditorFinishTyping(); d.EditorNext();
        }
        Require(d.EditorStep?.stepId=="G04","Failure reaches choices");
        d.EditorFinishTyping(); await Task.Delay(250);
        Require(!b.FinaleEntrancePlaying,"Boss entrance cancelled on defeat");
        var hud=UnityEngine.Object.FindFirstObjectByType<DefensePlayerHUD>();
        Require(hud.FinaleFailureVisible,"Failure modal visible");
        Require(d.EditorBridge.TryGetArea(TutorialKeys.RetryFinale,out _)&&d.EditorBridge.TryGetArea(TutorialKeys.Quit,out _),"Both choices available");
        Require(!b.TutorialAllows("Battle.Start")&&!b.TutorialAllows("Battle.Restart"),"Unrelated controls locked");
        Require(Field<Button>(hud,"retryFinale").interactable&&Field<Button>(hud,"quitFinale").interactable,"Both choices interactable");
        ScreenCapture.CaptureScreenshot("TestResults/1-5.failure.png");
        await Task.Delay(100);
        var inventory=string.Join(",",sim.Inventory.OrderBy(p=>p.Key));
        var robots=sim.Robots.Select(r=>r.Id+":"+r.Position+":"+r.Program.Source).ToArray();
        Field<Button>(hud,"retryFinale").onClick.Invoke();
        await Task.Delay(250);
        Require(b.Simulation==sim&&sim.WaveIndex==4&&sim.Phase==BattlePhase.Ready&&sim.BaseHealth>0,"Retry restores 1-5, same session");
        Require(sim.Enemies.Count==0&&sim.Projectiles.Count==0&&sim.FinaleBossId==0,"Combat and boss state cleared");
        Require(robots.SequenceEqual(sim.Robots.Select(r=>r.Id+":"+r.Position+":"+r.Program.Source)),"Placement and programs restored");
        Require(inventory==string.Join(",",sim.Inventory.OrderBy(p=>p.Key)),"No repeated grants");
        Require(d.EditorStep?.stepId=="H01","Retry script line H01");
        Require(!hud.FinaleFailureVisible,"Failure modal closed");
        d.EditorFinishTyping(); ScreenCapture.CaptureScreenshot("TestResults/1-5.retry.png");
        return "PASS: G01-G04 failure dialogue, two buttons, input lock, camera cleanup, H01 retry, wave/core/enemies/projectiles/robots/code/inventory restoration. Exit listener is wired; application exit was not invoked during QA.";
    }
}
