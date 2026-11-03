using UnityEngine;

namespace VaroGO.Client
{
    // P19 임시 연출: 퍼지며 사라지는 구체(부딪힘·마법 효과·펑 등장). 정식 이펙트가 들어오면 교체한다
    public class FlightFx : MonoBehaviour
    {
        static Material shared;

        Material material;
        Color color;
        float start, duration, fromSize, toSize;

        public static void Burst(Vector3 position, Color color, float size, float duration = 0.5f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "FlightFx";
            Destroy(go.GetComponent<Collider>());
            var fx = go.AddComponent<FlightFx>();
            shared ??= new Material(Shader.Find("Sprites/Default"));
            fx.material = new Material(shared);
            go.GetComponent<Renderer>().sharedMaterial = fx.material;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.position = position;
            fx.color = color;
            fx.start = Time.time;
            fx.duration = Mathf.Max(0.05f, duration);
            fx.fromSize = size * 0.3f;
            fx.toSize = size;
            fx.Update();
        }

        void Update()
        {
            float t = Mathf.Clamp01((Time.time - start) / duration);
            float e = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * Mathf.Lerp(fromSize, toSize, e);
            var c = color;
            c.a = Mathf.Lerp(0.8f, 0f, t);
            material.color = c;
            if (t >= 1f) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
