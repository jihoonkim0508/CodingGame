// Audit of the battle HUD and the actor/hit visuals (H1-H11). Run in Play Mode in the Defense scene through the Unity MCP run_script tool:
//   1. entry HudAuditCheck.Main      -> one "FAIL Hn: ..." line per failing check, then "AUDIT hud: <passed>/<total> passed".
//      It ends by opening a result panel and the Esc menu for H9 (needs real frames).
//   2. wait about 2 seconds, then entry HudAuditCheck.MenuEnd -> the H9 line and "AUDIT hud (menu step): <passed>/1 passed";
//      it closes the menu and restarts the session, so the editor is left out of the menu.
// Nothing here is compiled by Unity: private members are reached by reflection, and the HUD's LateUpdate is invoked by hand so
// most checks need no frame. Each check starts its own session (battle.EditorStartSession) with the tutorial off.
// H4 only guards the boss goal line (see the note on it); H7 assumes the shooter's impact is drawn when its "hit" lands.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using CodingGame.BlockCoding;
using CodingGame.Defense;
using Point = System.Numerics.Vector2;

public static class HudAuditCheck
{
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static DefenseBattle battle;
    static DefensePlayerHUD hud;
    const string MarkerName = "HudAuditMenuStart@";

    // --- helpers ---
    static FieldInfo Field(object o, string name)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, All | BindingFlags.DeclaredOnly);
            if (f != null) return f;
        }
        throw new Exception("no field " + name + " on " + o.GetType().Name);
    }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static void SetPhase(DefenseSimulation sim, BattlePhase phase) => typeof(DefenseSimulation).GetProperty("Phase").GetSetMethod(true).Invoke(sim, new object[] { phase });
    static DefenseSimulation Fresh(int stage, int wave)
    {
        battle.EditorStartSession(stage, wave, true, new DefenseTestOptions { tutorial = false, tutorialInput = false, unlockAll = true, inventoryLimit = false });
        return battle.Simulation;
    }
    static void Late(DefensePlayerHUD h) => typeof(DefensePlayerHUD).GetMethod("LateUpdate", All).Invoke(h, null);
    static int Place(DefenseSimulation sim, RobotRole role)
    {
        int index = Enumerable.Range(0, 6).Single(i => battle.DefinitionRole(i) == role);
        for (float x = sim.Setup.Min.X + 1; x < sim.Setup.Max.X - 1; x++)
            for (float y = sim.Setup.Min.Y + 1; y < sim.Setup.Max.Y - 1; y++)
                if (sim.PlacementError(battle.RobotDefinition(index).stats, new Point(x, y)) == null &&
                    battle.PlaceRobot(index, new Vector3(x, 0, y))) return sim.Robots.Last().Id;
        throw new Exception("No legal placement for " + role);
    }
    static DefenseActorView ViewOf(int robotId) => Get<Dictionary<int, DefenseActorView>>(battle, "robotViews")[robotId];

    static int total, passed;
    static readonly List<string> lines = new List<string>();
    static void Run(string name, Func<string> body)
    {
        total++;
        string problem;
        try { problem = body(); }
        catch (Exception e) { problem = "threw " + (e is TargetInvocationException && e.InnerException != null ? e.InnerException : e); }
        if (problem == null) passed++; else lines.Add("FAIL " + name + ": " + problem);
    }

    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Enter Play Mode first");
        battle = UnityEngine.Object.FindFirstObjectByType<DefenseBattle>();
        hud = UnityEngine.Object.FindFirstObjectByType<DefensePlayerHUD>();
        if (hud.MenuOpen) hud.ToggleMenu();
        foreach (var old in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name.StartsWith(MarkerName)).ToList()) UnityEngine.Object.Destroy(old.gameObject);
        total = passed = 0; lines.Clear();

        Run("H1 victory text wave count", () =>
        {
            var problems = new List<string>();
            for (int stage = 1; stage <= 3; stage++)
            {
                var sim = Fresh(stage, 1);
                int waves = sim.Setup.Waves.Length, all = sim.Setup.CampaignWaves.Sum(s => s.Length);
                Late(hud); SetPhase(sim, BattlePhase.Victory); Late(hud);
                string text = Get<TMP_Text>(hud, "rewardItems").text;
                // The last built stage (3-4 won) shows the prototype end, whose wave count is the stage's last wave, "3-4까지".
                if (battle.PrototypeEnded)
                {
                    if (!text.Contains("프로토타입") || !text.Contains($"{stage}-{waves}까지")) problems.Add($"stage {stage} prototype-end text lacks '{stage}-{waves}까지': {text}");
                    continue;
                }
                if (stage == 3) problems.Add("stage 3 won without the prototype end");
                var said = Regex.Match(text, @"(\d+)개 웨이브 방어 성공");
                if (said.Success && int.Parse(said.Groups[1].Value) != waves) problems.Add($"stage {stage} has {waves} waves but the victory text says {said.Groups[1].Value}");
                if (!said.Success && !text.Contains($"총 {all}개 웨이브")) problems.Add($"stage {stage} final text lacks the real total {all}");
            }
            return problems.Count == 0 ? null : string.Join("; ", problems);
        });

        Run("H2 goal banner says 목표 once", () =>
        {
            var sim = Fresh(2, 1);
            Late(hud);
            var guide = Get<TMP_Text>(hud, "guide");
            var parent = guide.transform.parent;
            if (!parent.gameObject.activeSelf) return "test setup: goal banner not shown in 2-1";
            string all = string.Join(" | ", parent.GetComponentsInChildren<TMP_Text>(false).Select(t => t.text));
            int count = Regex.Matches(all, "목표").Count;
            return count == 1 ? null : $"\"목표\" appears {count} times in the banner: {all}";
        });

        Run("H3 boss gets its own next-wave card", () =>
        {
            var problems = new List<string>();
            for (int stage = 1; stage <= 3; stage++)
            {
                int last = Fresh(stage, 1).Setup.Waves.Length;
                var sim = Fresh(stage, last);
                Late(hud);
                var groups = sim.UpcomingWave;
                if (!groups.Any(g => g.Boss)) { problems.Add($"test setup: {stage}-{last} has no boss group"); continue; }
                var expected = groups.GroupBy(g => (g.DefinitionIndex, g.Boss)).Select(g => g.Sum(x => x.Count)).OrderBy(n => n).ToList();
                var cards = Get<GameObject[]>(hud, "enemyCards"); var counts = Get<TMP_Text[]>(hud, "enemyCounts");
                var shown = Enumerable.Range(0, cards.Length).Where(i => cards[i].activeSelf)
                    .Select(i => int.Parse(Regex.Match(counts[i].text, @"\d+").Value)).OrderBy(n => n).ToList();
                if (expected.Count > cards.Length) { problems.Add($"{stage}-{last} needs {expected.Count} cards, only {cards.Length} exist"); continue; }
                if (!shown.SequenceEqual(expected)) problems.Add($"{stage}-{last} cards show [{string.Join(",", shown)}], expected [{string.Join(",", expected)}] (boss counted apart from its minions)");
            }
            return problems.Count == 0 ? null : string.Join("; ", problems);
        });

        // Not failing today in the real campaign: stages 2-3 read the goal from the lesson data, and 1-5's hard-coded line only
        // shows under the stage-1 tutorial's B01 step, which a synthetic session cannot reach (reported as skipped).
        // It guards the fix: every boss wave that shows a goal line names the boss and the small enemies, with no stage/wave number.
        Run("H4 boss goal line", () =>
        {
            var problems = new List<string>();
            for (int stage = 1; stage <= 3; stage++)
            {
                int last = Fresh(stage, 1).Setup.Waves.Length;
                Fresh(stage, last); Late(hud);
                var guide = Get<TMP_Text>(hud, "guide");
                if (!guide.transform.parent.gameObject.activeSelf) continue; // stage 1: goal comes with the tutorial
                string text = guide.text;
                if (!text.Contains("보스") || !text.Contains("작은")) problems.Add($"{stage}-{last} goal \"{text}\" does not tell to stop the small enemies and beat the boss");
                if (Regex.IsMatch(text, @"\d")) problems.Add($"{stage}-{last} goal \"{text}\" names a stage/wave number");
            }
            return problems.Count == 0 ? null : string.Join("; ", problems);
        });

        // Contract: TickVisual(delta) gets the battle clock's delta (DefenseBattle passes Simulation.Time's change, already scaled by
        // battle speed, 0 outside a running wave); SetPace only drives the animator and the thrusters.
        Run("H5 spin follows battle speed once", () =>
        {
            var sim = Fresh(1, 1);
            var view = ViewOf(Place(sim, RobotRole.Warrior)); var head = Get<Transform>(view, "head");
            var target = head.position + new Vector3(3, 0, 0);
            float Turn(float pace) { view.SetPace(pace); view.Spin(target, 1f); var r0 = head.rotation; view.TickVisual(.1f); return Quaternion.Angle(r0, head.rotation); }
            float expected = 360 * Mathf.SmoothStep(0, 1, .1f / 2); // .1 s of battle time into a 2 s turn
            float at1 = Turn(1), at3 = Turn(3);
            view.SetPace(1);
            if (Mathf.Abs(at1 - expected) > .3f) return $"test setup: pace 1 turned {at1:0.0}°, expected {expected:0.0}°";
            return Mathf.Abs(at3 - expected) <= .3f ? null : $"at pace 3 the same .1 s of battle time turned {at3:0.0}° instead of {expected:0.0}° (battle speed applied twice)";
        });
        Run("H5 hover follows battle speed once", () =>
        {
            var sim = Fresh(1, 1);
            var view = ViewOf(Place(sim, RobotRole.Warrior));
            var bob = new GameObject("hover test").transform; bob.SetParent(view.transform, false);
            Set(view, "hover", bob); Set(view, "hoverY", 0f); Set(view, "hoverTime", 0f);
            view.SetPace(3); view.TickVisual(.1f);
            float time = Get<float>(view, "hoverTime");
            view.SetPace(1); Set(view, "hover", null); UnityEngine.Object.Destroy(bob.gameObject);
            return Mathf.Abs(time - .1f) < .001f ? null : $"hover clock moved {time:0.000} s for .1 s of battle time at pace 3 (battle speed applied twice)";
        });
        Run("H5 visuals cleared puts the model back", () =>
        {
            var sim = Fresh(1, 1);
            var view = ViewOf(Place(sim, RobotRole.Warrior)); var head = Get<Transform>(view, "head");
            view.SetPace(1); view.Spin(head.position + new Vector3(3, 0, 0), 1f);
            var facing = head.rotation; view.TickVisual(.5f);
            float mid = Quaternion.Angle(facing, head.rotation);
            view.ClearVisuals();
            float after = Quaternion.Angle(facing, head.rotation);
            if (mid < 20) return $"test setup: spin only turned {mid:0.0}° mid-way";
            return after < 1 ? null : $"model left {after:0.0}° from its facing after ClearVisuals (a wave ending mid-spin)";
        });

        Run("H6 no beam to itself on buff/stun", () =>
        {
            var sim = Fresh(1, 1);
            int id = Place(sim, RobotRole.Shooter); var view = ViewOf(id); var beam = Get<LineRenderer>(view, "beam");
            var fx = Get<object>(battle, "hitEffects"); var show = fx.GetType().GetMethod("Show");
            var own = sim.FindRobot(id).Position; var problems = new List<string>();
            foreach (var kind in new[] { "buff", "stun" })
            {
                view.ClearVisuals();
                show.Invoke(fx, new object[] { new CombatEvent { Kind = kind, Melee = false, Source = id, Position = own }, view.transform.parent, Vector3.zero, Quaternion.identity, view, null });
                if (beam.enabled) problems.Add($"a {kind} event draws a beam to the robot's own position");
            }
            return problems.Count == 0 ? null : string.Join("; ", problems);
        });

        Run("H7 laser impact at the enemy when the hit lands", () =>
        {
            var sim = Fresh(1, 1);
            int id = Place(sim, RobotRole.Shooter); var view = ViewOf(id);
            if (!view.HasMuzzle) return "test setup: shooter view has no muzzle";
            var fx = Get<object>(battle, "hitEffects"); var type = fx.GetType(); var active = Get<IList>(fx, "active");
            type.GetMethod("Clear").Invoke(fx, null);
            var from = sim.FindRobot(id).Position; Point launched = from + new Point(6, 0), landed = from + new Point(6, 5);
            object[] Args(CombatEvent e) => new object[] { e, view.transform.parent, Vector3.zero, Quaternion.identity, view, null };
            type.GetMethod("Show").Invoke(fx, Args(new CombatEvent { Kind = "shot", Source = id, Position = launched, Seconds = .5f }));
            type.GetMethod("Tick").Invoke(fx, new object[] { .6f });  // the shot has arrived
            type.GetMethod("Show").Invoke(fx, Args(new CombatEvent { Kind = "hit", Source = id, Position = landed, Amount = 10 })); // the enemy has moved on
            float Near(Point p) => active.Cast<ValueTuple<ParticleSystem, float>>().Select(e => Vector2.Distance(new Vector2(e.Item1.transform.position.x, e.Item1.transform.position.z), new Vector2(p.X, p.Y))).DefaultIfEmpty(99).Min();
            var problems = new List<string>();
            if (Near(launched) < .6f) problems.Add("an impact effect sits where the enemy was at launch");
            if (Near(landed) > .6f) problems.Add("no impact effect where the enemy is when the hit lands");
            type.GetMethod("Clear").Invoke(fx, null);
            return problems.Count == 0 ? null : string.Join("; ", problems);
        });

        Run("H8 result panel first frame is the animation start", () =>
        {
            var sim = Fresh(1, 1);
            Late(hud);                                   // Ready: panel hidden, result timer reset
            SetPhase(sim, BattlePhase.Reward); Late(hud); // the frame the panel first shows
            var panel = Get<GameObject>(hud, "rewardPanel");
            if (!panel.activeSelf) return "test setup: result panel did not open";
            var group = panel.GetComponent<CanvasGroup>();
            float alpha = group ? group.alpha : 1;
            return alpha <= .05f ? null : $"panel shows with alpha {alpha:0.00} on its first frame (should start at 0 and fade in)";
        });

        Run("H10 guide has every handed-out block", () =>
        {
            var sim = Fresh(2, 1); var progression = sim.Setup.Progression;
            var handed = new HashSet<BlockKind>();
            foreach (var l in progression.lessons) foreach (var b in l.blocks.Concat(l.rewards)) handed.Add(DefenseProgression.Canonical(b.kind));
            foreach (var b in progression.initial) handed.Add(DefenseProgression.Canonical(b.kind));
            foreach (var d in progression.drops) handed.Add(DefenseProgression.Canonical(d.kind));
            var help = UnityEngine.Object.FindFirstObjectByType<DefenseBlockHelp>(FindObjectsInactive.Include);
            var have = new HashSet<BlockKind>(Get<DefenseBlockHelp.Entry[]>(help, "entries").Select(e => DefenseProgression.Canonical(e.kind)));
            var missing = handed.Where(k => !have.Contains(k)).Select(DefenseProgression.Label).ToList();
            return missing.Count == 0 ? null : "no guide entry for: " + string.Join(", ", missing);
        });
        Run("H10 guide examples use current names", () =>
        {
            var help = UnityEngine.Object.FindFirstObjectByType<DefenseBlockHelp>(FindObjectsInactive.Include);
            // The names the editor's blocks print (palette labels), plus Python's range and the distance method.
            var known = new HashSet<string>(Enum.GetValues(typeof(BlockKind)).Cast<BlockKind>().Select(k => Regex.Match(DefenseProgression.Label(k), @"^[a-z_]+(?=\()").Value)
                .Where(n => n != "")) { "range", "distance", "robot" }; // robot: the "def robot():" the examples wrap their code in
            var problems = new List<string>();
            foreach (var e in Get<DefenseBlockHelp.Entry[]>(help, "entries"))
            {
                foreach (Match m in Regex.Matches(e.example ?? "", @"[A-Za-z_][A-Za-z0-9_]*(?=\()"))
                    if (!known.Contains(m.Value)) problems.Add($"{e.title}: example calls {m.Value}()");
                if (Regex.IsMatch((e.example ?? "") + (e.description ?? ""), @"get_distance|\bAttack\(\)")) problems.Add($"{e.title}: old name in text");
            }
            return problems.Count == 0 ? null : string.Join("; ", problems.Distinct());
        });

        Run("H11 shooter text matches the damage rule", () =>
        {
            var shooter = battle.RobotDefinitions.Single(r => r.stats.role == RobotRole.Shooter);
            // Damage does not depend on distance (inverse-distance bonus dropped 2026-11-18): no text may promise more up close.
            string text = shooter.description;
            return new[] { "1m", "1/3", "반비례", "가까운 적일수록", "최대 피해" }.Any(text.Contains) ? "description still describes distance-based damage: " + text : null;
        });

        // H9 step 1: a result panel and the Esc menu opened together; MenuEnd reads them after real time has passed.
        try
        {
            var sim = Fresh(1, 1);
            Late(hud); SetPhase(sim, BattlePhase.Reward); Late(hud);
            hud.ToggleMenu();
            // run_script compiles a fresh assembly per call, so statics do not survive: keep the start time on a scene object.
            new GameObject(MarkerName + Time.realtimeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception e) { lines.Add("FAIL H9 setup: threw " + e); }

        lines.Add($"AUDIT hud: {passed}/{total} passed (H9 pending: wait 2 s, then run HudAuditCheck.MenuEnd)");
        return string.Join("\n", lines);
    }

    public static string MenuEnd()
    {
        if (!Application.isPlaying) throw new Exception("Enter Play Mode first");
        battle = UnityEngine.Object.FindFirstObjectByType<DefenseBattle>();
        hud = UnityEngine.Object.FindFirstObjectByType<DefensePlayerHUD>();
        var marker = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name.StartsWith(MarkerName));
        if (!marker || !hud.MenuOpen) return "H9 not started: run HudAuditCheck.Main first";
        float waited = Time.realtimeSinceStartup - float.Parse(marker.name.Substring(MarkerName.Length), System.Globalization.CultureInfo.InvariantCulture);
        if (waited < 1.6f) return $"too early ({waited:0.0} s): wait about 2 s and run MenuEnd again";
        string problem = null;
        try
        {
            var panel = Get<GameObject>(hud, "rewardPanel"); var group = panel.GetComponent<CanvasGroup>();
            float alpha = group ? group.alpha : 1;
            // The panel fades in after 1.3 s of result time; with the menu open that time must not pass.
            if (alpha > .05f) problem = $"after {waited:0.0} s behind the Esc menu the result panel has alpha {alpha:0.00}: its timers ran while GameClock was paused";
        }
        finally
        {
            if (hud.MenuOpen) hud.ToggleMenu();
            UnityEngine.Object.Destroy(marker.gameObject);
            Fresh(1, 1);
        }
        return (problem == null ? "" : "FAIL H9 result animation waits for the menu: " + problem + "\n") + $"AUDIT hud (menu step): {(problem == null ? 1 : 0)}/1 passed";
    }
}
