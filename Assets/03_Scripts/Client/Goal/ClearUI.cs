using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // C-12: ChapterCleared가 오면 화면 중앙에 "CLEAR!", 효과음, 3초 뒤 "R: 다시 하기".
    // IGoalState만 구독한다. 그레이박스용 OnGUI라 정식 UI가 들어오면 교체한다.
    public class ClearUI : MonoBehaviour
    {
        [Tooltip("연결할 골 상태 오브젝트 (골 상태 컴포넌트가 붙은 GameObject)")]
        [SerializeField] GameObject stateObject;
        [SerializeField, HideInInspector] MonoBehaviour stateSource; // 예전 참조. OnValidate에서 stateObject로 옮긴다
        [Tooltip("CLEAR 뒤 다시 하기 안내까지 걸리는 시간 (초)")]
        [SerializeField] float retryHintDelay = 3f;
        [SerializeField] AudioSource audioSource;
        [Tooltip("비어 있으면 임시 합성음을 쓴다")]
        [SerializeField] AudioClip clearClip;
        [Range(0f, 1f)]
        [SerializeField] float volume = 0.7f;

        IGoalState state;
        float clearedAt = -1f;
        GUIStyle titleStyle, hintStyle;

        void Awake()
        {
            state = SwitchStates.ResolveGoal(stateObject, stateSource, this);
            if (clearClip == null) clearClip = TempSfx.Fanfare;
        }

        void OnValidate()
        {
            if (stateObject == null && stateSource != null) stateObject = stateSource.gameObject;
        }

        void OnEnable()
        {
            if (state == null) return;
            state.ChapterCleared += OnCleared;
            if (state.IsCleared) OnCleared();
        }

        void OnDisable()
        {
            if (state != null) state.ChapterCleared -= OnCleared;
        }

        void OnCleared()
        {
            if (clearedAt >= 0f) return;
            clearedAt = Time.unscaledTime;
            if (audioSource != null) audioSource.PlayOneShot(clearClip, volume);
            else AudioSource.PlayClipAtPoint(clearClip, Camera.main != null ? Camera.main.transform.position : transform.position, volume);
        }

        bool RetryReady => clearedAt >= 0f && Time.unscaledTime - clearedAt >= retryHintDelay;

        void Update()
        {
            // 온라인 재시작은 서버 쪽(세션 단위)에서 처리한다
            if (!RetryReady || GameServices.IsOnline) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart();
        }

        static void Restart()
        {
            var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // Build Settings에 없는 씬(10_DemoRoom)도 다시 열 수 있다. 디스크에 저장된 내용으로 열린다
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        }

        void OnGUI()
        {
            if (clearedAt < 0f) return;
            titleStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 72, fontStyle = FontStyle.Bold };
            hintStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24 };

            // 0.3초 동안 커지며 등장
            float t = Mathf.Clamp01((Time.unscaledTime - clearedAt) / 0.3f);
            titleStyle.fontSize = Mathf.RoundToInt(Mathf.Lerp(110f, 72f, t));
            var titleRect = new Rect(0, Screen.height * 0.5f - 60, Screen.width, 120);
            titleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.6f * t);
            GUI.Label(new Rect(titleRect.x + 3, titleRect.y + 3, titleRect.width, titleRect.height), "CLEAR!", titleStyle);
            titleStyle.normal.textColor = new Color(1f, 0.85f, 0.2f, t);
            GUI.Label(titleRect, "CLEAR!", titleStyle);

            if (RetryReady && !GameServices.IsOnline)
                GUI.Label(new Rect(0, Screen.height * 0.5f + 60, Screen.width, 40), "R: 다시 하기", hintStyle);
        }
    }
}
