using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using VaroGO.Client;
using Object = UnityEngine.Object;

namespace HiddenPath.Stage2.Editor
{
    [InitializeOnLoad]
    public static class Stage2DungeonBuilder
    {
        const string Target = "Assets/01_Scenes/MapDesign_Stage2.unity";
        const string Folder = "Assets/Stage2_Dungeon";
        const string FW = "Assets/04_Resources/Map/TriForge Assets/Fantasy Worlds - Forest/Prefabs/";
        const string Int = "Ruins/Interior/";
        const string Request = "Temp/Stage2Dungeon.build-request";
        const string Output = "Captures/Stage2_Dungeon";
        static Transform root, architecture, ceilings, lights, mechanics;
        static Transform[] z;
        static Material dark, slate, metal, gold, cyan, violet, red, blue, glass;
        static int instanceCount;
        static readonly List<Transform> mirrors = new List<Transform>(), rollers = new List<Transform>(), alternators = new List<Transform>();
        static readonly List<Vector3> walkSamples = new List<Vector3>();
        static Stage2DungeonBuilder() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying || !File.Exists(Request)) return;
            string action = File.ReadAllText(Request).Trim(); File.Delete(Request);
            try { if(action == "refresh") AssetDatabase.Refresh(); else if(action == "playcheck") Stage2PlayCheck.Begin(); else if(action == "render") RenderViews(); else if(action == "audit") Audit(); else Build(); }
            catch(Exception e) { Directory.CreateDirectory(Output); File.WriteAllText(Output+"/build-error.txt",e.ToString()); Debug.LogException(e); }
        }
        static Transform Group(string name, Transform parent)
        {
            var g=new GameObject(name);g.transform.SetParent(parent);return g.transform;
        }
        static Material MakeMaterial(string name, Color color, float glow=0)
        {
            string path=Folder+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.24f);
            if(glow>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*glow);}
            EditorUtility.SetDirty(m);return m;
        }
        static GameObject Box(string name, Vector3 pos, Vector3 size, Material material, Transform parent, bool collision=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,true);g.transform.position=pos;g.transform.localScale=Vector3.Scale(g.transform.localScale,size);g.GetComponent<Renderer>().sharedMaterial=material;
            if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        static GameObject Round(string name, Vector3 pos, Vector3 size, Material material, Transform parent, bool collision=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(parent,true);g.transform.position=pos;g.transform.localScale=Vector3.Scale(g.transform.localScale,size);g.GetComponent<Renderer>().sharedMaterial=material;
            if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        static Bounds BoundsOf(GameObject g)
        {
            var renderers=g.GetComponentsInChildren<Renderer>(true).Where(r=>!(r is ParticleSystemRenderer)).ToArray();
            if(renderers.Length==0)throw new Exception("Prefab has no mesh: "+g.name);
            var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;
        }
        // Keep the original Fantasy Worlds prefab/material references. Only scene instance transforms change.
        static GameObject Asset(string relative, Vector3 bottom, Vector3 size, Transform parent, float yaw=0, bool solid=false)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(FW+relative+".prefab");if(prefab==null)throw new Exception("Missing Fantasy Worlds asset: "+relative);
            var holder=Group(Path.GetFileName(relative),parent);var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,holder);g.transform.localPosition=Vector3.zero;g.transform.localRotation=Quaternion.identity;g.transform.localScale=Vector3.one;
            foreach(var c in g.GetComponentsInChildren<Collider>(true)) c.enabled=false;
            foreach(var l in g.GetComponentsInChildren<LODGroup>(true))l.ForceLOD(0);
            var b=BoundsOf(g);g.transform.localScale=new Vector3(size.x/Mathf.Max(.01f,b.size.x),size.y/Mathf.Max(.01f,b.size.y),size.z/Mathf.Max(.01f,b.size.z));
            b=BoundsOf(g);g.transform.position+=new Vector3(-b.center.x,-b.min.y,-b.center.z);
            holder.position=bottom;holder.rotation=Quaternion.Euler(0,yaw,0);
            if(solid){var c=holder.gameObject.AddComponent<BoxCollider>();c.center=Vector3.up*size.y*.5f;c.size=size;}
            instanceCount++;return holder.gameObject;
        }
        static void Value(Object target,string property,float value)
        {
            var s=new SerializedObject(target);var p=s.FindProperty(property);if(p==null)throw new Exception("Missing field "+property);
            if(p.propertyType==SerializedPropertyType.Boolean)p.boolValue=value!=0;else if(p.propertyType==SerializedPropertyType.Integer || p.propertyType==SerializedPropertyType.Enum)p.intValue=(int)value;else p.floatValue=value;s.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Reference(Object target,string property,Object value){var s=new SerializedObject(target);s.FindProperty(property).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        static void PointLight(Vector3 p,Color color,float intensity,float range,Transform parent)
        {
            var g=Group("Dungeon_Light",parent);g.position=p;var l=g.gameObject.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;
        }
        static void Torch(Vector3 p,Transform parent,bool cold=false)
        {
            Asset("Doodads/P_FW01_StandingTorch_01",p,new Vector3(.65f,2.6f,.65f),parent);
            var flame=GameObject.CreatePrimitive(PrimitiveType.Sphere);flame.name=cold?"Arcane_Flame":"Amber_Flame";flame.transform.SetParent(parent);flame.transform.position=p+Vector3.up*2.5f;flame.transform.localScale=new Vector3(.35f,.7f,.35f);flame.GetComponent<Renderer>().sharedMaterial=cold?cyan:gold;Object.DestroyImmediate(flame.GetComponent<Collider>());
            PointLight(p+Vector3.up*3,cold?new Color(.16f,.7f,1):new Color(1,.53f,.2f),cold?5:7,17,lights);
        }
        static void Arch(Vector3 p,Transform parent,float width=7,float height=9)
        {
            Asset(Int+"P_FW01_Ruins_Int_Doorway_A_6x6_01",p,new Vector3(width,height,1.5f),parent,90);
            foreach(int side in new[]{-1,1}){var c=Group("Arch_Side_Collision",parent).gameObject;c.transform.position=p+new Vector3(0,height*.5f,side*(width*.5f-.5f));var b=c.AddComponent<BoxCollider>();b.size=new Vector3(1.5f,height,1);}
        }
        static void Room(string name,Vector3 p,float width,float depth,Transform parent,bool roof=false)
        {
            var room=Group(name,parent);
            Box("Foundation",p-Vector3.up*5.2f,new Vector3(width,10,depth),dark,room);
            int nx=Mathf.CeilToInt(width/6),nz=Mathf.CeilToInt(depth/6);
            for(int i=0;i<nx;i++)for(int j=0;j<nz;j++)
            {
                float w=width/nx,d=depth/nz;var q=p+new Vector3(-width*.5f+(i+.5f)*w,-.35f,-depth*.5f+(j+.5f)*d);
                Asset(Int+"P_FW01_Ruins_Int_Floor_6x6_01",q,new Vector3(w,.35f,d),room,0,true);
                if(roof)Asset(Int+"P_FW01_Ruins_Int_Ceiling_A_6x6_01",q+Vector3.up*10,new Vector3(w,.8f,d),ceilings);
            }
            for(int i=0;i<nx;i++) foreach(int side in new[]{-1,1})
            {
                float x=p.x-width*.5f+(i+.5f)*width/nx;
                Asset(Int+(i%3==1?"P_FW01_Ruins_Int_Wall_A_4x4_Brkn_01":"P_FW01_Ruins_Int_Wall_B_6x6_01"),new Vector3(x,p.y,p.z+side*(depth*.5f+.3f)),new Vector3(width/nx,roof?10:8,.9f),room,0,true);
                if(i%2==0){Asset(Int+"P_FW01_Ruins_Int_Pillar_A_2x6_01",new Vector3(x,p.y,p.z+side*(depth*.5f-1)),new Vector3(1.4f,10,1.4f),room,0,true);Torch(new Vector3(x,p.y,p.z+side*(depth*.5f-2.5f)),room,i%4==0);}
            }
            // High arches and entablature establish the dungeon volume without hiding the route from a following camera.
            if(width>=20 && !name.Contains("Mirror"))Arch(p+Vector3.right*(width*.25f),room,depth-2,11);
            walkSamples.Add(p);
        }
        static void Ramp(Vector3 a,Vector3 b,float width,Transform parent)
        {
            var d=b-a;var slab=Box("Walkable_Ramp",(a+b)*.5f-Vector3.up*.2f,new Vector3(width,.4f,d.magnitude+.3f),slate,parent);slab.transform.rotation=Quaternion.LookRotation(d);
            var side=Vector3.Cross(Vector3.up,d.normalized)*width*.5f;
            for(int j=0;j<=8;j++){
                var p=Vector3.Lerp(a,b,j/8f);walkSamples.Add(p);
                if(j%2==0)foreach(int sign in new[]{-1,1})Box("Ramp_Edge_Buttress",p+side*sign-Vector3.up*.1f,new Vector3(.25f,.35f,.6f),metal,parent);
            }
        }
        static void Checkpoint(Vector3 p,int order,Transform parent)
        {
            var g=Group("CP"+(order+1),parent).gameObject;g.transform.position=p;
            var c=g.AddComponent<BoxCollider>();c.isTrigger=true;c.size=new Vector3(6,3,6);c.center=Vector3.up*1.5f;
            var ring=Round("Checkpoint_Rune",p+Vector3.up*.06f,new Vector3(4,.06f,4),cyan,g.transform,false);
            var spawn=Group("Respawn",g.transform);spawn.position=p+Vector3.up*.1f;spawn.rotation=Quaternion.Euler(0,90,0);
            var cp=g.AddComponent<VaroGO.Client.Checkpoint>();Value(cp,"order",order);Reference(cp,"spawnPoint",spawn);Reference(cp,"indicator",ring.GetComponent<Renderer>());Torch(p+Vector3.forward*3.4f,parent,true);
        }
        static void Label(Vector3 p,string title,Transform parent)
        {
            var g=Group("Wayfinding_Plaque",parent);g.position=p;g.rotation=Quaternion.Euler(0,-90,0);
            var board=Box("Plaque",p,new Vector3(.15f,1.1f,3.4f),dark,g);
            var label=Group("Inscription",g);label.localPosition=new Vector3(0,0,.09f);label.localRotation=Quaternion.Euler(0,180,0);
            var t=label.gameObject.AddComponent<TextMesh>();t.text=title;t.fontSize=48;t.characterSize=.1f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=new Color(.9f,.8f,.55f);
        }
        [MenuItem("Tools/Hidden Path/Build Stage 2 Dungeon")]
        public static void Build()
        {
            if(Application.isPlaying)throw new Exception("Stop play mode first.");
            Directory.CreateDirectory(Output);
            var originalHashes=Directory.GetFiles("Assets/01_Scenes","*.unity").Where(p=>p.Replace('\\','/')!=Target).ToDictionary(p=>p,p=>Hash(p));
            if(SceneManager.GetActiveScene().path!=Target)
            {
                for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("An open scene has unsaved changes. Save it manually before building Stage 2.");
                EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
            }
            var scene=SceneManager.GetActiveScene();if(scene.path!=Target)throw new Exception("Scene guard failed.");
            if(!File.Exists(Output+"/MapDesign_Stage2.before.unity"))File.Copy(Target,Output+"/MapDesign_Stage2.before.unity");
            foreach(var g in scene.GetRootGameObjects())if(g.GetComponent<Camera>()==null && g.GetComponent<Light>()==null)Undo.DestroyObjectImmediate(g);
            Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
            dark=MakeMaterial("Basalt_Foundation",new Color(.10f,.12f,.17f));slate=MakeMaterial("Slate_Ramps",new Color(.27f,.30f,.34f));metal=MakeMaterial("Forged_Iron",new Color(.14f,.16f,.20f));
            gold=MakeMaterial("Amber_Rune",new Color(1,.52f,.12f),2);cyan=MakeMaterial("Aether_Cyan",new Color(.05f,.55f,.9f),2);violet=MakeMaterial("Violet_Ritual",new Color(.55f,.12f,.85f),1.5f);red=MakeMaterial("Red_Trial",new Color(.8f,.1f,.15f),.6f);blue=MakeMaterial("Blue_Trial",new Color(.08f,.25f,.85f),.7f);glass=MakeMaterial("Glass_Surface",new Color(.12f,.32f,.43f),.4f);
            instanceCount=0;mirrors.Clear();rollers.Clear();alternators.Clear();walkSamples.Clear();
            root=Group("HiddenPath_Stage2_Dungeon",null);Undo.RegisterCreatedObjectUndo(root.gameObject,"Build Stage 2 dungeon");
            architecture=Group("FantasyWorlds_Architecture",root);ceilings=Group("Ceilings_HideForEditing",root);lights=Group("Dungeon_Lighting",root);mechanics=Group("Offline_Preview_Systems",root);
            string[] names={"Sunken_Gate","Three_Glass_Paths","Alternating_Crossing","Forge_Your_Path","Oath_Exchange","Mirror_Portcullis","Chain_Swing_Chasm","Crusher_Gallery","Final_Seal"};z=new Transform[9];for(int i=0;i<9;i++)z[i]=Group("Z"+i+"_"+names[i],root);

            // Z0: covered entrance nave, staggered stair-ramp out to the first abyss.
            Room("Entrance_Nave",new Vector3(16,0,30),30,24,z[0],true);
            Asset("Doodads/P_fwOF_Statue_01",new Vector3(8,0,22),new Vector3(2.4f,5.5f,2.4f),z[0],90);
            Asset("Doodads/P_fwOF_Statue_02",new Vector3(8,0,38),new Vector3(2.4f,5.5f,2.4f),z[0],90);
            Label(new Vector3(11,3,23),"THE SUNKEN GATE\nDUNGEON II",z[0]);
            Ramp(new Vector3(27,0,30),new Vector3(35,4,38),6,z[0]);
            Room("Glass_Threshold",new Vector3(38,4,38),4,22,z[1]);Ramp(new Vector3(35,4,38),new Vector3(39,4,38),5,z[1]);
            for(int lane=0;lane<3;lane++)for(int i=0;i<8;i++)
            {
                var p=new Vector3(42+i*4,4+i*.375f,32+lane*6);
                var g=Box("Lane_"+(lane+1)+"_Tile_"+(i+1),p-Vector3.up*.2f,new Vector3(3.6f,.4f,3.8f),glass,z[1]);
                bool infoRed=(i+lane)%3!=1;var marker=Box("Asymmetric_Information",p+Vector3.up*.035f,new Vector3(1.1f,.06f,1.1f),infoRed?red:blue,g.transform,false);
                marker.transform.rotation=Quaternion.Euler(0,45,0);var vis=marker.AddComponent<MagicVisibility>();Value(vis,"type",infoRed?1:2);
                foreach(int side in new[]{-1,1})Box("Glass_Rim",p+new Vector3(0,.04f,side*1.8f),new Vector3(3.6f,.08f,.08f),cyan,z[1],false);
                walkSamples.Add(p);
            }
            Room("Glass_Exit",new Vector3(75,7,40),6,24,z[1]);
            // Z2: two offset timing lanes crossing toward the elevated eastern landing.
            Ramp(new Vector3(76,7,46),new Vector3(82,8,58),5,z[2]);Room("Cycle_West",new Vector3(83,8,60),6,16,z[2]);
            for(int i=0;i<2;i++)
            {
                var g=Box("Alternating_Bridge_"+i,new Vector3(91,7.8f,56+i*8),new Vector3(10,.4f,4.5f),i==0?blue:gold,z[2]);alternators.Add(g.transform);
                for(int k=0;k<5;k++)Box("Cycle_Bridge_Band",new Vector3(87+k*2,8.05f,56+i*8),new Vector3(.15f,.08f,4.3f),metal,g.transform,false);
            }
            Room("Cycle_East",new Vector3(99,8,60),6,16,z[2]);
            Ramp(new Vector3(100,8,54),new Vector3(107,9,48),5,z[3]);
            Room("Forge_Crypt",new Vector3(118,9,45),26,26,z[3]);Checkpoint(new Vector3(108,9,48),0,z[3]);
            // Movable crate stepping puzzle at the side of a three-metre dais; access remains readable.
            Box("Raised_Path_Dais",new Vector3(126,10.4f,45),new Vector3(7,2.8f,10),slate,z[3]);
            for(int i=0;i<2;i++)
            {
                var crate=Asset("Doodads/P_FW01_Crate_A_01",new Vector3(117+i*4,9,44+i*4),new Vector3(2,1.4f+i*.1f,2),z[3],0,true);
                crate.AddComponent<Rigidbody>();crate.AddComponent<LocalPushableState>();crate.AddComponent<Pushable>();
            }
            Asset("Doodads/P_FW01_Chest_B_01",new Vector3(112,9,36),new Vector3(1.7f,1.3f,1.2f),z[3],45);
            Label(new Vector3(114,11,35),"FORGE YOUR PATH\nF + MOVE : PUSH",z[3]);
            Ramp(new Vector3(126,11.8f,45),new Vector3(137,12,65),4.5f,z[4]);
            Room("Oath_Rotunda",new Vector3(151,12,77),28,28,z[4]);
            Ramp(new Vector3(137,12,65),new Vector3(142,12,70),5,z[4]);
            var altar=Round("Role_Exchange_Altar",new Vector3(151,12.22f,77),new Vector3(6,.22f,6),violet,z[4]);
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Round("Altar_Rune",new Vector3(151+Mathf.Cos(a)*4,12.08f,77+Mathf.Sin(a)*4),new Vector3(.55f,.08f,.55f),gold,z[4],false);}
            Asset(Int+"P_FW01_Ruins_Decor_Column_6m_01",new Vector3(150,12,88),new Vector3(2,9,2),z[4]);
            Checkpoint(new Vector3(162,12,77),1,z[4]);
            Ramp(new Vector3(163,12,77),new Vector3(169,12,75),6,z[5]);
            Room("Mirror_Vestibule",new Vector3(180,12,75),26,26,z[5]);
            for(int i=0;i<2;i++)
            {
                var p=new Vector3(181,12,69+i*12);Arch(p,z[5],7,9);
                var door=Asset("Ruins/P_FW01_Castle_Gate_01",p,new Vector3(5,6,1),z[5],90,true);door.name=i==0?"Red_Mirror_Gate":"Blue_Mirror_Gate";mirrors.Add(door.transform);
                Box("Gate_Role_Light",p+Vector3.up*6.7f,new Vector3(.5f,.4f,5),i==0?red:blue,z[5],false);Torch(p+new Vector3(-2,0,3.4f),z[5],i==1);
            }
            // Central screen prevents walking between the paired gates.
            Asset(Int+"P_FW01_Ruins_Int_Wall_B_6x6_01",new Vector3(181,12,75),new Vector3(5,8,1.4f),z[5],90,true);
            Ramp(new Vector3(191,12,69),new Vector3(199,10,55),5,z[6]);
            Room("Swing_West_Dock",new Vector3(200,10,52),6,14,z[6]);
            for(int i=0;i<3;i++)
            {
                var p=new Vector3(207+i*8,10+i*.6f,48);
                var deck=Box("Swing_"+(i+1),p-Vector3.up*.25f,new Vector3(4.5f,.5f,4.2f),slate,z[6]);deck.AddComponent<Rigidbody>();
                var swing=deck.AddComponent<Stage2SwingPlatform>();swing.amplitude=2;swing.period=7;swing.direction=Vector3.right;Value(swing,"phaseOffset",i*.6f);
                swing.chains=new Transform[2];swing.anchors=new Vector3[2];
                for(int k=0;k<2;k++){
                    var anchor=p+new Vector3(0,11,k==0?-1.5f:1.5f);swing.anchors[k]=anchor;
                    swing.chains[k]=Round("Suspension_Chain",(p+anchor)*.5f,new Vector3(.1f,5.5f,.1f),metal,z[6],false).transform;
                }
                Asset(Int+"P_FW01_Ruins_Int_Pillar_A_2x6_01",p+new Vector3(0,-4,-5),new Vector3(1.6f,16,1.6f),z[6],0,true);
                Asset(Int+"P_FW01_Ruins_Int_Pillar_A_2x6_01",p+new Vector3(0,-4,5),new Vector3(1.6f,16,1.6f),z[6],0,true);
                Box("Swing_Overhead_Beam",p+Vector3.up*11.5f,new Vector3(1.3f,1.1f,12),metal,z[6]);
            }
            Room("Swing_East_Dock",new Vector3(230,12,48),6,14,z[6]);
            Ramp(new Vector3(231,12,53),new Vector3(235.4f,14,64),5,z[7]);
            Room("Crusher_Gallery",new Vector3(252,14,73),32,24,z[7],true);
            Ramp(new Vector3(235.4f,14,64),new Vector3(240,14,68),5,z[7]);
            for(int i=0;i<3;i++)
            {
                var p=new Vector3(244+i*8,15.1f,73);var roller=Round("Cross_Roller_"+i,p,new Vector3(2.3f,3.5f,2.3f),i%2==0?red:blue,z[7]);roller.transform.rotation=Quaternion.Euler(90,0,0);roller.GetComponent<Collider>().isTrigger=true;roller.tag="KillZone";rollers.Add(roller.transform);
                for(int j=0;j<7;j++){
                    var tooth=Box("Roller_Tooth",p+new Vector3(0,1.2f,-3+j),new Vector3(.7f,.5f,.35f),metal,roller.transform,false);
                }
                foreach(int side in new[]{-1,1})Asset(Int+"P_FW01_Ruins_Int_Pillar_B_2x6_02",new Vector3(p.x,14,73+side*9),new Vector3(2,8,2),z[7],0,true);
                Box("Safe_Alcove_Marker",new Vector3(p.x+3,14.04f,81),new Vector3(2.2f,.08f,2.2f),cyan,z[7],false);
            }
            Checkpoint(new Vector3(266,14,78),2,z[7]);
            Ramp(new Vector3(267,14,78),new Vector3(276,17,82),5,z[8]);
            Room("Final_Seal_Chamber",new Vector3(287,17,84),24,24,z[8],true);
            for(int i=0;i<5;i++)Box("Exit_Step_"+i,new Vector3(289+i,17+(i+1)*.1f,84),new Vector3(1.1f,(i+1)*.2f,7),slate,z[8]);
            Box("Exit_Dais",new Vector3(295,17.5f,84),new Vector3(5,1,8),slate,z[8]);Arch(new Vector3(296,18,84),z[8],8,10);
            Box("Exit_Seal_Light",new Vector3(296,22,84),new Vector3(.15f,6,5),cyan,z[8],false);
            var goal=Group("Stage2_Goal",z[8]).gameObject;goal.transform.position=new Vector3(295,20,84);var goalCol=goal.AddComponent<BoxCollider>();goalCol.isTrigger=true;goalCol.size=new Vector3(3,5,5);var state=goal.AddComponent<LocalGoalState>();var gz=goal.AddComponent<GoalZone>();Reference(gz,"stateObject",goal);
            // Dungeon shell: sheer rock walls, broken masonry and suspended galleries, no forest canopy.
            Box("Abyss_Visual",new Vector3(150,-16,67),new Vector3(310,.4f,150),dark,architecture,false);
            for(int i=0;i<21;i++)foreach(int side in new[]{-1,1}){
                var p=new Vector3(i*15,-15,side<0?0:132);Asset("Rocks/P_fwOF_Rock_L_01",p,new Vector3(18,36,15),architecture,i*37);
                if(i%3==0)Asset("Ruins/Structures/P_fwOF_Ruins_Tower_01",p+Vector3.up*8,new Vector3(9,25,9),architecture,90);
            }
            foreach(int index in new[]{0,3,4,5,7,8}){
                var zone=z[index];var positions=zone.GetComponentsInChildren<BoxCollider>().Where(c=>!c.isTrigger).Select(c=>c.bounds.center).ToArray();
                if(positions.Length>0){var p=positions[0];Asset(Int+"P_FW01_Floor_Debris_03",new Vector3(p.x+5,p.y+5.2f,p.z-6),new Vector3(3,.35f,2),zone);}
            }
            mechanics.gameObject.AddComponent<OfflineBootstrap>();
            var playerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Client/Player.prefab");var player=(GameObject)PrefabUtility.InstantiatePrefab(playerPrefab,mechanics);player.name="Player_Stage2_OfflinePreview";player.transform.position=new Vector3(8,.15f,30);player.transform.rotation=Quaternion.Euler(0,90,0);player.GetComponent<CharacterController>().stepOffset=.5f;
            var cam=scene.GetRootGameObjects().Select(g=>g.GetComponent<Camera>()).FirstOrDefault(c=>c!=null);if(cam==null){var cg=new GameObject("Main Camera");cg.tag="MainCamera";cam=cg.AddComponent<Camera>();cg.AddComponent<AudioListener>();}
            cam.orthographic=false;cam.fieldOfView=65;cam.nearClipPlane=.1f;cam.farClipPlane=500;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.035f,.045f,.08f);cam.transform.position=new Vector3(1,3.7f,29);cam.transform.LookAt(player.transform.position+Vector3.up*1.4f);if(cam.GetComponent<CinemachineBrain>()==null)cam.gameObject.AddComponent<CinemachineBrain>();
            var orbit=player.GetComponentInChildren<CinemachineOrbitalFollow>(true);if(orbit!=null){orbit.HorizontalAxis.Value=90;orbit.VerticalAxis.Value=14;orbit.Radius=6;}
            var preview=mechanics.gameObject.AddComponent<Stage2DungeonPreview>();preview.player=player.transform;preview.exchangeAltar=altar.transform;preview.mirrorDoors=mirrors.ToArray();preview.rollers=rollers.ToArray();preview.alternatingBridges=alternators.ToArray();preview.goal=state;
            var rewards=new List<Transform>();var sapPatches=new List<Transform>();
            Vector3[] rewardPositions={new Vector3(113,10,36),new Vector3(143,13,86),new Vector3(253,15,82)};
            foreach(var p in rewardPositions){
                var acorn=Group("Optional_Acorn_Reward",mechanics);acorn.position=p;
                var nut=GameObject.CreatePrimitive(PrimitiveType.Sphere);nut.name="Acorn_Body";nut.transform.SetParent(acorn);nut.transform.localPosition=Vector3.zero;nut.transform.localScale=new Vector3(.7f,1,.7f);nut.GetComponent<Renderer>().sharedMaterial=gold;Object.DestroyImmediate(nut.GetComponent<Collider>());
                Round("Acorn_Cap",p+Vector3.up*.32f,new Vector3(.82f,.16f,.82f),metal,acorn,false);Box("Acorn_Stem",p+Vector3.up*.6f,new Vector3(.12f,.28f,.12f),gold,acorn,false);rewards.Add(acorn);
            }
            foreach(var p in new[]{new Vector3(111,9.04f,55),new Vector3(143,12.04f,69),new Vector3(190,12.04f,82)}){
                var patch=Round("Living_Aether_Sap_Environmental",p,new Vector3(3.4f,.045f,2.4f),violet,mechanics,false);sapPatches.Add(patch.transform);PointLight(p+Vector3.up*.5f,new Color(.52f,.12f,1),2,5,lights);
            }
            preview.rewardAcorns=rewards.ToArray();preview.livingSap=sapPatches.ToArray();
            var sun=scene.GetRootGameObjects().Select(g=>g.GetComponent<Light>()).FirstOrDefault(l=>l!=null);if(sun!=null){sun.intensity=.65f;sun.color=new Color(.53f,.66f,.85f);sun.transform.rotation=Quaternion.Euler(65,-25,0);sun.shadows=LightShadows.Soft;}
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.3f,.37f,.51f);RenderSettings.ambientEquatorColor=new Color(.23f,.28f,.37f);RenderSettings.ambientGroundColor=new Color(.10f,.11f,.17f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.05f,.08f,.14f);RenderSettings.fogStartDistance=60;RenderSettings.fogEndDistance=190;
            Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            foreach(var pair in originalHashes)if(Hash(pair.Key)!=pair.Value)throw new Exception("Unexpected scene change: "+pair.Key);
            File.WriteAllText(Output+"/build-report.txt","Scene: "+Target+"\nFantasy Worlds prefab instances: "+instanceCount+"\nZones: 9\nOther scene SHA256 values unchanged.\nPlayer: existing offline prefab and Cinemachine.\nGimmicks: offline layout previews; no online authority changes.\n");
            Selection.activeGameObject=root.gameObject;if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(22,5,31),Quaternion.Euler(22,80,0),25,false);
            Audit();RenderViews();Debug.Log("[Stage2 Dungeon] Build and save complete.");
        }
        static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));}
        [MenuItem("Tools/Hidden Path/Audit Stage 2 Dungeon")]
        public static void Audit()
        {
            if(SceneManager.GetActiveScene().path!=Target)throw new Exception("Stage 2 scene must be active.");
            var r=GameObject.Find("HiddenPath_Stage2_Dungeon");var lines=new List<string>();int missing=0;foreach(var t in r.GetComponentsInChildren<Transform>(true))missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            lines.Add("Missing scripts: "+missing);lines.Add("Missing materials: "+r.GetComponentsInChildren<Renderer>(true).Count(x=>x.sharedMaterials.Any(m=>m==null)));
            lines.Add("Checkpoints: "+r.GetComponentsInChildren<VaroGO.Client.Checkpoint>().Length);lines.Add("Swing platforms: "+r.GetComponentsInChildren<Stage2SwingPlatform>().Length);lines.Add("Zones: "+r.transform.Cast<Transform>().Count(t=>t.name.StartsWith("Z")));
            Physics.SyncTransforms();int failed=0;foreach(var p in walkSamples){if(!Physics.Raycast(p+Vector3.up*.8f,Vector3.down,out var hit,1.6f,~0,QueryTriggerInteraction.Ignore)){lines.Add("No support at "+p);failed++;}}
            lines.Add("Static floor / ramp samples: "+walkSamples.Count+", unsupported: "+failed);File.WriteAllText(Output+"/audit-report.txt",string.Join("\n",lines));
        }
        [MenuItem("Tools/Hidden Path/Render Stage 2 Dungeon")]
        public static void RenderViews()
        {
            if(SceneManager.GetActiveScene().path!=Target)throw new Exception("Stage 2 scene must be active.");
            Directory.CreateDirectory(Output);var go=new GameObject("Stage2_Render_Temporary");var c=go.AddComponent<Camera>();c.fieldOfView=64;c.nearClipPlane=.1f;c.farClipPlane=700;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.025f,.035f,.065f);
            bool fog=RenderSettings.fog;
            try{
                // Warm up the newly imported shaders before the first exported view.
                c.transform.position=new Vector3(5,3.4f,30);c.transform.LookAt(new Vector3(31,3,32));
                Capture(c,new Vector3(5,3.4f,30),new Vector3(31,3,32),"01_Entrance");
                Capture(c,new Vector3(33,13,22),new Vector3(63,6,40),"02_GlassChasm");
                Capture(c,new Vector3(139,17,67),new Vector3(158,14,79),"03_RitualHall");
                Capture(c,new Vector3(199,18,35),new Vector3(218,11,49),"04_ChainSwings");
                Capture(c,new Vector3(238,18,73),new Vector3(264,15,73),"05_CrusherGallery");
                RenderSettings.fog=false;Capture(c,new Vector3(150,180,-130),new Vector3(150,2,65),"06_Overview");
            }finally{RenderSettings.fog=fog;Object.DestroyImmediate(go);}
        }
        static void Capture(Camera c,Vector3 from,Vector3 to,string name)
        {
            c.transform.position=from;c.transform.LookAt(to);var rt=new RenderTexture(1600,900,24);var previous=RenderTexture.active;
            try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;var tex=new Texture2D(1600,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,900),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+name+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);}
            finally{c.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}
