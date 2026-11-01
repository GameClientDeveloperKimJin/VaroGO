using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VaroGO.Client.Voice;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// ESC 일시정지 메뉴 + 음성 설정 팝업 (인게임 보이스 설계서 01·03·04).
    /// - ESC(패드 Start)로 열고 닫는다. 음성 설정에서 ESC = 뒤로.
    /// - 게임은 멈추지 않는다(2인 실시간). 메뉴가 열린 동안 내 입력만 막고 커서를 푼다.
    /// - 음성은 끊기지 않는다. 마이크·장치·상대 음성 볼륨은 바꾸는 즉시 반영·저장.
    /// 씬에 미리 놓은 PersistentUI 프리팹 안에 있다. 모양은 프리팹에서 바꾼다.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        enum Page { Closed, Pause, Voice }

        [Header("화면")]
        [Tooltip("메뉴 전체(뒤 화면 어둡게 포함). 닫히면 꺼진다")]
        [SerializeField] GameObject menuRoot;
        [SerializeField] GameObject pausePanel;
        [SerializeField] GameObject voicePanel;

        [Header("일시정지")]
        [SerializeField] Button continueButton;
        [SerializeField] Button optionButton;

        [Header("음성 설정")]
        [SerializeField] Button closeButton;
        [SerializeField] Button backButton;
        [SerializeField] Button micOnButton;
        [SerializeField] Button micOffButton;
        [SerializeField] TMP_Dropdown deviceDropdown;
        [Tooltip("입력 확인 막대의 채움 부분. 가로 길이가 입력 크기만큼 늘어난다")]
        [SerializeField] RectTransform levelFill;
        [SerializeField] Slider partnerVolumeSlider;
        [SerializeField] TMP_Text partnerVolumeText;

        [Header("마이크 버튼 색")]
        [SerializeField] Color selectedColor = new(0.3f, 0.45f, 0.75f);
        [SerializeField] Color unselectedColor = new(0.22f, 0.24f, 0.3f);

        const string DefaultDeviceLabel = "기본 장치";

        static PauseMenu instance;
        static int escHandledFrame = -1;

        Page page = Page.Closed;
        readonly List<string> deviceValues = new();

        public static bool IsOpen => instance != null && instance.page != Page.Closed;

        // 다른 메뉴가 이번 프레임 Esc를 이미 썼다고 알린다
        public static void MarkEscHandled() => escHandledFrame = Time.frameCount;

        void Awake()
        {
            if (instance == null) instance = this;

            continueButton.onClick.AddListener(Close);
            optionButton.onClick.AddListener(() => ShowPage(Page.Voice));
            closeButton.onClick.AddListener(Close);
            backButton.onClick.AddListener(() => ShowPage(Page.Pause));
            micOnButton.onClick.AddListener(() => VoiceManager.SetMicEnabled(true));
            micOffButton.onClick.AddListener(() => VoiceManager.SetMicEnabled(false));
            deviceDropdown.onValueChanged.AddListener(OnDeviceChanged);
            partnerVolumeSlider.minValue = 0f;
            partnerVolumeSlider.maxValue = 100f;
            partnerVolumeSlider.wholeNumbers = true;
            partnerVolumeSlider.onValueChanged.AddListener(v => VoiceSettings.PartnerVolume = Mathf.RoundToInt(v));

            ShowPage(Page.Closed);
        }

        void OnDestroy()
        {
            if (instance != this) return;
            if (page != Page.Closed) PlayerInputReader.PauseOpen = false;
            instance = null;
        }

        void Update()
        {
            if (instance != this) return;

            // 내 캐릭터가 없거나 끊김 화면이 뜨면 닫는다
            if (FindLocal() == null || PlayerInputReader.Blocked)
            {
                if (page != Page.Closed) Close();
                return;
            }

            if (page == Page.Voice) RefreshVoice();

            bool esc = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (!esc || escHandledFrame == Time.frameCount) return;

            switch (page)
            {
                case Page.Closed:
                    if (!PlayerInputReader.MenuOpen) Open(); // 빠른 이동 메뉴가 열려 있으면 그쪽이 Esc를 쓴다
                    break;
                case Page.Pause:
                    Close();
                    break;
                case Page.Voice:
                    if (deviceDropdown.IsExpanded) deviceDropdown.Hide(); // 펼친 목록부터 닫는다
                    else ShowPage(Page.Pause);
                    break;
            }
            MarkEscHandled();
        }

        static PlayerController FindLocal()
        {
            var all = PlayerController.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].IsLocal) return all[i];
            return null;
        }

        void Open()
        {
            EnsureEventSystem();
            PlayerInputReader.PauseOpen = true;
            Cursor.lockState = CursorLockMode.None;
            ShowPage(Page.Pause);
        }

        void Close()
        {
            ShowPage(Page.Closed);
            PlayerInputReader.PauseOpen = false;
            if (!PlayerInputReader.Blocked && !PlayerInputReader.MenuOpen) Cursor.lockState = CursorLockMode.Locked;
        }

        void ShowPage(Page next)
        {
            page = next;
            if (menuRoot != null) menuRoot.SetActive(next != Page.Closed);
            pausePanel.SetActive(next == Page.Pause);
            voicePanel.SetActive(next == Page.Voice);

            if (next == Page.Voice)
            {
                FillDevices();
                partnerVolumeSlider.SetValueWithoutNotify(VoiceSettings.PartnerVolume);
                RefreshVoice();
            }

            // 패드로도 고를 수 있게 첫 버튼을 선택해 둔다
            var es = EventSystem.current;
            if (es == null) return;
            if (next == Page.Pause) es.SetSelectedGameObject(continueButton.gameObject);
            else if (next == Page.Voice) es.SetSelectedGameObject(micOnButton.gameObject);
            else es.SetSelectedGameObject(null);
        }

        void FillDevices()
        {
            var voice = VoiceManager.Current;
            string[] devices = voice != null ? voice.MicDevices : Microphone.devices;
            string current = voice != null ? voice.MicDevice : VoiceSettings.MicDevice;

            deviceValues.Clear();
            deviceValues.Add(""); // 0번 = 기본 장치
            var labels = new List<string> { DefaultDeviceLabel };
            if (devices != null)
                foreach (var d in devices) { deviceValues.Add(d); labels.Add(d); }

            deviceDropdown.ClearOptions();
            deviceDropdown.AddOptions(labels);
            int index = deviceValues.IndexOf(current ?? "");
            deviceDropdown.SetValueWithoutNotify(index < 0 ? 0 : index);
            deviceDropdown.RefreshShownValue();
        }

        void OnDeviceChanged(int index)
        {
            if (index < 0 || index >= deviceValues.Count) return;
            VoiceManager.SetMicDevice(deviceValues[index]);
        }

        void RefreshVoice()
        {
            var voice = VoiceManager.Current;
            bool micOn = voice != null ? voice.MicEnabled : VoiceSettings.MicEnabled;
            Tint(micOnButton, micOn);
            Tint(micOffButton, !micOn);

            if (levelFill != null)
            {
                float level = voice != null ? voice.InputLevel : 0f;
                levelFill.anchorMax = new Vector2(Mathf.Clamp01(level), levelFill.anchorMax.y);
            }

            if (partnerVolumeText != null) partnerVolumeText.text = VoiceSettings.PartnerVolume.ToString();
        }

        void Tint(Button button, bool selected)
        {
            if (button.targetGraphic != null) button.targetGraphic.color = selected ? selectedColor : unselectedColor;
        }

        // 코스 씬에는 EventSystem이 없을 수 있다. 없으면 버튼이 눌리지 않으므로 만든다
        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem (PauseMenu)", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }
    }
}
