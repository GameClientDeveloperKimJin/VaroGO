using Fusion;
using Photon.Voice;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// 온라인 보이스(Photon Voice 2) → IVoiceChat 어댑터 (계약 §3.10).
    /// NetBootstrap.CreateRunner가 러너 오브젝트에 FusionVoiceClient와 함께 붙인다.
    /// - 보이스 방 = Fusion 세션 이름 + "_voice" (FusionVoiceClient가 자동으로 따라 들어감)
    /// - Player_Net의 VoiceNetworkObject(Recorder·Speaker 자식)를 찾아 내 것/상대 것으로 나눈다
    /// - 3D·볼륨·벽 막힘은 클라(VoiceManager)가 PartnerAudio에 적용한다. 여기선 전송·수신만.
    /// AppId는 ServerConfig.json의 appIdVoice. 비어 있으면 붙이지 않는다(클라는 오프라인 구현으로 동작).
    /// </summary>
    public class NetVoiceChat : MonoBehaviour, IVoiceChat
    {
        const float LevelGain = 6f; // OfflineVoiceChat과 같은 막대 느낌

        FusionVoiceClient voiceClient;
        NetworkRunner runner;

        bool micEnabled = true;
        string micDevice = "";

        VoiceNetworkObject local, partner;
        Recorder appliedRecorder;
        string appliedDevice;

        public static NetVoiceChat Attach(NetworkRunner runner, ServerConfigData config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.appIdVoice))
            {
                Debug.LogWarning("[Voice] ServerConfig.json에 appIdVoice 없음 → 보이스 꺼짐");
                return null;
            }

            var go = runner.gameObject;
            var client = go.AddComponent<FusionVoiceClient>();
            // Fusion 에셋(PhotonAppSettings)의 AppIdVoice는 비워 두고 설정 파일 값을 쓴다
            client.UseFusionAppSettings = false;
            client.Settings = new Photon.Realtime.AppSettings
            {
                AppIdVoice = config.appIdVoice,
                FixedRegion = config.fixedRegion, // FusionVoiceClient가 Fusion 세션 지역으로 다시 맞춘다
            };

            var chat = go.AddComponent<NetVoiceChat>();
            chat.voiceClient = client;
            chat.runner = runner;
            GameServices.RegisterVoice(chat);
            return chat;
        }

        void OnDestroy()
        {
            if (ReferenceEquals(GameServices.Voice, this)) GameServices.RegisterVoice(null);
        }

        // ---- IVoiceChat ----

        public bool IsConnected =>
            voiceClient != null && voiceClient.Client != null && voiceClient.Client.InRoom
            && runner != null && runner.GameMode != GameMode.Single;

        public bool MicEnabled => micEnabled;

        public void SetMicEnabled(bool on)
        {
            micEnabled = on;
            ApplyToRecorder();
        }

        public string[] MicDevices => Microphone.devices;
        public string MicDevice => micDevice;

        public void SetMicDevice(string device)
        {
            micDevice = device ?? "";
            ApplyToRecorder();
        }

        public float InputLevel
        {
            get
            {
                var rec = local != null ? local.RecorderInUse : null;
                if (rec == null || rec.LevelMeter == null) return 0f;
                return Mathf.Clamp01(rec.LevelMeter.CurrentAvgAmp * LevelGain);
            }
        }

        public bool PartnerSpeaking => partner != null && partner.IsSpeaking;

        public AudioSource PartnerAudio
        {
            get
            {
                var speaker = partner != null ? partner.SpeakerInUse : null;
                return speaker != null ? speaker.GetComponent<AudioSource>() : null;
            }
        }

        // ---- 내부 ----

        void Update()
        {
            if (local == null || partner == null) FindVoiceObjects();
            ApplyToRecorder();
        }

        void FindVoiceObjects()
        {
            if (runner == null || !runner.IsRunning) return;
            var all = FindObjectsByType<VoiceNetworkObject>(FindObjectsSortMode.None);
            foreach (var v in all)
            {
                if (v.Object == null || !v.Object.IsValid) continue;
                if (v.IsLocal) local = v;
                else partner = v;
            }
        }

        void ApplyToRecorder()
        {
            var rec = local != null ? local.RecorderInUse : null;
            if (rec == null) return;

            if (rec != appliedRecorder)
            {
                appliedRecorder = rec;
                appliedDevice = null;
                // 꺼져 있어도 입력 확인 막대가 움직이도록 녹음은 계속, 전송만 켜고 끈다
                rec.SourceType = Recorder.InputSourceType.Microphone;
                rec.MicrophoneType = Recorder.MicType.Unity; // 장치 이름 = Unity Microphone.devices
                rec.RecordWhenJoined = true;
                rec.StopRecordingWhenPaused = false;
                if (!rec.RecordingEnabled) rec.RecordingEnabled = true;
            }

            if (rec.TransmitEnabled != micEnabled) rec.TransmitEnabled = micEnabled;

            if (appliedDevice != micDevice)
            {
                appliedDevice = micDevice;
                rec.MicrophoneDevice = string.IsNullOrEmpty(micDevice) ? DeviceInfo.Default : new DeviceInfo(micDevice);
            }
        }
    }
}
