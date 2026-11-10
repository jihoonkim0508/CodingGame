using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    /// <summary>Simple rounded UI geometry. Keeps the existing Image and Button references intact.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class LearningCardShape : BaseMeshEffect
    {
        [SerializeField, Min(0)] float radius = 16;
        [SerializeField, Min(0)] float borderWidth = 2;
        [SerializeField] Color border = new Color(.62f, .79f, .9f, 1);
        [SerializeField] bool shadow = true;
        [SerializeField] bool fadeBorderWithFill = true;
        const int Segments = 8, Points = (Segments + 1) * 4;
        public void Configure(float corner, float width, Color edge, bool dropShadow = true, bool fadeEdgeWithFill = true)
        { radius=corner; borderWidth=width; border=edge; shadow=dropShadow; fadeBorderWithFill=fadeEdgeWithFill; graphic.SetVerticesDirty(); }
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            vh.Clear();
            Rect rect=graphic.rectTransform.rect;
            if (rect.width<=0 || rect.height<=0) return;
            float r=Mathf.Min(radius,Mathf.Min(rect.width,rect.height)*.5f);
            Color fill=graphic.color, edge=border; if(fadeBorderWithFill)edge.a*=fill.a;
            if(shadow) { Rect drop=rect; drop.y-=4; Disc(vh,drop,r,new Color(.05f,.18f,.28f,.14f*fill.a)); }
            float b=Mathf.Min(borderWidth,Mathf.Min(rect.width,rect.height)*.5f);
            Rect inner=new Rect(rect.x+b,rect.y+b,rect.width-2*b,rect.height-2*b);
            if(b>0)Ring(vh,rect,r,inner,Mathf.Max(0,r-b),edge);
            Disc(vh,inner,Mathf.Max(0,r-b),fill);
        }
        static Vector2 Corner(Rect rect,float radius,int corner,int segment)
        {
            Vector2 center=new Vector2(corner<2?rect.xMax-radius:rect.xMin+radius,corner==0||corner==3?rect.yMin+radius:rect.yMax-radius);
            float angle=(-90+corner*90+segment*90f/Segments)*Mathf.Deg2Rad;
            return center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
        }
        static void Ring(VertexHelper vh,Rect outer,float outerRadius,Rect inner,float innerRadius,Color color)
        {
            int first=vh.currentVertCount;
            for(int corner=0;corner<4;corner++)for(int j=0;j<=Segments;j++){
                vh.AddVert(Corner(outer,outerRadius,corner,j),color,Vector2.zero);
                vh.AddVert(Corner(inner,innerRadius,corner,j),color,Vector2.zero);
            }
            for(int i=0;i<Points;i++){int a=first+i*2,b=first+(i+1)%Points*2;vh.AddTriangle(a,b,a+1);vh.AddTriangle(a+1,b,b+1);}
        }
        static void Disc(VertexHelper vh,Rect rect,float radius,Color color)
        {
            int first=vh.currentVertCount;
            vh.AddVert(rect.center,color,Vector2.zero);
            for(int corner=0;corner<4;corner++)
            {
                Vector2 center=new Vector2(corner<2?rect.xMax-radius:rect.xMin+radius,
                    corner==0||corner==3?rect.yMin+radius:rect.yMax-radius);
                for(int j=0;j<=Segments;j++)
                {
                    float a=(-90+corner*90+j*90f/Segments)*Mathf.Deg2Rad;
                    vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);
                }
            }
            for(int i=0;i<Points;i++) vh.AddTriangle(first,first+1+i,first+1+(i+1)%Points);
        }
    }
}
