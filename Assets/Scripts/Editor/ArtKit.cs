using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OnlyUp.EditorTools
{
    /// <summary>Poly Haven (CC0) teksturalari va modellaridan material, mesh va bezak yasaydi.</summary>
    static class ArtKit
    {
        const string Source = "Assets/Art/PolyHaven";
        public const string Generated = "Assets/Art/Generated";

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static readonly HashSet<string> preparedModels = new HashSet<string>();

        public static void Reset()
        {
            Directory.CreateDirectory(Generated + "/Meshes");
            AssetDatabase.Refresh();
            materials.Clear();
            meshes.Clear();
            preparedModels.Clear();
        }

        // ---------- Materiallar ----------

        public static Material Flat(string name, Color color, float smoothness = 0.2f, float metallic = 0f)
        {
            if (materials.TryGetValue(name, out var cached) && cached != null) return cached;
            string path = $"{Generated}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return materials[name] = material;
        }

        /// <summary>Takrorlanadigan tekstura. UV metrlarda bo'lgan mesh bilan ishlatiladi.</summary>
        public static Material Tiled(string name, string texture, Color tint, float metersPerTile,
            float smoothness = 0.15f, float metallic = 0f)
        {
            if (materials.TryGetValue(name, out var cached) && cached != null) return cached;
            var material = Flat(name, tint, smoothness, metallic);
            var albedo = Directory.GetFiles($"{Source}/Textures/{texture}", "Diffuse.*").First(f => !f.EndsWith(".meta"));
            var normal = Directory.GetFiles($"{Source}/Textures/{texture}", "nor_gl.*").First(f => !f.EndsWith(".meta"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
            material.SetTextureScale("_BaseMap", Vector2.one / metersPerTile);
            SetNormal(material, normal);
            return material;
        }

        static void SetNormal(Material material, string path, float strength = 1f)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            material.SetFloat("_BumpScale", strength);
            material.EnableKeyword("_NORMALMAP");
        }

        static Material ModelMaterial(string model, string name)
        {
            string key = $"M_{model}_{name}";
            if (materials.TryGetValue(key, out var cached) && cached != null) return cached;

            var files = Directory.GetFiles($"{Source}/Models/{model}/textures")
                .Where(f => !f.EndsWith(".meta")).Select(f => f.Replace('\\', '/')).ToArray();
            string Find(string kind) =>
                files.FirstOrDefault(f => Path.GetFileName(f).StartsWith($"{name}_{kind}")) ??
                files.FirstOrDefault(f => Path.GetFileName(f).Contains($"_{kind}"));

            string albedo = Find("diff");
            string alpha = Find("alpha");
            var material = Flat(key, Color.white, 0.15f);
            if (alpha != null)
            {
                material.SetTexture("_BaseMap", Cutout(albedo, alpha, $"{Generated}/{key}.png"));
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.4f);
                material.SetFloat("_Cull", 0f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));

            string normal = Find("nor_gl");
            if (normal != null) SetNormal(material, normal);
            return material;
        }

        /// <summary>Rang va alohida shaffoflik rasmini bitta RGBA teksturaga birlashtiradi.</summary>
        static Texture2D Cutout(string albedoPath, string alphaPath, string output)
        {
            var color = new Texture2D(2, 2);
            color.LoadImage(File.ReadAllBytes(albedoPath));
            var mask = new Texture2D(2, 2);
            mask.LoadImage(File.ReadAllBytes(alphaPath));
            var result = new Texture2D(color.width, color.height, TextureFormat.RGBA32, false);
            var pixels = color.GetPixels();
            for (int y = 0; y < color.height; y++)
            for (int x = 0; x < color.width; x++)
            {
                int i = y * color.width + x;
                pixels[i].a = mask.GetPixelBilinear((x + 0.5f) / color.width, (y + 0.5f) / color.height).r;
            }
            result.SetPixels(pixels);
            File.WriteAllBytes(output, result.EncodeToPNG());
            Object.DestroyImmediate(color);
            Object.DestroyImmediate(mask);
            Object.DestroyImmediate(result);
            AssetDatabase.ImportAsset(output);
            var importer = (TextureImporter)AssetImporter.GetAtPath(output);
            importer.alphaIsTransparency = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = 0.4f;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(output);
        }

        // ---------- Meshlar ----------

        public static Mesh SaveMesh(string key, System.Func<Mesh> create)
        {
            if (meshes.TryGetValue(key, out var cached) && cached != null) return cached;
            string path = $"{Generated}/Meshes/{key}.asset";
            var mesh = create();
            mesh.name = key;
            // Mavjud asset o'rnida yangilanadi, aks holda boshqa sahnalardagi havolalar uziladi.
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            return meshes[key] = mesh;
        }

        /// <summary>UV'si metrlarda bo'lgan quti: tekstura cho'zilmaydi.</summary>
        public static Mesh BoxMesh(Vector3 size)
        {
            string key = $"Box_{size.x:0.00}_{size.y:0.00}_{size.z:0.00}";
            return SaveMesh(key, () =>
            {
                var source = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                var vertices = source.vertices;
                var normals = source.normals;
                var uvs = new Vector2[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 v = Vector3.Scale(vertices[i], size);
                    Vector3 n = normals[i];
                    uvs[i] = Mathf.Abs(n.y) > 0.5f ? new Vector2(v.x, v.z)
                        : Mathf.Abs(n.x) > 0.5f ? new Vector2(v.z, v.y) : new Vector2(v.x, v.y);
                    vertices[i] = v;
                }
                var mesh = new Mesh { vertices = vertices, normals = normals, uv = uvs, triangles = source.triangles };
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        public static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material material,
            Vector3 position, Quaternion rotation, bool isStatic = true)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.isStatic = isStatic;
            return go;
        }

        public static GameObject Block(Transform parent, string name, Vector3 center, Vector3 size, Quaternion rotation,
            Material material) => MeshObject(parent, name, BoxMesh(size), material, center, rotation);

        /// <summary>Kub shaklidagi platformaning ko'rinishini tekstura qo'yilgan mesh bilan almashtiradi.</summary>
        public static void Skin(GameObject box, Material material)
        {
            box.GetComponent<MeshRenderer>().enabled = false;
            Vector3 size = box.transform.localScale;
            var art = new GameObject("Art", typeof(MeshFilter), typeof(MeshRenderer));
            art.transform.SetParent(box.transform, false);
            // Ota obyektning cho'zilishi bekor qilinadi, mesh o'zi haqiqiy o'lchamda.
            art.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f / size.z);
            art.GetComponent<MeshFilter>().sharedMesh = BoxMesh(size);
            art.GetComponent<MeshRenderer>().sharedMaterial = material;
            art.isStatic = box.isStatic;
        }

        // ---------- Modellar ----------

        static GameObject LoadModel(string model)
        {
            string path = $"{Source}/Models/{model}/{model}_1k.fbx";
            if (preparedModels.Add(model))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                // Uzoqdagi modellar soddaroq chiziladi.
                importer.generateMeshLods = true;
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>
        /// Modelni (yoki uning part qismini) berilgan o'lchamli qutiga joylaydi.
        /// center: qutining markazi. Fizik chegara qo'shilmaydi.
        /// </summary>
        public static GameObject Fit(Transform parent, string model, string part, Vector3 center, Vector3 size,
            Quaternion rotation)
        {
            var source = LoadModel(model);
            var piece = (GameObject)Object.Instantiate(source);
            if (part != null)
            {
                var child = piece.GetComponentsInChildren<Transform>().First(t => t.name.EndsWith(part));
                child.SetParent(null, true);
                Object.DestroyImmediate(piece);
                piece = child.gameObject;
            }
            foreach (var collider in piece.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);

            var renderers = piece.GetComponentsInChildren<MeshRenderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                renderer.sharedMaterials = renderer.sharedMaterials
                    .Select(m => ModelMaterial(model, m != null ? m.name : model)).ToArray();
            }

            var holder = new GameObject(piece.name).transform;
            var scaler = new GameObject("Scale").transform;
            scaler.SetParent(holder, false);
            piece.transform.position -= bounds.center;
            piece.transform.SetParent(scaler, false);
            scaler.localScale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);

            holder.SetParent(parent, false);
            holder.SetPositionAndRotation(center, rotation);
            foreach (var t in holder.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            return holder.gameObject;
        }
    }
}
