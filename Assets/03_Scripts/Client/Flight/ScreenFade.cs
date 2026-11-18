using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VaroGO.Client
{
    // 화면 전체를 검게 덮는 페이드. 씬이 바뀌어도 남아 있다가(DontDestroyOnLoad), 새 씬이 열리면 서서히 밝아진다.
    // 비행 씬 끝(소용돌이로 빨려 들어감) → 검은 화면 → 스테이지 2가 밝아지며 나타나는 연결에 쓴다. 포탈 진입에도 쓴다.
    public class ScreenFade : MonoBehaviour
    {
        static ScreenFade instance;

        Image image;
        float alpha;
        float target;
        float speed = 1f;
        bool fadeInOnNextScene;
        float fadeInDuration = 1.5f;
        float holdUntil; // 새 씬이 열린 뒤 잠깐 검은 채로 둔다 (첫 프레임 튐 방지)

        public static float Alpha => instance != null ? instance.alpha : 0f;

        static ScreenFade Get()
        {
            if (instance != null) return instance;
            var go = new GameObject("ScreenFade");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var imgGo = new GameObject("Black");
            imgGo.transform.SetParent(go.transform, false);
            var rt = imgGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = imgGo.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;
            instance = go.AddComponent<ScreenFade>();
            instance.image = img;
            SceneManager.sceneLoaded += instance.OnSceneLoaded;
            return instance;
        }

        // 지금 검기를 바로 정한다 (0 = 투명, 1 = 완전 검정). 진행도로 직접 조절할 때
        public static void Set(float a)
        {
            var f = Get();
            f.alpha = f.target = Mathf.Clamp01(a);
            f.Apply();
        }

        // duration초 동안 검게. keepForNextScene이면 다음 씬이 열린 뒤 inDuration초 동안 밝아진다
        public static void FadeOut(float duration, bool keepForNextScene, float inDuration = 1.5f)
        {
            var f = Get();
            f.target = 1f;
            f.speed = 1f / Mathf.Max(0.01f, duration);
            f.fadeInOnNextScene = keepForNextScene;
            f.fadeInDuration = inDuration;
        }

        public static void FadeIn(float duration)
        {
            var f = Get();
            f.target = 0f;
            f.speed = 1f / Mathf.Max(0.01f, duration);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || !fadeInOnNextScene) return;
            fadeInOnNextScene = false;
            alpha = 1f;
            target = 0f;
            speed = 1f / Mathf.Max(0.01f, fadeInDuration);
            holdUntil = Time.unscaledTime + 0.3f;
            Apply();
        }

        void Update()
        {
            if (Time.unscaledTime < holdUntil) return;
            if (!Mathf.Approximately(alpha, target))
            {
                alpha = Mathf.MoveTowards(alpha, target, speed * Time.unscaledDeltaTime);
                Apply();
            }
        }

        void Apply()
        {
            if (image != null) image.color = new Color(0f, 0f, 0f, alpha);
            if (image != null) image.enabled = alpha > 0.001f;
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                instance = null;
            }
        }
    }
}
