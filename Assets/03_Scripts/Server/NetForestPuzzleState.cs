using System;
using Fusion;
using HiddenPath.Blockout;
using UnityEngine;

namespace VaroGO.Net
{
    /// <summary>
    /// Z3 숲 퍼즐 온라인 공유 상태. Z3ForestPuzzle과 같은 오브젝트(NetworkObject 루트)에 붙는다.
    /// 방장(상태 권한)이 정답을 뽑고, 진행도·라운드를 정한다. 두 PC는 같은 단서·정답을 보고,
    /// 누가 틀리거나 떨어지면 새 라운드(새 정답, 진행도 0)가 두 사람 모두에게 시작된다.
    /// 정답은 문제당 2비트로 한 int에 담는다 (최대 15문제).
    /// </summary>
    [RequireComponent(typeof(Z3ForestPuzzle))]
    public class NetForestPuzzleState : NetworkBehaviour, IForestPuzzleState
    {
        [Networked, OnChangedRender(nameof(OnValuesChanged))] int AnswersPacked { get; set; }
        [Networked, OnChangedRender(nameof(OnValuesChanged))] int Round { get; set; }
        [Networked, OnChangedRender(nameof(OnValuesChanged))] int Cleared { get; set; }
        // (문제 × 4 + 길 + 1). 0 = 없음, 길 3 = 추락
        [Networked] int FailCode { get; set; }
        [Networked, OnChangedRender(nameof(OnFailChanged))] int FailCount { get; set; }

        Z3ForestPuzzle puzzle;
        bool spawned;

        public bool Ready => spawned && Round > 0;
        public int RoundVersion => spawned ? Round : 0;
        public int ClearedForks => spawned ? Cleared : 0;
        public int Answer(int fork) => spawned && fork >= 0 && fork < 15 ? (AnswersPacked >> (fork * 2)) & 3 : 0;
        public int LastFailFork => spawned && FailCode > 0 ? (FailCode - 1) / 4 : -1;
        public int LastFailBranch => spawned && FailCode > 0 ? ((FailCode - 1) % 4 == 3 ? -1 : (FailCode - 1) % 4) : -1;
        public event Action Changed;
        public event Action Failed;

        int ForkCount => Mathf.Clamp(puzzle != null && puzzle.forkReturns != null ? puzzle.forkReturns.Length : 0, 0, 15);

        void Awake() => puzzle = GetComponent<Z3ForestPuzzle>();

        public override void Spawned()
        {
            spawned = true;
            if (HasStateAuthority && Round == 0)
            {
                AnswersPacked = Roll(-1);
                Cleared = 0;
                Round = 1;
            }
            Changed?.Invoke();
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => spawned = false;

        // 이전 정답과 다른 값으로 다시 뽑는다 (오프라인 NewRound와 같은 규칙). previous = -1이면 처음
        int Roll(int previous)
        {
            int packed = 0;
            for (int i = 0; i < ForkCount; i++)
            {
                int next;
                if (previous < 0) next = UnityEngine.Random.Range(0, 3);
                else
                {
                    int prev = (previous >> (i * 2)) & 3;
                    next = UnityEngine.Random.Range(0, 2);
                    if (next >= prev) next++;
                }
                packed |= next << (i * 2);
            }
            return packed;
        }

        public void RequestAdvance(int fork, int round)
        {
            if (!spawned || Object == null || !Object.IsValid) return;
            RPC_Advance(fork, round);
        }

        public void RequestFail(int fork, int branch, int round)
        {
            if (!spawned || Object == null || !Object.IsValid) return;
            RPC_Fail(fork, branch, round);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_Advance(int fork, int round)
        {
            if (round != Round || fork != Cleared || fork >= ForkCount) return;
            Cleared = fork + 1;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_Fail(int fork, int branch, int round)
        {
            if (round != Round) return; // 이미 새 라운드 (두 사람이 거의 동시에 틀림)
            AnswersPacked = Roll(AnswersPacked);
            Cleared = 0;
            Round++;
            FailCode = Mathf.Clamp(fork, 0, 14) * 4 + (branch < 0 ? 3 : Mathf.Clamp(branch, 0, 2)) + 1;
            FailCount++;
        }

        void OnValuesChanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var ans = "";
            for (int i = 0; i < ForkCount; i++) ans += Answer(i);
            Debug.Log($"[Net] Z3 퍼즐 라운드={Round} 진행={Cleared} 정답={ans} (권한={HasStateAuthority})");
#endif
            Changed?.Invoke();
        }

        void OnFailChanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Net] Z3 퍼즐 오답·추락 문제={LastFailFork} 길={LastFailBranch} (권한={HasStateAuthority})");
#endif
            Failed?.Invoke();
        }
    }
}
