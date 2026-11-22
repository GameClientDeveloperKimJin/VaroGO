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

        [Header("가루 프리팹 (아트, 1회 재생 후 자동 삭제). 비어 있으면 아래 코드 가루로 대신한다")]
        [Tooltip("빨강(A, C1) 캐릭터가 죽을 때")]
        [SerializeField] GameObject powderRed;
        [Tooltip("파랑(B, C2) 캐릭터가 죽을 때")]
        [SerializeField] GameObject powderBlue;
        [Tooltip("가루 프리팹 크기 배율 (프리팹 루트 스케일에 곱한다)")]
        [SerializeField, Min(0.05f)] float powderScale = 1f;
        [Tooltip("켜면 리스폰 지점에서도 같은 반짝이를 띄운다 (사라진 자리 → 나타난 자리 연결)")]
        [SerializeField] bool powderOnRespawn = true;

        /// <summary>리스폰 반짝이를 이 컴포넌트가 띄우는지 (PlayerReactionFx는 그러면 예전 부활 반짝임을 생략)</summary>
        public bool PlaysRespawnSparkle => isActiveAndEnabled && powderOnRespawn && (powderRed != null || powderBlue != null);

        [Header("코드 가루 (프리팹이 없을 때)")]
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
        // 모델(Animator)별 원래 크기. 프리팹 모델은 1이 아닐 수 있다(예: 1.8) → 부활 연출이 1로 덮어쓰지 않게 기억해 둔다
        readonly System.Collections.Generic.Dictionary<Transform, Vector3> baseScales = new();

        void Awake()
        {
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (impulse == null) impulse = GetComponentInParent<CinemachineImpulseSource>();
            foreach (var a in GetComponentsInChildren<Animator>(true)) BaseScale(a.transform);
        }

        Vector3 BaseScale(Transform t)
        {
            if (!baseScales.TryGetValue(t, out var s)) baseScales[t] = s = t.localScale;
            return s;
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
            if (baked != null) Destroy(baked);
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
            // 발 위치(캐릭터 기준점)에 역할 색 가루를 띄운다. 프리팹이 없으면 코드 가루
            if (!SpawnPowder()) foreach (var smr in renderers) Burst(smr);

            hidden = renderers;
            hideTween?.Kill();
            hideTween = DOVirtual.DelayedCall(hideDelay, () => SetVisible(false)).SetUpdate(true).SetLink(gameObject);

            if (player != null && player.IsLocal && impulse != null) impulse.GenerateImpulse(impulseForce);
        }

        // 역할 색 가루. 역할이 아직 없으면 PlayerModelSwap과 같은 규칙(내 캐릭터 = 빨강, 상대 = 파랑)
        GameObject PowderPrefab()
        {
            var role = player != null ? player.Role : PlayerRole.None;
            bool red = role == PlayerRole.A_Red || (role == PlayerRole.None && (player == null || player.IsLocal));
            var p = red ? powderRed : powderBlue;
            return p != null ? p : (red ? powderBlue : powderRed);
        }

        const float RemoteRespawnDelay = 0.12f;

        bool SpawnPowder()
        {
            var prefab = PowderPrefab();
            if (prefab == null) return false;
            var fx = Instantiate(prefab, transform.position, transform.rotation);
            fx.transform.localScale = prefab.transform.localScale * powderScale;
            return true;
        }

        void OnRespawned()
        {
            hideTween?.Kill();
            // 리스폰 지점(이미 옮겨진 위치)에서 같은 반짝이. 원격 캐릭터는 이벤트가 위치 동기화보다 먼저 올 수 있어 잠깐 뒤에 띄운다
            if (powderOnRespawn)
            {
                if (player == null || player.IsLocal) SpawnPowder();
                else DOVirtual.DelayedCall(RemoteRespawnDelay, () => SpawnPowder()).SetLink(gameObject);
            }
            // 이미 보이는 상태(연출 없이 부활)여도 튀어나오는 연출은 재생
            var anim = GetComponentInChildren<Animator>(false);
            SetVisible(true);
            if (anim == null) return;
            appearTarget = anim.transform;
            var end = BaseScale(appearTarget);
            appearTarget.localScale = end * appearFromScale;
            appearTween?.Kill();
            appearTween = appearTarget.DOScale(end, appearTime).SetEase(Ease.OutBack, 2f).SetUpdate(true).SetLink(gameObject);
        }

        void RestoreModel()
        {
            SetVisible(true);
            if (appearTarget != null) appearTarget.localScale = BaseScale(appearTarget);
        }

        void SetVisible(bool visible)
        {
            if (hidden == null) return;
            foreach (var r in hidden) if (r != null) r.enabled = visible;
        }

        // 지금 자세의 몸 메시를 구워서 꼭짓점 자리(월드 좌표)에 가루를 직접 뿌린다.
        // (Shape 모듈의 SkinnedMeshRenderer 방식은 모델 FBX 루트 스케일 100 때문에 위치·속도가 엉뚱하게 커져
        //  가루가 사망 위치가 아닌 곳(리스폰 지점 근처)에서 나왔다 → 직접 계산한다)
        Mesh baked;

        void Burst(SkinnedMeshRenderer smr)
        {
            EnsureDust();
            if (baked == null) baked = new Mesh { name = "DeathDust Bake (runtime)" };
            // 이 모델(스케일 180)은 useScale=false로 구워 회전·위치만 곱해야 실제 몸 크기가 나온다 (Play에서 측정: 약 1.1×1.6×0.9m)
            smr.BakeMesh(baked, false);
            var verts = baked.vertices;
            if (verts.Length == 0) return;
            var toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            Color a = Tint(smr, 0f), b = Tint(smr, 0.25f);
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < dustCount; i++)
            {
                ep.position = toWorld.MultiplyPoint3x4(verts[Random.Range(0, verts.Length)]);
                ep.startColor = Color.Lerp(a, b, Random.value);
                dust.Emit(ep, 1);
            }
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
            var shape = dust.shape;
            shape.enabled = false; // 위치는 Burst에서 직접 정한다
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

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
