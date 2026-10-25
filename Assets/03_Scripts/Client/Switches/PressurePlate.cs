using UnityEngine;

namespace VaroGO.Client
{
    // C-11: 로컬 플레이어가 올라가면 켠다. 트리거 Collider와 함께 쓴다.
    [RequireComponent(typeof(Collider))]
    public class PressurePlate : SwitchInputBase
    {
        [Tooltip("켜면 내려왔을 때 끈다")]
        [SerializeField] bool offWhenLeft;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (LocalPlayerOf(other) != null) TryRequest(true);
        }

        void OnTriggerExit(Collider other)
        {
            // 권한 없는 역할이 내려올 때는 다시 삑 하지 않는다
            if (offWhenLeft && LocalPlayerOf(other) != null && SwitchStates.CanUse(state, SwitchStates.LocalRole))
                TryRequest(false);
        }
    }
}
