using System;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // Stage2 Z3 밧줄 구조 온라인 공유 상태. 구현: VaroGO.Net.NetRopeRescueState (같은 오브젝트).
    // 오프라인은 이 상태 없이 RopeRescue가 직접 들고 있다.
    public interface IRopeRescueState
    {
        bool Ready { get; }
        int Stage { get; }                 // 0 밧줄 없음, 1 주움, 2 고리에 걸림, 3 구조 끝(출구 열림)
        int PullCount { get; }             // 끌어올리기를 시작할 때마다 1씩 증가
        PlayerRole PullTarget { get; }     // 마지막으로 끌어올리는 사람
        event Action Changed;              // 모든 PC
        event Action Pulled;               // 모든 PC. 끌어올리기 시작 → PullTarget 본인 PC가 자기 캐릭터를 옮긴다

        void RequestStage(int stage);      // 다음 단계로 (줍기 1, 걸기 2, 끝 3). 되돌아가지 않는다
        void RequestPull(PlayerRole target);
    }
}
