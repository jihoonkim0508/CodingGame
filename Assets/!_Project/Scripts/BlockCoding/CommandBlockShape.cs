using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.BlockCoding
{
    // A resizable UI graphic; the editable block hierarchy is authored in prefabs.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CommandBlockShape : MaskableGraphic
    {
        [SerializeField] BlockSlotKind slot;
        [SerializeField] bool control;
        bool controlFamily;
        internal const float ControlSpineWidth = 28;
        internal const float ControlFooterHeight = 24;
        public float HeaderHeight { get; set; } = 48;
        public bool JoinNext { get; set; }
        // Out of stock or left behind while dragged: the face mixes towards the dark workspace. Lifted marks the dragged copy.
        public float Fade { get; set; }
        public bool Lifted { get; set; }
        // An edge round the block, for the reward panel's previews only (the editor's blocks have none): there a while True
        // the colour of the panel was lost on it. 시안: three looks; the user keeps one and the others go.
        public enum OutlineLook { None, Dark, Light, Bevel }
        OutlineLook outline;
        public OutlineLook Outline { get => outline; set { if (outline == value) return; outline = value; SetVerticesDirty(); } }
        const float OutlineWidth = 2;
        Color Tint => Fade > 0 ? Color.Lerp(color, new Color(.12f, .18f, .25f, color.a), Fade) : color;
        readonly List<UIVertex> hitTriangles = new List<UIVertex>();
        Texture2D generatedSurface;
        Texture2D openSurface;
        public override Texture mainTexture => (control || controlFamily) && openSurface ? openSurface : generatedSurface ? generatedSurface : base.mainTexture;
        protected override void OnEnable()
        {
            base.OnEnable();
            generatedSurface = Resources.Load<Texture2D>("LearningSkins/JellySurface");
            openSurface = Resources.Load<Texture2D>("LearningSkins/ControlOpenFrameV2");
            SetMaterialDirty(); SetVerticesDirty();
        }
#if UNITY_EDITOR
        public IReadOnlyList<UIVertex> EditorTriangles => hitTriangles;
#endif

        public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
        {
            if (!base.Raycast(screenPoint, eventCamera) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var point)) return false;
            for (int i = 0; i < hitTriangles.Count; i += 3)
            {
                Vector2 a = hitTriangles[i].position, b = hitTriangles[i + 1].position, c = hitTriangles[i + 2].position;
                float ab = Cross(b - a, point - a), bc = Cross(c - b, point - b), ca = Cross(a - c, point - c);
                if (Mathf.Abs(Cross(b - a, c - a)) > .0001f &&
                    (ab >= 0 && bc >= 0 && ca >= 0 || ab <= 0 && bc <= 0 && ca <= 0)) return true;
            }
            return false;
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public void Configure(BlockSlotKind output, bool hasBody, bool isControl = false)
        {
            if(control!=hasBody || controlFamily!=(isControl||hasBody))SetMaterialDirty();
            controlFamily=isControl||hasBody;
            slot = output;
            control = hasBody;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            hitTriangles.Clear();
            Rect r = rectTransform.rect;
            float w = r.width, h = r.height;
            if (w <= 0 || h <= 0) return;
            if (slot == BlockSlotKind.Condition)
                SoftPolygon(vh, r, new[] { new Vector2(0, h / 2), new Vector2(12, 0), new Vector2(w - 12, 0),
                    new Vector2(w, h / 2), new Vector2(w - 12, h), new Vector2(12, h) });
            else if (slot == BlockSlotKind.Value)
            {
                float radius = Mathf.Min(18, h / 2);
                var points = new Vector2[26];
                for (int i = 0; i < 13; i++)
                {
                    float angle = (-90 + i * 15) * Mathf.Deg2Rad;
                    points[i] = new Vector2(w - radius + Mathf.Cos(angle) * radius, h / 2 + Mathf.Sin(angle) * h / 2);
                    angle = (90 + i * 15) * Mathf.Deg2Rad;
                    points[i + 13] = new Vector2(radius + Mathf.Cos(angle) * radius, h / 2 + Mathf.Sin(angle) * h / 2);
                }
                Polygon(vh, r, points);
            }
            else
            {
                float header = control ? Mathf.Min(HeaderHeight, h) : h;
                RoundedQuad(vh, r, 0, 0, w, header, controlFamily?8:12);
                if (control)
                {
                    RoundedQuad(vh, r, 0, header-8, ControlSpineWidth, h-header+8, 7);
                    if (!JoinNext) RoundedQuad(vh, r, 0, h - ControlFooterHeight, w, ControlFooterHeight, 7);
                }
            }
            vh.GetUIVertexStream(hitTriangles);
            if(control && openSurface) {
                vh.Clear();
                OpenFrame(vh,r);
            }
        }

        // Stretch the transparent cavity, preserving the generated rounded joins and surface.
        void OpenFrame(VertexHelper vh,Rect r)
        {
            float top = Mathf.Min(HeaderHeight, r.height);
            float bottom = Mathf.Min(ControlFooterHeight, r.height - top);
            float[] x={r.xMin,r.xMin+ControlSpineWidth,r.xMax-14,r.xMax};
            float[] y={r.yMin,r.yMin+bottom,r.yMax-top,r.yMax};
            // Match the texture's transparent cutout to the same edges as the body slot.
            float[] u={.012f,.1055f,.91f,.988f},v={.04f,.191f,.603f,.95f};
            for(int row=0;row<3;row++)for(int column=0;column<3;column++){
                if(JoinNext && row==0 && column>0)continue;
                int start=vh.currentVertCount;
                vh.AddVert(new Vector3(x[column],y[row]),Tint,new Vector2(u[column],v[row]));
                vh.AddVert(new Vector3(x[column],y[row+1]),Tint,new Vector2(u[column],v[row+1]));
                vh.AddVert(new Vector3(x[column+1],y[row+1]),Tint,new Vector2(u[column+1],v[row+1]));
                vh.AddVert(new Vector3(x[column+1],y[row]),Tint,new Vector2(u[column+1],v[row]));
                vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
            }
            if(Outline==OutlineLook.None)return;
            // The C traced on the texture's own edge (its pixels, y down), rounded there and mapped through the same
            // nine cells, so the outline bends with the stretched corners. Joined to an elif/else: no footer, open at the bottom.
            var corners=JoinNext
                ?new[]{new Vector3(187,847,0),new Vector3(187,352,30),new Vector3(1750,352,50),new Vector3(1750,48,58),new Vector3(23,48,58),new Vector3(23,847,0)}
                :new[]{new Vector3(23,48,58),new Vector3(1750,48,58),new Vector3(1750,352,50),new Vector3(187,352,30),
                    new Vector3(187,716,36),new Vector3(1750,716,45),new Vector3(1750,847,45),new Vector3(23,847,58)};
            var path=new List<Vector2>();
            foreach(var p in Rounded(corners,!JoinNext))
                path.Add(new Vector2(Piece(p.x/1774f,u,x),Piece(1-p.y/887f,v,y)));
            Stroke(vh,path,!JoinNext,new Vector2(.5f,.8f),new Color(.761f,.769f,.788f));
        }
        static float Piece(float t,float[] from,float[] to)
        {
            int i=1;while(i<from.Length-1&&t>from[i])i++;
            return Mathf.LerpUnclamped(to[i-1],to[i],(t-from[i-1])/(from[i]-from[i-1]));
        }
        // Corners (x, y, radius) of right-angled outline; each one becomes a quarter arc tangent to its two edges.
        static List<Vector2> Rounded(Vector3[] corners,bool closed)
        {
            var points=new List<Vector2>();int n=corners.Length;
            for(int i=0;i<n;i++){
                Vector2 c=corners[i];float radius=corners[i].z;
                if(radius<=0||!closed&&(i==0||i==n-1)){points.Add(c);continue;}
                Vector2 a=((Vector2)corners[(i+n-1)%n]-c).normalized,b=((Vector2)corners[(i+1)%n]-c).normalized,center=c+(a+b)*radius;
                for(int j=0;j<=6;j++)points.Add(center+(Vector2)Vector3.Slerp(-b,-a,j/6f)*radius);
            }
            return points;
        }
        // A band just inside the edge (the hit area and the layout stay as they were). texel: the surface colour at uv,
        // divided out so the band shows its own colour. Bevel adds a 1 px light line inside it on the upward-facing edges.
        void Stroke(VertexHelper vh,List<Vector2> path,bool closed,Vector2 uv,Color texel)
        {
            for(int i=path.Count-1;i>0;i--)if((path[i]-path[i-1]).sqrMagnitude<.0001f)path.RemoveAt(i);
            if(closed&&path.Count>1&&(path[0]-path[path.Count-1]).sqrMagnitude<.0001f)path.RemoveAt(path.Count-1);
            if(path.Count<2)return;
            Color edge=Outline==OutlineLook.Light?Color.Lerp(Tint,Color.white,.65f):Color.Lerp(Tint,new Color(.06f,.07f,.12f,Tint.a),.78f);
            Band(vh,path,closed,0,OutlineWidth,edge,false,uv,texel);
            if(Outline==OutlineLook.Bevel)Band(vh,path,closed,OutlineWidth,OutlineWidth+1,Color.Lerp(Tint,Color.white,.7f),true,uv,texel);
        }
        static void Band(VertexHelper vh,List<Vector2> path,bool closed,float from,float to,Color tone,bool upward,Vector2 uv,Color texel)
        {
            int n=path.Count,start=vh.currentVertCount;
            float area=0;for(int i=0;i<n;i++)area+=Cross(path[i],path[(i+1)%n]);
            float side=area>0?1:-1;
            Vector2 Normal(int i,int j){var d=(path[j]-path[i]).normalized;return new Vector2(-d.y,d.x)*side;} // inward
            var shade=new Color(Mathf.Min(1,tone.r/texel.r),Mathf.Min(1,tone.g/texel.g),Mathf.Min(1,tone.b/texel.b),tone.a);
            for(int i=0;i<n;i++){
                bool first=!closed&&i==0,last=!closed&&i==n-1;
                Vector2 before=first?Normal(0,1):Normal((i+n-1)%n,i),after=last?Normal(n-2,n-1):Normal(i,(i+1)%n);
                Vector2 miter=(before+after).normalized;float stretch=1/Mathf.Max(.35f,Vector2.Dot(miter,before));
                var c=shade;if(upward)c.a*=Mathf.Clamp01(-miter.y*1.4f-.2f);
                vh.AddVert(path[i]+miter*stretch*from,c,uv);
                vh.AddVert(path[i]+miter*stretch*to,c,uv);
            }
            for(int i=0;i<(closed?n:n-1);i++){
                int a=start+2*i,b=start+2*((i+1)%n);
                vh.AddTriangle(a,b,b+1);vh.AddTriangle(a,b+1,a+1);
            }
        }

        void RoundedQuad(VertexHelper vh,Rect r,float x,float y,float w,float h,float radius)
        {
            if(w<=0||h<=0)return;radius=Mathf.Min(radius,Mathf.Min(w,h)/2);
            var points=new Vector2[28];
            for(int corner=0;corner<4;corner++){
                var center=new Vector2(corner<2?x+w-radius:x+radius,corner==0||corner==3?y+radius:y+h-radius);
                for(int j=0;j<7;j++){float angle=(-90+corner*90+j*15)*Mathf.Deg2Rad;points[corner*7+j]=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;}
            }
            Polygon(vh,r,points);
        }
        void SoftPolygon(VertexHelper vh,Rect r,Vector2[] corners)
        {
            var points=new Vector2[corners.Length*5];
            for(int i=0;i<corners.Length;i++){
                var c=corners[i];var a=Vector2.MoveTowards(c,corners[(i+corners.Length-1)%corners.Length],5);var b=Vector2.MoveTowards(c,corners[(i+1)%corners.Length],5);
                for(int j=0;j<5;j++){float t=j/4f;points[i*5+j]=(1-t)*(1-t)*a+2*(1-t)*t*c+t*t*b;}
            }
            Polygon(vh,r,points);
        }

        void Polygon(VertexHelper vh, Rect r, Vector2[] points)
        {
            int start = vh.currentVertCount;
            // Each piece of a control block gets its own surface. Mapping the whole
            // C-shaped rect sampled transparent image corners into the spine/footer.
            Vector2 min=points[0],max=points[0];
            foreach(var p in points){min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            Vector2 size=max-min;
            foreach (var p in points) vh.AddVert(new Vector3(r.xMin + p.x, r.yMax - p.y), Tint,
                new Vector2(Mathf.Lerp(.14f,.86f,(p.x-min.x)/Mathf.Max(.001f,size.x)),
                    Mathf.Lerp(controlFamily && !control ? .94f : .84f,controlFamily && !control ? .62f : .16f,(p.y-min.y)/Mathf.Max(.001f,size.y))));
            for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
            // A C block's pieces overlap; its outline is traced whole in OpenFrame.
            if (control || Outline == OutlineLook.None) return;
            var path = new List<Vector2>();
            foreach (var p in points) path.Add(new Vector2(r.xMin + p.x, r.yMax - p.y));
            if (mainTexture == openSurface) Stroke(vh, path, true, new Vector2(.5f, .8f), new Color(.761f, .769f, .788f));
            else Stroke(vh, path, true, new Vector2(.5f, .5f), new Color(.706f, .749f, .804f));
        }
    }
}
