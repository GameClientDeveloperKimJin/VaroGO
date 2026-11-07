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

                string label = i == 0 ? "P1 · 방장" : "P2";
                if (info.Team != PlayerRole.None) label += $" · {TeamName(info.Team)}";
                SetText(s.name, label);
                if (s.plate != null)
                    s.plate.effectColor = info.Team == PlayerRole.A_Red ? RedColor : info.Team == PlayerRole.B_Blue ? BlueColor : NoTeamColor;
                SetText(s.ready, info.Team == PlayerRole.None ? "팀 미선택" : info.Ready ? "준비 완료" : "준비 전");
            }
        }
    }
}
