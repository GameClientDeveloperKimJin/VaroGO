using System;

namespace VaroGO.Client
{
    // Stage2 Z1 유리 다리 온라인 공유 상태. 구현: VaroGO.Net.NetGlassBridgeState (같은 오브젝트).
    // 오프라인은 이 상태 없이 GlassBridge가 직접 들고 있다. 클라 코드는 Fusion을 모르고 이 인터페이스만 본다.
    public interface IGlassBridgeState
    {
        bool Ready { get; }        // 방장이 첫 배치를 뽑았는가
        int Round { get; }         // 누가 떨어질 때마다 1씩 증가 (새 배치)
        long Layout { get; }       // 칸마다 2비트: 0 가짜, 1 빨강 안전, 2 파랑 안전
        int BrokenMask { get; }    // 깨진 칸 비트
        event Action Changed;      // 모든 PC. 값이 바뀌면
        event Action Failed;       // 모든 PC. 누군가 떨어져 새 배치가 시작됨 → 각자 자기 캐릭터를 시작점으로

        void RequestBreak(int index, int round);
        void RequestFail(int round);
    }
}
