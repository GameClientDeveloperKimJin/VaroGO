using System;
using Fusion;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// S2-06 온라인 공용 체크포인트 상태 (계약 §3.6). 코스 씬에 1개 (NetCheckpointState.prefab, Master Client Object).
    /// 각 PC가 자기 역할의 도착만 보고 → 상태 권한(마스터)이 역할별 비트마스크에 기록 →
    /// 두 역할이 모두 도착한 가장 큰 Order가 ActiveOrder (올라가기만 한다).
    /// Spawned에서 GameServices.Checkpoints를 이 구현으로 덮어쓴다 (OfflineBootstrap이 먼저 LocalCheckpointState를 등록해 둠).
    /// </summary>
    public class NetCheckpointState : NetworkBehaviour, ICheckpointState
    {
        public const int MaxOrder = 31; // 비트마스크(int) 한 칸에 Order 하나

        [Networked, OnChangedRender(nameof(OnStateChanged))] int RedMask { get; set; }
        [Networked, OnChangedRender(nameof(OnStateChanged))] int BlueMask { get; set; }
        // Networked 기본값이 0이라 +1 해서 저장한다 (0 = 시작점 = ActiveOrder -1)
        [Networked, OnChangedRender(nameof(OnStateChanged))] int ActivePlusOne { get; set; }

        bool spawned;

        public int ActiveOrder => spawned ? ActivePlusOne - 1 : -1;
        public event Action Changed;

        public override void Spawned()
        {
            spawned = true;
            GameServices.RegisterCheckpoints(this);
            Changed?.Invoke(); // 늦게 들어온 PC: 받은 값을 1회 알림 (계약 §3.6)
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            spawned = false;
        }

        public bool IsReached(int order, PlayerRole role)
        {
            if (!spawned || order < 0 || order > MaxOrder) return false;
            return (MaskOf(role) & (1 << order)) != 0;
        }

        public void ReportReached(int order, PlayerRole role)
        {
            if (!spawned || Object == null || !Object.IsValid || role == PlayerRole.None) return;
            if (order < 0 || order > MaxOrder)
            {
                Debug.LogError($"[Net] 체크포인트 Order {order}는 0~{MaxOrder}만 지원 (NetCheckpointState.MaxOrder)");
                return;
            }
            if (IsReached(order, role)) return; // 이미 기록됨 → RPC 생략
            RPC_Report(order, role);
        }

        public bool CanFastTravel(int order) => order <= ActiveOrder;

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_Report(int order, PlayerRole role)
        {
            if (order < 0 || order > MaxOrder) return;
            int bit = 1 << order;
            if (role == PlayerRole.A_Red) RedMask |= bit;
            else if (role == PlayerRole.B_Blue) BlueMask |= bit;
            else return;

            // 두 역할 모두 도착 + 앞 구간일 때만 갱신 (아래층으로 떨어져 이전 체크포인트를 밟아도 되돌아가지 않음)
            if ((RedMask & bit) != 0 && (BlueMask & bit) != 0 && order + 1 > ActivePlusOne)
                ActivePlusOne = order + 1;
        }

        void OnStateChanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Net] 체크포인트 red={Convert.ToString(RedMask, 2)} blue={Convert.ToString(BlueMask, 2)} active={ActivePlusOne - 1} (권한={HasStateAuthority})");
#endif
            Changed?.Invoke();
        }

        int MaskOf(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.A_Red: return RedMask;
                case PlayerRole.B_Blue: return BlueMask;
                default: return 0;
            }
        }
    }
}
