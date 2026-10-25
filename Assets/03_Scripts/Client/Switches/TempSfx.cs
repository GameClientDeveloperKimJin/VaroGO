using UnityEngine;

namespace VaroGO.Client
{
    // 사운드 에셋이 들어오기 전 임시 합성음
    public static class TempSfx
    {
        static AudioClip beep, click, fanfare, scream, thud, chime, oof;

        public static AudioClip Beep => beep ??= Make("TempBeep", 880f, 0.15f, square: true, decay: 8f);
        public static AudioClip Click => click ??= Make("TempClick", 600f, 0.06f, square: false, decay: 60f);
        public static AudioClip Fanfare => fanfare ??= MakeArpeggio("TempFanfare", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.12f, 0.5f);
        public static AudioClip Scream => scream ??= MakeScream("TempScream", 0.9f);
        public static AudioClip Thud => thud ??= Make("TempThud", 90f, 0.18f, square: false, decay: 25f);
        public static AudioClip Chime => chime ??= MakeArpeggio("TempChime", new[] { 1046.5f, 1318.5f, 1568f }, 0.06f, 0.35f);
        public static AudioClip Oof => oof ??= Make("TempOof", 220f, 0.1f, square: true, decay: 30f);

        // "으아아아" 비명 흉내. 음이 올라갔다 떨어지고 떨림(비브라토)이 있다
        public static AudioClip MakeScream(string name, float duration)
        {
            const int rate = 44100;
            int samples = Mathf.CeilToInt(rate * duration);
            var data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / rate;
                float k = t / duration;
                float pitch = k < 0.25f ? Mathf.Lerp(420f, 760f, k / 0.25f) : Mathf.Lerp(760f, 300f, (k - 0.25f) / 0.75f);
                pitch *= 1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 9f * t);
                phase += pitch / rate;
                float p = phase - Mathf.Floor(phase);
                // 배음이 섞인 목소리 느낌: 톱니파와 사인파를 섞는다
                float wave = (2f * p - 1f) * 0.35f + Mathf.Sin(2f * Mathf.PI * p) * 0.5f;
                float envelope = Mathf.Clamp01(t / 0.03f) * Mathf.Clamp01((duration - t) / 0.2f);
                data[i] = wave * envelope * 0.6f;
            }
            var clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // 음을 차례로 올리고 마지막 음은 길게 끈다 (도-미-솔-도)
        public static AudioClip MakeArpeggio(string name, float[] notes, float noteLength, float lastLength)
        {
            const int rate = 44100;
            int noteSamples = Mathf.CeilToInt(rate * noteLength);
            int lastSamples = Mathf.CeilToInt(rate * lastLength);
            var data = new float[noteSamples * (notes.Length - 1) + lastSamples];
            for (int n = 0; n < notes.Length; n++)
            {
                int start = n * noteSamples;
                int length = n == notes.Length - 1 ? lastSamples : noteSamples;
                float decay = n == notes.Length - 1 ? 4f : 12f;
                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / rate;
                    data[start + i] = Mathf.Sin(2f * Mathf.PI * notes[n] * t) * Mathf.Exp(-t * decay) * 0.6f;
                }
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // 빠르게 감쇠하는 사인파/사각파
        public static AudioClip Make(string name, float frequency, float duration, bool square = false, float decay = 40f)
        {
            const int rate = 44100;
            int samples = Mathf.CeilToInt(rate * duration);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / rate;
                float wave = Mathf.Sin(2f * Mathf.PI * frequency * t);
                if (square) wave = Mathf.Sign(wave) * 0.4f;
                data[i] = wave * Mathf.Exp(-t * decay);
            }
            var clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
