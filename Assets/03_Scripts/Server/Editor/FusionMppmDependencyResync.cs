using System.IO;
using Fusion;
using Fusion.Editor;
using UnityEditor;

namespace VaroGO.Net.Editor
{
    /// <summary>
    /// MPPM 가상 플레이어에서 "Failed to load NetworkProjectConfigAsset"이 나는 문제 수정.
    /// Fusion은 NetworkProjectConfig 임포터가 쓰는 커스텀 의존성 해시를 에디터 세션당 한 번만
    /// 가상 플레이어에 보낸다(Temp/FusionMppm/*.json). 가상 플레이어는 켜질 때 이 파일을 한 번만 읽으므로,
    /// 그 순간 파일이 빠져 있으면 해시가 0이라 .fusion 아티팩트를 out-of-date로 보고, 읽기 전용 DB라
    /// 재임포트도 못 해 로드에 실패한다.
    /// 메인 에디터가 도메인 리로드 때 + 2초마다 빠진 해시 파일을 확인해 다시 보낸다.
    /// (파일이 왜 지워지는지는 미확인, 2026-11-02. 재발하면 Fusion MPPM 추적 로그로 원인 확인)
    /// </summary>
    [InitializeOnLoad]
    static class FusionMppmDependencyResync
    {
        const string PersistentCommandsFolder = "Temp/FusionMppm";
        const double CheckInterval = 2.0;

        // Fusion.AddressablesDependency처럼 해시가 null이라 Fusion이 원래 파일을 만들지 않는 항목은 건너뛴다
        static readonly string[] SkipNames = { "Fusion.AddressablesDependency" };

        static double nextCheckTime;

        static FusionMppmDependencyResync()
        {
            if (FusionMppm.Status != FusionMppmStatus.MainInstance) return;
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            EditorApplication.delayCall += Resync;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextCheckTime) return;
            nextCheckTime = EditorApplication.timeSinceStartup + CheckInterval;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            Resync();
        }

        static void Resync()
        {
            foreach (var field in TypeCache.GetFieldsWithAttribute<FusionCustomDependencyEagerAttribute>())
            {
                if (field.GetValue(null) is not FusionCustomDependency dependency) continue;
                if (System.Array.IndexOf(SkipNames, dependency.Name) >= 0) continue;
                if (File.Exists(PersistentFilePath(dependency.Name))) continue;

                UnityEngine.Debug.Log($"[Net] MPPM 의존성 해시 재전송: {dependency.Name}");
                dependency.Refresh();
            }
        }

        // FusionMppm.BroadcastInternal과 같은 규칙으로 파일명을 만든다
        static string PersistentFilePath(string dependencyName)
        {
            var fileName = $"Dependency_{dependencyName}";
            foreach (var c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');
            return Path.Combine(PersistentCommandsFolder, fileName.TrimEnd('.') + ".json");
        }
    }
}
