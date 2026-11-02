using System.Linq;
using Fusion;

namespace VaroGO.Net
{
    /// <summary>
    /// 세션 공용 상태 (S2-01·02). 00_Boot에서 마스터가 스폰한다.
    /// 스폰 플래그 DontDestroyOnLoad(코스 씬으로 넘어가도 유지) + SharedModeStateAuthMasterClient(상태 권한 = 항상 현재 마스터,
    /// 마스터가 나가면 새 마스터로 넘어감). 프리팹: 02_Prefabs/Net/NetSession.prefab
    /// 지금은 게스트 Ready만 둔다. 끊김 대기(S2-04)·시계 정지도 여기에 붙일 예정.
    /// </summary>
    public class NetSession : NetworkBehaviour
    {
        public static NetSession Instance { get; private set; }

        [Networked] NetworkBool GuestReadyNet { get; set; }

        bool spawned;

        /// <summary>게스트가 준비를 눌렀는지. 스폰 전에는 false.</summary>
        public bool GuestReady => spawned && GuestReadyNet;

        public override void Spawned()
        {
            spawned = true;
            Instance = this;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            spawned = false;
            if (Instance == this) Instance = null;
        }

        /// <summary>게스트 PC에서 호출. 상태 권한(마스터)이 값을 바꾼다. 결과는 GuestReady로 확인.</summary>
        public void RequestReady(bool ready)
        {
            if (!spawned) return;
            RPC_SetReady(ready);
        }

        /// <summary>상태 권한에서만. 게스트가 나갔을 때 등 Ready를 내린다.</summary>
        public void ClearReady()
        {
            if (spawned && HasStateAuthority) GuestReadyNet = false;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_SetReady(NetworkBool ready)
        {
            GuestReadyNet = ready;
        }

        public override void FixedUpdateNetwork()
        {
            // S2-02: 혼자 남으면 Ready 해제 (게스트가 나갔거나, 마스터가 나가서 게스트가 새 마스터가 된 경우)
            if (HasStateAuthority && GuestReadyNet && Runner.ActivePlayers.Count() < 2)
                GuestReadyNet = false;
        }
    }
}
