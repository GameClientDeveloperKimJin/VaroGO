using System;
using Fusion;
using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// S-09 온라인 골 상태(계약 §3.4). Net Variant(Goal_Net)에서 LocalGoalState 옆에 추가된다(로컬 구현은 지우지 않음).
    /// 각 PC가 자기 역할의 진입/이탈만 보고 → 상태 권한(마스터)이 기록 → 두 역할이 동시에 안에 있으면 한 번만 클리어.
    /// 클리어는 모든 PC에서 ChapterCleared 1회. Spawned 전에는 IsCleared = false.
    /// </summary>
    [RequireComponent(typeof(LocalGoalState))]
    public class NetGoalState : NetworkBehaviour, IGoalState
    {
        [Networked] NetworkBool RedInside { get; set; }
        [Networked] NetworkBool BlueInside { get; set; }
        [Networked, OnChangedRender(nameof(OnClearedChanged))] NetworkBool ClearedNet { get; set; }

        bool observed;
        bool spawned;

        public bool IsCleared => observed;
        public event Action ChapterCleared;

        public override void Spawned()
        {
            spawned = true;
            Sync(); // 늦게 들어온 PC: 이미 클리어면 여기서 1회
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => spawned = false;

        public void ReportInGoal(PlayerRole role, bool inside)
        {
            if (!spawned || Object == null || !Object.IsValid || role == PlayerRole.None) return;
            RPC_Report(role, inside);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_Report(PlayerRole role, NetworkBool inside)
        {
            if (ClearedNet) return;
            if (role == PlayerRole.A_Red) RedInside = inside;
            else if (role == PlayerRole.B_Blue) BlueInside = inside;
            if (RedInside && BlueInside) ClearedNet = true;
        }

        /// <summary>
        /// S2-05: 나간 플레이어가 골 안에 있었으면 그 역할의 진입 표시를 지운다(C-1031-2 ③).
        /// 상태 권한에서만 동작하고, 지웠으면(또는 지울 게 없으면) true. 권한이 아직 안 넘어왔으면 false.
        /// </summary>
        internal bool ClearRole(PlayerRole role)
        {
            if (!spawned || !HasStateAuthority) return false;
            if (role == PlayerRole.A_Red) RedInside = false;
            else if (role == PlayerRole.B_Blue) BlueInside = false;
            return true;
        }

        /// <summary>활성 씬의 골 전부에서 role을 지운다. 모두 처리됐으면 true.</summary>
        internal static bool ClearRoleAll(PlayerRole role)
        {
            bool done = true;
            foreach (var g in FindObjectsByType<NetGoalState>(FindObjectsSortMode.None))
                if (g.spawned && !g.ClearRole(role)) done = false;
            return done;
        }

        void OnClearedChanged() => Sync();

        void Sync()
        {
            if (observed || !ClearedNet) return;
            observed = true;
            Debug.Log("[Net] 골 CLEAR (온라인)");
            ChapterCleared?.Invoke();
        }
    }
}
