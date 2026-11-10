using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.TitleScreen
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TitleProjectileEffect : MaskableGraphic
    {
        public TitleTurretMotion turret;
        public RectTransform muzzle;
        public Vector2 target = new Vector2(490, -290);
        public float flightSeconds = .55f;
        public float arcHeight = 50;
        public int VisibleProjectiles { get; private set; }
        public float ImpactIntensity { get; private set; }
        readonly Vector2[] origins = new Vector2[4];
        float previousPhase = -1;
        float phase = -1;

        void LateUpdate()
        {
            if (!turret || !muzzle || !turret.presentation.IsReady) return;
            phase = turret.ShotPhase;
            if (phase < previousPhase) previousPhase = -1;
            bool wasVisible = VisibleProjectiles > 0 || ImpactIntensity > 0;
            VisibleProjectiles = 0;
            ImpactIntensity = 0;
            for (int i=0;i<Mathf.Min(4,turret.burstCount);i++)
            {
                float launch=i*turret.burstSpacing+.025f;
                if(previousPhase<launch&&phase>=launch)origins[i]=rectTransform.InverseTransformPoint(muzzle.position);
                float age=phase-launch;
                if(age>=0&&age<flightSeconds)VisibleProjectiles++;
                float impactAge=age-flightSeconds;
                if(impactAge>=0&&impactAge<.13f)ImpactIntensity=Mathf.Max(ImpactIntensity,1-impactAge/.13f);
            }
            previousPhase=phase;
            if (wasVisible || VisibleProjectiles > 0 || ImpactIntensity > 0) SetVerticesDirty();
        }
        Vector2 Position(int index,float t) => Vector2.Lerp(origins[index],target,t)+Vector2.up*(arcHeight*4*t*(1-t));
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if(!turret||!turret.presentation.IsReady||phase<0)return;
            for(int i=0;i<Mathf.Min(4,turret.burstCount);i++)
            {
                float age=phase-i*turret.burstSpacing-.025f;
                if(age<0||age>=flightSeconds)continue;
                float p=age/flightSeconds;
                Vector2 head=Position(i,p),tail=Position(i,Mathf.Max(0,p-.075f));
                Segment(vh,tail,head,8,new Color(.1f,.75f,1,.18f));
                Segment(vh,tail,head,2.5f,new Color(.66f,.95f,1,.94f));
                Segment(vh,head-Vector2.right*5,head+Vector2.right*2,3,new Color(1,1,.88f,.95f));
            }
            if(ImpactIntensity>0)
            {
                for(int i=0;i<6;i++)
                {
                    float angle=i*Mathf.PI/3;
                    Vector2 direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                    Segment(vh,target+direction*2,target+direction*(8+8*(1-ImpactIntensity)),1.8f,
                        new Color(.5f,.88f,1,ImpactIntensity*.75f));
                }
            }
        }
        static void Segment(VertexHelper vh,Vector2 a,Vector2 b,float width,Color tint)
        {
            Vector2 direction=(b-a).normalized;
            Vector2 normal=new Vector2(-direction.y,direction.x)*width*.5f;
            int n=vh.currentVertCount;
            vh.AddVert(a-normal,tint,Vector2.zero);vh.AddVert(a+normal,tint,Vector2.zero);
            vh.AddVert(b+normal,tint,Vector2.zero);vh.AddVert(b-normal,tint,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
