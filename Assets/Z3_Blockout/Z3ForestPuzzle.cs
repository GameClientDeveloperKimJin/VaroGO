using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;

namespace HiddenPath.Blockout
{
    // Z3 숲 갈림길 퍼즐.
    // 오프라인: 정답·진행도를 이 컴포넌트가 직접 들고 있다.
    // 온라인: 같은 오브젝트의 IForestPuzzleState(NetForestPuzzleState)가 정답·진행도·라운드를 두 PC에 공유한다.
    //   누가 틀리거나 떨어지면 새 라운드가 두 사람 모두에게 시작되고(정답·단서 교체), 틀린 발판 붕괴 연출도 두 화면에 나온다.
    //   되돌아가는 이동(튕김·추락·입구 복귀)은 각자 자기 캐릭터만 자기 PC에서 한다 → 위치는 플레이어 동기화로 전달.
    public sealed class Z3ForestPuzzle : MonoBehaviour
    {
        public Transform[] forkReturns;
        public int[] correctBranches = { 0, 2, 1, 0 };
        public Checkpoint startCheckpoint;
        // 문제당 3개 단서, 길 순서: owl/moon, fox/star, deer/leaf. 문제 수보다 많은 단서는 숨긴다
        public GameObject[] clueVariants;
        [Header("Wrong-path feedback (metres / seconds)")]
        [Min(0f)] public float bounceDistance = 12f;
        [Min(0f)] public float bounceHeight = 10f;
        [Min(0.1f)] public float bounceDuration = 1.8f;

        int localRound;
        int localCleared;
        public int RoundVersion => Net != null ? Net.RoundVersion : localRound;
        public int ClearedForks => Net != null ? Net.ClearedForks : localCleared;
        public int CurrentFork { get; private set; }
        public bool Recovering => passenger != null;
        int ForkCount => forkReturns != null ? forkReturns.Length : 0;

        IForestPuzzleState net;
        bool netResolvedOnline;
        IForestPuzzleState Net
        {
            get
            {
                if (!GameServices.IsOnline) { if (net != null) Unsubscribe(); return null; }
                if (net == null || !netResolvedOnline)
                {
                    netResolvedOnline = true;
                    net = GetComponent<IForestPuzzleState>();
                    if (net != null) { net.Changed += OnNetChanged; net.Failed += OnNetFailed; }
                }
                return net != null && net.Ready ? net : null;
            }
        }

        PlayerController passenger;
        Transform carrier;
        // 복귀 직후 잠깐은 판정하지 않는다. 온라인 순간이동은 다음 틱에 적용돼, 그 사이 떨어지던 몸이 추락 판정에 닿으면 오답이 두 번 들어간다
        const float RecoverGrace = 1f;
        float graceUntil;
        // 끝에서 목적지에 앉힌 채 기다리는 시간. 온라인 순간이동이 적용되기 전 원래 자리(낭떠러지 아래)로 잠깐 돌아가 죽는 것을 막는다
        const float ParkTime = 0.25f;
        float parkUntil = -1f;
        // 이번 생에 도착 판정을 지나 퍼즐 안에 있는 내 캐릭터. 퍼즐 밖에서 죽고 부활할 때 추락 판정에 닿아도 무시한다
        PlayerController entered;
        Vector3 origin, direction;
        float elapsed;
        bool bounce;
        bool newRoundOnEnd;

        // 붕괴 발판 연출 (내가 떨어지는 것과 상대 화면 연출 모두)
        Transform floor;
        Vector3 floorPosition;
        Quaternion floorRotation;
        Collider[] floorColliders;
        bool[] colliderStates;
        float floorElapsed;
        const float FloorTime = 1.4f;

        void Start()
        {
            if (!GameServices.IsOnline) NewRound();
            else RefreshClues();
        }

        void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (net != null) { net.Changed -= OnNetChanged; net.Failed -= OnNetFailed; }
            net = null;
            netResolvedOnline = false;
        }

        void OnNetChanged() => RefreshClues();

        // 상대가 틀렸다: 같은 붕괴 발판 연출 (내가 그 발판으로 떨어지는 중이면 이미 재생 중)
        void OnNetFailed()
        {
            if (net == null || floor != null) return;
            var trap = FindTrapFloor(net.LastFailFork, net.LastFailBranch);
            if (trap != null) StartFloor(trap);
        }

        Transform FindTrapFloor(int fork, int branch)
        {
            if (fork < 0 || branch < 0) return null;
            foreach (var t in GetComponentsInChildren<Z3RouteTrigger>(true))
                if (t.kind == Z3RouteTrigger.TriggerKind.Choice && t.fork == fork && t.branch == branch && !t.bounce && t.trapFloor != null)
                    return t.trapFloor;
            return null;
        }

        int Answer(int fork)
        {
            var n = Net;
            if (n != null) return n.Answer(fork);
            return correctBranches != null && fork >= 0 && fork < correctBranches.Length ? correctBranches[fork] : -1;
        }

        // 오프라인 전용: 정답을 이전과 다른 값으로 다시 뽑는다 (온라인은 방장이 NetForestPuzzleState에서)
        public void NewRound()
        {
            if (GameServices.IsOnline) return;
            if (correctBranches == null || correctBranches.Length != ForkCount)
                correctBranches = new int[ForkCount];
            for (int i = 0; i < correctBranches.Length; i++)
            {
                int previous = correctBranches[i];
                int next = Random.Range(0, 2);
                correctBranches[i] = next >= previous ? next + 1 : next;
            }
            localCleared = 0;
            CurrentFork = 0;
            localRound++;
            RefreshClues();
        }

        public void RefreshClues()
        {
            if (clueVariants == null) return;
            bool ready = !GameServices.IsOnline || Net != null;
            for (int i = 0; i < clueVariants.Length; i++)
                if (clueVariants[i] != null)
                    clueVariants[i].SetActive(ready && i / 3 < ForkCount && i % 3 == Answer(i / 3));
        }

        public bool Arrive(PlayerController player, int fork)
        {
            if (!CanUse(player) || fork < 0 || fork >= ForkCount) return false;
            if (fork > ClearedForks)
            {
                // 순서 건너뜀(또는 상대가 틀려 새 라운드가 됨): 입구로 돌려보낸다
                Recover(player, Mathf.Min(ClearedForks, ForkCount - 1), true, null, -player.transform.forward, false);
                return false;
            }
            CurrentFork = fork;
            entered = player;
            if (fork == 0 && startCheckpoint != null)
                player.GetComponent<PlayerRespawn>()?.ReachCheckpoint(startCheckpoint);
            return true;
        }

        public bool Choose(PlayerController player, int fork, int branch, Transform trapFloor, bool eject, Vector3 away)
        {
            if (!CanUse(player) || entered != player || fork < 0 || fork >= ForkCount) return false;
            if (fork > ClearedForks)
            {
                Recover(player, Mathf.Min(ClearedForks, ForkCount - 1), true, null, -away, false);
                return false;
            }
            CurrentFork = fork;
            if (branch == Answer(fork))
            {
                var n = Net;
                if (n != null) n.RequestAdvance(fork, n.RoundVersion);
                else if (fork == localCleared) localCleared++;
                return true;
            }
            RequestFail(fork, branch);
            Recover(player, fork, eject, trapFloor, away, true);
            return false;
        }

        // 온라인은 정답이 공유된 뒤에만 판정한다
        bool CanUse(PlayerController player) => passenger == null && Time.time >= graceUntil && player != null && player.IsLocal &&
            !player.IsDead && !player.IsSeated && (!GameServices.IsOnline || Net != null);

        public void Fall(PlayerController player)
        {
            if (!CanUse(player) || entered != player) return;
            RequestFail(CurrentFork, -1);
            Recover(player, CurrentFork, false, null, Vector3.zero, true);
        }

        void RequestFail(int fork, int branch)
        {
            var n = Net;
            if (n != null) n.RequestFail(fork, branch, n.RoundVersion);
        }

        void Recover(PlayerController player, int fork, bool eject, Transform trapFloor, Vector3 away, bool failed)
        {
            CurrentFork = fork;
            passenger = player;
            parkUntil = -1f;
            newRoundOnEnd = failed || !GameServices.IsOnline; // 오프라인은 예전처럼 항상 새 라운드
            origin = player.transform.position;
            direction = Vector3.ProjectOnPlane(away, Vector3.up).normalized;
            bounce = eject;
            elapsed = 0;
            if (carrier == null)
            {
                carrier = new GameObject("Z3RecoverCarrier").transform;
                carrier.SetParent(transform, false);
            }
            carrier.SetPositionAndRotation(origin, player.transform.rotation);
            player.SetSeat(carrier, false); // 좌석에 붙여 옮긴다 (오프라인·온라인 같은 방식)
            if (trapFloor != null && !bounce) StartFloor(trapFloor);
        }

        void StartFloor(Transform trap)
        {
            StopFloor();
            floor = trap;
            floorElapsed = 0f;
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

        void StopFloor()
        {
            if (floor == null) return;
            floor.SetPositionAndRotation(floorPosition, floorRotation);
            for (int i = 0; i < floorColliders.Length; i++)
                if (floorColliders[i] != null) floorColliders[i].enabled = colliderStates[i];
            floor = null;
        }

        void Update()
        {
            if (floor != null)
            {
                floorElapsed += Time.deltaTime;
                float ft = Mathf.Clamp01(floorElapsed / FloorTime);
                floor.position = floorPosition + Vector3.down * (8f * ft * ft);
                floor.rotation = floorRotation * Quaternion.Euler(22f * ft, 0, 12f * ft);
                // 내가 떨어지는 중이 아니면 연출이 끝나는 대로 되돌린다 (떨어지는 중이면 복귀할 때)
                if (ft >= 1f && passenger == null) StopFloor();
            }

            if (entered != null && (entered.IsDead || !entered.IsLocal)) entered = null;
            if (passenger == null) return;
            if (passenger.IsDead || passenger.Seat != carrier) { Release(); return; }
            var destination = startCheckpoint != null ? startCheckpoint.SpawnPoint : forkReturns[0];
            if (parkUntil >= 0f)
            {
                // 목적지에 앉힌 채 기다렸다가 내려 준다 (그동안 좌석이 네트워크 위치를 목적지로 옮긴다)
                if (Time.time < parkUntil) return;
                var p = passenger;
                Release();
                graceUntil = Time.time + RecoverGrace;
                p.Respawn(destination.position, destination.rotation);
                return;
            }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / (bounce ? Mathf.Max(0.1f, bounceDuration) : FloorTime));
            float y = bounce ? bounceHeight * Mathf.Sin(Mathf.PI * t) : -7f * t * t;
            carrier.position = origin + direction * (bounce ? bounceDistance : 1.5f) * t + Vector3.up * y;
            if (t >= 1)
            {
                if (newRoundOnEnd) NewRound(); // 온라인은 방장이 이미 새 라운드를 시작했다
                CurrentFork = 0;
                entered = null;
                StopFloor();
                carrier.SetPositionAndRotation(destination.position, destination.rotation);
                parkUntil = Time.time + ParkTime;
            }
        }

        void Release()
        {
            StopFloor();
            if (passenger != null && passenger.Seat == carrier) passenger.SetSeat(null);
            passenger = null;
            parkUntil = -1f;
        }

        void OnDisable() => Release();
    }
}
