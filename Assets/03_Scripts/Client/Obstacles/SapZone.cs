using System.Collections.Generic;
using UnityEngine;

namespace VaroGO.Client
{
    // P18: 끈적한 수액. 들어가 있는 동안 느려지고 점프가 낮아진다 (발버둥 애니메이션 = Animator "Struggle").
    // 판정은 각 PC가 자기 화면에서 한다 (트리거). 파트너를 밀어 넣으면 그 PC에서도 똑같이 걸린다
    [RequireComponent(typeof(Collider))]
    public class SapZone : MonoBehaviour
    {
        [Tooltip("이동 속도 배율. 0.35 = 원래 속도의 35%")]
        [SerializeField, Range(0.05f, 1f)] float speedMultiplier = 0.35f;
        [Tooltip("점프 높이 배율. 0.5 = 원래 높이의 절반, 0 = 점프 불가")]
        [SerializeField, Range(0f, 1f)] float jumpMultiplier = 0.5f;

        readonly HashSet<PlayerController> inside = new HashSet<PlayerController>();

        public float SpeedMultiplier => speedMultiplier;
        public float JumpMultiplier => jumpMultiplier;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var p = other.GetComponentInParent<PlayerController>();
            if (p == null || !inside.Add(p)) return;
            p.EnterSap(this);
        }

        // 순간이동(리스폰)은 OnTriggerExit가 오지 않아 플레이어 쪽에서 목록을 비운다. 아직 안에 있으면 여기서 다시 건다
        void OnTriggerStay(Collider other)
        {
            var p = other.GetComponentInParent<PlayerController>();
            if (p == null) return;
            inside.Add(p);
            p.EnterSap(this);
        }

        void OnTriggerExit(Collider other)
        {
            var p = other.GetComponentInParent<PlayerController>();
            if (p == null || !inside.Remove(p)) return;
            p.ExitSap(this);
        }

        void OnDisable()
        {
            foreach (var p in inside)
                if (p != null) p.ExitSap(this);
            inside.Clear();
        }
    }
}
