using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using VaroGO.Client;

namespace HiddenPath.Stage1.Editor
{
    public static class Stage1PlayableBuilder
    {
        const string ScenePath = "Assets/01_Scenes/MapDesign_Stage1.unity";
        static Transform root, env;
        static Transform[] zones;
        static Material grass, sand, stone, darkStone, wood, leaves, lime, purple, red, blue, cyan, gold, white, rose;
        static readonly List<RevealablePlatform> hidden = new List<RevealablePlatform>();
        static readonly List<Vector3> trees = new List<Vector3>();

        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage1_Blockout/Materials/" + name + ".mat");
        static Transform Group(string name, Transform parent)
        {
            var g = new GameObject(name); g.transform.SetParent(parent); return g.transform;
        }
        static GameObject Shape(string name, PrimitiveType type, Vector3 p, Vector3 scale, Material mat, Transform parent, bool solid = true)
        {
            var g = GameObject.CreatePrimitive(type); g.name = name; g.transform.SetParent(parent);
            g.transform.position = p; g.transform.localScale = scale; g.GetComponent<Renderer>().sharedMaterial = mat;
            if (!solid) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }
        static GameObject Box(string n, Vector3 p, Vector3 s, Material m, Transform t, bool solid = true) => Shape(n, PrimitiveType.Cube, p, s, m, t, solid);
        static void Set(Object target, string property, float value)
        {
            var so = new SerializedObject(target); var prop = so.FindProperty(property);
            if (prop == null) throw new System.Exception(target.name + ": missing " + property);
            if (prop.propertyType == SerializedPropertyType.Integer || prop.propertyType == SerializedPropertyType.Enum) prop.intValue = (int)value;
            else if (prop.propertyType == SerializedPropertyType.Boolean) prop.boolValue = value != 0;
            else prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Ref(Object target, string property, Object value)
        {
            var so = new SerializedObject(target); so.FindProperty(property).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
        }
        static GameObject Prefab(string name, Vector3 p, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Client/" + name + ".prefab");
            if (asset == null) throw new System.Exception("Missing prefab " + name);
            var g = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent); g.transform.position = p; return g;
        }
        // Broad land masses, with a level collision surface and uneven rock silhouettes below it.
        static void Land(string name, Vector3 surface, float width, float depth, Transform parent)
        {
            var group = Group(name, parent);
            Box("Solid_Cliff", new Vector3(surface.x, (surface.y - 14 - .6f) / 2, surface.z), new Vector3(width, surface.y + 14 - .6f, depth), darkStone, group);
            Box("Walkable_Grass", surface - Vector3.up * .3f, new Vector3(width, .6f, depth), grass, group);
            for (int i = 0; i < 6; i++)
            {
                float x = surface.x - width * .45f + width * .9f * i / 5;
                float z = surface.z + (i % 2 == 0 ? depth * .48f : -depth * .48f);
                var b = Shape("Rock_Face", PrimitiveType.Sphere, new Vector3(x, surface.y - 5, z), new Vector3(width / 4, 12, 6), i % 2 == 0 ? stone : darkStone, group, false);
                b.transform.rotation = Quaternion.Euler(0, i * 29, 8);
            }
        }
        // Top endpoints are used so slopes have no raised lips at their joins.
        static void Walk(Vector3 a, Vector3 b, float width, Transform parent, bool timber = false)
        {
            var p = Group(timber ? "Timber_Bridge" : "Walkable_Trail", parent);
            var d = (b - a).normalized;
            var g = Box("Ramp_Surface", (a + b) * .5f - Vector3.up * .25f, new Vector3(width, .5f, Vector3.Distance(a, b) + .8f), timber ? wood : sand, p);
            g.transform.rotation = Quaternion.LookRotation(d);
            if (timber)
            {
                var side = Vector3.Cross(Vector3.up, d).normalized * (width * .5f - .1f);
                for (int i = 0; i <= Mathf.CeilToInt(Vector3.Distance(a, b) / 3); i++)
                {
                    var v = Vector3.Lerp(a, b, i / (float)Mathf.CeilToInt(Vector3.Distance(a, b) / 3));
                    foreach (int sign in new[] { -1, 1 })
                        Box("Rail_Post", v + side * sign + Vector3.up * .6f, new Vector3(.18f, 1.2f, .18f), wood, p);
                }
            }
        }
        static void Tree(Vector3 ground, float size, Transform parent)
        {
            trees.Add(ground);
            Shape("Trunk", PrimitiveType.Cylinder, ground + Vector3.up * size * .45f, new Vector3(size * .18f, size * .45f, size * .18f), wood, parent);
            Shape("Canopy_Lower", PrimitiveType.Sphere, ground + Vector3.up * size, new Vector3(size * .95f, size * .7f, size * .95f), leaves, parent, false);
            Shape("Canopy_Upper", PrimitiveType.Sphere, ground + new Vector3(.4f, size * 1.35f, 0), new Vector3(size * .7f, size * .6f, size * .7f), lime, parent, false);
        }
        static void Lantern(Vector3 p, Transform parent)
        {
            Shape("Lantern_Post", PrimitiveType.Cylinder, p + Vector3.up * 1.1f, new Vector3(.18f, 1.1f, .18f), wood, parent);
            Box("Lantern", p + Vector3.up * 2.3f, new Vector3(.5f, .65f, .5f), gold, parent, false);
            Box("Lantern_Cap", p + Vector3.up * 2.7f, new Vector3(.75f, .15f, .75f), wood, parent, false);
        }
        static void Checkpoint(Vector3 p, int order, Transform parent)
        {
            var g = Group("CP" + (order + 1), parent).gameObject; g.transform.position = p + Vector3.up * 1.2f;
            var col = g.AddComponent<BoxCollider>(); col.isTrigger = true; col.size = new Vector3(6, 3, 6);
            var pad = Shape("Beacon_Pad", PrimitiveType.Cylinder, p + Vector3.up * .06f, new Vector3(4, .06f, 4), cyan, g.transform, false);
            var spawn = Group("Respawn", g.transform); spawn.position = p + Vector3.up * .15f; spawn.rotation = Quaternion.Euler(0, 90, 0);
            var cp = g.AddComponent<VaroGO.Client.Checkpoint>(); Set(cp, "order", order); Ref(cp, "spawnPoint", spawn); Ref(cp, "indicator", pad.GetComponent<Renderer>());
            Lantern(p + Vector3.forward * 3, parent);
        }
        static void Sign(Vector3 p, string text, Transform parent, float yaw = -90)
        {
            var g = Group("Trail_Sign", parent); g.position = p; g.rotation = Quaternion.Euler(0, yaw, 0);
            Box("Post", p + Vector3.up * 1, new Vector3(.18f, 2, .18f), wood, g);
            var panel = Box("Board", p + Vector3.up * 2, new Vector3(2.7f, 1.2f, .18f), wood, g);
            panel.transform.rotation = g.rotation;
            var label = Group("Text", g); label.localPosition = new Vector3(0, 2, .105f); label.localRotation = Quaternion.Euler(0, 180, 0);
            var tm = label.gameObject.AddComponent<TextMesh>(); tm.text = text; tm.fontSize = 48; tm.characterSize = .09f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        }

        [MenuItem("Tools/Hidden Path/Rebuild Stage 1 Playable Map")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || Application.isPlaying) throw new System.Exception("Open MapDesign_Stage1 in edit mode first.");
            var old = GameObject.Find("HiddenPath_Stage1_Blockout"); if (old != null) Undo.DestroyObjectImmediate(old);
            old = GameObject.Find("HiddenPath_Stage1_Playable"); if (old != null) Undo.DestroyObjectImmediate(old);
            hidden.Clear(); trees.Clear();
            grass = Mat("Grass"); sand = Mat("Sand_Path"); stone = Mat("Cliff_Light"); darkStone = Mat("Cliff"); wood = Mat("Wood"); leaves = Mat("Forest_Green"); lime = Mat("Forest_Lime");
            purple = Mat("Mushroom_Purple"); red = Mat("Player_A_Red"); blue = Mat("Player_B_Blue"); cyan = Mat("Waterfall_Cyan"); gold = Mat("Route_Gold"); white = Mat("Marker_White"); rose = Mat("Thorn_Rose");
            root = Group("HiddenPath_Stage1_Playable", null); Undo.RegisterCreatedObjectUndo(root.gameObject, "Build playable stage");
            env = Group("Forest_And_Ravines", root); zones = new Transform[9];
            string[] names = { "Entrance", "TwoVisions", "MushroomAscent", "ForkedForest", "HiddenCrossing", "StickyGrove", "BoulderPassage", "ThornCanyon", "ForestGate" };
            for (int i = 0; i < 9; i++) zones[i] = Group("Z" + i + "_" + names[i], root);
            Box("Ravine_Water_Visual", new Vector3(140, -13, 55), new Vector3(310, .3f, 150), Mat("Void_Water"), env, false);

            Land("Entrance_Glade", new Vector3(13, 0, 22), 26, 28, zones[0]);
            Land("Entrance_Bend", new Vector3(25, 0, 29), 12, 18, zones[0]);
            Walk(new Vector3(4, .03f, 18), new Vector3(21, .03f, 24), 5, zones[0]);
            Walk(new Vector3(21, .03f, 24), new Vector3(26, 2, 30), 5, zones[0]);
            Walk(new Vector3(26, 2, 30), new Vector3(30, 2, 34), 5, zones[0]);
            Land("First_Landing", new Vector3(30, 2, 34), 7, 12, zones[1]);
            Land("Far_Landing", new Vector3(57, 2, 40), 9, 14, zones[1]);
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(36 + i * 5.2f, 2, i % 2 == 0 ? 35 : 38);
                var g = Box("Vision_Step_" + i, p - Vector3.up * .3f, new Vector3(4.2f, .6f, 4.6f), i % 2 == 0 ? red : blue, zones[1]);
                var visibility = g.AddComponent<MagicVisibility>(); Set(visibility, "type", i % 2 == 0 ? 1 : 2);
                // Small common perimeter pegs give depth cues without exposing the other role's surface.
                Lantern(p + Vector3.forward * 2, zones[1]);
            }
            Sign(new Vector3(28, 2, 30), "TWO VISIONS\nF1 / F2", zones[1]);
            Walk(new Vector3(55, 2, 38), new Vector3(63, 6, 44), 5, zones[2]);
            Walk(new Vector3(63, 6, 44), new Vector3(65, 6, 47), 5, zones[2]);
            Land("Mushroom_Foot", new Vector3(68, 6, 50), 10, 12, zones[2]);
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(69 + i * 5, 6 + i * 2 + (i == 0 ? .25f : 0), 54 + i * 4.4f);
                var m = Group("Bounce_Mushroom_" + i, zones[2]);
                Shape("Stem", PrimitiveType.Cylinder, p - Vector3.up * 2, new Vector3(1.3f, 1.8f, 1.3f), white, m);
                Shape("Cap_Dome", PrimitiveType.Sphere, p - Vector3.up * .5f, new Vector3(5, 1, 5), purple, m, false);
                Shape("Flat_Bounce_Surface", PrimitiveType.Cylinder, p - Vector3.up * .1f, new Vector3(4.5f, .1f, 4.5f), purple, m);
                var bounce = m.gameObject.AddComponent<JumpMushroom>(); Set(bounce, "launchSpeed", 14);
                for (int j = 0; j < 3; j++) Shape("Cap_Spot", PrimitiveType.Sphere, p + new Vector3(-1 + j, .02f, .6f), new Vector3(.45f, .08f, .45f), white, m, false);
            }
            Land("Mushroom_Summit", new Vector3(85, 12, 75), 12, 14, zones[2]);
            Walk(new Vector3(89, 12, 77), new Vector3(95, 6, 80), 4.5f, zones[3], true);
            Land("Forest_Fork", new Vector3(103, 6, 79), 26, 28, zones[3]);
            Checkpoint(new Vector3(96, 6, 80), 0, zones[3]);
            Box("Fork_Central_Outcrop", new Vector3(104, 8.5f, 79), new Vector3(8, 5, 9), stone, zones[3]);
            Walk(new Vector3(96, 6.03f, 80), new Vector3(100, 6.03f, 68), 4, zones[3]);
            Walk(new Vector3(100, 6.03f, 68), new Vector3(114, 6.03f, 70), 4, zones[3]);
            Walk(new Vector3(96, 6.03f, 80), new Vector3(101, 6.03f, 89), 3.5f, zones[3]);
            Walk(new Vector3(101, 6.03f, 89), new Vector3(113, 6.03f, 87), 3.5f, zones[3]);
            Walk(new Vector3(113, 6.03f, 87), new Vector3(114, 6.03f, 70), 3.5f, zones[3]);
            Walk(new Vector3(114, 6, 70), new Vector3(122, 8, 64), 4, zones[4]);
            Land("Reveal_West", new Vector3(122, 8, 60), 11, 16, zones[4]);
            var crystal = Shape("Reveal_Crystal_Interact_E", PrimitiveType.Cube, new Vector3(122, 9, 57), Vector3.one * 1.2f, cyan, zones[4]); crystal.transform.rotation = Quaternion.Euler(0, 45, 35);
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(130 + i * 5, 8.6f + i * .6f, 57 - i * 3);
                var g = Prefab("Level/Magic/RevealablePlatform", p - Vector3.up * .3f, zones[4]);
                g.transform.localScale = Vector3.one;
                // Prefab visual/collision layout is normalized below before assigning dimensions.
                g.name = "Hidden_Step_" + i;
                foreach (var col in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
                foreach (var renderer in g.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                var solid = Box("Revealed_Surface", p - Vector3.up * .3f, new Vector3(4.2f, .6f, 4.2f), g.GetComponentInChildren<Renderer>(true).sharedMaterial, g.transform);
                var reveal = g.GetComponent<RevealablePlatform>();
                var so = new SerializedObject(reveal); var rr = so.FindProperty("renderers"); rr.arraySize = 1; rr.GetArrayElementAtIndex(0).objectReferenceValue = solid.GetComponent<Renderer>();
                var cc = so.FindProperty("colliders"); cc.arraySize = 1; cc.GetArrayElementAtIndex(0).objectReferenceValue = solid.GetComponent<Collider>(); so.ApplyModifiedPropertiesWithoutUndo();
                Set(reveal, "hiddenAlpha", .08f); Set(reveal, "solidWhenHidden", 1); Set(g.GetComponent<LocalSwitchState>(), "autoOffSeconds", 0); hidden.Add(reveal);
            }
            Land("Reveal_East", new Vector3(151, 11, 45), 9, 14, zones[4]);
            Checkpoint(new Vector3(151, 11, 45), 1, zones[4]);
            Walk(new Vector3(151, 11, 44), new Vector3(153, 14, 35), 4, zones[4]);
            Walk(new Vector3(153, 14, 35), new Vector3(153, 14, 32), 4, zones[4]);
            Land("Canyon_Overlook", new Vector3(153, 14, 30), 9, 8, zones[4]);
            Walk(new Vector3(156, 14, 30), new Vector3(164, 10, 24), 4.5f, zones[5], true);
            Land("Sticky_Grove", new Vector3(170, 10, 24), 24, 22, zones[5]);
            var sap = Shape("Sticky_Sap", PrimitiveType.Cylinder, new Vector3(170, 10.12f, 24), new Vector3(12, .12f, 10), purple, zones[5], false);
            var sapTrigger = sap.AddComponent<BoxCollider>(); sapTrigger.isTrigger = true; sapTrigger.size = new Vector3(.9f, 15, .85f); sapTrigger.center = new Vector3(0, 7, 0); sap.AddComponent<SapZone>();
            for (int i = 0; i < 3; i++) Box("Dry_Hopping_Stone", new Vector3(166 + i * 4, 10.35f, 22 + (i % 2) * 3), new Vector3(2.6f, .7f, 2.6f), stone, zones[5]);
            Walk(new Vector3(180, 10, 27), new Vector3(185, 12, 32), 5, zones[6]);
            Walk(new Vector3(185, 12, 32), new Vector3(188, 12, 35), 5, zones[6]);
            Land("Boulder_Court", new Vector3(197, 12, 36), 24, 24, zones[6]);
            Walk(new Vector3(186, 12.03f, 36), new Vector3(209, 12.03f, 36), 6, zones[6]);
            Box("Push_Lane_Wall_N", new Vector3(198, 14, 40), new Vector3(16, 4, 2), stone, zones[6]);
            Box("Push_Lane_Wall_S", new Vector3(198, 14, 32), new Vector3(16, 4, 2), stone, zones[6]);
            var rock = Shape("Pushable_Boulder", PrimitiveType.Sphere, new Vector3(195, 13.8f, 36), Vector3.one * 3.6f, stone, zones[6]);
            rock.AddComponent<Rigidbody>(); rock.AddComponent<LocalPushableState>(); var push = rock.AddComponent<Pushable>(); Set(push, "requiredPushers", 2); Set(push, "offlineSoloCounts", 1);
            Sign(new Vector3(188, 12, 32), "HOLD F\nPUSH TOGETHER", zones[6]);
            Walk(new Vector3(208, 12, 36), new Vector3(214, 14, 44), 4, zones[7]);
            Walk(new Vector3(214, 14, 44), new Vector3(216, 14, 48), 4, zones[7]);
            Land("Thorn_Approach", new Vector3(216, 14, 49), 8, 9, zones[7]);
            Vector3[] path = { new Vector3(216,14,51), new Vector3(218,14,62), new Vector3(226,14,72), new Vector3(235,14,70), new Vector3(240,14,58), new Vector3(246,14,58) };
            for (int i = 0; i < path.Length - 1; i++)
            {
                Walk(path[i], path[i+1], 3.6f, zones[7], true);
                var support = (path[i] + path[i+1]) * .5f;
                Shape("Canyon_Column", PrimitiveType.Cylinder, new Vector3(support.x, 0, support.z), new Vector3(3.8f, 13.6f, 3.8f), darkStone, zones[7]);
            }
            // Raised thorn arches keep the walking headroom clear; low bars are actual jump hazards.
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = path[i+1];
                for (int j = 0; j < 7; j++)
                {
                    float a = j * Mathf.PI / 6;
                    var b = Box("Thorn_Arch", p + new Vector3(Mathf.Cos(a) * 4, 2 + Mathf.Sin(a) * 4, 0), new Vector3(2.3f, .65f, .75f), rose, zones[7]);
                    b.transform.rotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 90);
                }
            }
            var hazard = Box("Low_Thorn_Jump_Hazard", new Vector3(222, 14.3f, 67), new Vector3(3.4f, .6f, .45f), rose, zones[7]);
            hazard.transform.rotation = Quaternion.Euler(0, 39, 0); hazard.tag = "KillZone"; hazard.GetComponent<Collider>().isTrigger = true;
            Land("Thorn_Exit", new Vector3(246, 14, 58), 7, 9, zones[7]); Checkpoint(new Vector3(246, 14, 58), 2, zones[7]);
            Walk(new Vector3(248, 14, 58), new Vector3(251.5f, 17, 60.6f), 4.5f, zones[8]);
            Walk(new Vector3(251.5f, 17, 60.6f), new Vector3(256, 17, 64), 4.5f, zones[8]);
            Land("Final_Glade", new Vector3(264, 17, 68), 24, 24, zones[8]);
            for (int i = 0; i < 5; i++) Box("Gate_Step_"+i, new Vector3(265 + i, 17 + (i+1)*.1f, 71), new Vector3(1.1f, (i+1)*.2f, 6), stone, zones[8]);
            Box("Gate_Dais", new Vector3(272, 17.5f, 71), new Vector3(6, 1, 8), stone, zones[8]);
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI / 8;
                var b = Box("Gate_Arch_Stone", new Vector3(273, 19 + Mathf.Sin(a)*4.5f, 71 + Mathf.Cos(a)*3.7f), new Vector3(1.8f, 1.7f, 1.7f), stone, zones[8]);
                b.transform.rotation = Quaternion.Euler(a*Mathf.Rad2Deg,0,0);
            }
            Box("Portal_Light", new Vector3(273, 21, 71), new Vector3(.2f, 6, 5.2f), cyan, zones[8], false);
            var goalObject = Group("Stage1_Goal", zones[8]).gameObject; goalObject.transform.position = new Vector3(272, 20, 71);
            var goalCollider = goalObject.AddComponent<BoxCollider>(); goalCollider.isTrigger = true; goalCollider.size = new Vector3(3, 5, 5);
            var goal = goalObject.AddComponent<LocalGoalState>(); var goalZone = goalObject.AddComponent<GoalZone>(); Ref(goalZone, "stateObject", goalObject);

            // Perimeter forest masses instead of isolated trees floating on individual pillars.
            Land("North_Forest_Ridge", new Vector3(140, 5, 111), 280, 20, env);
            Land("South_Forest_Ridge", new Vector3(140, -2, -8), 280, 14, env);
            var random = new System.Random(724);
            for (int i = 0; i < 58; i++)
            {
                float x = 3 + i * 4.8f;
                Tree(new Vector3(x, 5, 105 + (float)random.NextDouble()*10), 7+(float)random.NextDouble()*5, env);
                if(i%2==0) Tree(new Vector3(x, -2, -8), 8+(float)random.NextDouble()*4, env);
            }
            Vector3[] edgeTrees = {new Vector3(5,0,30),new Vector3(13,0,33),new Vector3(19,0,12),new Vector3(5,0,9),new Vector3(24,0,38),new Vector3(60,2,45),new Vector3(64,6,53),new Vector3(70,6,45),new Vector3(81,12,79),new Vector3(89,12,80),new Vector3(94,6,89),new Vector3(107,6,90),new Vector3(114,6,82),new Vector3(119,8,66),new Vector3(123,8,54),new Vector3(149,11,50),new Vector3(162,10,16),new Vector3(173,10,16),new Vector3(178,10,32),new Vector3(187,12,45),new Vector3(204,12,45),new Vector3(256,17,75),new Vector3(262,17,78),new Vector3(274,17,62)};
            foreach(var p in edgeTrees) Tree(p, 6+(float)random.NextDouble()*3, env);
            foreach(var p in new[]{new Vector3(18,0,27),new Vector3(56,2,35),new Vector3(64,6,52),new Vector3(86,12,72),new Vector3(112,6,69),new Vector3(124,8,63),new Vector3(154,14,30),new Vector3(179,10,28),new Vector3(208,12,38),new Vector3(216,14,51),new Vector3(257,17,66)}) Lantern(p,env);
            for(int i=0;i<4;i++)
            {
                float x=46+i*61;
                Box("Waterfall",new Vector3(x,-4,102),new Vector3(4,18,.2f),cyan,env,false);
                for(int j=0;j<4;j++)Box("Waterfall_Streak",new Vector3(x-1.4f+j*.9f,-4,101.8f),new Vector3(.12f,18,.1f),white,env,false);
            }
            Sign(new Vector3(11,0,22),"HIDDEN PATH\nFOREST TRAIL  >",zones[0]);
            var systems=Group("Offline_Playtest",root);
            systems.gameObject.AddComponent<OfflineBootstrap>();
            var player=Prefab("Player",new Vector3(7,.15f,18),systems); player.name="Player_Stage1"; player.transform.rotation=Quaternion.Euler(0,70,0);
            // Scene-instance override only: permits the bevels where the inclined trails meet their landings.
            player.GetComponent<CharacterController>().stepOffset=.5f;
            var camera=Camera.main; camera.orthographic=false;camera.fieldOfView=65;camera.farClipPlane=500;camera.nearClipPlane=.1f;
            camera.transform.position=new Vector3(-1,5,13);camera.transform.LookAt(player.transform.position+Vector3.up*1.4f);
            camera.clearFlags=CameraClearFlags.Skybox;
            if(camera.GetComponent<CinemachineBrain>()==null)camera.gameObject.AddComponent<CinemachineBrain>();
            var orbit=player.GetComponentInChildren<CinemachineOrbitalFollow>(true);orbit.HorizontalAxis.Value=70;orbit.VerticalAxis.Value=20;orbit.Radius=7;
            var guide=systems.gameObject.AddComponent<HiddenPath.Stage1.Stage1PlaytestGuide>();guide.player=player.transform;guide.revealConsole=crystal.transform;guide.hiddenPlatforms=hidden.ToArray();guide.goal=goal;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.65f,.75f,.82f);RenderSettings.ambientEquatorColor=new Color(.4f,.48f,.38f);RenderSettings.ambientGroundColor=new Color(.22f,.25f,.2f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.52f,.68f,.7f);RenderSettings.fogStartDistance=100;RenderSettings.fogEndDistance=270;
            var sun=Object.FindFirstObjectByType<Light>();if(sun!=null){sun.transform.rotation=Quaternion.Euler(48,-32,0);sun.intensity=1.3f;sun.shadows=LightShadows.Soft;}
            Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject=player;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(23,4,31),Quaternion.Euler(25,65,0),36,false);
            Debug.Log("[Stage1] Playable map saved: existing Player + Cinemachine + gameplay components, scene-local only.");
        }
    }
}
