using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using VaroGO.Client;
using VaroGO.Shared;
using Fusion;
using Object=UnityEngine.Object;
namespace HiddenPath.Stage2Z2.Editor
{
    public static class Z2CoopBuilder
    {
        const string Target="Assets/01_Scenes/MapDesign/MapDesign_Stage2.unity";
        static Material stone,red,blue,metal,gold;
        public static Vector3 SpecialPosition(int i,int side)
        {return new Vector3((side==0?-1:1)*new[]{8f,12f,6f,10f,12f,6f}[i],new[]{2.3f,6.5f,6.8f,7f,11.2f,9.5f}[i],new[]{68f,73f,78f,83f,88f,93f}[i]);}
        public static Vector3 WaitPosition(int i,int side)
        {return new Vector3((side==0?-1:1)*new[]{12.4f,2.1f,9.3f,13.4f,2.1f,8.3f}[i],new[]{6.5f,6.5f,7f,11.2f,11.2f,7.5f}[i],SpecialPosition(i,side).z+2.4f);}
        static GameObject Box(string n,Transform t,Vector3 p,Vector3 size,Material m,bool solid=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(t);g.transform.position=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;
            if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        static Transform G(string n,Transform t,Vector3 p){var g=new GameObject(n);g.transform.SetParent(t);g.transform.position=p;return g.transform;}
        static Material Mat(string n,Color c)
        {string path="Assets/Stage2_Z2/Materials/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=c;return m;}
        [MenuItem("Tools/Hidden Path/Stage 02 Z2/Build cooperative crossing")]
        public static void Build()
        {
            var scene=SceneManager.GetActiveScene();if(scene.path!=Target||Application.isPlaying)throw new InvalidOperationException("Wrong scene");
            var root=GameObject.Find("Stage02_Rough_20261127").transform;
            if(root.Find("Z2_CooperativeCrossing"))throw new InvalidOperationException("Already built; preserve manual edits");
            Directory.CreateDirectory("Assets/Stage2_Z2/Materials");AssetDatabase.Refresh();
            stone=Mat("Stone",new Color(.39f,.36f,.42f));metal=Mat("Iron",new Color(.12f,.14f,.18f));red=Mat("Red",new Color(.85f,.11f,.10f));blue=Mat("Blue",new Color(.04f,.43f,.95f));gold=Mat("Gold",new Color(1,.67f,.16f));
            var old=root.Find("Z2_PartnerControlledPlatforms");
            foreach(Transform c in old)if(c.name.StartsWith("A_Route")||c.name.StartsWith("B_Route")||c.name.StartsWith("Small_Rest")){Undo.RecordObject(c.gameObject,"Archive Z2 static layout");c.gameObject.SetActive(false);}
            var art=root.Find("Dungeon_Spatial_Pass");
            if(art){var foundation=art.Find("Z2_Chasm_Foundations");if(foundation){Undo.RecordObject(foundation.gameObject,"Archive Z2 dressing");foundation.gameObject.SetActive(false);}
                foreach(var tr in art.GetComponentsInChildren<Transform>(true).Where(x=>x.name.StartsWith("Flagstones_Small_Rest"))){Undo.RecordObject(tr.gameObject,"Archive old rest skin");tr.gameObject.SetActive(false);}}
            var t=G("Z2_CooperativeCrossing",root,Vector3.zero);Undo.RegisterCreatedObjectUndo(t.gameObject,"Z2 cooperative crossing");
            var seq=t.gameObject.AddComponent<Z2Sequence>();seq.redPlatforms=new Z2Platform[6];seq.bluePlatforms=new Z2Platform[6];
            var net=t.gameObject.AddComponent<NetworkObject>();net.Flags|=NetworkObjectFlags.MasterClientObject;net.Flags&=~(NetworkObjectFlags.DestroyWhenStateAuthorityLeaves|NetworkObjectFlags.AllowStateAuthorityOverride);t.gameObject.AddComponent<Z2NetState>();
            seq.redStart=G("A_Start",t,new Vector3(-8,2.1f,63));seq.blueStart=G("B_Start",t,new Vector3(8,2.1f,63));
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;var color=side==0?red:blue;var lane=G(side==0?"A_Red_Lane":"B_Blue_Lane",t,Vector3.zero);
                for(int i=0;i<6;i++)
                {
                    var pos=new Vector3(sign*8,2.3f+i*.6f,68+i*4.8f);
                    var p=G((side==0?"R":"B")+(i+1)+"_"+(Z2Motion)(i%3),lane,pos);
                    var platform=p.gameObject.AddComponent<Z2Platform>();platform.sequence=seq;platform.role=side==0?PlayerRole.A_Red:PlayerRole.B_Blue;platform.index=i;platform.motion=(Z2Motion)(i%3);
                    platform.travel=i%3==0?Vector3.up: i%3==1?Vector3.right*sign*1.2f:Vector3.zero;
                    var surface=p.gameObject.AddComponent<BoxCollider>();surface.size=new Vector3(2.8f,.4f,2.8f);surface.center=new Vector3(0,-.2f,0);platform.surface=surface;
                    var trigger=G("Landing_Sensor",p,pos+Vector3.up*.18f).gameObject.AddComponent<BoxCollider>();trigger.size=new Vector3(2.7f,.5f,2.7f);trigger.isTrigger=true;
                    var vis=G("Active_Visual",p,pos);platform.visible=vis;
                    Box("Deck",vis,pos-Vector3.up*.2f,new Vector3(2.8f,.4f,2.8f),color,false);
                    for(int s=-1;s<=1;s+=2){Box("Rim",vis,pos+new Vector3(s*1.35f,.035f,0),new Vector3(.1f,.07f,2.8f),gold,false);Box("Rim",vis,pos+new Vector3(0,.035f,s*1.35f),new Vector3(2.8f,.07f,.1f),gold,false);}
                    platform.rim=Box("Activation_Indicator",vis,pos+Vector3.up*.04f,new Vector3(.7f,.08f,.7f),color,false).GetComponent<Renderer>();
                    // Raised arrows use a small number of cubes. No floating authoring labels.
                    if(i%3!=2)for(int s=-1;s<=1;s+=2)Box("Motion_Glyph",vis,pos+new Vector3(s*.2f,.095f,.3f),new Vector3(.12f,.06f,.6f),gold,false).transform.Rotate(0,s*40,0);
                    if(side==0)seq.redPlatforms[i]=platform;else seq.bluePlatforms[i]=platform;
                    var wait=new Vector3(sign*(8+(i%2==0?3:-3)),2.55f+i*.6f,70.4f+i*4.8f);
                    Box("WaitStone_"+(i+1),lane,new Vector3(wait.x,(wait.y-6)/2,wait.z),new Vector3(2.2f,wait.y+6,2.2f),stone);
                    Box("WaitStone_Coping",lane,wait-Vector3.up*.12f,new Vector3(2.25f,.24f,2.25f),stone);
                    if(i%3!=2)
                    {
                        var track=G("MotionTrack_"+(i+1),lane,Vector3.zero);
                        var midpoint=pos+platform.travel*.5f-Vector3.up*.7f;
                        Box("GuideRail",track,midpoint,i%3==0?new Vector3(.16f,1.8f,.16f):new Vector3(4,.16f,.16f),metal,false);
                    }
                }
            }
            var cp=G("CP_Z2_Entry",t,new Vector3(0,2.7f,63));var cpBox=cp.gameObject.AddComponent<BoxCollider>();cpBox.size=new Vector3(33,2,5);cpBox.isTrigger=true;
            var checkpoint=cp.gameObject.AddComponent<Checkpoint>();var so=new SerializedObject(checkpoint);so.FindProperty("order").intValue=1;so.FindProperty("spawnPoint").objectReferenceValue=G("Spawn",t,new Vector3(0,2.15f,63));so.ApplyModifiedPropertiesWithoutUndo();
            var kill=G("Fall_Reset",t,new Vector3(0,-3,80));var kc=kill.gameObject.AddComponent<BoxCollider>();kc.isTrigger=true;kc.size=new Vector3(36,3,36);kill.gameObject.AddComponent<Z2FallReset>().sequence=seq;
            for(int sign=-1;sign<=1;sign+=2)foreach(float z in new[]{66,80,94})
            {var light=G("Z2_CoolLight",t,new Vector3(sign*14,7,z)).gameObject.AddComponent<Light>();light.type=LightType.Point;light.range=13;light.intensity=5;light.color=new Color(.4f,.55f,1);}
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Selection.activeGameObject=t.gameObject;
        }
        public static void RefineLayout()
        {
            var scene=SceneManager.GetActiveScene();if(scene.path!=Target||Application.isPlaying)throw new InvalidOperationException("Wrong scene");
            var seq=Object.FindFirstObjectByType<Z2Sequence>();if(seq.transform.Find("Varied_Shape_Revision"))throw new InvalidOperationException("Already refined");
            G("Varied_Shape_Revision",seq.transform,Vector3.zero);
            metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Z2/Materials/Iron.mat");gold=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Z2/Materials/Gold.mat");
            for(int side=0;side<2;side++)for(int i=0;i<6;i++)
            {
                var p=(side==0?seq.redPlatforms:seq.bluePlatforms)[i];var lane=p.transform.parent;
                Undo.RecordObject(p.transform,"Varied Z2 placement");p.transform.position=SpecialPosition(i,side);
                var material=p.visible.Find("Deck").GetComponent<Renderer>().sharedMaterial;
                var parts=new System.Collections.Generic.List<BoxCollider>{p.surface};
                if(i%3!=2)
                {
                    var local=new Vector3(2.05f,-.2f,.7f);
                    var wing=Box("L_Wing",p.visible,p.transform.position+local,new Vector3(1.35f,.4f,1.4f),material,false);
                    var collider=G("L_Wing_Collision",p.transform,p.transform.position+local).gameObject.AddComponent<BoxCollider>();collider.size=new Vector3(1.35f,.4f,1.4f);parts.Add(collider);
                    var trigger=G("L_Wing_Sensor",p.transform,p.transform.position+new Vector3(2.05f,.18f,.7f)).gameObject.AddComponent<BoxCollider>();trigger.size=new Vector3(1.3f,.5f,1.35f);trigger.isTrigger=true;
                    Box("Wing_Edge",p.visible,p.transform.position+new Vector3(2.7f,.04f,.7f),new Vector3(.1f,.08f,1.4f),gold,false);
                }
                else
                {
                    p.surface.size=new Vector3(3.5f,.4f,2.8f);
                    var d=p.visible.Find("Deck");d.localScale=new Vector3(3.5f,.4f,2.8f);
                }
                p.surfaces=parts.ToArray();
                float yaw=new[]{0,180,25,90,270,-20}[i]*(side==0?1:-1);p.transform.rotation=Quaternion.Euler(0,yaw,0);
                if(p.motion==Z2Motion.Horizontal)p.travel=Quaternion.Inverse(p.transform.rotation)*Vector3.right*(side==0?-1.2f:1.2f);
                if(i==3)p.travel=Vector3.up*.7f;
                if(i==4)p.travel=Quaternion.Inverse(p.transform.rotation)*Vector3.right*(side==0?-.9f:.9f);
                var wait=WaitPosition(i,side);var pillar=lane.Find("WaitStone_"+(i+1));pillar.position=new Vector3(wait.x,(wait.y-6)/2,wait.z);pillar.localScale=new Vector3(2.2f,wait.y+6,2.2f);
                var coping=lane.GetComponentsInChildren<Transform>().Where(x=>x.name=="WaitStone_Coping").OrderBy(x=>x.position.z).ElementAt(i);
                coping.position=wait-Vector3.up*.12f;
                var track=lane.Find("MotionTrack_"+(i+1));if(track)track.gameObject.SetActive(false);
                EditorUtility.SetDirty(p);
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
