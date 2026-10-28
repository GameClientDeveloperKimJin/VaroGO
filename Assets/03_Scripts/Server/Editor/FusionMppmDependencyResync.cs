using System.IO;
using Fusion;
using Fusion.Editor;
using UnityEditor;

namespace VaroGO.Net.Editor
{
    /// <summary>
    /// MPPM 가상 플레이어에서 "Failed to load NetworkProjectConfigAsset"이 나는 문제 수정.
    /// Fusion은 NetworkProjectConfig 임포터가 쓰는 커스텀 의존성 해시를 에디터 세션당 한 번만
    /// 가상 플레이어에 보낸다(Temp/FusionMppm/*.json). 이 파일이 빠지면 새로 뜬 가상 플레이어는 해시가 0이라
    /// .fusion 아티팩트를 out-of-date로 보고, 읽기 전용 DB라 재임포트도 못 해 로드에 실패한다.
    /// 메인 에디터가 도메인 리로드마다 빠진 해시를 다시 보내 파일을 복구한다.
    /// </summary>
    [InitializeOnLoad]
    static class FusionMppmDependencyResync
    {
        const string PersistentCommandsFolder = "Temp/FusionMppm";

        static FusionMppmDependencyResync()
        {
            if (FusionMppm.Status != FusionMppmStatus.MainInstance) return;
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            EditorApplication.delayCall += Resync;
        }

        static void Resync()
        {
            foreach (var field in TypeCache.GetFieldsWithAttribute<FusionCustomDependencyEagerAttribute>())
            {
                if (field.GetValue(null) is not FusionCustomDependency dependency) continue;
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
