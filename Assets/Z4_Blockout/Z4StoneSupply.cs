using System.Collections.Generic;
using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;

namespace HiddenPath.Blockout
{
    // Offline blockout supply. Online creation must use the network spawn authority.
    public sealed class Z4StoneSupply : MonoBehaviour
    {
        public ThrowableStone stonePrefab;
        public Transform[] sockets;
        public ThrowableStone[] initialStones;
        [Min(0.1f)] public float refillSeconds = 2f;
        [Min(1)] public int maximumStones = 12;
        readonly List<ThrowableStone> pool = new List<ThrowableStone>();
        float[] emptySince;
        public int StoneCount => pool.Count;

        void Awake()
        {
            if (initialStones != null) pool.AddRange(initialStones);
            emptySince = new float[sockets.Length];
        }

        void Start()
        {
            if (GameServices.IsOnline || initialStones == null) return;
            // Run after all stone Awake methods, which initialize their carry physics.
            for (int i = 0; i < initialStones.Length && i < sockets.Length; i++)
                if (initialStones[i] != null && sockets[i] != null && initialStones[i].Holder == PlayerRole.None)
                    Park(initialStones[i], sockets[i]);
        }

        void Update()
        {
            if (GameServices.IsOnline || stonePrefab == null) return;
            pool.RemoveAll(s => s == null);
            for (int i = 0; i < sockets.Length; i++)
            {
                var socket = sockets[i];
                if (socket == null) continue;
                bool occupied = pool.Exists(s => s.Holder == PlayerRole.None &&
                    (s.transform.position - socket.position).sqrMagnitude < 0.8f * 0.8f);
                if (occupied) { emptySince[i] = 0; continue; }
                if (emptySince[i] == 0) emptySince[i] = Time.time;
                if (Time.time - emptySince[i] < refillSeconds) continue;
                if (pool.Count < maximumStones)
                {
                    var stone = Instantiate(stonePrefab, socket.position, socket.rotation, transform);
                    stone.name = "Supply_Stone_" + pool.Count;
                    pool.Add(stone);
                    Park(stone, socket);
                }
                else
                {
                    // Recycle only abandoned, resting stones; never take a held or flying stone.
                    var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                    var reusable = pool.Find(s => CanRecycle(s, players));
                    if (reusable == null) continue;
                    Park(reusable, socket);
                }
                emptySince[i] = 0;
            }
        }

        static void Park(ThrowableStone stone, Transform socket)
        {
            // Resolve lazy carry state before parking: its first read applies dynamic physics.
            if (stone.Holder != PlayerRole.None) return;
            var body = stone.GetComponent<Rigidbody>();
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            // ThrowableStone's existing pickup/throw events take over physics when used.
            // Do not keep snapping the stone every frame: a throw must immediately leave the tray.
            body.isKinematic = true;
            body.position = socket.position;
            body.rotation = socket.rotation;
            stone.transform.SetPositionAndRotation(socket.position, socket.rotation);
        }

        bool CanRecycle(ThrowableStone stone, PlayerController[] players)
        {
            if (stone.Holder != PlayerRole.None || stone.Stuck) return false;
            var body = stone.GetComponent<Rigidbody>();
            if (body == null || body.isKinematic || body.linearVelocity.sqrMagnitude > 0.16f) return false;
            foreach (var socket in sockets)
                if (socket != null && (stone.transform.position - socket.position).sqrMagnitude < 9f) return false;
            foreach (var player in players)
                if ((player.transform.position - stone.transform.position).sqrMagnitude < 16f) return false;
            return true;
        }
    }
}
