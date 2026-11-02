using System;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// 구역 리셋 온라인 구현 (S2-07, 계약 §3.8).
    /// 요청 → NetSession 상태 권한(마스터)이 같은 Zone의 NetSwitchState를 StartOn으로 되돌림 → 모든 PC에서 ZoneReset 1회.
    /// Solo(NetSession 없음)는 이 PC가 권한이라 바로 되돌리고 이벤트를 낸다.
    /// </summary>
    public class NetZoneReset : IZoneReset
    {
        static NetZoneReset current;

        public event Action<int> ZoneReset;

        public NetZoneReset()
        {
            current = this;
        }

        public void RequestReset(int zone, PlayerRole requester)
        {
            var session = NetSession.Instance;
            if (session != null)
            {
                session.RequestZoneReset(zone);
                return;
            }
            NetSwitchState.ResetZone(zone);
            Raise(zone);
        }

        /// <summary>NetSession.RPC_ZoneResetDone이 모든 PC에서 부른다.</summary>
        internal static void Raise(int zone)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log($"[Net] 구역 리셋 zone={zone}");
#endif
            current?.ZoneReset?.Invoke(zone);
        }
    }
}
