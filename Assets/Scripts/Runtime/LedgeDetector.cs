using UnityEngine;

namespace OnlyUp
{
    public struct Ledge
    {
        public Collider Collider;
        /// <summary>Osilib turganda oyoq nuqtasi.</summary>
        public Vector3 HangPoint;
        /// <summary>Tepaga chiqib bo'lgandagi oyoq nuqtasi.</summary>
        public Vector3 StandPoint;
        /// <summary>Devordan tashqariga qaragan yo'nalish.</summary>
        public Vector3 WallNormal;
    }

    /// <summary>Personaj oldida qo'l yetadigan qirra bor-yo'qligini aniqlaydi.</summary>
    public static class LedgeDetector
    {
        /// <summary>Oyoqdan qirragacha bo'lgan eng kichik va eng katta balandlik.</summary>
        public const float MinReach = 1.0f;
        public const float MaxReach = 2.2f;
        /// <summary>Osilganda oyoq qirradan shuncha pastda turadi.</summary>
        public const float HangDepth = 1.65f;

        public static bool TryFind(Vector3 feet, Vector3 direction, float radius, float height, out Ledge ledge)
        {
            ledge = default;
            const int mask = Physics.DefaultRaycastLayers;
            const QueryTriggerInteraction ignore = QueryTriggerInteraction.Ignore;

            // 1. Yuqoridan pastga: qirraning ustki sirti.
            Vector3 probe = feet + direction * (radius + 0.25f) + Vector3.up * MaxReach;
            if (!Physics.Raycast(probe, Vector3.down, out var top, MaxReach - MinReach, mask, ignore))
                return false;
            if (top.normal.y < 0.85f || top.collider.GetComponent<BouncePad>() != null) return false;

            // 2. Oldinga: qirra ostidagi devor.
            Vector3 chest = new Vector3(feet.x, top.point.y - 0.05f, feet.z);
            if (!Physics.Raycast(chest, direction, out var wall, radius + 0.6f, mask, ignore)) return false;
            if (Mathf.Abs(wall.normal.y) > 0.3f) return false;

            Vector3 normal = new Vector3(wall.normal.x, 0f, wall.normal.z).normalized;
            Vector3 edge = new Vector3(wall.point.x, top.point.y, wall.point.z);
            Vector3 hang = edge + normal * (radius + 0.05f) + Vector3.down * HangDepth;
            Vector3 stand = edge - normal * (radius + 0.05f) + Vector3.up * 0.05f;
            Vector3 above = new Vector3(hang.x, stand.y, hang.z);

            // 3. Tepada va chiqish yo'lida joy bo'sh bo'lishi kerak.
            if (!Fits(stand, radius, height) || !Fits(above, radius, height)) return false;

            ledge = new Ledge
            {
                Collider = top.collider,
                HangPoint = hang,
                StandPoint = stand,
                WallNormal = normal
            };
            return true;
        }

        static bool Fits(Vector3 feet, float radius, float height)
        {
            float r = radius - 0.02f;
            Vector3 bottom = feet + Vector3.up * (radius + 0.02f);
            Vector3 head = feet + Vector3.up * (height - radius);
            return !Physics.CheckCapsule(bottom, head, r, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
        }
    }
}
