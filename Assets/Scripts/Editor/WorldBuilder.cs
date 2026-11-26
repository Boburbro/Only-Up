using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OnlyUp.EditorTools
{
    /// <summary>
    /// Asosiy xarita: 4 zona, har biri bir necha "qavat"dan iborat. Qavatlar shimol, sharq va g'arbga
    /// siljib boradi (janubga hech qachon), shuning uchun yo'l aylana bo'lmaydi. Har qavat tagida
    /// yiqilganni ushlab qoladigan maydon bor. Oraliqlar o'lchangan sakrash masofalariga tayanadi
    /// (JumpMetricsTests).
    /// </summary>
    public static class WorldBuilder
    {
        public const string ScenePath = "Assets/Scenes/World.unity";
        /// <summary>Qavat ichidagi yo'l shakli: asosiy yo'nalishdan necha gradus og'ishi.</summary>
        static readonly float[][] Weaves =
        {
            new[] { 0f, 22f, 42f, 50f, 30f, 6f, -22f, -44f, -50f, -28f }, // ilon izi
            new[] { 28f, 28f, -28f, -28f },                               // keng zigzag
            new[] { -45f, -45f, -45f, -15f, 15f, 45f, 45f, 45f, 15f, -15f }, // keskin burilishlar
            new[] { 0f, 0f, 18f, 36f, 36f, 8f, -26f, -26f, 0f },          // egri yo'lak
            new[] { 35f, 10f, -35f, -10f }                                // qisqa to'lqin
        };

        /// <summary>Har qavatning asosiy yo'nalishi (0 shimol, 90 sharq, -90 g'arb).</summary>
        static readonly float[] Directions = { 0f, 90f, 0f, -90f, -90f, 0f, 90f, 0f, 90f, 90f, 0f, -90f, 0f, -90f };

        public const float CatchDepth = 0.06f;

        static Transform level;
        static readonly List<Collider> route = new List<Collider>();
        static readonly List<int> zoneStarts = new List<int>();
        static Vector3 cursor;
        static Vector3 cursorHalf;
        static float cursorYaw, weaveYaw;
        static int floorStep;
        static bool keepHeading;

        class Floor
        {
            public int first, zone;
            public float height;
        }

        static readonly List<Floor> floors = new List<Floor>();

        static Material Mat(string name, float r, float g, float b) =>
            TestSceneBuilder.Material(name, new Color(r, g, b));

        [MenuItem("Only Up/Asosiy xaritani qayta yaratish")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            level = new GameObject("Level").transform;
            route.Clear();
            zoneStarts.Clear();

            TestSceneBuilder.Box(level, "Ground", new Vector3(0f, -0.5f, 15f), new Vector3(140f, 1f, 140f),
                Mat("Ground", 0.35f, 0.3f, 0.25f));

            cursor = new Vector3(0f, 0f, 4f);
            cursorHalf = Vector3.zero;
            cursorYaw = 0f;
            keepHeading = false;
            floors.Clear();
            floors.Add(new Floor());
            floorStep = 0;

            Yard();
            Pipes();
            Construction();
            Sky();

            var finish = Step("Finish", 1.4f, 0.8f, new Vector3(6f, 0.8f, 6f), Mat("Finish", 0.95f, 0.75f, 0.1f));
            level.gameObject.AddComponent<CourseRoute>().steps = route.ToArray();

            var decks = CatchDecks();
            Decorate(decks);

            TestSceneBuilder.AddPlayerAndCamera();
            Camera.main.farClipPlane = 600f;
            var session = new GameObject("GameSession").AddComponent<GameSession>();
            session.Configure(Object.FindAnyObjectByType<PlayerMotor>(),
                Camera.main.GetComponent<ThirdPersonCamera>(), finish);
            session.gameObject.AddComponent<UI.GameHud>();

            WorldArt.Apply(route, zoneStarts, decks);

            EditorSceneManager.SaveScene(scene, ScenePath);
            ProjectSetup.UpdateBuildScenes();
            Debug.Log($"OnlyUp: asosiy xarita yaratildi, {route.Count} ta platforma, balandlik {cursor.y:0.0} m, " +
                      $"zonalar: {string.Join(",", zoneStarts)}, " +
                      $"qavatlar: {floors.Count}, tutib qoluvchi bo'laklar: {decks.Count}");
        }

        // ---------- 1-zona: hovli va eski binolar (oson) ----------
        static void Yard()
        {
            StartZone();
            var wood = Mat("Block", 0.75f, 0.5f, 0.25f);
            var brick = Mat("Wall", 0.55f, 0.22f, 0.17f);

            for (int i = 0; i < 12; i++) Step("Crate", 0.9f + 0.04f * i, 0.8f, new Vector3(2.4f, 0.8f, 2.4f), wood);
            NextFloor();
            for (int i = 0; i < 8; i++) Step("Balcony", 1.2f, 0.75f, new Vector3(2.4f, 0.35f, 2f), brick);
            Step("Ledge", 1.1f, 2.4f, new Vector3(3f, 0.5f, 2.4f), brick);
            for (int i = 0; i < 6; i++) Step("Crate", 1.3f, 0.8f, new Vector3(2f, 0.8f, 2f), wood);
            NextFloor();
            while (cursor.y < 33f) Step("Balcony", 1.3f, 0.8f, new Vector3(2.2f, 0.35f, 2f), brick);
        }

        // ---------- 2-zona: quvurlar va yashiklar ----------
        static void Pipes()
        {
            NextFloor();
            StartZone();
            var wood = Mat("Block", 0.75f, 0.5f, 0.25f);
            var pipe = Mat("Pipe", 0.75f, 0.65f, 0.15f);
            var rust = Mat("Rust", 0.5f, 0.28f, 0.16f);

            for (int i = 0; i < 4; i++)
            {
                Step("Beam", 1.2f, 0.6f, new Vector3(0.6f, 0.3f, 4f), pipe);
                Step("Box", 1.3f, 0.75f, new Vector3(1.6f, 1f, 1.6f), wood);
            }
            Trampoline();
            for (int i = 0; i < 4; i++) Step("Pipe", 1.4f, 0.75f, new Vector3(0.7f, 0.4f, 2.6f), pipe);
            NextFloor();
            for (int i = 0; i < 3; i++) Step("Pipe", 1.4f, 0.75f, new Vector3(0.7f, 0.4f, 2.6f), pipe);
            Bridge(7f, 2.2f);
            Step("Box", 1.2f, 0.6f, new Vector3(2f, 1f, 2f), wood);
            for (int i = 0; i < 5; i++) Step("Barrel", 1.35f, 0.8f, new Vector3(1.5f, 1.1f, 1.5f), rust);
            Step("Ledge", 1.1f, 2.4f, new Vector3(2.8f, 0.4f, 2.4f), rust);
            NextFloor();
            while (cursor.y < 73f)
            {
                Step("Beam", 1.3f, 0.7f, new Vector3(0.55f, 0.3f, 3.2f), pipe);
                Step("Box", 1.4f, 0.8f, new Vector3(1.5f, 1f, 1.5f), wood);
            }
        }

        // ---------- 3-zona: qurilish maydoni ----------
        static void Construction()
        {
            NextFloor();
            StartZone();
            var steel = Mat("Steel", 0.62f, 0.2f, 0.1f);
            var concrete = Mat("Concrete", 0.62f, 0.62f, 0.6f);

            for (int i = 0; i < 5; i++) Step("Girder", 1.45f, 0.75f, new Vector3(0.55f, 0.4f, 3.6f), steel);
            Step("Slab", 1.3f, 0.6f, new Vector3(2.6f, 0.4f, 2.6f), concrete);
            Lift(8f);
            Step("Slab", 1.2f, 0.4f, new Vector3(2.6f, 0.4f, 2.6f), concrete);
            NextFloor();
            for (int i = 0; i < 3; i++) Step("Ledge", 1.1f, 2.4f, new Vector3(2.8f, 0.4f, 2.4f), concrete);
            Step("Slab", 1.4f, 0.7f, new Vector3(2.4f, 0.4f, 2.4f), concrete);
            Spinner();
            Step("Slab", 1.2f, 0.7f, new Vector3(2.4f, 0.4f, 2.4f), concrete);
            for (int i = 0; i < 4; i++) Step("Girder", 1.5f, 0.8f, new Vector3(0.5f, 0.4f, 2.8f), steel);
            NextFloor();
            for (int i = 0; i < 2; i++) Step("Girder", 1.5f, 0.8f, new Vector3(0.5f, 0.4f, 2.8f), steel);
            Bridge(8f, 2.4f);
            Step("Slab", 1.2f, 0.6f, new Vector3(2.4f, 0.4f, 2.4f), concrete);
            while (cursor.y < 118f)
            {
                Step("Scaffold", 1.55f, 0.85f, new Vector3(1.5f, 0.25f, 1.5f), steel);
                Step("Girder", 1.45f, 0.7f, new Vector3(0.5f, 0.4f, 2.4f), steel);
            }
        }

        // ---------- 4-zona: uchib turgan toshlar (qiyin) ----------
        static void Sky()
        {
            NextFloor();
            StartZone();
            var rock = Mat("Rock", 0.42f, 0.4f, 0.38f);

            for (int i = 0; i < 10; i++)
                Step("Rock", 1.5f + 0.02f * i, 0.8f, new Vector3(1.7f - 0.04f * i, 0.8f, 1.7f - 0.04f * i), rock);
            Trampoline();
            NextFloor();
            // Uzun oraliqlar: yurib yetmaydi, Shift bilan yugurish kerak.
            for (int i = 0; i < 3; i++)
            {
                Step("Runway", 1.3f, 0.5f, new Vector3(2f, 0.5f, 5f), rock);
                Step("FarRock", 2.6f, 0.3f, new Vector3(2.4f, 0.8f, 2.4f), rock);
            }
            Bridge(7f, 2.6f);
            Step("Rock", 1.2f, 0.6f, new Vector3(2f, 0.8f, 2f), rock);
            Spinner();
            Step("Rock", 1.2f, 0.7f, new Vector3(2f, 0.8f, 2f), rock);
            NextFloor();
            Lift(7f);
            Step("Rock", 1.2f, 0.4f, new Vector3(2f, 0.8f, 2f), rock);
            Step("Ledge", 1.1f, 2.4f, new Vector3(2.4f, 0.5f, 2.2f), rock);
            while (cursor.y < 169f) Step("Rock", 1.6f, 0.85f, new Vector3(1.35f, 0.7f, 1.35f), rock);
        }

        // ---------- Qismlar ----------

        static void StartZone()
        {
            // Zona qavat boshidagi dam olish maydonchasidan boshlanadi.
            zoneStarts.Add(route.Count == 0 ? 0 : route.Count - 1);
            floors[floors.Count - 1].zone = zoneStarts.Count - 1;
        }

        /// <summary>
        /// Dam olish maydonchasini qo'yadi va yangi qavatni boshlaydi: shu balandlikda ushlab qoluvchi
        /// maydon bo'ladi, yo'l esa boshqa tomonga va boshqa shaklda davom etadi.
        /// </summary>
        static void NextFloor()
        {
            Step("Rest", 1.2f, 0.5f, new Vector3(4f, 0.5f, 4f), Mat("Rest", 0.82f, 0.78f, 0.68f));
            floors.Add(new Floor { first = route.Count - 1, height = cursor.y, zone = zoneStarts.Count - 1 });
            floorStep = 0;
        }

        /// <summary>
        /// Oldingi platformadan keyingisini qo'yadi. gap: qirralar orasi, rise: ustki sirtlar farqi.
        /// </summary>
        static Collider Step(string kind, float gap, float rise, Vector3 size, Material material, bool straight = false)
        {
            int floor = floors.Count - 1;
            float[] weave = Weaves[floor % Weaves.Length];
            // Harakatlanuvchi ko'prikdan keyingi qadam uning yurish chizig'ini davom ettiradi.
            if (!keepHeading) weaveYaw = Directions[floor % Directions.Length] + (straight ? 0f : weave[floorStep++ % weave.Length]);
            keepHeading = false;
            float yaw = weaveYaw;

            // Oldingi platforma boshqa tomonga qaragan bo'lishi mumkin: uning qirrasi shu yo'nalishda o'lchanadi.
            float turn = (yaw - cursorYaw) * Mathf.Deg2Rad;
            float edge = cursorHalf == Vector3.zero ? 0f : Mathf.Min(
                cursorHalf.z / Mathf.Max(Mathf.Abs(Mathf.Cos(turn)), 0.001f),
                cursorHalf.x / Mathf.Max(Mathf.Abs(Mathf.Sin(turn)), 0.001f));

            Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 top = cursor + direction * (edge + gap + size.z * 0.5f);
            top.y = cursor.y + rise;

            string name = $"{route.Count:000}_{kind}";
            var box = TestSceneBuilder.Box(level, name, top + Vector3.down * size.y * 0.5f, size, material);
            box.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            cursor = top;
            cursorHalf = size * 0.5f;
            cursorYaw = yaw;

            var collider = box.GetComponent<Collider>();
            route.Add(collider);
            return collider;
        }

        static void Trampoline()
        {
            var green = Mat("Bounce", 0.2f, 0.65f, 0.3f);
            Step("Trampoline", 1.3f, 0f, new Vector3(2f, 0.3f, 2f), green).gameObject.AddComponent<BouncePad>();
            Step("Landing", 1.0f, 4f, new Vector3(2.8f, 0.4f, 2.8f), Mat("Rest", 0.82f, 0.78f, 0.68f));
        }

        /// <summary>Ko'ndalang yuradigan ko'prik: bir chetida minib, ikkinchi chetida tushiladi.</summary>
        static void Bridge(float length, float speed)
        {
            var blue = Mat("Moving", 0.2f, 0.45f, 0.7f);
            // Ko'prik to'g'ri chiziqda yuradi, shuning uchun undan oldin va keyin yo'l burilmaydi.
            var box = Step("Bridge", 1.1f, 0.3f, new Vector3(2.4f, 0.35f, 2.4f), blue, true);
            keepHeading = true;
            box.gameObject.isStatic = false;
            Vector3 travel = Quaternion.Euler(0f, cursorYaw, 0f) * Vector3.forward * length;
            var mover = box.gameObject.AddComponent<MovingPlatform>();
            mover.travel = travel;
            mover.speed = speed;
            cursor += travel;
        }

        /// <summary>Lift: tepaga va pastga yuradi.</summary>
        static void Lift(float height)
        {
            var blue = Mat("Moving", 0.2f, 0.45f, 0.7f);
            var box = Step("Lift", 1.1f, 0.2f, new Vector3(2.6f, 0.35f, 2.6f), blue);
            box.gameObject.isStatic = false;
            var mover = box.gameObject.AddComponent<MovingPlatform>();
            mover.travel = Vector3.up * height;
            mover.speed = 2.2f;
            cursor += mover.travel;
        }

        static void Spinner()
        {
            var blue = Mat("Moving", 0.2f, 0.45f, 0.7f);
            var box = Step("Spinner", 1.3f, 0.5f, new Vector3(3.4f, 0.35f, 3.4f), blue);
            box.gameObject.isStatic = false;
            box.gameObject.AddComponent<RotatingPlatform>().degreesPerSecond = 30f;
        }

        // ---------- Ushlab qoluvchi maydonlar ----------

        /// <summary>Platforma egallaydigan joy; harakatlanuvchisi uchun butun yurish yo'li.</summary>
        static Bounds Swept(Collider step)
        {
            var bounds = step.bounds;
            var mover = step.GetComponent<MovingPlatform>();
            if (mover != null)
            {
                var end = bounds;
                end.center += mover.travel;
                bounds.Encapsulate(end);
            }
            return bounds;
        }

        /// <summary>
        /// Har qavatning yo'li ostiga katak-katak maydon yotqiziladi. Pastdagi platformalarga shift
        /// bo'lib qoladigan kataklar tashlab ketiladi. Birinchi qavat ostida orolning o'zi bor.
        /// </summary>
        static List<(Collider deck, int zone)> CatchDecks()
        {
            // Aks holda collider.bounds hali yangilanmagan joyni qaytaradi.
            Physics.SyncTransforms();
            const float cell = 1.5f, reach = 4.2f;
            var result = new List<(Collider, int)>();
            var parent = new GameObject("CatchDecks").transform;
            var material = Mat("CatchDeck", 0.5f, 0.45f, 0.38f);
            var swept = route.Select(Swept).ToArray();

            for (int f = 1; f < floors.Count; f++)
            {
                var floor = floors[f];
                int end = f + 1 < floors.Count ? floors[f + 1].first : route.Count - 1;
                float top = floor.height - CatchDepth;

                var cells = new HashSet<Vector2Int>();
                for (int i = floor.first; i <= end; i++)
                {
                    var area = swept[i];
                    var min = area.min - new Vector3(reach, 0f, reach);
                    var max = area.max + new Vector3(reach, 0f, reach);
                    for (int x = Mathf.FloorToInt(min.x / cell); x <= Mathf.FloorToInt(max.x / cell); x++)
                    for (int z = Mathf.FloorToInt(min.z / cell); z <= Mathf.FloorToInt(max.z / cell); z++)
                    {
                        var point = new Vector3((x + 0.5f) * cell, area.center.y, (z + 0.5f) * cell);
                        var near = area.ClosestPoint(point) - point;
                        if (near.magnitude <= reach) cells.Add(new Vector2Int(x, z));
                    }
                }

                // Shu qavatdan oldingi yo'lning ustida boshi tegadigan joylar ochiq qoladi.
                for (int i = 0; i < floor.first; i++)
                {
                    var step = swept[i];
                    if (step.max.y < top - 4.5f || step.max.y > top + 2.5f) continue;
                    var space = step;
                    space.Expand(new Vector3(2.4f, 0f, 2.4f));
                    cells.RemoveWhere(c => (c.x + 1) * cell > space.min.x && c.x * cell < space.max.x &&
                                           (c.y + 1) * cell > space.min.z && c.y * cell < space.max.z);
                }

                // Kataklar yirik to'rtburchaklarga birlashtiriladi.
                int part = 0;
                while (cells.Count > 0)
                {
                    var start = cells.OrderBy(c => c.y).ThenBy(c => c.x).First();
                    int width = 1, length = 1;
                    while (cells.Contains(start + new Vector2Int(width, 0))) width++;
                    while (Enumerable.Range(0, width).All(i => cells.Contains(start + new Vector2Int(i, length)))) length++;
                    for (int x = 0; x < width; x++)
                    for (int z = 0; z < length; z++)
                        cells.Remove(start + new Vector2Int(x, z));

                    var size = new Vector3(width * cell, 0.35f, length * cell);
                    var center = new Vector3(start.x * cell + size.x * 0.5f, top - size.y * 0.5f,
                        start.y * cell + size.z * 0.5f);
                    var box = TestSceneBuilder.Box(parent, $"Deck_{f:00}_{part++}", center, size, material);
                    result.Add((box.GetComponent<Collider>(), floor.zone));
                }
            }
            Physics.SyncTransforms();
            return result;
        }

        // ---------- Bezak ----------

        /// <summary>
        /// Zonani tanitadigan yirik shakllar. Yo'lga yaqin tushadiganlari qo'yilmaydi.
        /// </summary>
        static void Decorate(List<(Collider deck, int zone)> decks)
        {
            // Aks holda collider.bounds hali yangilanmagan joyni qaytaradi va bezak yo'lga tushib qoladi.
            Physics.SyncTransforms();
            var parent = new GameObject("Scenery").transform;
            var corridor = route.Select(c =>
            {
                var bounds = c.bounds;
                var mover = c.GetComponent<MovingPlatform>();
                if (mover != null)
                {
                    var end = bounds;
                    end.center += mover.travel;
                    bounds.Encapsulate(end);
                }
                // Sakrash yoyi va kamera uchun joy.
                bounds.Expand(new Vector3(7f, 10f, 7f));
                return bounds;
            }).Concat(decks.Select(d =>
            {
                var bounds = d.deck.bounds;
                bounds.Expand(new Vector3(2f, 6f, 2f));
                return bounds;
            })).ToArray();

            var materials = new[]
            {
                Mat("SceneBrick", 0.45f, 0.2f, 0.16f), Mat("SceneTank", 0.4f, 0.36f, 0.24f),
                Mat("SceneSteel", 0.45f, 0.18f, 0.1f), Mat("Rock", 0.42f, 0.4f, 0.38f)
            };

            var random = new System.Random(7);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

            for (int zone = 0; zone < zoneStarts.Count; zone++)
            {
                int first = zoneStarts[zone];
                int last = zone + 1 < zoneStarts.Count ? zoneStarts[zone + 1] : route.Count;
                int placed = 0;
                for (int attempt = 0; attempt < 600 && placed < 22; attempt++)
                {
                    var anchor = route[random.Next(first, last)].bounds.center;
                    var offset = Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward * Range(9f, 22f);
                    var size = zone switch
                    {
                        0 => new Vector3(Range(4f, 8f), Range(7f, 14f), Range(4f, 8f)),
                        1 => new Vector3(Range(1f, 3f), Range(6f, 16f), Range(1f, 3f)),
                        2 => new Vector3(Range(0.5f, 0.9f), Range(10f, 20f), Range(0.5f, 0.9f)),
                        _ => new Vector3(Range(3f, 9f), Range(3f, 7f), Range(3f, 9f))
                    };
                    var position = anchor + offset + Vector3.up * Range(-4f, 3f);
                    if (zone == 0) position.y = Mathf.Max(position.y, size.y * 0.5f);

                    // Burilgan shakl ham sig'ishi uchun diagonal bo'yicha tekshiriladi.
                    float diagonal = new Vector2(size.x, size.z).magnitude * (zone == 3 ? 1.3f : 1f);
                    var space = new Bounds(position, new Vector3(diagonal, size.y * (zone == 3 ? 1.5f : 1f), diagonal));
                    if (corridor.Any(c => c.Intersects(space))) continue;

                    var box = TestSceneBuilder.Box(parent, $"Scenery_{zone}_{placed}", position, size, materials[zone]);
                    box.transform.rotation = zone == 3
                        ? Quaternion.Euler(Range(-18f, 18f), Range(0f, 360f), Range(-18f, 18f))
                        : Quaternion.Euler(0f, Range(0f, 90f), 0f);
                    placed++;
                }
            }
        }
    }
}
