using System.Collections.Generic;
using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;

namespace HiddenPath.Blockout
{
    // 돌 보충대. 오프라인: 빈 자리에 돌을 새로 만들고(최대 maximumStones), 한도면 버려진 돌을 가져온다.
    // 온라인: 새로 만들지 않는다. 처음 놓인 돌(Z4SupplyStone_Net)만 돌려 쓰며, 돌 물리 권한이 있는 PC(방장)만 옮긴다
    // → 위치는 돌의 NetworkTransform이 상대 PC에 보낸다. 떨어진 돌은 ThrowableStone이 시작 자리로 되돌린다.
    public sealed class Z4StoneSupply : MonoBehaviour
    {
        public ThrowableStone stonePrefab;
        public Transform[] sockets;
        public ThrowableStone[] initialStones;
        [Min(0.1f)] public float refillSeconds = 2f;
        [Min(1)] public int maximumStones = 12;
        readonly List<ThrowableStone> pool = new List<ThrowableStone>();
        float[] emptySince;
        bool onlineParked;
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
            pool.RemoveAll(s => s == null);
            bool online = GameServices.IsOnline;
            if (online)
            {
                // 권한은 돌마다 있지만 보충대의 돌은 모두 방장 것이다 → 첫 돌로 판단
                if (pool.Count == 0 || !pool[0].HasAuthority) { onlineParked = false; return; }
                if (!onlineParked)
                {
                    onlineParked = true;
                    for (int i = 0; i < initialStones.Length && i < sockets.Length; i++)
                        if (initialStones[i] != null && sockets[i] != null && initialStones[i].Holder == PlayerRole.None)
                            Park(initialStones[i], sockets[i]);
                }
            }
            else if (stonePrefab == null) return;
            for (int i = 0; i < sockets.Length; i++)
            {
                var socket = sockets[i];
                if (socket == null) continue;
                bool occupied = pool.Exists(s => s.Holder == PlayerRole.None &&
                    (s.transform.position - socket.position).sqrMagnitude < 0.8f * 0.8f);
                if (occupied) { emptySince[i] = 0; continue; }
                if (emptySince[i] == 0) emptySince[i] = Time.time;
                if (Time.time - emptySince[i] < refillSeconds) continue;
                if (!online && pool.Count < maximumStones)
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
                    var reusable = pool.Find(s => s.HasAuthority && CanRecycle(s, players));
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
