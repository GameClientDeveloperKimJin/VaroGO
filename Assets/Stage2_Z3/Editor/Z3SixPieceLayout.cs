using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace HiddenPath.Stage2Z3.Editor
{
    public static class Z3SixPieceLayout
    {
        public static string Apply()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(Application.isPlaying||scene.path!="Assets/01_Scenes/MapDesign/MapDesign_Stage2.unity")throw new InvalidOperationException();
            var p=UnityEngine.Object.FindFirstObjectByType<Z3Puzzle>();if(p.transform.Find("Six_Piece_Revision"))throw new InvalidOperationException("Already applied");
            Undo.RegisterFullObjectHierarchyUndo(p.gameObject,"Six-piece high crawl puzzle");
            var movedNames=new[]{"Upper_Search_Gallery","North_Wall","East_Upper_North","East_Upper_South","Crawl_Front_West","Crawl_Front_Middle","Crawl_Front_East","Crawl_Tunnel_Roof","Crawl_Outer_Lip","Rescue_Outer_Lip","Rescue_Grille","Reunion_Exit_Gate","Search_Partition_A","Search_Partition_B","Rope_Pickup","Rope_Anchor","Rope_Winch","Rescue_End","Upper_Supply_Shelf","Gallery_Light"};
            foreach(Transform t in p.transform)if(movedNames.Contains(t.name))t.position+=Vector3.up*2;
            var retaining=p.transform.Find("Retaining_Face");retaining.position+=Vector3.up;retaining.localScale+=Vector3.up*2;
            var west=p.transform.Find("West_Wall");west.position+=Vector3.up;west.localScale+=Vector3.up*2;
            p.ropeLine.transform.position+=Vector3.up;p.ropeLine.transform.localScale+=Vector3.up*2;
            var stage=p.transform.parent;
            foreach(var t in stage.GetComponentsInChildren<Transform>(true))
                if(t.name=="Return_Balcony"||t.name=="Flagstones_Return_Balcony"||t.name=="Upper_Exit_Bridge"||t.name=="Upper_Exit_Parapet"||t.name=="Upper_Exit_Trim"){Undo.RecordObject(t,"Raise Z3 outlet");t.position+=Vector3.up*2;}
            var slots=new Transform[6];
            for(int i=0;i<6;i++)
            {
                var slot=i<4?p.slots[i]:new GameObject("Step_Socket_"+(i+1)).transform;slot.SetParent(p.transform);slot.position=new Vector3(-55,6.5f+i,102.6f+i*.7f);slots[i]=slot;
                Z3Piece piece;
                if(i<4)piece=p.pieces.First(x=>x.fittingStep==i);
                else
                {
                    var g=GameObject.CreatePrimitive(i==4?PrimitiveType.Cube:PrimitiveType.Cylinder);g.name=i==4?"Red_Only_Rune_Slab":"Blue_Only_Rune_Drum";g.transform.SetParent(p.transform.Find("Interior_Blockout"));
                    g.transform.position=new Vector3(i==4?-70:-43,6.5f,91);piece=g.AddComponent<Z3Piece>();piece.fittingStep=i;piece.visibleRole=i==4?0:1;
                    var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage2_Z2/Materials/"+(i==4?"Red":"Blue")+".mat");g.GetComponent<Renderer>().sharedMaterial=mat;
                }
                piece.transform.localScale=new Vector3(6.4f-i*.6f,i==5?.5f:1,8.8f-i*1.4f);
                if(i==0){piece.transform.position=new Vector3(-55,6.5f,85);piece.transform.rotation=Quaternion.identity;piece.name="Heavy_Base_TwoPlayerPush";piece.needsTwo=true;}
                EditorUtility.SetDirty(piece);
            }
            p.slots=slots;p.bracePoint.position=new Vector3(-55,12.02f,106);p.assemblyPoint=new Vector3(-55,6,96.5f);
            var mark=p.transform.Find("Assembly_Foundation");mark.position=new Vector3(-55,6.035f,102.6f);mark.localScale=new Vector3(6.6f,.06f,9);
            p.pieces=p.GetComponentsInChildren<Z3Piece>();
            Physics.SyncTransforms();
            // Preserve furnishings while opening a physical corridor for the wide base and both pushers.
            foreach(var piece in p.pieces.Where(x=>x.fittingStep<0))
            {
                var colliders=piece.GetComponentsInChildren<Collider>();var b=colliders[0].bounds;foreach(var c in colliders)b.Encapsulate(c.bounds);
                bool corridor=b.Intersects(new Bounds(new Vector3(-55,7,94),new Vector3(8,3,29)));
                bool conflict=p.pieces.Where(x=>x.fittingStep>=0).Any(x=>x.GetComponentsInChildren<Collider>().Any(c=>b.Intersects(c.bounds)));
                if(!corridor&&!conflict)continue;
                var original=piece.transform.position;bool found=false;
                for(int row=0;row<10&&!found;row++)for(int col=0;col<6&&!found;col++)
                {
                    var dest=new Vector3(-79+col*3.5f,original.y,75+row*3.1f);piece.transform.position=dest;Physics.SyncTransforms();var bb=colliders[0].bounds;
                    if(Physics.OverlapBox(bb.center,bb.extents+new Vector3(.3f,-.08f,.3f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(c=>!c.transform.IsChildOf(piece.transform)))continue;
                    found=true;
                }
                if(!found)throw new InvalidOperationException("Cannot clear "+piece.name);
            }
            new GameObject("Six_Piece_Revision").transform.SetParent(p.transform);EditorUtility.SetDirty(p);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Six pieces, 15.2m entrance, 12m final step, two-player push base";
        }
    }
}
