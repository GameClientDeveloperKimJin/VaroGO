using UnityEngine;

namespace VaroGO.Client
{
    // 밀리는 공(바위) 연출. 판정·이동은 건드리지 않고 보이는 모델과 효과만 다룬다.
    //  - 굴림: 이 오브젝트가 수평으로 움직인 거리만큼 Visual을 돌린다 (각도 = 거리 ÷ 반지름)
    //  - 떨어져 멈추면 쿵 소리 + 먼지
    // 위치 변화만 보고 계산하므로 방장이 아닌 PC(네트워크로 위치만 받는 쪽)에서도 똑같이 보인다.
    // 루트는 돌리지 않는다 (Pushable이 루트의 앞 방향으로 미는 방향을 정한다)
    public class BoulderFx : MonoBehaviour
    {
        [Header("굴림")]
        [Tooltip("돌릴 모델 (보통 자식 Visual). 중심이 공의 중심이어야 한다")]
        [SerializeField] Transform visual;
        [Tooltip("공 반지름 (m). 클수록 천천히 돈다")]
        [SerializeField] float radius = 1.3f;
        [Tooltip("한 프레임에 이보다 멀리 움직이면 순간이동으로 보고 굴리지 않는다 (처음 자리로 돌아갈 때)")]
        [SerializeField] float maxStep = 2f;

        [Header("떨어져 멈출 때")]
        [Tooltip("이 높이(m) 이상 떨어졌다 멈추면 쿵 연출")]
        [SerializeField] float dropMinHeight = 0.5f;
        [Tooltip("쿵 소리. 비우면 임시음")]
        [SerializeField] AudioClip dropClip;
        [Range(0f, 1f), Tooltip("쿵 소리 크기. 0이면 소리 없음")]
        [SerializeField] float dropVolume = 0.9f;
        [Tooltip("바닥에 터뜨릴 먼지 이펙트 (비워도 됨)")]
        [SerializeField] GameObject dropVfx;
        [SerializeField] float dropVfxScale = 2f;

        const float StopConfirm = 0.1f;    // 떨어지다 이 시간 동안 안 내려가면 멈춘 것

        Vector3 last;
        bool falling;
        float fallFromY;
        float lastFellAt;

        void Awake()
        {
            last = transform.position;
        }

        void LateUpdate()
        {
            Vector3 p = transform.position;
            Vector3 d = p - last;
            last = p;

            // 순간이동 (떨어져 처음 자리로 돌아감 등)
            if (d.magnitude > maxStep) { falling = false; return; }

            Vector3 flat = new Vector3(d.x, 0f, d.z);
            float dist = flat.magnitude;
            if (dist > 0.00005f && visual != null && radius > 0.01f)
                visual.Rotate(Vector3.Cross(Vector3.up, flat / dist), dist / radius * Mathf.Rad2Deg, Space.World);

            UpdateDrop(p, d.y);
        }

        void UpdateDrop(Vector3 p, float dy)
        {
            if (dy < -0.0005f)
            {
                if (!falling) { falling = true; fallFromY = p.y - dy; }
                lastFellAt = Time.time;
                return;
            }
            if (!falling || Time.time - lastFellAt < StopConfirm) return;
            falling = false;
            if (fallFromY - p.y < dropMinHeight) return;

            if (dropVolume > 0f) AudioSource.PlayClipAtPoint(dropClip != null ? dropClip : TempSfx.Thud, p, dropVolume);
            if (dropVfx != null)
            {
                var fx = Instantiate(dropVfx, p, Quaternion.identity);
                fx.transform.localScale *= dropVfxScale;
            }
        }
    }
}
