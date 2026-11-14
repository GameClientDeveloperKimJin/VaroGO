using System.Collections;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // 스위치가 켜질 때(또는 꺼질 때) 지정한 지점들에 순서대로 이펙트와 소리를 낸다.
    // 예: 레버를 당기면 발판이 생기는 자리마다 차례로 반짝임.
    // 보이는 연출만 한다. 발판이 생기는 것 자체는 TimedPlatform·Door가 한다.
    // 스위치 상태는 모든 PC에서 같이 바뀌므로 (계약 §3.3) 두 화면에서 같은 순간에 나온다
    public class SwitchFx : MonoBehaviour
    {
        [Tooltip("지켜볼 스위치 오브젝트 (상태 컴포넌트가 붙은 GameObject)")]
        [SerializeField] GameObject stateObject;
        [Tooltip("이펙트를 낼 자리들. 위에서부터 순서대로. 비우면 이 오브젝트 자리 한 곳")]
        [SerializeField] Transform[] points;
        [Tooltip("자리마다 띄울 이펙트 (비워도 됨)")]
        [SerializeField] GameObject vfxPrefab;
        [SerializeField] float vfxScale = 1f;
        [Tooltip("자리의 윗면에 띄우려면 올릴 높이 (m)")]
        [SerializeField] float heightOffset;
        [Tooltip("자리 사이 시간 간격 (초)")]
        [SerializeField] float stagger = 0.12f;
        [Tooltip("소리. 비우면 임시음(맑은 소리)")]
        [SerializeField] AudioClip clip;
        [Range(0f, 1f)]
        [SerializeField] float volume = 0.6f;
        [Tooltip("켜면 자리마다 소리, 끄면 첫 자리에서 한 번만")]
        [SerializeField] bool soundEveryPoint;
        [Tooltip("켜질 때 연출")]
        [SerializeField] bool onTurnOn = true;
        [Tooltip("꺼질 때 연출")]
        [SerializeField] bool onTurnOff;

        ISwitchState state;

        void OnEnable()
        {
            GameServices.Registered += Resolve;
            Resolve();
        }

        void OnDisable()
        {
            GameServices.Registered -= Resolve;
            if (state != null) state.Changed -= OnChanged;
            state = null;
        }

        // 온라인/오프라인이 바뀌면 상태 구현을 다시 고른다
        void Resolve()
        {
            if (state != null) state.Changed -= OnChanged;
            state = stateObject != null ? SwitchStates.Resolve(stateObject, null, this) : null;
            if (state != null) state.Changed += OnChanged;
        }

        void OnChanged(bool on)
        {
            if (on ? !onTurnOn : !onTurnOff) return;
            if (!isActiveAndEnabled) return;
            StopAllCoroutines();
            StartCoroutine(Play());
        }

        IEnumerator Play()
        {
            int count = points != null && points.Length > 0 ? points.Length : 1;
            for (int i = 0; i < count; i++)
            {
                Transform t = points != null && i < points.Length && points[i] != null ? points[i] : transform;
                Vector3 pos = t.position + Vector3.up * heightOffset;
                if (vfxPrefab != null)
                {
                    var fx = Instantiate(vfxPrefab, pos, Quaternion.identity);
                    fx.transform.localScale *= vfxScale;
                }
                if (volume > 0f && (i == 0 || soundEveryPoint))
                    AudioSource.PlayClipAtPoint(clip != null ? clip : TempSfx.Chime, pos, volume);
                if (stagger > 0f && i < count - 1) yield return new WaitForSeconds(stagger);
            }
        }
    }
}
