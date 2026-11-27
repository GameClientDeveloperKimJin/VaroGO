using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VaroGO.Client;
using VaroGO.Shared;
using Object=UnityEngine.Object;
namespace HiddenPath.Stage2Z2.Editor
{
    public static class Z2CoopAudit
    {
        class Clock:IGameClock{public double time;public double Now=>time;}
        class Role:IRoleProvider{public PlayerRole value;public PlayerRole LocalRole=>value;public event Action<PlayerRole> LocalRoleChanged {add{}remove{}}}
        static Clock clock;static Role role;static PlayerController player;static PlayerInputReader input;static Z2Sequence seq;
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly FieldInfo Jump=typeof(PlayerInputReader).GetField("jumpPressed",Flags);
        static readonly PropertyInfo Move=typeof(PlayerInputReader).GetProperty("Move");
        static readonly FieldInfo CameraField=typeof(PlayerController).GetField("cameraTransform",Flags);
        static List<string> lines;
        static Z2Platform[] allPlatforms;
        static void Tick(float dt,Vector2 move,bool jump=false)
        {
            foreach(var p in allPlatforms){p.GetComponent<Rigidbody>().position=p.PositionAt(clock.time);}
            Physics.SyncTransforms();Move.SetValue(input,move);Jump.SetValue(input,jump);player.Simulate(dt);Physics.Simulate(dt);clock.time+=dt;
        }
        static void Place(Vector3 p,float dt)
        {player.Respawn(p+Vector3.up*.025f,Quaternion.identity);for(int i=0;i<10;i++)Tick(dt,Vector2.zero);}
        static bool Traverse(Func<Vector3> from,Func<Vector3> to,float dt,out string detail,Z2Platform target=null,bool place=true)
        {
            if(place)Place(from(),dt);
            // Wait on the actual terrain/platform for an attainable boarding or disembarking window.
            // A full cycle may be needed; Tick preserves platform riding while waiting.
            for(int w=0;w<Mathf.CeilToInt(15/dt);w++)
            {
                var delta=to()-player.transform.position;
                bool near=new Vector2(delta.x,delta.z).magnitude<5.2f&&delta.y<.7f;
                bool steady=target==null||(target.PositionAt(clock.time+.1)-target.PositionAt(clock.time)).magnitude<.1f;
                if(near&&steady)break;
                Tick(dt,Vector2.zero);
            }
            var start=player.transform.position;bool jump=false,air=false;float airborneAt=0;
            for(int tick=0;tick<Mathf.CeilToInt(3/dt);tick++)
            {
                var dest=to();var delta=dest-player.transform.position;delta.y=0;var dir=delta.normalized;
                bool press=!jump&&Vector3.Distance(new Vector3(player.transform.position.x,0,player.transform.position.z),new Vector3(start.x,0,start.z))>.62f;
                if(press){jump=true;airborneAt=tick*dt;}
                Tick(dt,delta.magnitude<.2f?Vector2.zero:new Vector2(dir.x,dir.z),press);
                if(jump&&!player.IsGrounded)air=true;
                if(air&&player.IsGrounded&&tick*dt>airborneAt+.12f)
                {
                    var end=to();var diff=player.transform.position-end;
                    bool ok=new Vector2(diff.x,diff.z).magnitude<1.65f&&Mathf.Abs(diff.y)<.35f;
                    if(target){RaycastHit hit;ok=Physics.SphereCast(player.transform.position+Vector3.up*.5f,player.GetComponent<CharacterController>().radius*.95f,Vector3.down,out hit,.8f,~0,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<Z2Platform>()==target;}
                    detail="land="+player.transform.position+" target="+end;return ok;
                }
                if(player.IsDead||player.transform.position.y<0)break;
            }
            detail="end="+player.transform.position+" target="+to()+" grounded="+player.IsGrounded;return false;
        }
        public static string Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Run in Play Mode");
            seq=Object.FindFirstObjectByType<Z2Sequence>();if(!seq)throw new InvalidOperationException("No Z2 sequence");
            allPlatforms=seq.GetComponentsInChildren<Z2Platform>();
            var original=Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Select(p=>p.gameObject).ToArray();foreach(var p in original)p.SetActive(false);
            var oldRole=GameServices.Role;var oldClock=GameServices.Clock;var oldMode=Physics.simulationMode;
            GameObject actor=null,axis=null;lines=new List<string>();int pass=0,fail=0;
            try
            {
                clock=new Clock();role=new Role{value=PlayerRole.A_Red};GameServices.Register(role,clock,false);
                Physics.simulationMode=SimulationMode.Script;
                actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Client/Player.prefab"));actor.name="Z2_Audit_Actor";
                player=actor.GetComponent<PlayerController>();input=actor.GetComponent<PlayerInputReader>();
                foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
                player.SetLocal(true);input.enabled=false;axis=new GameObject("Test_Move_Axis");CameraField.SetValue(player,axis.transform);
                foreach(var cam in actor.GetComponentsInChildren<Camera>(true))cam.enabled=false;
                lines.Add("Actual PlayerController + CharacterController; move="+player.Tuning.moveSpeed+" jump="+player.Tuning.jumpHeight+" gravity="+player.Tuning.gravity);
                foreach(int hz in new[]{30,60})foreach(int side in new[]{0,1})foreach(float phase in new[]{0f,1.5f,3f,4.8f})for(int i=0;i<6;i++)
                {
                    float dt=1f/hz;role.value=side==0?PlayerRole.A_Red:PlayerRole.B_Blue;clock.time=100+phase;
                    seq.SetSnapshot(i,i,63,63,100,100,seq.Revision+1);var p=(side==0?seq.redPlatforms:seq.bluePlatforms)[i];
                    foreach(var q in seq.redPlatforms.Concat(seq.bluePlatforms))q.GetComponent<Rigidbody>().position=q.PositionAt(clock.time);Physics.SyncTransforms();
                    Vector3 previous=i==0?new Vector3(side==0?-8:8,2,65):Z2CoopBuilder.WaitPosition(i-1,side);
                    string d;bool ok=Traverse(()=>previous,()=>p.PositionAt(clock.time),dt,out d,p);
                    if(ok)pass++;else{fail++;lines.Add("FAIL enter "+p.name+" "+hz+"Hz phase="+phase+" "+d);}
                    clock.time=100+phase;seq.SetSnapshot(i,i,63,63,100,100,seq.Revision+1);
                    foreach(var q in seq.redPlatforms.Concat(seq.bluePlatforms))q.GetComponent<Rigidbody>().position=q.PositionAt(clock.time);Physics.SyncTransforms();
                    var next=Z2CoopBuilder.WaitPosition(i,side);ok=Traverse(()=>p.PositionAt(clock.time),()=>next,dt,out d);
                    if(ok)pass++;else{fail++;lines.Add("FAIL leave "+p.name+" "+hz+"Hz phase="+phase+" "+d);}
                }
                lines.Insert(1,"Transition tests: PASS="+pass+" FAIL="+fail+" (both roles, 30/60 Hz, four motion phases, enter/leave each platform)");
                foreach(int hz in new[]{30,60})foreach(int side in new[]{0,1})
                {
                    float dt=1f/hz;clock.time=200;role.value=side==0?PlayerRole.A_Red:PlayerRole.B_Blue;seq.ResetLocal();
                    Place(new Vector3(side==0?-8:8,2,65),dt);bool all=true;string d="";
                    for(int i=0;i<6;i++)
                    {
                        var p=(side==0?seq.redPlatforms:seq.bluePlatforms)[i];
                        if(!Traverse(()=>player.transform.position,()=>p.PositionAt(clock.time),dt,out d,p,false)){all=false;d="enter "+p.name+" "+d;break;}
                        var wait=Z2CoopBuilder.WaitPosition(i,side);
                        if(!Traverse(()=>player.transform.position,()=>wait,dt,out d,null,false)){all=false;d="leave "+p.name+" "+d;break;}
                        if(i<5){var partner=side==0?PlayerRole.B_Blue:PlayerRole.A_Red;seq.TryStep(partner,side==0?seq.BlueIndex:seq.RedIndex);}
                    }
                    lines.Add("Continuous "+role.value+" "+hz+"Hz="+all+(all?"":" "+d));
                }
                foreach(int hz in new[]{30,60})foreach(int index in new[]{0,1})
                {
                    clock.time=300;role.value=PlayerRole.A_Red;seq.SetSnapshot(index,index,63,63,300,300,seq.Revision+1);var p=seq.redPlatforms[index];
                    Place(p.PositionAt(clock.time),1f/hz);var offset=player.transform.position-p.PositionAt(clock.time);float maxError=0;
                    for(int i=0;i<hz*8;i++){Tick(1f/hz,Vector2.zero);maxError=Mathf.Max(maxError,(player.transform.position-p.PositionAt(clock.time)-offset).magnitude);}
                    lines.Add("Riding "+p.motion+" "+hz+"Hz maxError="+maxError.ToString("F3")+" pass="+(maxError<.35f));
                }
                // Actual landing contact, duplicate suppression and immediate collider removal.
                clock.time=100;role.value=PlayerRole.A_Red;seq.ResetLocal();Place(seq.redPlatforms[0].PositionAt(clock.time),1f/60);
                lines.Add("Landing R1 advances B2="+(seq.BlueIndex==1));int before=seq.Revision;for(int i=0;i<30;i++)Tick(1f/60,Vector2.zero);lines.Add("Standing does not repeat="+(seq.Revision==before));
                lines.Add("Old B1 collision removed="+(!seq.bluePlatforms[0].surface.enabled));
                seq.ResetLocal();role.value=PlayerRole.B_Blue;seq.SetSnapshot(0,0,0,1,clock.time,clock.time,seq.Revision+1);Place(seq.bluePlatforms[0].PositionAt(clock.time),1f/60);float height=player.transform.position.y;seq.ApplyStep(PlayerRole.A_Red,0,clock.time);for(int i=0;i<25;i++)Tick(1f/60,Vector2.zero);lines.Add("Occupied B1 immediately drops rider="+(player.transform.position.y<height-.4f));
                bool steps=true;for(int first=0;first<2;first++)
                {
                    seq.ResetLocal();for(int j=0;j<14;j++){var rr=(j+first)%2==0?PlayerRole.A_Red:PlayerRole.B_Blue;int idx=rr==PlayerRole.A_Red?seq.RedIndex:seq.BlueIndex;seq.TryStep(rr,idx);}
                    steps&=seq.RedIndex==5&&seq.BlueIndex==5;
                }
                lines.Add("Both opening orders reach final pair="+steps);
                int bypass=0;for(int side=0;side<2;side++)for(int i=1;i<6;i++)
                {
                    seq.ResetLocal();foreach(var p in seq.redPlatforms.Concat(seq.bluePlatforms))p.SetState(false,clock.time,false);
                    string d;var from=Z2CoopBuilder.WaitPosition(i-1,side);var to=Z2CoopBuilder.WaitPosition(i,side);
                    if(Traverse(()=>from,()=>to,1f/60,out d))bypass++;
                }
                lines.Add("Direct wait-stone bypasses="+bypass+" / 10 tested");
                int motionBypasses=0;
                for(int side=0;side<2;side++)foreach(int i in new[]{0,1,3,4})
                {
                    clock.time=500;seq.SetSnapshot(i,i,63,63,500,500,seq.Revision+1);
                    var p=(side==0?seq.redPlatforms:seq.bluePlatforms)[i];var travel=p.travel;
                    try{p.travel=Vector3.zero;string d;var dest=Z2CoopBuilder.WaitPosition(i,side);if(Traverse(()=>p.PositionAt(clock.time),()=>dest,1f/60,out d))motionBypasses++;}
                    finally{p.travel=travel;}
                }
                lines.Add("Moving-platform gaps crossed with motion disabled="+motionBypasses+" / 8 (expected 0)");
                foreach(int side in new[]{0,1})
                {
                    role.value=side==0?PlayerRole.A_Red:PlayerRole.B_Blue;clock.time=600;seq.SetSnapshot(1,1,63,63,600,600,seq.Revision+1);
                    var lift=seq.GetComponentsInChildren<Z2EntryLift>().Select(e=>e.GetComponent<Z2Platform>()).First(p=>side==0?p.Origin.x<0:p.Origin.x>0);
                    lift.SetState(true,600,false);string d;float sign=side==0?-1:1;
                    bool ok=Traverse(()=>new Vector3(sign*14,2,65),()=>lift.PositionAt(clock.time),1f/60,out d,lift);
                    var terrace=Z2CoopBuilder.WaitPosition(0,side);
                    ok&=Traverse(()=>player.transform.position,()=>terrace,1f/60,out d,null,false);
                    var shuttle=(side==0?seq.redPlatforms:seq.bluePlatforms)[1];
                    ok&=Traverse(()=>player.transform.position,()=>shuttle.PositionAt(clock.time),1f/60,out d,shuttle,false);
                    lines.Add("Opening recovery lift to shuttle "+role.value+"="+ok+(ok?"":" "+d));
                    seq.SetSnapshot(5,5,63,63,clock.time,clock.time,seq.Revision+1);
                    var exit=new Vector3(sign*8,6.08f,98);
                    bool exitOk=Traverse(()=>Z2CoopBuilder.WaitPosition(5,side),()=>exit,1f/60,out d);
                    lines.Add("Final terrace to reunion "+role.value+"="+exitOk+(exitOk?"":" "+d));
                }
                seq.ResetLocal();role.value=PlayerRole.B_Blue;Place(seq.redPlatforms[0].PositionAt(clock.time),1f/60);lines.Add("Wrong role rejected="+(seq.BlueIndex==0&&seq.RedIndex==0));
                seq.ApplyStep(PlayerRole.A_Red,0,clock.time);player.Respawn(new Vector3(8,-1.8f,80),Quaternion.identity);for(int i=0;i<10;i++)Tick(1f/60,Vector2.zero);lines.Add("Fall resets pair and kills actor="+(seq.RedIndex==0&&seq.BlueIndex==0&&player.IsDead));
                var text=string.Join("\n",lines);Directory.CreateDirectory("Captures/Stage2_Z2");File.WriteAllText("Captures/Stage2_Z2/physics-audit.txt",text);return text;
            }
            finally
            {
                Physics.simulationMode=oldMode;if(actor)Object.DestroyImmediate(actor);if(axis)Object.DestroyImmediate(axis);
                GameServices.Register(oldRole,oldClock,false);seq.ResetLocal();foreach(var p in original)if(p)p.SetActive(true);
            }
        }
    }
}
