using UnityEngine;

namespace VaroGO.Client
{
    // C-11: 범위(트리거) 안에서 Interact를 누르면 토글한다.
    [RequireComponent(typeof(Collider))]
    public class Lever : SwitchInputBase
    {
        [Tooltip("레버 손잡이. 상태에 따라 기울인다 (비워도 됨)")]
        [SerializeField] Transform handle;
        [SerializeField] float handleAngle = 35f;

        PlayerController inRange;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SetInRange(null);
        }

        void OnTriggerEnter(Collider other)
        {
            var player = LocalPlayerOf(other);
            if (player != null) SetInRange(player);
        }

        void OnTriggerExit(Collider other)
        {
            if (inRange != null && other.gameObject == inRange.gameObject) SetInRange(null);
        }

        void SetInRange(PlayerController player)
        {
            if (inRange != null) inRange.Interacted -= OnInteract;
            inRange = player;
            if (inRange != null) inRange.Interacted += OnInteract;
        }

        void OnInteract()
        {
            if (state != null) TryRequest(!state.IsOn);
        }

        void Update()
        {
            if (handle == null || state == null) return;
            var target = Quaternion.Euler(state.IsOn ? -handleAngle : handleAngle, 0f, 0f);
            handle.localRotation = Quaternion.RotateTowards(handle.localRotation, target, 360f * Time.deltaTime);
        }

        // 그레이박스용 임시 안내. UI가 들어오면 교체한다.
        void OnGUI()
        {
            if (inRange == null) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            GUI.Label(new Rect(0, Screen.height * 0.65f, Screen.width, 30), "E: 레버", style);
        }
    }
}
