using UnityEngine;
using UnityEngine.SceneManagement;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // 스테이지 클리어 뒤 다음 스테이지 씬으로 넘어간다 (스테이지 1 → 스테이지 2 비행 보너스).
    // 골 상태의 ChapterCleared를 받고 delay초 뒤 이동. 온라인은 방장 PC가 씬을 바꾸고 모두 함께 넘어간다.
    public class StageExit : MonoBehaviour
    {
        [Tooltip("연결할 골 상태 오브젝트 (골 상태 컴포넌트가 붙은 GameObject)")]
        [SerializeField] GameObject goalStateObject;
        [Tooltip("다음 씬 경로 (Build Settings에 있어야 온라인·빌드에서 열린다)")]
        [SerializeField] string nextScenePath = "Assets/01_Scenes/PlayScene_Stage2.unity";
        [Tooltip("CLEAR 뒤 다음 스테이지로 넘어가기까지 (초)")]
        [SerializeField, Min(0f)] float delay = 4f;

        IGoalState goal;
        bool cleared;
        float clearedAtLocal = -1f; // 동기화된 클리어 시각을 못 얻을 때만 쓰는 대체값
        bool loading;
        GUIStyle style;

        void Awake()
        {
            goal = SwitchStates.ResolveGoal(goalStateObject, null, this);
        }

        void OnEnable()
        {
            if (goal == null) return;
            goal.ChapterCleared += OnCleared;
            if (goal.IsCleared) OnCleared();
        }

        void OnDisable()
        {
            if (goal != null) goal.ChapterCleared -= OnCleared;
        }

        void OnCleared()
        {
            if (cleared) return;
            cleared = true;
            clearedAtLocal = Time.unscaledTime;
        }

        // 클리어 뒤 지난 시간(초). 골 상태가 클리어 시각(GameClock)을 주면 그걸로 계산해 두 PC가 같은 숫자를 본다
        float Elapsed()
        {
            var clock = GameServices.Clock;
            if (clock != null && goal is IGoalClearTime t && t.ClearedTime >= 0d)
                return Mathf.Max(0f, (float)(clock.Now - t.ClearedTime));
            return Time.unscaledTime - clearedAtLocal;
        }

        void Update()
        {
            if (loading || !cleared || Elapsed() < delay) return;
            loading = true;
            StageLoading.Load(nextScenePath);
        }

        void OnGUI()
        {
            if (!cleared || string.IsNullOrEmpty(nextScenePath)) return;
            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
            float left = Mathf.Max(0f, delay - Elapsed());
            GUI.Label(new Rect(0, Screen.height * 0.5f + 100, Screen.width, 36), loading ? "다음 스테이지로 이동 중…" : $"{Mathf.CeilToInt(left)}초 뒤 다음 스테이지로", style);
        }
    }

    // 씬 이동 공통. 온라인 = GameServices.StageLoader(방장만 실제로 바꾼다), 오프라인 = 바로 연다
    public static class StageLoading
    {
        public static void Load(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return;
            if (GameServices.IsOnline)
            {
                if (GameServices.StageLoader != null) GameServices.StageLoader.LoadStage(scenePath);
                else Debug.LogWarning("[Stage] 온라인인데 StageLoader가 없습니다");
                return;
            }
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            int index = SceneUtility.GetBuildIndexByScenePath(scenePath);
            if (index >= 0) SceneManager.LoadScene(index);
            else UnityEngine.AddressableAssets.Addressables.LoadSceneAsync(scenePath, LoadSceneMode.Single); // 스테이지 씬은 Addressable 씬 (주소 = 씬 경로)
#endif
        }
    }
}
