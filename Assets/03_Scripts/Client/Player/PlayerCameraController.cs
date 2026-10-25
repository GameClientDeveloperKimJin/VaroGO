using Unity.Cinemachine;
using UnityEngine;

namespace VaroGO.Client
{
    // 마우스·오른쪽 스틱으로 궤도 카메라를 돌린다. CameraRig에 붙인다.
    // CinemachineInputAxisController는 입력에 deltaTime을 곱해서 마우스(프레임당 픽셀)에 맞지 않고,
    // 복제된 입력 맵을 읽지 못하므로 PlayerInputReader 값을 직접 쓴다.
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    [DefaultExecutionOrder(10)] // PlayerInputReader.Update 다음에 읽는다
    public class PlayerCameraController : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerTuning tuning;

        CinemachineOrbitalFollow orbit;
        CinemachineDeoccluder deoccluder;

        void Awake()
        {
            orbit = GetComponent<CinemachineOrbitalFollow>();
            deoccluder = GetComponent<CinemachineDeoccluder>();
        }

        void Update()
        {
            // 플레이 중에 튜닝 값을 바꿔도 바로 반영되도록 매 프레임 맞춘다
            if (deoccluder != null && deoccluder.AvoidObstacles.Enabled != tuning.cameraAvoidObstacles)
            {
                var avoid = deoccluder.AvoidObstacles;
                avoid.Enabled = tuning.cameraAvoidObstacles;
                deoccluder.AvoidObstacles = avoid;
            }

            if (input == null || !input.enabled) return;

            Vector2 delta = input.LookIsPointer
                ? input.Look * tuning.mouseSensitivity
                : input.Look * (tuning.stickLookSpeed * Time.deltaTime);
            if (!tuning.invertY) delta.y = -delta.y;

            orbit.HorizontalAxis.Value = orbit.HorizontalAxis.ClampValue(orbit.HorizontalAxis.Value + delta.x);
            orbit.VerticalAxis.Value = orbit.VerticalAxis.ClampValue(orbit.VerticalAxis.Value + delta.y);
        }
    }
}
