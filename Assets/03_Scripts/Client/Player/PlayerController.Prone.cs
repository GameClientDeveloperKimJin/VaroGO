using UnityEngine;

namespace VaroGO.Client
{
    // 엎드리기: 엎드리기 키(C / 패드 오른쪽 스틱 누르기)를 한 번 누르면 엎드리고 다시 누르면 일어난다(토글). 엎드리면 이동이 느려지고 몸(충돌)이 낮아진다.
    // 땅에서만 엎드릴 수 있고, 머리 위가 막혀 있으면 일어나지 못한다(낮은 틈을 기어서 지나간 뒤 일어난다).
    // 온라인: 로컬 PC가 판정해서 NetPlayer가 [Networked] Prone으로 상대 PC에 알린다 → SetRemoteProne
    public partial class PlayerController
    {
        static readonly int ProneHash = Animator.StringToHash("Prone");
        const float ProneStandClearance = 0.02f;
        // 엎드린 동안 계단 오르기 높이. CharacterController는 (키 + 계단 높이)만큼 틈이 있어야 앞으로 간다
        // → 서 있을 때 값(0.5)을 그대로 두면 1.28 + 0.5 = 1.78m 틈이 필요해 낮은 개구멍(1.45m)을 못 지나간다
        const float ProneStepOffset = 0.1f;

        bool prone;
        bool proneToggled;
        bool remoteProne;
        bool standCaptured;
        float stoodUpAt = float.NegativeInfinity;
        float standHeight;
        Vector3 standCenter;
        float standStepOffset;

        /// <summary>엎드려 있는지 (원격은 동기화된 값)</summary>
        public bool IsProne => Simulated ? prone : remoteProne;

        void CaptureStandShape()
        {
            if (standCaptured || cc == null) return;
            standCaptured = true;
            standHeight = cc.height;
            standCenter = cc.center;
            standStepOffset = cc.stepOffset;
        }

        // 엎드리면 캡슐 아래쪽(발)은 그대로 두고 위만 낮춘다
        void ApplyProneShape(bool value)
        {
            CaptureStandShape();
            float h = value ? Mathf.Clamp(tuning.proneHeight, cc.radius * 2f, standHeight) : standHeight;
            cc.height = h;
            cc.center = standCenter + Vector3.up * ((h - standHeight) * 0.5f);
            cc.stepOffset = value ? Mathf.Min(standStepOffset, ProneStepOffset) : standStepOffset;
        }

        // 일어날 자리가 비어 있는지 (머리 위 확인)
        bool CanStandUp()
        {
            CaptureStandShape();
            float r = cc.radius;
            Vector3 bottom = transform.position + standCenter - Vector3.up * (standHeight * 0.5f - r);
            Vector3 top = transform.position + standCenter + Vector3.up * (standHeight * 0.5f - r);
            // 발 쪽 구는 바닥에 닿아 있으므로 살짝 올려서 바닥은 안 맞게 한다
            bottom += Vector3.up * (cc.skinWidth + ProneStandClearance);
            if (top.y < bottom.y) top = bottom;
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, r - ProneStandClearance, hitBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var col = hitBuffer[i];
                if (col == cc || col.transform.IsChildOf(transform)) continue;
                if (carried != null && col.transform.IsChildOf(carried.transform)) continue;
                return false;
            }
            return true;
        }

        void SetProne(bool value)
        {
            if (prone == value) return;
            prone = value;
            if (!value) stoodUpAt = simTime;
            ApplyProneShape(value);
        }

        // Simulate에서 이동 계산 전에 부른다
        void UpdateProne()
        {
            bool pressed = input.ConsumeProne();
            if (tuning.proneToggle)
            {
                if (pressed) proneToggled = !proneToggled;
            }
            else proneToggled = input.ProneHeld;

            // 엎드릴 수 없는 상황: 공중, 넉백, 묶임, 매달림, 물건 들기·밀기, 비행 치트
            bool blocked = !grounded || knockedBack || bound || hanging || climbing || carried != null
                || pushTarget != null || grabbedPushable != null || DevFlying;
            bool want = proneToggled && !blocked;

            if (want) SetProne(true);
            else if (prone)
            {
                // 일어나고 싶어도 머리 위가 막혀 있으면 엎드린 채 기다린다 (공중에 뜨면 바로 일어난다)
                if (!grounded || CanStandUp()) SetProne(false);
            }
            if (!prone && blocked && tuning.proneToggle) proneToggled = false;
        }

        // 엎드린 동안 이동 속도 감소 (AdjustMoveForGimmicks에서 쓴다)
        // 일어난 직후 proneStandUpTime 동안은 엎드린 속도에서 평소 속도로 서서히 돌아온다
        float ProneSpeedMultiplier
        {
            get
            {
                float m = Mathf.Clamp01(tuning.proneSpeedMultiplier);
                if (IsProne) return m;
                float t = tuning.proneStandUpTime > 0f ? (simTime - stoodUpAt) / tuning.proneStandUpTime : 1f;
                return t >= 1f ? 1f : Mathf.Lerp(m, 1f, t * t);
            }
        }

        // 순간이동·죽음 등에서 바로 일어선다 (자리 확인 없이)
        void ResetProne()
        {
            proneToggled = false;
            if (prone) SetProne(false);
            stoodUpAt = float.NegativeInfinity;
        }

        /// <summary>원격 플레이어: 상대 PC가 보낸 엎드림 상태를 적용한다 (NetPlayer가 부름)</summary>
        public void SetRemoteProne(bool value)
        {
            if (Simulated || remoteProne == value) return;
            remoteProne = value;
            ApplyProneShape(value); // 내 캐릭터가 상대 몸에 부딪히는 높이도 맞춘다
        }
    }
}
