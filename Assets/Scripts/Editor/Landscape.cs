using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OnlyUp.EditorTools
{
    /// <summary>
    /// Atrofdagi manzara: qorli tog'lar va archazor, mayoq, qayiqlar, havo sharlari, qushlar va bulutlar.
    /// Hammasi kod bilan yasaladi. Hech birida fizik chegara yo'q.
    /// </summary>
    static class Landscape
    {
        const float SeaLevel = -6.9f;
        // Tog' shakli birlik o'lchamda yasaladi, keyin har joyda o'z o'lchamiga cho'ziladi.
        const float BaseRadius = 100f, BaseHeight = 100f;
        const int Variants = 4;

        static System.Random random;
        static float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        // ---------- Tog'lar ----------

        /// <summary>area atrofida halqa bo'lib turadigan tog'lar. clearance: area chetidan eng kam masofa.</summary>
        public static void Mountains(Transform parent, Bounds area, float clearance, int count)
        {
            random = new System.Random(23);
            var shapes = Enumerable.Range(0, Variants).Select(MountainShape).ToArray();
            var pine = ArtKit.Flat("Pine", new Color(0.09f, 0.19f, 0.11f), 0.05f);
            var root = new GameObject("Mountains").transform;
            root.SetParent(parent, false);

            for (int ring = 0; ring < 2; ring++)
            for (int i = 0; i < count; i++)
            {
                float angle = (i + ring * 0.5f) * 360f / count + Range(-8f, 8f);
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                float radius = ring == 0 ? Range(60f, 105f) : Range(125f, 200f);
                float height = ring == 0 ? Range(40f, 85f) : Range(95f, 175f);
                // To'g'ri to'rtburchak chetigacha bo'lgan masofa.
                float edge = Mathf.Min(
                    area.extents.x / Mathf.Max(Mathf.Abs(direction.x), 0.001f),
                    area.extents.z / Mathf.Max(Mathf.Abs(direction.z), 0.001f));
                float distance = edge + radius + clearance + (ring == 0 ? Range(0f, 40f) : Range(150f, 230f));
                var position = new Vector3(area.center.x, SeaLevel, area.center.z) + direction * distance;

                var shape = shapes[random.Next(shapes.Length)];
                var holder = new GameObject($"Mountain_{ring}_{i}").transform;
                holder.SetParent(root, false);
                holder.SetPositionAndRotation(position, Quaternion.Euler(0f, Range(0f, 360f), 0f));
                holder.localScale = new Vector3(radius / BaseRadius, height / BaseHeight, radius / BaseRadius);
                NoShadow(Child(holder, "Slopes", shape.ground, shape.paint));
                if (ring == 0) NoShadow(Child(holder, "Forest", shape.forest, pine));
            }
        }

        static (Mesh ground, Material paint, Mesh forest) MountainShape(int variant)
        {
            float offsetX = 13.7f * variant + 3.1f, offsetY = 7.3f * variant + 11.9f;

            float Ridged(float x, float y)
            {
                float sum = 0f, weight = 0.6f, frequency = 1f;
                for (int octave = 0; octave < 3; octave++)
                {
                    // PerlinNoise 0..1 dan biroz chiqishi mumkin: manfiy sonning darajasi NaN bo'lib, tog'da teshik ochadi.
                    float n = Mathf.Clamp01(1f - Mathf.Abs(Mathf.PerlinNoise(x * frequency, y * frequency) * 2f - 1f));
                    sum += Mathf.Pow(n, 1.4f) * weight;
                    weight *= 0.42f;
                    frequency *= 2.1f;
                }
                return sum;
            }

            // u, v: -1..1. Qaytadigan qiymat dengiz sathidan balandlik (0..~100).
            float Height(float u, float v)
            {
                float wobble = (Mathf.PerlinNoise(u * 1.7f + offsetY, v * 1.7f + offsetX) - 0.5f) * 0.5f;
                float distance = Mathf.Sqrt(u * u + v * v) + wobble;
                float falloff = Mathf.Clamp01(1f - distance);
                if (falloff <= 0f) return -3f;
                float ridge = Ridged(u * 1.6f + offsetX, v * 1.6f + offsetY);
                return -3f + BaseHeight * Mathf.Pow(falloff, 1.15f) * (0.55f + 0.65f * ridge) +
                       Mathf.Min(falloff * 25f, 4f);
            }

            Vector3 Normal(float u, float v)
            {
                const float e = 0.015f;
                float dx = (Height(u + e, v) - Height(u - e, v)) / (2f * e * BaseRadius);
                float dz = (Height(u, v + e) - Height(u, v - e)) / (2f * e * BaseRadius);
                return new Vector3(-dx, 1f, -dz).normalized;
            }

            const int cells = 64;
            var ground = ArtKit.SaveMesh($"Mountain_{variant}", () =>
            {
                var vertices = new Vector3[(cells + 1) * (cells + 1)];
                var uvs = new Vector2[vertices.Length];
                var triangles = new List<int>();
                for (int z = 0; z <= cells; z++)
                for (int x = 0; x <= cells; x++)
                {
                    int i = z * (cells + 1) + x;
                    float u = x * 2f / cells - 1f, v = z * 2f / cells - 1f;
                    vertices[i] = new Vector3(u * BaseRadius, Height(u, v), v * BaseRadius);
                    uvs[i] = new Vector2((float)x / cells, (float)z / cells);
                }
                for (int z = 0; z < cells; z++)
                for (int x = 0; x < cells; x++)
                {
                    int a = z * (cells + 1) + x, b = a + 1, c = a + cells + 1, d = c + 1;
                    // Butunlay suv ostidagi uchburchaklar tashlab yuboriladi.
                    if (Mathf.Max(vertices[a].y, vertices[b].y, vertices[c].y, vertices[d].y) < -2.5f) continue;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
                var mesh = new Mesh { vertices = vertices, uv = uvs };
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                return mesh;
            });

            // Rang balandlik va qiyalikka qarab bo'yaladi: qum, o't, qoya, qor.
            var texture = Painting($"MountainPaint_{variant}", 512, 512, (px, py) =>
            {
                float u = px * 2f - 1f, v = py * 2f - 1f;
                float height = Height(u, v);
                float flat = Normal(u, v).y;
                float patch = Mathf.PerlinNoise(u * 9f + offsetX, v * 9f + offsetY);
                float grain = Mathf.PerlinNoise(u * 47f + offsetY, v * 47f + offsetX);

                var rock = Color.Lerp(new Color(0.25f, 0.24f, 0.23f), new Color(0.44f, 0.41f, 0.38f), patch);
                var grass = Color.Lerp(new Color(0.11f, 0.23f, 0.09f), new Color(0.27f, 0.36f, 0.13f), patch);
                var color = rock;
                float green = Mathf.InverseLerp(60f, 42f, height + (patch - 0.5f) * 26f) *
                              Mathf.InverseLerp(0.38f, 0.6f, flat);
                color = Color.Lerp(color, grass, green);
                color = Color.Lerp(color, new Color(0.62f, 0.52f, 0.36f), Mathf.InverseLerp(4f, 1.5f, height));
                float snow = Mathf.InverseLerp(58f, 68f, height + (patch - 0.5f) * 24f) *
                             Mathf.InverseLerp(0.38f, 0.62f, flat + (grain - 0.5f) * 0.3f);
                color = Color.Lerp(color, new Color(0.93f, 0.93f, 0.96f), snow);
                color *= 0.86f + grain * 0.28f;
                color.a = 1f;
                return color;
            }, TextureWrapMode.Clamp, false);
            var paint = ArtKit.Flat($"MountainArt_{variant}", Color.white, 0.04f);
            paint.SetTexture("_BaseMap", texture);

            var forest = ArtKit.SaveMesh($"Forest_{variant}", () =>
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                var seeds = new System.Random(100 + variant);
                float Next() => (float)seeds.NextDouble();
                int planted = 0;
                for (int attempt = 0; attempt < 20000 && planted < 650; attempt++)
                {
                    float u = Next() * 2f - 1f, v = Next() * 2f - 1f;
                    float height = Height(u, v);
                    if (height < 3.5f || height > 36f || Normal(u, v).y < 0.6f) continue;
                    // To'da-to'da o'sadi.
                    if (Mathf.PerlinNoise(u * 5f + offsetY, v * 5f + offsetX) < 0.45f) continue;
                    var foot = new Vector3(u * BaseRadius, height - 0.6f, v * BaseRadius);
                    float size = 2.6f + Next() * 2.4f;
                    Cone(vertices, triangles, foot + Vector3.up * size * 0.15f, size * 0.42f, size * 0.85f, 6);
                    Cone(vertices, triangles, foot + Vector3.up * size * 0.6f, size * 0.3f, size * 0.75f, 6);
                    planted++;
                }
                var mesh = new Mesh();
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                return mesh;
            });

            return (ground, paint, forest);
        }

        static void Cone(List<Vector3> vertices, List<int> triangles, Vector3 foot, float radius, float height,
            int sides)
        {
            for (int k = 0; k < sides; k++)
            {
                int start = vertices.Count;
                var a = Quaternion.Euler(0f, k * 360f / sides, 0f) * Vector3.forward * radius;
                var b = Quaternion.Euler(0f, (k + 1) * 360f / sides, 0f) * Vector3.forward * radius;
                vertices.Add(foot + a);
                vertices.Add(foot + Vector3.up * height);
                vertices.Add(foot + b);
                triangles.AddRange(new[] { start, start + 2, start + 1 });
            }
        }

        // ---------- Mayoq ----------

        public static void Lighthouse(Transform parent, Vector3 position)
        {
            var root = new GameObject("Lighthouse").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(position.x, SeaLevel, position.z);

            ArtKit.Fit(root, "rock_moss_set_01", "rock01", root.position + Vector3.up * 1.5f,
                new Vector3(26f, 9f, 22f), Quaternion.Euler(0f, 35f, 0f));

            var white = ArtKit.Flat("TowerWhite", new Color(0.9f, 0.87f, 0.8f), 0.15f);
            var red = ArtKit.Flat("TowerRed", new Color(0.68f, 0.12f, 0.09f), 0.2f);
            var dark = ArtKit.Flat("DarkMetal", new Color(0.09f, 0.09f, 0.1f), 0.4f, 0.4f);
            float y = 5f;
            for (int i = 0; i < 6; i++)
            {
                Cylinder(root, new Vector3(0f, y + 2f, 0f), 6.4f - i * 0.5f, 4f, i % 2 == 0 ? white : red);
                y += 4f;
            }
            Cylinder(root, new Vector3(0f, y + 0.2f, 0f), 5f, 0.4f, dark);
            Cylinder(root, new Vector3(0f, y + 1.6f, 0f), 2.6f, 2.4f,
                Glow("LampGlow", new Color(1f, 0.85f, 0.45f), 3f));
            Cylinder(root, new Vector3(0f, y + 3.1f, 0f), 3.6f, 0.6f, red);

            // Aylanadigan nur: ikki tomonga qaragan yarim shaffof konus.
            var beam = new GameObject("Beam").transform;
            beam.SetParent(root, false);
            beam.localPosition = new Vector3(0f, y + 1.6f, 0f);
            beam.gameObject.AddComponent<Spinning>().degreesPerSecond = 35f;
            var mesh = ArtKit.SaveMesh("LightBeam", () =>
            {
                var vertices = new List<Vector3>();
                var uvs = new List<Vector2>();
                var triangles = new List<int>();
                // Kesishgan ikki yassi uchburchak, har biri ikkala tomonga.
                foreach (float direction in new[] { 1f, -1f })
                foreach (var side in new[] { Vector3.up, Vector3.right })
                {
                    int start = vertices.Count;
                    vertices.Add(Vector3.zero);
                    vertices.Add(Vector3.forward * 55f * direction + side * 4f);
                    vertices.Add(Vector3.forward * 55f * direction - side * 4f);
                    uvs.AddRange(new[] { new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
                    triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 1 });
                }
                var m = new Mesh();
                m.SetVertices(vertices);
                m.SetUVs(0, uvs);
                m.SetTriangles(triangles, 0);
                m.RecalculateNormals();
                return m;
            });
            var fade = Painting("BeamFade", 64, 16, (u, v) =>
                new Color(1f, 1f, 1f, (1f - u) * (1f - Mathf.Abs(v - 0.5f) * 1.6f)), TextureWrapMode.Clamp, true);
            NoShadow(Child(beam, "Light", mesh,
                Transparent("BeamArt", fade, new Color(1f, 0.9f, 0.55f, 0.28f)), false));
        }

        // ---------- Qayiqlar ----------

        public static void Boats(Transform parent, Vector3 center, int count, float minRadius, float maxRadius)
        {
            random = new System.Random(31);
            var wood = ArtKit.Tiled("Planks", "weathered_brown_planks", Color.white, 2f);
            var cloth = ArtKit.Flat("SailWhite", new Color(0.92f, 0.88f, 0.78f), 0.1f);
            var stripe = ArtKit.Flat("SailRed", new Color(0.75f, 0.2f, 0.12f), 0.1f);
            var hull = ArtKit.SaveMesh("Hull", HullMesh);
            var sail = ArtKit.SaveMesh("Sail", () => Triangle(
                new Vector3(0f, 1.6f, 0.1f), new Vector3(0f, 8.2f, 0.1f), new Vector3(0f, 1.6f, -3.4f)));
            var jib = ArtKit.SaveMesh("Jib", () => Triangle(
                new Vector3(0f, 1.6f, 0.4f), new Vector3(0f, 6.8f, 0.3f), new Vector3(0f, 1.4f, 3.8f)));

            for (int i = 0; i < count; i++)
            {
                var boat = new GameObject($"Boat_{i}").transform;
                boat.SetParent(parent, false);
                Child(boat, "Hull", hull, wood, false);
                var mast = Cylinder(boat, new Vector3(0f, 4.6f, 0.25f), 0.22f, 7.6f, wood);
                mast.isStatic = false;
                Child(boat, "Sail", sail, i % 2 == 0 ? cloth : stripe, false);
                Child(boat, "Jib", jib, cloth, false);

                var mover = boat.gameObject.AddComponent<Cruiser>();
                mover.center = new Vector3(center.x, SeaLevel + 0.25f, center.z);
                mover.radius = Mathf.Lerp(minRadius, maxRadius, (i + Range(0.1f, 0.9f)) / count);
                mover.startAngle = i * 360f / count + Range(-20f, 20f);
                mover.degreesPerSecond = (i % 2 == 0 ? 1f : -1f) * Range(0.5f, 1.2f);
                mover.bobHeight = 0.18f;
                mover.roll = 4f;
                boat.localScale = Vector3.one * Range(1.1f, 1.7f);
                boat.position = mover.center +
                                Quaternion.Euler(0f, mover.startAngle, 0f) * Vector3.forward * mover.radius;
            }
        }

        /// <summary>Uchi o'tkir qayiq korpusi (uzunligi +Z bo'ylab).</summary>
        static Mesh HullMesh()
        {
            var deck = new[]
            {
                new Vector3(0f, 1.1f, 4.6f), new Vector3(1.5f, 0.95f, 1.8f), new Vector3(1.5f, 0.95f, -2.2f),
                new Vector3(0.9f, 1.05f, -3.8f), new Vector3(-0.9f, 1.05f, -3.8f), new Vector3(-1.5f, 0.95f, -2.2f),
                new Vector3(-1.5f, 0.95f, 1.8f)
            };
            var keel = deck.Select(p => new Vector3(p.x * 0.55f, -0.5f, p.z * 0.82f)).ToArray();
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();

            void Face(Vector3 a, Vector3 b, Vector3 c)
            {
                foreach (var p in new[] { a, b, c })
                {
                    vertices.Add(p);
                    uvs.Add(new Vector2(p.z + p.x, p.y + p.x * 0.5f));
                }
            }

            var middle = new Vector3(0f, 0.9f, 0f);
            for (int i = 0; i < deck.Length; i++)
            {
                int j = (i + 1) % deck.Length;
                Face(deck[i], deck[j], keel[j]);
                Face(deck[i], keel[j], keel[i]);
                Face(middle, deck[j], deck[i]);
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(Enumerable.Range(0, vertices.Count).ToArray(), 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Ikki tomondan ko'rinadigan uchburchak.</summary>
        static Mesh Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            var mesh = new Mesh
            {
                vertices = new[] { a, b, c, a, c, b },
                triangles = new[] { 0, 1, 2, 3, 4, 5 }
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        // ---------- Havo sharlari ----------

        /// <summary>spots: shar turadigan joylar (yo'ldan uzoqda bo'lishi kerak).</summary>
        public static void Balloons(Transform parent, IList<Vector3> spots)
        {
            random = new System.Random(41);
            var colors = new[]
            {
                ArtKit.Flat("BalloonRed", new Color(0.85f, 0.16f, 0.1f), 0.35f),
                ArtKit.Flat("BalloonYellow", new Color(0.98f, 0.72f, 0.1f), 0.35f),
                ArtKit.Flat("BalloonBlue", new Color(0.12f, 0.4f, 0.8f), 0.35f),
                ArtKit.Flat("BalloonCream", new Color(0.94f, 0.9f, 0.8f), 0.35f)
            };
            var wood = ArtKit.Tiled("Planks", "weathered_brown_planks", Color.white, 2f);
            var dark = ArtKit.Flat("DarkMetal", new Color(0.09f, 0.09f, 0.1f), 0.4f, 0.4f);
            var envelope = ArtKit.SaveMesh("Envelope", EnvelopeMesh);

            for (int i = 0; i < spots.Count; i++)
            {
                var balloon = new GameObject($"Balloon_{i}").transform;
                balloon.SetParent(parent, false);
                var skin = Child(balloon, "Envelope", envelope, colors[i % colors.Length], false);
                skin.GetComponent<MeshRenderer>().sharedMaterials =
                    new[] { colors[i % colors.Length], colors[(i + 1 + i / 4) % colors.Length] };
                ArtKit.Block(balloon, "Basket", Vector3.zero, new Vector3(1.8f, 1.3f, 1.8f), Quaternion.identity, wood)
                    .isStatic = false;
                for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Cylinder(balloon, new Vector3(x * 0.8f, 1.9f, z * 0.8f), 0.07f, 2.8f, dark).isStatic = false;

                var mover = balloon.gameObject.AddComponent<Cruiser>();
                mover.center = spots[i];
                mover.radius = Range(2f, 5f);
                mover.degreesPerSecond = Range(1.5f, 3f);
                mover.startAngle = Range(0f, 360f);
                mover.bobHeight = 1.4f;
                mover.bobSpeed = 0.35f;
                balloon.position = spots[i];
                balloon.localScale = Vector3.one * Range(0.9f, 1.4f);
            }
        }

        /// <summary>Bo'lakli shar: toq va juft bo'laklar ikki xil rangda (ikki submesh).</summary>
        static Mesh EnvelopeMesh()
        {
            // (balandlik, radius) profili: pastki og'izdan tepa nuqtagacha.
            var profile = new[]
            {
                new Vector2(3f, 0.9f), new Vector2(4.2f, 2.3f), new Vector2(5.8f, 3.7f), new Vector2(7.6f, 4.5f),
                new Vector2(9.4f, 4.5f), new Vector2(11f, 3.8f), new Vector2(12.2f, 2.4f), new Vector2(12.9f, 0f)
            };
            const int slices = 16, steps = 3;
            var vertices = new List<Vector3>();
            var parts = new[] { new List<int>(), new List<int>() };
            for (int slice = 0; slice < slices; slice++)
            {
                int start = vertices.Count;
                for (int step = 0; step <= steps; step++)
                {
                    var turn = Quaternion.Euler(0f, (slice + (float)step / steps) * 360f / slices, 0f);
                    foreach (var ring in profile)
                        vertices.Add(turn * new Vector3(0f, ring.x, ring.y));
                }
                for (int step = 0; step < steps; step++)
                for (int k = 0; k < profile.Length - 1; k++)
                {
                    int a = start + step * profile.Length + k, b = a + profile.Length;
                    parts[slice % 2].AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            }
            var mesh = new Mesh { subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(parts[0], 0);
            mesh.SetTriangles(parts[1], 1);
            mesh.RecalculateNormals();
            return mesh;
        }

        // ---------- Qushlar ----------

        public static void Flock(Transform parent, Vector3 center, float radius, int count, float speed)
        {
            var feathers = ArtKit.Flat("Feathers", new Color(0.12f, 0.11f, 0.13f), 0.1f);
            var wing = ArtKit.SaveMesh("BirdWing", () => Triangle(
                new Vector3(0f, 0f, 0.35f), new Vector3(1.5f, 0f, -0.35f), new Vector3(0f, 0f, -0.2f)));

            var flock = new GameObject("Flock").transform;
            flock.SetParent(parent, false);
            var mover = flock.gameObject.AddComponent<Cruiser>();
            mover.center = center;
            mover.radius = radius;
            mover.degreesPerSecond = speed;
            mover.startAngle = center.y * 7f % 360f;
            mover.bobHeight = 2.5f;
            mover.bobSpeed = 0.4f;
            flock.position = center + Quaternion.Euler(0f, mover.startAngle, 0f) * Vector3.forward * radius;

            for (int i = 0; i < count; i++)
            {
                var bird = new GameObject("Bird").transform;
                bird.SetParent(flock, false);
                // Uchburchak saf: birinchisi oldinda, qolganlari ikki yonida ortda.
                int row = (i + 1) / 2;
                float side = i % 2 == 0 ? 1f : -1f;
                bird.localPosition = new Vector3(side * row * 2.6f, Mathf.Sin(i * 2.3f) * 0.7f, -row * 2.2f);

                var flap = bird.gameObject.AddComponent<BirdFlap>();
                flap.phase = i * 1.7f;
                flap.right = Child(bird, "Right", wing, feathers, false).transform;
                flap.left = Child(bird, "Left", wing, feathers, false).transform;
                flap.left.localScale = new Vector3(-1f, 1f, 1f);
                NoShadow(flap.right.gameObject);
                NoShadow(flap.left.gameObject);
            }
        }

        // ---------- Bulutlar ----------

        /// <summary>spots: bulut to'plamlarining markazlari.</summary>
        public static void Clouds(Transform parent, IList<Vector3> spots)
        {
            random = new System.Random(53);
            var texture = Painting("CloudPuff", 128, 128, (u, v) =>
            {
                float x = u * 2f - 1f, y = v * 2f - 1f;
                float noise = Mathf.PerlinNoise(u * 5f + 2.3f, v * 5f + 8.1f) * 0.65f +
                              Mathf.PerlinNoise(u * 13f, v * 13f) * 0.35f;
                // Pasti yassiroq, chetlari yumshoq va notekis.
                float distance = Mathf.Sqrt(x * x + y * y * (y < 0f ? 2.6f : 1.3f));
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - distance - (noise - 0.5f) * 0.9f) * 1.8f));
                alpha *= Mathf.Clamp01((1f - Mathf.Sqrt(x * x + y * y)) * 4f);
                float light = Mathf.Lerp(0.82f, 1f, Mathf.Clamp01(v * 1.4f - 0.1f));
                return new Color(light, light, light, alpha);
            }, TextureWrapMode.Clamp, true);
            var material = Transparent("CloudArt", texture, new Color(1f, 0.93f, 0.9f, 0.82f));
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

            var clouds = new GameObject("Clouds").AddComponent<CloudBillboards>();
            clouds.transform.SetParent(parent, false);
            var puffs = new List<Transform>();
            foreach (var spot in spots)
            {
                for (int i = 0; i < 4; i++)
                {
                    var puff = Child(clouds.transform, "Puff", quad, material, false);
                    puff.transform.position = spot + new Vector3(Range(-14f, 14f), Range(-2f, 2f), Range(-14f, 14f));
                    float width = Range(18f, 34f);
                    puff.transform.localScale = new Vector3(width, width * Range(0.42f, 0.6f), 1f);
                    NoShadow(puff);
                    puffs.Add(puff.transform);
                }
            }
            clouds.puffs = puffs.ToArray();
        }

        // ---------- Yordamchilar ----------

        static GameObject Child(Transform parent, string name, Mesh mesh, Material material, bool isStatic = true)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.isStatic = isStatic;
            return go;
        }

        static GameObject Cylinder(Transform parent, Vector3 local, float diameter, float length, Material material)
        {
            var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(tube.GetComponent<Collider>());
            tube.transform.SetParent(parent, false);
            tube.transform.localPosition = local;
            tube.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
            tube.GetComponent<Renderer>().sharedMaterial = material;
            tube.isStatic = true;
            return tube;
        }

        static void NoShadow(GameObject go)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material Glow(string name, Color color, float strength)
        {
            var material = ArtKit.Flat(name, color, 0.5f);
            material.SetColor("_EmissionColor", color * strength);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return material;
        }

        static Material Transparent(string name, Texture2D texture, Color color)
        {
            string path = $"{ArtKit.Generated}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Funksiya bo'yicha rasm chizib, loyihaga tekstura sifatida saqlaydi. u, v: 0..1.</summary>
        static Texture2D Painting(string name, int width, int height, System.Func<float, float, Color> color,
            TextureWrapMode wrap, bool alpha)
        {
            string path = $"{ArtKit.Generated}/{name}.png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, color((x + 0.5f) / width, (y + 0.5f) / height));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = wrap;
            importer.alphaIsTransparency = alpha;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
