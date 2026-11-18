// Render audit, bugs R1-R3: the map's look after its build animation, the character outline, the stage map's anti-aliasing.
// Run in Play mode, from any scene (it loads Stage, then Defense; about 20 seconds), one run at a time:
//   run_script entry RenderAuditCheck.Begin   starts the run and returns at once
//   run_script entry RenderAuditCheck.Main    "RUNNING: <check>" until the run is over, then the result
// The result (also Temp/RenderAudit.txt): one line per failing check, then "AUDIT render: <passed>/<total> passed".
// It puts the anti-aliasing option back and leaves the game in a Stage 1 test battle with one robot placed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodingGame;
using CodingGame.Art.MapPainter;
using CodingGame.Defense;
using CodingGame.StageMap;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Point = System.Numerics.Vector2;

public static class RenderAuditCheck
{
    const string ResultPath = "Temp/RenderAudit.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly DefenseTestOptions Sandbox = new DefenseTestOptions { tutorial = false, tutorialInput = false };
    static readonly List<string> failures = new List<string>();
    static int total;

    public static string Begin()
    {
        if (!Application.isPlaying) return "Enter Play mode first.";
        File.WriteAllText(ResultPath, "RUNNING: starting");
        _ = Run();
        return "Started. Ask entry RenderAuditCheck.Main for the result.";
    }
    public static string Main() => File.Exists(ResultPath) ? File.ReadAllText(ResultPath) : "Not started: run entry RenderAuditCheck.Begin in Play mode.";

    static async Task Run()
    {
        failures.Clear(); total = 0;
        bool background = Application.runInBackground;
        Application.runInBackground = true; // frames keep coming while the editor is not in front
        try
        {
            await Check("R3 stage-map anti-aliasing follows the option", StageAntiAliasing);
            await Check("R1 map colours after the build animation", MapColours);
            await Check("R2 character outline over two backgrounds", Outline);
        }
        finally
        {
            Application.runInBackground = background;
            failures.Sort(StringComparer.Ordinal);
            File.WriteAllText(ResultPath, string.Join("\n", failures.Append($"AUDIT render: {total - failures.Count}/{total} passed")));
        }
    }
    // A check answers null when it passes, or what is wrong; one that throws has failed too, and the others still run.
    static async Task Check(string name, Func<Task<string>> check)
    {
        total++; File.WriteAllText(ResultPath, "RUNNING: " + name);
        string wrong;
        try { wrong = Application.isPlaying ? await check() : "Play mode ended"; }
        catch (Exception error) { wrong = $"did not finish: {error.GetType().Name}: {error.Message} {error.StackTrace?.Split('\n')[0].Trim()}"; }
        if (wrong != null) failures.Add($"FAIL {name}: {wrong}");
    }
    static async Task Until(Func<bool> done, float seconds, string what)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!done())
        {
            if (!Application.isPlaying) throw new Exception("Play mode ended");
            if (Time.realtimeSinceStartup > end) throw new TimeoutException($"{what} not there after {seconds:0} s");
            await Task.Delay(30);
        }
    }
    static Task Frames(int count) { int at = Time.frameCount + count; return Until(() => Time.frameCount >= at, 10, count + " frames"); }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);

    // R3. The option (끔, 2x, 4x: GraphicsOptions.Msaa, which both Esc menus set) decides the stage map's anti-aliasing: 끔 leaves
    // none, and with MSAA on the camera runs no post-process pass on top of it. The camera's SMAA High dates from 2026-11-03
    // (c70335a), when the pipeline had no MSAA; the pipeline's 4x came on 11-05 (d27be71).
    static async Task<string> StageAntiAliasing()
    {
        const string key = "Graphics.MSAA"; // GraphicsOptions.MsaaKey
        var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        bool saved = PlayerPrefs.HasKey(key); int option = PlayerPrefs.GetInt(key), samples = pipeline.msaaSampleCount;
        var wrong = new List<string>();
        try
        {
            GraphicsOptions.Msaa = 1; // 끔 before the map opens: Configure is the first to see it
            var old = Object.FindFirstObjectByType<StageMapController>();
            SceneManager.LoadScene(GameFlow.StageScene);
            StageMapController map = null;
            await Until(() => (map = Object.FindFirstObjectByType<StageMapController>()) && map != old, 30, "the Stage scene");
            var data = Field<Camera>(map, "mapCamera").GetUniversalAdditionalCameraData();
            foreach (var (level, how) in new[] { (1, "off when the map opens"), (4, "4x picked on the map"), (1, "off picked on the map") })
            {
                GraphicsOptions.Msaa = level;
                await Frames(3);
                if (data.antialiasing != AntialiasingMode.None || pipeline.msaaSampleCount != level)
                    wrong.Add($"{how}: camera {data.antialiasing}, pipeline MSAA {pipeline.msaaSampleCount}x");
            }
        }
        finally
        {
            // Not through GraphicsOptions.Msaa: it would save a value where the player had none.
            if (saved) PlayerPrefs.SetInt(key, option); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            if (Application.isPlaying) pipeline.msaaSampleCount = samples; // after Play the Editor has put the asset back itself
        }
        return wrong.Count == 0 ? null : "the stage camera should add no anti-aliasing of its own to the option's MSAA, but " + string.Join("; ", wrong);
    }

    // The Defense scene with its battle begun (Play starts at Title) and the loading cover gone: the map's build waits for that.
    static async Task<DefenseBattle> Battle()
    {
        if (!Object.FindFirstObjectByType<DefenseBattle>()) SceneManager.LoadScene(GameFlow.DefenseScene);
        DefenseBattle battle = null;
        await Until(() => (battle = Object.FindFirstObjectByType<DefenseBattle>()) && battle.Simulation != null && !GameFlow.Loading, 60, "the Defense scene");
        return battle;
    }

    // R1. After the build animation every floor and road tile has the property blocks ModularMapRoot gives it without the
    // animation (colours, shading). Each map is entered as the game enters it.
    static async Task<string> MapColours()
    {
        var battle = await Battle();
        var workshop = Field<ModularMapRoot>(battle, "workshopMap"); var stageOne = Field<ModularMapRoot>(battle, "stageOneMap");
        // The workshop is on when the scene loads, so ModularMapRoot has styled it by the time its build starts.
        battle.EditorStartSession(2, 1, true, Sandbox);
        var build = workshop.GetComponent<MapBuildSequence>();
        build.Finish(false);
        var plain = Tiles(workshop).Select(Look).ToArray();
        build.PlayStageEntry();
        var second = await AfterBuild(workshop, plain);
        // Stage 1's map is off in the scene and a scene saves no property blocks: DefenseBattle turns it on with bare tiles.
        // First what the map gives them when it is turned on with no animation, then the same entry with the animation.
        if (stageOne.gameObject.activeSelf) return "Stage 2 did not turn Stage 1's map off";
        build = stageOne.GetComponent<MapBuildSequence>(); bool plays = build.playOnStart;
        Unstyle(stageOne); build.playOnStart = false; stageOne.gameObject.SetActive(true);
        plain = Tiles(stageOne).Select(Look).ToArray();
        stageOne.gameObject.SetActive(false); build.playOnStart = plays; Unstyle(stageOne);
        MapBuildSequence.BeginStageEntry();
        battle.EditorStartSession(1, 1, true, Sandbox);
        var first = await AfterBuild(stageOne, plain);
        return first.same && second.same ? null : first.text + "; " + second.text;
    }
    // Waits for the map's build animation, then compares its tiles with plain: their look without the animation.
    static async Task<(bool same, string text)> AfterBuild(ModularMapRoot map, string[][] plain)
    {
        var build = map.GetComponent<MapBuildSequence>();
        if (!build.IsAnimating) return (false, map.name + ": the build animation did not start");
        await Until(() => !build.IsAnimating, build.duration + 15, map.name + "'s built map");
        var tiles = Tiles(map);
        // A map that styles nothing would pass with nothing to lose.
        int styled = plain.Count(look => look.Any(value => value.Contains("_ShadowStrength=") && !value.EndsWith("=none")));
        if (styled == 0 || tiles.Length != plain.Length) return (false, $"{map.name}: {styled} of {plain.Length} tiles styled without the animation, {tiles.Length} tiles after it");
        int changed = 0, bare = 0; string example = null;
        for (int i = 0; i < tiles.Length; i++)
        {
            var built = Look(tiles[i]);
            if (built.All(value => value.EndsWith("=none") || value.Contains("_BuildGlow="))) bare++;
            if (built.SequenceEqual(plain[i])) continue;
            if (changed++ > 0) continue;
            example = tiles[i].transform.parent.name + "/" + tiles[i].name + " " +
                (plain[i].Zip(built, (was, now) => was == now ? null : was + " is now " + now.Substring(now.IndexOf('=') + 1)).FirstOrDefault(change => change != null) ?? "has other material slots");
        }
        return changed == 0 ? (true, $"{map.name}: all {tiles.Length} tiles as without the animation") :
            (false, $"{map.name}: {changed} of {tiles.Length} tiles differ from the map without the animation ({bare} with no property block left), e.g. {example}");
    }
    // The renderers ModularMapRoot.ApplyFloorColors styles.
    static MeshRenderer[] Tiles(ModularMapRoot map) =>
        new[] { map.floorGroup, map.roadGroup }.Where(group => group).SelectMany(group => group.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
    static void Unstyle(ModularMapRoot map)
    {
        foreach (var tile in Tiles(map)) { tile.SetPropertyBlock(null); for (int slot = 0; slot < tile.sharedMaterials.Length; slot++) tile.SetPropertyBlock(null, slot); }
    }
    static readonly string[] Shading = { "_LightSoftness", "_ShadowStrength", "_GradientStrength", "_BevelHighlight", "_MapTopY", "_MapHeight" }, Tints = { "_BaseColor", "_Color" };
    // What ModularMapRoot sets on a tile, per material slot ("none": not set, the material's own value shows), and the build's glow
    // (0 when not set: the same to the eye).
    static string[] Look(MeshRenderer tile)
    {
        var look = new List<string>(); var block = new MaterialPropertyBlock();
        for (int slot = 0; slot < tile.sharedMaterials.Length; slot++)
        {
            tile.GetPropertyBlock(block, slot);
            foreach (var name in Shading) look.Add($"slot {slot} {name}={(block.HasFloat(name) ? block.GetFloat(name).ToString("0.###") : "none")}");
            foreach (var name in Tints) look.Add($"slot {slot} {name}={(block.HasColor(name) ? block.GetColor(name).ToString("F3") : "none")}");
            look.Add($"slot {slot} _BuildGlow={(block.HasFloat("_BuildGlow") ? block.GetFloat("_BuildGlow") : 0):0.###}");
        }
        return look.ToArray();
    }

    // R2. The outline's colour does not depend on what is drawn behind it. One robot of the battle, alone in the battle camera's
    // view (its character renderers on a layer of their own), is drawn over the battle's dark backdrop colour and over white,
    // without MSAA, so each pixel is one surface. The outline's pixels are those that appear when the outline is turned on.
    static async Task<string> Outline()
    {
        const string surface = "CodingGame/Art/Character Surface";
        const int layer = 31, tolerance = 4, likeToday = 6; // a layer nothing is on; colour steps of 255
        var battle = await Battle();
        battle.EditorStartSession(battle.Stage, 1, true, Sandbox); // the map stays, the robots go
        var sim = battle.Simulation;
        bool Outlined(Renderer part) => part.sharedMaterials.Any(m => m && m.shader.name == surface && m.GetFloat("_OutlineWidth") > 0);
        var outlined = Enumerable.Range(0, battle.RobotDefinitions.Count).Where(i => battle.RobotDefinition(i).prefab.GetComponentsInChildren<Renderer>(true).Any(Outlined)).ToArray();
        if (outlined.Length == 0) return "no robot is drawn with the Character Surface outline";
        int index = outlined[0]; var spec = battle.RobotDefinition(index).stats;
        sim.GrantRobot(spec.role, 1);
        RobotState robot = null;
        for (float x = sim.Setup.Min.X + 1; robot == null && x < sim.Setup.Max.X - 1; x++)
            for (float y = sim.Setup.Min.Y + 1; robot == null && y < sim.Setup.Max.Y - 1; y++)
                if (sim.PlacementError(spec, new Point(x, y)) == null && battle.PlaceRobot(index, new Vector3(x, 0, y))) robot = sim.Robots.Last();
        if (robot == null) return "no place on the map for a robot";
        await Frames(3); // its animator has posed the model
        var view = Field<Dictionary<int, DefenseActorView>>(battle, "robotViews")[robot.Id];
        var parts = view.GetComponentsInChildren<Renderer>().Where(Outlined).ToArray();
        if (parts.Length == 0) return view.name + " shows no outlined renderer";
        var layers = parts.Select(part => part.gameObject.layer).ToArray();
        var worn = parts.Select(part => { var block = new MaterialPropertyBlock(); part.GetPropertyBlock(block); return block; }).ToArray();
        void Dress() { for (int i = 0; i < parts.Length; i++) parts[i].SetPropertyBlock(worn[i].isEmpty ? null : worn[i]); }
        var eye = battle.BattleCamera; var probe = new GameObject("RenderAudit camera").AddComponent<Camera>();
        var target = RenderTexture.GetTemporary(1920, 1080, 24);
        // The Editor draws a shader variant it has not compiled yet as a placeholder; the robot alone may need one the battle did not.
        bool later = UnityEditor.ShaderUtil.allowAsyncCompilation; UnityEditor.ShaderUtil.allowAsyncCompilation = false;
        try
        {
            probe.CopyFrom(eye); probe.enabled = false; probe.targetTexture = target; probe.allowMSAA = false;
            probe.projectionMatrix = eye.projectionMatrix; // the battle's lens offset (DefenseCameraLensOffset)
            probe.clearFlags = CameraClearFlags.SolidColor; probe.cullingMask = 1 << layer;
            foreach (var part in parts) part.gameObject.layer = layer;
            Color dark = eye.backgroundColor, edge = parts[0].sharedMaterial.GetColor("_OutlineColor");
            foreach (var part in parts) { var off = new MaterialPropertyBlock(); part.GetPropertyBlock(off); off.SetFloat("_OutlineWidth", 0); part.SetPropertyBlock(off); }
            var bare = Shot(probe, dark);
            Dress();
            Color32[] onDark = Shot(probe, dark), onLight = Shot(probe, Color.white);
            Color32 backdrop = dark;
            int count = 0, changed = 0, worst = 0; var sums = new long[6];
            for (int i = 0; i < bare.Length; i++)
            {
                if (Apart(bare[i], backdrop) > 2 || Apart(onDark[i], backdrop) <= tolerance) continue; // backdrop without the outline, something with it
                int apart = Apart(onDark[i], onLight[i]);
                count++; if (apart > tolerance) changed++; worst = Math.Max(worst, apart);
                sums[0] += onDark[i].r; sums[1] += onDark[i].g; sums[2] += onDark[i].b; sums[3] += onLight[i].r; sums[4] += onLight[i].g; sums[5] += onLight[i].b;
            }
            if (count < 30) return $"only {count} outline pixels of {view.name} in the battle camera's view (at {probe.WorldToViewportPoint(view.transform.position)})";
            string Mean(int at) => $"({sums[at] / count},{sums[at + 1] / count},{sums[at + 2] / count})";
            if (changed > 0) return $"{changed} of {count} outline pixels of {view.name} change with what is behind them: {Mean(0)} over the backdrop {Text(backdrop)}, {Mean(3)} over white (up to {worst}/255 apart)";
            // Opaque, it should stay the navy seen today where the dark backdrop was behind it: the colour mixed 85/15 with that backdrop.
            Color32 seen = new Color32((byte)(sums[0] / count), (byte)(sums[1] / count), (byte)(sums[2] / count), 255), today = Color.Lerp(dark.linear, edge.linear, edge.a).gamma;
            return Apart(seen, today) > likeToday ? $"the outline is {Text(seen)}, not today's navy {Text(today)} over the dark backdrop" : null;
        }
        finally
        {
            for (int i = 0; i < parts.Length; i++) parts[i].gameObject.layer = layers[i];
            Dress(); UnityEditor.ShaderUtil.allowAsyncCompilation = later;
            probe.targetTexture = null; RenderTexture.ReleaseTemporary(target); Object.Destroy(probe.gameObject);
        }
    }
    static Color32[] Shot(Camera camera, Color background)
    {
        camera.backgroundColor = background; camera.Render();
        var target = camera.targetTexture; var active = RenderTexture.active;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); RenderTexture.active = active;
        var pixels = image.GetPixels32(); Object.Destroy(image);
        return pixels;
    }
    static int Apart(Color32 a, Color32 b) => Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b)));
    static string Text(Color32 c) => $"({c.r},{c.g},{c.b})";
}
