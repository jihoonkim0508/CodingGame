using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.StageMap
{
    // Canvas-native shapes stay crisp at any Canvas Scaler resolution.
    public sealed class StageMapSymbolGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var mint=new Color(.38f,1,.72f);
            Disc(mesh,new Vector2(0,12),63,new Color(.15f,1,.64f,.06f));
            Ring(mesh,new Vector2(0,12),55,58,new Color(.5f,1,.77f,.55f));
            Ring(mesh,new Vector2(0,12),46,49,new Color(.84f,1,.92f,.8f));
            var shield=new[]{new Vector2(-30,38),new Vector2(0,49),new Vector2(30,38),new Vector2(27,1),new Vector2(0,-23),new Vector2(-27,1)};
            Polygon(mesh,shield,new Color(.17f,.78f,.51f));
            var inner=new Vector2[shield.Length];
            for(int i=0;i<inner.Length;i++)inner[i]=new Vector2(shield[i].x*.84f,(shield[i].y-12)*.84f+12);
            Polygon(mesh,inner,new Color(.035f,.23f,.17f,.98f));
            Segment(mesh,new Vector2(-15,13),new Vector2(-3,1),7,Color.white);
            Segment(mesh,new Vector2(-3,1),new Vector2(19,27),7,Color.white);
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<5;i++)
                {
                    float y=-15+i*12;
                    float x=side*(47+Mathf.Sin(i*.6f)*5);
                    Leaf(mesh,new Vector2(x,y),side*(35+i*8),mint);
                }
                Star(mesh,new Vector2(side*72,20+side*14),8,new Color(.88f,1,.95f));
            }
            Star(mesh,new Vector2(0,78),9,new Color(1,.82f,.43f));
        }
        void Leaf(VertexHelper m,Vector2 p,float angle,Color c)
        {
            var d=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
            var n=new Vector2(-d.y,d.x);
            Polygon(m,new[]{p-d*9,p+n*4,p+d*9,p-n*4},c);
        }
        void Star(VertexHelper m,Vector2 p,float size,Color c)
        {
            Polygon(m,new[]{p+Vector2.up*size,p+Vector2.right*2,p-Vector2.up*size,p-Vector2.right*2},c);
            Polygon(m,new[]{p+Vector2.right*size,p+Vector2.up*2,p-Vector2.right*size,p-Vector2.up*2},c);
        }
        void Segment(VertexHelper m,Vector2 a,Vector2 b,float width,Color c)
        {
            var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;
            Polygon(m,new[]{a+n,b+n,b-n,a-n},c);
        }
        void Disc(VertexHelper m,Vector2 p,float r,Color c)
        {
            var points=new Vector2[64];for(int i=0;i<points.Length;i++){float a=i*Mathf.PI*2/points.Length;points[i]=p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;}
            Polygon(m,points,c);
        }
        void Ring(VertexHelper m,Vector2 p,float inner,float outer,Color c)
        {
            for(int i=0;i<64;i++)
            {
                float a=i*Mathf.PI*2/64,b=(i+1)*Mathf.PI*2/64;
                var u=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var v=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                Polygon(m,new[]{p+u*inner,p+u*outer,p+v*outer,p+v*inner},c);
            }
        }
        void Polygon(VertexHelper m,Vector2[] points,Color tint)
        {
            int start=m.currentVertCount;
            var origin=rectTransform.rect.center;
            Color final=tint*color;
            foreach(var p in points)m.AddVert(new Vector3(p.x+origin.x,p.y+origin.y),final,Vector2.zero);
            for(int i=1;i<points.Length-1;i++)m.AddTriangle(start,start+i,start+i+1);
        }
    }
}
