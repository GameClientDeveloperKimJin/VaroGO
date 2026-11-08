using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VaroGO.Shared;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// 닉네임 팝업 (UI_NicknamePopup 프리팹 루트). 저장은 Shared PlayerNickname(P22)에 맡긴다. 네트워크 동작 없음.
    /// ① 입력칸: 저장된 닉네임이 있으면 미리 채움 ② 글자 수 "현재 / 최대"
    /// ③ 규칙 안내, 어긋나면 빨간 오류 문구 ④ 취소·확인: 규칙을 지켜야 확인 활성, 확인 = 저장.
    /// 열리는 곳: 타이틀 "게임 시작"(닉네임 없을 때, TitleNicknameGate). 로비의 닉네임 변경 기능은 2026-11-09 제거.
    /// </summary>
    public class NicknamePopup : MonoBehaviour
    {
        [SerializeField] GameObject window;
        [SerializeField] TMP_InputField input;
        [SerializeField] TMP_Text countText;
        [SerializeField] TMP_Text ruleText;
        [SerializeField] Button confirmButton;
        [SerializeField] Button cancelButton;
        [SerializeField] Button closeButton;

        [Header("색")]
        [SerializeField] Color ruleColor = new(0.45f, 0.47f, 0.52f);
        [SerializeField] Color errorColor = new(0.86f, 0.22f, 0.22f);

        Action<string> onConfirm;
        Action onCancel;

        string RuleHint => $"{PlayerNickname.MinLength}~{PlayerNickname.MaxLength}자, 한글 · 영문 · 숫자만 쓸 수 있어요";

        public bool IsOpen => window != null && window.activeSelf;

        void Awake()
        {
            input.characterLimit = PlayerNickname.MaxLength;
            input.onValueChanged.AddListener(_ => Refresh());
            input.onSubmit.AddListener(_ => Confirm());
            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(Cancel);
            if (closeButton != null) closeButton.onClick.AddListener(Cancel);
            if (!IsOpen) window.SetActive(false);
        }

        /// <summary>팝업을 연다. 확인하면 저장된 닉네임으로 onConfirm, 취소하면 onCancel</summary>
        public void Open(Action<string> onConfirm = null, Action onCancel = null)
        {
            this.onConfirm = onConfirm;
            this.onCancel = onCancel;
            window.SetActive(true);
            input.SetTextWithoutNotify(PlayerNickname.Current);
            Refresh();
            input.Select();
            input.ActivateInputField();
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Cancel();
        }

        void Refresh()
        {
            string text = input.text ?? string.Empty;
            countText.text = $"{text.Trim().Length} / {PlayerNickname.MaxLength}";

            // 비어 있을 땐 안내 문구, 쓰기 시작하면 규칙 검사
            string error = text.Length == 0 ? null : PlayerNickname.Validate(text);
            ruleText.text = error ?? RuleHint;
            ruleText.color = error != null ? errorColor : ruleColor;
            confirmButton.interactable = PlayerNickname.IsValid(text);
        }

        void Confirm()
        {
            if (!IsOpen || !PlayerNickname.TrySave(input.text)) return;
            var callback = onConfirm;
            Close();
            callback?.Invoke(PlayerNickname.Current);
        }

        void Cancel()
        {
            if (!IsOpen) return;
            var callback = onCancel;
            Close();
            callback?.Invoke();
        }

        void Close()
        {
            onConfirm = null;
            onCancel = null;
            window.SetActive(false);
        }
    }
}
