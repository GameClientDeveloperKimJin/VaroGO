using System;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    public class OfflineRoleProvider : IRoleProvider
    {
        PlayerRole localRole = PlayerRole.A_Red;

        public PlayerRole LocalRole => localRole;
        public event Action<PlayerRole> LocalRoleChanged;

        public void SetRole(PlayerRole role)
        {
            if (role == localRole) return;
            localRole = role;
            LocalRoleChanged?.Invoke(role);
        }
    }

    public class OfflineGameClock : IGameClock
    {
        // 상태 누적이 없는지 확인하기 위한 테스트용 오프셋 (C-08 완료 조건)
        public double Offset { get; set; }
        public double Now => Time.timeAsDouble + Offset;
    }
}
