using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client.Voice
{
    /// <summary>
    /// 온라인 보이스가 등록되지 않았을 때 쓰는 기본 구현 (오프라인·싱글 테스트용).
    /// 상대가 없으므로 전송은 하지 않는다. 마이크 장치 목록과 입력 크기(입력 확인 막대)만 실제로 동작한다.
    /// 입력 크기는 누군가 읽는 동안만 마이크를 켜고, 1초 동안 안 읽으면 끈다.
    /// </summary>
    public class OfflineVoiceChat : MonoBehaviour, IVoiceChat
    {
        const int SampleRate = 16000;
        const int WindowSamples = 512;   // 약 32ms
        const float IdleStopSeconds = 1f;

        readonly float[] samples = new float[WindowSamples];
        bool micEnabled = true;
        string device = "";
        AudioClip clip;
        string clipDevice;
        float level;
        float lastReadTime = -10f;

        public bool IsConnected => false;
        public bool MicEnabled => micEnabled;
        public void SetMicEnabled(bool on) => micEnabled = on;
        public string[] MicDevices => Microphone.devices;
        public string MicDevice => device;
        public bool PartnerSpeaking => false;
        public AudioSource PartnerAudio => null;

        public void SetMicDevice(string value)
        {
            device = value ?? "";
            StopMic(); // 다음 측정 때 새 장치로 다시 연다
        }

        public float InputLevel
        {
            get
            {
                lastReadTime = Time.unscaledTime;
                return level;
            }
        }

        void Update()
        {
            // 온라인 보이스가 마이크를 쓰는 중이면 손대지 않는다 (같은 장치를 Microphone.End로 끊지 않게)
            bool wanted = GameServices.Voice == null && Time.unscaledTime - lastReadTime < IdleStopSeconds;
            if (!wanted)
            {
                StopMic();
                return;
            }
            if (clip == null) StartMic();
            if (clip == null) return;

            int pos = Microphone.GetPosition(clipDevice);
            int start = pos - WindowSamples;
            if (start < 0)
            {
                level = 0f;
                return;
            }
            clip.GetData(samples, start);
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            float rms = Mathf.Sqrt(sum / samples.Length);
            // 보통 말소리 RMS가 0.02~0.2 정도라 막대가 잘 보이게 키운다
            level = Mathf.Clamp01(rms * 6f);
        }

        void StartMic()
        {
            if (Microphone.devices.Length == 0) return;
            // 저장된 장치가 빠져 있으면(블루투스 이어폰 끊김 등) 기본 장치로 연다
            clipDevice = string.IsNullOrEmpty(device) || System.Array.IndexOf(Microphone.devices, device) < 0 ? null : device;
            try { clip = Microphone.Start(clipDevice, true, 1, SampleRate); }
            catch (System.ArgumentException) { clip = null; }
        }

        void StopMic()
        {
            if (clip == null) return;
            Microphone.End(clipDevice);
            clip = null;
            level = 0f;
        }

        void OnDisable() => StopMic();
    }
}
