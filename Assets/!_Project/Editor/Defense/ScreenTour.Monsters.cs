using System.Collections;
using System.Linq;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // The stage-2 monsters (MonsterModels) in real play from the battle camera, into TestResults/Editor/monster-models: 2-9's
    // imps, raptors and shield brutes walking the road past the placed robots, which stand idle (no code runs, so nothing
    // dies and every monster stays on its walk; leaks cost the core nothing): NN_battle.png and walk.mp4, then a close look
    // at one of each (the battle camera's angle zoomed in on it, NN_close_<type>.png) and walk_close.mp4 (zoomed in on the
    // road, half speed). Batchmode like the tour:
    //   Unity.exe -batchmode -projectPath . -executeMethod CodingGame.Defense.Editor.ScreenTour.RunMonsterModels -logFile m.log
    public static partial class ScreenTour
    {
        public static void RunMonsterModels() => Start("TestResults/Editor/monster-models", MonsterModelsTour());
        static readonly DefenseTestOptions Idle = new DefenseTestOptions { tutorial = false, tutorialInput = false, robotCode = false, baseDamage = false };

        static IEnumerator MonsterModelsTour()
        {
            yield return Wave29();
            yield return Seconds(.6); // the Editor compiles the new shaders on first sight
            yield return Shot("battle");
            yield return Record("walk", 6);
            // Again from the wave's start for the close looks, while every kind is still on the road.
            yield return Wave29();
            foreach (var type in new[] { EnemyType.Normal, EnemyType.Fast, EnemyType.Tank }) yield return Close(type.ToString(), type);
            var walker = Pick(EnemyType.Fast, .3f) ?? Pick(EnemyType.Normal, .3f);
            if (walker)
            {
                var restore = Zoom(walker.transform.position, 8);
                DevTest.Speed(.5f);
                yield return Record("walk_close", 6);
                restore(); DevTest.Speed(1);
            }
        }

        // 시안/탱커 크기: the shield brute (Heavy) at 100, 90 and 80% of its size in the same moments, into
        // TestResults/Editor/brute-sizes: 2-9 stopped at one battle time (NN_<size> 전투 화면.png), the brute nearest a warrior
        // robot zoomed in at one fixed framing (근접) and 2-10's boss brute after its entrance (보스); logs each one's height on a
        // 1080-line screen against the warrior's. Heavy is put back to 100% at the end.
        public static void RunBruteSizes() => Start("TestResults/Editor/brute-sizes", BruteSizes(true));
        public static void RunBruteBossSizes() => Start("TestResults/Editor/brute-sizes-boss", BruteSizes(false));
        // A boss wave fails the moment a minion gets through, so the robots fight until the boss is on the road (the core
        // takes no damage), then stand idle while it walks.
        static readonly DefenseTestOptions Fight = new DefenseTestOptions { tutorial = false, tutorialInput = false, baseDamage = false };

        static IEnumerator BruteSizes(bool battle)
        {
            double moment = 0, bossMoment = 0; float current = 1;
            foreach (var size in new[] { 1f, .9f, .8f })
            {
                if (size != current) { ScaleModel("Heavy", size / current); current = size; }
                string tag = $"{size * 100:0}%";
                if (!battle) goto Boss;
                DevTest.PlayDefense(2, 9, true, Idle);
                yield return Until(() => DevTest.Ready);
                yield return Seconds(.5);
                Deploy();
                foreach (var hover in Object.FindObjectsByType<DefenseHoverInfo>(FindObjectsInactive.Include, FindObjectsSortMode.None)) hover.enabled = false;
                DevTest.Battle.StartBattle();
                var sim = DevTest.Sim;
                if (moment == 0) // the first size picks the moment: several of each kind on screen
                {
                    int Count(EnemyType type) => sim.Enemies.Count(e => e.Active && e.Spec.type == type && OnScreen(e));
                    DevTest.Speed(3);
                    yield return Until(() => Count(EnemyType.Normal) >= 3 && Count(EnemyType.Fast) >= 3 && Count(EnemyType.Tank) >= 2, 40);
                    moment = System.Math.Ceiling(sim.Time);
                }
                yield return RunTo(moment);
                yield return Shot(tag + " 전투 화면");
                var tank = sim.Enemies.Where(e => e.Active && e.Spec.type == EnemyType.Tank)
                    .OrderBy(e => sim.Robots.Min(r => System.Numerics.Vector2.Distance(r.Position, e.Position))).First();
                var brute = ViewAt(tank.Position);
                var warrior = ViewAt(sim.Robots.Where(r => r.Spec.NativeAction == RobotAction.Slash)
                    .OrderBy(r => System.Numerics.Vector2.Distance(r.Position, tank.Position)).First().Position);
                float b = ScreenPx(brute), w = ScreenPx(warrior);
                Debug.Log($"[BruteSizes] {tag} t={sim.Time:0.00}: brute #{tank.Id} {b:0} px, warrior {w:0} px, ratio {b / w:0.00}");
                var restore = Zoom(brute.transform.position + Vector3.up * 2, 11);
                yield return Seconds(.3);
                yield return Shot(tag + " 근접");
                restore();
                DevTest.Speed(1);
                continue;

            Boss:
                // 2-10's boss brute, the boss warning over, at one battle time.
                DevTest.Speed(1);
                DevTest.PlayDefense(2, 10, true, Fight);
                yield return Until(() => DevTest.Ready);
                yield return Seconds(.5);
                Deploy();
                foreach (var hover in Object.FindObjectsByType<DefenseHoverInfo>(FindObjectsInactive.Include, FindObjectsSortMode.None)) hover.enabled = false;
                DevTest.Battle.StartBattle(); DevTest.Speed(4);
                var sim2 = DevTest.Sim;
                yield return Until(() => sim2.Enemies.Any(e => e.Active && e.Boss) || sim2.Phase == BattlePhase.Defeat, 240);
                DevTest.SetOptions(Idle);
                DevTest.Speed(1);
                sim = sim2;
                yield return Seconds(DefenseBossWarning.Seconds + .5);
                if (bossMoment == 0) bossMoment = System.Math.Ceiling(sim.Time) + 1;
                yield return RunTo(bossMoment);
                var bossState = sim.Enemies.FirstOrDefault(e => e.Active && e.Boss);
                Debug.Log($"[BruteSizes] {tag} 2-10 t={sim.Time:0.00} {sim.Phase} wave {sim.WaveIndex + 1}: entered {DevTest.Battle.EditorBossEntered}, {sim.Enemies.Count(e => e.Active)} on the road, boss {(bossState == null ? "none" : "#" + bossState.Id)}");
                if (bossState == null) continue;
                var boss = ViewAt(bossState.Position);
                var near = ViewAt(sim.Robots.Where(r => r.Spec.NativeAction == RobotAction.Slash)
                    .OrderBy(r => System.Numerics.Vector2.Distance(r.Position, bossState.Position)).First().Position);
                float bb = ScreenPx(boss), ww = ScreenPx(near);
                Debug.Log($"[BruteSizes] {tag} boss t={sim.Time:0.00}: boss {bb:0} px, warrior {ww:0} px, ratio {bb / ww:0.00}");
                restore = Zoom(boss.transform.position + Vector3.up * 3, 18);
                yield return Seconds(.3);
                yield return Shot(tag + " 보스");
                restore();
                DevTest.Speed(1);
            }
            ScaleModel("Heavy", 1 / current);
        }

        // The battle run on to one battle time and held there.
        static IEnumerator RunTo(double time)
        {
            var sim = DevTest.Sim;
            DevTest.Speed(3);
            yield return Until(() => sim.Time >= time - .4, 120);
            DevTest.Speed(.5f);
            yield return Until(() => sim.Time >= time, 20);
            DevTest.Speed(0);
            yield return Seconds(.4);
        }

        static DefenseActorView ViewAt(System.Numerics.Vector2 at) => Object.FindObjectsByType<DefenseActorView>(FindObjectsSortMode.None)
            .OrderBy(v => (v.transform.position - new Vector3(at.X, 0, at.Y)).sqrMagnitude).First();

        // A size draft: the prefab's model scaled by k about the prefab's origin, its health bar and walk (planted feet) with it,
        // as MonsterModels' size would make them.
        static void ScaleModel(string prefab, float k)
        {
            string path = "Assets/!_Project/Defense/Prefabs/" + prefab + ".prefab";
            var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
            try
            {
                var model = root.transform.Find("Model"); model.localScale *= k; model.localPosition *= k;
                root.transform.Find("Health").localPosition *= k;
                var view = new UnityEditor.SerializedObject(root.GetComponent<DefenseActorView>());
                view.FindProperty("walkSpeed").floatValue *= k; view.ApplyModifiedPropertiesWithoutUndo();
                UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
        }

        // An actor's silhouette height on a 1080-line screen from the battle camera: its posed meshes, projected.
        static float ScreenPx(DefenseActorView view)
        {
            var camera = BattleCamera; float lo = float.MaxValue, hi = float.MinValue;
            foreach (var r in view.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r.GetComponentsInParent<Transform>(true).Any(t => t.name == "Health")) continue;
                System.Collections.Generic.IEnumerable<Vector3> points;
                if (r is SkinnedMeshRenderer skin)
                {
                    // skinned by hand (bones and bind poses), so the pose and every scale above it count exactly once
                    var mesh = skin.sharedMesh; var weights = mesh.boneWeights;
                    var bone = skin.bones.Select((b, i) => b.localToWorldMatrix * mesh.bindposes[i]).ToArray();
                    points = mesh.vertices.Select((v, i) =>
                    {
                        var w = weights[i];
                        return bone[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0 + bone[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1 +
                               bone[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2 + bone[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                    });
                }
                else if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh) points = filter.sharedMesh.vertices.Select(r.transform.TransformPoint);
                else continue;
                foreach (var p in points) { float y = camera.WorldToScreenPoint(p).y; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
            }
            return (hi - lo) * 1080f / camera.pixelHeight;
        }

        // 2-9 at three times speed until several of each kind walk on screen.
        static IEnumerator Wave29()
        {
            DevTest.PlayDefense(2, 9, true, Idle);
            yield return Until(() => DevTest.Ready);
            yield return Seconds(.5);
            Deploy();
            // The batchmode pointer rests on a wave card, whose tooltip came and went over the battle.
            foreach (var hover in Object.FindObjectsByType<DefenseHoverInfo>(FindObjectsInactive.Include, FindObjectsSortMode.None)) hover.enabled = false;
            DevTest.Battle.StartBattle();
            var sim = DevTest.Sim;
            int Count(EnemyType type) => sim.Enemies.Count(e => e.Active && e.Spec.type == type && OnScreen(e));
            DevTest.Speed(3);
            yield return Until(() => Count(EnemyType.Normal) >= 3 && Count(EnemyType.Fast) >= 3 && Count(EnemyType.Tank) >= 2, 40);
            DevTest.Speed(1);
            Debug.Log($"[MonsterModels] on screen: {Count(EnemyType.Normal)} imps, {Count(EnemyType.Fast)} raptors, {Count(EnemyType.Tank)} brutes");
        }

        static Camera BattleCamera => Camera.main ? Camera.main : Object.FindFirstObjectByType<Camera>();
        static bool OnScreen(EnemyState e)
        {
            var at = BattleCamera.WorldToViewportPoint(new Vector3(e.Position.X, 0, e.Position.Y));
            return at.z > 0 && at.x > .08f && at.x < .92f && at.y > .12f && at.y < .85f;
        }

        // The view of a walking enemy of the type on screen whose facing points down the screen by at least -facing: the one
        // turned furthest toward the camera; alone: no other enemy within 2.5 units (they share the road and overlap).
        static DefenseActorView Pick(EnemyType type, float facing, bool alone = false)
        {
            var views = Object.FindObjectsByType<DefenseActorView>(FindObjectsSortMode.None);
            var sim = DevTest.Sim;
            return sim.Enemies.Where(e => e.Active && !e.Boss && e.Spec.type == type && OnScreen(e) && e.BlockedBy == 0 &&
                    (!alone || sim.Enemies.All(o => o == e || !o.Active || System.Numerics.Vector2.Distance(o.Position, e.Position) > 2.5f)))
                .Select(e => views.OrderBy(v => (v.transform.position - new Vector3(e.Position.X, 0, e.Position.Y)).sqrMagnitude).First())
                .Where(v => v.transform.forward.z <= facing).OrderBy(v => v.transform.forward.z).FirstOrDefault();
        }

        // The camera at its own angle zoomed in on a point (height world units fill the screen's height), the lens offset and
        // the HUD off but the health bars; returns what puts everything back.
        static System.Action Zoom(Vector3 at, float height)
        {
            var camera = BattleCamera;
            var lens = camera.GetComponent<DefenseCameraLensOffset>();
            var pose = (camera.transform.position, camera.transform.rotation, camera.fieldOfView);
            var bars = Object.FindFirstObjectByType<DefenseHealthBars>();
            var hidden = Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None)
                .Where(g => g.enabled && !(bars && (g.transform.IsChildOf(bars.transform) || bars.transform.IsChildOf(g.transform)))).ToArray();
            if (lens) lens.enabled = false;
            foreach (var g in hidden) g.enabled = false;
            camera.fieldOfView = height / 33 * Mathf.Rad2Deg;
            camera.transform.position = at - camera.transform.forward * 33;
            return () =>
            {
                camera.transform.SetPositionAndRotation(pose.position, pose.rotation); camera.fieldOfView = pose.fieldOfView;
                if (lens) lens.enabled = true;
                foreach (var g in hidden) if (g) g.enabled = true;
            };
        }

        // The battle held still on one walking enemy of the type, preferably coming down the screen, else across it, zoomed in
        // on it with its health bar: NN_close_<name>.png.
        static IEnumerator Close(string name, EnemyType type)
        {
            yield return Until(() => Pick(type, -.5f, true) != null, 20);
            if (!Pick(type, -.5f, true)) yield return Until(() => Pick(type, .3f, true) != null, 10);
            var view = Pick(type, .3f, true) ?? Pick(type, .3f);
            if (!view) { Debug.LogWarning("[MonsterModels] no " + name + " to photograph"); yield break; }
            DevTest.Speed(0);
            yield return Seconds(.2);
            float tall = view.HealthAnchor.y - view.transform.position.y + .3f; // up to its health bar
            var restore = Zoom(view.transform.position + Vector3.up * tall * .45f, tall * 1.6f);
            yield return Seconds(.3);
            yield return Shot("close_" + name);
            Debug.Log($"[MonsterModels] close {name}: facing {view.transform.forward}");
            restore();
            DevTest.Speed(1);
        }
    }
}
