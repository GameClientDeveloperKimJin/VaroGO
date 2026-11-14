using UnityEngine;
using UnityEngine.InputSystem;
using VaroGO.Client;
using VaroGO.Shared;

namespace HiddenPath.Stage1
{
    // Scene-local onboarding for the offline level-design course.
    public sealed class Stage1PlaytestGuide : MonoBehaviour
    {
        public Transform player;
        public Transform revealConsole;
        public RevealablePlatform[] hiddenPlatforms;
        public LocalGoalState goal;
        [Tooltip("수정에서 이 거리(m) 안이면 E로 숨겨진 발판을 드러낸다")]
        public float revealRange = 4.5f;
        [Tooltip("구역 안내가 '08 가시 협곡'으로 바뀌는 x 위치 (구역을 옮긴 씬에서만 바꾼다)")]
        public float Zone7StartX = 212f;
        [Tooltip("구역 안내가 '09 숲의 문'으로 바뀌는 x 위치")]
        public float Zone8StartX = 248f;
        bool revealed;
        GUIStyle title, small, prompt;

        bool NearConsole => player != null && revealConsole != null &&
                            Vector3.Distance(player.position, revealConsole.position) < revealRange;

        void Update()
        {
            if (NearConsole && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                foreach (var platform in hiddenPlatforms)
                    if (platform != null) platform.Reveal(SwitchStates.LocalRole);
                revealed = true;
            }
        }

        void OnGUI()
        {
            if (player == null) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
                small = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            }
            float x = player.position.x;
            string area = x < 28 ? "01 / Forest entrance" : x < 60 ? "02 / Two visions" :
                x < 88 ? "03 / Mushroom ascent" : x < 116 ? "04 / Forked forest" :
                x < 156 ? "05 / Hidden crossing" : x < 182 ? "06 / Sticky grove" :
                x < Zone7StartX ? "07 / Boulder passage" : x < Zone8StartX ? "08 / Thorn canyon" : "09 / Forest gate";
            GUI.Box(new Rect(18, 42, 360, 78), GUIContent.none);
            GUI.Label(new Rect(32, 50, 338, 27), area, title);
            GUI.Label(new Rect(32, 80, 338, 34), "WASD Move   Mouse Look   Space Jump\nE Interact   F + Move Push   Esc Release mouse", small);
            string hint = x < 28 ? "Follow the lanterns into the forest." : x < 60 ? "F1 / F2: switch red and blue vision for solo testing." :
                x < 88 ? "Land on a purple mushroom to bounce higher." : x < 116 ? "Take either forest trail. The green beacon saves your progress." :
                x < 156 ? (revealConsole == null ? "Pick up a stone (E) and throw it (hold E) at the hidden steps to reveal them." :
                           revealed ? "The hidden crossing is revealed." : "Approach the cyan crystal and press E to reveal the crossing.") :
                x < 182 ? "Sap slows you down: use the dry stones. Push the small stone (E) to the wall to climb the terrace." :
                x < Zone7StartX ? "Grab the boulder (E) and walk into it together. Drop it into the pit to climb up. Solo pushing is enabled here." :
                x < Zone8StartX ? "Jump the gaps and the low thorn. Watch for falling branches: free a bound partner with A/D." :
                          "Seesaw: one stands on the low end, the other jumps on the high end (solo: jump on the high end). Pull the lever (E) to raise the stepping stones.";
            GUI.Label(new Rect(26, Screen.height - 56, Screen.width - 52, 48), hint + "\nF5 Respawn   F6 Next checkpoint (playtest)", small);
            if (NearConsole)
            {
                if (prompt == null) prompt = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
                GUI.Label(new Rect(0, Screen.height * 0.65f, Screen.width, 30), revealed ? "건널목이 드러났다" : "E: 수정 만지기", prompt);
            }
            if (goal != null && goal.IsCleared)
                GUI.Label(new Rect(Screen.width * .5f - 110, Screen.height * .35f, 250, 40), "FOREST PATH COMPLETE", title);
        }
    }
}
