using UnityEngine;
using VaroGO.Client;

namespace HiddenPath.Stage2
{
    // Uses the project's clock-driven platform contract, so the existing player rides it.
    public sealed class Stage2SwingPlatform : ClockDrivenObstacle
    {
        public Vector3 direction = Vector3.right;
        public float amplitude = 3f;
        public float period = 6f;
        public Transform[] chains;
        public Vector3[] anchors;
        protected override void Evaluate(double t, out Vector3 position, out Quaternion rotation)
        {
            float a = (float)t * Mathf.PI * 2 / period;
            position = originPos + direction.normalized * (Mathf.Sin(a) * amplitude) + Vector3.up * (.7f * (1 - Mathf.Cos(a * 2)));
            rotation = originRot;
        }
        void LateUpdate()
        {
            if (chains == null || anchors == null) return;
            for (int i = 0; i < chains.Length && i < anchors.Length; i++)
            {
                var bottom = transform.position + new Vector3(0, .35f, i == 0 ? -1.5f : 1.5f);
                var delta = anchors[i] - bottom;
                chains[i].position = (bottom + anchors[i]) * .5f;
                chains[i].rotation = Quaternion.FromToRotation(Vector3.up, delta);
                chains[i].localScale = new Vector3(.1f, delta.magnitude / 2, .1f);
            }
        }
    }
}
