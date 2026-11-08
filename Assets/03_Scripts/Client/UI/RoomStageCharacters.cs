using UnityEngine;
using VaroGO.Net;
using VaroGO.Shared;

namespace VaroGO.Client
{
    /// <summary>
    /// 로비 대기실 무대(RoomStage) 슬롯에 고른 팀의 캐릭터를 세운다. 레드 = C1, 블루 = C2.
    /// 팀을 아직 안 골랐으면 P1 = C1, P2 = C2. 슬롯이 비면 Occupied가 꺼지므로 모델도 같이 숨는다 (RoomStageView).
    /// 슬롯마다 하나씩, Slot_P1/Slot_P2 의 CharacterAnchor 에 붙인다.
    /// </summary>
    public class RoomStageCharacters : MonoBehaviour
    {
        [Tooltip("0 = P1(방장), 1 = P2")]
        [SerializeField] int slotIndex;
        [SerializeField] GameObject redModel;
        [SerializeField] GameObject blueModel;

        void Awake() => Show(slotIndex == 0);

        void Update()
        {
            var net = NetBootstrap.Instance;
            if (net == null) return;
            var team = net.GetSlot(slotIndex).Team;
            Show(team == PlayerRole.A_Red || (team == PlayerRole.None && slotIndex == 0));
        }

        void Show(bool red)
        {
            if (redModel != null && redModel.activeSelf != red) redModel.SetActive(red);
            if (blueModel != null && blueModel.activeSelf == red) blueModel.SetActive(!red);
        }
    }
}
