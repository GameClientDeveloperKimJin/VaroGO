using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    /// <summary>
    /// 역할에 맞는 캐릭터 모델만 켠다. 레드(A) = C1, 블루(B) = C2.
    /// 역할이 아직 없으면(오프라인 시작 직후 등) 내 캐릭터 = 레드, 상대 = 블루로 보여 준다.
    /// </summary>
    public class PlayerModelSwap : MonoBehaviour
    {
        [Tooltip("레드(A) 캐릭터 모델 (C1)")]
        [SerializeField] GameObject redModel;
        [Tooltip("블루(B) 캐릭터 모델 (C2)")]
        [SerializeField] GameObject blueModel;
        [SerializeField] PlayerController player;

        bool? shownRed;

        void Awake()
        {
            if (player == null) player = GetComponentInParent<PlayerController>();
            Refresh();
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            bool red;
            var role = player != null ? player.Role : PlayerRole.None;
            if (role == PlayerRole.A_Red) red = true;
            else if (role == PlayerRole.B_Blue) red = false;
            else red = player == null || player.IsLocal;
            if (shownRed == red) return;
            shownRed = red;

            if (redModel != null) redModel.SetActive(red);
            if (blueModel != null) blueModel.SetActive(!red);
            var shown = red ? redModel : blueModel;
            if (player != null && shown != null) player.SetAnimator(shown.GetComponentInChildren<Animator>(true));
        }
    }
}
