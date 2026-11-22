using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;

namespace HiddenPath.Blockout
{
    // 지름길 버섯: Z4 입구까지 정해진 궤적으로 날려 보낸다.
    // 내 캐릭터만 내 PC에서 옮긴다(좌석에 붙여 이동) → 온라인에서는 플레이어 위치 동기화(NetworkTransform)가 상대 화면에 그대로 보낸다.
    public sealed class Z2ShortcutMushroom : MonoBehaviour
    {
        public Transform destination;
        public float flightSeconds = 6f;
        public float arcHeight = 38f;
        public float surfaceY;
        public bool InFlight => passenger != null;
        PlayerController passenger;
        Transform carrier;
        Vector3 start;
        float elapsed;
        // 도착 뒤 목적지에 앉힌 채 기다리는 시간. 온라인 순간이동이 적용되기 전 원래 자리로 잠깐 돌아가는 것을 막는다
        const float ParkTime = 0.25f;
        float parkUntil = -1f;

        void OnTriggerStay(Collider other)
        {
            var player = other.GetComponent<PlayerController>();
            if (player == null || !player.IsGrounded ||
                Mathf.Abs(player.transform.position.y - surfaceY) > 0.3f) return;
            TryLaunch(player);
        }

        public bool TryLaunch(PlayerController player)
        {
            if (passenger != null || destination == null || player == null ||
                !player.IsLocal || player.IsDead || player.IsSeated) return false;
            if (carrier == null)
            {
                carrier = new GameObject("ShortcutCarrier").transform;
                carrier.SetParent(transform, false);
            }
            passenger = player;
            start = player.transform.position;
            carrier.SetPositionAndRotation(start, player.transform.rotation);
            elapsed = 0f;
            parkUntil = -1f;
            player.SetSeat(carrier, false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Z2] 지름길 버섯 발사: {player.Role}");
#endif
            return true;
        }

        void Update()
        {
            if (passenger == null) return;
            if (passenger.IsDead || passenger.Seat != carrier) { Release(); return; }
            if (parkUntil >= 0f)
            {
                if (Time.time < parkUntil) return;
                var p = passenger;
                Release();
                p.Respawn(destination.position, destination.rotation);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[Z2] 지름길 버섯 도착: {p.Role}");
#endif
                return;
            }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.5f, flightSeconds));
            Vector3 position = Vector3.Lerp(start, destination.position, t);
            position.y += 4f * arcHeight * t * (1f - t);
            carrier.position = position;
            if (t >= 1f)
            {
                carrier.SetPositionAndRotation(destination.position, destination.rotation);
                parkUntil = Time.time + ParkTime;
            }
        }

        void Release()
        {
            if (passenger != null && passenger.Seat == carrier) passenger.SetSeat(null);
            passenger = null;
            parkUntil = -1f;
        }

        void OnDisable() => Release();
    }
}
