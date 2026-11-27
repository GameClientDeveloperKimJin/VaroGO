using Fusion;
using UnityEngine;
using VaroGO.Shared;
using VaroGO.Net;
namespace HiddenPath.Stage2Z2
{
    [RequireComponent(typeof(Z2Sequence))]
    public class Z2NetState:NetworkBehaviour,IZ2Transport
    {
        [Networked] int Red {get;set;}
        [Networked] int Blue {get;set;}
        [Networked] int RedMask {get;set;}
        [Networked] int BlueMask {get;set;}
        [Networked] double RedAt {get;set;}
        [Networked] double BlueAt {get;set;}
        [Networked] int Revision {get;set;}
        [Networked] NetworkBool Initialized {get;set;}
        Z2Sequence sequence;bool ready;int seen=-1;
        void Awake(){sequence=GetComponent<Z2Sequence>();sequence.Transport=this;}
        public override void Spawned(){ready=true;if(HasStateAuthority&&!Initialized){sequence.ResetLocal();Publish();Initialized=true;}Sync();}
        public override void Despawned(NetworkRunner runner,bool hasState){ready=false;}
        public override void Render(){Sync();}
        void Sync(){if(!ready||seen==Revision)return;seen=Revision;sequence.SetSnapshot(Red,Blue,RedMask,BlueMask,RedAt,BlueAt,Revision);}
        void Publish(){Red=sequence.RedIndex;Blue=sequence.BlueIndex;RedMask=sequence.RedUsed;BlueMask=sequence.BlueUsed;RedAt=sequence.RedTime;BlueAt=sequence.BlueTime;Revision++;seen=Revision;}
        public void RequestStep(PlayerRole role,int index){if(ready)RPC_Step(role,index);}
        public void RequestReset(){if(ready)RPC_Reset();}
        bool Sender(PlayerRole role,RpcInfo info,out NetPlayer player)
        {player=NetPlayer.Find(role);return player&&player.Object&&player.Object.StateAuthority==info.Source;}
        [Rpc(RpcSources.All,RpcTargets.StateAuthority)]
        void RPC_Step(PlayerRole role,int index,RpcInfo info=default)
        {
            if(!Sender(role,info,out var player)||index<0||index>=sequence.Count)return;
            var platform=(role==PlayerRole.A_Red?sequence.redPlatforms:sequence.bluePlatforms)[index];
            // Allow latency margin but reject requests for remote/unrelated platforms.
            if(Vector3.Distance(player.transform.position,platform.transform.position)>4.5f)return;
            Sync();if(sequence.ApplyStep(role,index,Z2Sequence.Now))Publish();
        }
        [Rpc(RpcSources.All,RpcTargets.StateAuthority)]
        void RPC_Reset(RpcInfo info=default)
        {
            NetPlayer player=null;foreach(var p in NetPlayer.All)if(p&&p.Object&&p.Object.IsValid&&p.Object.StateAuthority==info.Source)player=p;
            if(!player)return;var v=player.transform.position;
            if(Mathf.Abs(v.x)>20||v.z<62||v.z>98||v.y>1)return;
            sequence.ResetLocal();Publish();
        }
    }
}
