using UnityEngine;
using UnityEngine.InputSystem;
using VaroGO.Client;
using VaroGO.Shared;

namespace HiddenPath.Stage2
{
    // Offline design preview only. Production two-player puzzle authority is intentionally separate.
    public sealed class Stage2DungeonPreview : MonoBehaviour
    {
        public Transform player, exchangeAltar;
        public Transform[] mirrorDoors;
        public Transform[] rollers;
        public Transform[] alternatingBridges;
        public Transform[] rewardAcorns;
        public Transform[] livingSap;
        public LocalGoalState goal;
        Vector3[] doorOrigins, rollerOrigins, bridgeOrigins;
        Vector3[] sapOrigins;
        int collected;
        GUIStyle text;
        void Start()
        {
            doorOrigins = System.Array.ConvertAll(mirrorDoors, t => t.position);
            rollerOrigins = System.Array.ConvertAll(rollers, t => t.position);
            bridgeOrigins = System.Array.ConvertAll(alternatingBridges, t => t.position);
            sapOrigins = System.Array.ConvertAll(livingSap, t => t.position);
        }
        void Update()
        {
            if (player == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame && Vector3.Distance(player.position, exchangeAltar.position) < 4f)
            {
                if (GameServices.Role is OfflineRoleProvider role)
                    role.SetRole(role.LocalRole == PlayerRole.A_Red ? PlayerRole.B_Blue : PlayerRole.A_Red);
            }
            float time = GameServices.Clock == null ? Time.time : (float)GameServices.Clock.Now;
            for(int i=0;i<livingSap.Length;i++)livingSap[i].position=sapOrigins[i]+Vector3.right*(Mathf.Sin(time*.55f+i)*1.3f);
            foreach(var acorn in rewardAcorns)
            {
                if(!acorn.gameObject.activeSelf)continue;
                acorn.Rotate(0,40*Time.deltaTime,0);
                if(Vector3.Distance(player.position+Vector3.up,acorn.position)<1.7f){acorn.gameObject.SetActive(false);collected++;}
            }
            bool leftOpen = Mathf.Repeat(time, 12) < 6;
            for (int i = 0; i < mirrorDoors.Length; i++)
                mirrorDoors[i].position = Vector3.Lerp(mirrorDoors[i].position, doorOrigins[i] + Vector3.up * ((i == 0) == leftOpen ? 6 : 0), Time.deltaTime * 3);
            for (int i = 0; i < rollers.Length; i++)
            {
                // Across the lane; safe alcoves lie outside the swept volume.
                rollers[i].position = rollerOrigins[i] + Vector3.forward * (Mathf.Sin(time * .7f + i * 2.1f) * 3.4f);
                rollers[i].Rotate(0, 75 * Time.deltaTime, 0, Space.Self);
            }
            for (int i = 0; i < alternatingBridges.Length; i++)
            {
                bool on = Mathf.Repeat(time + i * 4, 8) < 4;
                // Lower rather than vanish, so a player can read the next cycle before jumping.
                alternatingBridges[i].position = Vector3.Lerp(alternatingBridges[i].position, bridgeOrigins[i] + Vector3.down * (on ? 0 : 1.2f), Time.deltaTime * 2);
            }
        }
        void OnGUI()
        {
            if (player == null) return;
            if (text == null) text = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            float x = player.position.x;
            string title = x < 34 ? "Z0 / THE SUNKEN GATE" : x < 72 ? "Z1 / THREE GLASS PATHS" : x < 101 ? "Z2 / ALTERNATING CROSSING" : x < 131 ? "Z3 / FORGE YOUR PATH" : x < 166 ? "Z4 / OATH EXCHANGE" : x < 198 ? "Z5 / MIRROR PORTCULLIS" : x < 232 ? "Z6 / CHAIN SWINGS" : x < 270 ? "Z7 / CRUSHER GALLERY" : "Z8 / THE FINAL SEAL";
            GUI.Box(new Rect(16, 38, 390, 72), GUIContent.none);
            GUI.Label(new Rect(28, 46, 370, 60), "HIDDEN PATH  /  DUNGEON II\n" + title + "\nWASD + Mouse | Space Jump | F Push | F1/F2 Vision", text);
            string hint = Vector3.Distance(player.position, exchangeAltar.position) < 4 ? "E: exchange roles (offline preview)" : "Follow amber lamps. Cyan beacons save progress. F5 respawn / F6 next checkpoint.";
            GUI.Label(new Rect(20, Screen.height - 40, Screen.width - 40, 34), goal != null && goal.IsCleared ? "DUNGEON II COMPLETE" : hint + "   Acorns: " + collected + "/" + rewardAcorns.Length, text);
        }
    }
}
