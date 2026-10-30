using System;
using System.Collections.Generic;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // C2-05: 오프라인 공용 체크포인트 상태. OfflineBootstrap이 씬마다 새로 등록한다.
    // 온라인에서는 서버의 NetCheckpointState가 같은 자리를 대신한다 (없으면 이 구현이 PC별로 동작).
    // 오프라인은 혼자 테스트하므로 기본은 한 역할만 도착해도 활성.
    public class LocalCheckpointState : ICheckpointState
    {
        readonly bool requireBoth;
        readonly HashSet<int> reachedRed = new HashSet<int>();
        readonly HashSet<int> reachedBlue = new HashSet<int>();
        int activeOrder = -1;

        public LocalCheckpointState(bool requireBoth)
        {
            this.requireBoth = requireBoth;
        }

        public int ActiveOrder => activeOrder;
        public event Action Changed;

        public bool IsReached(int order, PlayerRole role)
        {
            var set = SetOf(role);
            return set != null && set.Contains(order);
        }

        public void ReportReached(int order, PlayerRole role)
        {
            var set = SetOf(role);
            if (set == null || !set.Add(order)) return;

            bool active = requireBoth ? reachedRed.Contains(order) && reachedBlue.Contains(order) : true;
            // 앞 구간으로만 갱신. 아래층으로 떨어져 이전 체크포인트를 밟아도 되돌아가지 않는다
            if (active && order > activeOrder) activeOrder = order;
            Changed?.Invoke();
        }

        public bool CanFastTravel(int order) => order <= activeOrder;

        HashSet<int> SetOf(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.A_Red: return reachedRed;
                case PlayerRole.B_Blue: return reachedBlue;
                default: return null;
            }
        }
    }
}
