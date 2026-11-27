using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// 타이틀 3D 배경 카메라 연출. 카메라(또는 Cinemachine 카메라) 오브젝트에 붙인다. 로컬 연출, timeScale 무관.
    /// 진입: 조금 뒤에서 시작해 앞으로 천천히 들어간다.
    /// 평소: 아주 느린 호흡 같은 흔들림(위치 몇 cm, 회전 1도 미만) + 마우스에 따라 아주 살짝 패럴랙스(패드 조작 중이면 끔).
    /// 씬에 놓인 위치·회전이 기준이다 — 매 프레임 기준값에 흔들림을 더해서 쓴다.
    /// </summary>
    public class TitleCameraFx : MonoBehaviour
    {
        [Header("진입 (앞으로 들어가기)")]
        [Tooltip("시작할 때 기준 위치에서 뒤로 물러나 있는 거리(m)")]
        [SerializeField] float introPullBack = 0.8f;
        [SerializeField] float introTime = 1.8f;
        [SerializeField] Ease introEase = Ease.OutCubic;
        [Tooltip("끄면 뒤로 물러난 채 기다리다가 PlayIntro()가 불릴 때 들어간다 (TitleScreenFx가 3D 배경 로드 뒤에 부름)")]
        [SerializeField] bool playIntroOnStart = false;

        [Header("호흡 흔들림")]
        [Tooltip("위치 흔들림(m)")]
        [SerializeField] Vector3 swayPosition = new(0.04f, 0.03f, 0.02f);
        [Tooltip("회전 흔들림(도) — x 끄덕, y 좌우, z 기울기")]
        [SerializeField] Vector3 swayRotation = new(0.35f, 0.45f, 0.25f);
        [Tooltip("한 번 흔들리는 데 걸리는 대략적 시간(초). 클수록 느림")]
        [SerializeField] float swayPeriod = 14f;

        [Header("마우스 패럴랙스")]
        [SerializeField] bool mouseParallax = true;
        [Tooltip("화면 끝에 마우스가 있을 때 회전(도) — x 위아래, y 좌우")]
        [SerializeField] Vector2 parallaxRotation = new(0.6f, 0.9f);
        [Tooltip("화면 끝에 마우스가 있을 때 옆 이동(m)")]
        [SerializeField] Vector2 parallaxMove = new(0.05f, 0.03f);
        [SerializeField] float parallaxSmooth = 0.6f;

        Vector3 basePos;
        Quaternion baseRot;
        float pull;
        Vector2 par, parVel;
        bool padMode;
        float seed;
        Tween intro;

        void Awake()
        {
            basePos = transform.localPosition;
            baseRot = transform.localRotation;
            seed = Random.value * 100f;
            pull = introPullBack;
        }

        void Start()
        {
            if (playIntroOnStart) PlayIntro();
        }

        public void PlayIntro()
        {
            intro?.Kill();
            pull = introPullBack;
            intro = DOTween.To(() => pull, v => pull = v, 0f, introTime).SetEase(introEase).SetUpdate(true).SetLink(gameObject);
        }

        void OnDisable()
        {
            intro?.Kill();
            transform.localPosition = basePos;
            transform.localRotation = baseRot;
        }

        void LateUpdate()
        {
            float t = Time.unscaledTime;
            float f = swayPeriod > 0f ? 1f / swayPeriod : 0f;

            // 펄린 노이즈 → -1..1, 축마다 다른 줄
            Vector3 n(float row) => new(N(t * f, row), N(t * f, row + 10f), N(t * f, row + 20f));
            var p = Vector3.Scale(n(seed), swayPosition);
            var r = Vector3.Scale(n(seed + 40f), swayRotation);

            var target = mouseParallax ? ParallaxInput() : Vector2.zero;
            par = Vector2.SmoothDamp(par, target, ref parVel, parallaxSmooth, Mathf.Infinity, Time.unscaledDeltaTime);
            r.x += -par.y * parallaxRotation.x;
            r.y += par.x * parallaxRotation.y;
            p.x += par.x * parallaxMove.x;
            p.y += par.y * parallaxMove.y;

            var rot = baseRot * Quaternion.Euler(r);
            transform.localRotation = rot;
            // 뒤로 물러남은 카메라가 보는 방향 기준
            transform.localPosition = basePos + baseRot * p - (baseRot * Vector3.forward) * pull;
        }

        static float N(float x, float row) => Mathf.PerlinNoise(x, row) * 2f - 1f;

        Vector2 ParallaxInput()
        {
            var pad = Gamepad.current;
            if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.04f || pad.dpad.ReadValue().sqrMagnitude > 0.04f
                || pad.buttonSouth.isPressed || pad.buttonEast.isPressed)) padMode = true;
            var mouse = Mouse.current;
            if (mouse == null) return Vector2.zero;
            if (mouse.delta.ReadValue().sqrMagnitude > 4f) padMode = false;
            if (padMode || Screen.width <= 0 || Screen.height <= 0) return Vector2.zero;
            var m = mouse.position.ReadValue();
            return new Vector2(Mathf.Clamp(m.x / Screen.width * 2f - 1f, -1f, 1f), Mathf.Clamp(m.y / Screen.height * 2f - 1f, -1f, 1f));
        }
    }
}
