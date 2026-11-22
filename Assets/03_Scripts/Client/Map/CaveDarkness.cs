using UnityEngine;
using UnityEngine.Rendering;

namespace VaroGO.Client.Map
{
    // Z3 동굴 안을 어둡게 하고, 카메라가 움직일 때만 카메라 주변을 밝혀준다.
    // 동굴 중심선(path)에서 insideHalfWidth 안쪽이면 "동굴 안"으로 본다.
    public class CaveDarkness : MonoBehaviour
    {
        [Header("동굴 범위")]
        [Tooltip("동굴 중심선 (월드 좌표, 입구 → 출구 순서)")]
        public Vector3[] path;
        [Tooltip("중심선에서 이 거리 안쪽이면 동굴 안 (m)")]
        public float insideHalfWidth = 24f;
        [Tooltip("중심선 위아래 이 높이 안쪽만 동굴 안 (m)")]
        public float insideHeight = 22f;

        [Header("어두운 연출")]
        [Range(0f, 1f), Tooltip("동굴 안에서 주변광 세기 배율 (작을수록 어두움)")]
        public float darkAmbient = 0.2f;
        [Tooltip("어두워지는/밝아지는 속도 (클수록 빠름)")]
        public float fadeSpeed = 3f;

        [Header("카메라 조명")]
        [Tooltip("켜면 카메라가 움직일 때만 밝힘, 끄면 동굴 안에서 항상 밝힘")]
        public bool lightOnlyWhileMoving = true;
        [Tooltip("이 속도(m/s) 이상 움직이면 '이동 중'")]
        public float moveThreshold = 0.5f;
        [Tooltip("멈춘 뒤 조명이 유지되는 시간 (초)")]
        public float holdAfterStop = 0.6f;
        public Color lightColor = new Color(1f, 0.85f, 0.6f);
        public float lightRange = 18f;
        public float lightIntensity = 4f;
        [Tooltip("조명 높이: 플레이어 머리 위 (m)")]
        public float lightHeight = 3.5f;

        [Header("먼 곳 어둡게 (안개)")]
        [Tooltip("동굴 안 안개 농도. 클수록 먼 곳이 빨리 어두워짐")]
        public float caveFogDensity = 0.016f;
        public Color caveFogColor = new Color(0.01f, 0.01f, 0.015f);

        [Range(0f, 1f), Tooltip("동굴 안에서 태양빛 세기 배율 (0 = 햇빛 없음)")]
        public float darkSun = 0.03f;
        Light _sun; float _sunBase;

        [Header("입구/출구 검은 판")]
        [Tooltip("플레이어와 카메라가 모두 동굴 밖일 때만 보이는 판 (입구·출구 어둠판)")]
        public GameObject[] mouthCovers;

        Light _light;
        VaroGO.Client.PlayerController _player;
        float _findTimer;
        bool _fogBase; Color _fogColorBase; float _fogDensityBase; FogMode _fogModeBase;
        Vector3 _lastCamPos;
        bool _hasLast;
        float _moveTimer;
        float _inside;     // 0~1 동굴 안 정도
        float _lit;        // 0~1 조명 켜짐 정도
        float _baseAmbient;
        bool _baseCaptured;

        void OnEnable()
        {
            _baseAmbient = RenderSettings.ambientIntensity;
            _fogBase = RenderSettings.fog; _fogColorBase = RenderSettings.fogColor;
            _fogDensityBase = RenderSettings.fogDensity; _fogModeBase = RenderSettings.fogMode;
            _baseCaptured = true;
            var go = new GameObject("CaveCameraLight") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.shadows = LightShadows.None;
            _light.intensity = 0f;
            _light.enabled = false;
            _hasLast = false;
        }

        void OnDisable()
        {
            if (_sun != null) _sun.intensity = _sunBase;
            if (_baseCaptured)
            {
                RenderSettings.ambientIntensity = _baseAmbient;
                RenderSettings.fog = _fogBase; RenderSettings.fogColor = _fogColorBase;
                RenderSettings.fogDensity = _fogDensityBase; RenderSettings.fogMode = _fogModeBase;
            }
            if (_light != null) Destroy(_light.gameObject);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || path == null || path.Length < 2) return;
            Vector3 cp = cam.transform.position;

            // 이동 감지
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            if (_hasLast && (cp - _lastCamPos).magnitude / dt > moveThreshold) _moveTimer = holdAfterStop;
            else _moveTimer -= dt;
            _lastCamPos = cp; _hasLast = true;

            // 플레이어 찾기 (카메라에서 가장 가까운 것) — 안 판정에도 쓰므로 먼저
            _findTimer -= dt;
            if (_player == null || _findTimer <= 0f)
            {
                _findTimer = 1f;
                float bd = float.MaxValue; _player = null;
                foreach (var pc in FindObjectsByType<VaroGO.Client.PlayerController>(FindObjectsSortMode.None))
                {
                    float d = (pc.transform.position - cp).sqrMagnitude;
                    if (d < bd) { bd = d; _player = pc; }
                }
            }

            // 동굴 안 판정: 플레이어 또는 카메라 중 하나라도 안이면 "안"
            bool playerIn = _player != null && IsInside(_player.transform.position);
            bool camIn = IsInside(cp);
            float target = (playerIn || camIn) ? 1f : 0f;
            // 입구/출구 검은 판은 둘 다 밖일 때만 보임 (플레이어가 지나간 뒤 카메라가 밖이어도 플레이어가 가려지지 않게)
            if (mouthCovers != null)
                foreach (var m in mouthCovers) if (m != null && m.activeSelf != !(playerIn || camIn)) m.SetActive(!(playerIn || camIn));
            _inside = Mathf.MoveTowards(_inside, target, fadeSpeed * Time.unscaledDeltaTime);
            RenderSettings.ambientIntensity = Mathf.Lerp(_baseAmbient, _baseAmbient * darkAmbient, _inside);
            // 태양빛: 그림자 거리 밖은 천장이 햇빛을 못 막아 밝아지므로, 동굴 안에서는 태양 자체를 줄인다
            if (_sun == null) { _sun = RenderSettings.sun; if (_sun == null) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { _sun = l; break; } if (_sun != null) _sunBase = _sun.intensity; }
            if (_sun != null) _sun.intensity = Mathf.Lerp(_sunBase, _sunBase * darkSun, _inside);
            // 먼 곳은 안개로 어둡게
            if (_inside > 0.001f)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = Color.Lerp(_fogBase ? _fogColorBase : caveFogColor, caveFogColor, _inside);
                RenderSettings.fogDensity = Mathf.Lerp(_fogBase ? _fogDensityBase : 0f, caveFogDensity, _inside);
            }
            else
            {
                RenderSettings.fog = _fogBase; RenderSettings.fogColor = _fogColorBase;
                RenderSettings.fogDensity = _fogDensityBase; RenderSettings.fogMode = _fogModeBase;
            }


            bool want = _inside > 0.01f && (!lightOnlyWhileMoving || _moveTimer > 0f);
            _lit = Mathf.MoveTowards(_lit, want ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
            _light.enabled = _lit > 0.01f;
            _light.color = lightColor;
            _light.range = lightRange;
            _light.intensity = lightIntensity * _lit * _inside;
            // 플레이어 머리 위에서 주변을 밝힘 (없으면 카메라 앞)
            _light.transform.position = _player != null
                ? _player.transform.position + Vector3.up * lightHeight
                : cp + cam.transform.forward * 3f;
        }

        bool IsInside(Vector3 p)
        {
            float best = float.MaxValue; float py = 0f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                Vector2 a2 = new Vector2(a.x, a.z), ab = new Vector2(b.x - a.x, b.z - a.z);
                float l2 = ab.sqrMagnitude; if (l2 < 1e-6f) continue;
                float t = Mathf.Clamp01(Vector2.Dot(new Vector2(p.x, p.z) - a2, ab) / l2);
                float d = (new Vector2(p.x, p.z) - (a2 + ab * t)).magnitude;
                if (d < best) { best = d; py = Mathf.Lerp(a.y, b.y, t); }
            }
            // 입구/출구 바깥(양 끝 점 너머)은 동굴 아님
            var first = path[0]; var last = path[path.Length - 1];
            if (p.z < first.z || p.z > last.z) return false;
            return best < insideHalfWidth && p.y > py - insideHeight && p.y < py + insideHeight;
        }
    }
}
