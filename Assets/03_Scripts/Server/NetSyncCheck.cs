using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using VaroGO.Client;

namespace VaroGO.Net
{
    /// <summary>
    /// 코스 씬에서 온라인 동기화가 빠진 오브젝트를 찾는다.
    /// 상태가 있는 레벨 오브젝트(스위치·골)는 Net Variant여야 한다(계약 §6): LocalSwitchState 옆 NetSwitchState,
    /// LocalGoalState 옆 NetGoalState, 그리고 부모(자기 포함)에 NetworkObject.
    /// 에디터 메뉴(VAROGO/Net/0·3)와 실행 중 코스 로드 직후(NetBootstrap) 둘 다 쓴다.
    /// </summary>
    public static class NetSyncCheck
    {
        public const string FixHint = "메뉴 VAROGO/Net/0. 코스 씬 온라인 동기화 적용 (전체) 실행 필요";

        public static List<string> FindMissing(Scene scene)
        {
            var missing = new List<string>();
            int checkpoints = 0, checkpointStates = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var s in root.GetComponentsInChildren<LocalSwitchState>(true))
                    Check(s.gameObject, s.GetComponent<NetSwitchState>() != null, "스위치", missing);
                foreach (var g in root.GetComponentsInChildren<LocalGoalState>(true))
                    Check(g.gameObject, g.GetComponent<NetGoalState>() != null, "골", missing);
                foreach (var c in root.GetComponentsInChildren<Checkpoint>(true))
                {
                    checkpoints++;
                    if (c.Order < 0 || c.Order > NetCheckpointState.MaxOrder)
                        missing.Add($"체크포인트 {PathOf(c.transform)} (Order {c.Order}: 0~{NetCheckpointState.MaxOrder}만 동기화됨)");
                }
                foreach (var n in root.GetComponentsInChildren<NetCheckpointState>(true))
                {
                    checkpointStates++;
                    Check(n.gameObject, true, "체크포인트 상태", missing);
                }
            }
            // 체크포인트 공용 상태(S2-06)는 코스 씬에 정확히 1개
            if (checkpoints > 0 && checkpointStates == 0) missing.Add("체크포인트 상태 (NetCheckpointState 없음)");
            if (checkpointStates > 1) missing.Add($"체크포인트 상태 {checkpointStates}개 (1개만 있어야 함)");
            return missing;
        }

        static void Check(GameObject go, bool hasNet, string kind, List<string> missing)
        {
            if (hasNet && go.GetComponentInParent<NetworkObject>(true) != null) return;
            missing.Add($"{kind} {PathOf(go.transform)}");
        }

        static string PathOf(Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
