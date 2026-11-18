using UnityEngine;
using VaroGO.Client;

namespace HiddenPath.Blockout
{
    public sealed class Z3RouteTrigger : MonoBehaviour
    {
        public enum TriggerKind { Arrival, Choice, Fall }
        public Z3ForestPuzzle puzzle;
        public TriggerKind kind;
        public int fork;
        public int branch;
        public Transform trapFloor;
        public bool bounce;
        public Vector3 outward;

        void OnTriggerEnter(Collider other)
        {
            if (puzzle == null) return;
            var player = other.GetComponent<PlayerController>();
            if (player == null) return;
            if (kind == TriggerKind.Arrival) puzzle.Arrive(player, fork);
            else if (kind == TriggerKind.Choice) puzzle.Choose(player, fork, branch, trapFloor, bounce,
                trapFloor != null ? trapFloor.forward : outward);
            else puzzle.Fall(player);
        }
    }
}
