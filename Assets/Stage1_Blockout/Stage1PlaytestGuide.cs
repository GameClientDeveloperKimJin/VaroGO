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
                x < 212 ? "07 / Boulder passage" : x < 248 ? "08 / Thorn canyon" : "09 / Forest gate";
            GUI.Box(new Rect(18, 42, 360, 78), GUIContent.none);
            GUI.Label(new Rect(32, 50, 338, 27), area, title);
            GUI.Label(new Rect(32, 80, 338, 34), "WASD Move   Mouse Look   Space Jump\nE Interact   F + Move Push   Esc Release mouse", small);
            string hint = x < 28 ? "Follow the lanterns into the forest." : x < 60 ? "F1 / F2: switch red and blue vision for solo testing." :
                x < 88 ? "Land on a purple mushroom to bounce higher." : x < 116 ? "Take either forest trail. The green beacon saves your progress." :
                x < 156 ? (revealConsole == null ? "Pick up a stone (E) and throw it (hold E) at the hidden steps to reveal them." :
                           revealed ? "The hidden crossing is revealed." : "Approach the cyan crystal and press E to reveal the crossing.") :
                x < 182 ? "Purple sap slows you down. Jump between the dry stones." :
                x < 212 ? "Hold F and walk into the boulder. Solo pushing is enabled here." :
                x < 248 ? "Stay on the narrow path. Jump the low thorn barriers." : "Climb the steps and enter the gate.";
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
