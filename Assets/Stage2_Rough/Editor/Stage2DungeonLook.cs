using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace HiddenPath.Stage2Rough.Editor
{
    // Adds primitive architectural skins without moving puzzle surfaces or changing their collision.
    public static class Stage2DungeonLook
    {
        const string Target="Assets/01_Scenes/MapDesign/MapDesign_Stage2.unity";
        static Transform root,art,ceilings;
        static Material[] stones;
        static Material mortar,edge,iron,wood,flame,ember,red,blue,gold,violet;
        static System.Random rng;
        static Transform G(string n,Transform p){var g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
        static Material M(string n,Color c,float glow=0)
        {
            string path="Assets/Stage2_Rough/Materials/Look_"+n+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.color=c;m.SetFloat("_Smoothness",.18f);
            if(glow>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*glow);}
            EditorUtility.SetDirty(m);return m;
        }
        static GameObject P(string n,PrimitiveType kind,Vector3 pos,Vector3 size,Material mat,Transform parent)
        {
            var g=GameObject.CreatePrimitive(kind);g.name=n;g.transform.SetParent(parent,false);g.transform.position=pos;g.transform.localScale=size;
            g.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        static GameObject B(string n,Vector3 p,Vector3 s,Material m,Transform t){return P(n,PrimitiveType.Cube,p,s,m,t);}
        static Material Stone(){return stones[rng.Next(stones.Length)];}
        static void Beam(string n,Vector3 a,Vector3 b,float w,Material m,Transform t)
        {var g=B(n,(a+b)/2,new Vector3(w,w,(b-a).magnitude),m,t);g.transform.rotation=Quaternion.LookRotation(b-a);}
        static void Point(Vector3 p,Color c,float range,float power,Transform t)
        {var g=G("Local_Light",t);g.position=p;var l=g.gameObject.AddComponent<Light>();l.type=LightType.Point;l.range=range;l.intensity=power;l.color=c;l.shadows=LightShadows.None;}
        static void Torch(Vector3 p,Transform t)
        {
            var g=G("Wall_Brazier",t);
            B("Stone_Plinth",p+Vector3.down*.75f,new Vector3(.7f,1.2f,.7f),Stone(),g);
            P("Iron_Bowl",PrimitiveType.Cylinder,p,new Vector3(.8f,.15f,.8f),iron,g);
            var f=P("Flame_Amber",PrimitiveType.Sphere,p+Vector3.up*.4f,new Vector3(.42f,.9f,.42f),flame,g);f.transform.Rotate(0,0,12);
            P("Flame_Core",PrimitiveType.Sphere,p+Vector3.up*.27f,new Vector3(.26f,.52f,.26f),ember,g);
            Point(p+Vector3.up*.5f,new Color(1,.49f,.16f),12,7,g);
        }
        static void Pillar(Vector3 p,float h,Transform t)
        {
            var g=G("Masonry_Pier",t);B("Foot",p+Vector3.up*.25f,new Vector3(1.6f,.5f,1.6f),edge,g);
            for(float y=.5f;y<h-.5f;y+=1.1f)B("Shaft_Block",p+Vector3.up*(y+.53f),new Vector3(1.1f,1.06f,1.1f),Stone(),g);
            B("Capital",p+Vector3.up*(h-.25f),new Vector3(1.7f,.5f,1.7f),edge,g);
        }
        static void Arch(Vector3 p,float w,float spring,Transform t)
        {
            var g=G("Stone_Arch",t);
            Pillar(p+Vector3.left*(w/2+.6f),spring,g);Pillar(p+Vector3.right*(w/2+.6f),spring,g);
            for(int i=0;i<11;i++)
            {
                float angle=(i+.5f)*Mathf.PI/11;
                var b=B("Voussoir",p+new Vector3(Mathf.Cos(angle)*(w/2+.55f),spring+Mathf.Sin(angle)*(w/2+.55f),0),new Vector3(w*.16f,.9f,1.3f),i==5?edge:Stone(),g);
                b.transform.rotation=Quaternion.Euler(0,0,angle*Mathf.Rad2Deg+90);
            }
        }
        static void SkinFloor(Transform src,Transform parent)
        {
            var s=src.localScale;if(s.y>1.3f||s.x<4||s.z<4)return;
            if(src.GetComponent<MeshFilter>()==null||src.GetComponent<MeshFilter>().sharedMesh.name!="Cube")return;
            var g=G("Flagstones_"+src.name,parent);int nx=Mathf.Max(1,Mathf.CeilToInt(s.x/2.8f)),nz=Mathf.Max(1,Mathf.CeilToInt(s.z/2.8f));
            for(int x=0;x<nx;x++)for(int z=0;z<nz;z++)
            {
                var local=new Vector3((x+.5f)/nx-.5f,.5f,(z+.5f)/nz-.5f);
                var b=B("Slab",src.TransformPoint(local)+src.up*.018f,new Vector3(s.x/nx-.07f,.035f,s.z/nz-.07f),Stone(),g);
                b.transform.rotation=src.rotation;
            }
        }
        static void SkinWall(Transform src,Transform t)
        {
            var s=src.localScale;bool alongZ=s.z>s.x;float length=alongZ?s.z:s.x;
            if(length<5||s.y<3)return;
            float h=Mathf.Min(5,s.y),top=src.position.y+s.y*.5f;
            int cols=Mathf.CeilToInt(length/2.6f),rows=Mathf.CeilToInt(h/1.25f);
            var g=G("Masonry_"+src.name,t);
            for(int side=-1;side<=1;side+=2)for(int r=0;r<rows;r++)for(int c=0;c<cols;c++)
            {
                float offset=-length/2+(c+.5f)*length/cols;
                Vector3 local=new Vector3(0,s.y/2-(r+.5f)*h/rows,0);
                if(alongZ){local.z=offset;local.x=side*(s.x/2+.045f);}else{local.x=offset;local.z=side*(s.z/2+.045f);}
                var brick=B("Ashlar",src.position+src.rotation*local,alongZ?new Vector3(.09f,h/rows-.06f,length/cols-.06f):new Vector3(length/cols-.06f,h/rows-.06f,.09f),Stone(),g);brick.transform.rotation=src.rotation;
            }
        }
        static void Barrel(Vector3 p,Transform t)
        {
            P("Barrel",PrimitiveType.Cylinder,p+Vector3.up*.7f,new Vector3(1.05f,.7f,1.05f),wood,t);
            foreach(float y in new[]{.18f,1.18f})P("Hoop",PrimitiveType.Cylinder,p+Vector3.up*y,new Vector3(1.1f,.055f,1.1f),iron,t);
        }
        static void Banner(Vector3 p,Material m,Transform t)
        {
            B("Banner",p,new Vector3(1.2f,2.8f,.1f),m,t);
            Beam("Banner_Rod",p+new Vector3(-.8f,1.5f,0),p+new Vector3(.8f,1.5f,0),.1f,iron,t);
            var rune=B("Diamond_Emblem",p+new Vector3(0,0,-.065f),new Vector3(.5f,.5f,.035f),gold,t);rune.transform.Rotate(0,0,45);
        }
        static void Ceiling(Vector3 center,Vector3 size,Transform t)
        {B("Ceiling_Vault_Blockout",center,size,mortar,t);}
        [MenuItem("Tools/Hidden Path/Stage 02 Rough/Add dungeon spatial pass")]
        public static void Build()
        {
            var scene=SceneManager.GetActiveScene();if(scene.path!=Target||Application.isPlaying)throw new InvalidOperationException("Wrong scene or Play Mode");
            root=GameObject.Find("Stage02_Rough_20261127").transform;
            if(root.Find("Dungeon_Spatial_Pass"))throw new InvalidOperationException("Detail pass already exists; preserve manual edits.");
            rng=new System.Random(2711);stones=new Material[4];
            for(int i=0;i<4;i++)stones[i]=M("Stone"+i,new Color(.29f+i*.035f,.28f+i*.035f,.34f+i*.035f));
            mortar=M("Mortar",new Color(.14f,.13f,.18f));edge=M("Sandstone",new Color(.48f,.43f,.39f));iron=M("Iron",new Color(.10f,.12f,.16f));wood=M("Wood",new Color(.29f,.16f,.075f));
            flame=M("Fire",new Color(1,.35f,.015f),3);ember=M("FireCore",new Color(1,.75f,.12f),4);
            red=M("RedRune",new Color(.85f,.07f,.09f),1);blue=M("BlueRune",new Color(.035f,.42f,.95f),1);gold=M("GoldRune",new Color(1,.57f,.11f),.8f);violet=M("Crystal",new Color(.65f,.16f,1),2);
            Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Stage2 dungeon spatial pass");
            var existing=root.GetComponentsInChildren<Transform>(false);
            art=G("Dungeon_Spatial_Pass",root);Undo.RegisterCreatedObjectUndo(art.gameObject,"Dungeon spatial pass");
            ceilings=G("Ceilings_HideForTopView",art);
            foreach(var tm in root.GetComponentsInChildren<TextMesh>(true)){Undo.RecordObject(tm.gameObject,"Hide authoring text");tm.gameObject.SetActive(false);}
            foreach(var tr in existing)
            {
                if(tr.name=="Editor_ExampleRoutes_HIDE_FOR_PUZZLE"||tr.name.Contains("_Travel")||tr.name=="Planned_Swing_Range"||tr.name=="Downhill_Motion"||tr.name.StartsWith("Collapse_Sequence"))
                {Undo.RecordObject(tr.gameObject,"Hide authoring guides");tr.gameObject.SetActive(false);}
            }
            var floors=G("Floor_Masonry",art);var walls=G("Wall_Masonry",art);
            foreach(var tr in existing)
            {
                var renderer=tr.GetComponent<MeshRenderer>();if(!renderer)continue;
                if(renderer.sharedMaterial&&renderer.sharedMaterial.name=="Stone")SkinFloor(tr,floors);
                if(renderer.sharedMaterial&&renderer.sharedMaterial.name=="Walls")SkinWall(tr,walls);
            }
            Z0Z1();Z2();Z3();Z4Z5();Z6();Z7();Z8();
            var l=scene.GetRootGameObjects().Select(g=>g.GetComponent<Light>()).FirstOrDefault(x=>x&&x.type==LightType.Directional);
            if(l){Undo.RecordObject(l,"Dungeon light");l.intensity=.45f;l.color=new Color(.68f,.72f,1);}
            RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.25f,.23f,.31f);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(0,2,28),Quaternion.Euler(9,0,0),14);
            Selection.activeGameObject=art.gameObject;
        }
        static void Z0Z1()
        {
            var t=G("Z0_Z1_Enclosed_GlassHall",art);
            foreach(float z in new[]{1,11,19,31,43,49})foreach(int s in new[]{-1,1})Torch(new Vector3(s*(z<14?7.5f:10.5f),2,z),t);
            foreach(float z in new[]{14,51})Arch(new Vector3(0,0,z),5,4,t);
            for(int i=0;i<4;i++)foreach(int s in new[]{-1,1})Pillar(new Vector3(s*11,0,20+i*9),8,t);
            Banner(new Vector3(-7,5,50),blue,t);Banner(new Vector3(7,5,50),red,t);
            // A back wall with a real doorway frames the goal, unlike a floating lintel.
            foreach(int s in new[]{-1,1})B("Exit_StoneWall",new Vector3(s*8,4,51),new Vector3(9,8,1.4f),mortar,t);
            Ceiling(new Vector3(0,10,32),new Vector3(25,1,39),ceilings);Ceiling(new Vector3(0,9,5),new Vector3(19,1,18),ceilings);
            for(int i=0;i<7;i++)foreach(int s in new[]{-1,1})
            {
                float z=22+i*3.6f;B("Bridge_Frame_Post",new Vector3(s*9.5f,.6f,z),new Vector3(.28f,1.2f,.28f),iron,t);
                if(i<6)for(int k=0;k<4;k++)
                {float zz=z+(k+.5f)*.9f;Beam("Hanging_Chain",new Vector3(s*9.5f,1-.25f*Mathf.Sin(k*Mathf.PI/3),zz-.45f),new Vector3(s*9.5f,1-.25f*Mathf.Sin((k+1)*Mathf.PI/3),zz+.45f),.065f,iron,t);}
            }
            var console=root.Find("Z1_GlassMemory_5x6/Observation_Console");
            var eye=P("Observation_Lens",PrimitiveType.Sphere,console.position+new Vector3(0,.76f,0),new Vector3(.7f,.12f,.45f),gold,t);
            P("Eye_Pupil",PrimitiveType.Sphere,eye.transform.position+Vector3.up*.08f,new Vector3(.18f,.07f,.18f),iron,t);
            Barrel(new Vector3(-8,0,16),t);Barrel(new Vector3(9,0,48),t);
        }
        static void Z2()
        {
            var t=G("Z2_Chasm_Foundations",art);
            foreach(var p in root.Find("Z2_PartnerControlledPlatforms").GetComponentsInChildren<Transform>().Where(x=>x.name.StartsWith("Platform_")))
            {
                float bottom=-6;float height=p.position.y-bottom;
                B("Deep_Stone_Pier",new Vector3(p.position.x,bottom+height/2-.3f,p.position.z),new Vector3(2.8f,height,2.8f),Stone(),t);
                B("Coping",p.position-Vector3.up*.38f,new Vector3(3.8f,.25f,3.8f),edge,t);
                foreach(int s in new[]{-1,1})B("Magic_Edge",p.position+new Vector3(s*1.68f,.27f,0),new Vector3(.08f,.08f,3.3f),p.position.x<0?red:blue,t);
            }
            foreach(float z in new[]{63,76,90,99})foreach(int s in new[]{-1,1})Torch(new Vector3(s*15.3f,7,z),t);
            foreach(int s in new[]{-1,1})for(int i=0;i<5;i++)
            {var c=P("Cavern_Crystal",PrimitiveType.Cube,new Vector3(s*16,3+i%2,68+i*6),new Vector3(.7f,2,.7f),blue,t);c.transform.Rotate(20,20,25);}
            Arch(new Vector3(0,6,102),5,4,t);
        }
        static void Z3()
        {
            var t=G("Z3_Storage_And_UpperGallery",art);
            foreach(float x in new[]{-60,-34})foreach(float z in new[]{89,105,119})Torch(new Vector3(x,z>110?15:8,z),t);
            for(int i=0;i<4;i++)Barrel(new Vector3(-60+i*1.4f,6,88),t);
            for(int i=0;i<3;i++)Barrel(new Vector3(-34,6,91+i*1.5f),t);
            foreach(var tr in root.Find("Z3_Optional_StackAndRescue").Cast<Transform>().Where(x=>x.name.StartsWith("Common_")&&!x.name.Contains("Brace")))
            {
                var s=tr.localScale;for(int i=-1;i<=1;i++)B("Crate_Plank",tr.position+new Vector3(i*s.x*.28f,0,-s.z*.51f),new Vector3(s.x*.26f,s.y*.94f,.08f),wood,t);
                foreach(int sign in new[]{-1,1})B("Crate_IronBand",tr.position+new Vector3(sign*s.x*.4f,0,-s.z*.55f),new Vector3(.1f,s.y,.12f),iron,t);
            }
            for(int row=0;row<5;row++)for(int col=0;col<12;col++)B("UpperGallery_FrontMasonry",new Vector3(-62+col*2.65f,6.7f+row*1.2f,109.42f),new Vector3(2.56f,1.14f,.25f),Stone(),t);
            // Leave the upper crawl aperture clear; this wall is entirely below its floor.
            Arch(new Vector3(-47,13.2f,110),4,1.5f,t);
            for(int i=0;i<2;i++){B("Upper_Shelf",new Vector3(-59+i*6,14.6f,120),new Vector3(4,2.8f,.65f),wood,t);Barrel(new Vector3(-59+i*6,13.2f,118.5f),t);}
            Ceiling(new Vector3(-47,22,104),new Vector3(33,1,36),ceilings);
            Banner(new Vector3(-55,17,120),red,t);Banner(new Vector3(-39,17,120),blue,t);
        }
        static void Z4Z5()
        {
            var t=G("Z4_Z5_LinkedDoor_Chambers",art);
            foreach(int s in new[]{-1,1}){Pillar(new Vector3(s*8,6,117),6,t);Torch(new Vector3(s*7,8,120),t);}
            foreach(float x in new[]{-9,9})Arch(new Vector3(x,6,136),4,3.4f,t);
            // No central corridors: dress each existing doorway and keep its aperture intact.
            var maze=root.Find("Z5_16Rooms_InverseDoors");
            foreach(Transform door in maze)
            {
                if(!door.name.StartsWith("DoorPair"))continue;var leaf=door.Find("DoorLeaf");if(!leaf)continue;
                bool horiz=leaf.localScale.x>leaf.localScale.z;Vector3 p=leaf.position;
                var mat=leaf.GetComponent<Renderer>().sharedMaterial;
                for(int s=-1;s<=1;s+=2)
                {
                    Vector3 q=new Vector3(p.x,7.8f,p.z)+(horiz?Vector3.right:Vector3.forward)*s*1.58f;
                    B("Door_Jamb",q,new Vector3(horiz?.24f:.65f,3.6f,horiz?.65f:.24f),edge,t);
                    B("Rune_Line",q+(horiz?Vector3.back:Vector3.left)*.38f,new Vector3(horiz?.10f:.06f,2.6f,horiz?.06f:.10f),mat,t);
                }
                if(door.name.Contains("Closed"))
                {
                    for(int k=-2;k<=2;k++)B("Door_Board",p+(horiz?Vector3.right:Vector3.forward)*k*.5f+(horiz?Vector3.back:Vector3.left)*.2f,new Vector3(horiz?.46f:.07f,3.2f,horiz?.07f:.46f),wood,t);
                    P("Door_Handle",PrimitiveType.Sphere,p+(horiz?Vector3.back:Vector3.left)*.3f,new Vector3(.2f,.2f,.2f),gold,t);
                }
            }
            for(int row=0;row<4;row++)for(int col=0;col<4;col++)
            {
                float x=-12+col*8,z=140+row*8;
                Torch(new Vector3(x-2.6f,8,z+2.6f),t);
                if((row+col)%3==0)Barrel(new Vector3(x+2.5f,6,z+2.6f),t);
                B("Ceiling_Rib",new Vector3(x,11,z),new Vector3(8,.5f,.4f),edge,t);
            }
            Ceiling(new Vector3(0,11.8f,152),new Vector3(33,.8f,33),ceilings);
            Arch(new Vector3(12,6,168),4,3.8f,t);Point(new Vector3(12,8,169),new Color(1,.6f,.2f),9,7,t);
            // Embossed symbols replace the floating text used in the first blockout.
            foreach(Vector3 p in new[]{new Vector3(-12,6.12f,148),new Vector3(12,6.12f,148)})
                B("Clue_Diamond",p,new Vector3(1.3f,.08f,1.3f),p.x<0?violet:gold,t).transform.Rotate(0,45,0);
        }
        static void Z6()
        {
            var t=G("Z6_Furnace_SwingHall",art);
            foreach(float z in new[]{183,197,211,224})foreach(int s in new[]{-1,1})Torch(new Vector3(s*11,8,z),t);
            foreach(var swing in root.Find("Z6_Lava_IronSwings").Cast<Transform>().Where(x=>x.name.StartsWith("Swing_")))
            {
                var deck=swing.Find("Iron_Deck");Vector3 p=deck.position;
                for(int i=-3;i<=3;i++)B("Iron_Grate",p+new Vector3(i*.5f,.24f,0),new Vector3(.16f,.08f,3.7f),iron,t);
                foreach(int s in new[]{-1,1})
                {
                    var a=p+new Vector3(s*1.7f,.2f,0);var b=new Vector3(s*1.7f,17,p.z);
                    int links=Mathf.CeilToInt((b-a).magnitude/.6f);
                    for(int i=0;i<links;i++){var link=P("Chain_Link_Proxy",PrimitiveType.Cube,Vector3.Lerp(a,b,(i+.5f)/links),new Vector3(.22f,.36f,.15f),iron,t);link.transform.Rotate(0,i%2*90,15);}
                }
            }
            for(int i=0;i<16;i++)
            {
                float x=-11+(float)rng.NextDouble()*22,z=187+(float)rng.NextDouble()*32;
                B("Lava_Bright_Fissure",new Vector3(x,-.70f,z),new Vector3(.12f,.035f,2+(float)rng.NextDouble()*3),ember,t).transform.Rotate(0,rng.Next(180),0);
            }
            foreach(float z in new[]{193,205,217})Point(new Vector3(0,1,z),new Color(1,.18f,.015f),17,10,t);
            Arch(new Vector3(0,9,228),5,4,t);
        }
        static void Z7()
        {
            var t=G("Z7_Roller_Gauntlet",art);
            for(int i=0;i<6;i++)
            {
                float z=239+i*10,y=10+i*2,w=16-i*1.5f;
                foreach(int s in new[]{-1,1}){Torch(new Vector3(s*(w/2+2.5f),y+2,z),t);Pillar(new Vector3(s*(w/2+.6f),y-1,z-3),5,t);}
                B("Chasm_Base",new Vector3(0,y-5,z),new Vector3(w-1,9,9.6f),mortar,t);
            }
            foreach(var roller in root.Find("Z7_Narrowing_Roller_Ascent").GetComponentsInChildren<Transform>().Where(x=>x.name=="Roller"))
            {
                var g=G("Roller_IronStuds",t);float width=roller.localScale.y*2;
                for(int row=0;row<5;row++)for(int a=0;a<6;a++)
                {
                    float angle=a*Mathf.PI/3;
                    var p=roller.position+new Vector3((row-2)*(width-.8f)/5,Mathf.Cos(angle)*1.4f,Mathf.Sin(angle)*1.4f);
                    var stud=B("Spike",p,new Vector3(.38f,.65f,.38f),edge,g);stud.transform.rotation=Quaternion.Euler(angle*Mathf.Rad2Deg,0,45);
                }
            }
            Arch(new Vector3(0,21,302),5,4,t);
        }
        static void Z8()
        {
            var t=G("Z8_Crystal_Sanctum",art);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;Vector3 p=new Vector3(Mathf.Sin(a)*12,21,320+Mathf.Cos(a)*12);
                if(i!=6){Pillar(p,9,t);Torch(p+Vector3.up*2,t);}
            }
            for(int ring=0;ring<3;ring++)for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI/12;var p=new Vector3(Mathf.Sin(a)*(4+ring*3),21.025f,320+Mathf.Cos(a)*(4+ring*3));
                B("Ritual_Inlay",p,new Vector3(.08f,.04f,.8f),ring==0?gold:edge,t).transform.Rotate(0,-i*15,0);
            }
            var proxy=root.Find("Z8_Reunion_CrystalHall/Magic_Stone_Proxy");Undo.RecordObject(proxy.gameObject,"Crystal silhouette");proxy.gameObject.SetActive(false);
            for(int i=0;i<3;i++)
            {var c=P("Floating_Crystal",PrimitiveType.Cube,new Vector3((i-1)*1.2f,24+(i==1?1:0),322),new Vector3(.8f,2.4f,.8f),violet,t);c.transform.Rotate(25,i*45,35);}
            Point(new Vector3(0,24,322),new Color(.62f,.19f,1),15,10,t);
            Banner(new Vector3(-6,27,332),red,t);Banner(new Vector3(6,27,332),blue,t);
            Ceiling(new Vector3(0,32,320),new Vector3(29,1,29),ceilings);
        }
        public static void Capture()
        {
            if(SceneManager.GetActiveScene().path!=Target)throw new InvalidOperationException("Wrong scene");
            Directory.CreateDirectory("Captures/Stage2_DungeonLook");var go=new GameObject("Temporary_DungeonCamera");var c=go.AddComponent<Camera>();
            c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.025f,.018f,.035f);c.nearClipPlane=.1f;c.farClipPlane=400;c.fieldOfView=66;
            try
            {
                Shot(c,new Vector3(0,2.8f,17),new Vector3(0,1,38),"Z1_PlayerView");
                Shot(c,new Vector3(0,4.5f,63),new Vector3(0,4,84),"Z2_PlayerView");
                Shot(c,new Vector3(-44,8.6f,89),new Vector3(-48,11,111),"Z3_PlayerView");
                Shot(c,new Vector3(-12,8.2f,138),new Vector3(-7,7.8f,145),"Z5_PlayerView");
                Shot(c,new Vector3(0,8.4f,181),new Vector3(0,7,204),"Z6_PlayerView");
                Shot(c,new Vector3(7,13.5f,233),new Vector3(0,16,262),"Z7_PlayerView");
                Shot(c,new Vector3(0,23.6f,309),new Vector3(0,24,322),"Z8_PlayerView");
            }
            finally{Object.DestroyImmediate(go);}
        }
        public static void Finish()
        {
            var scene=SceneManager.GetActiveScene();if(scene.path!=Target||Application.isPlaying)throw new InvalidOperationException("Wrong scene");
            root=GameObject.Find("Stage02_Rough_20261127").transform;art=root.Find("Dungeon_Spatial_Pass");
            if(art.Find("Final_Spatial_Adjustments"))throw new InvalidOperationException("Already finished");
            var t=G("Final_Spatial_Adjustments",art);Undo.RegisterCreatedObjectUndo(t.gameObject,"Final dungeon adjustments");
            var unlit=Shader.Find("Universal Render Pipeline/Unlit");
            foreach(string name in new[]{"Fire","FireCore","Crystal","GoldRune"})
            {
                var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Rough/Materials/Look_"+name+".mat");
                if(m&&unlit){var color=m.color;m.shader=unlit;m.SetColor("_BaseColor",color);EditorUtility.SetDirty(m);}
            }
            var glassMat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Rough/Materials/Glass.mat");
            glassMat.SetFloat("_Surface",1);glassMat.SetFloat("_Blend",0);glassMat.SetFloat("_SrcBlend",5);glassMat.SetFloat("_DstBlend",10);glassMat.SetFloat("_ZWrite",0);
            glassMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glassMat.SetOverrideTag("RenderType","Transparent");glassMat.renderQueue=3000;
            glassMat.SetColor("_BaseColor",new Color(.45f,.72f,.84f,.42f));EditorUtility.SetDirty(glassMat);
            var masonry=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Rough/Materials/Look_Stone1.mat");
            for(int i=0;i<14;i++)foreach(int sign in new[]{-1,1})B("UpperHall_Frieze",new Vector3(sign*12.1f,8,14+i*2.8f),new Vector3(1.2f,3.9f,2.72f),masonry,t);
            // Close the gap between the circular hall wall and its high ceiling.
            foreach(var w in root.Find("Z8_Reunion_CrystalHall").Cast<Transform>().Where(x=>x.name=="Hall_Wall"))
            {var b=B("UpperSanctum_Wall",w.position+Vector3.up*5.2f,new Vector3(5.5f,4.5f,1),masonry,t);b.transform.rotation=w.rotation;}
            // A basic bipyramid mesh reads as a crystal while retaining the blockout aesthetic.
            var mesh=new Mesh();mesh.name="Primitive_Crystal_Bipyramid";
            mesh.vertices=new[]{new Vector3(0,1.6f,0),new Vector3(-.65f,0,-.65f),new Vector3(.65f,0,-.65f),new Vector3(.65f,0,.65f),new Vector3(-.65f,0,.65f),new Vector3(0,-1.1f,0)};
            mesh.triangles=new[]{0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1};mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,"Assets/Stage2_Rough/Materials/Crystal_Bipyramid.asset");
            foreach(var c in art.GetComponentsInChildren<Transform>().Where(x=>x.name=="Floating_Crystal"))
            {c.GetComponent<MeshFilter>().sharedMesh=mesh;c.localScale=new Vector3(.8f,1.2f,.8f);}
            RenderSettings.ambientLight=new Color(.32f,.29f,.38f);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        static void Shot(Camera c,Vector3 from,Vector3 to,string n)
        {
            c.transform.position=from;c.transform.LookAt(to);var rt=new RenderTexture(1440,900,24);var prev=RenderTexture.active;var tex=new Texture2D(1440,900,TextureFormat.RGB24,false);
            try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1440,900),0,0);tex.Apply();File.WriteAllBytes("Captures/Stage2_DungeonLook/"+n+".png",tex.EncodeToPNG());}
            finally{RenderTexture.active=prev;c.targetTexture=null;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}
