using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VaroGO.Client;

namespace HiddenPath.Stage1.Editor
{
    // Opt-in, play-mode motor checks against the actual scene colliders and production player tuning.
    public static class Stage1TraversalAudit
    {
        public static string RunSystems()
        {
            if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MapDesign_Stage1")
                throw new System.InvalidOperationException("Run in MapDesign_Stage1 play mode.");
            var p=Object.FindFirstObjectByType<PlayerController>();
            var respawn=p.GetComponent<PlayerRespawn>();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            var result=new List<string>();
            try
            {
                var checkpoints=new LocalCheckpointState(false);VaroGO.Shared.GameServices.RegisterCheckpoints(checkpoints);
                p.Respawn(new Vector3(96,6.15f,80),Quaternion.identity);Physics.SyncTransforms();Physics.Simulate(.02f);
                result.Add("Checkpoint trigger: "+(checkpoints.ActiveOrder==0?"PASS":"FAIL"));
                p.Teleport(new Vector3(120,-7,40),Quaternion.identity);
                typeof(PlayerRespawn).GetMethod("Update",flags).Invoke(respawn,null);
                result.Add("Fall death: "+(p.IsDead?"PASS":"FAIL"));
                typeof(PlayerRespawn).GetField("deathTimer",flags).SetValue(respawn,1f);
                typeof(PlayerRespawn).GetMethod("Update",flags).Invoke(respawn,null);
                result.Add("Respawn at checkpoint: "+(!p.IsDead && Vector3.Distance(p.transform.position,new Vector3(96,6.15f,80))<1?"PASS":"FAIL"));
                var guide=Object.FindFirstObjectByType<HiddenPath.Stage1.Stage1PlaytestGuide>();
                foreach(var platform in guide.hiddenPlatforms){platform.Reveal(VaroGO.Shared.PlayerRole.A_Red);typeof(RevealablePlatform).GetMethod("Update",flags).Invoke(platform,null);}
                result.Add("Hidden platform reveal: "+(System.Array.TrueForAll(guide.hiddenPlatforms,x=>x.Revealed)?"PASS":"FAIL"));
                var push=Object.FindFirstObjectByType<Pushable>();Vector3 before=push.transform.position;
                push.Report(VaroGO.Shared.PlayerRole.A_Red,Vector3.right);
                for(int i=0;i<40;i++){typeof(Pushable).GetMethod("FixedUpdate",flags).Invoke(push,null);Physics.Simulate(.02f);}
                result.Add("Boulder movement: "+(push.transform.position.x>before.x+.3f?"PASS":"FAIL")+" delta="+(push.transform.position.x-before.x).ToString("F2"));
                p.Respawn(new Vector3(222,14.12f,67),Quaternion.identity);Physics.SyncTransforms();Physics.Simulate(.02f);
                result.Add("Thorn damage trigger: "+(p.IsDead?"PASS":"FAIL"));
                p.Respawn(new Vector3(272,18.12f,71),Quaternion.identity);Physics.SyncTransforms();Physics.Simulate(.02f);
                result.Add("Exit completion: "+(guide.goal.IsCleared?"PASS":"FAIL"));
                p.Respawn(new Vector3(7,.15f,18),Quaternion.Euler(0,70,0));
                return string.Join("\n",result);
            }
            finally{Physics.simulationMode=mode;}
        }

        public static string Run()
        {
            if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MapDesign_Stage1")
                throw new System.InvalidOperationException("Run in MapDesign_Stage1 play mode.");
            var p = Object.FindFirstObjectByType<PlayerController>();
            var input = p.GetComponent<PlayerInputReader>(); var driver = p.GetComponent<OfflinePlayerDriver>();
            var move = typeof(PlayerInputReader).GetProperty("Move");
            var jump = typeof(PlayerInputReader).GetField("jumpPressed", BindingFlags.Instance | BindingFlags.NonPublic);
            var velocity = typeof(PlayerController).GetField("verticalVelocity", BindingFlags.Instance | BindingFlags.NonPublic);
            var cameraField = typeof(PlayerController).GetField("cameraTransform", BindingFlags.Instance | BindingFlags.NonPublic);
            var oldCamera = cameraField.GetValue(p); var basis = new GameObject("Audit_World_Basis");
            var originalPosition = p.transform.position; var originalRotation = p.transform.rotation;
            var oldMode = Physics.simulationMode; bool oldInput = input.enabled, oldDriver = driver.enabled;
            var results = new List<string>();
            input.enabled = false; driver.enabled = false; cameraField.SetValue(p, basis.transform);
            Physics.simulationMode = SimulationMode.Script;
            try
            {
                // Each segment starts from a real supporting surface; no movement transforms are injected mid-flight.
                Vector3[] starts = {new Vector3(8,.12f,19), new Vector3(22,.12f,25), new Vector3(31,2.12f,35), new Vector3(36,2.12f,35), new Vector3(41.2f,2.12f,38), new Vector3(46.4f,2.12f,35), new Vector3(57,2.12f,40),new Vector3(69,6.12f,54),new Vector3(74,8.12f,58.4f),new Vector3(79,10.12f,62.8f),new Vector3(84,12.12f,67.2f),new Vector3(89,12.12f,77),new Vector3(124.5f,8.12f,59),new Vector3(130,8.72f,57),new Vector3(135,9.32f,54),new Vector3(140,9.92f,51),new Vector3(145,10.52f,48),new Vector3(151,11.12f,44),new Vector3(156,14.12f,30),new Vector3(180,10.12f,27),new Vector3(208,12.12f,36),new Vector3(218,14.12f,62),new Vector3(248,14.12f,58),new Vector3(264,17.12f,71)};
                Vector3[] ends = {new Vector3(20,0,24),new Vector3(30,2,34),new Vector3(36,2,35),new Vector3(41.2f,2,38),new Vector3(46.4f,2,35),new Vector3(51.6f,2,38),new Vector3(65,6,47),new Vector3(74,8,58.4f),new Vector3(79,10,62.8f),new Vector3(84,12,67.2f),new Vector3(85,12,73),new Vector3(95,6,80),new Vector3(130,8.6f,57),new Vector3(135,9.2f,54),new Vector3(140,9.8f,51),new Vector3(145,10.4f,48),new Vector3(151,11,45),new Vector3(153,14,32),new Vector3(164,10,24),new Vector3(188,12,35),new Vector3(216,14,48),new Vector3(221,14,65.8f),new Vector3(256,17,64),new Vector3(271,18,71)};
                for (int n = 0; n < starts.Length; n++)
                {
                    if(n==6) starts[n]=new Vector3(55,2.12f,38);
                    if(n==7) starts[n].y=6.37f;
                    p.Respawn(starts[n], Quaternion.identity); Physics.SyncTransforms();
                    move.SetValue(input, Vector2.zero);
                    bool mushroom = n >= 7 && n <= 10;
                    for(int j=0;j<(mushroom?1:8);j++){p.Simulate(1f/60);Physics.Simulate(1f/60);}
                    bool needsJump = (n>=2 && n<=5)||(n>=12 && n<=16);
                    if(needsJump)jump.SetValue(input,true);
                    bool reached=false;float maxY=p.transform.position.y;
                    for(int frame=0;frame<300;frame++)
                    {
                        Vector3 delta=ends[n]-p.transform.position;delta.y=0;
                        Vector3 desired=delta.magnitude<.45f?Vector3.zero:delta.normalized;
                        move.SetValue(input,new Vector2(desired.x,desired.z));
                        float beforeVelocity=(float)velocity.GetValue(p);
                        p.Simulate(1f/60);Physics.Simulate(1f/60);maxY=Mathf.Max(maxY,p.transform.position.y);
                        if(mushroom && n<10 && delta.magnitude<3 && beforeVelocity<0 && (float)velocity.GetValue(p)>13){reached=true;break;}
                        if(delta.magnitude<1.1f && p.transform.position.y>=ends[n].y-.15f && p.transform.position.y<ends[n].y+.7f){reached=true;break;}
                        if(p.transform.position.y<starts[n].y-7)break;
                    }
                    results.Add(n+" "+(reached?"PASS":"FAIL")+" at "+p.transform.position.ToString("F2")+" peak="+maxY.ToString("F2"));
                }
                return string.Join("\n",results);
            }
            finally
            {
                move.SetValue(input,Vector2.zero);jump.SetValue(input,false);p.Respawn(originalPosition,originalRotation);
                cameraField.SetValue(p,oldCamera);Physics.simulationMode=oldMode;driver.enabled=oldDriver;input.enabled=oldInput;
                Object.DestroyImmediate(basis);
            }
        }
    }
}
