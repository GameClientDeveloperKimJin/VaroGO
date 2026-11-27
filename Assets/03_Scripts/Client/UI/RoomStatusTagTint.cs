using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VaroGO.Client.UI
{
    // 로비 방 목록 한 줄(RoomItem_Template)에 붙는다. 서버 LobbyUI가 StateText에 쓴 글자를 보고 상태 태그 색을 칠한다.
    // (LobbyUI는 줄을 SetActive(true) 한 뒤 글자를 쓰므로 OnEnable이 아니라 글자가 바뀔 때마다 본다)
    public class RoomStatusTagTint : MonoBehaviour
    {
        [SerializeField] TMP_Text stateText;
        [SerializeField] Image tag;
        [SerializeField] Color waiting = new(0.55f, 0.95f, 0.45f);
        [SerializeField] Color inGame = new(1f, 0.7f, 0.3f);
        [SerializeField] Color full = new(1f, 0.45f, 0.4f);
        [SerializeField] Color other = new(0.75f, 0.75f, 0.75f);

        string last;

        void LateUpdate()
        {
            if (stateText == null || tag == null) return;
            string s = stateText.text;
            if (s == last) return;
            last = s;
            tag.color = s == "대기 중" ? waiting : s == "게임 중" ? inGame : s == "가득 참" ? full : other;
        }
    }
}
