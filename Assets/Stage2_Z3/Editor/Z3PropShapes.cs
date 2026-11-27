using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace HiddenPath.Stage2Z3.Editor
{
    public static class Z3PropShapes
    {
        static GameObject Part(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material,bool solid)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            if(solid){var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;collider.convex=true;}
            return go;
        }
        public static string Apply()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(Application.isPlaying||scene.path!="Assets/01_Scenes/MapDesign/MapDesign_Stage2.unity")throw new InvalidOperationException();
            var puzzle=UnityEngine.Object.FindFirstObjectByType<Z3Puzzle>();if(puzzle.transform.Find("Mixed_Primitive_Revision"))return "Already applied";
            Undo.RegisterFullObjectHierarchyUndo(puzzle.gameObject,"Mixed interior primitives");
            var iron=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Z3/Iron.mat");
            int cylinders=0,spheres=0,capsules=0;var props=puzzle.pieces.Where(p=>p.fittingStep<0).ToArray();
            for(int i=0;i<props.Length;i++)
            {
                if(i%4==3)continue;var p=props[i];var mat=p.GetComponent<Renderer>().sharedMaterial;
                p.GetComponent<Renderer>().enabled=false;UnityEngine.Object.DestroyImmediate(p.GetComponent<BoxCollider>());
                if(i%4==0)
                {
                    p.name="Barrel_"+p.name;Part(p.transform,"Barrel_Body",PrimitiveType.Cylinder,Vector3.zero,new Vector3(1,.5f,1),mat,true);
                    foreach(float y in new[]{-.33f,.33f})Part(p.transform,"Iron_Hoop",PrimitiveType.Cylinder,new Vector3(0,y,0),new Vector3(1.03f,.025f,1.03f),iron,false);cylinders++;
                }
                else if(i%4==1){p.name="Rounded_Stone_"+p.name;Part(p.transform,"Rounded_Stone",PrimitiveType.Sphere,Vector3.zero,Vector3.one,mat,true);spheres++;}
                else{p.name="Supply_Sack_"+p.name;Part(p.transform,"Sack_Body",PrimitiveType.Capsule,Vector3.zero,new Vector3(.9f,.5f,.9f),mat,true);capsules++;}
            }
            new GameObject("Mixed_Primitive_Revision").transform.SetParent(puzzle.transform);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            return "Converted: barrels="+cylinders+", rounded stones="+spheres+", sacks="+capsules+"; four staircase pieces retain flat load-bearing tops";
        }
    }
}
