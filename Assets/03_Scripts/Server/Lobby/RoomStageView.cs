using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VaroGO.Shared;
using static VaroGO.Net.Lobby.LobbyUiUtil;

namespace VaroGO.Net.Lobby
{
    /// <summary>
    /// LobbyScene 대기실 3D 무대(RoomStage, 클라 세팅 C-1104-1)에 붙는 서버 연결 스크립트.
    /// Slot_P1 = 방장, Slot_P2 = 참가자. 사람이 있으면 Occupied, 없으면 Empty.
    /// 이름표 `P1 · 방장 · 블루`, 이름표 테두리(Plate Outline) = 팀 색, 발밑 준비 상태.
    /// </summary>
    public class RoomStageView : MonoBehaviour
    {
        static readonly Color RedColor = new Color(0.90f, 0.25f, 0.25f);
        static readonly Color BlueColor = new Color(0.25f, 0.45f, 0.95f);
        static readonly Color NoTeamColor = new Color(0.6f, 0.6f, 0.6f);

        class Slot
        {
            public GameObject occupied, empty;
            public TMP_Text name, ready;
            public Outline plate;
        }

        readonly Slot[] slots = new Slot[2];

        void Awake()
        {
            for (int i = 0; i < 2; i++)
            {
                string p = $"Slot_P{i + 1}";
                slots[i] = new Slot
                {
                    occupied = FindGo(transform, $"{p}/Occupied"),
                    empty = FindGo(transform, $"{p}/Empty"),
                    name = Find<TMP_Text>(transform, $"{p}/Occupied/NameTag/NameText"),
                    plate = Find<Outline>(transform, $"{p}/Occupied/NameTag/Plate"),
                    ready = Find<TMP_Text>(transform, $"{p}/Occupied/ReadyState/ReadyStateText"),
                };
            }
        }

        void Update()
        {
            var net = NetBootstrap.Instance;
            if (net == null) return;
            bool inRoom = net.State == SessionState.WaitingPartner || net.State == SessionState.Lobby;
            if (!inRoom) return; // 방 리스트 화면에선 불투명 패널이 무대를 가린다

            for (int i = 0; i < 2; i++)
            {
                var s = slots[i];
                var info = net.GetSlot(i);
                SetActive(s.occupied, info.Present);
                SetActive(s.empty, !info.Present);
                if (!info.Present) continue;

                // 이름표 = 닉네임 (C-1109-1). 닉네임이 아직 없으면 예전처럼 P1/P2. 팀은 발밑 팀 발판(클라 RoomStageTeamPad)이 보여 준다
                string who = string.IsNullOrEmpty(info.Nickname) ? (i == 0 ? "P1" : "P2") : info.Nickname;
                SetText(s.name, i == 0 ? $"{who} · 방장" : who);
                if (s.plate != null)
                    s.plate.effectColor = info.Team == PlayerRole.A_Red ? RedColor : info.Team == PlayerRole.B_Blue ? BlueColor : NoTeamColor;
                // 발밑 문구는 쓰지 않는다 (2026-11-09 기획: 팀은 발판, 준비는 대기실 UI가 보여 줌)
                SetText(s.ready, string.Empty);
            }
        }
    }
}
