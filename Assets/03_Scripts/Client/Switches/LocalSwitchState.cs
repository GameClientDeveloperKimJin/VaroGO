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
        [SerializeField] bool startOn;

        bool isOn;
        double lastOnTime;

        public bool IsOn => isOn;
        public PlayerRole AllowedRole => allowedRole;
        public float AutoOffSeconds => autoOffSeconds;
        public double LastOnTime => lastOnTime;
        public bool StartOn => startOn; // NetSwitchState가 초기값으로 읽는다 (계약 §3.3)
        public event Action<bool> Changed;

        void Awake()
        {
            isOn = startOn;
            lastOnTime = SwitchStates.Now;
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
