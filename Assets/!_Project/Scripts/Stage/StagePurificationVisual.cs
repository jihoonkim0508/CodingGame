using UnityEngine;
using UnityEngine.Rendering;

namespace CodingGame.StageMap
{
    // The green field and slow motes remain after clearing; the rising scan only plays once.
    public sealed class StagePurificationVisual : MonoBehaviour
    {
        Material material;
        Mesh field;
        MeshRenderer fill;
        LineRenderer perimeter, scan;
        ParticleSystem motes;
        readonly Vector3[] points=new Vector3[65];
        Vector3 center;
        float radiusX,radiusZ,minY,height;
        bool burst;
        public float Progress { get; private set; }
        public bool IsPurifying { get; private set; }

        public void Configure(Bounds bounds)
        {
            center=transform.InverseTransformPoint(bounds.center); center.y=.18f;
            radiusX=bounds.extents.x*1.10f; radiusZ=bounds.extents.z*1.10f;
            minY=transform.InverseTransformPoint(bounds.min).y; height=bounds.size.y;
            material=new Material(Shader.Find("Sprites/Default")){name="Purified mint field"};
            material.SetColor("_Color",new Color(.55f,1.4f,.9f,1));
            perimeter=Ring("Purified sanctuary border",.045f);
            scan=Ring("Rising purification scan",.07f);
            var vertices=new Vector3[130]; var colors=new Color[130]; var triangles=new int[64*6];
            for(int i=0;i<=64;i++)
            {
                float a=i*Mathf.PI*2/64; var offset=new Vector3(Mathf.Cos(a)*radiusX,0,Mathf.Sin(a)*radiusZ);
                vertices[i*2]=center+offset*.90f; vertices[i*2+1]=center+offset*1.055f;
                colors[i*2]=new Color(.25f,1,.6f,0); colors[i*2+1]=new Color(.25f,1,.6f,.15f);
                if(i==64)continue;
                int t=i*6,v=i*2;
                triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;
                triangles[t+3]=v+1;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            field=new Mesh{name="Purified island aura"}; field.vertices=vertices; field.colors=colors; field.triangles=triangles; field.RecalculateBounds();
            var floor=new GameObject("Purified field",typeof(MeshFilter),typeof(MeshRenderer)); floor.transform.SetParent(transform,false);
            floor.GetComponent<MeshFilter>().sharedMesh=field; fill=floor.GetComponent<MeshRenderer>(); fill.sharedMaterial=material;
            fill.shadowCastingMode=ShadowCastingMode.Off; fill.receiveShadows=false;
            var go=new GameObject("Purified rising motes"); go.transform.SetParent(transform,false); go.transform.localPosition=Vector3.up*.48f;
            motes=go.AddComponent<ParticleSystem>(); motes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=motes.main;main.playOnAwake=false;main.loop=true;main.useUnscaledTime=true;main.maxParticles=80;
            main.startLifetime=new ParticleSystem.MinMaxCurve(1.2f,2.8f);main.startSpeed=.08f;
            main.startSize=new ParticleSystem.MinMaxCurve(.025f,.065f);main.startColor=new Color(.4f,1,.7f,.7f);
            var shape=motes.shape;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=2.4f;shape.rotation=new Vector3(-90,0,0);
            var velocity=motes.velocityOverLifetime;velocity.enabled=true;velocity.y=.32f;
            var fade=new Gradient();fade.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.7f,.25f),new GradientAlphaKey(0,1)});
            var color=motes.colorOverLifetime;color.enabled=true;color.color=fade;
            motes.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;
            SetCleared(false);
        }
        LineRenderer Ring(string name,float width)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=false;
            line.positionCount=points.Length;line.widthMultiplier=width;line.shadowCastingMode=ShadowCastingMode.Off;
            return line;
        }
        void DrawRing(LineRenderer line,float y,float scale,Color color)
        {
            for(int i=0;i<points.Length;i++)
            {
                float a=i*Mathf.PI*2/(points.Length-1);
                points[i]=new Vector3(center.x+Mathf.Cos(a)*radiusX*scale,y,center.z+Mathf.Sin(a)*radiusZ*scale);
            }
            line.SetPositions(points);line.startColor=line.endColor=color;
        }
        public void SetCleared(bool cleared)
        {
            IsPurifying=false;Progress=cleared?1:0; gameObject.SetActive(cleared);
            if(!cleared)return;
            DrawRing(perimeter,.18f,1,new Color(.25f,1,.6f,.75f));scan.enabled=false;fill.enabled=true;
            var emission=motes.emission;emission.rateOverTime=5;motes.Play();
        }
        public void Begin()
        {
            gameObject.SetActive(true); IsPurifying=true;burst=false;Progress=0;
            motes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);motes.Play();
            scan.enabled=true;fill.enabled=false;SetProgress(0);
        }
        public void SetProgress(float progress)
        {
            Progress=Mathf.Clamp01(progress);
            float flare=Mathf.Sin(Progress*Mathf.PI);
            DrawRing(scan,minY+height*Progress,Mathf.Lerp(.72f,1.05f,Progress),new Color(.4f,1,.7f,flare));
            DrawRing(perimeter,.18f,Mathf.Lerp(.75f,1,Progress),new Color(.25f,1,.6f,Progress*.85f));
            fill.enabled=Progress>.6f;
            var emission=motes.emission;emission.rateOverTime=5+flare*22;
            if(Progress>.72f&&!burst){burst=true;motes.Emit(45);}
            if(Progress>=1)SetCleared(true);
        }
        void OnDestroy(){if(material)Destroy(material);if(field)Destroy(field);}
    }
}
