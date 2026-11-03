using System.Collections.Generic;
using UnityEngine;

namespace VaroGO.Client
{
    // P19 나무 비행 뗏목 모습. 위치는 FlightCourse가 정하고, 여기서는 조립 연출·기울기·피격 깜빡임·좌석만 맡는다.
    // 자식: Pieces(판자·밧줄·룬 조각들), Seat_Red(왼쪽 자리), Seat_Blue(오른쪽 자리)
    public class FlightRaft : MonoBehaviour
    {
        [Tooltip("조립되는 조각들의 부모. 비우면 자식 Pieces")]
        [SerializeField] Transform pieces;
        [Tooltip("빨강(왼쪽 조종) 자리")]
        [SerializeField] Transform seatRed;
        [Tooltip("파랑(오른쪽 조종) 자리")]
        [SerializeField] Transform seatBlue;
        [Tooltip("조각이 흩어져 있다 날아오는 거리 (m)")]
        [SerializeField, Min(0f)] float scatter = 5f;

        struct Piece { public Transform t; public Vector3 pos; public Quaternion rot; public Vector3 from; public Quaternion fromRot; public bool rune; }

        readonly List<Piece> list = new List<Piece>();
        readonly List<Renderer> renderers = new List<Renderer>();
        float assembled = -1f;
        bool hiddenByBlink;

        public Transform SeatRed => seatRed;
        public Transform SeatBlue => seatBlue;

        void Awake()
        {
            if (pieces == null) pieces = transform.Find("Pieces");
            if (seatRed == null) seatRed = transform.Find("Seat_Red");
            if (seatBlue == null) seatBlue = transform.Find("Seat_Blue");
            if (pieces == null) return;
            var rng = new System.Random(1234);
            for (int i = 0; i < pieces.childCount; i++)
            {
                var t = pieces.GetChild(i);
                var dir = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 0.8f + 0.4f, (float)rng.NextDouble() * 2f - 1f).normalized;
                list.Add(new Piece
                {
                    t = t,
                    pos = t.localPosition,
                    rot = t.localRotation,
                    from = t.localPosition + dir * scatter,
                    fromRot = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f),
                    rune = t.name.StartsWith("Rune"),
                });
            }
            pieces.GetComponentsInChildren(true, renderers);
        }

        // 0 = 안 보임, 0~1 = 조각이 날아와 붙는 중, 1 = 완성
        public void SetAssembly(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (Mathf.Approximately(progress, assembled)) return;
            assembled = progress;
            bool show = progress > 0f;
            if (pieces != null && pieces.gameObject.activeSelf != show) pieces.gameObject.SetActive(show);
            int n = Mathf.Max(1, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                // 판자 먼저, 룬은 마지막에 붙는다
                float delay = p.rune ? 0.75f : (float)i / n * 0.55f;
                float k = Mathf.Clamp01((progress - delay) / 0.25f);
                k = k * k * (3f - 2f * k);
                p.t.localPosition = Vector3.Lerp(p.from, p.pos, k);
                p.t.localRotation = Quaternion.Slerp(p.fromRot, p.rot, k);
                p.t.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, k);
                if (k <= 0f) p.t.localScale = Vector3.zero;
            }
        }

        // 기울기: 좌우 속도에 따라 옆으로, 피격·추락 때는 흔들림
        public void SetTilt(float roll, float pitch)
        {
            transform.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        public void SetBlink(bool blinking)
        {
            bool hide = blinking && Mathf.Repeat(Time.time * 10f, 1f) < 0.5f;
            if (hide == hiddenByBlink) return;
            hiddenByBlink = hide;
            foreach (var r in renderers) if (r != null) r.enabled = !hide;
        }

        // 룬은 은은하게 빛난다 (조립 뒤)
        void Update()
        {
            if (assembled < 1f) return;
            float s = 1f + 0.08f * Mathf.Sin(Time.time * 4f);
            foreach (var p in list) if (p.rune) p.t.localScale = Vector3.one * s;
        }
    }
}
