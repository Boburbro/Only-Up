using UnityEngine;

namespace OnlyUp
{
    /// <summary>O'yinchi atrofida uchib yuradigan kapalaklar.</summary>
    public class Butterflies : MonoBehaviour
    {
        public Transform follow;
        public Mesh wing;
        public Material material;
        public int count = 36;
        public float radius = 11f;
        public float wingScale = 1f;

        struct Butterfly
        {
            public Transform body, left, right;
            public Vector3 home;
            public float phase, speed;
        }

        Butterfly[] items;

        void Start()
        {
            items = new Butterfly[count];
            for (int i = 0; i < count; i++)
            {
                var body = new GameObject("Butterfly").transform;
                body.SetParent(transform, false);
                items[i] = new Butterfly
                {
                    body = body,
                    left = Wing(body, 1f),
                    right = Wing(body, -1f),
                    home = RandomHome(),
                    phase = Random.value * 20f,
                    speed = Random.Range(0.35f, 0.8f)
                };
                body.localScale = Vector3.one * wingScale;
                body.position = items[i].home;
            }
        }

        Transform Wing(Transform body, float side)
        {
            var go = new GameObject("Wing", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(body, false);
            go.transform.localScale = new Vector3(side, 1f, 1f);
            go.GetComponent<MeshFilter>().sharedMesh = wing;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        Vector3 Center => follow != null ? follow.position : transform.position;

        Vector3 RandomHome()
        {
            Vector3 offset = Random.insideUnitSphere * radius;
            offset.y = Mathf.Abs(offset.y) * 0.5f + 0.2f;
            return Center + offset;
        }

        void Update()
        {
            for (int i = 0; i < items.Length; i++)
            {
                ref Butterfly item = ref items[i];
                // Uzoqda qolgan kapalak o'yinchi yaqinida qayta paydo bo'ladi.
                if ((item.home - Center).sqrMagnitude > radius * radius * 2.5f) item.home = RandomHome();

                float t = Time.time * item.speed + item.phase;
                var target = item.home + new Vector3(Mathf.Sin(t * 1.3f), Mathf.Sin(t * 2.1f) * 0.35f,
                    Mathf.Cos(t)) * Mathf.Min(1.4f, radius * 0.2f);
                Vector3 move = target - item.body.position;
                item.body.position = target;
                move.y = 0f;
                if (move.sqrMagnitude > 1e-8f) item.body.rotation = Quaternion.LookRotation(move);

                float flap = Mathf.Sin(Time.time * 18f + item.phase) * 55f + 15f;
                item.left.localRotation = Quaternion.Euler(0f, 0f, flap);
                item.right.localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
        }
    }
}
