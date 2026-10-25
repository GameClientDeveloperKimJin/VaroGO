using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // C-09 임시 연출: 강한 넉백으로 날아가는 동안 몸을 빙글빙글 굴린다. 착지하면 바로 선다.
    // EventRaised만 보므로 원격 플레이어도 이벤트가 오면 똑같이 돈다.
    // Visual은 틱 보간이 매 프레임 덮어쓰므로 그 아래의 별도 피벗(spinPivot)을 돌린다.
    public class KnockbackSpin : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [Tooltip("돌릴 피벗. 몸 가운데에 두고 모델을 자식으로 넣는다")]
        [SerializeField] Transform spinPivot;
        [Tooltip("앞으로 구르는 속도 (초당 각도)")]
        [SerializeField] float tumbleSpeed = 900f;
        [Tooltip("제자리 팽이 회전 속도 (초당 각도)")]
        [SerializeField] float twirlSpeed = 540f;
        [Tooltip("이 시간(초)이 지나면 착지 이벤트가 없어도 멈춘다 (원격에서 이벤트 유실 대비)")]
        [SerializeField] float maxSpinTime = 3f;
        [Tooltip("착지 후 똑바로 서는 속도 (초당 각도)")]
        [SerializeField] float recoverSpeed = 1440f;

        Quaternion baseRotation;
        Vector3 tumbleAxis;
        float spinTime;
        bool spinning;

        void Awake()
        {
            if (player == null) player = GetComponent<PlayerController>();
            if (spinPivot != null) baseRotation = spinPivot.localRotation;
        }

        void OnEnable()
        {
            if (player != null) player.EventRaised += OnPlayerEvent;
        }

        void OnDisable()
        {
            if (player != null) player.EventRaised -= OnPlayerEvent;
            spinning = false;
            if (spinPivot != null) spinPivot.localRotation = baseRotation;
        }

        void OnPlayerEvent(PlayerEvent e)
        {
            switch (e)
            {
                case PlayerEvent.KnockbackHeavy:
                    spinning = true;
                    spinTime = 0f;
                    // 매번 조금씩 다른 축으로 굴러야 더 웃기다
                    tumbleAxis = Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * Vector3.right;
                    break;
                case PlayerEvent.Landed:
                case PlayerEvent.Respawned:
                case PlayerEvent.Died:
                    spinning = false;
                    break;
            }
        }

        void LateUpdate()
        {
            if (spinPivot == null) return;
            if (spinning)
            {
                spinTime += Time.deltaTime;
                if (spinTime >= maxSpinTime) spinning = false;
                spinPivot.localRotation = Quaternion.AngleAxis(tumbleSpeed * Time.deltaTime, tumbleAxis)
                                        * spinPivot.localRotation
                                        * Quaternion.AngleAxis(twirlSpeed * Time.deltaTime, Vector3.up);
            }
            else
            {
                spinPivot.localRotation = Quaternion.RotateTowards(spinPivot.localRotation, baseRotation, recoverSpeed * Time.deltaTime);
            }
        }
    }
}
