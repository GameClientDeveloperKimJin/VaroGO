using System.IO;
using Fusion.Photon.Realtime;
using UnityEngine;

namespace VaroGO.Net
{
    [System.Serializable]
    public class ServerConfigData
    {
        public string appIdFusion;
        public string fixedRegion = "kr";
    }

    /// <summary>
    /// ServerConfig.json(저장소에 올리지 않음)을 읽어 Photon 설정에 반영한다.
    /// 에디터: 프로젝트 루트/ServerConfig/ServerConfig.json
    /// 빌드: StreamingAssets/ServerConfig.json (빌드 전처리가 복사)
    /// </summary>
    public static class ServerConfigLoader
    {
        public const string FileName = "ServerConfig.json";

        public static string FilePath =>
#if UNITY_EDITOR
            Path.Combine(EditorProjectRoot, "ServerConfig", FileName);
#else
            Path.Combine(Application.streamingAssetsPath, FileName);
#endif

#if UNITY_EDITOR
        // Multiplayer Play Mode 가상 플레이어는 <프로젝트>/Library/VP/<id>/ 클론에서 실행된다.
        // 클론에는 ServerConfig/가 없으므로 원본 프로젝트 루트로 올라간다.
        static string EditorProjectRoot
        {
            get
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                string normalized = root.Replace('\\', '/');
                int vp = normalized.IndexOf("/Library/VP/", System.StringComparison.OrdinalIgnoreCase);
                return vp >= 0 ? root.Substring(0, vp) : root;
            }
        }
#endif

        public static bool TryLoad(out ServerConfigData config, out string error)
        {
            config = null;
            error = null;

            if (!File.Exists(FilePath))
            {
                error = $"{FileName} 없음: {FilePath}";
                return false;
            }

            config = JsonUtility.FromJson<ServerConfigData>(File.ReadAllText(FilePath));
            if (config == null || string.IsNullOrWhiteSpace(config.appIdFusion))
            {
                error = $"{FileName}에 appIdFusion이 비어 있음";
                return false;
            }

            return true;
        }

        /// <summary>에셋(PhotonAppSettings)은 건드리지 않고 복사본에 값을 넣어 돌려준다. StartGameArgs.CustomPhotonAppSettings에 넣어 쓴다.</summary>
        public static FusionAppSettings BuildAppSettings(ServerConfigData config)
        {
            var settings = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.GetCopy();
            settings.AppIdFusion = config.appIdFusion;
            if (!string.IsNullOrEmpty(config.fixedRegion))
                settings.FixedRegion = config.fixedRegion;
            return settings;
        }
    }
}
