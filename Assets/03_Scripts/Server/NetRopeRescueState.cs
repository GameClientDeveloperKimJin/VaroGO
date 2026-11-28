using System;
using Fusion;
using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// Stage2 Z3 밧줄 구조 온라인 공유 상태. RopeRescue와 같은 오브젝트(씬에 놓인 NetworkObject 루트)에 붙는다.
    /// 방장(상태 권한)이 단계(0 없음 → 1 주움 → 2 걸림 → 3 끝)를 정한다. 단계는 앞으로만 간다.
    /// 끌어올리기는 PullCount를 올려 모든 PC에 알리고, 끌려가는 본인 PC가 자기 캐릭터를 옮긴 뒤 단계 3을 요청한다.
    /// </summary>
    [RequireComponent(typeof(RopeRescue))]
    public class NetRopeRescueState : NetworkBehaviour, IRopeRescueState
    {
        [Networked, OnChangedRender(nameof(OnValuesChanged))] int StageNet { get; set; }
        [Networked] PlayerRole PullTargetNet { get; set; }
        [Networked, OnChangedRender(nameof(OnPullChanged))] int PullCountNet { get; set; }

        bool spawned;

        public bool Ready => spawned;
        public int Stage => spawned ? StageNet : 0;
        public int PullCount => spawned ? PullCountNet : 0;
        public PlayerRole PullTarget => spawned ? PullTargetNet : PlayerRole.None;
        public event Action Changed;
        public event Action Pulled;

        public override void Spawned()
        {
            spawned = true;
            Changed?.Invoke();
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => spawned = false;

        public void RequestStage(int stage)
        {
            if (!spawned || Object == null || !Object.IsValid) return;
            RPC_Stage(stage);
        }

        public void RequestPull(PlayerRole target)
        {
            if (!spawned || Object == null || !Object.IsValid) return;
            RPC_Pull(target);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_Stage(int stage)
        {
            if (stage < 1 || stage > 3 || stage <= StageNet) return;
            if (stage == 3 && StageNet < 2) return; // 걸기 전에 끝날 수 없다
            StageNet = stage;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_Pull(PlayerRole target)
        {
            if (StageNet != 2 || target == PlayerRole.None) return;
            PullTargetNet = target;
            PullCountNet++;
        }

        void OnValuesChanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Net] Z3 밧줄 단계={StageNet} (권한={HasStateAuthority})");
#endif
            Changed?.Invoke();
        }

        void OnPullChanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Net] Z3 밧줄 끌어올리기 대상={PullTargetNet} (권한={HasStateAuthority})");
#endif
            Pulled?.Invoke();
        }
    }
}
