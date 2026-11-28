using System;
using Fusion;
using UnityEngine;
using VaroGO.Client;

namespace VaroGO.Net
{
    /// <summary>
    /// Stage2 Z1 유리 다리 온라인 공유 상태. GlassBridge와 같은 오브젝트(씬에 놓인 NetworkObject 루트)에 붙는다.
    /// 방장(상태 권한)이 배치를 뽑고, 깨진 칸·라운드를 정한다.
    /// 누가 떨어지면 새 배치 + 깨진 칸 복구 + 라운드 증가 → 모든 PC에 Failed가 가고, 각자 자기 캐릭터를 시작점으로 보낸다.
    /// 배치는 칸마다 2비트(0 가짜, 1 빨강, 2 파랑) × 30칸 = 60비트를 long 하나에 담는다.
    /// </summary>
    [RequireComponent(typeof(GlassBridge))]
    public class NetGlassBridgeState : NetworkBehaviour, IGlassBridgeState
    {
        [Networked, OnChangedRender(nameof(OnValuesChanged))] long LayoutNet { get; set; }
        [Networked, OnChangedRender(nameof(OnValuesChanged))] int BrokenNet { get; set; }
        [Networked, OnChangedRender(nameof(OnValuesChanged))] int RoundNet { get; set; }
        [Networked, OnChangedRender(nameof(OnFailChanged))] int FailCount { get; set; }

        bool spawned;

        public bool Ready => spawned && RoundNet > 0;
        public int Round => spawned ? RoundNet : 0;
        public long Layout => spawned ? LayoutNet : 0;
        public int BrokenMask => spawned ? BrokenNet : 0;
        public event Action Changed;
        public event Action Failed;

        public override void Spawned()
        {
            spawned = true;
            if (HasStateAuthority && RoundNet == 0)
            {
                LayoutNet = GlassBridge.Generate(0);
                BrokenNet = 0;
                RoundNet = 1;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Net] Z1 유리 다리 접속 시 라운드={RoundNet} 배치={LayoutNet:X} 깨짐={Convert.ToString(BrokenNet, 2)} (권한={HasStateAuthority})");
#endif
            Changed?.Invoke();
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => spawned = false;

        public void RequestBreak(int index, int round)
        {
            if (!spawned || Object == null || !Object.IsValid) return;
            RPC_Break(index, round);
        }

        public void RequestFail(int round)
        {
            if (!spawned || Object == null || !Object.IsValid) return;
            RPC_Fail(round);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_Break(int index, int round)
        {
            if (round != RoundNet || index < 0 || index >= GlassBridge.TileCount) return;
            BrokenNet |= 1 << index;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_Fail(int round)
        {
            if (round != RoundNet) return; // 이미 새 라운드 (두 사람이 거의 동시에 떨어짐)
            LayoutNet = GlassBridge.Generate(LayoutNet);
            BrokenNet = 0;
            RoundNet++;
            FailCount++;
        }

        void OnValuesChanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Net] Z1 유리 다리 라운드={RoundNet} 배치={LayoutNet:X} 깨짐={Convert.ToString(BrokenNet, 2)} (권한={HasStateAuthority})");
#endif
            Changed?.Invoke();
        }

        void OnFailChanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Net] Z1 유리 다리 추락 → 새 배치 라운드={RoundNet} (권한={HasStateAuthority})");
#endif
            Failed?.Invoke();
        }
    }
}
