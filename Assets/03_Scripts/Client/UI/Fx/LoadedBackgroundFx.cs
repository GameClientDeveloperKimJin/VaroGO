using System;
using System.Collections.Generic;
using UnityEngine;
using VaroGO.Client.Loading;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// Addressable로 뜨는 3D 배경(타이틀 숲 등)에 연출을 붙인다. 로더(AddressablePrefabLoader)와 같은 오브젝트에 붙인다.
    /// 배경 프리팹은 빌더 메뉴가 다시 만들기 때문에 프리팹을 직접 고치지 않고, 생성된 뒤 이름으로 찾아서 붙인다.
    /// 지금은 깜빡임(AmbientFlicker)만. 로컬 연출.
    /// </summary>
    public class LoadedBackgroundFx : MonoBehaviour
    {
        [Serializable]
        public class FlickerRule
        {
            [Tooltip("이 글자가 이름에 들어간 오브젝트 모두 (예: LightRays)")]
            public string nameContains = "LightRays";
            public Vector2 range = new(0.45f, 1f);
            public float period = 5f;
            [Range(0f, 1f)] public float jitter = 0.15f;
        }

        [SerializeField] AddressablePrefabLoader loader;
        [SerializeField] List<FlickerRule> flicker = new() { new FlickerRule() };

        void Awake()
        {
            if (loader == null) loader = GetComponent<AddressablePrefabLoader>();
        }

        void Start()
        {
            if (loader == null) return;
            if (loader.IsDone) Apply(loader);
            else loader.Loaded += Apply;
        }

        void OnDestroy()
        {
            if (loader != null) loader.Loaded -= Apply;
        }

        void Apply(AddressablePrefabLoader l)
        {
            l.Loaded -= Apply;
            if (l.Instance == null) return;
            var all = l.Instance.GetComponentsInChildren<Transform>(true);
            int n = 0;
            foreach (var rule in flicker)
            {
                if (string.IsNullOrEmpty(rule.nameContains)) continue;
                foreach (var t in all)
                {
                    if (!t.name.Contains(rule.nameContains)) continue;
                    if (t.GetComponent<Renderer>() == null && t.GetComponent<Light>() == null) continue;
                    var f = t.GetComponent<AmbientFlicker>();
                    if (f == null) f = t.gameObject.AddComponent<AmbientFlicker>();
                    f.Configure(rule.range, rule.period, rule.jitter);
                    n++;
                }
            }
            if (n == 0) Debug.LogWarning($"[LoadedBackgroundFx] {name}: 깜빡임을 붙일 오브젝트를 못 찾음", this);
        }
    }
}
