using System;
using UnityEngine;

namespace VaroGO.Shared
{
    /// <summary>
    /// 내 PC에 저장된 닉네임 (P22). 클라 닉네임 팝업이 저장하고, 서버 로비가 읽어서 방에 알린다.
    /// 규칙: 2~12자, 한글(완성형)·영문·숫자만. 저장 위치는 PlayerPrefs.
    /// </summary>
    public static class PlayerNickname
    {
        public const int MinLength = 2;
        public const int MaxLength = 12;
        const string BaseKey = "VaroGO.Nickname";

        // 에디터 MPPM 가상 플레이어는 PlayerPrefs(레지스트리)를 메인 에디터와 같이 쓴다 → 클론마다 키를 나눈다.
        // 클론은 <프로젝트>/Library/VP/<id>/ 에서 실행된다 (ServerConfigLoader와 같은 판정). 빌드는 키 그대로
        static string prefsKey;
        static string PrefsKey
        {
            get
            {
                if (prefsKey != null) return prefsKey;
                prefsKey = BaseKey;
#if UNITY_EDITOR
                string path = Application.dataPath.Replace('\\', '/');
                int vp = path.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase);
                if (vp >= 0)
                {
                    string id = path.Substring(vp + "/Library/VP/".Length).Split('/')[0];
                    prefsKey = $"{BaseKey}.VP.{id}";
                }
#endif
                return prefsKey;
            }
        }

        /// <summary>저장된 닉네임 (없으면 빈 문자열)</summary>
        public static string Current
        {
            get
            {
                var saved = PlayerPrefs.GetString(PrefsKey, string.Empty);
                return IsValid(saved) ? saved : string.Empty;
            }
        }

        public static bool HasNickname => !string.IsNullOrEmpty(Current);

        /// <summary>닉네임이 바뀌면 새 값으로 불린다 (로비가 방에 다시 알릴 때 사용)</summary>
        public static event Action<string> Changed;

        /// <summary>규칙에 맞으면 저장하고 true. 앞뒤 공백은 지운다</summary>
        public static bool TrySave(string nickname)
        {
            nickname = nickname?.Trim() ?? string.Empty;
            if (!IsValid(nickname)) return false;
            if (nickname == Current) return true;
            PlayerPrefs.SetString(PrefsKey, nickname);
            PlayerPrefs.Save();
            Changed?.Invoke(nickname);
            return true;
        }

        public static bool IsValid(string nickname) => Validate(nickname) == null;

        /// <summary>규칙에 어긋나면 화면에 보여줄 오류 문구, 맞으면 null</summary>
        public static string Validate(string nickname)
        {
            nickname = nickname?.Trim() ?? string.Empty;
            if (nickname.Length < MinLength) return $"{MinLength}자 이상 입력해 주세요";
            if (nickname.Length > MaxLength) return $"{MaxLength}자까지 쓸 수 있어요";
            foreach (char c in nickname)
                if (!IsAllowedChar(c)) return "한글 · 영문 · 숫자만 쓸 수 있어요";
            return null;
        }

        static bool IsAllowedChar(char c) =>
            (c >= '가' && c <= '힣') ||
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
            (c >= '0' && c <= '9');
    }
}
