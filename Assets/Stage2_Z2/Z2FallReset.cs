using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;
namespace HiddenPath.Stage2Z2
{
    public class Z2FallReset:MonoBehaviour
    {
        public Z2Sequence sequence;
        void OnTriggerEnter(Collider other)
        {
            var p=other.GetComponentInParent<PlayerController>();if(!p||!p.IsLocal||p.IsDead)return;
            sequence.RequestReset();p.Die();
        }
    }
}
