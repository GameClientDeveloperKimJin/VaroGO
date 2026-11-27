using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;

namespace HiddenPath.Stage2Z2
{
    public enum Z2Motion {Vertical,Horizontal,Spawn}
    // Retains the existing player's ClockDrivenObstacle riding/velocity contract.
    public class Z2Platform : ClockDrivenObstacle
    {
        public Z2Sequence sequence;
        public PlayerRole role;
        public int index;
        public Z2Motion motion;
        public Vector3 travel;
        public float moveSeconds=2.5f,pauseSeconds=.8f;
        public BoxCollider surface;
        public BoxCollider[] surfaces;
        public Transform visible;
        public Renderer rim;
        public bool IsActive {get;private set;}
        public double ActivatedAt {get;private set;}
        public bool Consumed {get;private set;}
        public Vector3 Origin => originPos;
        MaterialPropertyBlock block;
        public Vector3 PositionAt(double time){Evaluate(time,out var p,out _);return p;}
        protected override void Evaluate(double t,out Vector3 p,out Quaternion r)
        {
            r=originRot;p=originPos;if(!IsActive||motion==Z2Motion.Spawn)return;
            double elapsed=System.Math.Max(0,t-ActivatedAt);float leg=moveSeconds+pauseSeconds;
            float cycle=(float)Repeat(elapsed,leg*2);
            float u=cycle<leg?Mathf.SmoothStep(0,1,Mathf.Clamp01(cycle/moveSeconds)):1-Mathf.SmoothStep(0,1,Mathf.Clamp01((cycle-leg)/moveSeconds));
            p+=originRot*travel*u;
        }
        public void SetState(bool active,double time,bool used)
        {
            IsActive=active;ActivatedAt=time;Consumed=used;
            if(surface)surface.enabled=active;
            if(surfaces!=null)foreach(var part in surfaces)if(part)part.enabled=active;
            if(visible)visible.gameObject.SetActive(active);
            if(rim){if(block==null)block=new MaterialPropertyBlock();rim.GetPropertyBlock(block);var c=role==PlayerRole.A_Red?new Color(1,.13f,.08f):new Color(.06f,.55f,1);if(used)c*=.45f;block.SetColor("_BaseColor",c);block.SetColor("_EmissionColor",c*1.5f);rim.SetPropertyBlock(block);}
            // A disabled platform stops immediately, even if somebody is riding it.
            if(!active&&Application.isPlaying){GetComponent<Rigidbody>().position=originPos;transform.position=originPos;}
        }
        void OnTriggerStay(Collider other){Contact(other);}
        void OnTriggerEnter(Collider other){Contact(other);}
        void Contact(Collider other)
        {
            if(!IsActive||Consumed||sequence==null)return;
            var player=other.GetComponentInParent<PlayerController>();
            if(!player||!player.IsLocal||player.IsDead||!player.IsGrounded)return;
            if(GameServices.Role==null||GameServices.Role.LocalRole!=role)return;
            var cc=player.GetComponent<CharacterController>();
            float feet=cc.bounds.min.y;
            var p=player.transform.position;bool onTop=false;
            var parts=surfaces!=null&&surfaces.Length>0?surfaces:new[]{surface};
            foreach(var part in parts){if(!part||!part.enabled)continue;var b=part.bounds;if(feet>=b.max.y-.18f&&feet<=b.max.y+.22f&&p.x>=b.min.x-cc.radius&&p.x<=b.max.x+cc.radius&&p.z>=b.min.z-cc.radius&&p.z<=b.max.z+cc.radius){onTop=true;break;}}
            if(!onTop)return;
            sequence.TryStep(role,index);
        }
    }
}
