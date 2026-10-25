using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // 오프라인 전용. 온라인에서는 NetPlayer가 이 역할(SetLocal, Simulate 호출)을 대신한다.
    [RequireComponent(typeof(PlayerController))]
    public class OfflinePlayerDriver : MonoBehaviour
    {
        [Tooltip("0이면 매 프레임. 30으로 두면 온라인 틱(30Hz)처럼 Simulate를 호출한다 (입력 버퍼 테스트용)")]
        [SerializeField] float fixedTickRate = 0f;
        [Tooltip("끄면 원격 플레이어처럼 입력·카메라 없이 서 있는다 (플레이어 2명 테스트용)")]
        [SerializeField] bool isLocal = true;

        // 틱 사이에 보간해서 보여줄 자식. 온라인에서는 Fusion 보간이 같은 일을 한다.
        static readonly string[] SmoothedChildren = { "Visual", "CameraTarget" };

        PlayerController player;
        float accumulator;
        Transform[] smoothed;
        Vector3[] baseLocalPos;
        Quaternion[] baseLocalRot;
        Vector3 prevPos;
        Quaternion prevRot;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            smoothed = new Transform[SmoothedChildren.Length];
            baseLocalPos = new Vector3[smoothed.Length];
            baseLocalRot = new Quaternion[smoothed.Length];
            for (int i = 0; i < smoothed.Length; i++)
            {
                smoothed[i] = transform.Find(SmoothedChildren[i]);
                if (smoothed[i] == null) continue;
                baseLocalPos[i] = smoothed[i].localPosition;
                baseLocalRot[i] = smoothed[i].localRotation;
            }
        }

        void Start()
        {
            // 온라인이면 NetPlayer가 대신 구동한다. 온라인 등록은 접속 뒤라 늦게 올 수 있다 → Registered도 본다
            GameServices.Registered += OnRegistered;
            if (GameServices.IsOnline)
            {
                enabled = false;
                return;
            }
            player.SetLocal(isLocal);
            prevPos = transform.position;
            prevRot = transform.rotation;
        }

        void OnDestroy()
        {
            GameServices.Registered -= OnRegistered;
        }

        void OnRegistered()
        {
            if (GameServices.IsOnline) enabled = false;
        }

        void Update()
        {
            if (fixedTickRate <= 0f)
            {
                player.Simulate(Time.deltaTime);
                ApplySmoothing(transform.position, transform.rotation);
                return;
            }

            float step = 1f / fixedTickRate;
            accumulator += Time.deltaTime;
            while (accumulator >= step)
            {
                prevPos = transform.position;
                prevRot = transform.rotation;
                player.Simulate(step);
                accumulator -= step;
            }

            // 틱 사이에는 직전 틱과 현재 틱 사이 위치를 보여준다 (그대로 두면 30Hz로 끊겨 떨려 보인다)
            float alpha = accumulator / step;
            ApplySmoothing(Vector3.Lerp(prevPos, transform.position, alpha),
                           Quaternion.Slerp(prevRot, transform.rotation, alpha));
        }

        void ApplySmoothing(Vector3 pos, Quaternion rot)
        {
            for (int i = 0; i < smoothed.Length; i++)
            {
                if (smoothed[i] == null) continue;
                smoothed[i].SetPositionAndRotation(pos + rot * baseLocalPos[i], rot * baseLocalRot[i]);
            }
        }
    }
}
