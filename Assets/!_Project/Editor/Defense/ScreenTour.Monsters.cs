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
