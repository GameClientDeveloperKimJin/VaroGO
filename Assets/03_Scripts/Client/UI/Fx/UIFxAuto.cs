using UnityEngine;
using UnityEngine.UI;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// 캔버스 루트에 붙이면 안의 모든 Button에 UIButtonFx를 자동으로 붙인다.
    /// 서버 LobbyUI가 런타임에 만드는 방 리스트 줄 버튼도 잡기 위해 주기적으로 다시 훑는다.
    /// </summary>
    public class UIFxAuto : MonoBehaviour
    {
        [SerializeField] float rescanInterval = 0.5f;
        float next;

        void OnEnable() => Scan();

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + rescanInterval;
            Scan();
        }

        void Scan()
        {
            foreach (var b in GetComponentsInChildren<Button>(true))
                if (b.GetComponent<UIButtonFx>() == null) b.gameObject.AddComponent<UIButtonFx>();
        }
    }
}
