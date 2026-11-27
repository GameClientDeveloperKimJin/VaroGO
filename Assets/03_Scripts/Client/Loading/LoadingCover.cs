using UnityEngine;

namespace VaroGO.Client.Loading
{
    // 씬의 배경(AddressablePrefabLoader)이 다 뜰 때까지 화면 전체를 덮고, 끝나면 페이드로 걷는다.
    // 로비에서 숲 배경 없이 맨 무대(회색 벽·바닥)가 먼저 보이던 문제(2026-11-28)용. 클릭을 막고, 걷히면 막지 않는다.
    // 덮개 모양(Image 색·글자)은 이 오브젝트의 자식에서 바꾼다. 로더가 없거나 실패해도 걷는다(화면이 영원히 가려지지 않게).
    [RequireComponent(typeof(CanvasGroup))]
    public class LoadingCover : MonoBehaviour
    {
        [Tooltip("기다릴 로더. 비우면 씬에서 찾는다")]
        [SerializeField] AddressablePrefabLoader loader;
        [Tooltip("로드가 끝난 뒤 한 번 더 기다리는 시간(초). 생성 직후 첫 프레임 끊김을 가린다")]
        [SerializeField, Min(0f)] float holdAfterLoad = 0.15f;
        [Tooltip("걷히는 시간(초)")]
        [SerializeField, Min(0f)] float fadeOut = 0.4f;
        [Tooltip("이 시간(초)이 지나면 로드가 안 끝나도 걷는다")]
        [SerializeField, Min(1f)] float maxWait = 20f;

        CanvasGroup group;
        float doneAt = -1f;
        float startAt;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            startAt = Time.unscaledTime;
            if (loader == null) loader = AddressablePrefabLoader.FindInScene();
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (doneAt < 0f)
            {
                bool ready = loader == null || loader.IsDone || now - startAt > maxWait;
                if (!ready) return;
                doneAt = now + holdAfterLoad;
            }
            if (now < doneAt) return;
            float t = fadeOut > 0f ? (now - doneAt) / fadeOut : 1f;
            group.alpha = 1f - Mathf.Clamp01(t);
            if (t < 1f) return;
            group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }
    }
}
