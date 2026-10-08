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
        public DefenseTestOptions Options = new DefenseTestOptions();
        bool showHitRanges;
        DefenseRangeRing areaRing;
        float areaSeconds;
        public bool IsConfigured => prefab && slashPrefab && slashPrefab.visualEffectAsset && hitRangePrefab && lifetime > 0 && !float.IsInfinity(lifetime);
        public void SetHitRangesVisible(bool visible)
        {
            showHitRanges = visible;
            if (visible) return;
            foreach (var item in hitRanges) { item.ring.Hide(); UnityEngine.Object.Destroy(item.ring.gameObject); }
            hitRanges.Clear();
        }

        public void Show(CombatEvent evt, Transform parent, Vector3 offset, Quaternion rotation,
            DefenseActorView source = null, DefenseRangeRing area = null)
        {
            var position = offset + new Vector3(evt.Position.X, 0, evt.Position.Y);
            if (source && Options.launchEffects)
            {
                if (evt.Kind == "launch" || evt.Kind == "slash") source.AimAt(position);
                else if (!evt.Melee && (evt.Kind == "hit" || evt.Kind == "buff" || evt.Kind == "stun")) source.FireAt(position);
            }
            if (Options.ranges && area && evt.Radius > 0 && evt.Kind != "slash")
            {
                areaRing = area;
                areaRing.Show(position, evt.Radius, evt.Kind == "buff" ? new Color(.4f, 1, .5f) : new Color(1, .65f, .25f));
                areaSeconds = .25f;
            }
            if (evt.Kind == "slash")
            {
                var origin = offset + new Vector3(evt.Origin.X, .1f, evt.Origin.Y);
                var target = offset + new Vector3(evt.Position.X, .1f, evt.Position.Y);
                var direction = target - origin;
                // The slash graph sweeps 180° starting at its local forward; turning it +90° makes the sweep run
                // from 90° right through the facing to 90° left, matching the half-plane the hit rule uses.
                var facing = direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
                var slash = Options.slashEffects ? UnityEngine.Object.Instantiate(slashPrefab, origin, facing * Quaternion.Euler(0, 90, 0), parent) : null;
                if (slash) { slash.transform.localScale *= evt.Radius;
                slash.pause = true;
                slash.Reinit();
                slash.Play();
                slashes.Add((slash, .6f)); }
                if (showHitRanges)
                {
                    var ring = UnityEngine.Object.Instantiate(hitRangePrefab, parent);
                    ring.ShowSemicircle(origin + Vector3.up * .005f, direction, evt.Radius, Color.cyan);
                    hitRanges.Add((ring, .6f));
                }
                return;
            }
            if (!Options.hitEffects) return;
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
            areaSeconds -= elapsed;
            if (areaRing && areaSeconds <= 0) areaRing.Hide();
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
            areaSeconds = 0;
            if (areaRing) areaRing.Hide();
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
