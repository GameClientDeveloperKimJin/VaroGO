using System;
using System.Collections.Generic;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // C-12: 오프라인 골 상태. 온라인에서는 서버의 NetGoalState가 같은 자리를 대신한다.
    // 오프라인은 혼자 테스트하므로 로컬 역할 하나만 들어와도 클리어한다.
    public class LocalGoalState : MonoBehaviour, IGoalState
    {
        [Tooltip("켜면 두 역할이 모두 들어와야 클리어 (온라인 규칙 확인용)")]
        [SerializeField] bool requireBoth;

        readonly HashSet<PlayerRole> inside = new HashSet<PlayerRole>();
        bool cleared;

        public bool IsCleared => cleared;
        public event Action ChapterCleared;

        public void ReportInGoal(PlayerRole role, bool isInside)
        {
            if (cleared || role == PlayerRole.None) return;
            if (isInside) inside.Add(role);
            else inside.Remove(role);

            bool done = requireBoth
                ? inside.Contains(PlayerRole.A_Red) && inside.Contains(PlayerRole.B_Blue)
                : inside.Count > 0;
            if (!done) return;
            cleared = true;
            Debug.Log("[Goal] CLEAR");
            ChapterCleared?.Invoke();
        }
    }
}
