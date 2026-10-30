using System;
using VaroGO.Shared;

namespace VaroGO.Client.UI
{
    // C2-02 테스트용 가짜 로비. 서버 없이 로비 UI의 Ready/Start 흐름을 확인한다 (BootMenuUI.fakeLobby).
    // 방 만들기: 잠시 뒤 파트너가 들어오고, 조금 더 뒤 준비 완료 → 시작 버튼 활성.
    // 코드 참가: 바로 로비. 준비를 켜고 잠시 기다리면 가짜 호스트가 시작한다.
    // 시작하면 코스를 불러오지 않고 잠깐 InGame을 보여준 뒤 처음 화면으로 돌아온다.
    public class FakeSessionLobby : ISessionLobby
    {
        const float Step = 1.5f;

        SessionPhase phase = SessionPhase.Idle;
        float timer;
        bool localReady;

        public SessionPhase Phase => phase;
        public bool IsHost { get; private set; }
        public string Code { get; private set; } = "";
        public string Status { get; private set; } = "";
        public bool IsFailure => false;
        public bool PartnerConnected { get; private set; }
        public bool PartnerReady { get; private set; }
        public float PartnerLostSeconds => 0f;
        public event Action Changed;

        public void CreateRoom()
        {
            if (phase != SessionPhase.Idle) return;
            IsHost = true;
            Code = "TEST42";
            Go(SessionPhase.Connecting, "[가짜] 방 만드는 중…");
        }

        public void JoinRoom(string code)
        {
            if (phase != SessionPhase.Idle) return;
            IsHost = false;
            Code = string.IsNullOrEmpty(code) ? "TEST42" : code;
            Go(SessionPhase.Connecting, "[가짜] 참가 중…");
        }

        public void Leave()
        {
            Code = "";
            PartnerConnected = PartnerReady = localReady = false;
            Go(SessionPhase.Idle, "");
        }

        public void SetReady(bool ready)
        {
            if (IsHost || phase != SessionPhase.Lobby || ready == localReady) return;
            localReady = ready;
            timer = 0f;
            Changed?.Invoke();
        }

        public void StartGame()
        {
            if (!IsHost || phase != SessionPhase.Lobby || !PartnerReady) return;
            Go(SessionPhase.LoadingCourse, "[가짜] 코스 불러오는 중…");
        }

        // BootMenuUI.Update가 호출한다
        public void Tick(float deltaTime)
        {
            timer += deltaTime;
            if (timer < Step) return;

            switch (phase)
            {
                case SessionPhase.Connecting:
                    if (IsHost) Go(SessionPhase.WaitingPartner, "");
                    else { PartnerConnected = true; Go(SessionPhase.Lobby, ""); }
                    break;
                case SessionPhase.WaitingPartner:
                    PartnerConnected = true;
                    Go(SessionPhase.Lobby, "");
                    break;
                case SessionPhase.Lobby:
                    if (IsHost && !PartnerReady) { PartnerReady = true; timer = 0f; Changed?.Invoke(); }
                    else if (!IsHost && localReady) Go(SessionPhase.LoadingCourse, "[가짜] 호스트가 시작함");
                    break;
                case SessionPhase.LoadingCourse:
                    Go(SessionPhase.InGame, "");
                    break;
                case SessionPhase.InGame:
                    Leave();
                    Status = "[가짜] 시작 흐름 확인 끝";
                    break;
            }
        }

        void Go(SessionPhase next, string status)
        {
            phase = next;
            Status = status;
            timer = 0f;
            Changed?.Invoke();
        }
    }
}
