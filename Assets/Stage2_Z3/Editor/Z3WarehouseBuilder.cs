using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace HiddenPath.Stage2Z3.Editor
{
    public static class Z3WarehouseBuilder
    {
        static Transform root;static Material stone,wood,iron,gold;
        static GameObject Box(string name,Vector3 p,Vector3 size,Material mat,bool solid=true)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root);g.transform.position=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
        static Transform Point(string n,Vector3 p){var g=new GameObject(n);g.transform.SetParent(root);g.transform.position=p;return g.transform;}
        static Material Mat(string name,Color color)
        {string path="Assets/Stage2_Z3/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;AssetDatabase.CreateAsset(m,path);}return m;}
        public static string Build()
        {
            var s=SceneManager.GetActiveScene();if(Application.isPlaying||s.path!="Assets/01_Scenes/MapDesign/MapDesign_Stage2.unity")throw new InvalidOperationException();
            var stage=GameObject.Find("Stage02_Rough_20261127").transform;if(stage.Find("Z3_Warehouse_Puzzle"))throw new InvalidOperationException("Already built");
            var old=stage.Find("Z3_Optional_StackAndRescue");
            foreach(Transform t in old)if(t.name!="Return_Balcony"){Undo.RecordObject(t.gameObject,"Archive Z3 blockout");t.gameObject.SetActive(false);}
            foreach(var t in stage.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="Z3_Storage_And_UpperGallery"||t.name=="Flagstones_Lower_Storage"||t.name=="Flagstones_Upper_Room"||t.name.StartsWith("Masonry_CrawlWall")||t.name=="Masonry_West_Wall"||t.name=="Masonry_South_Wall"||t.name=="Lower_East_South"||t.name=="Lower_East_North"||(t.name.Contains("Ceiling")&&t.GetComponent<Renderer>()&&t.position.x<-30))
                {Undo.RecordObject(t.gameObject,"Archive Z3 visual");t.gameObject.SetActive(false);}
            }
            root=new GameObject("Z3_Warehouse_Puzzle").transform;root.SetParent(stage);Undo.RegisterCreatedObjectUndo(root.gameObject,"Expanded Z3 warehouse");
            stone=Mat("Stone",new Color(.28f,.27f,.33f));wood=Mat("Wood",new Color(.40f,.24f,.12f));iron=Mat("Iron",new Color(.13f,.15f,.19f));gold=Mat("Rope",new Color(.85f,.61f,.25f));
            var puzzle=root.gameObject.AddComponent<Z3Puzzle>();puzzle.playerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Client/Player.prefab");
            Box("Lower_52x38",new Vector3(-57,5.4f,91),new Vector3(52,1.2f,38),stone);
            Box("Upper_Search_Gallery",new Vector3(-57,12.6f,121),new Vector3(52,1.2f,22),stone);
            Box("West_Wall",new Vector3(-83.6f,15,102),new Vector3(1.2f,18,60),stone);
            Box("South_Wall",new Vector3(-57,12,71.4f),new Vector3(52,12,1.2f),stone);
            Box("North_Wall",new Vector3(-57,18,132.6f),new Vector3(52,10,1.2f),stone);
            Box("East_Lower_South",new Vector3(-30.4f,12,83.5f),new Vector3(1.2f,12,23),stone);
            Box("East_Lower_North",new Vector3(-30.4f,15,106),new Vector3(1.2f,18,10),stone);
            Box("East_Upper_North",new Vector3(-30.4f,18,126.5f),new Vector3(1.2f,10,13),stone);
            Box("East_Upper_South",new Vector3(-30.4f,18,113),new Vector3(1.2f,10,6),stone);
            // Continuous sheer retaining wall. The only openings are crawl entrance and rope rescue dock.
            Box("Retaining_Face",new Vector3(-57,9.6f,110),new Vector3(52,7.2f,1),stone);
            Box("Crawl_Front_West",new Vector3(-69.7f,18,110),new Vector3(26.6f,9.6f,1),stone);
            Box("Crawl_Front_Middle",new Vector3(-45.55f,18,110),new Vector3(16.1f,9.6f,1),stone);
            Box("Crawl_Front_East",new Vector3(-32.75f,18,110),new Vector3(3.5f,9.6f,1),stone);
            // 1.45m tunnel fits prone capsule1.28m, rejects standing capsule1.8m.
            Box("Crawl_Tunnel_Roof",new Vector3(-55,16.25f,110.5f),new Vector3(2.8f,3.2f,2),stone);
            Box("Crawl_Outer_Lip",new Vector3(-55,12.8f,108.9f),new Vector3(2.8f,.8f,1.2f),stone);
            Box("Rescue_Outer_Lip",new Vector3(-36,12.8f,108.6f),new Vector3(3,.8f,1.8f),stone);
            puzzle.rescueGate=Box("Rescue_Grille",new Vector3(-36,15.2f,110),new Vector3(3,4,.35f),iron);
            puzzle.exitGate=Box("Reunion_Exit_Gate",new Vector3(-30.5f,15.7f,118),new Vector3(.5f,5,4),iron);
            // Two turns hide the rope from the crawl entrance; the upper search has its own space.
            Box("Search_Partition_A",new Vector3(-62,16.2f,117),new Vector3(1,6,14),stone);
            Box("Search_Partition_B",new Vector3(-74,16.2f,123),new Vector3(16,6,1),stone);
            puzzle.ropeCoil=Box("Rope_Pickup",new Vector3(-76,13.65f,128),new Vector3(1.2f,.9f,1.2f),gold,false);
            puzzle.ropeAnchor=Point("Rope_Anchor",new Vector3(-36,13.2f,113));
            Box("Rope_Winch",new Vector3(-36,14,113.8f),new Vector3(1.4f,1.6f,.7f),gold,false);
            puzzle.rescueBottom=Point("Partner_Wait",new Vector3(-36,6.05f,107.8f));
            puzzle.rescueTop=Point("Rescue_End",new Vector3(-36,13.25f,112.2f));
            Box("Rescue_Floor_Mark",new Vector3(-36,6.035f,107.5f),new Vector3(3,.06f,3),gold,false);
            puzzle.ropeLine=Box("Deployed_Rope",new Vector3(-36,10.2f,108),new Vector3(.12f,8.4f,.12f),gold,false);puzzle.ropeLine.SetActive(false);
            puzzle.slots=new Transform[4];
            var sizes=new[]{new Vector3(5.2f,1,6),new Vector3(4.6f,1,4.6f),new Vector3(4,1,3.2f),new Vector3(3.4f,1,1.8f)};
            var homes=new[]{new Vector3(-74,6.5f,82),new Vector3(-42,6.5f,80),new Vector3(-77,6.5f,102),new Vector3(-66,6.5f,94)};
            for(int i=0;i<4;i++)
            {
                puzzle.slots[i]=Point("Step_Socket_"+(i+1),new Vector3(-55,6.5f+i,104+i*.7f));
                var g=Box("Stack_Block_"+(i+1),homes[i],sizes[i],wood);var p=g.AddComponent<Z3Piece>();p.fittingStep=i;p.needsTwo=i==0;
                // Iron edge detail travels with the piece.
                var band=Box("Carry_Band",homes[i]+Vector3.up*.51f,new Vector3(sizes[i].x,.04f,.14f),iron,false);band.transform.SetParent(g.transform,true);
            }
            Box("Assembly_Foundation",new Vector3(-55,6.035f,104),new Vector3(5.4f,.06f,6.2f),iron,false);
            puzzle.bracePoint=Point("Partner_Brace",new Vector3(-55,10.02f,106));
            var random=new System.Random(213);
            // Wide walkable aisles surround a dense but readable supply field.
            for(int row=0;row<5;row++)for(int col=0;col<7;col++)
            {
                var p=new Vector3(-79+col*6.3f,6,76+row*6.2f);
                if(Mathf.Abs(p.x+55)<5&&p.z>96)continue;
                if(homes.Any(h=>Vector2.Distance(new Vector2(h.x,h.z),new Vector2(p.x,p.z))<4))continue;
                var size=new Vector3(1.1f+(float)random.NextDouble()*2.4f,.45f+(float)random.NextDouble()*1.65f,1.1f+(float)random.NextDouble()*2);
                var g=Box("Supply_Candidate_"+row+"_"+col,p+Vector3.up*size.y*.5f,size,row%2==0?wood:stone);g.AddComponent<Z3Piece>();
            }
            for(int i=0;i<20;i++)
            {
                var p=new Vector3(i%2==0?-81:-33.5f,6.4f+(i%3)*.15f,75+(i/2)*3.2f);
                var g=Box("Fixed_Rubble_"+i,p,new Vector3(1.5f,.8f+(i%3)*.3f,1.5f),stone);g.transform.Rotate(0,i*23,0);
            }
            for(int i=0;i<7;i++)Box("Upper_Supply_Shelf",new Vector3(-80+i*6,14.6f,130.8f),new Vector3(3,2.8f,1),wood);
            for(int side=0;side<2;side++)for(int i=0;i<5;i++)
            {
                var p=new Vector3(side==0?-81:-33,10,77+i*7.5f);Box("Torch_Bracket",p,new Vector3(.4f,1,.6f),iron,false);
                var light=Point("Warm_Light",p+Vector3.up).gameObject.AddComponent<Light>();light.type=LightType.Point;light.range=14;light.intensity=4;light.color=new Color(1,.64f,.32f);
            }
            foreach(var p in new[]{new Vector3(-55,17,115),new Vector3(-76,17,128),new Vector3(-36,17,120)}){var l=Point("Gallery_Light",p).gameObject.AddComponent<Light>();l.type=LightType.Point;l.range=18;l.intensity=5;l.color=new Color(.65f,.6f,1);}
            puzzle.pieces=root.GetComponentsInChildren<Z3Piece>();
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);
            return "52x38m lower hall; "+puzzle.pieces.Length+" carry candidates; 20 rubble; 7 shelves; 4-step assembly -> partner shoulder -> crawl -> hidden rope -> rescue -> gated Z4";
        }
    }
}
