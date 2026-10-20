using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CodingGame.Art.MapPainter.Editor;
using Object=UnityEngine.Object;

namespace CodingGame.Art.Workshop
{
 public static class WorkshopModels
 {
  public const string Root="Assets/!_Project/Resources/Models/Stage01_Prop";
  static readonly Dictionary<string,Material> mats=new Dictionary<string,Material>();
  static Mesh rounded,cube,sphere,cylinder;static GameObject model;
  static Color C(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var c);return c;}
  static Material Mat(string name,string hex,bool glow=false,float grain=0)
  {
   string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
   var shader=glow?Shader.Find("Universal Render Pipeline/Unlit"):AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/WorkshopSurface.shader");
   if(!m){m=new Material(shader){name=name};AssetDatabase.CreateAsset(m,path);}else m.shader=shader;
   m.SetColor("_BaseColor",C(hex));if(!glow)m.SetFloat("_GrainStrength",grain);EditorUtility.SetDirty(m);mats[name]=m;return m;
  }
  public static void Bake()
  {
   foreach(var dir in new[]{Root,Root+"/Meshes",Root+"/Materials",Root+"/Prefabs"})
   {
    if(Directory.Exists(dir)){if(!AssetDatabase.IsValidFolder(dir))AssetDatabase.ImportAsset(dir,ImportAssetOptions.ForceSynchronousImport);}
    else MapPainterAssets.EnsureFolder(dir);
   }
   Mat("Cream","EDE2C9");Mat("Ivory","FFF3D8");Mat("Ink","243646");Mat("Screen","142635",true);Mat("Blue","397BC8");Mat("Azure","67BCE2");Mat("Orange","EFA34D");Mat("Terracotta","B87852");Mat("Green","48804D");Mat("LeafLight","91AD50");Mat("LeafDark","30664B");Mat("Coffee","493527");Mat("Paper","E9DCC1");Mat("Coral","D56B55");Mat("Mint","7DA77D");Mat("Gold","E2BB65");Mat("CyanGlow","84E6DC",true);Mat("AmberGlow","FFE2A0",true);Mat("RedGlow","FA605C",true);Mat("WhiteGlow","E4FFF0",true);Mat("DeskWood","D4A76E",false,.8f);Mat("DeskEdge","A57249",false,.8f);Mat("RoadCream","F5DBAD");Mat("RoadEdge","BA955C");
   rounded=RoundBox();cube=Builtin(PrimitiveType.Cube);sphere=Builtin(PrimitiveType.Sphere);cylinder=Builtin(PrimitiveType.Cylinder);
   Make("Monitor",Monitor);Make("Keyboard",Keyboard);Make("Mouse",Mouse);Make("Speaker",Speaker);Make("Mug",Mug);Make("Plant",Plant);Make("Books",Books);Make("Notes",Notes);Make("Lamp",Lamp);Make("Board",Board);Make("BuildPad",Pad);Make("ErrorGate",ErrorGate);Make("CodeBase",CodeBase);
   Object.DestroyImmediate(rounded);AssetDatabase.SaveAssets();
  }
  public static Material Material(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+name+".mat");
  static Mesh Builtin(PrimitiveType type){var go=GameObject.CreatePrimitive(type);var mesh=go.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(go);return mesh;}
  static void Part(string name,Mesh mesh,Vector3 p,Vector3 size,string material,Vector3 angles=default)
  {
   var go=new GameObject(name);go.transform.SetParent(model.transform,false);go.transform.localPosition=p;go.transform.localScale=size;go.transform.localRotation=Quaternion.Euler(angles);
   go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mats[material];
  }
  static void Box(Vector3 p,Vector3 s,string m,Vector3 a=default,bool bevel=true)=>Part(m,bevel?rounded:cube,p,s,m,a);
  static void Ball(Vector3 p,Vector3 s,string m,Vector3 a=default)=>Part(m,sphere,p,s,m,a);
  static void Cyl(Vector3 p,float radius,float height,string m,Vector3 a=default)=>Part(m,cylinder,p,new Vector3(radius*2,height*.5f,radius*2),m,a);
  static void Make(string name,Action build)
  {
   model=new GameObject(name);
   try
   {
    build();var filters=model.GetComponentsInChildren<MeshFilter>();
    var temporary=filters.Select(f=>f.sharedMesh).Where(m=>m!=rounded&&!EditorUtility.IsPersistent(m)).Distinct().ToArray();
    foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
    {
     var combined=new Mesh{name=name+"_"+group.Key.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
     combined.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=model.transform.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
     string path=Root+"/Meshes/"+combined.name+".asset";MapPainterAssets.SaveMesh(combined,path);
     var go=new GameObject(group.Key.name);go.transform.SetParent(model.transform,false);go.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);go.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
    }
    foreach(var filter in filters)Object.DestroyImmediate(filter.gameObject);
    foreach(var mesh in temporary)Object.DestroyImmediate(mesh);
    PrefabUtility.SaveAsPrefabAsset(model,Root+"/Prefabs/"+name+".prefab");
   }
   finally{Object.DestroyImmediate(model);}
  }
  static void Screen(Vector3 p,float width,float height)
  {
   Box(p,new Vector3(width,height,.055f),"Screen");
   Box(p+new Vector3(-width*.36f,0,-.04f),new Vector3(width*.16f,height*.91f,.025f),"Ink",default,false);
   for(int i=0;i<3;i++)Ball(p+new Vector3(-width*.43f+i*.14f,height*.42f,-.04f),new Vector3(.075f,.075f,.04f),i==0?"Coral":i==1?"Gold":"Mint");
   for(int row=0;row<10;row++)
   {
    float y=height*.28f-row*height*.065f;float indent=(row%5==0?0:row%3==0?.2f:.4f)*width*.1f;
    float start=-width*.23f+indent;
    for(int segment=0;segment<3;segment++)
    {
     float length=width*(.07f+((row*7+segment*3)%5)*.026f);
     Box(p+new Vector3(start+length*.5f,y,-.045f),new Vector3(length,height*.024f,.015f),new[]{"Azure","Mint","Gold","Coral"}[(row+segment)%4],default,false);start+=length+width*.025f;
    }
   }
  }
  static void Monitor()
  {
   Box(new Vector3(0,.13f,.1f),new Vector3(2.8f,.25f,1.6f),"Ink");Box(new Vector3(0,1.25f,.22f),new Vector3(.5f,2.3f,.42f),"Ink");
   Box(new Vector3(0,3.5f,0),new Vector3(7.1f,4.15f,.4f),"Ink");Box(new Vector3(0,3.5f,-.22f),new Vector3(6.8f,3.86f,.08f),"Blue");Screen(new Vector3(0,3.54f,-.28f),6.55f,3.55f);
   Box(new Vector3(2.6f,2.05f,-.34f),new Vector3(.8f,.72f,.025f),"Gold",new Vector3(0,0,-7));Ball(new Vector3(2.95f,1.55f,-.24f),new Vector3(.08f,.08f,.04f),"CyanGlow");
  }
  static void Keyboard()
  {
   Box(new Vector3(0,.13f,0),new Vector3(4.4f,.26f,1.75f),"Cream");Box(new Vector3(0,.27f,0),new Vector3(4.2f,.09f,1.55f),"Ink");
   for(int z=0;z<5;z++)for(int x=0;x<12;x++)
   {if(z==0&&x>2&&x<9)continue;Box(new Vector3((x-5.5f)*.337f,.36f,(z-2)*.285f),new Vector3(.28f,.15f,.23f),z==4&&x==0?"Mint":"Ivory");}
   Box(new Vector3(0,.36f,-.57f),new Vector3(2.23f,.15f,.23f),"Ivory");
  }
  static void Mouse()
  {Box(new Vector3(0,.035f,0),new Vector3(1.65f,.06f,1.95f),"Ink");Ball(new Vector3(0,.2f,0),new Vector3(.75f,.4f,1.08f),"Cream");Box(new Vector3(0,.39f,.16f),new Vector3(.08f,.06f,.22f),"Ink");}
  static void Speaker()
  {
   Box(new Vector3(0,1.05f,0),new Vector3(.95f,2.1f,.85f),"Ink");
   Cyl(new Vector3(0,.68f,-.45f),.33f,.06f,"Coffee",new Vector3(90,0,0));Cyl(new Vector3(0,.68f,-.49f),.22f,.04f,"Blue",new Vector3(90,0,0));Cyl(new Vector3(0,1.5f,-.45f),.19f,.06f,"Gold",new Vector3(90,0,0));
  }
  static void Mug()
  {
   var mesh=Lathe(new[]{new Vector2(0,0),new Vector2(.46f,0),new Vector2(.52f,.1f),new Vector2(.54f,1.02f),new Vector2(.49f,1.08f),new Vector2(.44f,1.02f),new Vector2(.42f,.15f)},28);Part("Cup",mesh,Vector3.zero,Vector3.one,"Ivory");
   Cyl(new Vector3(0,.98f,0),.435f,.025f,"Coffee");Part("Handle",Torus(.32f,.10f),new Vector3(.63f,.63f,0),Vector3.one,"Ivory",new Vector3(90,0,0));
   Box(new Vector3(0,.53f,-.52f),new Vector3(.3f,.32f,.02f),"Blue");
  }
  static void Plant()
  {
   Part("Pot",Lathe(new[]{new Vector2(0,0),new Vector2(.45f,0),new Vector2(.65f,.83f),new Vector2(.69f,.85f),new Vector2(.69f,.98f),new Vector2(.58f,.98f),new Vector2(.55f,.78f)},20),Vector3.zero,Vector3.one,"Terracotta");Cyl(new Vector3(0,.83f,0),.56f,.04f,"Coffee");
   for(int i=0;i<10;i++){float a=i*137.5f*Mathf.Deg2Rad;Ball(new Vector3(Mathf.Sin(a)*.38f,1.28f+(i%3)*.18f,Mathf.Cos(a)*.38f),new Vector3(.52f,.95f,.32f),i%3==0?"LeafDark":i%3==1?"Green":"LeafLight",new Vector3(Mathf.Cos(a)*45,i*137.5f,Mathf.Sin(a)*45));}
  }
  static void Books()
  {
   for(int i=0;i<3;i++)
   {float y=.2f+i*.43f;string c=new[]{"Coral","Blue","Mint"}[i];Box(new Vector3(0,y,0),new Vector3(2.2f,.29f,1.65f),"Paper");Box(new Vector3(0,y-.18f,0),new Vector3(2.3f,.07f,1.75f),c);Box(new Vector3(0,y+.18f,0),new Vector3(2.3f,.07f,1.75f),c);Box(new Vector3(-1.1f,y,0),new Vector3(.16f,.38f,1.75f),c);for(int line=0;line<3;line++)Box(new Vector3(.05f,y-.08f+line*.08f,-.83f),new Vector3(1.9f,.012f,.01f),"Cream",default,false);}
   Box(new Vector3(.12f,1.42f,0),new Vector3(1.7f,.08f,1.2f),"Ink");Box(new Vector3(.4f,1.47f,0),new Vector3(.2f,.02f,1.2f),"Gold");
  }
  static void Notes()
  {
   Box(new Vector3(0,.04f,0),new Vector3(1.5f,.08f,1.8f),"Paper",new Vector3(0,-8,0));
   for(int i=0;i<5;i++)Box(new Vector3(0,.087f,.5f-i*.19f),new Vector3(i==0?.75f:1.1f,.012f,.035f),i==0?"Blue":"Terracotta",default,false);
   Box(new Vector3(.45f,.13f,-.4f),new Vector3(.6f,.025f,.55f),"Gold",new Vector3(0,17,0));Cyl(new Vector3(.9f,.11f,.1f),.045f,1.6f,"Orange",new Vector3(0,0,65));
  }
  static void Lamp()
  {
   Cyl(new Vector3(0,.1f,0),.65f,.2f,"Ink");Box(new Vector3(0,1.1f,.1f),new Vector3(.14f,1.9f,.14f),"Gold",new Vector3(0,0,-15));Ball(new Vector3(.2f,1.95f,.1f),Vector3.one*.24f,"Ink");Box(new Vector3(-.28f,2.36f,.1f),new Vector3(1.35f,.15f,.16f),"Gold",new Vector3(0,0,-35));
   Part("Shade",Lathe(new[]{new Vector2(.6f,0),new Vector2(.2f,.65f),new Vector2(0,.65f)},24),new Vector3(-.78f,2.12f,.1f),Vector3.one,"Orange",new Vector3(0,0,-18));Cyl(new Vector3(-.78f,2.12f,.1f),.49f,.025f,"AmberGlow");
  }
  static void Board()
  {
   Box(new Vector3(0,1.7f,0),new Vector3(4.3f,2.7f,.22f),"Cream");Box(new Vector3(0,1.7f,-.15f),new Vector3(4.0f,2.4f,.035f),"Ivory");
   foreach(float x in new[]{-1.5f,1.5f}){Box(new Vector3(x,.5f,.1f),new Vector3(.12f,1,.16f),"Ink");Box(new Vector3(x,.08f,0),new Vector3(.7f,.15f,1),"Ink");}
   for(int i=0;i<5;i++){float x=-1.4f+(i%3)*1.3f;float y=2.3f-(i/3)*1.2f;Box(new Vector3(x,y,-.19f),new Vector3(.87f,.7f,.025f),i%2==0?"Gold":"Mint",new Vector3(0,0,i*4-7));for(int l=0;l<3;l++)Box(new Vector3(x,y+.15f-l*.13f,-.21f),new Vector3(.5f,.025f,.01f),"Ink",default,false);}
  }
  static void Pad()
  {Box(new Vector3(0,.065f,0),new Vector3(1.55f,.13f,1.55f),"Mint");for(int i=0;i<4;i++){float a=i*90;var q=Quaternion.Euler(0,a,0);Box(q*new Vector3(.52f,.14f,.62f),new Vector3(.25f,.018f,.035f),"Ivory",new Vector3(0,a,0),false);Box(q*new Vector3(.62f,.14f,.52f),new Vector3(.035f,.018f,.25f),"Ivory",new Vector3(0,a,0),false);}}
  public const float DoorWidth=1.9f,DoorHeight=2.15f;
  static void CodeBase()
  {
   Box(new Vector3(0,-.18f,0),new Vector3(5.4f,.4f,4.7f),"Ink");Box(new Vector3(0,.05f,.55f),new Vector3(5.15f,.22f,3.4f),"Cream");
   foreach(float side in new[]{-1f,1f})Box(new Vector3(side*1.8f,.05f,-1.7f),new Vector3(1.55f,.22f,1.2f),"Cream");
   foreach(float side in new[]{-1f,1f})
   {
    Box(new Vector3(side*1.65f,1.2f,0),new Vector3(1.25f,2.3f,3.4f),"Cream");Box(new Vector3(side*2.18f,.65f,-1.3f),new Vector3(.8f,1.3f,1.1f),"Blue");Box(new Vector3(side*1.85f,2.7f,.3f),new Vector3(1.2f,.7f,2.6f),"Blue");
    Box(new Vector3(side*1.65f,1.8f,-1.73f),new Vector3(.85f,1.1f,.08f),"Ink");for(int line=0;line<4;line++)Box(new Vector3(side*1.65f,2.1f-line*.21f,-1.8f),new Vector3(.52f,.065f,.025f),line==0?"CyanGlow":"Cream",default,false);
    Box(new Vector3(side*(DoorWidth*.5f+.13f),DoorHeight*.5f,-1.3f),new Vector3(.23f,DoorHeight,.3f),"Blue");
    Box(new Vector3(side*1.6f,.5f,-2.0f),new Vector3(.58f,.5f,1.1f),"Cream");Box(new Vector3(side*1.6f,.76f,-2.1f),new Vector3(.2f,.04f,.6f),"Orange");
   }
   // Open passage: separate jambs and lintel, no cube or collider across the opening.
   Box(new Vector3(0,DoorHeight+.25f,-1.3f),new Vector3(2.5f,.5f,.5f),"Blue");Box(new Vector3(0,DoorHeight+.03f,-1.58f),new Vector3(DoorWidth,.055f,.035f),"CyanGlow");
   Box(new Vector3(0,1.0f,1.25f),new Vector3(2.4f,2.0f,.2f),"Ink");
   Part("Entrance ramp",Ramp(),Vector3.zero,Vector3.one,"Blue");
   foreach(float side in new[]{-1f,1f})Box(new Vector3(side*1.11f,.28f,-1.8f),new Vector3(.15f,.16f,2),"Cream",new Vector3(-7.4f,0,0));
   Box(new Vector3(0,3.7f,-.25f),new Vector3(3.6f,2.5f,.65f),"Cream");Screen(new Vector3(0,3.72f,-.63f),3.1f,2.05f);
   Box(new Vector3(0,5.12f,0),new Vector3(2.7f,.35f,1.8f),"Blue");Box(new Vector3(0,5.75f,0),new Vector3(1.2f,1.0f,1.2f),"Azure");
   foreach(float side in new[]{-1f,1f})foreach(float upper in new[]{-1f,1f})Box(new Vector3(side*.29f,5.75f+upper*.11f,-.63f),new Vector3(.065f,.28f,.04f),"WhiteGlow",new Vector3(0,0,side*upper*40));
   Box(new Vector3(0,5.75f,-.63f),new Vector3(.05f,.43f,.04f),"WhiteGlow",new Vector3(0,0,-20));
   Cyl(new Vector3(1.9f,3.7f,.5f),.09f,1.5f,"Azure");Ball(new Vector3(1.9f,4.5f,.5f),Vector3.one*.26f,"CyanGlow");
   Cyl(new Vector3(-1.9f,3.4f,.6f),.09f,.9f,"Ink");Part("Dish",Lathe(new[]{new Vector2(0,0),new Vector2(.65f,.18f),new Vector2(.7f,.28f)},24),new Vector3(-1.9f,3.9f,.6f),Vector3.one,"Ivory",new Vector3(30,0,-25));
  }
  static void ErrorGate()
  {
   Box(new Vector3(0,-.12f,0),new Vector3(4.2f,.3f,3.3f),"Ink");
   foreach(float side in new[]{-1f,1f})
   {Box(new Vector3(side*1.65f,1.9f,.4f),new Vector3(1,3.8f,2),side<0?"Coral":"Blue");Box(new Vector3(side*1.65f,1.9f,-.65f),new Vector3(.72f,3.5f,.12f),"Cream");Box(new Vector3(side*1.17f,1.5f,-.3f),new Vector3(.1f,2.8f,.1f),"RedGlow");for(int j=0;j<4;j++)Box(new Vector3(side*1.65f,2.8f-j*.65f,-.74f),new Vector3(.47f,.08f,.02f),"Ink",default,false);}
   Box(new Vector3(0,3.4f,.3f),new Vector3(2.45f,.5f,.5f),"Ink");for(int i=0;i<5;i++)Box(new Vector3(-.8f+i*.4f,3.4f,-.01f),new Vector3(.2f,.12f,.03f),i%2==0?"RedGlow":"Gold",default,false);
  }
  static Mesh RoundBox()
  {
   var v=new List<Vector3>();var n=new List<Vector3>();var t=new List<int>();float r=.07f;
   float[] c={-.5f,-.5f+r*.3f,-.5f+r,0,.5f-r,.5f-r*.3f,.5f};
   var axes=new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
   foreach(var normal in axes)
   {Vector3 u=normal==Vector3.up||normal==Vector3.down?Vector3.right:Vector3.Cross(Vector3.up,normal);Vector3 w=Vector3.Cross(normal,u);int start=v.Count;
    for(int y=0;y<c.Length;y++)for(int x=0;x<c.Length;x++){var p=normal*.5f+u*c[x]+w*c[y];var q=new Vector3(Mathf.Clamp(p.x,-.5f+r,.5f-r),Mathf.Clamp(p.y,-.5f+r,.5f-r),Mathf.Clamp(p.z,-.5f+r,.5f-r));var dir=(p-q).normalized;v.Add(q+dir*r);n.Add(dir);}
    for(int y=0;y<c.Length-1;y++)for(int x=0;x<c.Length-1;x++){int a=start+y*c.Length+x;t.AddRange(new[]{a,a+1,a+c.Length+1,a,a+c.Length+1,a+c.Length});}}
   var mesh=new Mesh{name="Workshop bevel box"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetTriangles(t,0);mesh.RecalculateBounds();return mesh;
  }
  static Mesh Ramp()
  {
   var p=new[]{new Vector3(-.95f,.04f,-2.8f),new Vector3(.95f,.04f,-2.8f),new Vector3(-.95f,.30f,-.8f),new Vector3(.95f,.30f,-.8f),new Vector3(-.95f,.015f,-2.8f),new Vector3(.95f,.015f,-2.8f),new Vector3(-.95f,.015f,-.8f),new Vector3(.95f,.015f,-.8f)};
   int[] faces={0,2,3,0,3,1,4,5,7,4,7,6,0,4,6,0,6,2,1,3,7,1,7,5,0,1,5,0,5,4,2,6,7,2,7,3};
   var v=faces.Select(i=>p[i]).ToArray();var mesh=new Mesh{name="Entrance ramp"};mesh.vertices=v;mesh.triangles=Enumerable.Range(0,v.Length).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
  }
  static Mesh Lathe(Vector2[] profile,int segments)
  {
   var v=new List<Vector3>();var t=new List<int>();
   for(int j=0;j<profile.Length;j++)for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;v.Add(new Vector3(Mathf.Sin(a)*profile[j].x,profile[j].y,Mathf.Cos(a)*profile[j].x));}
   for(int j=0;j<profile.Length-1;j++)for(int i=0;i<segments;i++){int a=j*(segments+1)+i,b=a+segments+1;t.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
   var mesh=new Mesh{name="Workshop lathe"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
  }
  static Mesh Torus(float major,float minor)
  {
   var v=new List<Vector3>();var t=new List<int>();const int aCount=24,bCount=8;
   for(int a=0;a<=aCount;a++)for(int b=0;b<=bCount;b++){float u=a*Mathf.PI*2/aCount,w=b*Mathf.PI*2/bCount;v.Add(new Vector3(Mathf.Cos(u)*(major+minor*Mathf.Cos(w)),minor*Mathf.Sin(w),Mathf.Sin(u)*(major+minor*Mathf.Cos(w))));}
   for(int a=0;a<aCount;a++)for(int b=0;b<bCount;b++){int i=a*(bCount+1)+b;t.AddRange(new[]{i,i+1,i+bCount+2,i,i+bCount+2,i+bCount+1});}
   var mesh=new Mesh{name="Workshop handle"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
  }
 }
}
