using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CodingGame.Defense
{
    // Views follow simulation time, so pause/speed and the isolated preview use identical trajectories.
    sealed class DefenseProjectileVisuals
    {
        readonly Dictionary<int, Transform> views = new Dictionary<int, Transform>();
        public void Sync(DefenseSimulation simulation, Transform prefab, Transform parent, Vector3 offset)
        {
            foreach (var id in views.Keys.Where(id => !simulation.Projectiles.Any(p => p.Id == id)).ToArray())
            { Object.Destroy(views[id].gameObject); views.Remove(id); }
            foreach (var shot in simulation.Projectiles)
            {
                if (!views.TryGetValue(shot.Id, out var view)) { view = Object.Instantiate(prefab, parent); views.Add(shot.Id, view); }
                float t = Mathf.Clamp01((float)((simulation.Time - shot.LaunchedAt) / (shot.ImpactAt - shot.LaunchedAt)));
                var p = System.Numerics.Vector2.Lerp(shot.Origin, shot.Destination, t);
                float height = Mathf.Lerp(1.1f, .12f, t) + Mathf.Sin(t * Mathf.PI) * 1.8f;
                view.position = offset + new Vector3(p.X, height, p.Y);
            }
        }
        public void Clear()
        {
            foreach (var view in views.Values) if (view) { view.gameObject.SetActive(false); Object.Destroy(view.gameObject); }
            views.Clear();
        }
    }
}
