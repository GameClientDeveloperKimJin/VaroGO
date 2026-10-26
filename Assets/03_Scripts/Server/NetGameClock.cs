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
                    last = Math.Max(last, runner.SimulationTime);
                return last;
            }
        }
    }
}
