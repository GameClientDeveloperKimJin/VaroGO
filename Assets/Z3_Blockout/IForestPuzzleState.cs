using System;

namespace HiddenPath.Blockout
{
    // Z3 숲 퍼즐 온라인 공유 상태 (정답·진행도·라운드). 구현: VaroGO.Net.NetForestPuzzleState (같은 오브젝트).
    // 오프라인은 이 상태 없이 Z3ForestPuzzle이 직접 들고 있다. 클라 코드는 Fusion을 모르고 이 인터페이스만 본다 (R5).
    public interface IForestPuzzleState
    {
        bool Ready { get; }             // 방장이 첫 정답을 뽑았는가
        int RoundVersion { get; }       // 오답·추락 때마다 1씩 증가
        int ClearedForks { get; }       // 지금 라운드에서 맞힌 문제 수 (두 사람 공용)
        int Answer(int fork);           // 문제별 정답 길 (0~2)
        int LastFailFork { get; }       // 마지막 오답 문제 (-1 = 없음) — 붕괴 연출용
        int LastFailBranch { get; }
        event Action Changed;           // 모든 PC. 값이 바뀌면
        event Action Failed;            // 모든 PC. 누군가 오답·추락으로 새 라운드가 시작됨

        void RequestAdvance(int fork, int round);           // 정답을 밟은 로컬 PC
        void RequestFail(int fork, int branch, int round);  // 오답·추락한 로컬 PC (branch = -1이면 추락)
    }
}
