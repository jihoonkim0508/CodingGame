using UnityEngine;

namespace CodingGame.Defense
{
    // Uses the ordinary enemy model; only the marked boss receives this visual treatment. The violet motes, light and halo
    // are a prefab (Resources/Effects/BossAura, Tools/Optimize/Stage Visuals) copied onto the boss; only without it are
    // they built here.
    public sealed class DefenseBossAura : MonoBehaviour
    {
        Light glow;
        LineRenderer halo;
        Material haloMaterial;
        static GameObject visual;
        static bool looked;
        Vector3 fullScale;
        // The grow-in, motes and flicker run on the battle clock (the enemy view's pace): still while paused, faster at 2x-5x.
        DefenseActorView view;
        ParticleSystem[] motes;
        float clock, shownPace = 1;
        void Awake()
        {
            fullScale = transform.localScale * 1.65f;
            transform.localScale = fullScale * .08f;
            view = GetComponent<DefenseActorView>();
            if (!looked) { visual = Resources.Load<GameObject>("Effects/BossAura"); looked = true; }
            if (visual)
            {
                var made = Instantiate(visual, transform, false); made.name = "Boss aura";
                glow = made.GetComponentInChildren<Light>(); halo = made.GetComponentInChildren<LineRenderer>();
            }
            else Build(transform, out glow, out halo, out haloMaterial);
            motes = GetComponentsInChildren<ParticleSystem>(true);
        }
        // The visuals under parent (the boss, or the prefab's root when the editor saves them).
        public static void Build(Transform parent, out Light glow, out LineRenderer halo, out Material haloMaterial)
        {
            var aura = new GameObject("Boss violet aura");
            aura.transform.SetParent(parent, false);
            aura.transform.localPosition = Vector3.up * .08f;
            var particles = aura.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.duration = 2;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.65f, 1.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.12f, .48f);
            main.startSize = new ParticleSystem.MinMaxCurve(.08f, .21f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.72f, .26f, 1f, .75f), new Color(.16f, .9f, 1f, .5f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particles.emission;
            emission.rateOverTime = 32;
            emission.SetBursts(new[] { new ParticleSystem.Burst(.12f, 56) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Donut;
            shape.radius = .63f;
            shape.donutRadius = .18f;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.85f, .25f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            particles.Play();

            glow = aura.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(.49f, .16f, 1f);
            glow.range = 2.6f;
            glow.intensity = 1.8f;
            glow.shadows = LightShadows.None;

            haloMaterial = new Material(Shader.Find("Sprites/Default"));
            var ring = new GameObject("Boss violet halo");
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = Vector3.up * .55f;
            halo = ring.AddComponent<LineRenderer>();
            halo.sharedMaterial = haloMaterial;
            halo.useWorldSpace = false;
            halo.loop = true;
            halo.positionCount = 64;
            halo.widthMultiplier = .055f;
            halo.numCornerVertices = 2;
            halo.startColor = halo.endColor = new Color(.85f, .35f, 1f, .85f);
            for (int i = 0; i < halo.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / halo.positionCount;
                halo.SetPosition(i, new Vector3(Mathf.Cos(angle) * .86f, 0, Mathf.Sin(angle) * .86f));
            }
        }

        void Update()
        {
            float pace = view ? view.Pace : 1;
            if (pace != shownPace) { shownPace = pace; foreach (var p in motes) { var main = p.main; main.simulationSpeed = pace; } }
            clock += Time.deltaTime * pace;
            transform.localScale = fullScale * Mathf.SmoothStep(.08f, 1f, Mathf.Clamp01(clock / 1.15f));
            if (glow) glow.intensity = 1.6f + .45f * Mathf.Sin(clock * 3.2f);
            if (halo) halo.widthMultiplier = .05f + .015f * Mathf.Sin(clock * 3.2f);
        }

        void OnDestroy()
        {
            if (haloMaterial) Destroy(haloMaterial);
        }
    }
}
