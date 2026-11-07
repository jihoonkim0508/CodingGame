using System.Collections.Generic;
using UnityEngine;

namespace CodingGame.Defense
{
    // Views follow simulation time, so pause/speed and the isolated preview use identical trajectories.
    // Synced every frame: no LINQ, and a landed bomb's view waits switched off for the next launch (13차 133).
    sealed class DefenseProjectileVisuals
    {
        readonly Dictionary<int, Transform> views = new Dictionary<int, Transform>();
        readonly Stack<Transform> spare = new Stack<Transform>();
        readonly List<int> landed = new List<int>();
        readonly HashSet<int> flying = new HashSet<int>();
        public void Sync(DefenseSimulation simulation, Transform prefab, Transform parent, Vector3 offset)
        {
            flying.Clear();
            foreach (var shot in simulation.Projectiles) flying.Add(shot.Id);
            landed.Clear();
            foreach (var id in views.Keys) if (!flying.Contains(id)) landed.Add(id);
            foreach (var id in landed) { Park(views[id]); views.Remove(id); }
            // Shots at one enemy are the shooter's laser, drawn by DefenseHitVisuals.
            foreach (var shot in simulation.Projectiles)
            {
                if (shot.Target != 0) continue;
                if (!views.TryGetValue(shot.Id, out var view)) { view = Take(prefab, parent); views.Add(shot.Id, view); }
                float t = Mathf.Clamp01((float)((simulation.Time - shot.LaunchedAt) / (shot.ImpactAt - shot.LaunchedAt)));
                var p = System.Numerics.Vector2.Lerp(shot.Origin, shot.Destination, t);
                float height = Mathf.Lerp(1.1f, .12f, t) + Mathf.Sin(t * Mathf.PI) * 1.8f;
                view.position = offset + DefenseViews.World(p, height);
            }
        }
        Transform Take(Transform prefab, Transform parent)
        {
            while (spare.Count > 0)
            {
                var view = spare.Pop();
                if (!view) continue;   // destroyed with an old parent
                view.SetParent(parent, false); view.gameObject.SetActive(true);
                return view;
            }
            return Object.Instantiate(prefab, parent);
        }
        void Park(Transform view) { if (view) { view.gameObject.SetActive(false); spare.Push(view); } }
        public void Clear()
        {
            foreach (var view in views.Values) Park(view);
            views.Clear();
        }
    }
}
