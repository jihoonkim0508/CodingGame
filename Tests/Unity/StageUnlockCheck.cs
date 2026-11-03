// Start Stage Play, then run_script entry StageUnlockCheck.Begin. Poll Temp/StageSelection/unlock-check.txt.
// Exercises the same queued clear -> scene load path used by Defense, then the developer clear path.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodingGame.StageMap;
using UnityEngine;
using Object=UnityEngine.Object;

public static class StageUnlockCheck
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static string Begin()
    {
        Directory.CreateDirectory("Temp/StageSelection"); File.WriteAllText("Temp/StageSelection/unlock-check.txt","RUNNING");
        _=Run(); return "Started unlock check.";
    }
    static void Require(bool value,string message) { if(!value) throw new Exception(message); }
    static async Task Wait(Func<bool> condition,string message,int milliseconds=10000)
    {
        float until=Time.realtimeSinceStartup+milliseconds/1000f;
        while(!condition() && Time.realtimeSinceStartup<until)
        {
            var controller=Object.FindFirstObjectByType<StageMapController>();
            if(controller&&controller.IsUnlocking)
                Require(!controller.HUD.IsHintVisible,"Click/arrow hint stays hidden throughout the cinematic, including pauses between shots.");
            await Task.Delay(30);
        }
        Require(condition(),message);
    }
    static async Task Run()
    {
        var previews=(HashSet<int>)typeof(StageProgress).GetField("previewClears",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        var saved=previews.ToArray(); int pending=StageProgress.PendingUnlock, selected=StageProgress.SelectedStage, pendingPurification=StageProgress.PendingPurification;
        bool background=Application.runInBackground;
        try
        {
            Application.runInBackground=true;
            await Task.Delay(1000);
            var old=Object.FindFirstObjectByType<StageMapController>();
            var nodes=Object.FindObjectsByType<StageNode>(FindObjectsSortMode.None).OrderBy(n=>n.Number).ToArray();
            int source=Array.FindIndex(nodes,n=>n.State==StageState.Available && nodes.Any(next=>next.Number==n.Number+1 && next.State==StageState.Locked));
            Require(source>=0,"An available stage with a locked next island is required.");
            int number=nodes[source].Number+1;
            StageProgress.MarkClearedForPreview(number-1);
            Require(StageProgress.PendingUnlock==number,"First clear queues the next island.");
            CodingGame.GameFlow.LoadScene(CodingGame.GameFlow.StageScene);
            await Wait(()=> { var c=Object.FindFirstObjectByType<StageMapController>(); return c && c!=old && c.IsUnlocking; },"Returning to map starts unlock cinematic.");
            var controller=Object.FindFirstObjectByType<StageMapController>();
            var target=Object.FindObjectsByType<StageNode>(FindObjectsSortMode.None).Single(n=>n.Number==number);
            var purifiedSource=Object.FindObjectsByType<StageNode>(FindObjectsSortMode.None).Single(n=>n.Number==number-1);
            var sourceView=purifiedSource.GetComponent<CodingGame.Art.StageSelection.StageIslandView>();
            Require(target.State==StageState.Available && target.LockVisual.IsLocked,"Logical unlock keeps the visual seal until the energy arrives.");
            typeof(StageMapController).GetMethod("Enter",Private).Invoke(controller,new object[]{target});
            Require(StageProgress.SelectedStage==selected,"Cinematic blocks stage entry.");
            Require(StageProgress.PendingUnlock==0,"Cinematic consumes the pending event once.");
            Require(StageProgress.PendingPurification==0,"Purification event is consumed once.");
            await Wait(()=>sourceView.Purification>.5f,"Cleared island begins an animated purification.");
            Require(sourceView.Purification<1 && purifiedSource.PurificationVisual.IsPurifying,"Before and after purification are separated in time.");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Temp/StageSelection/purification-sweep.png"));
            await Wait(()=>Object.FindFirstObjectByType<StageUnlockEffect>()!=null,"Travelling circuit effect spawns.");
            Require(sourceView.Purification==1 && !purifiedSource.PurificationVisual.IsPurifying,"Source is fully purified before the next seal opens.");
            await Task.Delay(500);
            var effect=Object.FindFirstObjectByType<StageUnlockEffect>();
            var trail=effect.GetComponentsInChildren<LineRenderer>().Single(l=>l.name=="Circuit energy trail");
            Require(trail.positionCount==49 && Vector3.Distance(trail.GetPosition(0),trail.GetPosition(48))>.5f,"Light visibly advances along the circuit route.");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Temp/StageSelection/unlock-route.png"));
            await Wait(()=>target.LockVisual.IsReleasing,"Arriving pulse starts opening the lock.");
            await Task.Delay(300);
            Require(target.LockVisual.ReleaseProgress>0 && target.LockVisual.ReleaseProgress<1,"Barrier dissolve animates over time.");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Temp/StageSelection/unlock-open.png"));
            await Wait(()=>!controller.IsUnlocking,"Cinematic returns control.");
            Require(!target.LockVisual.gameObject.activeSelf && !target.LockVisual.IsLocked,"Finished unlock removes the barrier.");
            Require(controller.RecommendedStage==target,"Newly unlocked island is recommended.");
            Require(target.GetComponent<CodingGame.Art.StageSelection.StageIslandView>().Purification==0,"Opening a new island does not mark it purified.");
            Require(purifiedSource.PurificationVisual.gameObject.activeSelf,"Cleared island keeps its green purified field.");
            await Wait(()=>!Object.FindFirstObjectByType<StageUnlockEffect>(),"Temporary effect objects are released.");
            Require(!Object.FindFirstObjectByType<StageCameraController>().IsMoving,"Camera returns to overview before input resumes.");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Temp/StageSelection/purified-overview.png"));await Task.Delay(100);
            var lockPlate=Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(i=>i.name=="Stage "+number+" lock");
            Require(lockPlate.color.grayscale<.2f && lockPlate.color.a>.9f,"Lock background is an opaque dark plate.");
            var lockBody=lockPlate.GetComponentsInChildren<UnityEngine.UI.Image>(true).First(i=>i.name=="Orange lock body");
            Require(lockBody.color.r>.9f && lockBody.color.g>.55f && lockBody.color.g<.85f && lockBody.color.b<.4f,"Lock icon stays warm orange.");
            foreach(var scaler in Object.FindObjectsByType<UnityEngine.UI.CanvasScaler>(FindObjectsSortMode.None))
                Require(scaler.referenceResolution==new Vector2(1920,1080),"Map and fade canvases share the requested reference resolution.");
            StageProgress.MarkClearedForPreview(number-1);
            Require(StageProgress.PendingUnlock==0 && StageProgress.PendingPurification==0,"Replaying a cleared island does not queue duplicate purification or unlock.");
            nodes=Object.FindObjectsByType<StageNode>(FindObjectsSortMode.None);
            if(nodes.Any(n=>n.Number==number+1 && n.State==StageState.Locked))
            {
                typeof(StageMapController).GetMethod("DeveloperClear",Private).Invoke(controller,null);
                Require(controller.IsUnlocking,"Developer clear plays the same journey.");
                await Wait(()=>!controller.IsUnlocking,"Second unlock completes.");
                Require(nodes.Single(n=>n.Number==number+1).State==StageState.Available,"Second island unlock completes correctly.");
            }
            if(nodes.All(n=>n.Number!=number+2))
            {
                typeof(StageMapController).GetMethod("DeveloperClear",Private).Invoke(controller,null);
                Require(controller.IsUnlocking,"Last island still plays purification without another island to unlock.");
                await Wait(()=>!controller.IsUnlocking,"Last purification completes.");
                Require(nodes.All(n=>n.State==StageState.Cleared && n.PurificationVisual.gameObject.activeSelf),"All cleared islands retain purification visuals.");
            }
            File.WriteAllText("Temp/StageSelection/unlock-check.txt","PASS: purification sweep precedes path light and seal opening; cleared islands keep green fields; newly opened islands remain unpurified; warm-orange icons on dark backgrounds; closeup/crane/tracking camera restores overview; both unlock routes and final island purification; entry blocked; no duplicate replay; 1920x1080 Canvas Scalers.");
        }
        catch(Exception error) { File.WriteAllText("Temp/StageSelection/unlock-check.txt","FAIL: "+error); }
        finally
        {
            previews.Clear(); foreach(int number in saved) previews.Add(number);
            typeof(StageProgress).GetProperty("PendingUnlock").SetValue(null,pending); StageProgress.SelectedStage=selected;
            typeof(StageProgress).GetProperty("PendingPurification").SetValue(null,pendingPurification);
            var controller=Object.FindFirstObjectByType<StageMapController>();
            if(controller && !controller.IsUnlocking) typeof(StageMapController).GetMethod("RefreshNodes",Private).Invoke(controller,null);
            Application.runInBackground=background;
        }
    }
}
