using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;

namespace HiddenPath.Blockout
{
    // Scene-local offline preview: an authored, one-way mushroom launch to Z4.
    // Networked movement authority must implement the same flight before shipping.
    public sealed class Z2ShortcutMushroom : MonoBehaviour
    {
        public Transform destination;
        public float flightSeconds = 6f;
        public float arcHeight = 38f;
        public float surfaceY;
        public bool InFlight => passenger != null;
        PlayerController passenger;
        OfflinePlayerDriver driver;
        bool driverWasEnabled;
        Vector3 start;
        float elapsed;

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
                !player.IsLocal || player.IsDead || GameServices.IsOnline) return false;
            var offline = player.GetComponent<OfflinePlayerDriver>();
            if (offline == null) return false;
            passenger = player;
            driver = offline;
            driverWasEnabled = driver.enabled;
            driver.enabled = false;
            start = player.transform.position;
            elapsed = 0f;
            return true;
        }

        void Update()
        {
            if (passenger == null) return;
            if (passenger.IsDead) { Release(); return; }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.5f, flightSeconds));
            Vector3 position = Vector3.Lerp(start, destination.position, t);
            position.y += 4f * arcHeight * t * (1f - t);
            passenger.Teleport(position, passenger.transform.rotation);
            if (t >= 1f)
            {
                passenger.Respawn(destination.position, destination.rotation);
                Release();
            }
        }

        void Release()
        {
            if (driver != null) driver.enabled = driverWasEnabled;
            driver = null;
            passenger = null;
        }

        void OnDisable() => Release();
    }
}
