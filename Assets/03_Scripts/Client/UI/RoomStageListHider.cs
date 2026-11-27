using UnityEngine;
using VaroGO.Net;

namespace VaroGO.Client.UI
{
    // 로비 RoomStage에 붙는다. 방 목록 화면(방 밖)에서는 대기실 슬롯(캐릭터·포털·이름표)을 숨기고, 방에 들어가면 다시 보인다.
    // 방 목록 판 바깥으로 숲 배경이 보이게 하면서(2026-11-28 KJ) 빈 무대가 비치지 않게 하려는 것. 서버 RoomStageView는 그대로 둔다.
    public class RoomStageListHider : MonoBehaviour
    {
        [Tooltip("방 밖에서 숨길 오브젝트 (Slot_P1, Slot_P2)")]
        [SerializeField] GameObject[] hideOutsideRoom;

        bool? shown;

        void Update()
        {
            var net = NetBootstrap.Instance;
            bool inRoom = net != null && (net.State == SessionState.WaitingPartner || net.State == SessionState.Lobby);
            if (shown == inRoom) return;
            shown = inRoom;
            foreach (var go in hideOutsideRoom)
                if (go != null) go.SetActive(inRoom);
        }
    }
}
