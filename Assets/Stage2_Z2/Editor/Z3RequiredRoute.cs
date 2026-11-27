using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace HiddenPath.Stage2Z2.Editor
{
    // Scene-only blockout migration. Does not pretend static crate/rope proxies are gameplay.
    public static class Z3RequiredRoute
    {
        static Transform root;static Material stone,iron,gold;
        static GameObject Box(string n,Vector3 pos,Vector3 size,Material material,bool solid=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(root);g.transform.position=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        public static string Apply()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.path!="Assets/01_Scenes/MapDesign/MapDesign_Stage2.unity")throw new InvalidOperationException("Target scene only");
            var stage=GameObject.Find("Stage02_Rough_20261127").transform;
            if(stage.Find("Z3_RequiredRoute"))throw new InvalidOperationException("Already applied");
            var z3=stage.Find("Z3_Optional_StackAndRescue");
            stone=z3.Find("CrawlWall_Left").GetComponent<Renderer>().sharedMaterial;
            iron=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Z2/Materials/Iron.mat");gold=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Z2/Materials/Gold.mat");
            foreach(var t in stage.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="To_Z4"||t.name=="Optional_Return_Ramp"||t.name=="Flagstones_To_Z4"||t.name=="Flagstones_Optional_Return_Ramp"||t.name.StartsWith("Z4_MainRoute_")||t.name.Contains("OPTIONAL Z3"))
                {Undo.RecordObject(t.gameObject,"Archive optional Z3 bypass");t.gameObject.SetActive(false);}
            }
            root=new GameObject("Z3_RequiredRoute").transform;root.SetParent(stage);Undo.RegisterCreatedObjectUndo(root.gameObject,"Mandatory Z3 route");
            // Full-height front closure turns the Z2 arrival deck toward the west branch.
            Box("Z2_Forward_SealedWall",new Vector3(0,10.5f,102.6f),new Vector3(35,11,1.2f),stone);
            // Keep the branch sides enclosed; only its western end leads into the lower room.
            Box("West_Link_NorthWall",new Vector3(-24,10.5f,101.1f),new Vector3(14,9,1.2f),stone);
            Box("West_Link_SouthWall",new Vector3(-24,10.5f,94.9f),new Vector3(14,9,1.2f),stone);
            // Close the lower room's open eastern edge except for the actual entrance.
            Box("Lower_East_South",new Vector3(-30.4f,11,90.25f),new Vector3(1.2f,10,8.5f),stone);
            Box("Lower_East_North",new Vector3(-30.4f,11,106),new Vector3(1.2f,10,9),stone);
            // Replace the reversible incline with a high one-way drop bridge.
            Box("Upper_Exit_Bridge",new Vector3(-15.25f,12.6f,118),new Vector3(15.5f,1.2f,5),stone);
            for(int s=-1;s<=1;s+=2)
            {
                Box("Upper_Exit_Parapet",new Vector3(-15.25f,14.1f,118+s*2.6f),new Vector3(15.5f,1.8f,.4f),stone);
                Box("Upper_Exit_Trim",new Vector3(-15.25f,15.03f,118+s*2.6f),new Vector3(15.5f,.12f,.5f),iron,false);
            }
            Box("Drop_Landing_Marker",new Vector3(-5,6.05f,118),new Vector3(4,.08f,4),gold,false);
            // Physical arrow strips at floor level; no developer labels in the game view.
            foreach(float x in new[]{5f,-3f,-11f,-22f})
            {
                Box("To_Storage_Arrow_Shaft",new Vector3(x,6.09f,98),new Vector3(1.8f,.08f,.18f),gold,false);
                foreach(int s in new[]{-1,1})Box("To_Storage_Arrow_Head",new Vector3(x-.6f,6.09f,98+s*.25f),new Vector3(.8f,.08f,.16f),gold,false).transform.Rotate(0,s*40,0);
            }
            var light= new GameObject("Z3_Entry_WarmLight");light.transform.SetParent(root);light.transform.position=new Vector3(-29,10,98);var l=light.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(1,.65f,.3f);l.range=14;l.intensity=4;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return Audit();
        }
        public static string Audit()
        {
            Physics.SyncTransforms();RaycastHit hit;
            bool blocked=Physics.Raycast(new Vector3(0,7,99),Vector3.forward,out hit,8)&&hit.collider.name=="Z2_Forward_SealedWall";
            bool westClear=!Physics.CheckCapsule(new Vector3(-30.8f,6.5f,98),new Vector3(-30.8f,7.5f,98),.35f,~0,QueryTriggerInteraction.Ignore);
            bool highBridge=Physics.Raycast(new Vector3(-16,16,118),Vector3.down,out hit,6)&&Mathf.Abs(hit.point.y-13.2f)<.02f;
            bool landing=Physics.Raycast(new Vector3(-5,10,118),Vector3.down,out hit,6)&&Mathf.Abs(hit.point.y-6)<.2f;
            bool noRamp=!GameObject.Find("Optional_Return_Ramp");
            var report="Direct route blocked="+blocked+"; lower entrance capsule clear="+westClear+"; upper bridge top13.2="+highBridge+"; Z4 landing top6="+landing+"; old ramp inactive="+noRamp+"; reverse jump height=7.2m > player jump1.6m. Crate transport, rope rescue and two-player completion remain blockout placeholders.";
            System.IO.File.WriteAllText("Captures/Stage2_Z3/route-audit.txt",report);return report;
        }
    }
}
