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
        [SerializeField] VisualEffect slashPrefab; // Stylized Slash VFX (HungNguyen), white-red bolder (planner)
        [SerializeField] DefenseRangeRing hitRangePrefab;
        [SerializeField, Min(.01f)] float lifetime = 1.5f;
        // Ranged attack (Stylized Projectile Pack, Laser_Blue_Small): flare at the muzzle, a shot flying to the target, impact there.
        [SerializeField] ParticleSystem shotPrefab, shotFlarePrefab, shotImpactPrefab;
        // The slash is one flat crescent lying round its origin (top-down render): at scale 1 its outer edge is 6.8 from the
        // centre and its thick part on its -X side; it shows for about half a second of its 1 s life and is fullest about .2 s in.
        // One per slash() (planner: not two joined into a circle), played whole, scaled so the outer edge is the attack range,
        // its thick part to the bottom of the screen, and started so it is fullest as the strike lands.
        const float SlashOuterRadius = 6.8f;
        const float SlashLead = .2f, SlashLife = 1;
        readonly List<(float wait, Action show)> delayed = new List<(float, Action)>();
        readonly List<(ParticleSystem shot, Vector3 target, float speed, Quaternion rotation)> shots = new List<(ParticleSystem, Vector3, float, Quaternion)>();
        readonly List<(ParticleSystem effect, float remaining)> active = new List<(ParticleSystem, float)>();
        readonly List<(VisualEffect effect, float remaining)> slashes = new List<(VisualEffect, float)>();
        readonly Stack<VisualEffect> slashPool = new Stack<VisualEffect>();
        readonly List<(DefenseRangeRing ring, float remaining)> hitRanges = new List<(DefenseRangeRing, float)>();
        public DefenseTestOptions Options { get; set; } = new DefenseTestOptions();
        // Effects are made per hit, slash and shot; a finished one waits switched off for the next of its kind (13차 133).
        readonly Dictionary<ParticleSystem, Stack<ParticleSystem>> pools = new Dictionary<ParticleSystem, Stack<ParticleSystem>>();
        readonly Dictionary<ParticleSystem, ParticleSystem> made = new Dictionary<ParticleSystem, ParticleSystem>();
        ParticleSystem Take(ParticleSystem effectPrefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            ParticleSystem effect = null;
            if (pools.TryGetValue(effectPrefab, out var pool))
                while (pool.Count > 0 && !effect) effect = pool.Pop();   // one destroyed with its old parent is skipped
            if (effect)
            {
                effect.transform.SetParent(parent, false);
                effect.transform.SetPositionAndRotation(position, rotation);
                effect.transform.localScale = effectPrefab.transform.localScale;
                effect.gameObject.SetActive(true);
            }
            else { effect = UnityEngine.Object.Instantiate(effectPrefab, position, rotation, parent); made[effect] = effectPrefab; }
            effect.Simulate(0, true, true, false);
            return effect;
        }
        void Return(ParticleSystem effect)
        {
            if (!effect) return;
            if (!made.TryGetValue(effect, out var origin)) { UnityEngine.Object.Destroy(effect.gameObject); return; }
            effect.Clear(true); effect.gameObject.SetActive(false);
            if (!pools.TryGetValue(origin, out var pool)) pools[origin] = pool = new Stack<ParticleSystem>();
            pool.Push(effect);
        }
        void ReturnSlash(VisualEffect slash)
        {
            if (!slash) return;
            slash.gameObject.SetActive(false);
            slashPool.Push(slash);
        }
        bool showHitRanges;
        DefenseRangeRing areaRing;
        float areaSeconds;
        public bool IsConfigured => prefab && slashPrefab && hitRangePrefab && shotPrefab && shotFlarePrefab && shotImpactPrefab && lifetime > 0 && !float.IsInfinity(lifetime);
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
            // The shooter's laser impact (on arrival, with the damage) replaces the generic hit spark.
            bool laser = source && source.HasMuzzle;
            if (source && Options.launchEffects)
            {
                if (evt.Kind == "launch") source.AimAt(position);
                else if (evt.Kind == "slash") source.Spin(position, evt.Seconds);
                else if (evt.Kind == "shot") Fire(source, position + Vector3.up * .45f, evt.Seconds, rotation, parent);
                else if (!evt.Melee && evt.Kind == "hit") source.FireAt(position); // buff/stun carry the robot's own position: no beam to itself
            }
            if (Options.ranges && area && evt.Radius > 0 && evt.Kind != "slash")
            {
                areaRing = area;
                areaRing.Show(position, evt.Radius, evt.Kind == "buff" ? new Color(.4f, 1, .5f) : new Color(1, .65f, .25f));
                areaSeconds = .25f;
            }
            // Started so it is fullest as the strike lands.
            if (evt.Kind == "slash") { Later(evt.Seconds - SlashLead, () => Slash(evt, parent, offset)); return; }
            if (!Options.hitEffects) return;
            if ((evt.Kind != "hit" && evt.Kind != "projectile-hit") || evt.Amount <= 0) return;
            // The laser's impact is drawn where the enemy is when the hit lands, not where it was at launch.
            if (laser) { Play(active, shotImpactPrefab, offset + DefenseViews.World(evt.Position, .45f), rotation, parent); return; }
            // Manual simulation keeps every child particle in sync with battle pause and speed.
            Play(active, prefab, offset + DefenseViews.World(evt.Position, .45f), rotation, parent);
        }
        void Later(float wait, Action show)
        {
            if (wait > 0) delayed.Add((wait, show)); else show();
        }
        void Slash(CombatEvent evt, Transform parent, Vector3 offset)
        {
            var origin = offset + DefenseViews.World(evt.Origin, .1f);
            // The whole effect as made, only turned: its -X side (the thick part) always to the bottom of the screen (world -Z
            // under the fixed camera), whichever side the target is on (planner). Round the robot's lower body, .45 up (planner).
            var facing = Quaternion.Euler(0, -90, 0);
            if (Options.slashEffects)
            {
                VisualEffect slash = null;
                while (slashPool.Count > 0 && !slash) slash = slashPool.Pop();   // one destroyed with its old parent is skipped
                if (!slash) slash = UnityEngine.Object.Instantiate(slashPrefab, parent);
                slash.transform.SetParent(parent, false);
                slash.transform.SetPositionAndRotation(origin + Vector3.up * .35f, facing);
                slash.transform.localScale = slashPrefab.transform.localScale * (evt.Radius / SlashOuterRadius);
                slash.gameObject.SetActive(true);
                // Stepped by Tick like the particle effects, so it keeps to battle pause and speed.
                slash.pause = true; slash.Reinit();
                slashes.Add((slash, SlashLife));
            }
            if (showHitRanges)
            {
                var ring = UnityEngine.Object.Instantiate(hitRangePrefab, parent);
                ring.Show(origin + Vector3.up * .005f, evt.Radius, Color.cyan);
                hitRanges.Add((ring, .6f));
            }
        }

        // Flies for the simulation's flight time, so it lands with the damage.
        void Fire(DefenseActorView source, Vector3 target, float seconds, Quaternion rotation, Transform parent)
        {
            source.AimAt(target);
            var from = source.Muzzle;
            var facing = Quaternion.LookRotation(target - from);
            Play(active, shotFlarePrefab, from, facing, parent);
            var flying = Take(shotPrefab, from, facing, parent);
            shots.Add((flying, target, Vector3.Distance(from, target) / Mathf.Max(seconds, .01f), rotation));
        }
        void Play(List<(ParticleSystem, float)> list, ParticleSystem effectPrefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            list.Add((Take(effectPrefab, position, rotation, parent), lifetime));
        }

        public void Tick(float elapsed)
        {
            if (elapsed <= 0) return;
            for (int i = delayed.Count - 1; i >= 0; i--)
            {
                var (wait, show) = delayed[i];
                if ((wait -= elapsed) > 0) { delayed[i] = (wait, show); continue; }
                delayed.RemoveAt(i);
                show();
            }
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var (flying, target, speed, rotation) = shots[i];
                if (!flying) { shots.RemoveAt(i); continue; }
                flying.transform.position = Vector3.MoveTowards(flying.transform.position, target, speed * elapsed);
                flying.Simulate(elapsed, true, false, false);
                if (flying.transform.position != target) continue;
                Return(flying);
                shots.RemoveAt(i);
            }
            areaSeconds -= elapsed;
            if (areaRing && areaSeconds <= 0) areaRing.Hide();
            // Simulate leaves systems paused, so IsAlive remains true after their last particle; expire by time instead.
            foreach (var (effect, _) in active) effect.Simulate(elapsed, true, false, false);
            foreach (var (effect, _) in slashes) effect.Simulate(elapsed);
            Expire(active, elapsed, Return); Expire(slashes, elapsed, ReturnSlash);
            Expire(hitRanges, elapsed, ring => UnityEngine.Object.Destroy(ring.gameObject)); // debug rings only
        }
        static void Expire<T>(List<(T, float)> items, float elapsed, Action<T> done) where T : Component
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var (item, remaining) = items[i];
                remaining -= elapsed;
                if (remaining > 0) { items[i] = (item, remaining); continue; }
                done(item);
                items.RemoveAt(i);
            }
        }

        public void Clear()
        {
            areaSeconds = 0;
            delayed.Clear();
            if (areaRing) areaRing.Hide();
            foreach (var (effect, _) in active) Return(effect);
            active.Clear();
            foreach (var (effect, _) in slashes) ReturnSlash(effect);
            slashes.Clear();
            foreach (var (flying, _, _, _) in shots) Return(flying);
            shots.Clear();
            ClearRanges();
        }
    }
}
