using UnityEngine;
using VaroGO.Net;
using VaroGO.Shared;

namespace VaroGO.Client
{
    /// <summary>
    /// 로비 대기실 무대 슬롯 발밑의 팀 발판. 고른 팀의 발판만 켠다 (레드 / 블루, 안 골랐으면 기본 발판).
    /// Slot_P1/Slot_P2 의 Occupied/TeamPad 에 붙인다. 슬롯이 비면 Occupied가 꺼지므로 발판도 같이 숨는다.
    /// 팀 값은 RoomStageCharacters와 같은 곳(NetBootstrap.GetSlot)에서 읽는다.
    /// </summary>
    public class RoomStageTeamPad : MonoBehaviour
    {
        [Tooltip("0 = P1(방장), 1 = P2")]
        [SerializeField] int slotIndex;
        [SerializeField] GameObject redPad;
        [SerializeField] GameObject bluePad;
        [Tooltip("팀을 안 골랐을 때 (없어도 됨)")]
        [SerializeField] GameObject nonePad;

        void Awake() => Show(PlayerRole.None);

        void Update()
        {
            var net = NetBootstrap.Instance;
            Show(net != null ? net.GetSlot(slotIndex).Team : PlayerRole.None);
        }

        void Show(PlayerRole team)
        {
            Set(redPad, team == PlayerRole.A_Red);
            Set(bluePad, team == PlayerRole.B_Blue);
            Set(nonePad, team == PlayerRole.None);
        }

        static void Set(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }
    }
}
