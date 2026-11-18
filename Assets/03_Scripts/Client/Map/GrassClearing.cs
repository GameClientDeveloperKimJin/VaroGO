using System.Collections.Generic;
using UnityEngine;

namespace VaroGO.Client
{
    // 섬 윗면 식물(IslandVisual이 그리는 풀·고사리·꽃)을 이 오브젝트 주변에서 비운다.
    // 스위치·체크포인트·시소처럼 풀에 묻히면 안 되는 오브젝트에 붙인다. 판정·동작에는 영향이 없다.
    [ExecuteAlways]
    public class GrassClearing : MonoBehaviour
    {
        [Tooltip("이 반지름(m) 안의 풀을 비운다")]
        [SerializeField, Min(0.1f)] float radius = 1.5f;
        [Tooltip("중심을 옮긴다 (이 오브젝트 기준, m)")]
        [SerializeField] Vector3 offset;

        public static readonly List<GrassClearing> All = new List<GrassClearing>();
        // 비우는 곳이 생기거나 없어지거나 움직이면 올라간다 → IslandVisual이 풀을 다시 심는다
        public static int Version { get; private set; }

        Vector3 lastCenter;
        float lastRadius;

        public Vector3 Center => transform.TransformPoint(offset);
        public float Radius => radius;

        void OnEnable()
        {
            All.Add(this);
            Remember();
            Version++;
        }

        void OnDisable()
        {
            All.Remove(this);
            Version++;
        }

        void OnValidate() => Version++;

        void Update()
        {
            if (Center == lastCenter && radius == lastRadius) return;
            Remember();
            Version++;
        }

        void Remember()
        {
            lastCenter = Center;
            lastRadius = radius;
        }

        public static bool Covers(Vector3 worldPoint)
        {
            for (int i = 0; i < All.Count; i++)
            {
                var c = All[i];
                if (c == null) continue;
                Vector3 d = worldPoint - c.lastCenter;
                d.y = 0f;
                if (d.sqrMagnitude < c.lastRadius * c.lastRadius) return true;
            }
            return false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.9f, 0.3f, 0.8f);
            Vector3 c = Center;
            const int n = 32;
            Vector3 prev = c + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
