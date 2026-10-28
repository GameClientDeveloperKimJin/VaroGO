using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VaroGO.Net.Editor
{
    /// <summary>
    /// 빌드 직전에 ServerConfig/ServerConfig.json을 Assets/StreamingAssets/로 복사한다.
    /// 빌드는 StreamingAssets 쪽을 읽는다(ServerConfigLoader). 복사본은 .gitignore로 제외돼 있다.
    /// 원본이 없으면 방 만들기가 안 되는 빌드가 나오므로 빌드를 멈춘다.
    /// </summary>
    class ServerConfigBuildCopy : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string source = Path.Combine(projectRoot, "ServerConfig", ServerConfigLoader.FileName);
            if (!File.Exists(source))
                throw new BuildFailedException($"[Net] {ServerConfigLoader.FileName} 없음: {source}");

            string folder = Path.Combine(Application.dataPath, "StreamingAssets");
            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, ServerConfigLoader.FileName);
            File.Copy(source, target, overwrite: true);
            AssetDatabase.ImportAsset("Assets/StreamingAssets/" + ServerConfigLoader.FileName);
            Debug.Log($"[Net] 빌드에 {ServerConfigLoader.FileName} 포함: {target}");
        }
    }
}
