using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace HiddenPath.Stage2Z2.Editor
{
    public static class Z2DynamicLayout
    {
        static Material stone,iron,gold;
        public static string WidenGaps()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.path!="Assets/01_Scenes/MapDesign/MapDesign_Stage2.unity")throw new InvalidOperationException("Target scene only");
            var seq=Object.FindFirstObjectByType<Z2Sequence>();
            if(seq.transform.Find("Precision_Jump_Spacing"))throw new InvalidOperationException("Already applied");
            Undo.RegisterFullObjectHierarchyUndo(seq.gameObject,"Widen Z2 landing gaps");
            for(int side=0;side<2;side++)for(int i=0;i<6;i++)
            {
                var p=(side==0?seq.redPlatforms:seq.bluePlatforms)[i];
                p.transform.localScale=new Vector3(.85f,1,.85f);
                var lane=p.transform.parent;var pillar=lane.Find("WaitStone_"+(i+1));
                var wait=Z2CoopBuilder.WaitPosition(i,side);var delta=new Vector3(wait.x-pillar.position.x,0,0);
                pillar.position+=delta;pillar.localScale=new Vector3(1.94f,pillar.localScale.y,1.94f);
                var coping=lane.GetComponentsInChildren<Transform>().Where(t=>t.name=="WaitStone_Coping").OrderBy(t=>t.position.z).ElementAt(i);
                coping.position+=delta;coping.localScale=new Vector3(1.98f,coping.localScale.y,1.98f);
                foreach(Transform d in seq.transform.Find("Dynamic_Terraces"))
                {
                    if((d.name=="Terrace_Buttress"||d.name=="Iron_Masonry_Band")&&Mathf.Abs(d.position.z-wait.z)<.5f&&Mathf.Sign(d.position.x)==(side==0?-1:1))
                    {d.position+=delta;d.localScale=Vector3.Scale(d.localScale,new Vector3(.88f,1,.88f));}
                }
            }
            new GameObject("Precision_Jump_Spacing").transform.SetParent(seq.transform);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Special footprints 85%, landings 88%, lateral separation +0.3~0.4m";
        }
        static GameObject Box(Transform root,string name,Vector3 p,Vector3 size,Material mat,bool solid=false)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root);g.transform.position=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;
            if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        public static string Apply()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.path!="Assets/01_Scenes/MapDesign/MapDesign_Stage2.unity")throw new InvalidOperationException("Target scene only");
            var seq=Object.FindFirstObjectByType<Z2Sequence>();
            if(seq.transform.Find("Dynamic_Terraces"))throw new InvalidOperationException("Already applied; retain manual edits");
            Undo.RegisterFullObjectHierarchyUndo(seq.gameObject,"Z2 dynamic terraces");
            var decor=new GameObject("Dynamic_Terraces").transform;decor.SetParent(seq.transform);
            stone=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Z2/Materials/Stone.mat");iron=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Z2/Materials/Iron.mat");gold=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Z2/Materials/Gold.mat");
            for(int side=0;side<2;side++)for(int i=0;i<6;i++)
            {
                float sign=side==0?-1:1;
                var p=(side==0?seq.redPlatforms:seq.bluePlatforms)[i];p.transform.position=Z2CoopBuilder.SpecialPosition(i,side);
                p.travel=p.motion==Z2Motion.Vertical?Vector3.up*4: p.motion==Z2Motion.Horizontal?Quaternion.Inverse(p.transform.rotation)*Vector3.right*(-sign*9):Vector3.zero;
                p.moveSeconds=p.motion==Z2Motion.Vertical?3.2f:4.5f;p.pauseSeconds=1.6f;
                var wait=Z2CoopBuilder.WaitPosition(i,side);var lane=p.transform.parent;
                var pillar=lane.Find("WaitStone_"+(i+1));pillar.position=new Vector3(wait.x,(wait.y-6)/2,wait.z);pillar.localScale=new Vector3(2.2f,wait.y+6,2.2f);
                var coping=lane.GetComponentsInChildren<Transform>().Where(t=>t.name=="WaitStone_Coping").OrderBy(t=>t.position.z).ElementAt(i);coping.position=wait-Vector3.up*.12f;
                // Buttresses, staggered masonry bands and broken side spurs emphasize the actual height.
                for(int tier=0;tier<3;tier++)
                {
                    float y=wait.y-1.2f-tier*2.4f;
                    var block=Box(decor,"Terrace_Buttress",new Vector3(wait.x+sign*.8f,y,wait.z+.35f),new Vector3(2.7f-tier*.3f,1.4f,2.3f),stone);
                    block.transform.Rotate(0,sign*(tier*7-7),0);
                    Box(decor,"Iron_Masonry_Band",new Vector3(wait.x,y-.65f,wait.z),new Vector3(2.3f,.12f,2.3f),iron);
                }
                if(p.motion==Z2Motion.Vertical)
                {
                    for(int s=-1;s<=1;s+=2)
                    {
                        var basePos=p.transform.position+new Vector3(s*1.7f,1.5f,-1.65f);
                        Box(decor,"Lift_Guide_Tower",basePos,new Vector3(.3f,8f,.4f),iron);
                        for(int j=0;j<5;j++)Box(decor,"Lift_Height_Marker",basePos+new Vector3(0,j-2,0),new Vector3(.42f,.12f,.48f),gold);
                    }
                }
                if(p.motion==Z2Motion.Horizontal)
                {
                    var center=p.transform.position+Vector3.right*(-sign*4.5f)-Vector3.up*1.1f;
                    Box(decor,"Shuttle_Suspended_Rail",center,new Vector3(12,.25f,.3f),iron);
                    foreach(float x in new[]{p.transform.position.x,p.transform.position.x-sign*9})
                        Box(decor,"Shuttle_Stop",new Vector3(x,center.y,center.z),new Vector3(.35f,.65f,.7f),gold);
                }
                EditorUtility.SetDirty(p);
            }
            // Broken stone ribs remain below the traversable route, never becoming substitute steps.
            for(int s=-1;s<=1;s+=2)for(int i=0;i<5;i++)
            {var g=Box(decor,"Broken_Chasm_Rib",new Vector3(s*(14.8f-i%2),-1+i%3,69+i*5),new Vector3(2,5+i%3,2.6f),stone);g.transform.Rotate(0,i*17, s*9);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            return "Z2: two 4m lifts and two 9m shuttles per role, terraces up to 11.2m";
        }
        public static string AddEntryRecovery()
        {
            if(Application.isPlaying||SceneManager.GetActiveScene().path!="Assets/01_Scenes/MapDesign/MapDesign_Stage2.unity")throw new InvalidOperationException();
            var seq=Object.FindFirstObjectByType<Z2Sequence>();
            if(seq.GetComponentInChildren<Z2EntryLift>())return "Already present";
            stone=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Z2/Materials/Stone.mat");
            for(int sign=-1;sign<=1;sign+=2)
            {
                var g=new GameObject("Neutral_Entry_Lift");g.transform.SetParent(seq.transform);g.transform.position=new Vector3(sign*14,2.2f,68);
                var p=g.AddComponent<Z2Platform>();p.motion=Z2Motion.Vertical;p.travel=Vector3.up*4.2f;p.moveSeconds=3.2f;p.pauseSeconds=1.6f;
                var surface=g.AddComponent<BoxCollider>();surface.center=new Vector3(0,-.25f,0);surface.size=new Vector3(2.4f,.5f,2.4f);p.surface=surface;p.surfaces=new[]{surface};
                var deck=Box(g.transform,"Stone_Lift_Deck",g.transform.position-Vector3.up*.25f,surface.size,stone);p.visible=deck.transform;
                g.AddComponent<Z2EntryLift>();
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());return "Neutral entry lifts added";
        }
    }
}
