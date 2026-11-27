using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit, one-shot authoring command. Never runs automatically or changes other scenes.
namespace HiddenPath.Stage2Rough.Editor
{
    public static class Stage2RoughBuilder
    {
        const string Target = "Assets/01_Scenes/MapDesign/MapDesign_Stage2.unity";
        const string RootName = "Stage02_Rough_20261127";
        const string Folder = "Assets/Stage2_Rough/Materials";
        static Transform root;
        static Material stone, wall, trim, red, blue, gold, glass, dark, lava, purple, hint;
        static int count;
        static Transform Group(string n, Transform parent)
        {
            var g = new GameObject(n); g.transform.SetParent(parent, false); return g.transform;
        }
        static Material Mat(string n, Color c, float emission = 0)
        {
            var path = Folder + "/" + n + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.color = c; m.SetFloat("_Smoothness", n == "Glass" ? .85f : .12f);
            if (emission > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * emission); }
            EditorUtility.SetDirty(m); return m;
        }
        static GameObject Shape(string n, PrimitiveType type, Vector3 p, Vector3 size, Material m, Transform t, bool solid = true)
        {
            var g = GameObject.CreatePrimitive(type); g.name = n; g.transform.SetParent(t, false);
            g.transform.position = p; g.transform.localScale = size; g.GetComponent<Renderer>().sharedMaterial = m;
            if (!solid) Object.DestroyImmediate(g.GetComponent<Collider>());
            count++; return g;
        }
        static GameObject Box(string n, float x, float y, float z, float w, float h, float d, Material m, Transform t, bool solid = true)
        { return Shape(n, PrimitiveType.Cube, new Vector3(x,y,z), new Vector3(w,h,d),m,t,solid); }
        static void Floor(string n, float x, float top, float z, float w, float d, Transform t)
        { Box(n,x,top-.6f,z,w,1.2f,d,stone,t); }
        static void Line(string n, Vector3 a, Vector3 b, float width, Material m, Transform t, bool solid = false)
        {
            var g = Shape(n,PrimitiveType.Cube,(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),m,t,solid);
            g.transform.rotation = Quaternion.LookRotation(b-a);
        }
        static void Label(string text, Vector3 p, Transform t, float size = .45f)
        {
            var g = Group("Label_"+text.Replace('\n','_'),t); g.position=p;
            var tm = g.gameObject.AddComponent<TextMesh>(); tm.text=text;tm.characterSize=size;tm.fontSize=48;
            tm.anchor=TextAnchor.MiddleCenter; tm.alignment=TextAlignment.Center;tm.color=Color.white;
            g.rotation=Quaternion.Euler(60,0,0);
        }
        static void Portal(string n, float x, float y, float z, Transform t, float width = 5)
        {
            Box(n+"_L",x-width/2-.4f,y+2.5f,z,.8f,5,.9f,trim,t);
            Box(n+"_R",x+width/2+.4f,y+2.5f,z,.8f,5,.9f,trim,t);
            Box(n+"_Lintel",x,y+5,z,width+1.6f,.8f,.9f,trim,t);
            Box(n+"_Threshold",x,y+.035f,z,width,.07f,1,gold,t,false);
        }
        static void Sides(float x,float y,float z,float w,float d,Transform t,float h=6)
        {
            Box("Boundary_West",x-w/2-.6f,y+h/2,z,1.2f,h,d,wall,t);
            Box("Boundary_East",x+w/2+.6f,y+h/2,z,1.2f,h,d,wall,t);
            for(int i=0;i<=Mathf.FloorToInt(d/8);i++)
                foreach(int sign in new[]{-1,1})
                    Box("Buttress",x+sign*(w/2),y+h*.45f,z-d/2+i*8,1.5f,h*.9f,1.5f,trim,t);
        }
        static void Marker(string n, Vector3 p, Material m, Transform t)
        {
            Shape(n,PrimitiveType.Cylinder,p+Vector3.up*.04f,new Vector3(1.5f,.04f,1.5f),m,t,false);
        }
        static void Ramp(string n, Vector3 a, Vector3 b, float width, Transform t)
        {
            // Surface centerline, with the primitive's thickness below walking surface.
            var g=Box(n,(a.x+b.x)/2,(a.y+b.y)/2-.2f,(a.z+b.z)/2,width,.4f,Vector3.Distance(a,b),stone,t);
            g.transform.rotation=Quaternion.LookRotation(b-a);
        }
        static void Crate(string n,Vector3 p,Vector3 scale,Material m,Transform t)
        {
            Box(n,p.x,p.y+scale.y/2,p.z,scale.x,scale.y,scale.z,m,t);
            Line(n+"_Brace",p+new Vector3(-scale.x*.42f,.1f,-scale.z*.51f),p+new Vector3(scale.x*.42f,scale.y-.1f,-scale.z*.51f),.13f,trim,t);
        }
        [MenuItem("Tools/Hidden Path/Stage 02 Rough/Build in MapDesign Stage2")]
        public static void Build()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying || scene.path!=Target) throw new InvalidOperationException("Open MapDesign/MapDesign_Stage2 in Edit Mode.");
            if(scene.GetRootGameObjects().Any(g=>g.name==RootName)) throw new InvalidOperationException("Rough layout already exists. Preserve manual edits; no automatic rebuild.");
            if(!Shader.Find("Universal Render Pipeline/Lit")) throw new InvalidOperationException("URP Lit shader missing.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            stone=Mat("Stone",new Color(.36f,.39f,.46f)); wall=Mat("Walls",new Color(.18f,.20f,.27f));
            trim=Mat("Trim",new Color(.48f,.47f,.53f)); dark=Mat("Abyss",new Color(.055f,.045f,.085f));
            red=Mat("Role_A",new Color(.9f,.14f,.16f),.2f);blue=Mat("Role_B",new Color(.08f,.5f,.95f),.2f);
            gold=Mat("Route",new Color(1,.65f,.12f),.25f);glass=Mat("Glass",new Color(.45f,.7f,.8f));
            lava=Mat("Lava_Placeholder",new Color(1,.18f,.025f),.8f);purple=Mat("Magic",new Color(.65f,.23f,.9f),.4f);
            hint=Mat("Guide",new Color(.5f,.85f,.58f),.15f);
            Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Stage 02 primitive rough layout");
            foreach(var old in scene.GetRootGameObjects().Where(g=>g.name=="HiddenPath_Stage2_Dungeon"))
            {Undo.RecordObject(old,"Archive previous layout");old.SetActive(false);}
            root=Group(RootName,null); Undo.RegisterCreatedObjectUndo(root.gameObject,"Create rough layout"); count=0;
            BuildZ0();BuildZ1();BuildZ2();BuildZ3();BuildZ4();BuildZ5();BuildZ6();BuildZ7();BuildZ8();
            var sys=Group("Review_Player",root);sys.gameObject.AddComponent<VaroGO.Client.OfflineBootstrap>();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Client/Player.prefab");
            if(prefab){var player=(GameObject)PrefabUtility.InstantiatePrefab(prefab,sys);player.name="Player_RoughWalkthrough";player.transform.position=new Vector3(0,.15f,1);player.transform.rotation=Quaternion.identity;}
            var cam=Camera.main;if(cam){Undo.RecordObject(cam,"Rough camera");Undo.RecordObject(cam.transform,"Rough camera");cam.transform.position=new Vector3(0,4,-5);cam.transform.LookAt(new Vector3(0,1.5f,3));cam.farClipPlane=650;}
            var key=scene.GetRootGameObjects().Select(g=>g.GetComponent<Light>()).FirstOrDefault(l=>l&&l.type==LightType.Directional);
            if(key){Undo.RecordObject(key,"Rough lighting");key.color=Color.white;key.intensity=1.3f;}
            RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.55f,.58f,.65f);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject=root.gameObject;
            if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(-8,10,150),Quaternion.Euler(60,0,0),180);
            Debug.Log("Stage02 rough: "+count+" primitives. Only "+Target+" saved.");
        }
        static void BuildZ0()
        {
            var t=Group("Z0_Entrance",root);Floor("Start_Hall",0,0,5,18,18,t);Sides(0,0,5,18,18,t);
            Box("Back_Wall",0,3,-4,18,6,1,wall,t);Portal("Z1_Entry",0,0,14,t);
            Marker("Spawn_A",new Vector3(-1.5f,0,1),red,t);Marker("Spawn_B",new Vector3(1.5f,0,1),blue,t);
            Label("Z0  /  DUNGEON ENTRY",new Vector3(0,4,10),t);
        }
        static void BuildZ1()
        {
            var t=Group("Z1_GlassMemory_5x6",root);Floor("Observe_Deck",0,0,18,24,8,t);
            Floor("Arrival_Deck",0,0,48,24,8,t);Sides(0,-8,33,24,38,t,14);
            Box("Abyss_Visual_NoCollision",0,-9,33,24,.5f,22,dark,t,false);
            var grid=Group("Bridge_30_GlassTiles_SWITCH_OFF",t);
            for(int r=0;r<6;r++)for(int c=0;c<5;c++)
            {
                float x=(c-2)*3.6f,z=23.8f+r*3.6f;
                Box("Glass_R"+(r+1)+"_C"+(c+1),x,-.15f,z,3.4f,.3f,3.4f,glass,grid);
                foreach(int s in new[]{-1,1}){Box("Frame",x+s*1.65f,.03f,z,.08f,.06f,3.4f,trim,grid,false);Box("Frame",x,.03f,z+s*1.65f,3.4f,.06f,.08f,trim,grid,false);}
            }
            Box("Observation_Console",0,.7f,18,1.6f,1.4f,1,gold,t);
            Label("Z1  /  MEMORY GLASS\n5 x 6  |  OBSERVE > CROSS",new Vector3(0,5,17),t,.35f);
            var on=Group("SWITCH_ON_TopView_RoleHint_Example_INACTIVE",t);
            int[] cols={2,2,3,3,2,1};for(int r=0;r<6;r++)Marker("Hint_R"+(r+1),new Vector3((cols[r]-2)*3.6f,.07f,23.8f+r*3.6f),r%2==0?red:blue,on);
            on.gameObject.SetActive(false);
            var broken=Group("BrokenGlass_Example_INACTIVE",t);for(int i=0;i<4;i++)Box("Shard",i*.5f-1,-1.5f,30,.6f,.12f,1,glass,broken,false).transform.Rotate(15,i*30,30);broken.gameObject.SetActive(false);
            var camera=Group("Observation_TopView_Anchor",t);camera.position=new Vector3(0,28,33);camera.rotation=Quaternion.Euler(90,0,0);
            Portal("Z2_Exit",0,0,51,t);
            Ramp("Rise_To_Z2",new Vector3(0,0,52),new Vector3(0,2,60),6,t);
        }
        static void BuildZ2()
        {
            var t=Group("Z2_PartnerControlledPlatforms",root);Floor("Split_Entry",0,2,63,34,6,t);Sides(0,-5,78,34,36,t,15);
            Box("Pit_NoCollision",0,-7,78,34,.5f,24,dark,t,false);
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;var m=side==0?red:blue;
                var lane=Group(side==0?"A_Route_ControlledBy_B":"B_Route_ControlledBy_A",t);
                Marker("Start",new Vector3(sign*8,2,63),m,lane);
                for(int i=0;i<6;i++)
                {
                    float x=sign*(8+(i%3==1?2:i%3==2?-2:0)),y=2.4f+i*.65f,z=68+i*4.4f;
                    string mode=new[]{"Vertical","Horizontal","Spawn","Flip_Suggestion","Horizontal","Vertical"}[i];
                    Box("Platform_"+(i+1)+"_"+mode,x,y-.25f,z,3.5f,.5f,3.5f,m,lane);
                    Marker("PartnerControl_TriggerLocation",new Vector3(x,y,z),gold,lane);
                    if(i==0||i==5){Box("Lift_Shaft",x,y-3,z,1,5,1,trim,lane);Line("Vertical_Travel",new Vector3(x+1.8f,y-1,z),new Vector3(x+1.8f,y+2,z),.10f,gold,lane);}
                    if(i==1||i==4)Line("Horizontal_Travel",new Vector3(x-3,y-.6f,z),new Vector3(x+3,y-.6f,z),.12f,gold,lane);
                    if(i==3){var axle=Shape("Flip_Axle_Proposal",PrimitiveType.Cylinder,new Vector3(x,y-.4f,z),new Vector3(.4f,2,.4f),trim,lane);axle.transform.rotation=Quaternion.Euler(0,0,90);}
                }
            }
            Floor("Small_Rest_A",-13,4,80,4,4,t);Floor("Small_Rest_B",13,4,80,4,4,t);
            Floor("Reunion_Deck",0,6,98,34,8,t);Portal("Z4_MainRoute",0,6,102,t);
            Label("Z2  /  PARTNER CHANGES YOUR PATH",new Vector3(0,10,64),t,.38f);
            Label("OPTIONAL Z3  <",new Vector3(-12,8,99),t,.3f);
            Floor("Optional_Z3_Branch",-24,6,98,14,5,t);
            Floor("To_Z4",0,6,106,7,8,t);
        }
        static void BuildZ3()
        {
            var t=Group("Z3_Optional_StackAndRescue",root);Floor("Lower_Storage",-47,6,98,32,24,t);
            Box("West_Wall",-63.6f,11,101,1.2f,10,30,wall,t);Box("South_Wall",-47,10,86,32,8,1.2f,wall,t);
            // Upper room is reached by arranged pieces, never a prebuilt staircase.
            Floor("Upper_Room",-47,13.2f,116,32,12,t);Sides(-47,6,116,32,12,t,12);
            foreach(var b in t.Cast<Transform>().Where(b=>b.name=="Boundary_East"||(b.name=="Buttress"&&b.position.x>-40)).ToArray())Object.DestroyImmediate(b.gameObject);
            Box("CrawlWall_Left",-56,14.7f,110,14,3,1,wall,t);Box("CrawlWall_Right",-38,14.7f,110,14,3,1,wall,t);
            Box("CrawlHole_Lintel",-47,15.6f,110,4,1.8f,1,wall,t);
            Crate("Common_01_Small_SoloCarry",new Vector3(-57,6,91),new Vector3(2,1.2f,2),trim,t);
            Crate("Common_02_Small_SoloCarry",new Vector3(-38,6,94),new Vector3(2,1.2f,2),trim,t);
            Crate("Common_03_Large_TwoPlayerCarry",new Vector3(-53,6,101),new Vector3(3.2f,1.2f,2.4f),stone,t);
            Crate("RequiredSpecial_A",new Vector3(-58,6,105),new Vector3(2.2f,1.4f,2.2f),red,t);
            Crate("RequiredSpecial_B",new Vector3(-35,6,102),new Vector3(2.2f,1.4f,2.2f),blue,t);
            Box("Stack_Target_Footprint",-47,6.03f,106,5,.06f,5,gold,t,false);
            Shape("Rope_Coil_Placeholder",PrimitiveType.Cylinder,new Vector3(-38,13.35f,117),new Vector3(1.4f,.15f,1.4f),gold,t,false);
            Line("Rope_Drop_Planned",new Vector3(-34,13.7f,111),new Vector3(-34,6.3f,107),.15f,gold,t);
            Label("Z3  /  OPTIONAL STORAGE\n3 COMMON + A + B  |  STACK > CRAWL > RESCUE",new Vector3(-47,15,91),t,.3f);
            Floor("Return_Balcony",-27,13.2f,118,8,5,t);
            Ramp("Optional_Return_Ramp",new Vector3(-23,13.2f,118),new Vector3(-9,6,118),5,t);
            // Orient a ramp whose direction is lateral: its local width still spans the corridor.
        }
        static void BuildZ4()
        {
            var t=Group("Z4_LowPriority_Interchange",root);Floor("Rest_Connector",0,6,118,18,16,t);
            Marker("Optional_RoleSwap_Placeholder",new Vector3(-4,6,119),purple,t);
            Label("Z4  /  REST + OPTIONAL ROLE SWAP",new Vector3(0,10,114),t,.3f);
            Floor("A_Approach",-9,6,130,5,12,t);Floor("B_Approach",9,6,130,5,12,t);
            Floor("Split_Link",0,6,125,22,3,t);
            Marker("A_SeparateEntry",new Vector3(-9,6,132),red,t);Marker("B_SeparateEntry",new Vector3(9,6,132),blue,t);
        }
        static void MazeWall(Transform t,Vector3 center,bool horizontal,bool open,int pair)
        {
            var g=Group("DoorPair_"+(pair/2+1)+"_"+(pair%2==0?"A":"B")+(open?"_Open_Example":"_Closed_Example"),t);
            var m=pair%2==0?red:blue;
            for(int s=-1;s<=1;s+=2)
            {
                Vector3 p=center+(horizontal?Vector3.right:Vector3.forward)*s*2.75f;
                Shape("Wall",PrimitiveType.Cube,p+Vector3.up*2,new Vector3(horizontal?2.5f:.65f,4,horizontal?.65f:2.5f),wall,g);
            }
            Shape("DoorLintel",PrimitiveType.Cube,center+Vector3.up*3.8f,new Vector3(horizontal?3:.7f,.4f,horizontal?.7f:3),m,g);
            var leaf=Shape("DoorLeaf",PrimitiveType.Cube,center+Vector3.up*(open?4.7f:1.75f),new Vector3(horizontal?2.8f:.35f,3.5f,horizontal?.35f:2.8f),m,g,!open);
            if(open)leaf.transform.localScale=new Vector3(horizontal?2.8f:.35f,.8f,horizontal?.35f:2.8f);
            Marker("LinkedPair_Marker",center+Vector3.up*.02f,m,g);
        }
        static void BuildZ5()
        {
            var t=Group("Z5_16Rooms_InverseDoors",root);Floor("Maze_Floor",0,6,152,32,32,t);
            // Shared walls only; no corridors between adjacent rooms.
            Sides(0,6,152,32,32,t,4);
            Box("SouthWall_Left",-14,8,136,4,4,.8f,wall,t);Box("SouthWall_Middle",0,8,136,12,4,.8f,wall,t);Box("SouthWall_Right",14,8,136,4,4,.8f,wall,t);
            Box("NorthWall",-4,8,168,24,4,.8f,wall,t);
            Box("NorthWall_Right",15,8,168,2,4,.8f,wall,t);
            int pair=0;
            for(int row=0;row<4;row++)for(int col=0;col<3;col++)
                MazeWall(t,new Vector3(-8+col*8,6,140+row*8),false,row==3||row==0,pair++);
            for(int row=0;row<3;row++)for(int col=0;col<4;col++)
                MazeWall(t,new Vector3(-12+col*8,6,144+row*8),true,col==0||col==3,pair++);
            var route=Group("Editor_ExampleRoutes_HIDE_FOR_PUZZLE",t);
            for(int r=0;r<4;r++)for(int c=0;c<4;c++)
            {var room=Group("Room_"+(r+1)+"_"+(c+1),t);room.position=new Vector3(-12+8*c,6,140+8*r);Label((r+1)+"-"+(c+1),room.position+new Vector3(0,.12f,1),room,.22f);}
            Vector3[] a={new Vector3(-9,6.08f,136),new Vector3(-12,6.08f,140),new Vector3(-12,6.08f,164),new Vector3(12,6.08f,164),new Vector3(12,6.08f,170)};
            Vector3[] b={new Vector3(9,6.1f,136),new Vector3(12,6.1f,140),new Vector3(12,6.1f,164)};
            for(int i=1;i<a.Length;i++)Line("A_Example",a[i-1],a[i],.13f,gold,route);
            for(int i=1;i<b.Length;i++)Line("B_Example",b[i-1],b[i],.13f,blue,route);
            Marker("Hint_Color_PurpleToRed",new Vector3(-12,6.05f,148),purple,t);
            Marker("Hint_Direction_North",new Vector3(12,6.05f,148),gold,t);
            var hintObj=Box("Hint_LinkedPair_Diamond",-4,7.2f,164,1.1f,1.1f,.2f,purple,t,false);hintObj.transform.Rotate(0,0,45);
            Label("Z5 / INVERSE DOORS\nA + B SEPARATE STARTS",new Vector3(0,12,135),t,.32f);
            Label("HINT: SAME SYMBOL = LINKED\nOPEN ONE / CLOSE ITS PAIR",new Vector3(0,11,154),t,.26f);
            Portal("Maze_Exit",12,6,168,t,4);Floor("Exit_Link",12,6,173,6,10,t);
            Floor("Exit_Turn",6,6,177,18,5,t);
        }
        static void BuildZ6()
        {
            var t=Group("Z6_Lava_IronSwings",root);Floor("Entry_Ledge",0,6,182,20,8,t);Floor("Exit_Ledge",0,9,224,20,8,t);
            Sides(0,-2,203,26,50,t,20);
            Box("Lava_VISUAL_ONLY_SHADER_TODO",0,-1,203,26,.5f,34,lava,t,false);
            for(int i=0;i<6;i++)
            {
                float x=(i%2==0?-2:2),z=189+i*5.5f,y=6+i*.5f;
                var swing=Group("Swing_"+(i+1)+"_Static_Pose",t);
                Box("Iron_Deck",x,y-.2f,z,4,.4f,4,trim,swing);
                for(int side=-1;side<=1;side+=2){Line("Suspension",new Vector3(x+side*1.7f,y,z),new Vector3(side*1.7f,17,z),.12f,trim,swing);}
                Box("Overhead_Anchor",0,17,z,26,.6f,.6f,wall,swing);
                for(int k=-1;k<=1;k++)Box("Deck_Slat",x+k, y+.035f,z,.15f,.07f,3.5f,dark,swing,false);
                Line("Planned_Swing_Range",new Vector3(-5,y-.6f,z),new Vector3(5,y-.6f,z),.09f,gold,swing);
            }
            for(int i=0;i<3;i++)
            {
                float x=i%2==0?-8:8,z=194+i*10;
                Shape("Lava_Eruption_Position",PrimitiveType.Cylinder,new Vector3(x,0,z),new Vector3(2,1,2),gold,t,false);
                var bat=Group("FlyingObstacle_"+new[]{"Common","A_Only","B_Only"}[i]+"_Placeholder",t);var m=new[]{purple,red,blue}[i];
                Shape("Body",PrimitiveType.Sphere,new Vector3(-x*.6f,10,z),new Vector3(1.3f,.9f,.8f),m,bat,false);
                Box("Wing",-x*.6f,10,z,3,.18f,.8f,m,bat,false).transform.Rotate(0,0,15);
            }
            var saw=Shape("Saw_Optional_Unspecified",PrimitiveType.Cylinder,new Vector3(-12,8,203),new Vector3(6,.25f,6),trim,t,false);saw.transform.rotation=Quaternion.Euler(0,0,90);
            Label("Z6 / IRON SWINGS OVER LAVA\nSTATIC TIMING LAYOUT",new Vector3(0,13,181),t,.32f);
            Portal("Z7_Entry",0,9,228,t);Floor("Z7_Link",0,9,231,12,6,t);
        }
        static void BuildZ7()
        {
            var t=Group("Z7_Narrowing_Roller_Ascent",root);
            for(int i=0;i<6;i++)
            {
                float z=234+i*10,y=9+i*2,w=16-i*1.5f;
                Ramp("Ascent_Ramp_"+(i+1),new Vector3(0,y,z),new Vector3(0,y+2,z+10),w,t);
                // Each ramp has recessed bays, outside the roller corridor.
                foreach(int sign in new[]{-1,1})
                {
                    Floor("Refuge_"+(i+1),sign*(w/2+1.5f),y+1, z+5,3,4,t);
                    Box("Refuge_Back",sign*(w/2+3.4f),y+3,z+5,.8f,4,5,wall,t);
                    Box("Wall_Lower",sign*(w/2+.4f),y+2,z+1.5f,.8f,4,3,wall,t);
                    Box("Wall_Upper",sign*(w/2+.4f),y+3,z+8.5f,.8f,4,3,wall,t);
                }
                var m=i%3==0?trim:i%3==1?red:blue;
                var hazard=Group("Hazard_"+(i+1)+"_"+new[]{"Roller","Boulder","Jar","BouncingRoller","Boulder","Roller"}[i]+"_Static",t);
                if(i==1||i==4)Shape("Boulder",PrimitiveType.Sphere,new Vector3(-2,y+2.8f,z+6),new Vector3(3,3,3),m,hazard);
                else if(i==2){Shape("Jar_Body",PrimitiveType.Capsule,new Vector3(2,y+2.5f,z+6),new Vector3(2.4f,1.4f,2.4f),m,hazard);Shape("Jar_Rim",PrimitiveType.Cylinder,new Vector3(2,y+4,z+6),new Vector3(1.6f,.2f,1.6f),trim,hazard);}
                else
                {
                    float high=i==3?4.7f:2.6f;
                    var roller=Shape("Roller",PrimitiveType.Cylinder,new Vector3(0,y+high,z+6),new Vector3(2.7f,(w-1)/2,2.7f),m,hazard);roller.transform.rotation=Quaternion.Euler(0,0,90);
                    for(int k=0;k<5;k++)Shape("Spike_Proxy",PrimitiveType.Cube,new Vector3((k-2)*(w-2)/5,y+high-1.3f,z+6),new Vector3(.4f,.6f,.4f),trim,hazard).transform.Rotate(0,0,45);
                }
                Line("Downhill_Motion",new Vector3(w*.35f,y+1.9f,z+8),new Vector3(w*.35f,y+.7f,z+2),.13f,gold,hazard);
                Marker("Collapse_Sequence_"+(i+1),new Vector3(0,y+.08f,z+.5f),lava,t);
            }
            Label("Z7 / ROLLERS + REFUGE BAYS\nUPHILL / NARROWING / COLLAPSE BEHIND",new Vector3(0,16,234),t,.32f);
            Floor("Summit",0,21,298,9,8,t);Portal("Z8_Entry",0,21,302,t);
        }
        static void BuildZ8()
        {
            var t=Group("Z8_Reunion_CrystalHall",root);Floor("Entry",0,21,306,7,8,t);
            Shape("Safe_Round_Floor",PrimitiveType.Cylinder,new Vector3(0,20.4f,320),new Vector3(28,.6f,28),stone,t);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI*2/16; if(i==8)continue;
                var p=new Vector3(Mathf.Sin(a)*14,24,320+Mathf.Cos(a)*14);
                var g=Shape("Hall_Wall",PrimitiveType.Cube,p,new Vector3(5.5f,6,1),wall,t);g.transform.rotation=Quaternion.Euler(0,i*22.5f,0);
                Shape("Pillar",PrimitiveType.Cylinder,p+Vector3.up*.5f,new Vector3(1.2f,3.5f,1.2f),trim,t);
            }
            Shape("Dais",PrimitiveType.Cylinder,new Vector3(0,21.3f,322),new Vector3(6,.3f,6),trim,t);
            var crystal=Shape("Magic_Stone_Proxy",PrimitiveType.Cube,new Vector3(0,24,322),new Vector3(2,3,2),purple,t,false);crystal.transform.rotation=Quaternion.Euler(25,0,35);
            Marker("JointArrival_A",new Vector3(-2,21,316),red,t);Marker("JointArrival_B",new Vector3(2,21,316),blue,t);
            Label("Z8 / MAGIC STONE\nBOTH PLAYERS ARRIVE",new Vector3(0,28,322),t,.38f);
        }
        public static void Capture()
        {
            if(SceneManager.GetActiveScene().path!=Target)throw new InvalidOperationException("Wrong scene");
            Directory.CreateDirectory("Captures/Stage2_Rough_20261127");
            var go=new GameObject("Temporary_RoughCapture");var c=go.AddComponent<Camera>();
            c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.06f,.07f,.1f);c.farClipPlane=700;
            try
            {
                c.orthographic=true;c.orthographicSize=185;
                Shot(c,new Vector3(-12,250,165),new Vector3(-12,0,165),"Overview",1000,1800);
                c.orthographicSize=49;Shot(c,new Vector3(48,68,18),new Vector3(-4,4,60),"Z0_Z2",1600,1100);
                c.orthographicSize=43;Shot(c,new Vector3(38,64,82),new Vector3(-22,7,112),"Z3_Z5",1600,1100);
                c.orthographicSize=65;Shot(c,new Vector3(60,83,180),new Vector3(0,12,251),"Z6_Z8",1600,1300);
            }
            finally{Object.DestroyImmediate(go);}
        }
        static void Shot(Camera c,Vector3 from,Vector3 to,string name,int w,int h)
        {
            c.transform.position=from;c.transform.LookAt(to);
            var rt=new RenderTexture(w,h,24);var old=RenderTexture.active;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
            try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes("Captures/Stage2_Rough_20261127/"+name+".png",tex.EncodeToPNG());}
            finally{c.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}
