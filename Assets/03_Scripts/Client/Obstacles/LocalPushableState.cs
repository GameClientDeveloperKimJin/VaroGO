using System;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P18: 오프라인 미는 오브젝트 상태. 이 PC가 항상 권한이다. 온라인은 서버의 NetPushableState가 대신한다
    public class LocalPushableState : MonoBehaviour, IPushableState
    {
        public bool HasAuthority => true;
        public event Action<PlayerRole, Vector3> PushReported;

        public void ReportPush(PlayerRole role, Vector3 dir) => PushReported?.Invoke(role, dir);
    }
}
