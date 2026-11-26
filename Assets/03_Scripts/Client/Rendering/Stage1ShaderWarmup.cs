using Unity.Jobs;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

namespace VaroGO.Client.Rendering
{
    // Stage1 처음 들어갈 때·처음 가는 구역에서 생기는 셰이더 준비 멈춤(빌드 1회차 7초)을 줄인다.
    // 게임 시작 직후(타이틀)부터 Stage1에서 쓰는 그래픽 상태(PSO)를 프레임마다 조금씩 미리 만든다.
    // 타이틀·로비에 있는 동안 끝나지 못하면 Stage1이 로드되는 순간 남은 것을 한 번에 만든다(로딩 중이라 플레이 중 끊김보다 낫다).
    // 기록 파일: Assets/Resources/Stage1Warmup.graphicsstate — 빌드에서 PerfRuntime -perftrace로 순회하며 만든다(성능_측정_계획.md 참고).
    // 그래픽 API·품질 단계가 기록 때와 다르면 해당 상태는 건너뛴다(오류 없음).
    static class Stage1ShaderWarmup
    {
        const string ResourceName = "Stage1Warmup"; // Assets/Resources/Stage1Warmup.graphicsstate (Resources.Load는 확장자 없이)
        const string StageScenePath = "Assets/01_Scenes/PlayScene_Stage1.unity";
        const int PerFrame = 20; // 한 프레임에 미리 만들 상태 수 (타이틀·로비 프레임을 크게 늦추지 않는 정도)

        static GraphicsStateCollection collection;
        static float startTime;
        static JobHandle pending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
#if UNITY_EDITOR
            return; // 에디터는 셰이더를 그때그때 만들어서 의미가 다르다 → 빌드에서만
#else
            var asset = Resources.Load<GraphicsStateCollection>(ResourceName);
            if (asset == null) { Debug.LogWarning("[Warmup] Resources/" + ResourceName + ".graphicsstate 없음 → 미리 준비 안 함"); return; }
            collection = asset;
            startTime = Time.realtimeSinceStartup;
            Debug.Log("[Warmup] Stage1 그래픽 상태 미리 준비 시작: " + collection.totalGraphicsStateCount + "개 (" + collection.graphicsDeviceType + ", " + collection.qualityLevelName + ")");
            SceneManager.sceneLoaded += OnSceneLoaded;
            var go = new GameObject("[Stage1ShaderWarmup]");
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Watcher>();
#endif
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (collection == null || collection.isWarmedUp || scene.path != StageScenePath) return;
            Debug.Log("[Warmup] Stage1 도착 시 남은 상태 " + (collection.totalGraphicsStateCount - collection.completedWarmupCount) + "개 → 한 번에 준비");
            pending.Complete();
            collection.WarmUp().Complete();
        }

        static void Done()
        {
            Debug.Log("[Warmup] 완료: " + collection.completedWarmupCount + "/" + collection.totalGraphicsStateCount + "개, "
                + (Time.realtimeSinceStartup - startTime).ToString("F1") + "초");
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        class Watcher : MonoBehaviour
        {
            void Update()
            {
                if (collection == null) { Destroy(gameObject); return; }
                if (!collection.isWarmedUp)
                {
                    // 한 번 호출 = 최대 PerFrame개 → 앞 작업이 끝나면 다음 묶음을 건다
                    if (pending.IsCompleted) { pending.Complete(); pending = collection.WarmUpProgressively(PerFrame); }
                    return;
                }
                pending.Complete();
                Done();
                Destroy(gameObject);
            }
        }
    }
}
