using System;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // 마법 오브젝트에 붙인다. 내 역할에 따라 보이는 것만 바꾸고, 충돌은 항상 그대로 둔다 (GDD 6.3).
    //            A_Red   B_Blue
    //   Common   보임    보임
    //   Red      보임    안 보임
    //   Blue     안 보임 보임
    public class MagicVisibility : MonoBehaviour
    {
        [SerializeField] MagicType type;

        // F3 디버그: 역할과 상관없이 모든 마법 오브젝트를 보여준다 (레벨 제작용)
        static bool debugShowAll;
        static event Action DebugShowAllChanged;
        public static bool DebugShowAll
        {
            get => debugShowAll;
            set
            {
                if (debugShowAll == value) return;
                debugShowAll = value;
                DebugShowAllChanged?.Invoke();
            }
        }

        Renderer[] renderers;
        Light[] lights;
        IRoleProvider subscribedRole;

        public MagicType Type => type;

        // 디버그 표시와 상관없이, 로컬 역할 기준으로 보이는 오브젝트인가 (C-07 투명 충돌 판정용)
        public bool IsVisibleToLocal => GameServices.Role == null || IsVisibleTo(GameServices.Role.LocalRole);

        public bool IsVisibleTo(PlayerRole role)
        {
            switch (type)
            {
                case MagicType.Red: return role == PlayerRole.A_Red;
                case MagicType.Blue: return role == PlayerRole.B_Blue;
                default: return true;
            }
        }

        void Awake()
        {
            // 파티클도 ParticleSystemRenderer라서 Renderer에 포함된다
            renderers = GetComponentsInChildren<Renderer>(true);
            lights = GetComponentsInChildren<Light>(true);
        }

        void OnEnable()
        {
            GameServices.Registered += OnRegistered;
            DebugShowAllChanged += Refresh;
            SubscribeRole();
            Refresh();
        }

        void OnDisable()
        {
            GameServices.Registered -= OnRegistered;
            DebugShowAllChanged -= Refresh;
            if (subscribedRole != null) subscribedRole.LocalRoleChanged -= OnRoleChanged;
            subscribedRole = null;
        }

        void OnRegistered()
        {
            SubscribeRole();
            Refresh();
        }

        void SubscribeRole()
        {
            if (subscribedRole != null) subscribedRole.LocalRoleChanged -= OnRoleChanged;
            subscribedRole = GameServices.Role;
            if (subscribedRole != null) subscribedRole.LocalRoleChanged += OnRoleChanged;
        }

        void OnRoleChanged(PlayerRole role) => Refresh();

        void Refresh()
        {
            bool visible = debugShowAll || IsVisibleToLocal;
            foreach (var r in renderers) r.enabled = visible;
            foreach (var l in lights) l.enabled = visible;
        }

        // Scene 뷰에서는 역할과 상관없이 위치가 보이도록 type 색 와이어 박스를 그린다
        void OnDrawGizmos()
        {
            if (!TryGetBounds(out Bounds bounds)) return;
            Gizmos.color = type == MagicType.Red ? new Color(1f, 0.25f, 0.25f)
                         : type == MagicType.Blue ? new Color(0.3f, 0.5f, 1f)
                         : new Color(0.8f, 0.8f, 0.8f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                if (!found) { bounds = c.bounds; found = true; }
                else bounds.Encapsulate(c.bounds);
            }
            if (found) return true;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found;
        }
    }
}
