using UnityEngine;
using UnityEngine.UI;
using VaroGO.Net;

namespace VaroGO.Client.UI
{
    // 로비 RoomStage 슬롯 이름표 옆 준비 배지 (2026-11-28 로비 ② 아트). 준비 완료 = 초록 체크, 아직 = 대기 점.
    // 슬롯마다 하나씩. 서버 RoomStageView(이름표 글자·테두리 색)는 그대로 둔다.
    public class RoomStageReadyBadge : MonoBehaviour
    {
        [Tooltip("0 = P1(방장), 1 = P2")]
        [SerializeField] int slotIndex;
        [SerializeField] Image badge;
        [SerializeField] Sprite readySprite;
        [SerializeField] Sprite waitSprite;

        void LateUpdate()
        {
            var net = NetBootstrap.Instance;
            if (net == null || badge == null) return;
            var s = net.GetSlot(slotIndex).Ready ? readySprite : waitSprite;
            if (s != null && badge.sprite != s) badge.sprite = s;
        }
    }
}
