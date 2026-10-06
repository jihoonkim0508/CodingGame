using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodingGame.Defense
{
    [Serializable]
    sealed class DefenseHitVisuals
    {
        [SerializeField] ParticleSystem prefab;
        [SerializeField, Min(.01f)] float lifetime = 1.5f;
        readonly List<(ParticleSystem effect, float remaining)> active = new List<(ParticleSystem, float)>();
        public bool IsConfigured => prefab && lifetime > 0 && !float.IsInfinity(lifetime);

        public void Show(CombatEvent evt, Transform parent, Vector3 offset, Quaternion rotation)
        {
            if ((evt.Kind != "hit" && evt.Kind != "projectile-hit") || evt.Amount <= 0) return;
            var effect = UnityEngine.Object.Instantiate(prefab,
                offset + new Vector3(evt.Position.X, .45f, evt.Position.Y), rotation, parent);
            // Manual simulation keeps every child particle in sync with battle pause and speed.
            effect.Simulate(0, true, true, false);
            active.Add((effect, lifetime));
        }

        public void Tick(float elapsed)
        {
            if (elapsed <= 0) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var (effect, remaining) = active[i];
                effect.Simulate(elapsed, true, false, false);
                remaining -= elapsed;
                // Simulate leaves systems paused, so IsAlive remains true after their last particle.
                if (remaining > 0) { active[i] = (effect, remaining); continue; }
                UnityEngine.Object.Destroy(effect.gameObject);
                active.RemoveAt(i);
            }
        }

        public void Clear()
        {
            foreach (var item in active)
                if (item.effect) { item.effect.gameObject.SetActive(false); UnityEngine.Object.Destroy(item.effect.gameObject); }
            active.Clear();
        }
    }
}
