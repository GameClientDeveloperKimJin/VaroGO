using System.Collections;
using UnityEngine;

namespace VaroGO.Client
{
    /// <summary>
    /// 로비 대기실 무대의 빈자리 포탈 연출. Slot_P1/Slot_P2 루트에 붙인다.
    /// - 대기 중: 포탈이 잔잔하게 돈다 (FX_Idle)
    /// - 입장: 포탈이 번쩍이고(FX_Flash) 캐릭터가 포탈에서 걸어 나와 자리에 선 뒤 포탈이 닫힌다
    /// - 퇴장: 포탈이 열리고 캐릭터가 포탈로 빨려 들어간다(FX_Suck). 다시 대기 상태
    /// 사람 유무는 서버 RoomStageView가 Occupied/Empty를 켜고 끄는 것을 보고 판단한다(서버 코드 수정 없음).
    /// Occupied가 꺼지면 캐릭터도 같이 꺼지므로, 퇴장 연출 동안만 CharacterAnchor를 슬롯 루트로 잠깐 옮긴다.
    /// </summary>
    public class RoomStagePortal : MonoBehaviour
    {
        [SerializeField] GameObject occupied;
        [SerializeField] Transform characterAnchor;
        [Tooltip("LobbyPortal 프리팹 인스턴스 (Empty 밖, 슬롯 바로 아래)")]
        [SerializeField] Transform portal;

        [Header("연출")]
        [Tooltip("캐릭터가 나오기 시작하는 위치 (슬롯 기준, 포탈 면)")]
        [SerializeField] Vector3 portalLocalPos = new(0f, 0f, -0.9f);
        [SerializeField] float enterWalkSeconds = 0.7f;
        [SerializeField] float leaveSuckSeconds = 0.6f;
        [SerializeField] float portalScaleSeconds = 0.25f;
        [Tooltip("입장 번쩍임 때 소용돌이·마법진 밝기 (기본 1)")]
        [SerializeField] float flashIntensity = 3f;
        [SerializeField] float flashSeconds = 0.3f;

        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        ParticleSystem fxIdle, fxFlash, fxSuck;
        Renderer[] glowRenderers;
        float[] baseIntensity;
        MaterialPropertyBlock block;

        Vector3 anchorLocalPos, anchorLocalScale;
        Quaternion anchorLocalRot;
        Vector3 portalScale;
        bool wasOccupied;
        Coroutine running;

        void Awake()
        {
            anchorLocalPos = characterAnchor.localPosition;
            anchorLocalRot = characterAnchor.localRotation;
            anchorLocalScale = characterAnchor.localScale;
            portal.localPosition = portalLocalPos;
            portalScale = portal.localScale;

            fxIdle = FindFx("FX_Idle");
            fxFlash = FindFx("FX_Flash");
            fxSuck = FindFx("FX_Suck");

            var front = portal.Find("Arch/Swirl_Front");
            var circle = portal.Find("MagicCircle");
            glowRenderers = new[] { front != null ? front.GetComponent<Renderer>() : null, circle != null ? circle.GetComponent<Renderer>() : null };
            baseIntensity = new float[glowRenderers.Length];
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                var r = glowRenderers[i];
                baseIntensity[i] = r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(IntensityId) ? r.sharedMaterial.GetFloat(IntensityId) : 1f;
            }
            block = new MaterialPropertyBlock();

            wasOccupied = occupied.activeSelf;
            ShowPortal(!wasOccupied, 1f);
        }

        ParticleSystem FindFx(string name)
        {
            var t = portal.Find(name);
            return t != null ? t.GetComponent<ParticleSystem>() : null;
        }

        // RoomStageView(Update)가 Occupied를 바꾼 뒤에 본다
        void LateUpdate()
        {
            bool now = occupied.activeSelf;
            if (now == wasOccupied) return;
            wasOccupied = now;
            if (running != null) { StopCoroutine(running); ResetAnchor(); }
            running = StartCoroutine(now ? Enter() : Leave());
        }

        IEnumerator Enter()
        {
            ShowPortal(true, 1f);
            if (fxFlash != null) fxFlash.Play(true);
            StartCoroutine(Flash());

            // 포탈 면에서 작게 나타나 앞으로 걸어 나온다
            Vector3 from = portalLocalPos;
            for (float t = 0; t < enterWalkSeconds; t += Time.deltaTime)
            {
                float k = Smooth(t / enterWalkSeconds);
                characterAnchor.localPosition = Vector3.Lerp(from, anchorLocalPos, k);
                characterAnchor.localScale = anchorLocalScale * Mathf.Lerp(0.6f, 1f, k);
                yield return null;
            }
            ResetAnchor();

            for (float t = 0; t < portalScaleSeconds; t += Time.deltaTime)
            {
                SetPortalScale(1f - Smooth(t / portalScaleSeconds));
                yield return null;
            }
            ShowPortal(false, 0f);
            running = null;
        }

        IEnumerator Leave()
        {
            // Occupied가 꺼져도 캐릭터가 보이도록 연출 동안만 슬롯 루트로 옮긴다
            characterAnchor.SetParent(transform, true);
            ShowPortal(true, 0f);
            for (float t = 0; t < portalScaleSeconds; t += Time.deltaTime)
            {
                SetPortalScale(Smooth(t / portalScaleSeconds));
                yield return null;
            }
            SetPortalScale(1f);
            if (fxSuck != null) fxSuck.Play(true);

            Vector3 start = characterAnchor.localPosition;
            Vector3 startScale = characterAnchor.localScale;
            for (float t = 0; t < leaveSuckSeconds; t += Time.deltaTime)
            {
                float k = t / leaveSuckSeconds;
                characterAnchor.localPosition = Vector3.Lerp(start, portalLocalPos + Vector3.up * 0.9f * k, k * k);
                characterAnchor.localScale = startScale * (1f - k * k);
                characterAnchor.Rotate(0f, 720f * Time.deltaTime, 0f, Space.Self);
                yield return null;
            }
            ResetAnchor();
            running = null;
        }

        IEnumerator Flash()
        {
            for (float t = 0; t < flashSeconds; t += Time.deltaTime)
            {
                // 0 → 최대 → 0 (사인 곡선)
                SetIntensity(Mathf.Sin(t / flashSeconds * Mathf.PI));
                yield return null;
            }
            SetIntensity(0f);
        }

        void SetIntensity(float boost01)
        {
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                var r = glowRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetFloat(IntensityId, Mathf.Lerp(baseIntensity[i], flashIntensity, boost01));
                r.SetPropertyBlock(block);
            }
        }

        // 캐릭터를 Occupied 아래 원래 자리로 되돌린다
        void ResetAnchor()
        {
            if (characterAnchor.parent != occupied.transform) characterAnchor.SetParent(occupied.transform, false);
            characterAnchor.localPosition = anchorLocalPos;
            characterAnchor.localRotation = anchorLocalRot;
            characterAnchor.localScale = anchorLocalScale;
            SetIntensity(0f);
        }

        void ShowPortal(bool on, float scale01)
        {
            if (portal.gameObject.activeSelf != on) portal.gameObject.SetActive(on);
            if (!on) return;
            SetPortalScale(scale01);
            if (fxIdle != null && !fxIdle.isPlaying) fxIdle.Play(true);
        }

        void SetPortalScale(float k) => portal.localScale = portalScale * Mathf.Max(0.0001f, k);

        static float Smooth(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));
    }
}
