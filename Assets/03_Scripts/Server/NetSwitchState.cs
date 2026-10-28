using System;
using Fusion;
using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// S-08 온라인 스위치 상태(계약 §3.3). Net Variant에서 LocalSwitchState 옆에 추가된다(로컬 구현은 지우지 않음).
    /// 설정값(AllowedRole·AutoOffSeconds·StartOn)은 같은 GameObject의 LocalSwitchState에서 읽고 자체 필드는 없다.
    /// 상태 권한(마스터 클라이언트, Is Master Client Object)만 값을 바꾸고, 다른 PC는 RPC로 요청한다.
    /// Spawned 전에는 Networked 값을 읽지 않고 StartOn을 돌려준다.
    /// </summary>
    [RequireComponent(typeof(LocalSwitchState))]
    public class NetSwitchState : NetworkBehaviour, ISwitchState, ISwitchDetails
    {
        [Networked, OnChangedRender(nameof(OnIsOnChanged))] NetworkBool IsOnNet { get; set; }
        [Networked] TickTimer OffTimer { get; set; }
        [Networked] float LastOnTimeNet { get; set; }
        [Networked] NetworkBool Initialized { get; set; }

        LocalSwitchState local;
        bool observed;
        bool spawned;

        public bool IsOn => observed;
        public PlayerRole AllowedRole => Local.AllowedRole;
        public float AutoOffSeconds => Local.AutoOffSeconds;
        public double LastOnTime => spawned ? LastOnTimeNet : Local.LastOnTime;
        public event Action<bool> Changed;

        LocalSwitchState Local => local != null ? local : local = GetComponent<LocalSwitchState>();

        void Awake() => observed = Local.StartOn;

        public override void Spawned()
        {
            if (HasStateAuthority && !Initialized)
            {
                IsOnNet = Local.StartOn;
                LastOnTimeNet = Runner.SimulationTime;
                OffTimer = TickTimer.None;
                Initialized = true;
            }
            spawned = true;
            Sync(); // 늦게 들어온 PC: StartOn과 다르면 여기서 Changed 1회
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => spawned = false;

        public void RequestSet(bool on, PlayerRole requester)
        {
            if (!spawned || Object == null || !Object.IsValid) return;
            RPC_RequestSet(on, requester);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_RequestSet(NetworkBool on, PlayerRole requester)
        {
            if (AllowedRole != PlayerRole.None && requester != AllowedRole) return; // 거부: 아무 일도 없음
            if (on)
            {
                // 켜진 상태에서 다시 누르면 시간·타이머만 갱신
                LastOnTimeNet = Runner.SimulationTime;
                OffTimer = AutoOffSeconds > 0f ? TickTimer.CreateFromSeconds(Runner, AutoOffSeconds) : TickTimer.None;
            }
            else
            {
                OffTimer = TickTimer.None;
            }
            IsOnNet = on;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (IsOnNet && OffTimer.Expired(Runner))
            {
                IsOnNet = false;
                OffTimer = TickTimer.None;
            }
        }

        void OnIsOnChanged() => Sync();

        void Sync()
        {
            bool on = IsOnNet;
            if (observed == on) return;
            observed = on;
            Changed?.Invoke(on);
        }
    }
}
