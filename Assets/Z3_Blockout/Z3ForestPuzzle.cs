using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;

namespace HiddenPath.Blockout
{
    // Local blockout prototype. Network authority must own progress/recovery in production.
    public sealed class Z3ForestPuzzle : MonoBehaviour
    {
        public Transform[] forkReturns;
        public int[] correctBranches = { 0, 2, 1, 0 };
        public Checkpoint startCheckpoint;
        // Seven slots, three variants each, in branch order: owl/moon, fox/star, deer/leaf.
        public GameObject[] clueVariants;
        public int RoundVersion { get; private set; }
        [Header("Wrong-path feedback (metres / seconds)")]
        [Min(0f)] public float bounceDistance = 12f;
        [Min(0f)] public float bounceHeight = 10f;
        [Min(0.1f)] public float bounceDuration = 1.8f;
        public int ClearedForks { get; private set; }
        public int CurrentFork { get; private set; }
        public bool Recovering => passenger != null;
        PlayerController passenger;
        OfflinePlayerDriver driver;
        bool driverEnabled;
        Vector3 origin, direction;
        float elapsed;
        bool bounce;
        Transform floor;
        Vector3 floorPosition;
        Quaternion floorRotation;
        Collider[] floorColliders;
        bool[] colliderStates;

        void Start()
        {
            if (!GameServices.IsOnline) NewRound();
            else RefreshClues();
        }

        public void NewRound()
        {
            if (GameServices.IsOnline) return;
            if (correctBranches == null || correctBranches.Length != forkReturns.Length)
                correctBranches = new int[forkReturns.Length];
            for (int i = 0; i < correctBranches.Length; i++)
            {
                int previous = correctBranches[i];
                int next = Random.Range(0, 2);
                correctBranches[i] = next >= previous ? next + 1 : next;
            }
            ClearedForks = 0;
            CurrentFork = 0;
            RoundVersion++;
            RefreshClues();
        }

        public void RefreshClues()
        {
            if (clueVariants == null) return;
            for (int i = 0; i < clueVariants.Length; i++)
                if (clueVariants[i] != null)
                    clueVariants[i].SetActive(i / 3 < correctBranches.Length && i % 3 == correctBranches[i / 3]);
        }

        public bool Arrive(PlayerController player, int fork)
        {
            if (!CanUse(player) || fork < 0 || fork >= forkReturns.Length) return false;
            if (fork > ClearedForks)
            {
                Recover(player, Mathf.Min(ClearedForks, forkReturns.Length - 1), true, null, -player.transform.forward);
                return false;
            }
            CurrentFork = fork;
            if (fork == 0 && startCheckpoint != null)
                player.GetComponent<PlayerRespawn>()?.ReachCheckpoint(startCheckpoint);
            return true;
        }

        public bool Choose(PlayerController player, int fork, int branch, Transform trapFloor, bool eject, Vector3 away)
        {
            if (!CanUse(player) || fork < 0 || fork >= correctBranches.Length) return false;
            if (fork > ClearedForks)
            {
                Recover(player, Mathf.Min(ClearedForks, forkReturns.Length - 1), true, null, -away);
                return false;
            }
            CurrentFork = fork;
            if (branch == correctBranches[fork])
            {
                if (fork == ClearedForks) ClearedForks++;
                return true;
            }
            Recover(player, fork, eject, trapFloor, away);
            return false;
        }

        bool CanUse(PlayerController player) => passenger == null && player != null && player.IsLocal &&
            !player.IsDead && !GameServices.IsOnline && player.GetComponent<OfflinePlayerDriver>() != null;

        public void Fall(PlayerController player)
        {
            if (CanUse(player)) Recover(player, CurrentFork, false, null, Vector3.zero);
        }

        void Recover(PlayerController player, int fork, bool eject, Transform trapFloor, Vector3 away)
        {
            CurrentFork = fork;
            passenger = player;
            driver = player.GetComponent<OfflinePlayerDriver>();
            driverEnabled = driver.enabled;
            driver.enabled = false;
            origin = player.transform.position;
            direction = Vector3.ProjectOnPlane(away, Vector3.up).normalized;
            bounce = eject;
            elapsed = 0;
            floor = trapFloor;
            if (floor != null && !bounce)
            {
                floorPosition = floor.position;
                floorRotation = floor.rotation;
                floorColliders = floor.GetComponentsInChildren<Collider>();
                colliderStates = new bool[floorColliders.Length];
                for (int i = 0; i < floorColliders.Length; i++)
                {
                    colliderStates[i] = floorColliders[i].enabled;
                    floorColliders[i].enabled = false;
                }
            }
        }

        void Update()
        {
            if (passenger == null) return;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / (bounce ? Mathf.Max(0.1f, bounceDuration) : 1.4f));
            float y = bounce ? bounceHeight * Mathf.Sin(Mathf.PI * t) : -7f * t * t;
            passenger.Teleport(origin + direction * (bounce ? bounceDistance : 1.5f) * t + Vector3.up * y,
                passenger.transform.rotation);
            if (floor != null && !bounce)
            {
                floor.position = floorPosition + Vector3.down * (8f * t * t);
                floor.rotation = floorRotation * Quaternion.Euler(22f * t, 0, 12f * t);
            }
            if (t >= 1)
            {
                var destination = startCheckpoint != null ? startCheckpoint.SpawnPoint : forkReturns[0];
                NewRound();
                passenger.Respawn(destination.position, destination.rotation);
                Release();
            }
        }

        void Release()
        {
            if (floor != null && !bounce)
            {
                floor.SetPositionAndRotation(floorPosition, floorRotation);
                for (int i = 0; i < floorColliders.Length; i++)
                    if (floorColliders[i] != null) floorColliders[i].enabled = colliderStates[i];
            }
            if (driver != null) driver.enabled = driverEnabled;
            passenger = null;
            driver = null;
            floor = null;
        }

        void OnDisable() => Release();
    }
}
