using System.Collections.Generic;
using UnityEngine;

namespace VaroGO.Client
{
    // 가시 넝쿨. 닿으면 묶인다 (낙사 아님) → 동료가 옆에서 A/D를 번갈아 눌러 풀어 준다.
    // 혼자 테스트(오프라인)에서는 묶인 사람이 E를 눌러 스스로 풀 수 있다 (PlayerController).
    // 풀린 직후에는 잠깐(PlayerTuning의 Rescue Immunity) 다시 묶이지 않으니 그 사이에 빠져나온다.
    // 닿았는지는 닿은 사람의 PC에서만 판정한다 (R6). 네트워크 동기화가 필요 없다.
    //
    // 이 오브젝트와 자식의 콜라이더(막는 것·트리거 모두)를 가시로 본다. 콜라이더 모양 그대로 판정한다.
    // 프리팹은 가시 모델과 같은 모양의 MeshCollider를 써서 "보이는 가시에 닿으면 묶임"이 되게 한다.
    public class ThornHazard : MonoBehaviour
    {
        [Tooltip("가시 표면에서 이 거리(m) 안으로 들어오면 묶인다. 0 = 몸이 닿아야 묶임")]
        [SerializeField, Min(0f)] float touchMargin = 0.1f;
        [Tooltip("묶일 때 소리 크기. 0이면 소리 없음")]
        [SerializeField, Range(0f, 1f)] float bindVolume = 0.6f;

        readonly HashSet<Collider> thorns = new HashSet<Collider>();
        static readonly Collider[] hits = new Collider[64];

        void Awake()
        {
            foreach (var col in GetComponentsInChildren<Collider>(true)) thorns.Add(col);
        }

        void Update()
        {
            if (thorns.Count == 0) return;
            foreach (var pc in PlayerController.All)
            {
                if (pc == null || !pc.Simulated || pc.IsDead) continue;
                var cc = pc.GetComponent<CharacterController>();
                if (cc == null || !Touching(cc)) continue;
                if (pc.Bind() && bindVolume > 0f)
                    AudioSource.PlayClipAtPoint(TempSfx.Oof, pc.transform.position, bindVolume);
            }
        }

        bool Touching(CharacterController cc)
        {
            // 플레이어 몸통(캡슐)을 touchMargin만큼 부풀려 가시 콜라이더와 겹치는지 물리에 묻는다.
            // 가시 모델 그대로인 MeshCollider도 모양대로 판정된다 (상자로 뭉뚱그리지 않는다).
            Vector3 center = cc.transform.TransformPoint(cc.center);
            float radius = cc.radius + touchMargin;
            float half = Mathf.Max(cc.height * 0.5f - cc.radius, 0f);
            int n = Physics.OverlapCapsuleNonAlloc(center + Vector3.up * half, center - Vector3.up * half, radius,
                hits, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
                if (thorns.Contains(hits[i])) return true;
            return false;
        }
    }
}
