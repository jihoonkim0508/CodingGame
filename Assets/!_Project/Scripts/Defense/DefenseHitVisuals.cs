using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodingGame.Defense
{
    [Serializable]
    sealed class DefenseHitVisuals
    {
        [SerializeField] ParticleSystem prefab;
        [SerializeField] ParticleSystem slashPrefab; // SwordSlash_3 (planner)
        [SerializeField] DefenseRangeRing hitRangePrefab;
        [SerializeField, Min(.01f)] float lifetime = 1.5f;
        readonly List<(ParticleSystem effect, float remaining)> active = new List<(ParticleSystem, float)>();
        readonly List<(ParticleSystem effect, float remaining)> slashes = new List<(ParticleSystem, float)>();
        readonly List<(DefenseRangeRing ring, float remaining)> hitRanges = new List<(DefenseRangeRing, float)>();
        public DefenseTestOptions Options { get; set; } = new DefenseTestOptions();
        bool showHitRanges;
        DefenseRangeRing areaRing;
        float areaSeconds;
        public bool IsConfigured => prefab && slashPrefab && hitRangePrefab && lifetime > 0 && !float.IsInfinity(lifetime);
        public void SetHitRangesVisible(bool visible)
        {
            showHitRanges = visible;
            if (!visible) ClearRanges();
        }
        void ClearRanges()
        {
            foreach (var (ring, _) in hitRanges) if (ring) { ring.Hide(); UnityEngine.Object.Destroy(ring.gameObject); }
            hitRanges.Clear();
        }

        public void Show(CombatEvent evt, Transform parent, Vector3 offset, Quaternion rotation,
            DefenseActorView source, DefenseRangeRing area)
        {
            var position = offset + DefenseViews.World(evt.Position);
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
                var origin = offset + DefenseViews.World(evt.Origin, .1f);
                var direction = position + Vector3.up * .1f - origin;
                // SwordSlash_3 arcs around its forward and is sized for a human-scale character: at scale 1 the arc
                // reaches about 3.2 units, so radius / 3.2 makes it reach the attack range (planner; checked in play).
                var facing = direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
                var slash = Options.slashEffects ? UnityEngine.Object.Instantiate(slashPrefab, origin, facing, parent) : null;
                if (slash)
                {
                    float scale = evt.Radius / 3.2f;
                    slash.transform.localScale *= scale;
                    // The prefab draws its arc 2 units above its origin (at a character's chest); lowered so the arc sweeps
                    // around the robot's lower body, .45 above the ground point (planner).
                    slash.transform.position += Vector3.up * (.45f - 2f * scale);
                    slash.Simulate(0, true, true, false);
                    slashes.Add((slash, slash.main.duration));
                }
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
                offset + DefenseViews.World(evt.Position, .45f), rotation, parent);
            // Manual simulation keeps every child particle in sync with battle pause and speed.
            effect.Simulate(0, true, true, false);
            active.Add((effect, lifetime));
        }

        public void Tick(float elapsed)
        {
            if (elapsed <= 0) return;
            areaSeconds -= elapsed;
            if (areaRing && areaSeconds <= 0) areaRing.Hide();
            // Simulate leaves systems paused, so IsAlive remains true after their last particle; expire by time instead.
            foreach (var (effect, _) in active) effect.Simulate(elapsed, true, false, false);
            foreach (var (effect, _) in slashes) effect.Simulate(elapsed, true, false, false);
            Expire(active, elapsed); Expire(slashes, elapsed); Expire(hitRanges, elapsed);
        }
        static void Expire<T>(List<(T, float)> items, float elapsed) where T : Component
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var (item, remaining) = items[i];
                remaining -= elapsed;
                if (remaining > 0) { items[i] = (item, remaining); continue; }
                UnityEngine.Object.Destroy(item.gameObject);
                items.RemoveAt(i);
            }
        }

        public void Clear()
        {
            areaSeconds = 0;
            if (areaRing) areaRing.Hide();
            foreach (var (effect, _) in active) DefenseViews.Discard(effect);
            active.Clear();
            foreach (var (effect, _) in slashes) DefenseViews.Discard(effect);
            slashes.Clear();
            ClearRanges();
        }
    }
}
