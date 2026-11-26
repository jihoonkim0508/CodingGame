using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CodingGame.Defense
{
    // The battle's show (2026-11-25, the user: "화려하고 재밌게, 하지만 읽히게"), all built at runtime so the scene stays as it is:
    // - the look: a post-processing volume on the battle camera (bloom on HDR glows only, a soft vignette, a little contrast),
    //   red at the edges while the core is low, colour drained with a chromatic split on defeat; the overlay HUD is untouched;
    // - the desk alive: data packets flowing along the road to the core, a rift where enemies come in, glowing monitor code
    //   and lamp, coffee steam, dust motes (the road's chevrons and the core's dome went on 2026-11-26: not asked for);
    // - combat juice: a burst on every kill (big for a boss, with hit-stop), bomb blasts, leaks, screen shake on big moments
    //   (boss footsteps, boss fall, blasts, leaks, the wave's last kill), slow motion on the last kill, confetti on victory.
    // Everything moves on the battle's pace (still while paused, faster at 2x/3x); shakes go through the lens
    // (DefenseCameraLensOffset.Shake), so they sit on top of the player's pan and zoom and never move the camera.
    [DefaultExecutionOrder(900)]
    public sealed class DefenseBattleFx : MonoBehaviour
    {
        DefenseBattle battle;
        Camera view;
        // Everything this makes, under one root at the scene's top (gone with the scene).
        Transform root;
        public void Setup(DefenseBattle owner, Camera camera)
        {
            battle = owner; view = camera;
            root = new GameObject("Battle FX").transform;
            BuildLook(); BuildBursts();
        }
        // Off: the battle as it looked before (the frame-time comparison in ScreenTour.RunBattleFx).
        public bool Shown
        {
            get => root && root.gameObject.activeSelf;
            set
            {
                if (root) root.gameObject.SetActive(value);
                enabled = value; TimeScale = EffectScale = 1; DefenseCameraLensOffset.Shake = Vector2.zero;
                var data = view.GetUniversalAdditionalCameraData(); if (data) data.renderPostProcessing = value;
            }
        }

        // ---- Time: hit-stop dips the battle's pace (DefenseBattle.Pace) for a moment; slow motion slows the effects that
        // linger after the wave's last kill. Both run on GameClock (still behind the Esc menu).
        public float TimeScale { get; private set; } = 1;
        public float EffectScale { get; private set; } = 1;
        float hitStop, slowMo;
        void HitStop(float seconds) { hitStop = Mathf.Max(hitStop, seconds); TimeScale = .05f; }

        // ---- Shake: trauma (0-1) decays; the offset is trauma² of MaxShake (a share of the screen), plus the boss's footsteps.
        const float MaxShake = .011f;
        float trauma, shakeClock, steps, walking;
        public void Kick(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        // ---- Materials: the monster effects' (Resources/Effects/Monsters), found by name, so nothing new is loaded.
        static readonly Dictionary<string, Material> found = new Dictionary<string, Material>();
        internal static Material Find(string name)
        {
            if (found.Count == 0 || found.ContainsValue(null))
            {
                found.Clear();
                foreach (var effect in new[] { "HealPulse", "SegmentDeath", "ShieldShatter", "StackPush", "BlockedSpark", "ShieldBubble", "OpenEndMarker", "ShieldCast" })
                {
                    var prefab = DefenseActorView.Effect(effect); if (!prefab) continue;
                    foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                        foreach (var m in r.sharedMaterials) if (m && !found.ContainsKey(m.name)) found[m.name] = m;
                }
            }
            found.TryGetValue(name, out var material);
            return material;
        }
        // An instance of a found material in a colour; above 1 it blooms. alpha: the alpha-blended particle look (the
        // marker ring's) with the found material's texture, which reads on the cream road where added light washes out.
        readonly List<Object> made = new List<Object>();
        Material Tinted(string name, Color color, bool alpha = false, Texture texture = null)
        {
            var source = Find(name); var shell = alpha ? Find("FxMarkerRing") : source;
            if (!shell) return null;
            var m = new Material(shell); made.Add(m);
            var tex = texture ? texture : source ? source.mainTexture : null;
            if (tex) m.mainTexture = tex;
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0); // ground strips and quads show from either side
            return m;
        }

        // ---- The look.
        Volume volume; Bloom bloom; Vignette vignette; ColorAdjustments grade; ChromaticAberration aberration;
        float bloomKick;
        void BuildLook()
        {
            volume = new GameObject("Battle look", typeof(Volume)).GetComponent<Volume>();
            volume.transform.SetParent(root, false);
            volume.isGlobal = true; volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); made.Add(profile);
            volume.sharedProfile = profile;
            // Only HDR glows bloom (effects, the props' glow parts); white UI-like text on the map stays crisp.
            bloom = profile.Add<Bloom>(true); bloom.threshold.Override(1.2f); bloom.intensity.Override(.45f); bloom.scatter.Override(.55f);
            bloom.highQualityFiltering.Override(false);
            vignette = profile.Add<Vignette>(true); vignette.intensity.Override(.2f); vignette.smoothness.Override(.45f); vignette.color.Override(Color.black);
            grade = profile.Add<ColorAdjustments>(true); grade.contrast.Override(6); grade.saturation.Override(8);
            aberration = profile.Add<ChromaticAberration>(true); aberration.intensity.Override(0);
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.None); // the URP asset's default profile would tone-map the art
            var data = view.GetUniversalAdditionalCameraData();
            if (data) data.renderPostProcessing = true;
        }
        void Look(DefenseSimulation sim, float dt)
        {
            int full = Mathf.Max(1, sim.Setup.BaseHealth);
            float health = Mathf.Clamp01(sim.BaseHealth / (float)full);
            bool defeat = sim.Phase == BattlePhase.Defeat, boss = BossOnRoad(sim) != null;
            float danger = defeat ? 0 : Mathf.InverseLerp(.4f, .1f, health);
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 4.2f);
            bloomKick = Mathf.MoveTowards(bloomKick, 0, dt * 1.2f);
            float k = 1 - Mathf.Exp(-dt * 6);
            bloom.intensity.value = Mathf.Lerp(bloom.intensity.value, .45f + bloomKick * .7f + (boss ? .1f : 0), k);
            vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, defeat ? .42f : .2f + danger * (.12f + .08f * pulse) + (boss ? .05f : 0), k);
            vignette.color.value = Color.Lerp(vignette.color.value, defeat ? new Color(.25f, 0, .05f) : Color.Lerp(Color.black, new Color(.55f, 0, .05f), danger), k);
            grade.saturation.value = Mathf.Lerp(grade.saturation.value, defeat ? -55 : 8, k * .6f);
            aberration.intensity.value = Mathf.Lerp(aberration.intensity.value, defeat ? .55f + .3f * pulse : boss ? .12f : 0, k);
        }

        // ---- Bursts: a few long-lived world-space particle systems that emit where something happens (no object per hit).
        ParticleSystem flash, sparks, ring, smoke, shards, stars, confetti;
        readonly List<ParticleSystem> paced = new List<ParticleSystem>();
        ParticleSystem Burster(string name, Material material, ParticleSystemRenderMode mode, int max, float life, float speedMin, float speedMax,
            float gravity = 0, float radius = 0, float grow = 1, bool keep = false)
        {
            var go = new GameObject(name); go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = true; main.duration = 1; main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * .75f, life); main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.gravityModifier = gravity; main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = radius > 0; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = Mathf.Max(.01f, radius);
            var fade = ps.colorOverLifetime; fade.enabled = !keep;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.8f, .5f), new GradientAlphaKey(0, 1) });
            fade.color = g;
            if (grow != 1)
            {
                var size = ps.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1, grow < 1 ? AnimationCurve.EaseInOut(0, 1, 1, grow) : new AnimationCurve(new Keyframe(0, 1 / grow, 0, 4 / grow), new Keyframe(1, 1)));
            }
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode; renderer.sharedMaterial = material; renderer.sortingFudge = -10;
            if (mode == ParticleSystemRenderMode.Stretch) { renderer.lengthScale = 1.5f; renderer.velocityScale = .06f; }
            ps.Play(); paced.Add(ps);
            return ps;
        }
        void BuildBursts()
        {
            flash = Burster("Flash", Tinted("FxGlow", new Color(1.8f, 1.8f, 1.8f)), ParticleSystemRenderMode.Billboard, 64, .22f, 0, 0, grow: 1.6f);
            sparks = Burster("Sparks", Tinted("FxSpark", new Color(2f, 2f, 2f)), ParticleSystemRenderMode.Stretch, 600, .45f, 4, 9, gravity: 1.4f, radius: .2f);
            ring = Burster("Ring", Tinted("FxRing", new Color(2.2f, 2.2f, 2.2f)), ParticleSystemRenderMode.HorizontalBillboard, 64, .42f, 0, 0, grow: 6f);
            smoke = Burster("Smoke", Tinted("FxSmoke", new Color(1, 1, 1, .8f)), ParticleSystemRenderMode.Billboard, 200, 1.1f, .4f, 1.4f, gravity: -.08f, radius: .35f, grow: 1.8f);
            shards = Burster("Shards", Tinted("FxShard", Color.white), ParticleSystemRenderMode.Billboard, 300, .7f, 3, 6.5f, gravity: 2.6f, radius: .15f, grow: .3f);
            stars = Burster("Stars", Tinted("FxStar", new Color(2, 2, 2)), ParticleSystemRenderMode.Billboard, 300, .8f, 1.5f, 4.5f, gravity: .4f, radius: .25f, grow: .2f);
            confetti = BuildConfetti();
        }
        static void Emit(ParticleSystem ps, Vector3 at, Color color, float size, int count = 1)
        {
            if (!ps) return;
            var p = new ParticleSystem.EmitParams { position = at, startColor = color, startSize = size, applyShapeToPosition = true };
            ps.Emit(p, count);
        }
        static readonly Color Error = new Color(1, .3f, .45f), Cyan = new Color(.35f, .9f, 1), Gold = new Color(1, .82f, .3f), Mint = new Color(.3f, 1, .7f), Ember = new Color(1, .55f, .2f);
        static Color Accent(EnemyType type) => type == EnemyType.Healer ? Mint : type == EnemyType.Shielder ? Cyan : type == EnemyType.Tank ? Ember :
            type == EnemyType.Fast ? new Color(.6f, .95f, 1) : type == EnemyType.Stack ? new Color(.8f, .6f, 1) : type == EnemyType.Queue ? Ember : Error;

        // ---- Enemies as last seen (the battle drops a killed one before its events are shown).
        struct Seen { public EnemyType type; public bool boss, chain; public Vector3 at; }
        readonly Dictionary<int, Seen> seen = new Dictionary<int, Seen>();
        Vector3 lastKill; bool lastKillSet;

        // Called by DefenseBattle for every combat event, after the views were synced.
        public void OnEvent(CombatEvent evt)
        {
            var sim = battle.Simulation; if (sim == null || !sim.Options.hitEffects) return;
            var at = DefenseViews.World(evt.Position, .6f);
            switch (evt.Kind)
            {
                case "killed":
                {
                    bool has = seen.TryGetValue(evt.Target, out var e);
                    if (has) at = e.at + Vector3.up * .6f;
                    lastKill = at; lastKillSet = true;
                    var color = has ? Accent(e.type) : Error;
                    if (has && e.boss)
                    {
                        Emit(flash, at + Vector3.up * .4f, Color.white, 7); Emit(flash, at, color, 10);
                        Emit(ring, DefenseViews.World(evt.Position, .08f), color, 12); Emit(ring, DefenseViews.World(evt.Position, .1f), Color.white, 7);
                        Emit(sparks, at, color, .35f, 60); Emit(shards, at, color, .35f, 40); Emit(smoke, at, new Color(.35f, .2f, .4f, .7f), 1.6f, 14);
                        Emit(stars, at, Gold, .5f, 30);
                        Kick(1); HitStop(.18f); bloomKick = 1.2f;
                        break;
                    }
                    if (has && e.chain) { Emit(sparks, at, color, .2f, 8); Emit(ring, DefenseViews.World(evt.Position, .08f), color, 2.2f); break; }
                    Emit(flash, at, color, 2.2f); Emit(flash, at, Color.white, .9f);
                    Emit(ring, DefenseViews.World(evt.Position, .08f), color, 3.4f);
                    Emit(sparks, at, color, .24f, 14); Emit(shards, at, color, .22f, 8);
                    if (has && e.type == EnemyType.Tank) { Emit(smoke, at, new Color(.5f, .4f, .35f, .6f), 1.1f, 5); Kick(.12f); }
                    break;
                }
                case "blast":
                    Emit(flash, at, Ember, 2.5f + evt.Radius * 1.4f); Emit(flash, at, Color.white, 1.6f);
                    Emit(ring, DefenseViews.World(evt.Position, .08f), Ember, evt.Radius * 2.3f);
                    Emit(sparks, at, Ember, .26f, 24); Emit(smoke, at, new Color(.45f, .4f, .38f, .7f), 1.2f, 7);
                    Kick(.22f);
                    break;
                case "leaked":
                    Emit(flash, at, Error, 3.2f); Emit(ring, DefenseViews.World(evt.Position, .08f), Error, 5);
                    Emit(sparks, at, Error, .25f, 18);
                    Kick(.45f);
                    break;
                case "shield-break":
                    Emit(sparks, at + Vector3.up * .3f, Cyan, .22f, 16); Emit(ring, DefenseViews.World(evt.Position, .1f), Cyan, 2.6f); Kick(.1f);
                    break;
                case "tank-destroyed":
                    Emit(flash, at, Ember, 3); Emit(sparks, at, Ember, .26f, 22); Emit(smoke, at, new Color(.4f, .4f, .4f, .7f), 1.2f, 8); Kick(.35f);
                    break;
                case "wave-completed":
                    // The wave's last kill: a moment of slow motion, a white flash, a ring and stars where it fell.
                    var where = lastKillSet ? lastKill : at;
                    Emit(flash, where, Color.white, 5); Emit(ring, new Vector3(where.x, .1f, where.z), Color.white, 8);
                    Emit(stars, where, Gold, .45f, 30);
                    slowMo = 1.1f; Kick(.45f); bloomKick = Mathf.Max(bloomKick, .8f);
                    break;
            }
        }

        // ---- The map: road packets, the rift, props, dust.
        Transform mapRoot; DefenseSimulation mapFor; int routesShown = -1; Bounds? gate;
        readonly List<(Transform glow, Transform dot, Vector3[] path, float[] along, float total, float offset)> packets = new List<(Transform, Transform, Vector3[], float[], float, float)>();
        readonly List<(Transform root, Transform ring, Transform swirl, ParticleSystem motes)> rifts = new List<(Transform, Transform, Transform, ParticleSystem)>();
        readonly List<(Renderer renderer, Color color, int role, float phase)> props = new List<(Renderer, Color, int, float)>();
        MaterialPropertyBlock block;
        ParticleSystem motes;
        readonly List<Object> mapMade = new List<Object>();
        float clock;
        static Texture2D square;

        void BuildMap(DefenseSimulation sim)
        {
            foreach (var o in mapMade) if (o) Destroy(o is Component c ? c.gameObject : o);
            mapMade.Clear(); packets.Clear(); rifts.Clear(); props.Clear();
            if (motes) paced.Remove(motes);
            mapFor = sim; routesShown = -1;
            var core = battle.CoreModel;
            mapRoot = core ? core.root : null;
            gate = null;
            if (mapRoot)
                foreach (var t in mapRoot.GetComponentsInChildren<Transform>())
                    if (t.name == "ErrorGate")
                        foreach (var r in t.GetComponentsInChildren<Renderer>()) { if (gate is Bounds b) { b.Encapsulate(r.bounds); gate = b; } else gate = r.bounds; }
            for (int i = 0; i < sim.Setup.Routes.Length; i++)
            {
                if (!sim.RouteInUse(i)) continue;
                var points = DefenseBattle.RoadPoints(sim.Setup.Routes[i]);
                if (points.Length < 2) continue;
                var path = new Vector3[points.Length];
                for (int p = 0; p < points.Length; p++) path[p] = DefenseViews.World(points[p], .09f);
                Packets(path, sim.Setup.Routes[i].HalfWidth);
                Rift(path);
            }
            routesShown = RoutesKey(sim);
            if (mapRoot) Props(mapRoot);
            Motes(sim);
        }
        static int RoutesKey(DefenseSimulation sim) { int key = 0; for (int i = 0; i < sim.Setup.Routes.Length && i < 30; i++) if (sim.RouteInUse(i)) key |= 1 << i; return key; }
        GameObject Made(string name) { var go = new GameObject(name); go.transform.SetParent(root, false); mapMade.Add(go); return go; }
        Transform Quad(string name, Transform parent, Material material, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localScale = Vector3.one * size;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go.transform;
        }
        // The packets: a blue dot that reads on the cream road under an added glow that blooms.
        void Packets(Vector3[] path, float halfWidth)
        {
            var along = new float[path.Length]; for (int i = 1; i < path.Length; i++) along[i] = along[i - 1] + Vector3.Distance(path[i - 1], path[i]);
            float total = along[along.Length - 1];
            var dotMaterial = Tinted("FxGlow", new Color(.08f, .42f, 1f, .95f), true);
            var glowMaterial = Tinted("FxGlow", new Color(.12f, .5f, 1.4f, 1));
            for (int i = 0; i < 5; i++)
            {
                var packet = Made("Packet").transform;
                var dot = Quad("Dot", packet, dotMaterial, halfWidth * .42f); dot.localRotation = Quaternion.Euler(90, 0, 0);
                var glow = Quad("Glow", packet, glowMaterial, halfWidth * .75f); glow.localRotation = Quaternion.Euler(90, 0, 0); glow.localPosition = Vector3.up * .01f;
                packets.Add((packet, dot, path, along, total, total * i / 5f));
            }
        }
        void Rift(Vector3[] path)
        {
            var root = Made("Rift").transform;
            // At the gate's mouth: the first point along the road clear of the ErrorGate model (it stands over the road's start).
            var at = path[0];
            if (gate is Bounds g0)
                for (int i = 1; i < path.Length; i++)
                {
                    var step = path[i] - path[i - 1]; float length = step.magnitude; bool clear = false;
                    for (float d = 0; d < length && !clear; d += .2f)
                    {
                        at = path[i - 1] + step * (d / length);
                        clear = !(g0.min.x - .3f < at.x && at.x < g0.max.x + .3f && g0.min.z - .3f < at.z && at.z < g0.max.z + .3f);
                    }
                    if (clear) break;
                }
            var dir = (path[1] - path[0]); dir.y = 0; dir.Normalize();
            root.position = new Vector3(at.x, .05f, at.z) + dir * (gate.HasValue ? .9f : 1.2f);
            var hole = Quad("Hole", root, Tinted("FxGlow", new Color(.22f, .02f, .3f, .9f), true), 2.6f); hole.localRotation = Quaternion.Euler(90, 0, 0);
            var ringT = Quad("Ring", root, Tinted("FxRing", new Color(2.6f, .5f, 2.2f, 1)), 2.6f); ringT.localRotation = Quaternion.Euler(90, 0, 0); ringT.localPosition = Vector3.up * .02f;
            var swirl = Quad("Swirl", root, Tinted("FxStar", new Color(1.6f, .3f, 1.4f, 1)), 2f); swirl.localRotation = Quaternion.Euler(90, 0, 0); swirl.localPosition = Vector3.up * .03f;
            // Sparks rising out of it.
            var go = new GameObject("Rift motes"); go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(.8f, 1.4f); main.startSpeed = new ParticleSystem.MinMaxCurve(.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.08f, .2f); main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 80;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, .35f, .9f), new Color(1, .25f, .35f));
            var emission = ps.emission; emission.rateOverTime = 16;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .9f; shape.rotation = new Vector3(-90, 0, 0);
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(0, 1) });
            fade.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Tinted("FxGlow", new Color(2.4f, 1.4f, 2.4f, 1));
            ps.Play(); paced.Add(ps);
            rifts.Add((root, ringT, swirl, ps));
        }
        // The desk's glowing parts (Stage01 props: CyanGlow, AmberGlow, WhiteGlow) breathe, the monitor's and the core's
        // code lines (Coral/Gold/Azure/Mint under Monitor or CodeBase) blink like code being run, the coffee steams.
        void Props(Transform root)
        {
            block ??= new MaterialPropertyBlock();
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.sharedMaterial || !r.sharedMaterial.HasProperty("_BaseColor")) continue;
                string n = r.gameObject.name; var parent = r.transform.parent ? r.transform.parent.name : "";
                bool core = parent.StartsWith("CodeBase") || n == "CodeBase";
                int role = n == "CyanGlow" || n == "WhiteGlow" ? (core ? 3 : 1) : n == "AmberGlow" ? 2 :
                    (n == "Coral" || n == "Gold" || n == "Azure" || n == "Mint") && (parent.StartsWith("Monitor") || core) ? 4 : 0;
                if (n == "Coffee") Steam(r.bounds);
                if (role != 0) props.Add((r, r.sharedMaterial.GetColor("_BaseColor"), role, Random.value * 6.28f));
                if (n == "AmberGlow")
                {
                    var halo = Quad("Lamp halo", Made("Lamp glow").transform, Tinted("FxGlow", new Color(1.2f, .7f, .25f, .55f)), 3.2f);
                    halo.parent.position = new Vector3(r.bounds.center.x, .05f, r.bounds.center.z); halo.localRotation = Quaternion.Euler(90, 0, 0);
                }
            }
        }
        void Steam(Bounds cup)
        {
            var go = Made("Coffee steam");
            go.transform.position = new Vector3(cup.center.x, cup.max.y + .05f, cup.center.z);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.6f); main.startSpeed = new ParticleSystem.MinMaxCurve(.25f, .45f);
            main.startSize = new ParticleSystem.MinMaxCurve(.25f, .4f); main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 20;
            main.startColor = new Color(1, 1, 1, .32f); main.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
            var emission = ps.emission; emission.rateOverTime = 3.5f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = Mathf.Max(.05f, cup.extents.x * .4f); shape.rotation = new Vector3(-90, 0, 0);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, 2.2f));
            var noise = ps.noise; noise.enabled = true; noise.strength = .15f; noise.frequency = .6f;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .25f), new GradientAlphaKey(0, 1) });
            fade.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Tinted("FxSmoke", Color.white);
            ps.Play(); paced.Add(ps);
        }
        void Motes(DefenseSimulation sim)
        {
            var go = Made("Dust motes");
            var min = DefenseViews.World(sim.Setup.Min); var max = DefenseViews.World(sim.Setup.Max);
            go.transform.position = (min + max) / 2 + Vector3.up * 1.6f;
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(5, 8); main.startSpeed = new ParticleSystem.MinMaxCurve(.05f, .2f);
            main.startSize = new ParticleSystem.MinMaxCurve(.06f, .14f); main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 120;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, .95f, .8f, .8f), new Color(.6f, .9f, 1, .8f));
            main.prewarm = true;
            var emission = ps.emission; emission.rateOverTime = 14;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(Mathf.Abs(max.x - min.x), 2.6f, Mathf.Abs(max.z - min.z));
            var noise = ps.noise; noise.enabled = true; noise.strength = .25f; noise.frequency = .3f;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) });
            fade.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Tinted("FxGlow", new Color(1.6f, 1.6f, 1.6f, 1));
            ps.Play(); paced.Add(ps); motes = ps;
        }
        ParticleSystem BuildConfetti()
        {
            if (!square) { square = new Texture2D(4, 4, TextureFormat.RGBA32, false); var px = new Color32[16]; for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255); square.SetPixels32(px); square.Apply(); }
            var ps = Burster("Confetti", Tinted("FxGlow", Color.white, true, square), ParticleSystemRenderMode.Billboard, 500, 3.6f, 10, 16, gravity: .35f, keep: true);
            var main = ps.main; main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(.07f, .12f); main.startSizeY = new ParticleSystem.MinMaxCurve(.11f, .19f); main.startSizeZ = 1;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 22; shape.radius = .3f;
            var spin = ps.rotationOverLifetime; spin.enabled = true; spin.z = new ParticleSystem.MinMaxCurve(-6, 6);
            var drag = ps.limitVelocityOverLifetime; drag.enabled = true; drag.drag = .6f; drag.limit = 100;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .8f), new GradientAlphaKey(0, 1) });
            fade.color = g;
            return ps;
        }
        void Celebrate()
        {
            var cam = view.transform;
            var colors = new[] { UITheme.Sunny, UITheme.Coral, UITheme.Mint, UITheme.Sky, UITheme.Grape, UITheme.Orange };
            for (int side = -1; side <= 1; side += 2)
            {
                var from = cam.position + cam.forward * 14 + cam.right * (side * 10) - cam.up * 4.6f;
                confetti.transform.rotation = Quaternion.LookRotation(cam.up * 2.2f - cam.right * (side * .9f) + cam.forward * .3f);
                confetti.transform.position = from;
                var shape = confetti.shape; shape.position = Vector3.zero;
                foreach (var c in colors)
                {
                    var p = new ParticleSystem.EmitParams { startColor = c, applyShapeToPosition = true, position = from };
                    confetti.Emit(p, 22);
                }
            }
            bloomKick = Mathf.Max(bloomKick, .6f);
        }

        // ---- Per frame.
        BattlePhase shownPhase = BattlePhase.Ready;
        void LateUpdate()
        {
            var sim = battle ? battle.Simulation : null;
            if (sim == null) return;
            // Built once the map stands (its build animation raises and scales the tiles and props first).
            var build = battle.CoreModel ? battle.CoreModel.GetComponentInParent<CodingGame.Art.MapPainter.MapBuildSequence>() : null;
            if ((sim != mapFor || RoutesKey(sim) != routesShown || (battle.CoreModel ? battle.CoreModel.root : null) != mapRoot) && !(build && build.IsAnimating)) BuildMap(sim);
            bool menu = GameClock.Paused;
            float real = menu ? 0 : Time.unscaledDeltaTime;
            // Hit-stop and slow motion on the real clock.
            if (hitStop > 0) { hitStop -= real; TimeScale = .05f; }
            else TimeScale = Mathf.MoveTowards(TimeScale, 1, real / .25f);
            if (slowMo > 0) slowMo -= real;
            EffectScale = slowMo > .45f ? .22f : slowMo > 0 ? Mathf.Lerp(1, .22f, slowMo / .45f) : 1;
            float pace = menu ? 0 : battle.ViewPace * EffectScale;
            float dt = real * pace;
            clock += dt;
            foreach (var ps in paced) if (ps) { var main = ps.main; if (main.simulationSpeed != pace) main.simulationSpeed = pace; }
            Phase(sim);
            Shake(sim, real, Time.deltaTime * battle.ViewPace);
            Look(sim, real);
            Map(sim, dt);
            seen.Clear();
            foreach (var e in sim.Enemies)
            {
                if (!e.Active) continue;
                var at = DefenseViews.World(e.Position);
                seen[e.Id] = new Seen { type = e.Spec.type, boss = e.Boss, chain = e.Chain != null, at = at };
                if (spawned.Add(e.Id)) Spawned(e, at);
            }
            if (sim.Phase == BattlePhase.Ready) { spawned.Clear(); lastKillSet = false; }
        }
        readonly HashSet<int> spawned = new HashSet<int>();
        void Spawned(EnemyState e, Vector3 at)
        {
            // Out of the rift it came through.
            foreach (var rift in rifts) if (rift.root && (rift.root.position - at).sqrMagnitude < 25) { at = new Vector3(rift.root.position.x, 0, rift.root.position.z); break; }
            var color = Accent(e.Spec.type);
            Emit(flash, at + Vector3.up * .6f, new Color(1, .4f, .9f), e.Boss ? 6 : 2.2f);
            Emit(ring, at + Vector3.up * .08f, new Color(1, .35f, .85f), e.Boss ? 9 : 2.8f);
            Emit(sparks, at + Vector3.up * .5f, color, .18f, e.Boss ? 30 : 6);
            riftPulse = 1;
            if (e.Boss) Kick(.6f);
        }
        float riftPulse;
        void Phase(DefenseSimulation sim)
        {
            if (sim.Phase == shownPhase) return;
            if (sim.Phase == BattlePhase.Victory) Celebrate();
            if (sim.Phase == BattlePhase.Defeat) Kick(.9f);
            shownPhase = sim.Phase;
        }
        EnemyState BossOnRoad(DefenseSimulation sim)
        {
            foreach (var e in sim.Enemies) if (e.Active && e.Boss) return e;
            return null;
        }
        void Shake(DefenseSimulation sim, float real, float battleDelta)
        {
            trauma = Mathf.Max(0, trauma - real * 1.4f);
            bool still = real <= 0 || sim.Phase == BattlePhase.Paused;
            if (!still) shakeClock += real;
            float amp = still ? 0 : trauma * trauma * MaxShake;
            var s = new Vector2(Mathf.PerlinNoise(shakeClock * 21, 3.1f) - .5f, Mathf.PerlinNoise(7.7f, shakeClock * 21) - .5f) * (2 * amp);
            // The boss's footsteps: a jolt down and back 1.6 times a battle second, left and right foot by foot, while it walks.
            var boss = BossOnRoad(sim);
            bool walks = boss != null && sim.Phase == BattlePhase.Running && boss.BlockedBy == 0 && !boss.Stunned(sim.Time);
            walking = Mathf.MoveTowards(walking, walks ? 1 : 0, real * 4);
            steps += battleDelta * 1.6f;
            float u = steps - Mathf.Floor(steps);
            float jolt = still ? 0 : walking * .0022f * Mathf.Exp(-7 * u) * Mathf.Sin(u * 45);
            s += new Vector2((Mathf.Floor(steps) % 2 == 0 ? .35f : -.35f) * jolt, -jolt);
            DefenseCameraLensOffset.Shake = s;
        }
        void Map(DefenseSimulation sim, float dt)
        {
            bool fighting = sim.Phase == BattlePhase.Running || sim.Phase == BattlePhase.Paused;
            for (int i = 0; i < packets.Count; i++)
            {
                var p = packets[i]; if (!p.glow) continue;
                float s = Mathf.Repeat(p.offset + clock * (fighting ? 4.2f : 2.4f), p.total);
                int k = 1; while (k < p.along.Length - 1 && p.along[k] < s) k++;
                float t = Mathf.InverseLerp(p.along[k - 1], p.along[k], s);
                p.glow.position = Vector3.Lerp(p.path[k - 1], p.path[k], t) + Vector3.up * .01f;
                // Fades in at the rift and out into the core.
                float edge = Mathf.Min(s, p.total - s);
                p.glow.localScale = Vector3.one * Mathf.Clamp01(edge / 1.5f);
            }
            riftPulse = Mathf.MoveTowards(riftPulse, 0, dt * 2.2f);
            foreach (var (root, ringT, swirl, _) in rifts)
            {
                if (!root) continue;
                ringT.localRotation = Quaternion.Euler(90, clock * 50, 0);
                swirl.localRotation = Quaternion.Euler(90, -clock * 120, 0);
                float breathe = 1 + .05f * Mathf.Sin(clock * 3) + .35f * riftPulse;
                root.localScale = Vector3.one * breathe;
            }
            if (props.Count > 0)
            {
                int full = Mathf.Max(1, sim.Setup.BaseHealth);
                float health = Mathf.Clamp01(sim.BaseHealth / (float)full), t = Time.unscaledTime;
                foreach (var (r, color, role, phase) in props)
                {
                    if (!r) continue;
                    Color c = color;
                    switch (role)
                    {
                        case 1: c *= 1.5f + .45f * Mathf.Sin(t * 2.1f + phase); break;
                        case 2: c *= 1.6f + .25f * Mathf.Sin(t * 1.3f + phase); break;
                        case 3: c = Color.Lerp(new Color(1, .25f, .2f), color, health) * (1.7f + (health < .35f ? .9f * Mathf.PerlinNoise(t * 8, phase) : .25f * Mathf.Sin(t * 2 + phase))); break;
                        case 4: c *= Mathf.Sin(t * 2.6f + phase) > -.2f ? 1.35f : .55f; break;
                    }
                    c.a = color.a;
                    block.SetColor("_BaseColor", c); r.SetPropertyBlock(block);
                }
            }
        }

        void OnDisable() => DefenseCameraLensOffset.Shake = Vector2.zero;
        void OnDestroy()
        {
            foreach (var o in made) if (o) Destroy(o);
            DefenseCameraLensOffset.Shake = Vector2.zero;
        }
    }
}
