using HiddenPath.Blockout;
using UnityEngine;
using VaroGO.Client;

namespace VaroGO.Client.Map
{
    // Z3 갈림길: 이미 통과한(정답으로 클리어한) 갈림길의 선택(Choice) 판정을 꺼서,
    // 정답 교차로에 도착한 뒤 옆 갈랫길로 되돌아가도 오답/붕괴 처리가 되지 않게 한다.
    // 새 라운드가 시작돼 ClearedForks가 줄어들면 다시 켜진다. Z3ForestPuzzle/Z3RouteTrigger는 수정하지 않는다.
    public sealed class Z3PassedForkGuard : MonoBehaviour
    {
        public Z3ForestPuzzle puzzle;

        Z3RouteTrigger[] _choices;
        Collider[] _colliders;
        bool[] _disabled;

        void Awake()
        {
            if (puzzle == null) puzzle = GetComponentInParent<Z3ForestPuzzle>();
            var all = GetComponentsInChildren<Z3RouteTrigger>(true);
            var list = new System.Collections.Generic.List<Z3RouteTrigger>();
            foreach (var t in all) if (t.kind == Z3RouteTrigger.TriggerKind.Choice) list.Add(t);
            _choices = list.ToArray();
            _colliders = new Collider[_choices.Length];
            _disabled = new bool[_choices.Length];
            for (int i = 0; i < _choices.Length; i++) _colliders[i] = _choices[i].GetComponent<Collider>();
        }

        // 붕괴/추락으로 복귀 중(Recovering)일 때 내 캐릭터가 항상 낙하 애니를 재생하게 한다.
        // (좌석에 앉혀 옮기는 방식이라 땅 판정이 켜져 서 있는 자세가 나오기 때문)
        PlayerController _fallPlayer;

        void UpdateFallAnim()
        {
            if (puzzle == null) return;
            if (puzzle.Recovering)
            {
                if (_fallPlayer == null)
                    foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                        if (pc.IsLocal && pc.IsSeated && pc.Seat != null && pc.Seat.name == "Z3RecoverCarrier") { _fallPlayer = pc; break; }
                if (_fallPlayer != null) _fallPlayer.ForceFallAnim = true;
            }
            else if (_fallPlayer != null)
            {
                _fallPlayer.ForceFallAnim = false;
                _fallPlayer = null;
            }
        }

        void Update()
        {
            UpdateFallAnim();
            if (puzzle == null || _choices == null) return;
            int cleared = puzzle.ClearedForks;
            for (int i = 0; i < _choices.Length; i++)
            {
                if (_colliders[i] == null) continue;
                bool passed = _choices[i].fork < cleared;
                if (passed != _disabled[i])
                {
                    _colliders[i].enabled = !passed;
                    _disabled[i] = passed;
                }
            }
        }

        void OnDisable()
        {
            if (_fallPlayer != null) { _fallPlayer.ForceFallAnim = false; _fallPlayer = null; }
            if (_choices == null) return;
            for (int i = 0; i < _choices.Length; i++)
                if (_colliders[i] != null && _disabled[i]) { _colliders[i].enabled = true; _disabled[i] = false; }
        }
    }
}
