using UnityEngine;

namespace VaroGO.Client
{
    // 사운드 에셋이 들어오기 전 임시 합성음
    public static class TempSfx
    {
        static AudioClip beep, click;

        public static AudioClip Beep => beep ??= Make("TempBeep", 880f, 0.15f, square: true, decay: 8f);
        public static AudioClip Click => click ??= Make("TempClick", 600f, 0.06f, square: false, decay: 60f);

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
