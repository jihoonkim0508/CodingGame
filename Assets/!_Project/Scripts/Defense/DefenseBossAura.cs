using UnityEngine;

namespace CodingGame.Defense
{
    // Uses the ordinary enemy model; only the marked boss receives this visual treatment.
    public sealed class DefenseBossAura : MonoBehaviour
    {
        Light glow;
        LineRenderer halo;
        Material haloMaterial;
        Vector3 fullScale;
        float appearedAt;
        void Awake()
        {
            fullScale = transform.localScale * 1.65f;
            transform.localScale = fullScale * .08f;
            appearedAt = Time.unscaledTime;

            var aura = new GameObject("Boss violet aura");
            aura.transform.SetParent(transform, false);
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
            ring.transform.SetParent(transform, false);
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
            float reveal = Mathf.Clamp01((Time.unscaledTime - appearedAt) / 1.15f);
            transform.localScale = fullScale * Mathf.SmoothStep(.08f, 1f, reveal);
            if (glow) glow.intensity = 1.6f + .45f * Mathf.Sin(Time.unscaledTime * 3.2f);
            if (halo) halo.widthMultiplier = .05f + .015f * Mathf.Sin(Time.unscaledTime * 3.2f);
        }

        void OnDestroy()
        {
            if (haloMaterial) Destroy(haloMaterial);
        }
    }
}
