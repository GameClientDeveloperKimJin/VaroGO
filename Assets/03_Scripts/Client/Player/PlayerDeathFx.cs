using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    /// <summary>
    /// 사망 "가루" 연출. PlayerEvent.Died → 몸 모양 그대로 가루가 되어 흩어지고 모델이 사라진다.
    /// PlayerEvent.Respawned → 모델이 통 튀며 다시 나타난다.
    /// EventRaised만 보므로 원격 플레이어도 똑같이 재생한다. 각 PC 로컬 연출 — 게임 상태·timeScale은 건드리지 않는다.
    /// 가루 파티클은 코드로 만든다(프리팹 없음). 몸 메시(SkinnedMeshRenderer)에서 직접 뿜어 실루엣대로 흩어진다.
    /// </summary>
    public class PlayerDeathFx : MonoBehaviour
    {
        [SerializeField] PlayerController player;

        [Header("가루")]
        [SerializeField] int dustCount = 220;
        [SerializeField] Vector2 dustSize = new(0.04f, 0.11f);
        [SerializeField] Vector2 dustLifetime = new(1.0f, 1.8f);
        [Tooltip("흩어지는 힘")]
        [SerializeField] float scatterSpeed = 1.6f;
        [Tooltip("가루가 위로 떠오르는 정도 (음수면 가라앉음)")]
        [SerializeField] float riseSpeed = 0.8f;
        [Tooltip("가루 색에 섞는 회색 비율 (0 = 모델 색 그대로, 1 = 완전 회색)")]
        [Range(0f, 1f)]
        [SerializeField] float grayMix = 0.35f;

        [Header("모델")]
        [Tooltip("가루가 뿜어진 뒤 모델을 숨기기까지 걸리는 시간(초)")]
        [SerializeField] float hideDelay = 0.08f;
        [Tooltip("부활 때 모델이 튀어나오는 시간(초)")]
        [SerializeField] float appearTime = 0.35f;
        [SerializeField] float appearFromScale = 0.2f;

        [Header("카메라 흔들림 (내 캐릭터만, Cinemachine Impulse)")]
        [SerializeField] CinemachineImpulseSource impulse;
        [SerializeField] float impulseForce = 0.35f;

        ParticleSystem dust;
        Renderer[] hidden;
        Tween hideTween;
        Tween appearTween;
        Transform appearTarget;

        void Awake()
        {
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (impulse == null) impulse = GetComponentInParent<CinemachineImpulseSource>();
        }

        void OnEnable()
        {
            if (player != null) player.EventRaised += OnPlayerEvent;
        }

        void OnDisable()
        {
            if (player != null) player.EventRaised -= OnPlayerEvent;
            hideTween?.Kill();
            appearTween?.Kill();
            RestoreModel();
        }

        void OnDestroy()
        {
            if (dust != null) Destroy(dust.gameObject);
        }

        void OnPlayerEvent(PlayerEvent e)
        {
            if (e == PlayerEvent.Died) OnDied();
            else if (e == PlayerEvent.Respawned) OnRespawned();
        }

        void OnDied()
        {
            RestoreModel();
            var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(false);
            foreach (var smr in renderers) Burst(smr);

            hidden = renderers;
            hideTween?.Kill();
            hideTween = DOVirtual.DelayedCall(hideDelay, () => SetVisible(false)).SetUpdate(true).SetLink(gameObject);

            if (player != null && player.IsLocal && impulse != null) impulse.GenerateImpulse(impulseForce);
        }

        void OnRespawned()
        {
            hideTween?.Kill();
            // 이미 보이는 상태(연출 없이 부활)여도 튀어나오는 연출은 재생
            var anim = GetComponentInChildren<Animator>(false);
            SetVisible(true);
            if (anim == null) return;
            appearTarget = anim.transform;
            var end = Vector3.one;
            appearTarget.localScale = end * appearFromScale;
            appearTween?.Kill();
            appearTween = appearTarget.DOScale(end, appearTime).SetEase(Ease.OutBack, 2f).SetUpdate(true).SetLink(gameObject);
        }

        void RestoreModel()
        {
            SetVisible(true);
            if (appearTarget != null) appearTarget.localScale = Vector3.one;
        }

        void SetVisible(bool visible)
        {
            if (hidden == null) return;
            foreach (var r in hidden) if (r != null) r.enabled = visible;
        }

        void Burst(SkinnedMeshRenderer smr)
        {
            EnsureDust();
            var tr = dust.transform;
            tr.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
            tr.localScale = smr.transform.lossyScale;

            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
            shape.skinnedMeshRenderer = smr;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;

            var main = dust.main;
            main.startColor = new ParticleSystem.MinMaxGradient(Tint(smr, 0f), Tint(smr, 0.25f));

            dust.Emit(new ParticleSystem.EmitParams { applyShapeToPosition = true }, dustCount);
        }

        Color Tint(SkinnedMeshRenderer smr, float extraGray)
        {
            var mat = smr.sharedMaterial;
            var c = Color.white;
            if (mat != null)
            {
                if (mat.HasProperty("_BaseColor")) c = mat.GetColor("_BaseColor");
                else if (mat.HasProperty("_Color")) c = mat.GetColor("_Color");
            }
            c.a = 1f;
            return Color.Lerp(c, new Color(0.6f, 0.6f, 0.6f, 1f), Mathf.Clamp01(grayMix + extraGray));
        }

        void EnsureDust()
        {
            if (dust != null) return;
            var go = new GameObject("DeathDust (fx)");
            dust = go.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = dust.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(dustLifetime.x, dustLifetime.y);
            main.startSize = new ParticleSystem.MinMaxCurve(dustSize.x, dustSize.y);
            main.startSpeed = 0f;
            main.maxParticles = 1000;
            main.gravityModifier = 0f;
            main.useUnscaledTime = true;

            var emission = dust.emission;
            emission.enabled = false;

            // 흩어짐: 사방으로 퍼지는 속도 + 위로 뜨는 힘 + 흔들림
            var vel = dust.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-scatterSpeed, scatterSpeed);
            vel.y = new ParticleSystem.MinMaxCurve(riseSpeed * 0.3f, riseSpeed);
            vel.z = new ParticleSystem.MinMaxCurve(-scatterSpeed, scatterSpeed);

            var noise = dust.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.8f;

            var drag = dust.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.dampen = 0.08f;
            drag.limit = scatterSpeed;

            var size = dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0f)));

            var col = dust.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "M_DeathDust (runtime)" };
        }
    }
}
