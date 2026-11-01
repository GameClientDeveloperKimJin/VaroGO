using UnityEngine;
using UnityEngine.SceneManagement;
using VaroGO.Shared;

namespace VaroGO.Client.Voice
{
    /// <summary>
    /// 인게임 보이스 클라 쪽 관리자. 씬에 따로 놓지 않는다(게임 시작 시 스스로 하나 만들어 유지).
    /// - Current: 온라인 보이스(GameServices.Voice)가 있으면 그것, 없으면 OfflineVoiceChat
    /// - 보이스가 바뀌면 저장된 설정(마이크 켜짐·장치)을 다시 적용
    /// - 상대 목소리 AudioSource에 공간 음성(거리·방향·벽 가림)과 상대 음성 볼륨 적용
    /// - AudioListener를 카메라 대신 내 캐릭터 머리 위치에 둔다 (3인칭 카메라는 캐릭터 뒤라 거리가 틀어짐)
    /// </summary>
    public class VoiceManager : MonoBehaviour
    {
        const float ListenerHeight = 1.5f;

        static VoiceManager instance;
        OfflineVoiceChat offline;
        IVoiceChat applied;
        readonly VoiceSpatialAudio spatial = new VoiceSpatialAudio();

        AudioListener ownListener;
        AudioListener[] disabledListeners = new AudioListener[0];

        public static IVoiceChat Current
        {
            get
            {
                if (GameServices.Voice != null) return GameServices.Voice;
                return instance != null ? instance.offline : null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("VoiceManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<VoiceManager>();
        }

        void Awake()
        {
            offline = gameObject.AddComponent<OfflineVoiceChat>();
            var listenerGo = new GameObject("VoiceListener");
            listenerGo.transform.SetParent(transform, false);
            ownListener = listenerGo.AddComponent<AudioListener>();
            ownListener.enabled = false;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this) instance = null;
        }

        // 새 씬의 카메라 리스너와 겹치지 않게 한 번 내려놓고, 다음 LateUpdate에서 다시 잡는다
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (ownListener.enabled) RestoreListeners();
        }

        // 마이크 설정 변경은 여기로. 저장과 적용을 같이 한다
        public static void SetMicEnabled(bool on)
        {
            VoiceSettings.MicEnabled = on;
            Current?.SetMicEnabled(on);
        }

        public static void SetMicDevice(string device)
        {
            VoiceSettings.MicDevice = device;
            Current?.SetMicDevice(device);
        }

        void Update()
        {
            var voice = Current;
            if (!ReferenceEquals(voice, applied))
            {
                applied = voice;
                if (voice != null)
                {
                    voice.SetMicDevice(VoiceSettings.MicDevice);
                    voice.SetMicEnabled(VoiceSettings.MicEnabled);
                }
            }
        }

        void LateUpdate()
        {
            var local = FindLocal();
            UpdateListener(local);
            var voice = Current;
            spatial.Update(voice != null ? voice.PartnerAudio : null,
                           ownListener.enabled ? ownListener.transform.position : (Vector3?)null,
                           VoiceSettings.PartnerVolume / 100f);
        }

        static PlayerController FindLocal()
        {
            var all = PlayerController.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].IsLocal) return all[i];
            return null;
        }

        void UpdateListener(PlayerController local)
        {
            var cam = Camera.main;
            if (local == null || cam == null)
            {
                if (ownListener.enabled) RestoreListeners();
                return;
            }

            if (!ownListener.enabled)
            {
                // 씬에 있던 리스너(보통 Main Camera)를 끄고 내 리스너를 켠다
                var all = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                int n = 0;
                var list = new AudioListener[all.Length];
                foreach (var l in all)
                {
                    if (l == ownListener || !l.enabled) continue;
                    l.enabled = false;
                    list[n++] = l;
                }
                System.Array.Resize(ref list, n);
                disabledListeners = list;
                ownListener.enabled = true;
            }

            // 위치는 캐릭터, 방향은 카메라 (화면 왼쪽 = 왼쪽 스피커)
            ownListener.transform.SetPositionAndRotation(
                local.transform.position + Vector3.up * ListenerHeight, cam.transform.rotation);
        }

        void RestoreListeners()
        {
            ownListener.enabled = false;
            foreach (var l in disabledListeners)
                if (l != null) l.enabled = true;
            disabledListeners = new AudioListener[0];
        }
    }
}
