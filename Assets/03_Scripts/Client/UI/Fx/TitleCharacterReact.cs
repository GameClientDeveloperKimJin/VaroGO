using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// 타이틀 화면 3D 주인공(C1·C2) 반응. 버튼에 마우스를 올리거나 패드로 선택하면 지정한 트리거를 보낸다
    /// (예: 게임 시작 → 둘 다 ArriveCheer). 평소에는 가끔 한 명씩 동작을 섞는다.
    /// 클릭 처리에는 끼지 않는다 — 버튼에 EventTrigger만 덧붙임. 로컬 연출, timeScale 무관.
    /// </summary>
    public class TitleCharacterReact : MonoBehaviour
    {
        [Serializable]
        public class ButtonCue
        {
            public Button button;
            [Tooltip("올렸을 때 보낼 Animator 트리거 이름. 비우면 반응 없음")]
            public string trigger = "ArriveCheer";
            [Tooltip("켜면 두 캐릭터 모두, 끄면 한 명만(번갈아)")]
            public bool both = true;
        }

        [SerializeField] Animator[] characters;
        [SerializeField] List<ButtonCue> cues = new();
        [Tooltip("같은 반응을 다시 보내기까지 최소 간격(초) — 버튼 사이를 오갈 때 계속 환호하지 않게")]
        [SerializeField] float cueCooldown = 2.5f;

        [Header("평소 (가끔 한 명씩)")]
        [Tooltip("가끔 보낼 트리거 목록. 비우면 끔")]
        [SerializeField] string[] idleTriggers = { "ArriveCheer" };
        [SerializeField] Vector2 idleInterval = new(9f, 16f);
        [Tooltip("시작할 때 두 캐릭터 Idle 박자를 어긋나게")]
        [SerializeField] bool desyncIdle = true;

        float lastCue = -99f;
        float nextIdle;
        int turn;

        void Start()
        {
            if (desyncIdle && characters != null)
                foreach (var a in characters)
                    if (a != null && a.isActiveAndEnabled) a.Play(0, 0, Random.value);
            nextIdle = Time.unscaledTime + Random.Range(idleInterval.x, idleInterval.y);

            foreach (var c in cues)
            {
                if (c.button == null || string.IsNullOrEmpty(c.trigger)) continue;
                var trig = c.button.GetComponent<EventTrigger>();
                if (trig == null) trig = c.button.gameObject.AddComponent<EventTrigger>();
                var cue = c;
                Add(trig, EventTriggerType.PointerEnter, _ => Fire(cue));
                Add(trig, EventTriggerType.Select, e => { if (!(e is PointerEventData)) Fire(cue); });
            }
        }

        static void Add(EventTrigger t, EventTriggerType type, Action<BaseEventData> a)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(e => a(e));
            t.triggers.Add(entry);
        }

        void Fire(ButtonCue c)
        {
            if (Time.unscaledTime - lastCue < cueCooldown) return;
            lastCue = Time.unscaledTime;
            if (c.both) foreach (var a in characters) Send(a, c.trigger);
            else Send(Next(), c.trigger);
            nextIdle = Time.unscaledTime + Random.Range(idleInterval.x, idleInterval.y); // 방금 반응했으니 평소 동작은 미룬다
        }

        void Update()
        {
            if (idleTriggers == null || idleTriggers.Length == 0 || characters == null || characters.Length == 0) return;
            if (Time.unscaledTime < nextIdle) return;
            nextIdle = Time.unscaledTime + Random.Range(idleInterval.x, idleInterval.y);
            Send(Next(), idleTriggers[Random.Range(0, idleTriggers.Length)]);
        }

        Animator Next()
        {
            if (characters == null || characters.Length == 0) return null;
            return characters[turn++ % characters.Length];
        }

        static void Send(Animator a, string trigger)
        {
            if (a == null || !a.isActiveAndEnabled || a.runtimeAnimatorController == null) return;
            foreach (var p in a.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == trigger) { a.SetTrigger(trigger); return; }
        }
    }
}
