using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OnlyUp.EditorTools
{
    /// <summary>
    /// Teri teksturasi ustiga mayka, shim va krossovka chizadi. Qaysi piksel qaysi kiyimga
    /// kirishi modelning suyak og'irliklari va T-poza koordinatalaridan aniqlanadi.
    /// </summary>
    static class OutfitPainter
    {
        const int Size = 1024;

        struct Vertex
        {
            public Vector2 uv;
            public Vector3 position;
            public float arm, upperArm, head, foot;
        }

        public static Texture2D Paint(SkinnedMeshRenderer body, string skinPath, string outputPath,
            Color shirt, Color pants, Color shoes)
        {
            var skin = new Texture2D(2, 2);
            skin.LoadImage(File.ReadAllBytes(skinPath));

            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                pixels[y * Size + x] = skin.GetPixelBilinear((x + 0.5f) / Size, (y + 0.5f) / Size);
            Object.DestroyImmediate(skin);

            var mesh = body.sharedMesh;
            var bones = body.bones;
            var bindPoses = mesh.bindposes;
            var weights = mesh.boneWeights;
            var uvs = mesh.uv;
            var positions = mesh.vertices;

            Transform Bone(string name) => bones.First(b => b.name == name);
            float neck = Bone("neck_01").position.y;
            float waist = Bone("pelvis").position.y + 0.07f;
            float ankle = Bone("foot_l").position.y + 0.05f;
            float shoulder = Mathf.Abs(Bone("upperarm_l").position.x);

            var vertices = new Vertex[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var v = new Vertex { uv = uvs[i] };
                var w = weights[i];
                void Add(int index, float weight)
                {
                    if (weight <= 0f) return;
                    v.position += (bones[index].localToWorldMatrix * bindPoses[index]).MultiplyPoint3x4(positions[i]) * weight;
                    string n = bones[index].name.ToLowerInvariant();
                    if (n.StartsWith("lowerarm") || n.StartsWith("hand") || n.StartsWith("index") ||
                        n.StartsWith("middle") || n.StartsWith("ring") || n.StartsWith("pinky") ||
                        n.StartsWith("thumb")) v.arm += weight;
                    else if (n.StartsWith("upperarm")) v.upperArm += weight;
                    else if (n.StartsWith("head")) v.head += weight;
                    else if (n.StartsWith("foot") || n.StartsWith("ball")) v.foot += weight;
                }
                Add(w.boneIndex0, w.weight0);
                Add(w.boneIndex1, w.weight1);
                Add(w.boneIndex2, w.weight2);
                Add(w.boneIndex3, w.weight3);
                vertices[i] = v;
            }

            var triangles = mesh.triangles;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                var a = vertices[triangles[t]];
                var b = vertices[triangles[t + 1]];
                var c = vertices[triangles[t + 2]];
                Vector2 pa = a.uv * Size, pb = b.uv * Size, pc = c.uv * Size;

                float area = Cross(pb - pa, pc - pa);
                if (Mathf.Abs(area) < 1e-5f) continue;

                int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, pb.x, pc.x)) - 1);
                int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(pa.x, pb.x, pc.x)) + 1);
                int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, pb.y, pc.y)) - 1);
                int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(pa.y, pb.y, pc.y)) + 1);

                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float wa = Cross(pb - p, pc - p) / area;
                    float wb = Cross(pc - p, pa - p) / area;
                    float wc = 1f - wa - wb;
                    // Kichik zaxira UV choklarida teri ko'rinib qolmasligi uchun.
                    const float margin = -0.08f;
                    if (wa < margin || wb < margin || wc < margin) continue;

                    Vector3 pos = a.position * wa + b.position * wb + c.position * wc;
                    float arm = a.arm * wa + b.arm * wb + c.arm * wc;
                    float upperArm = a.upperArm * wa + b.upperArm * wb + c.upperArm * wc;
                    float head = a.head * wa + b.head * wb + c.head * wc;
                    float foot = a.foot * wa + b.foot * wb + c.foot * wc;
                    float side = Mathf.Abs(pos.x);

                    Color? cloth = null;
                    if (foot > 0.5f || (pos.y < ankle && arm < 0.1f))
                        cloth = pos.y < 0.025f ? new Color(0.8f, 0.78f, 0.72f) : shoes;
                    else if (arm < 0.35f && head < 0.2f)
                    {
                        float neckline = neck - 0.015f - Mathf.Clamp01(1f - side / 0.09f) * 0.07f;
                        if (pos.y < waist)
                            cloth = pos.y > waist - 0.045f ? pants * 0.55f : pants;
                        else if (pos.y < neckline && upperArm < 0.5f && side < shoulder - 0.015f)
                            cloth = shirt;
                    }

                    if (cloth.HasValue)
                    {
                        // Mato donadorligi tekis rang sun'iy ko'rinmasligi uchun.
                        float grain = 0.94f + 0.12f * Mathf.PerlinNoise(x * 0.35f, y * 0.35f);
                        Color color = cloth.Value * grain;
                        color.a = 1f;
                        pixels[y * Size + x] = color;
                    }
                }
            }

            var result = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            result.SetPixels(pixels);
            File.WriteAllBytes(outputPath, result.EncodeToPNG());
            Object.DestroyImmediate(result);
            AssetDatabase.ImportAsset(outputPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
