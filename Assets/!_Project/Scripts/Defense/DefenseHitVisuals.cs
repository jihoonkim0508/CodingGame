using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace CodingGame.Defense
{
    [Serializable]
    sealed class DefenseHitVisuals
    {
        [SerializeField] ParticleSystem prefab;
        [SerializeField] VisualEffect slashPrefab;
        [SerializeField] DefenseRangeRing hitRangePrefab;
        [SerializeField, Min(.01f)] float lifetime = 1.5f;
        readonly List<(ParticleSystem effect, float remaining)> active = new List<(ParticleSystem, float)>();
        readonly List<(VisualEffect effect, float remaining)> slashes = new List<(VisualEffect, float)>();
        readonly List<(DefenseRangeRing ring, float remaining)> hitRanges = new List<(DefenseRangeRing, float)>();
        bool showHitRanges;
        public bool IsConfigured => prefab && slashPrefab && slashPrefab.visualEffectAsset && hitRangePrefab && lifetime > 0 && !float.IsInfinity(lifetime);
        public void SetHitRangesVisible(bool visible)
        {
            showHitRanges = visible;
            if (visible) return;
            foreach (var item in hitRanges) { item.ring.Hide(); UnityEngine.Object.Destroy(item.ring.gameObject); }
            hitRanges.Clear();
        }

        public void Show(CombatEvent evt, Transform parent, Vector3 offset, Quaternion rotation)
        {
            if (evt.Kind == "slash")
            {
                var origin = offset + new Vector3(evt.Origin.X, .1f, evt.Origin.Y);
                var target = offset + new Vector3(evt.Position.X, .1f, evt.Position.Y);
                var direction = target - origin;
                var slash = UnityEngine.Object.Instantiate(slashPrefab, origin,
                    direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction) : Quaternion.identity, parent);
                slash.transform.localScale *= evt.Radius;
                slash.pause = true;
                slash.Reinit();
                slash.Play();
                slashes.Add((slash, .6f));
                if (showHitRanges)
                {
                    var ring = UnityEngine.Object.Instantiate(hitRangePrefab, parent);
                    ring.ShowSemicircle(origin + Vector3.up * .005f, direction, evt.Radius, Color.cyan);
                    hitRanges.Add((ring, .6f));
                }
                return;
            }
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
            for (int i = slashes.Count - 1; i >= 0; i--)
            {
                var (effect, remaining) = slashes[i];
                effect.Simulate(elapsed * 3, 1);
                remaining -= elapsed;
                if (remaining > 0) { slashes[i] = (effect, remaining); continue; }
                UnityEngine.Object.Destroy(effect.gameObject);
                slashes.RemoveAt(i);
            }
            for (int i = hitRanges.Count - 1; i >= 0; i--)
            {
                var (ring, remaining) = hitRanges[i];
                remaining -= elapsed;
                if (remaining > 0) { hitRanges[i] = (ring, remaining); continue; }
                UnityEngine.Object.Destroy(ring.gameObject); hitRanges.RemoveAt(i);
            }
        }

        public void Clear()
        {
            foreach (var item in active)
                if (item.effect) { item.effect.gameObject.SetActive(false); UnityEngine.Object.Destroy(item.effect.gameObject); }
            active.Clear();
            foreach (var item in slashes)
                if (item.effect) { item.effect.gameObject.SetActive(false); UnityEngine.Object.Destroy(item.effect.gameObject); }
            slashes.Clear();
            foreach (var item in hitRanges)
                if (item.ring) { item.ring.Hide(); UnityEngine.Object.Destroy(item.ring.gameObject); }
            hitRanges.Clear();
        }
    }
}
