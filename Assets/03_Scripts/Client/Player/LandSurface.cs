using UnityEngine;

namespace VaroGO.Client
{
    /// <summary>착지 이펙트용 바닥 종류. 바닥 오브젝트에 붙이면 이름·머티리얼로 추측하지 않고 이 값을 쓴다</summary>
    public enum LandSurfaceType { Auto = 0, Dust = 1, Grass = 2, Stone = 3, Wood = 4, MagicRed = 5, MagicBlue = 6 }

    // 바닥 종류를 직접 정할 때 바닥(또는 그 부모)에 붙인다. 안 붙이면 PlayerLandFx가 마법 색 → 지형 레이어 → 머티리얼 이름 순으로 추측한다
    public class LandSurface : MonoBehaviour
    {
        [Tooltip("이 바닥에 착지했을 때 쓸 이펙트 종류. Auto면 붙이지 않은 것과 같다")]
        public LandSurfaceType type = LandSurfaceType.Dust;
    }
}
