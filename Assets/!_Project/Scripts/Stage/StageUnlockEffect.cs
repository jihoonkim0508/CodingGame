using UnityEngine;
using UnityEngine.Rendering;

namespace CodingGame.StageMap
{
    public sealed class StageUnlockEffect : MonoBehaviour
    {
        readonly Vector3[] path = new Vector3[4];
        readonly Vector3[] trailPoints = new Vector3[49];
        readonly Vector3[] ringPoints = new Vector3[65];
        Material material;
        LineRenderer trail, ring;
        Transform pulse;
        ParticleSystem sparks;
        Light glow;
        float length, burstAt = -10;
        Vector3 destination;
        public Vector3 HeadPosition => pulse.position;

        public void Configure(StageNode from, StageNode to)
        {
            material = new Material(Shader.Find("Sprites/Default")) { name = "Unlock circuit light" };
            material.SetColor("_Color",new Color(1.8f,1.5f,.85f,1));
            // The landing road sits .32 above the island anchor and bobs by .035.
            // Float the pulse above that surface so it remains visible across the bridge.
            path[0] = from.transform.position + new Vector3(0,.48f,-1.15f);
            path[1] = from.transform.position + new Vector3(2.55f,.48f,-1.15f);
            path[2] = to.transform.position + new Vector3(-2.55f,.48f,-1.15f);
            path[3] = to.transform.position + new Vector3(0,.48f,-1.15f);
            for (int i=1;i<path.Length;i++) length += Vector3.Distance(path[i-1],path[i]);
            destination = to.transform.position + Vector3.up*.48f;
            trail = Line("Circuit energy trail",.12f,new Color(1,.82f,.42f));
            trail.positionCount = trailPoints.Length;
            ring = Line("Unlock shockwave",.06f,new Color(1,.88f,.55f));
            ring.positionCount = ringPoints.Length; ring.enabled = false;
            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "Travelling unlock pulse"; tip.transform.SetParent(transform,false); Destroy(tip.GetComponent<Collider>());
            pulse = tip.transform; pulse.localScale = Vector3.one*.23f;
            tip.GetComponent<Renderer>().sharedMaterial = material;
            tip.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            glow = tip.AddComponent<Light>(); glow.color = new Color(1,.75f,.3f); glow.range = 3; glow.intensity = 2; glow.shadows = LightShadows.None;
            var particles = new GameObject("Unlock sparks"); particles.transform.SetParent(transform,false);
            sparks = particles.AddComponent<ParticleSystem>(); sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main; main.playOnAwake = false; main.loop = false; main.useUnscaledTime = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.45f,1f); main.startSpeed = new ParticleSystem.MinMaxCurve(1,3);
            main.startSize = new ParticleSystem.MinMaxCurve(.045f,.11f); main.startColor = new ParticleSystem.MinMaxGradient(new Color(1,.66f,.25f),new Color(1,.97f,.75f));
            main.gravityModifier = .12f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = sparks.emission; emission.enabled = false;
            var shape = sparks.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .4f;
            var color = sparks.colorOverLifetime; color.enabled = true;
            var fade = new Gradient(); fade.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)}); color.color = fade;
            sparks.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            Show(0);
        }
        LineRenderer Line(string label,float width,Color color)
        {
            var go = new GameObject(label); go.transform.SetParent(transform,false);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.useWorldSpace = true; line.widthMultiplier = width; line.startColor = line.endColor = color;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            line.numCapVertices = 4; line.numCornerVertices = 3;
            return line;
        }
        Vector3 Point(float t)
        {
            float remaining = Mathf.Clamp01(t)*length;
            for (int i=1;i<path.Length;i++)
            {
                float segment = Vector3.Distance(path[i-1],path[i]);
                if (remaining<=segment) return Vector3.Lerp(path[i-1],path[i],remaining/Mathf.Max(.001f,segment));
                remaining -= segment;
            }
            return path[3];
        }
        public void Show(float progress)
        {
            for (int i=0;i<trailPoints.Length;i++) trailPoints[i] = Point(progress*i/(trailPoints.Length-1));
            trail.SetPositions(trailPoints); pulse.position = Point(progress);
        }
        public void Burst()
        {
            burstAt = Time.unscaledTime; pulse.gameObject.SetActive(false);
            sparks.transform.position = destination+Vector3.up*.9f;
            sparks.Play(); sparks.Emit(70); ring.enabled = true;
        }
        void Update()
        {
            if (burstAt<0) return;
            float t = Mathf.Clamp01((Time.unscaledTime-burstAt)/1.35f);
            float radius = Mathf.Lerp(.5f,4.2f,1-(1-t)*(1-t));
            for (int i=0;i<ringPoints.Length;i++)
            {
                float angle = Mathf.PI*2*i/(ringPoints.Length-1);
                ringPoints[i] = destination+new Vector3(Mathf.Cos(angle)*radius,.1f,Mathf.Sin(angle)*radius);
            }
            ring.SetPositions(ringPoints);
            ring.startColor = ring.endColor = new Color(1,.86f,.5f,1-t);
            trail.startColor = trail.endColor = new Color(1,.8f,.4f,(1-t)*.9f);
        }
        void OnDestroy() { if(material) Destroy(material); }
    }
}
