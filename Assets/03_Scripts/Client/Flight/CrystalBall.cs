using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P19 전망대 수정구. 가까이 가서 E(상호작용)를 누르면 조력자를 불러 비행 보너스를 시작한다.
    // 자식 Orb(빛나는 구슬)는 대기 중에 천천히 맥박치고, 부른 뒤에는 꺼진다.
    public class CrystalBall : MonoBehaviour
    {
        [SerializeField] FlightCourse course;
        [Tooltip("상호작용 거리 (m)")]
        [SerializeField, Min(0.5f)] float range = 2.5f;
        [SerializeField] Transform orb;

        PlayerController subscribed;
        Vector3 orbBaseScale = Vector3.one;
        Renderer orbRenderer;
        Color baseColor = new Color(0.55f, 0.85f, 1f);

        void Awake()
        {
            if (course == null) course = FindAnyObjectByType<FlightCourse>();
            if (orb == null) orb = transform.Find("Orb");
            if (orb != null)
            {
                orbBaseScale = orb.localScale;
                orbRenderer = orb.GetComponent<Renderer>();
                if (orbRenderer != null && orbRenderer.sharedMaterial != null && orbRenderer.sharedMaterial.HasProperty("_BaseColor"))
                    baseColor = orbRenderer.sharedMaterial.GetColor("_BaseColor");
            }
        }

        void OnDisable() => Subscribe(null);

        bool Waiting => course != null && course.State != null && course.Phase == FlightPhase.Idle;

        void Update()
        {
            PlayerController near = null;
            if (Waiting)
                foreach (var p in PlayerController.All)
                    if (p != null && p.IsLocal && (p.transform.position - transform.position).sqrMagnitude < range * range) { near = p; break; }
            Subscribe(near);

            if (orb == null) return;
            float glow = Waiting ? 1f + 0.1f * Mathf.Sin(Time.time * 3f) * (near != null ? 2f : 1f) : 0.85f;
            orb.localScale = orbBaseScale * glow;
            if (orbRenderer != null)
            {
                var c = Waiting ? baseColor * (near != null ? 1.6f : 1.1f) : baseColor * 0.35f;
                orbRenderer.material.SetColor("_BaseColor", c);
                if (orbRenderer.material.HasProperty("_EmissionColor")) orbRenderer.material.SetColor("_EmissionColor", c * 1.5f);
            }
        }

        void Subscribe(PlayerController player)
        {
            if (subscribed == player) return;
            if (subscribed != null) subscribed.Interacted -= OnInteract;
            subscribed = player;
            if (subscribed != null) subscribed.Interacted += OnInteract;
        }

        void OnInteract()
        {
            if (!Waiting) return;
            course.RequestSummon();
            if (orb != null) FlightFx.Burst(orb.position, new Color(0.6f, 0.9f, 1f), 4f, 0.7f);
            AudioSource.PlayClipAtPoint(TempSfx.Chime, Camera.main != null ? Camera.main.transform.position : transform.position, 0.8f);
        }

        // 그레이박스용 임시 안내. UI가 들어오면 교체한다.
        void OnGUI()
        {
            if (subscribed == null) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            GUI.Label(new Rect(0, Screen.height * 0.65f, Screen.width, 30), "E: 수정구 만지기", style);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
