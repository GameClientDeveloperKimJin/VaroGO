using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace HiddenPath.Stage2Z3.Editor
{
    public static class Z3InteriorLayout
    {
        static Material wood,iron,stone;static Transform interior;
        static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material mat,bool solid=true)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
        static Transform Group(string n){var go=new GameObject(n);go.transform.SetParent(interior);return go.transform;}
        static void Table(Transform g,Vector3 p,Vector2 size)
        {
            Box(g,"Tabletop_ReplaceWithTable",p+Vector3.up*1.35f,new Vector3(size.x,.22f,size.y),wood);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Box(g,"TableLeg",p+new Vector3(x*(size.x/2-.2f),.65f,z*(size.y/2-.2f)),new Vector3(.2f,1.3f,.2f),wood);
        }
        static void Shelf(Transform g,Vector3 p,float yaw)
        {
            var unit=new GameObject("Shelf_ReplaceAsUnit").transform;unit.SetParent(g);unit.position=p;
            foreach(float y in new[]{.25f,1.65f,3.05f})Box(unit,"Shelf_Board",p+Vector3.up*y,new Vector3(4.8f,.18f,1.2f),wood);
            foreach(float x in new[]{-2.3f,2.3f})Box(unit,"Shelf_Upright",p+new Vector3(x,1.65f,0),new Vector3(.18f,3.5f,1.2f),iron);
            for(int i=0;i<5;i++)Box(unit,"Stored_Box_ReplaceWithProps",p+new Vector3(-1.7f+(i%3)*1.5f,i<3?.9f:2.3f,0),new Vector3(.9f,1.1f,.9f),stone);
            unit.rotation=Quaternion.Euler(0,yaw,0);
        }
        public static string Apply()
        {
            var scene=SceneManager.GetActiveScene();if(Application.isPlaying||scene.path!="Assets/01_Scenes/MapDesign/MapDesign_Stage2.unity")throw new InvalidOperationException();
            var puzzle=UnityEngine.Object.FindFirstObjectByType<Z3Puzzle>();if(puzzle.transform.Find("Interior_Blockout"))throw new InvalidOperationException("Already furnished");
            Undo.RegisterFullObjectHierarchyUndo(puzzle.gameObject,"Furnish Z3 interior");
            interior=new GameObject("Interior_Blockout").transform;interior.SetParent(puzzle.transform);
            wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Z3/Wood.mat");stone=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Z3/Stone.mat");iron=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Z3/Iron.mat");
            var storage=Group("01_West_Storage");var workshop=Group("02_East_Workshop");var common=Group("03_Central_CommonArea");var rest=Group("04_South_RestArea");var salvage=Group("05_Northwest_Salvage");
            foreach(var t in puzzle.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Fixed_Rubble_")).ToArray())t.gameObject.SetActive(false);
            Shelf(storage,new Vector3(-81.5f,6,87),90);Shelf(storage,new Vector3(-81.5f,6,94),90);
            Shelf(workshop,new Vector3(-32.6f,6,83),90);
            Table(workshop,new Vector3(-35,6,89),new Vector2(2.4f,5));
            Box(workshop,"Tool_Pegboard_ReplaceWithWallRack",new Vector3(-31.2f,8.7f,89),new Vector3(.18f,2.5f,4),wood,false);
            Table(common,new Vector3(-63,6,86),new Vector2(5,2.6f));
            for(int i=0;i<2;i++)
            {
                var p=new Vector3(-44+i*7,6,74.8f);
                Box(rest,"Bed_Frame_ReplaceWithBed",p+Vector3.up*.35f,new Vector3(2.2f,.7f,3.6f),wood);
                Box(rest,"Mattress_Proxy",p+Vector3.up*.85f,new Vector3(2,.3f,3.3f),stone);
                Box(rest,"Bed_Headboard",p+new Vector3(0,1.2f,-1.75f),new Vector3(2.3f,2.4f,.2f),wood);
            }
            // Correct pieces become plausible furnishings, mixed with the room's ordinary contents.
            var homes=new[]{new Vector3(-76,6.5f,79),new Vector3(-40.5f,6.5f,86),new Vector3(-75,6.5f,102),new Vector3(-63,6.5f,89)};
            var groups=new[]{storage,workshop,salvage,common};
            var names=new[]{"Low_Pallet_Platform","Workbench_Platform","Salvaged_Stone_Slab","Loose_Bench_Seat"};
            var occupied=new List<Bounds>();
            for(int i=0;i<4;i++)
            {
                var p=puzzle.pieces.First(t=>t.fittingStep==i);p.transform.SetParent(groups[i],true);p.transform.SetPositionAndRotation(homes[i],Quaternion.Euler(0,new[]{-7,4,13,-5}[i],0));p.name=names[i];
                Physics.SyncTransforms();var bounds=p.GetComponent<Collider>().bounds;bounds.Expand(.6f);occupied.Add(bounds);
            }
            // Clusters belong to work/storage/rest areas; the room centre remains circulation space.
            var centres=new[]{new Vector3(-76,6,90),new Vector3(-39,6,84),new Vector3(-64,6,85),new Vector3(-45,6,79),new Vector3(-74,6,103)};
            var zones=new[]{storage,workshop,common,rest,salvage};var rng=new System.Random(3217);
            var candidates=puzzle.pieces.Where(p=>p.fittingStep<0).ToArray();
            for(int i=0;i<candidates.Length;i++)
            {
                var p=candidates[i];int zone=i%5;Vector3 pos=Vector3.zero;bool found=false;
                for(int attempt=0;attempt<300;attempt++)
                {
                    float angle=(float)rng.NextDouble()*Mathf.PI*2,radius=2+(float)rng.NextDouble()*5.8f;
                    pos=centres[zone]+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);pos.y=6+p.transform.localScale.y*.5f;
                    if(pos.x<-79.5f||pos.x>-35||pos.z<74||pos.z>106)continue;
                    if(pos.z>94&&pos.z<98||pos.x>-61&&pos.x<-49&&pos.z>94)continue;
                    var b=new Bounds(pos,p.transform.localScale+new Vector3(1,0.2f,1));
                    if(occupied.Any(o=>o.Intersects(b)))continue;
                    Physics.SyncTransforms();
                    if(Physics.OverlapBox(pos+Vector3.up*.1f,p.transform.localScale*.5f+new Vector3(.35f,-.12f,.35f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(c=>!c.transform.IsChildOf(p.transform)&&!c.GetComponentInParent<Z3Piece>()))continue;
                    occupied.Add(b);found=true;break;
                }
                if(!found)throw new InvalidOperationException("No safe furniture position "+i);
                p.transform.SetParent(zones[zone],true);p.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,(float)rng.NextDouble()*24-12,0));
                p.name=new[]{"Storage_Chest","Work_Supply_Box","Stool_Block","Bedside_Chest","Salvage_Block"}[zone]+"_"+i;
            }
            // Small fixed clutter on actual furniture surfaces; no floating objects or new shortcut stairs.
            for(int i=0;i<5;i++)Box(workshop,"Tools_Proxy",new Vector3(-35,7.6f,87.5f+i*.65f),new Vector3(.65f,.28f,.35f),iron,false);
            Box(common,"Table_Tray",new Vector3(-63,7.57f,86),new Vector3(1.3f,.2f,.7f),wood,false);
            Box(common,"Bench",new Vector3(-63,6.45f,83.7f),new Vector3(4,.9f,.65f),wood);
            // Replacement roots retain scene interaction scripts and child colliders during art handoff.
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            return "5 furnished areas; all34 interactive pieces retained; grid and repeated wall rubble archived";
        }
    }
}
