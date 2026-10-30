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
    // C2-01: 오프라인은 로비 없이 바로 코스. 방 관련 호출은 무시한다
    public class OfflineSession : ISessionLobby
    {
        public SessionPhase Phase => SessionPhase.InGame;
        public bool IsHost => true;
        public string Code => "";
        public string Status => "오프라인";
        public bool IsFailure => false;
        public bool PartnerConnected => false;
        public bool PartnerReady => false;
        public float PartnerLostSeconds => 0f;
        public event Action Changed { add { } remove { } }
        public void CreateRoom() { }
        public void JoinRoom(string code) { }
        public void Leave() { }
        public void SetReady(bool ready) { }
        public void StartGame() { }
    }

    // C2-01: 요청 즉시 이 PC에 리셋을 알린다
    public class OfflineZoneReset : IZoneReset
    {
        public event Action<int> ZoneReset;

        public void RequestReset(int zone, PlayerRole requester)
        {
            ZoneReset?.Invoke(zone);
        }
    }

    // C2-01: 요청 즉시 이 PC에 밀림을 알린다. 미는 쪽은 대상이 아닌 역할로 본다
    public class OfflinePlayerInteraction : IPlayerInteraction
    {
        public event Action<PlayerRole, Vector3> Pushed;

        public void RequestPush(PlayerRole target, Vector3 dir)
        {
            var from = target == PlayerRole.A_Red ? PlayerRole.B_Blue : PlayerRole.A_Red;
            Pushed?.Invoke(from, dir);
        }
    }
}
