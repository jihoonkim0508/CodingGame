// DevTest.PlayStageMap(), then MCP run_script with entry StageMapPresentationCheck.Main.
// Only session previews change; saved progression is never written or deleted.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodingGame.Art.StageSelection;
using CodingGame.StageMap;
using UnityEngine;
using Object = UnityEngine.Object;

public static class StageMapPresentationCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Require(bool valid, string message) { if (!valid) throw new Exception(message); }
    static void Tick(StageCameraController mover, float elapsed)
    {
        typeof(StageCameraController).GetField("elapsed", Private).SetValue(mover, elapsed - Time.unscaledDeltaTime);
        typeof(StageCameraController).GetMethod("LateUpdate", Private).Invoke(mover, null);
    }
    static void InFrame(Camera camera, StageNode node, bool detail=false)
    {
        var bounds = detail?StageCameraController.DetailBounds(node):node.VisualBounds;
        for (int i = 0; i < 8; i++)
        {
            var corner = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var p = camera.WorldToViewportPoint(corner);
            Require(p.z > 0 && p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1,
                $"Stage {node.Number} clipped at aspect {camera.aspect}, viewport {p}");
        }
    }

    public static string Main()
    {
        Require(Application.isPlaying, "Start Stage Play Mode first.");
        var controller = Object.FindFirstObjectByType<StageMapController>();
        var mover = Object.FindFirstObjectByType<StageCameraController>();
        var nodes = Object.FindObjectsByType<StageNode>(FindObjectsSortMode.None).OrderBy(n => n.Number).ToArray();
        Require(controller && mover && nodes.Length == 3, "Expected the three-island stage map.");
        var camera = Camera.main;
        float aspect = camera.aspect, fov = camera.fieldOfView;
        var previews = (HashSet<int>)typeof(StageProgress).GetField("previewClears", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var savedPreviews = previews.ToArray();
        int selected = StageProgress.SelectedStage;
        int pendingUnlock = StageProgress.PendingUnlock;
        int pendingPurification = StageProgress.PendingPurification;
        var refresh = typeof(StageMapController).GetMethod("RefreshNodes", Private);
        int framingSamples = 0;
        try
        {
            mover.Configure(camera,nodes); Tick(mover,.851f);
            var scaler=controller.HUD.Scaler;
            Require(scaler.uiScaleMode==UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize && scaler.referenceResolution==new Vector2(1920,1080),"Native Canvas Scaler uses 1920x1080.");
            foreach (var node in nodes)
            {
                Require(node.LockVisual && node.LockVisual.IsLocked == (node.State == StageState.Locked), "Lock presentation matches progress.");
                var before = node.VisualBounds;
                node.LockVisual.SetLocked(true);
                Require(Vector3.Distance(before.size, node.VisualBounds.size) < .001f, "Barrier must not enlarge framing bounds.");
                node.LockVisual.SetLocked(false);
                Require(!node.LockVisual.gameObject.activeSelf, "Unlock removes the entire barrier.");
                node.Refresh();
            }
            var locked = nodes.FirstOrDefault(n => n.State == StageState.Locked);
            if (locked)
            {
                typeof(StageMapController).GetMethod("Enter", Private).Invoke(controller, new object[] { locked });
                Require(StageProgress.SelectedStage == selected && locked.LockVisual.Rejection > .9f,
                    "Locked click must react without selecting a stage.");
                Require(!(bool)typeof(StageMapController).GetField("entering", Private).GetValue(controller), "Locked click must not begin scene transition.");
                StageProgress.MarkClearedForPreview(locked.Number - 1);
                refresh.Invoke(controller, null);
                Require(locked.State == StageState.Available && !locked.LockVisual.gameObject.activeSelf, "Preview clear unlocks the next island and removes its barrier.");
            }
            foreach (float ratio in new[] { 16f / 9, 16f / 10, 4f / 3 })
            {
                foreach (var target in nodes)
                {
                    camera.aspect = ratio; camera.fieldOfView = fov;
                    mover.Configure(camera, nodes); Tick(mover, .851f);
                    foreach (var node in nodes) InFrame(camera, node);
                    mover.Focus(target.transform);
                    for (int step = 0; step <= 20; step++)
                    {
                        Tick(mover, StageCameraController.Duration * step / 20f);
                        InFrame(camera, target,true); framingSamples++;
                        Require(float.IsFinite(camera.transform.position.x), "Camera position must remain finite.");
                    }
                    Require(!mover.IsMoving, "Camera finishes within the scene fade delay.");
                }
                mover.ShowUnlockRoute(nodes[0],nodes[1]); Tick(mover,.801f);
                InFrame(camera,nodes[0]); InFrame(camera,nodes[1]);
                mover.ShowPurificationIsland(nodes[0]);Tick(mover,.851f);InFrame(camera,nodes[0],true);
                float lowHeight=camera.transform.position.y;
                mover.RiseOverPurification(nodes[0]);Tick(mover,1.251f);InFrame(camera,nodes[0],true);
                Require(camera.transform.position.y>lowHeight+.5f,"Purification shot rises rather than only panning sideways.");
                mover.ShowUnlockIsland(nodes[1]); Tick(mover,1.051f); InFrame(camera,nodes[1],true);
                mover.ShowOpenedIsland(nodes[1]);Tick(mover,.601f);InFrame(camera,nodes[1],true);
                mover.RestoreOverview(); Tick(mover,1.051f);
                foreach(var node in nodes) InFrame(camera,node);
            }
            return $"PASS: native 1920x1080 Canvas Scaler; lock state, rejection, preview unlock, effect bounds isolation; {framingSamples} camera samples and unlock route/closeup/overview shots at 16:9, 16:10 and 4:3.";
        }
        finally
        {
            previews.Clear(); foreach (int number in savedPreviews) previews.Add(number);
            StageProgress.SelectedStage = selected;
            typeof(StageProgress).GetProperty("PendingUnlock").SetValue(null,pendingUnlock);
            typeof(StageProgress).GetProperty("PendingPurification").SetValue(null,pendingPurification);
            refresh.Invoke(controller, null);
            camera.aspect = aspect; camera.fieldOfView = fov;
            mover.Configure(camera, nodes); Tick(mover, .851f);
        }
    }
}
