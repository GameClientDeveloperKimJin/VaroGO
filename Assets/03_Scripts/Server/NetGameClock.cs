using System;
using Fusion;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// 공유 시계. Runner.SimulationTime 기반(계약 §3.2). 러너가 없거나 멈추면 마지막 값을 유지한다(감소 금지).
    /// 두 PC 일치 정도와 틱 단위 계단 현상은 S-07에서 검증한다 (확인 필요).
    /// </summary>
    public class NetGameClock : IGameClock
    {
        NetworkRunner runner;
        double last;

        public void Bind(NetworkRunner value) => runner = value;
        public void Unbind() => runner = null;

        public double Now
        {
            get
            {
                if (runner != null && runner.IsRunning)
                {
                    // S2-04: 파트너가 끊긴 동안은 멈춘 순간 값에 고정, 재개 후엔 멈춘 시간만큼 빼서 이어간다 (두 PC 같은 값)
                    // Shared에서 NetSession이 아직 안 왔으면 올리지 않는다: 재접속한 PC가 멈춘 시간을 모른 채 앞서가면
                    // 감소 금지(Max) 때문에 그만큼 시계가 서 버린다. NetSession은 보통 코스 로드 전에 도착한다
                    var session = NetSession.Instance;
                    if (session != null)
                        last = Math.Max(last, session.AdjustClock(runner.SimulationTime));
                    else if (runner.GameMode != GameMode.Shared)
                        last = Math.Max(last, runner.SimulationTime);
                }
                return last;
            }
        }
    }
}
