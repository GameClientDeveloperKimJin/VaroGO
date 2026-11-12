using UnityEngine;
using UnityEngine.InputSystem;

namespace VaroGO.Client
{
    /// <summary>
    /// 개발 치트 (DevCheatGUI 버튼으로 켠다). 내 PC의 로컬 플레이어에만 적용되고 네트워크로 보내지 않는다.
    /// - SelfClimb: 혼자 매달렸을 때 점프·앞 입력으로 스스로 올라간다 (PlayerTuning.ledgeSelfClimb 에셋 값은 건드리지 않음)
    /// - Fly: 무중력 비행. Space = 위, Left Ctrl / Q = 아래 (C는 웅크리기라 안 씀). 매달리기·점프는 하지 않는다
    /// </summary>
    public static class DevCheats
    {
        public static bool SelfClimb;
        public static bool Fly;
        public static float FlyVerticalSpeed = 6f;   // 비행 위아래 속도 (m/s)
        public static float FlySpeedMultiplier = 2f; // 비행 중 이동 속도 배수
    }

    public partial class PlayerController
    {
        bool DevFlying => DevCheats.Fly && IsLocal;

        // 비행 중 위아래 속도. 키보드를 직접 읽는다 (치트 전용, 입력 맵에 추가하지 않음)
        float DevFlyVertical()
        {
            var kb = Keyboard.current;
            if (kb == null) return 0f;
            float v = 0f;
            if (kb.spaceKey.isPressed) v += 1f;
            if (kb.leftCtrlKey.isPressed || kb.qKey.isPressed) v -= 1f;
            return v * DevCheats.FlyVerticalSpeed;
        }
    }
}
