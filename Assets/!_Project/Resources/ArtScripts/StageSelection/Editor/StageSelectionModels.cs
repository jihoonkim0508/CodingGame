using System;
using System.Collections.Generic;
using System.Linq;
using CodingGame.Art.MapPainter.Editor;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace CodingGame.Art.StageSelection.Editor
{
    public static class StageSelectionModels
    {
        public const string Root="Assets/!_Project/Resources/Models/Stage01_Prop/StageSelection";
        public static readonly string[] Titles={"개발자의 작업실","데이터 분기 구역","자동화 생산 공장"};
        public static readonly string[] Names={"WorkshopIsland","DataBranchIsland","AutomationIsland"};
        public static readonly string[] AccentNames={"MintGlow","CyanGlow","AmberGlow"};
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        static readonly List<Mesh> temporary=new List<Mesh>();
        static Transform current;static List<GameObject> parts;static Mesh box,cube,cylinder,sphere;
        static string prefix;
        public static Color Hex(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var color);return color;}
        public static Material Material(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+name+".mat");
        public static void Bake()
        {
            foreach(string dir in new[]{Root,Root+"/Materials",Root+"/Meshes",Root+"/Prefabs"})MapPainterAssets.EnsureFolder(dir);
            Mat("Ink","192D40");Mat("Slate","3C566B");Mat("Rock","263E51");Mat("RockLight","345267");
            Mat("Cream","F1E5CA");Mat("White","E3F0F0");Mat("Wood","D9AC73",false,.6f);Mat("WoodEdge","A2734D",false,.5f);
            Mat("Blue","397EC5");Mat("Ice","84C4D9");Mat("IceFloor","688FAD");Mat("Orange","EAA048");Mat("OrangeDark","A86630");
            Mat("Steel","95A8AF");Mat("Belt","202F3A");Mat("Paper","ECDDAD");Mat("Coral","CC7767");Mat("Green","65955B");Mat("Leaf","99BB68");Mat("Coffee","4C382D");
            Mat("MintGlow","8DF1CB",true);Mat("CyanGlow","66DFFF",true);Mat("AmberGlow","FFC36B",true);Mat("VioletGlow","BDA2F1",true);Mat("WhiteGlow","DDFFF8",true);
            box=BeveledBox();cube=Primitive(PrimitiveType.Cube);cylinder=Primitive(PrimitiveType.Cylinder);sphere=LowSphere();
            temporary.Add(box);temporary.Add(sphere);
            try{for(int i=0;i<3;i++)Island(i);BridgePrefab();AssetDatabase.SaveAssets();}
            finally{foreach(var mesh in temporary)if(mesh&&!EditorUtility.IsPersistent(mesh))Object.DestroyImmediate(mesh);temporary.Clear();}
        }
        static void Mat(string name,string color,bool glow=false,float grain=0)
        {
            string path=Root+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=glow?Shader.Find("Universal Render Pipeline/Unlit"):grain>0?AssetDatabase.LoadAssetAtPath<Shader>("Assets/!_Project/Resources/Models/Stage01_Prop/Shaders/WorkshopSurface.shader"):Shader.Find("Universal Render Pipeline/Lit");
            if(!shader)throw new InvalidOperationException("Stage selection surface shader missing.");
            if(!material){material=new Material(shader){name=name};AssetDatabase.CreateAsset(material,path);}
            else material.shader=shader;
            material.SetColor("_BaseColor",Hex(color)*(glow?1.25f:1));
            if(!glow&&grain>0){material.SetFloat("_GrainStrength",grain);material.SetFloat("_LightSoftness",.72f);material.SetFloat("_ShadowStrength",.48f);material.SetFloat("_GradientStrength",.11f);material.SetFloat("_MapTopY",2);material.SetFloat("_MapHeight",4);}
            else if(!glow){material.SetFloat("_Metallic",name=="Steel"||name=="Slate"||name=="Ink"?.08f:0);material.SetFloat("_Smoothness",.3f);}
            materials[name]=material;EditorUtility.SetDirty(material);
        }
        static Mesh Primitive(PrimitiveType type){var go=GameObject.CreatePrimitive(type);var mesh=go.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(go);return mesh;}
        static void Part(Mesh mesh,Vector3 position,Vector3 size,string material,Vector3 rotation=default)
        {
            var go=new GameObject("Part");go.transform.SetParent(current,false);go.transform.localPosition=position;go.transform.localScale=size;go.transform.localRotation=Quaternion.Euler(rotation);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=materials[material];parts.Add(go);
        }
        static void Box(Vector3 p,Vector3 s,string m,Vector3 r=default,bool bevel=true)=>Part(bevel?box:cube,p,s,m,r);
        static void Ball(Vector3 p,Vector3 s,string m)=>Part(sphere,p,s,m);
        static void Cyl(Vector3 p,float radius,float height,string m,Vector3 r=default)=>Part(cylinder,p,new Vector3(radius*2,height*.5f,radius*2),m,r);
        static void Beam(Vector3 a,Vector3 b,float width,string m)=>Box((a+b)*.5f,new Vector3(width,Vector3.Distance(a,b),width),m,Quaternion.FromToRotation(Vector3.up,b-a).eulerAngles);
        static void Group(string name,Action build,Vector3 p=default)
        {
            var previous=current;var oldParts=parts;var go=new GameObject(name);go.transform.SetParent(previous,false);go.transform.localPosition=p;
            current=go.transform;parts=new List<GameObject>();build();Combine(prefix+"_"+name);current=previous;parts=oldParts;
        }
        static void Combine(string name)
        {
            foreach(var group in parts.GroupBy(p=>p.GetComponent<MeshRenderer>().sharedMaterial))
            {
                var mesh=new Mesh{name=name+"_"+group.Key.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                mesh.CombineMeshes(group.Select(p=>new CombineInstance{mesh=p.GetComponent<MeshFilter>().sharedMesh,transform=current.worldToLocalMatrix*p.transform.localToWorldMatrix}).ToArray());
                string path=Root+"/Meshes/"+mesh.name+".asset";MapPainterAssets.SaveMesh(mesh,path);
                var go=new GameObject(group.Key.name);go.transform.SetParent(current,false);go.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);go.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
            foreach(var go in parts)Object.DestroyImmediate(go);parts.Clear();
        }
        static void Island(int index)
        {
            prefix="Stage"+(index+1);var root=new GameObject("Stage");
            try
            {
                var ring=Ring(2.88f,2.53f,.055f);ring.name=prefix+"_SelectionRing";string ringPath=Root+"/Meshes/"+ring.name+".asset";MapPainterAssets.SaveMesh(ring,ringPath);
                root.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(ringPath);
                var status=root.AddComponent<MeshRenderer>();status.sharedMaterial=materials[AccentNames[index]];
                current=root.transform;parts=new List<GameObject>();
                Group("IslandVisual",()=>
                {
                    Group("FloatingFoundation",()=>Foundation(index));
                    Group("Theme",()=>{if(index==0)Workshop();else if(index==1)Data();else Factory();});
                    Group("LandingPorts",()=>
                    {
                        foreach(float side in new[]{-1f,1f})
                        {
                            Box(new Vector3(side*2.56f,.16f,-1.15f),new Vector3(.62f,.24f,.95f),"Ink");
                            Box(new Vector3(side*2.56f,.292f,-1.15f),new Vector3(.58f,.025f,.76f),index==0?"Cream":index==1?"Ice":"Steel");
                            foreach(float z in new[]{-.27f,.27f})Box(new Vector3(side*2.56f,.32f,-1.15f+z),new Vector3(.43f,.03f,.035f),AccentNames[index],default,false);
                        }
                    });
                });
                var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,.7f,0);collider.size=new Vector3(5.4f,4.5f,4.7f);
                var view=root.AddComponent<StageIslandView>();view.stageNumber=index+1;view.stageTitle=Titles[index];view.accent=Hex(index==0?"8DF1CB":index==1?"66DFFF":"FFC36B");
                view.visualRoot=root.transform.Find("IslandVisual");view.statusRing=status;view.rotors=root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Rotor_")).ToArray();view.RefreshState(index==0?StageMap.StageState.Available:StageMap.StageState.Locked);
                PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+Names[index]+".prefab");
            }
            finally{Object.DestroyImmediate(root);}
        }
        static void Foundation(int index)
        {
            var outline=Outline(2.7f,2.35f,.5f);
            var levels=new[]{new Vector3(1,.02f,1),new Vector3(1.025f,-.32f,1.025f),new Vector3(.82f,-1.15f,.80f),new Vector3(.58f,-1.58f,.56f)};
            for(int l=0;l<levels.Length-1;l++)for(int i=0;i<outline.Length;i++)
            {
                var a=outline[i];var b=outline[(i+1)%outline.Length];Vector3 P(Vector2 q,Vector3 level)=>new Vector3(q.x*level.x,level.y,q.y*level.z);
                var mesh=Quad(P(a,levels[l]),P(b,levels[l]),P(b,levels[l+1]),P(a,levels[l+1]));temporary.Add(mesh);Part(mesh,Vector3.zero,Vector3.one,(i+l)%3==0?"RockLight":"Rock");
            }
            foreach(bool bottom in new[]{false,true})
            {
                var level=bottom?levels[levels.Length-1]:levels[0];var vertices=outline.Select(p=>new Vector3(p.x*level.x,level.y,p.y*level.z)).ToArray();var indices=new List<int>();
                for(int i=1;i<outline.Length-1;i++)indices.AddRange(bottom?new[]{0,i,i+1}:new[]{0,i+1,i});
                var cap=new Mesh();cap.vertices=vertices;cap.triangles=indices.ToArray();cap.RecalculateNormals();temporary.Add(cap);Part(cap,Vector3.zero,Vector3.one,"Rock");
            }
            Box(new Vector3(0,.09f,0),new Vector3(5.32f,.23f,4.62f),index==0?"WoodEdge":index==1?"Slate":"OrangeDark");
            Box(new Vector3(0,.205f,0),new Vector3(5.2f,.12f,4.5f),index==0?"Wood":index==1?"IceFloor":"Steel");
            for(int i=0;i<4;i++)
            {
                float x=i%2==0?-2.54f:2.54f,z=i<2?-1.74f:1.74f;
                Cyl(new Vector3(x,-.6f,z),.24f,.23f,"Ink");Cyl(new Vector3(x,-.735f,z),.18f,.035f,AccentNames[index]);
                var plume=Frustum(.06f,.14f,.33f);temporary.Add(plume);Part(plume,new Vector3(x,-.91f,z),Vector3.one,AccentNames[index]);
            }
            Box(new Vector3(-2.28f,-1.35f,.9f),Vector3.one*.35f,"RockLight",new Vector3(18,30,22));
            Box(new Vector3(1.7f,-1.63f,-.25f),new Vector3(.28f,.4f,.31f),"Rock",new Vector3(13,48,-17));
            string pathMat=index==0?"Cream":index==1?"Ice":"Belt";
            Box(new Vector3(0,.285f,-1.15f),new Vector3(5.12f,.035f,.67f),pathMat);
            for(int i=-2;i<=2;i++)Arrow(new Vector3(i*.9f,.31f,-1.15f),.16f,index==1?"WhiteGlow":index==0?"WoodEdge":"AmberGlow");
        }
        static void Workshop()
        {
            Box(new Vector3(-.15f,.38f,1.0f),new Vector3(1.02f,.2f,.65f),"Ink");Box(new Vector3(-.15f,1.03f,1.04f),new Vector3(.22f,1.2f,.24f),"Slate");
            Box(new Vector3(-.15f,1.94f,1.08f),new Vector3(2.75f,1.55f,.22f),"Ink");
            Box(new Vector3(-.15f,1.94f,.943f),new Vector3(2.58f,1.39f,.045f),"Blue");Box(new Vector3(-.15f,1.94f,.91f),new Vector3(2.43f,1.25f,.025f),"Ink");
            for(int row=0;row<9;row++)for(int seg=0;seg<3;seg++)
            {
                float length=.16f+((row*7+seg*3)%4)*.08f;
                Box(new Vector3(-1.03f+seg*.48f+(row%3)*.035f,2.37f-row*.105f,.885f),new Vector3(length,.027f,.014f),new[]{"Ice","MintGlow","AmberGlow","Coral"}[(row+seg)%4],default,false);
            }
            Glyph("</>",new Vector3(.52f,2.04f,.881f),.035f,"CyanGlow");
            Box(new Vector3(-.1f,.345f,.02f),new Vector3(1.9f,.15f,.75f),"Cream");
            for(int row=0;row<3;row++)for(int col=0;col<9;col++)Box(new Vector3(-.87f+col*.19f,.445f,-.2f+row*.21f),new Vector3(.145f,.08f,.15f),col==0&&row==2?"Green":"White");
            Box(new Vector3(.06f,.447f,-.35f),new Vector3(.72f,.08f,.12f),"White");
            Box(new Vector3(1.25f,.29f,-.08f),new Vector3(.6f,.035f,.75f),"Ink");Ball(new Vector3(1.25f,.38f,-.08f),new Vector3(.29f,.18f,.43f),"Cream");
            Mug(new Vector3(-1.62f,.3f,-.15f));Books(new Vector3(2.08f,.3f,-.32f));Plant(new Vector3(1.86f,.3f,1.48f));
            Cyl(new Vector3(-2.05f,.34f,1.05f),.28f,.11f,"Ink");Beam(new Vector3(-2.05f,.4f,1.05f),new Vector3(-2.11f,1.6f,1.05f),.065f,"Orange");Beam(new Vector3(-2.11f,1.6f,1.05f),new Vector3(-1.68f,1.96f,.9f),.075f,"Cream");
            var shade=Frustum(.24f,.12f,.32f);temporary.Add(shade);Part(shade,new Vector3(-1.68f,1.86f,.9f),Vector3.one,"Orange");Cyl(new Vector3(-1.68f,1.69f,.9f),.2f,.025f,"AmberGlow");
            Box(new Vector3(1.91f,.32f,-1.8f),new Vector3(.65f,.025f,.44f),"Paper",new Vector3(0,12,0));
            Box(new Vector3(-.6f,.3f,1.96f),new Vector3(.85f,.05f,.28f),"Coral",new Vector3(0,-8,0));
        }
        static void Mug(Vector3 p)
        {
            Cyl(p+Vector3.up*.21f,.19f,.39f,"Cream");Cyl(p+Vector3.up*.413f,.164f,.012f,"Coffee");
            var handle=Torus(.11f,.035f);temporary.Add(handle);Part(handle,p+new Vector3(.22f,.22f,0),Vector3.one,"Cream",new Vector3(90,0,0));
        }
        static void Books(Vector3 p)
        {
            for(int i=0;i<3;i++)
            {
                var q=p+Vector3.up*(.1f+i*.17f);string m=new[]{"Coral","Blue","Green"}[i];
                Box(q,new Vector3(.84f,.13f,.61f),"Paper");Box(q+Vector3.up*.075f,new Vector3(.9f,.035f,.65f),m);Box(q-Vector3.up*.075f,new Vector3(.9f,.035f,.65f),m);
            }
        }
        static void Plant(Vector3 p)
        {
            var pot=Frustum(.23f,.32f,.45f);temporary.Add(pot);Part(pot,p+Vector3.up*.225f,Vector3.one,"OrangeDark");Cyl(p+Vector3.up*.45f,.27f,.045f,"Coffee");
            for(int i=0;i<7;i++){float angle=i*137.5f*Mathf.Deg2Rad;Ball(p+new Vector3(Mathf.Sin(angle)*.17f,.65f+i%3*.1f,Mathf.Cos(angle)*.17f),new Vector3(.24f,.5f,.24f),i%2==0?"Leaf":"Green");}
        }
        static void Data()
        {
            Cyl(new Vector3(0,.39f,.8f),.81f,.25f,"Ink");Cyl(new Vector3(0,.55f,.8f),.72f,.10f,"Ice");Cyl(new Vector3(0,.63f,.8f),.67f,.055f,"CyanGlow");
            foreach(float x in new[]{-.42f,.42f})Cyl(new Vector3(x,1.25f,.8f),.055f,1.2f,"Ice");
            Cyl(new Vector3(0,1.3f,.8f),.17f,1.35f,"CyanGlow");Cyl(new Vector3(0,1.97f,.8f),.56f,.10f,"Ink");Cyl(new Vector3(0,2.04f,.8f),.59f,.04f,"CyanGlow");
            Group("Rotor_Hub",()=>
            {
                Box(new Vector3(0,.15f,0),Vector3.one*.55f,"Blue",new Vector3(0,25,0));
                var ring=Torus(.48f,.035f);temporary.Add(ring);Part(ring,Vector3.zero,Vector3.one,"WhiteGlow");
                Box(new Vector3(.45f,.55f,.1f),Vector3.one*.2f,"Ice",new Vector3(20,30,10));Box(new Vector3(-.4f,.33f,-.24f),Vector3.one*.15f,"CyanGlow",new Vector3(12,45,15));
            },new Vector3(0,2.3f,.8f));
            for(int i=0;i<3;i++)
            {
                float x=(i-1)*1.55f;string glow=new[]{"CyanGlow","VioletGlow","MintGlow"}[i];
                Beam(new Vector3(0,.3f,.6f),new Vector3(x,.3f,-.32f),.085f,glow);
                Box(new Vector3(x,.38f,-.56f),new Vector3(.74f,.16f,.55f),"Slate");Box(new Vector3(x,.69f,-.54f),new Vector3(.1f,.56f,.12f),"Ice");
                Box(new Vector3(x,1.13f,-.55f),new Vector3(1.24f,.69f,.14f),glow);
                Box(new Vector3(x,1.13f,-.637f),new Vector3(1.13f,.58f,.035f),"Ink");
                Glyph(new[]{"IF","ELIF","ELSE"}[i],new Vector3(x,1.14f,-.665f),.038f,glow);
            }
            foreach(float side in new[]{-1f,1f})
            {
                Box(new Vector3(side*1.96f,.9f,1.32f),new Vector3(.63f,1.28f,.67f),"Slate");Box(new Vector3(side*1.96f,.9f,.966f),new Vector3(.53f,1.12f,.025f),"Ink");
                for(int j=0;j<4;j++){Box(new Vector3(side*1.96f,1.3f-j*.24f,.943f),new Vector3(.35f,.055f,.02f),"Ice",default,false);Box(new Vector3(side*1.78f,1.3f-j*.24f,.93f),new Vector3(.045f,.055f,.025f),"CyanGlow",default,false);}
            }
            for(int i=0;i<3;i++)Box(new Vector3(-1.7f+i*.45f,1.7f+(i%2)*.22f,1.1f),Vector3.one*.16f,i%2==0?"CyanGlow":"VioletGlow",new Vector3(23,i*40,15));
        }
        static void Factory()
        {
            Box(new Vector3(0,.42f,-.42f),new Vector3(4.25f,.28f,.97f),"OrangeDark");Box(new Vector3(0,.59f,-.42f),new Vector3(4.16f,.12f,.84f),"Belt");
            for(int i=0;i<15;i++)Cyl(new Vector3(-1.96f+i*.28f,.66f,-.42f),.055f,.73f,"Slate",new Vector3(90,0,0));
            foreach(float z in new[]{-.91f,.07f})Box(new Vector3(0,.67f,z),new Vector3(4.3f,.14f,.08f),"Orange");
            for(int i=0;i<3;i++)
            {
                float x=-1.35f+i*1.35f;Box(new Vector3(x,.92f,-.42f),new Vector3(.54f,.5f,.54f),i==1?"Orange":"Steel");Box(new Vector3(x,1.19f,-.42f),new Vector3(.10f,.035f,.53f),"AmberGlow",default,false);
            }
            foreach(float side in new[]{-1f,1f})
            {
                float x=side*1.5f;Cyl(new Vector3(x,.42f,1.13f),.4f,.3f,"Ink");Cyl(new Vector3(x,.64f,1.13f),.32f,.18f,"Orange");
                var a=new Vector3(x,.75f,1.13f);var b=new Vector3(side*.95f,2.08f,1.0f);var c=new Vector3(side*.36f,1.55f,.24f);
                Beam(a,b,.28f,"Orange");Beam(b,c,.24f,"Orange");
                foreach(var p in new[]{a,b,c})Cyl(p,.19f,.34f,"Ink",new Vector3(0,0,90));
                Box(c-Vector3.up*.22f,new Vector3(.22f,.24f,.2f),"Steel");foreach(float offset in new[]{-.13f,.13f})Box(c+new Vector3(offset,-.38f,0),new Vector3(.055f,.27f,.08f),"Ink");
            }
            Box(new Vector3(0,.95f,1.72f),new Vector3(1.18f,1.4f,.71f),"Slate");Box(new Vector3(0,1.72f,1.72f),new Vector3(1.28f,.2f,.78f),"Orange");
            Box(new Vector3(0,1.19f,1.33f),new Vector3(.91f,.52f,.055f),"Ink");Glyph("FOR",new Vector3(0,1.2f,1.29f),.042f,"AmberGlow");
            foreach(float x in new[]{-.38f,.38f}){Cyl(new Vector3(x,2.05f,1.76f),.14f,.48f,"Steel");Cyl(new Vector3(x,2.32f,1.76f),.18f,.08f,"Ink");}
            Box(new Vector3(2.0f,.77f,.91f),new Vector3(.57f,.97f,.65f),"Orange");Box(new Vector3(2,.93f,.563f),new Vector3(.38f,.4f,.025f),"Ink");
            for(int i=0;i<3;i++)Ball(new Vector3(1.88f+i*.12f,.6f,.55f),Vector3.one*.075f,i==0?"MintGlow":"AmberGlow");
            // Keep the complete rotated stripe footprint on top of the walkway.
            // Its top is y=.3025 and its z interval is [-1.485, -.815].
            foreach(float x in new[]{-2.12f,2.12f})for(int i=0;i<7;i++)Box(new Vector3(x,.314f,-1.39f+i*.08f),new Vector3(.22f,.012f,.055f),i%2==0?"Ink":"Orange",new Vector3(0,25,0),false);
            Box(new Vector3(-1.68f,.46f,1.92f),new Vector3(.65f,.38f,.5f),"OrangeDark");
        }
        static void Arrow(Vector3 p,float s,string m)
        {
            var mesh=new Mesh{name="Direction arrow"};mesh.vertices=new[]{new Vector3(-s,0,-s*.6f),new Vector3(s,0,0),new Vector3(-s,0,s*.6f)};mesh.triangles=new[]{0,2,1};mesh.RecalculateNormals();temporary.Add(mesh);Part(mesh,p,Vector3.one,m);
        }
        static readonly Dictionary<char,string[]> letters=new Dictionary<char,string[]>
        {
            ['I']=new[]{"111","010","010","010","111"},['F']=new[]{"111","100","110","100","100"},['E']=new[]{"111","100","110","100","111"},
            ['L']=new[]{"100","100","100","100","111"},['S']=new[]{"111","100","111","001","111"},['O']=new[]{"111","101","101","101","111"},['R']=new[]{"110","101","110","101","101"},
            ['<']=new[]{"001","010","100","010","001"},['>']=new[]{"100","010","001","010","100"},['/']=new[]{"001","001","010","100","100"}
        };
        static void Glyph(string text,Vector3 center,float pixel,string material)
        {
            float width=(text.Length*4-1)*pixel;
            for(int letter=0;letter<text.Length;letter++)if(letters.TryGetValue(text[letter],out var rows))
                for(int y=0;y<5;y++)for(int x=0;x<3;x++)if(rows[y][x]=='1')Box(center+new Vector3(-width*.5f+(letter*4+x+.5f)*pixel,(2-y)*pixel,0),new Vector3(pixel*.82f,pixel*.82f,.012f),material,default,false);
        }
        static void BridgePrefab()
        {
            prefix="Bridge";var root=new GameObject("Circuit Bridge");current=root.transform;parts=new List<GameObject>();
            try
            {
                // A normalized bridge; the scene wrapper stretches only its length.
                Box(new Vector3(0,.12f,0),new Vector3(1,.16f,.72f),"Ink");Box(new Vector3(0,.215f,0),new Vector3(1,.035f,.59f),"Slate");
                foreach(float z in new[]{-.35f,.35f})Box(new Vector3(0,.245f,z),new Vector3(1,.04f,.035f),"CyanGlow",default,false);
                Combine("CircuitBridge");PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/CircuitBridge.prefab");
            }
            finally{Object.DestroyImmediate(root);}
        }
        static Vector2[] Outline(float x,float z,float cut)=>new[]{new Vector2(-x+cut,-z),new Vector2(x-cut,-z),new Vector2(x,-z+cut),new Vector2(x,z-cut),new Vector2(x-cut,z),new Vector2(-x+cut,z),new Vector2(-x,z-cut),new Vector2(-x,-z+cut)};
        static Mesh Ring(float x,float z,float thickness)
        {
            var outer=Outline(x,z,.48f);var inner=Outline(x-thickness,z-thickness,.45f);var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<8;i++){v.Add(new Vector3(outer[i].x,-.04f,outer[i].y));v.Add(new Vector3(inner[i].x,-.04f,inner[i].y));}
            for(int i=0;i<8;i++){int a=i*2,b=(i+1)%8*2;t.AddRange(new[]{a,b+1,b,a,a+1,b+1});}
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {var mesh=new Mesh();mesh.vertices=new[]{a,b,c,d};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        static Mesh BeveledBox()
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var t=new List<int>();const float radius=.065f;float[] coordinates={-.5f,-.5f+radius,.5f-radius,.5f};
            foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
            {
                var u=normal==Vector3.up||normal==Vector3.down?Vector3.right:Vector3.Cross(Vector3.up,normal);var w=Vector3.Cross(normal,u);int start=v.Count;
                foreach(float y in coordinates)foreach(float x in coordinates)
                {var p=normal*.5f+u*x+w*y;var q=new Vector3(Mathf.Clamp(p.x,-.5f+radius,.5f-radius),Mathf.Clamp(p.y,-.5f+radius,.5f-radius),Mathf.Clamp(p.z,-.5f+radius,.5f-radius));var direction=(p-q).normalized;v.Add(q+direction*radius);n.Add(direction);}
                for(int y=0;y<3;y++)for(int x=0;x<3;x++){int a=start+y*4+x;t.AddRange(new[]{a,a+1,a+5,a,a+5,a+4});}
            }
            var mesh=new Mesh{name="Chamfered box"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetTriangles(t,0);mesh.RecalculateBounds();return mesh;
        }
        static Mesh LowSphere()
        {
            var v=new List<Vector3>();var t=new List<int>();const int segments=12,rings=6;
            for(int j=0;j<=rings;j++)for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments,b=j*Mathf.PI/rings;v.Add(new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a))*.5f);}
            for(int j=0;j<rings;j++)for(int i=0;i<segments;i++){int a=j*(segments+1)+i;t.AddRange(new[]{a,a+1,a+segments+1,a+1,a+segments+2,a+segments+1});}
            var mesh=new Mesh{name="Low-poly foliage"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetNormals(v.Select(p=>p.normalized).ToList());return mesh;
        }
        static Mesh Frustum(float bottom,float top,float height)
        {
            var v=new List<Vector3>();var t=new List<int>();const int segments=16;
            for(int j=0;j<2;j++)for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments,r=j==0?bottom:top;v.Add(new Vector3(Mathf.Sin(a)*r,(j-.5f)*height,Mathf.Cos(a)*r));}
            for(int i=0;i<segments;i++)t.AddRange(new[]{i,i+1,i+segments+1,i+1,i+segments+2,i+segments+1});
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();return mesh;
        }
        static Mesh Torus(float major,float minor)
        {
            var v=new List<Vector3>();var t=new List<int>();const int segments=20,sides=6;
            for(int a=0;a<=segments;a++)for(int b=0;b<=sides;b++){float u=a*Mathf.PI*2/segments,w=b*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(u)*(major+minor*Mathf.Cos(w)),minor*Mathf.Sin(w),Mathf.Sin(u)*(major+minor*Mathf.Cos(w))));}
            for(int a=0;a<segments;a++)for(int b=0;b<sides;b++){int i=a*(sides+1)+b;t.AddRange(new[]{i,i+1,i+sides+2,i,i+sides+2,i+sides+1});}
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();return mesh;
        }
    }
}
