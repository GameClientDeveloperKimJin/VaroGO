using UnityEngine;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// 씬에 미리 놓는 공용 UI(일시정지 메뉴·보이스 HUD) 루트. 씬이 바뀌어도 유지한다.
    /// 여러 씬에 놓아도 된다: 먼저 살아 있던 것만 남고, 새 씬에 있던 것은 스스로 지운다.
    /// (00_Boot에서 시작하든 코스 씬에서 바로 시작하든 하나만 있게)
    /// </summary>
    [DefaultExecutionOrder(-1000)] // 자식 UI보다 먼저 Awake → 중복이면 자식이 깨어나기 전에 끈다
    public class PersistentUI : MonoBehaviour
    {
        static PersistentUI instance;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            instance = this;
            if (transform.parent != null) transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
