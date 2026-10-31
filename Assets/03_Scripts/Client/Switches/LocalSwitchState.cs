using System;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // C-11: 오프라인 스위치 상태. 온라인에서는 서버의 NetSwitchState가 같은 자리를 대신한다.
    // 입력(PressurePlate, Lever)과 연출(Door, TimedPlatform)은 ISwitchState로만 이 컴포넌트를 본다.
    public class LocalSwitchState : MonoBehaviour, ISwitchState, ISwitchDetails
    {
        [Tooltip("누를 수 있는 역할. None이면 둘 다")]
        [SerializeField] PlayerRole allowedRole = PlayerRole.None;
        [Tooltip("켜진 뒤 이 시간(초)이 지나면 자동으로 꺼진다. 0이면 계속 유지 (시간제한 발판)")]
        [SerializeField] float autoOffSeconds;
        [Tooltip("처음에 켜진 상태로 시작")]
        [SerializeField] bool startOn;
        [Tooltip("퍼즐 구역 번호. 리셋 장치를 쓰면 같은 번호의 스위치가 모두 처음 상태로 돌아간다 (C2-06)")]
        [SerializeField] int zone;

        bool isOn;
        double lastOnTime;

        public bool IsOn => isOn;
        public PlayerRole AllowedRole => allowedRole;
        public float AutoOffSeconds => autoOffSeconds;
        public double LastOnTime => lastOnTime;
        public bool StartOn => startOn; // NetSwitchState가 초기값으로 읽는다 (계약 §3.3)
        public int Zone => zone;        // 서버가 구역 리셋 대상을 고를 때 읽는다 (계약 §3.8)
        public event Action<bool> Changed;

        IZoneReset subscribedReset;

        void Awake()
        {
            isOn = startOn;
            lastOnTime = SwitchStates.Now;
        }

        // C2-06: 죽음은 스위치를 건드리지 않는다. 리셋 요청(ZoneReset)이 올 때만 처음 상태로
        void OnEnable()
        {
            GameServices.Registered += SubscribeReset;
            SubscribeReset();
        }

        void OnDisable()
        {
            GameServices.Registered -= SubscribeReset;
            if (subscribedReset != null) subscribedReset.ZoneReset -= OnZoneReset;
            subscribedReset = null;
        }

        void SubscribeReset()
        {
            if (subscribedReset != null) subscribedReset.ZoneReset -= OnZoneReset;
            subscribedReset = GameServices.ZoneReset;
            if (subscribedReset != null) subscribedReset.ZoneReset += OnZoneReset;
        }

        void OnZoneReset(int resetZone)
        {
            // 온라인은 서버(NetSwitchState)가 상태를 되돌린다
            if (resetZone != zone || GameServices.IsOnline) return;
            lastOnTime = SwitchStates.Now;
            Set(startOn);
        }

        public void RequestSet(bool on, PlayerRole requester)
        {
            if (allowedRole != PlayerRole.None && requester != allowedRole) return;
            if (on) lastOnTime = SwitchStates.Now; // 켜진 상태에서 다시 누르면 타이머만 다시 시작
            Set(on);
        }

        void Update()
        {
            if (isOn && autoOffSeconds > 0f && SwitchStates.Now - lastOnTime >= autoOffSeconds) Set(false);
        }

        void Set(bool on)
        {
            if (isOn == on) return;
            isOn = on;
            Changed?.Invoke(on);
        }
    }
}
