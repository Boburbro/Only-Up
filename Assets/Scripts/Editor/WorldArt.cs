using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OnlyUp.EditorTools
{
    /// <summary>
    /// Asosiy xaritaga ko'rinish beradi. Platformalarning fizik chegaralari o'zgarmaydi,
    /// faqat ustiga tekstura va modellar kiydiriladi.
    /// </summary>
    static class WorldArt
    {
        static Material brick, planks, rust, concrete, plaster, soil, yellow, moving, bounce, gold, dark, trim, steel;
        static Transform art;
        static System.Random random;

        static float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        public static void Apply(List<Collider> route, List<int> zoneStarts, List<(Collider deck, int zone)> decks)
        {
            ArtKit.Reset();
            random = new System.Random(11);
            art = new GameObject("WorldArt").transform;
            CreateMaterials();

            for (int i = 0; i < route.Count; i++)
                DressStep(route[i].gameObject, i, zoneStarts.Count(start => start <= i) - 1);

            foreach (var (deck, zone) in decks) DressDeck(deck.gameObject, zone, route);
            DressScenery();
            BuildStartIsland();
            BuildSea(art);
            DistantRocks();
            var camera = Camera.main;
            Atmosphere.Apply(camera, 0.0021f);
            BuildLandscape(route);
            AddButterflies(Object.FindAnyObjectByType<PlayerMotor>().transform);

            // Dengizga tushgan o'yinchi boshlanish joyiga qaytadi.
            var fall = Object.FindAnyObjectByType<FallReset>();
            fall.MinHeight = -4f;
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(fall);
        }

        static void BuildLandscape(List<Collider> route)
        {
            var land = new GameObject("Landscape").transform;
            var area = GameObject.Find("Ground").GetComponent<Collider>().bounds;
            foreach (var step in route) area.Encapsulate(step.bounds);

            Landscape.Mountains(land, area, 15f, 11);

            // Yo'ldan gorizontal bo'yicha eng yaqin masofa.
            float FromRoute(Vector3 p) => route.Min(s =>
                Vector2.Distance(new Vector2(p.x, p.z), new Vector2(s.bounds.center.x, s.bounds.center.z)));

            // Yo'l yonidan, ma'lum masofadagi bo'sh joy.
            Vector3 Beside(int index, float min, float max, float lift)
            {
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    var p = route[index].bounds.center +
                            Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward * Range(min, max);
                    if (FromRoute(p) < min * 0.8f) continue;
                    return p + Vector3.up * lift;
                }
                return route[index].bounds.center + new Vector3(max * 2f, lift, 0f);
            }

            Landscape.Lighthouse(land, Beside(24, 62f, 90f, 0f));
            Landscape.Boats(land, new Vector3(0f, 0f, 15f), 7, 115f, 230f);

            var balloons = new List<Vector3>();
            for (int i = 8; i < route.Count; i += route.Count / 9)
                balloons.Add(Beside(i, 38f, 70f, Range(-6f, 14f)));
            Landscape.Balloons(land, balloons);

            foreach (var (index, count, speed) in new[] { (20, 7, 7f), (70, 5, -9f), (120, 9, 6f), (170, 5, -8f) })
                Landscape.Flock(land, route[index].bounds.center + new Vector3(0f, 11f, 0f), 34f, count, speed);

            // Bulutlar qatlami: osmon zonasiga chiqqanda o'yinchi ular ustida qoladi.
            var clouds = new List<Vector3>();
            for (int attempt = 0; attempt < 600 && clouds.Count < 34; attempt++)
            {
                var p = new Vector3(area.center.x + Range(-230f, 230f), Range(92f, 108f),
                    area.center.z + Range(-230f, 230f));
                if (FromRoute(p) < 34f || clouds.Any(c => Vector3.Distance(c, p) < 40f)) continue;
                clouds.Add(p);
            }
            Landscape.Clouds(land, clouds);
        }

        static void CreateMaterials()
        {
            brick = ArtKit.Tiled("Brick", "red_brick_03", new Color(1f, 0.93f, 0.9f), 2.2f);
            planks = ArtKit.Tiled("Planks", "weathered_brown_planks", Color.white, 2f);
            rust = ArtKit.Tiled("RustySteel", "rusty_metal_02", new Color(0.95f, 0.8f, 0.72f), 1.6f, 0.35f, 0.35f);
            concrete = ArtKit.Tiled("ConcreteArt", "concrete_floor_worn_001", Color.white, 2.5f);
            plaster = ArtKit.Tiled("Plaster", "painted_plaster_wall", new Color(0.92f, 0.86f, 0.76f), 2.5f);
            soil = ArtKit.Tiled("Soil", "forrest_ground_01", new Color(0.9f, 0.85f, 0.78f), 3f, 0.05f);
            yellow = ArtKit.Tiled("YellowPipe", "rusty_metal_02", new Color(0.88f, 0.7f, 0.26f), 1.4f, 0.4f, 0.25f);
            moving = ArtKit.Tiled("BlueSteel", "rusty_metal_02", new Color(0.3f, 0.62f, 1f), 1.6f, 0.4f, 0.3f);
            bounce = ArtKit.Flat("BounceArt", new Color(0.12f, 0.75f, 0.28f), 0.5f);
            gold = ArtKit.Tiled("GoldDeck", "concrete_floor_worn_001", new Color(1f, 0.76f, 0.24f), 1.6f, 0.55f, 0.55f);
            steel = ArtKit.Tiled("PipeSteel", "rusty_metal_02", new Color(0.5f, 0.57f, 0.62f), 1.6f, 0.4f, 0.35f);
            dark = ArtKit.Flat("DarkMetal", new Color(0.09f, 0.09f, 0.1f), 0.4f, 0.4f);
            trim = ArtKit.Tiled("Trim", "painted_plaster_wall", new Color(0.7f, 0.66f, 0.6f), 2f);
        }

        // ---------- Yo'ldagi platformalar ----------

        static void DressStep(GameObject box, int index, int zone)
        {
            string kind = box.name.Substring(box.name.IndexOf('_') + 1);
            var t = box.transform;
            Vector3 size = t.localScale;
            Vector3 center = t.position;
            Quaternion rotation = t.rotation;
            float top = center.y + size.y * 0.5f;

            switch (kind)
            {
                case "Crate":
                case "Box":
                {
                    box.GetComponent<MeshRenderer>().enabled = false;
                    string model = index % 2 == 0 ? "wooden_crate_02" : "wooden_crate_01";
                    ArtKit.Fit(art, model, null, center, size, rotation);
                    break;
                }
                case "Barrel":
                {
                    box.GetComponent<MeshRenderer>().enabled = false;
                    string model = index % 2 == 0 ? "barrel_03" : "Barrel_01";
                    // Fizik chegara kvadrat, bochka dumaloq: burchaklar ko'rinishi uchun biroz kattaroq.
                    ArtKit.Fit(art, model, null, center, Vector3.Scale(size, new Vector3(1.12f, 1f, 1.12f)), rotation);
                    break;
                }
                case "Rock":
                case "FarRock":
                {
                    box.GetComponent<MeshRenderer>().enabled = false;
                    // Yassiroq toshlar: tepasi yurish sirtiga mos keladi, pasti esa chuqurroq.
                    string[] parts = { "rock07", "rock08", "rock09", "rock10", "rock12" };
                    float depth = Mathf.Max(size.y * 1.6f, 1.1f);
                    var rockSize = new Vector3(size.x * 1.22f, depth, size.z * 1.22f);
                    ArtKit.Fit(art, "rock_moss_set_02", parts[random.Next(parts.Length)],
                        new Vector3(center.x, top + 0.07f - depth * 0.5f, center.z), rockSize, rotation);
                    break;
                }
                case "Pipe":
                {
                    box.GetComponent<MeshRenderer>().enabled = false;
                    Tube(art, new Vector3(center.x, top - size.x * 0.5f, center.z), size.x, size.z,
                        rotation * Quaternion.Euler(90f, 0f, 0f), yellow);
                    for (int end = -1; end <= 1; end += 2)
                        Tube(art, center + rotation * new Vector3(0f, top - center.y - size.x * 0.5f, end * (size.z * 0.5f - 0.08f)),
                            size.x * 1.18f, 0.16f, rotation * Quaternion.Euler(90f, 0f, 0f), dark);
                    break;
                }
                case "Beam":
                    ArtKit.Skin(box, yellow);
                    break;
                case "Girder":
                case "Scaffold":
                    ArtKit.Skin(box, rust);
                    // I-balka ko'rinishi: ostida ingichka devor va pastki tokcha.
                    ArtKit.Block(art, "Web", center + Vector3.down * (size.y * 0.5f + 0.13f),
                        new Vector3(size.x * 0.3f, 0.26f, size.z * 0.96f), rotation, rust);
                    ArtKit.Block(art, "Flange", center + Vector3.down * (size.y * 0.5f + 0.3f),
                        new Vector3(size.x * 0.85f, 0.08f, size.z * 0.96f), rotation, rust);
                    break;
                case "Slab":
                    ArtKit.Skin(box, concrete);
                    break;
                case "Balcony":
                    ArtKit.Skin(box, plaster);
                    ArtKit.Block(art, "Support", center + Vector3.down * (size.y * 0.5f + 0.16f),
                        new Vector3(size.x * 0.7f, 0.32f, size.z * 0.5f), rotation, brick);
                    break;
                case "Ledge":
                    ArtKit.Skin(box, zone >= 3 ? concrete : zone == 1 ? rust : brick);
                    // Och rangli qirra: osilish mumkinligini ko'rsatadi.
                    foreach (int side in new[] { -1, 1 })
                        ArtKit.Block(art, "Lip", center + rotation * new Vector3(0f, size.y * 0.5f - 0.06f, side * (size.z * 0.5f + 0.015f)),
                            new Vector3(size.x + 0.04f, 0.12f, 0.03f), rotation, trim);
                    break;
                case "Rest":
                case "Landing":
                case "Runway":
                    ArtKit.Skin(box, zone == 3 ? soil : planks);
                    if (zone == 3)
                    {
                        float depth = 2.2f;
                        ArtKit.Fit(art, "rock_moss_set_01", "rock03",
                            new Vector3(center.x, center.y - size.y * 0.5f - depth * 0.5f + 0.25f, center.z),
                            new Vector3(size.x * 1.05f, depth, size.z * 1.05f), rotation);
                    }
                    break;
                case "Trampoline":
                    ArtKit.Skin(box, bounce);
                    ArtKit.Block(art, "Frame", center + Vector3.down * 0.22f,
                        new Vector3(size.x + 0.15f, 0.14f, size.z + 0.15f), rotation, dark);
                    break;
                case "Bridge":
                case "Lift":
                case "Spinner":
                    ArtKit.Skin(box, moving);
                    break;
                case "Finish":
                    ArtKit.Skin(box, gold);
                    Flag(new Vector3(center.x, top, center.z) + rotation * new Vector3(2.2f, 0f, 2.2f));
                    break;
            }
        }

        // ---------- Ushlab qoluvchi maydonlar ----------

        /// <summary>Har zonada o'z ko'rinishi: bino tomi, taxta supa, beton plita, qoya ustidagi yer.</summary>
        static void DressDeck(GameObject deck, int zone, List<Collider> route)
        {
            var surface = zone switch { 0 => concrete, 1 => planks, 2 => rust, _ => soil };
            ArtKit.Skin(deck, surface);
            Vector3 size = deck.transform.localScale, center = deck.transform.position;

            // Tagidagi to'sinlar yoki qoya. Pastdagi yo'lga xalaqit beradigan joyga qo'yilmaydi.
            float depth = zone == 3 ? Mathf.Min(3f, Mathf.Min(size.x, size.z) * 0.7f) : 0.4f;
            if (Mathf.Min(size.x, size.z) < 2.9f || (zone != 3 && Mathf.Max(size.x, size.z) < 5f)) return;
            var under = new Bounds(center + Vector3.down * (size.y * 0.5f + depth * 0.5f),
                new Vector3(size.x, depth, size.z));
            var space = under;
            space.Expand(new Vector3(1f, 5f, 1f));
            if (route.Any(step => step.bounds.max.y < center.y - 0.5f && space.Intersects(step.bounds))) return;

            if (zone == 3)
            {
                string[] parts = { "rock01", "rock02", "rock04", "rock05", "rock06" };
                ArtKit.Fit(art, "rock_moss_set_01", parts[random.Next(parts.Length)], under.center, under.size,
                    Quaternion.identity);
                return;
            }
            bool alongX = size.x > size.z;
            var beam = zone == 0 ? brick : rust;
            for (int side = -1; side <= 1; side += 2)
            {
                var offset = alongX
                    ? new Vector3(0f, 0f, side * (size.z * 0.5f - 0.35f))
                    : new Vector3(side * (size.x * 0.5f - 0.35f), 0f, 0f);
                var beamSize = alongX
                    ? new Vector3(size.x - 0.2f, depth, 0.3f)
                    : new Vector3(0.3f, depth, size.z - 0.2f);
                ArtKit.Block(art, "DeckBeam", under.center + offset, beamSize, Quaternion.identity, beam);
            }
        }

        static void Tube(Transform parent, Vector3 center, float diameter, float length, Quaternion rotation,
            Material material)
        {
            var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(tube.GetComponent<Collider>());
            tube.transform.SetParent(parent, false);
            tube.transform.SetPositionAndRotation(center, rotation);
            tube.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
            tube.GetComponent<Renderer>().sharedMaterial = material;
            tube.isStatic = true;
        }

        static void Flag(Vector3 position)
        {
            Tube(art, position + Vector3.up * 1.8f, 0.12f, 3.6f, Quaternion.identity, dark);
            ArtKit.Block(art, "FlagCloth", position + new Vector3(0.65f, 3.15f, 0f), new Vector3(1.2f, 0.75f, 0.05f),
                Quaternion.identity, ArtKit.Flat("FlagRed", new Color(0.85f, 0.1f, 0.08f), 0.2f));
        }

        // ---------- Atrofdagi shakllar ----------

        static void DressScenery()
        {
            var items = GameObject.Find("Scenery").transform.Cast<Transform>().ToArray();
            string[] bigRocks = { "rock01", "rock02", "rock04", "rock05", "rock06" };

            foreach (var item in items)
            {
                int zone = item.name.Split('_')[1][0] - '0';
                int number = int.Parse(item.name.Split('_')[2]);
                Vector3 size = item.localScale, center = item.position;
                var go = item.gameObject;

                switch (zone)
                {
                    case 0:
                        ArtKit.Skin(go, brick);
                        Building(center, size, item.rotation);
                        break;
                    case 1:
                    {
                        // Mo'ri va suv quvurlari.
                        go.GetComponent<MeshRenderer>().enabled = false;
                        float diameter = Mathf.Min(size.x, size.z);
                        Tube(art, center, diameter, size.y, item.rotation, number % 3 == 0 ? rust : steel);
                        for (float y = -0.4f; y <= 0.41f; y += 0.4f)
                            Tube(art, center + Vector3.up * size.y * y, diameter * 1.15f, 0.22f, item.rotation, dark);
                        if (number % 2 == 0)
                        {
                            string part = $"pipe0{1 + number % 7}";
                            ArtKit.Fit(art, "modular_industrial_pipes_01", part,
                                center + item.rotation * new Vector3(0f, 0f, diameter * 0.5f + 0.28f),
                                new Vector3(1.2f, 3f, 0.5f), item.rotation);
                        }
                        break;
                    }
                    case 2:
                        ArtKit.Skin(go, rust);
                        for (float y = -0.35f; y <= 0.36f; y += 0.35f)
                            ArtKit.Block(art, "CrossBeam", center + Vector3.up * size.y * y,
                                new Vector3(3.2f, 0.22f, 0.22f), item.rotation, rust);
                        break;
                    default:
                    {
                        go.GetComponent<MeshRenderer>().enabled = false;
                        Object.DestroyImmediate(go.GetComponent<Collider>());
                        var visual = ArtKit.Fit(art, "rock_moss_set_01", bigRocks[random.Next(bigRocks.Length)],
                            center, size, item.rotation);
                        // Yiqilgan o'yinchi tosh shakliga qo'nadi, ko'rinmas qutiga emas.
                        foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
                            filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                        break;
                    }
                }
            }
        }

        /// <summary>G'isht binoga karniz, derazalar va konditsionerlar qo'shadi.</summary>
        static void Building(Vector3 center, Vector3 size, Quaternion rotation)
        {
            float bottom = center.y - size.y * 0.5f;
            ArtKit.Block(art, "Roof", center + Vector3.up * (size.y * 0.5f + 0.12f),
                new Vector3(size.x + 0.35f, 0.24f, size.z + 0.35f), rotation, trim);

            for (float y = bottom + 3.4f; y < bottom + size.y - 1f; y += 3.4f)
                ArtKit.Block(art, "Cornice", new Vector3(center.x, y, center.z),
                    new Vector3(size.x + 0.14f, 0.16f, size.z + 0.14f), rotation, plaster);

            for (int face = 0; face < 4; face++)
            {
                var turn = rotation * Quaternion.Euler(0f, face * 90f, 0f);
                float width = face % 2 == 0 ? size.x : size.z;
                float depth = face % 2 == 0 ? size.z : size.x;
                int columns = Mathf.FloorToInt(width / 2.4f);
                for (int column = 0; column < columns; column++)
                for (float y = bottom + 1.7f; y < bottom + size.y - 1f; y += 3.4f)
                {
                    if (Range(0f, 1f) < 0.35f) continue;
                    float x = (column - (columns - 1) * 0.5f) * (width / columns);
                    Vector3 position = new Vector3(center.x, y, center.z) + turn * new Vector3(x, 0f, depth * 0.5f + 0.115f);
                    ArtKit.Fit(art, "rollershutter_window_01", "rollershutter_window_01", position,
                        new Vector3(1.5f, 1.45f, 0.2f), turn);
                    if (Range(0f, 1f) < 0.1f)
                        ArtKit.Fit(art, "exterior_aircon_unit", "exterior_aircon_unit",
                            position + turn * new Vector3(0f, -1.25f, 0.11f), new Vector3(0.9f, 1f, 0.42f), turn);
                }
            }
        }

        // ---------- Tabiat ----------

        static void BuildStartIsland()
        {
            var ground = GameObject.Find("Ground");
            ArtKit.Skin(ground, soil);
            Vector3 size = ground.transform.localScale, center = ground.transform.position;

            // Orol chetidagi qoyalar: dengizga tik tushadi.
            string[] parts = { "rock01", "rock02", "rock04", "rock06" };
            for (int side = 0; side < 4; side++)
            for (float along = -0.5f; along <= 0.5f; along += 0.1f)
            {
                var turn = Quaternion.Euler(0f, side * 90f, 0f);
                Vector3 offset = turn * new Vector3(along * size.x, 0f, size.z * 0.5f + 1.5f);
                ArtKit.Fit(art, "rock_moss_set_01", parts[random.Next(parts.Length)],
                    new Vector3(center.x + offset.x, -4.2f + Range(-0.6f, 0.4f), center.z + offset.z),
                    new Vector3(Range(15f, 19f), Range(7f, 8.5f), Range(7f, 10f)),
                    turn * Quaternion.Euler(0f, Range(-14f, 14f), 0f));
            }

            Flowers(ArtKit.Flat("PetalWhite", new Color(0.95f, 0.93f, 0.9f), 0.1f), "FlowersWhite", 520);
            Flowers(ArtKit.Flat("PetalPink", new Color(0.9f, 0.42f, 0.58f), 0.1f), "FlowersPink", 160);

            // Butalar va mayda o'simliklar.
            string[] shrubs = { "shrub_04_a_LOD0", "shrub_04_b_LOD0", "shrub_04_c_LOD0" };
            for (int i = 0; i < 26; i++)
            {
                Vector3 p = GroundSpot(6f, 26f);
                float scale = Range(0.9f, 1.7f);
                ArtKit.Fit(art, "shrub_04", shrubs[random.Next(shrubs.Length)], p + Vector3.up * scale * 0.5f,
                    new Vector3(scale, scale, scale), Quaternion.Euler(0f, Range(0f, 360f), 0f));
            }
            for (int i = 0; i < 60; i++)
            {
                Vector3 p = GroundSpot(2f, 18f);
                float scale = Range(0.2f, 0.38f);
                ArtKit.Fit(art, "shrub_sorrel_01", "_a", p + Vector3.up * scale * 0.5f,
                    new Vector3(scale, scale, scale), Quaternion.Euler(0f, Range(0f, 360f), 0f));
            }
        }

        static Vector3 GroundSpot(float min, float max)
        {
            var direction = Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward;
            Vector3 p = direction * Range(min, max);
            return new Vector3(p.x, 0f, p.z + 6f);
        }

        /// <summary>Yuzlab gul bitta meshga yig'iladi, shuning uchun bitta chizish buyrug'i ketadi.</summary>
        static void Flowers(Material petal, string name, int count)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < count; i++)
            {
                // To'da-to'da o'sadi.
                Vector3 patch = GroundSpot(1.5f, 20f);
                Vector3 p = patch + new Vector3(Range(-0.9f, 0.9f), 0f, Range(-0.9f, 0.9f));
                float height = Range(0.12f, 0.3f), radius = Range(0.07f, 0.12f);
                int start = vertices.Count;
                vertices.Add(p + Vector3.up * height);
                for (int k = 0; k < 10; k++)
                {
                    float angle = k * 36f;
                    float r = k % 2 == 0 ? radius : radius * 0.35f;
                    vertices.Add(p + Vector3.up * (height + (k % 2 == 0 ? 0.018f : 0f)) +
                                 Quaternion.Euler(0f, angle, 0f) * Vector3.forward * r);
                }
                for (int k = 0; k < 10; k++)
                    triangles.AddRange(new[] { start, start + 1 + k, start + 1 + (k + 1) % 10 });
            }
            var mesh = ArtKit.SaveMesh(name, () =>
            {
                var flowers = new Mesh();
                flowers.SetVertices(vertices);
                flowers.SetTriangles(triangles, 0);
                flowers.RecalculateNormals();
                return flowers;
            });
            var go = ArtKit.MeshObject(art, name, mesh, petal, Vector3.zero, Quaternion.identity);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public static void BuildSea(Transform parent)
        {
            var water = ArtKit.Flat("Sea", new Color(0.08f, 0.24f, 0.29f), 0.82f);
            string normalPath = WaveNormal();
            var waves = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            water.SetTexture("_BumpMap", waves);
            water.SetFloat("_BumpScale", 0.2f);
            water.EnableKeyword("_NORMALMAP");
            // URP'da normal xaritaning takrorlanishi asosiy tekstura sozlamasidan olinadi.
            water.SetTextureScale("_BaseMap", new Vector2(1f / 37f, 1f / 37f));

            var sea = ArtKit.Block(parent, "Sea", new Vector3(0f, -7f, 15f), new Vector3(4000f, 0.2f, 4000f),
                Quaternion.identity, water);
            sea.isStatic = false;
            sea.AddComponent<SeaDrift>();
        }

        /// <summary>Choksiz takrorlanadigan to'lqin normal xaritasi.</summary>
        static string WaveNormal()
        {
            const int size = 256;
            string path = $"{ArtKit.Generated}/SeaNormal.png";
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            // Butun sonli to'lqinlar yig'indisi chetlarda o'zi bilan ulanadi.
            var waves = new[]
            {
                new Vector3(3, 1, 0.8f), new Vector3(-2, 5, 0.6f), new Vector3(7, -4, 0.45f), new Vector3(11, 9, 0.35f),
                new Vector3(-13, -6, 0.3f), new Vector3(5, 17, 0.25f), new Vector3(-23, 14, 0.2f), new Vector3(19, -27, 0.15f)
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = 0f, dy = 0f;
                foreach (var w in waves)
                {
                    float slope = Mathf.Cos((x * w.x + y * w.y) * 2f * Mathf.PI / size) * w.z;
                    float length = Mathf.Sqrt(w.x * w.x + w.y * w.y);
                    dx += slope * w.x / length;
                    dy += slope * w.y / length;
                }
                Vector3 n = new Vector3(-dx * 0.35f, -dy * 0.35f, 1f).normalized;
                texture.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f));
            }
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            UnityEditor.AssetDatabase.ImportAsset(path);
            var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
            importer.textureType = UnityEditor.TextureImporterType.NormalMap;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return path;
        }

        /// <summary>Ufqdagi uchib turgan katta qoyalar (rasmdagidek).</summary>
        static void DistantRocks()
        {
            string[] parts = { "rock01", "rock02", "rock04", "rock05", "rock06" };
            for (int i = 0; i < 9; i++)
            {
                var direction = Quaternion.Euler(0f, i * 40f + Range(-14f, 14f), 0f) * Vector3.forward;
                float scale = Range(18f, 48f);
                var position = direction * Range(180f, 340f) + Vector3.up * Range(15f, 150f);
                var rock = ArtKit.Fit(art, "rock_moss_set_01", parts[random.Next(parts.Length)], position,
                    new Vector3(scale, scale * Range(0.6f, 1.2f), scale * 0.8f),
                    Quaternion.Euler(Range(-12f, 12f), Range(0f, 360f), Range(-12f, 12f)));
                foreach (var renderer in rock.GetComponentsInChildren<MeshRenderer>())
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        public static void AddButterflies(Transform follow, int count = 36, float radius = 11f, float scale = 1f)
        {
            var mesh = ArtKit.SaveMesh("Wing", () =>
            {
                var wing = new Mesh
                {
                    vertices = new[]
                    {
                        new Vector3(0f, 0f, 0.02f), new Vector3(0.09f, 0f, 0.07f), new Vector3(0.075f, 0f, 0.005f),
                        new Vector3(0.06f, 0f, -0.055f), new Vector3(0f, 0f, -0.025f)
                    },
                    // Ikki tomondan ham ko'rinishi uchun uchburchaklar ikki marta.
                    triangles = new[] { 0, 1, 2, 0, 2, 4, 4, 2, 3, 2, 1, 0, 4, 2, 0, 3, 2, 4 }
                };
                wing.RecalculateNormals();
                return wing;
            });

            var material = ArtKit.Flat("ButterflyBlue", new Color(0.7f, 0.86f, 1f), 0.2f);
            material.SetColor("_EmissionColor", new Color(0.28f, 0.42f, 0.6f));
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            var butterflies = new GameObject("Butterflies").AddComponent<Butterflies>();
            butterflies.follow = follow;
            butterflies.wing = mesh;
            butterflies.material = material;
            butterflies.count = count;
            butterflies.radius = radius;
            butterflies.wingScale = scale;
        }
    }
}
