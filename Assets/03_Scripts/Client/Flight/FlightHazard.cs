using UnityEngine;

namespace VaroGO.Client
{
    // P19 비행 장애물 공통. FlightCourse 아래 어디에 두어도 되고, 위치는 코스 기준 좌표로 계산한다
    // (x = 좌우, y = 높이, z = 출발점에서 앞으로 간 거리).
    // 움직임은 비행 시간만으로 정해져 모든 PC에서 같게 보이고, 다시 시작하면 자동으로 처음 상태가 된다.
    public abstract class FlightHazard : MonoBehaviour
    {
        [Tooltip("기획서 패턴 이름 (P01~P06). 표시용")]
        [SerializeField] string patternId = "";

        public string PatternId => patternId;

        // 이번 도전에서 이미 뗏목에 부딪혔는지 (같은 장애물로 피해가 두 번 나지 않게)
        public bool HitThisAttempt { get; set; }

        protected FlightCourse Course { get; private set; }

        public virtual void Bind(FlightCourse course) => Course = course;

        // 코스 기준 좌표의 내 위치
        public Vector3 LocalAnchor => Course != null ? Course.transform.InverseTransformPoint(transform.position) : transform.localPosition;

        // 매 프레임 모든 PC에서. flying = 비행 중인지, flyTime = 비행 시작 뒤 지난 초
        public abstract void Tick(bool flying, float flyTime);

        // 코스 기준 좌표의 뗏목 상자(raftCenter ± half)와 지금 겹치는지
        public abstract bool Overlaps(Vector3 raftCenter, Vector3 half);

        // 부딪힌 순간 연출 (모든 PC에서 각자 판정)
        public virtual void PlayHit() { }

        // 다시 시작할 때
        public virtual void ResetAttempt() => HitThisAttempt = false;

        protected static bool SphereBox(Vector3 center, float radius, Vector3 boxCenter, Vector3 half)
        {
            Vector3 d = center - boxCenter;
            Vector3 c = new Vector3(Mathf.Clamp(d.x, -half.x, half.x), Mathf.Clamp(d.y, -half.y, half.y), Mathf.Clamp(d.z, -half.z, half.z));
            return (d - c).sqrMagnitude < radius * radius;
        }
    }
}
