using UnityEngine;
using UnityEngine.UI;
namespace CodingGame.Defense {
 public enum LearningGlyph { Sword, Shield, Target, Bomb, Support, Signal, Clock, Coin, Wave, Bug, Code }
 // Crisp, small role emblems drawn as UI geometry; no model portraits or font glyph dependency.
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class LearningSymbol:MaskableGraphic {
  [SerializeField] LearningGlyph symbol;
  public void Set(LearningGlyph value,Color tint){symbol=value;color=tint;raycastTarget=false;SetVerticesDirty();}
  VertexHelper mesh;
  Vector2 P(float x,float y){var r=rectTransform.rect;return r.center+new Vector2(x,y)*Mathf.Min(r.width,r.height)*.5f;}
  void Line(float x,float y,float a,float b,float w=.13f){Vector2 p=P(x,y),q=P(a,b),n=new Vector2(-(q-p).y,(q-p).x).normalized*Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*w*.25f;int i=mesh.currentVertCount;mesh.AddVert(p+n,color,Vector2.zero);mesh.AddVert(q+n,color,Vector2.zero);mesh.AddVert(q-n,color,Vector2.zero);mesh.AddVert(p-n,color,Vector2.zero);mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);}
  void Circle(float x,float y,float r,bool solid=false){for(int i=0;i<24;i++){float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;if(solid){int n=mesh.currentVertCount;mesh.AddVert(P(x,y),color,Vector2.zero);mesh.AddVert(P(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r),color,Vector2.zero);mesh.AddVert(P(x+Mathf.Cos(b)*r,y+Mathf.Sin(b)*r),color,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);}else Line(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r,x+Mathf.Cos(b)*r,y+Mathf.Sin(b)*r);}}
  protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();mesh=vh;switch(symbol){
   case LearningGlyph.Sword:Line(-.52f,-.65f,.5f,.55f,.24f);Line(.5f,.55f,.58f,.8f,.22f);Line(-.55f,-.18f,-.08f,-.58f,.2f);break;
   case LearningGlyph.Shield:Line(-.6f,.65f,0,.8f);Line(0,.8f,.6f,.65f);Line(-.6f,.65f,-.48f,-.25f);Line(.6f,.65f,.48f,-.25f);Line(-.48f,-.25f,0,-.75f);Line(0,-.75f,.48f,-.25f);Line(0,-.25f,0,.4f,.18f);break;
   case LearningGlyph.Target:Circle(0,0,.57f);Circle(0,0,.18f,true);Line(-.85f,0,-.38f,0);Line(.38f,0,.85f,0);Line(0,-.85f,0,-.38f);Line(0,.38f,0,.85f);break;
   case LearningGlyph.Bomb:Circle(-.08f,-.18f,.5f,true);Line(.16f,.28f,.38f,.57f,.17f);Line(.38f,.57f,.66f,.53f);Line(.65f,.65f,.8f,.82f);Line(.76f,.46f,.95f,.44f);break;
   case LearningGlyph.Support:Circle(0,0,.72f);Line(-.4f,0,.4f,0,.28f);Line(0,-.4f,0,.4f,.28f);break;
   case LearningGlyph.Signal:Circle(0,-.52f,.15f,true);Line(0,-.4f,0,.6f,.18f);Line(-.4f,-.05f,-.5f,.3f);Line(-.5f,.3f,-.32f,.6f);Line(.4f,-.05f,.5f,.3f);Line(.5f,.3f,.32f,.6f);break;
   case LearningGlyph.Clock:Circle(0,-.05f,.65f);Line(0,-.05f,0,.37f,.18f);Line(0,-.05f,.36f,-.25f,.18f);Line(-.2f,.82f,.2f,.82f,.2f);break;
   case LearningGlyph.Coin:Circle(0,0,.7f);Circle(0,0,.5f);Line(-.25f,.15f,-.4f,0);Line(-.4f,0,-.25f,-.15f);Line(.25f,.15f,.4f,0);Line(.4f,0,.25f,-.15f);break;
   case LearningGlyph.Wave:for(int j=0;j<3;j++){float y=.5f-j*.5f;Line(-.7f,y-.13f,-.3f,y+.1f);Line(-.3f,y+.1f,.2f,y-.1f);Line(.2f,y-.1f,.7f,y+.13f);}break;
   case LearningGlyph.Bug:Circle(0,0,.45f,true);for(int j=-1;j<=1;j++){Line(-.35f,j*.35f,-.75f,j*.5f);Line(.35f,j*.35f,.75f,j*.5f);}Line(-.2f,.4f,-.4f,.75f);Line(.2f,.4f,.4f,.75f);break;
   default:Line(-.2f,.55f,-.7f,0,.2f);Line(-.7f,0,-.2f,-.55f,.2f);Line(.2f,.55f,.7f,0,.2f);Line(.7f,0,.2f,-.55f,.2f);break;
  }}
 }
}
