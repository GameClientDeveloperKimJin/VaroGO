using UnityEngine;

namespace VaroGO.Client
{
    // P19: 탈것 좌석 고정. 앉아 있는 동안 이동·점프·중력·충돌을 하지 않고 좌석 Transform에 붙는다.
    // 위치는 그대로 transform에 쓰므로 온라인에서는 NetworkTransform이 상대 PC에 보낸다.
    public partial class PlayerController
    {
        Transform seat;

        public bool IsSeated => seat != null;
        public Transform Seat => seat;

        // target = null이면 내린다. 내린 뒤 위치는 호출한 쪽이 Teleport로 정한다
        public void SetSeat(Transform target)
        {
            if (seat == target) return;
            seat = target;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            pushVelocity = Vector3.zero;
            knockedBack = false;
            jumping = false;
            groundObstacle = null;
            if (cc != null) cc.enabled = target == null;
            if (target != null) StickToSeat();
            grounded = true;
        }

        void SimulateSeated()
        {
            input.ConsumeJump();
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            grounded = true;
            StickToSeat();
            UpdateAnimator();
        }

        void StickToSeat()
        {
            if (seat == null) return;
            transform.SetPositionAndRotation(seat.position, seat.rotation);
        }

        // 뗏목은 매 프레임 움직이고 Simulate는 틱마다 불릴 수 있으므로 화면에서는 프레임마다 다시 붙인다
        void LateUpdate()
        {
            if (seat != null && Simulated) StickToSeat();
        }
    }
}
