using UnityEngine;

namespace VaroGO.Client
{
    // P19 비행 장애물 공통. FlightCourse 아래 어디에 두어도 되고, 위치는 코스 기준 좌표로 계산한다
    // (x = 좌우, y = 높이, z = 출발점에서 앞으로 간 거리).
    // 움직임은 뗏목이 간 거리만으로 정해져 모든 PC에서 같게 보이고 (속도가 변해도), 다시 시작하면 자동으로 처음 상태가 된다.
    public abstract class FlightHazard : MonoBehaviour
    {
        [Tooltip("기획서 패턴 이름 (P01~P06). 표시용")]
        [SerializeField] string patternId = "";

        [Tooltip("부딪힌 순간 맞은 자리에 나오는 이펙트 (한 번 재생 후 스스로 사라지는 프리팹). 비우면 기본 단색 원")]
        [SerializeField] GameObject hitVfx;
        [Tooltip("hitVfx 크기 배율 (프리팹 원래 크기 기준)")]
        [SerializeField, Min(0.01f)] float hitVfxScale = 1f;

        public string PatternId => patternId;

        Renderer[] hazardWarnRenderers;
        MaterialPropertyBlock hazardWarnMpb;
        static readonly int HazardWarnProgressId = Shader.PropertyToID("_WarnProgress");

        // 경고 표시(Warning) 아래 Renderer를 모아 둔다. 셰이더 _WarnProgress로 연출하는 경고용
        protected void CacheWarning(Transform warning)
        {
            hazardWarnRenderers = warning != null ? warning.GetComponentsInChildren<Renderer>(true) : null;
            hazardWarnMpb ??= new MaterialPropertyBlock();
        }

        // 경고 진행도 k (0 = 막 보임 → 1 = 부딪히는 순간)를 경고 셰이더에 넣는다
        protected void SetWarnProgress(float k)
        {
            if (hazardWarnRenderers == null || hazardWarnRenderers.Length == 0) return;
            hazardWarnMpb.SetFloat(HazardWarnProgressId, k);
            foreach (var r in hazardWarnRenderers) if (r != null) r.SetPropertyBlock(hazardWarnMpb);
        }

        // 맞은 자리에 hitVfx를 띄운다. 없으면 false (호출 쪽이 기본 연출로 대신)
        protected bool SpawnHitVfx(Vector3 at)
        {
            if (hitVfx == null) return false;
            var go = Instantiate(hitVfx, at, Quaternion.identity);
            go.transform.localScale = hitVfx.transform.localScale * hitVfxScale;
            return true;
        }

        // 장애물 모습(visualPos)에서 비행기 쪽으로 radius만큼 다가간 점 = 부딪힌 자리
        protected Vector3 HitPoint(Vector3 visualPos, float radius)
        {
            var raft = Course != null ? Course.Raft : null;
            return raft != null ? Vector3.MoveTowards(visualPos, raft.transform.position, radius) : visualPos;
        }

        // true면 맞았을 때 내구도 대신 출발 위치로 돌려보낸다 (천둥 번개)
        public virtual bool SendsBackToStart => false;

        // 이번 도전에서 이미 뗏목에 부딪혔는지 (같은 장애물로 피해가 두 번 나지 않게)
        public bool HitThisAttempt { get; set; }

        protected FlightCourse Course { get; private set; }

        public virtual void Bind(FlightCourse course) => Course = course;

        // 코스 기준 좌표의 내 위치
        public Vector3 LocalAnchor => Course != null ? Course.transform.InverseTransformPoint(transform.position) : transform.localPosition;

        // 매 프레임 모든 PC에서. flying = 비행 중인지, distance = 뗏목이 출발점에서 간 거리 (m)
        public abstract void Tick(bool flying, float distance);

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
