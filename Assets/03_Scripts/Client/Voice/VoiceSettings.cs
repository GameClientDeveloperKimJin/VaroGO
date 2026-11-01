using UnityEngine;

namespace VaroGO.Client.Voice
{
    /// <summary>
    /// 음성 설정 저장값 (이 PC에만 저장, PlayerPrefs). 마이크 켜짐·장치는 IVoiceChat에 넘기고,
    /// 상대 음성 볼륨은 클라가 상대 AudioSource에 직접 곱한다 (거리 감쇠 곡선 위에 곱해짐).
    /// </summary>
    public static class VoiceSettings
    {
        const string KeyMic = "voice.micEnabled";
        const string KeyDevice = "voice.micDevice";
        const string KeyVolume = "voice.partnerVolume";

        public const int DefaultPartnerVolume = 80; // 설계서 기본값

        public static bool MicEnabled
        {
            get => PlayerPrefs.GetInt(KeyMic, 1) == 1;
            set { PlayerPrefs.SetInt(KeyMic, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // "" = 기본 장치
        public static string MicDevice
        {
            get => PlayerPrefs.GetString(KeyDevice, "");
            set { PlayerPrefs.SetString(KeyDevice, value ?? ""); PlayerPrefs.Save(); }
        }

        // 0~100
        public static int PartnerVolume
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KeyVolume, DefaultPartnerVolume), 0, 100);
            set { PlayerPrefs.SetInt(KeyVolume, Mathf.Clamp(value, 0, 100)); PlayerPrefs.Save(); }
        }
    }
}
