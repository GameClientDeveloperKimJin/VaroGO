using System;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>내 PC의 역할. 접속 직후엔 None일 수 있다(계약 §3.1). 같은 값 재설정은 이벤트를 내지 않는다.</summary>
    public class NetRoleProvider : IRoleProvider
    {
        PlayerRole localRole = PlayerRole.None;

        public PlayerRole LocalRole => localRole;
        public event Action<PlayerRole> LocalRoleChanged;

        public void SetRole(PlayerRole role)
        {
            if (role == localRole) return;
            localRole = role;
            LocalRoleChanged?.Invoke(role);
        }
    }
}
