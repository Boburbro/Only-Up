using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OnlyUp.EditorTools
{
    /// <summary>Quaternius (CC0) modellaridan 3 ta kiyingan personaj va umumiy animator yaratadi.</summary>
    public static class CharacterBuilder
    {
        const string Root = "Assets/Art/Characters";
        const string Generated = Root + "/Generated";
        const string RosterPath = "Assets/Prefabs/CharacterRoster.asset";
        const string ControllerPath = Generated + "/Character.controller";
        const string AnimationPath = Root + "/Animations/UAL1_Standard.fbx";

        class Recipe
        {
            public string id, displayName, body, skin, normal;
            public string[] hair;
            public string hairTexture;
            public Color hairTint, shirt, pants, shoes;
        }

        static readonly Recipe[] Recipes =
        {
            new Recipe
            {
                id = "Aziz", displayName = "Aziz", body = "Superhero_Male_FullBody",
                skin = "T_Superhero_Male_Ligh", normal = "T_Superhero_Male_Normal",
                hair = new[] { "Hair_SimpleParted" }, hairTexture = "T_Hair_1_BaseColor",
                hairTint = new Color(0.45f, 0.36f, 0.3f),
                shirt = new Color(0.86f, 0.85f, 0.82f), pants = new Color(0.24f, 0.24f, 0.22f),
                shoes = new Color(0.12f, 0.12f, 0.13f)
            },
            new Recipe
            {
                id = "Malika", displayName = "Malika", body = "Superhero_Female_FullBody",
                skin = "T_Superhero_Female_Light_BaseColor", normal = "T_Superhero_Female_Normal",
                hair = new[] { "Hair_Long" }, hairTexture = "T_Hair_2_BaseColor",
                hairTint = new Color(0.55f, 0.35f, 0.25f),
                shirt = new Color(0.66f, 0.12f, 0.14f), pants = new Color(0.13f, 0.2f, 0.34f),
                shoes = new Color(0.82f, 0.82f, 0.8f)
            },
            new Recipe
            {
                id = "Sardor", displayName = "Sardor", body = "Superhero_Male_FullBody",
                skin = "T_Superhero_Male_Dark", normal = "T_Superhero_Male_Normal",
                hair = new[] { "Hair_Buzzed", "Hair_Beard" }, hairTexture = "T_Hair_1_BaseColor",
                hairTint = new Color(0.22f, 0.18f, 0.16f),
                shirt = new Color(0.1f, 0.42f, 0.36f), pants = new Color(0.46f, 0.37f, 0.24f),
                shoes = new Color(0.25f, 0.14f, 0.08f)
            }
        };

        public static CharacterRoster LoadOrBuildRoster()
        {
            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>(RosterPath);
            return roster != null && roster.Count == Recipes.Length && roster.characters.All(c => c.model != null)
                ? roster
                : Build();
        }

        [MenuItem("Only Up/Personajlarni qayta yaratish")]
        public static CharacterRoster Build()
        {
            Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh();
            ImportHumanoid(AnimationPath, true);
            foreach (var body in Recipes.Select(r => r.body).Distinct())
                ImportHumanoid($"{Root}/Models/{body}.fbx", false);

            var controller = BuildController();
            var entries = Recipes.Select(r => new CharacterRoster.Entry
            {
                displayName = r.displayName,
                model = BuildCharacter(r, controller)
            }).ToArray();

            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>(RosterPath);
            if (roster == null)
            {
                roster = ScriptableObject.CreateInstance<CharacterRoster>();
                AssetDatabase.CreateAsset(roster, RosterPath);
            }
            roster.characters = entries;
            EditorUtility.SetDirty(roster);
            AssetDatabase.SaveAssets();
            Debug.Log($"OnlyUp: {entries.Length} ta personaj yaratildi");
            return roster;
        }

        static void ImportHumanoid(string path, bool isAnimation)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = isAnimation;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            // Kiyim chizish uchun mesh ma'lumotlari o'qiladigan bo'lishi kerak.
            importer.isReadable = !isAnimation;
            importer.SaveAndReimport();

            if (isAnimation)
            {
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    clip.loopTime = clip.name.EndsWith("_Loop");
                    clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = clip.keepOriginalPositionY = true;
                    clip.keepOriginalPositionXZ = false;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }

            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException($"Humanoid avatar yaratilmadi: {path}");
        }

        static AnimationClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(AnimationPath).OfType<AnimationClip>()
                // __preview__ kliplari Unity'ning ichki nusxalari, ular takrorlanmaydi.
                .FirstOrDefault(c => !c.name.StartsWith("__preview__") &&
                                     (c.name == name || c.name.EndsWith("|" + name)));
            if (clip == null) throw new InvalidOperationException("Animatsiya topilmadi: " + name);
            return clip;
        }

        static AnimatorController BuildController()
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            var machine = controller.layers[0].stateMachine;

            var locomotion = controller.CreateBlendTreeInController(CharacterAnimator.Locomotion, out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip("Idle_Loop"), 0f);
            tree.AddChild(Clip("Walk_Loop"), 1.6f);
            tree.AddChild(Clip("Jog_Fwd_Loop"), 3.8f);
            tree.AddChild(Clip("Sprint_Loop"), 6.8f);
            machine.defaultState = locomotion;

            void State(string name, string clip, float speed = 1f)
            {
                var state = machine.AddState(name);
                state.motion = Clip(clip);
                state.speed = speed;
            }

            State(CharacterAnimator.JumpStart, "Jump_Start", 2.5f);
            State(CharacterAnimator.Air, "Jump_Loop");
            State(CharacterAnimator.Land, "Jump_Land", 2.2f);
            State(CharacterAnimator.Slide, "Crouch_Idle_Loop");
            State(CharacterAnimator.Hang, "Jump_Loop", 0.25f);
            State(CharacterAnimator.Climb, "Jump_Start", 1.6f);
            return controller;
        }

        static GameObject BuildCharacter(Recipe recipe, AnimatorController controller)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/{recipe.body}.fbx");
            var model = (GameObject)Object.Instantiate(source);
            model.name = recipe.id;

            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            var body = renderers.OrderByDescending(r => r.sharedMesh.vertexCount).First();

            var outfit = OutfitPainter.Paint(body, TexturePath(recipe.skin), $"{Generated}/{recipe.id}_Outfit.png",
                recipe.shirt, recipe.pants, recipe.shoes);
            var bodyMaterial = Lit($"{recipe.id}_Body", outfit, NormalMap(recipe.normal), Color.white, 0.2f);
            var hairNormal = NormalMap(recipe.hairTexture.Replace("BaseColor", "Normal"));
            var hairMaterial = Lit($"{recipe.id}_Hair", Texture(recipe.hairTexture), hairNormal, recipe.hairTint, 0.25f);
            var eyeMaterial = Lit("Eyes", Texture("T_Eye_Brown"), null, Color.white, 0.5f);

            foreach (var renderer in renderers)
            {
                var material = renderer == body ? bodyMaterial
                    : renderer.name.StartsWith("Eyebrow") ? hairMaterial : eyeMaterial;
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            }

            var bones = model.GetComponentsInChildren<Transform>().GroupBy(t => t.name)
                .ToDictionary(g => g.Key, g => g.First());
            foreach (var hair in recipe.hair)
                AttachHair($"{Root}/Hair/{hair}.fbx", model, bones, hairMaterial);

            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            model.AddComponent<CharacterAnimator>();

            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            // Bu FBX'lar -Z tomonga qaragan holda keladi, o'yinda esa oldinga +Z.
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(model, $"Assets/Prefabs/Character_{recipe.id}.prefab");
            Object.DestroyImmediate(model);
            return prefab;
        }

        /// <summary>Soch modelining suyaklarini personajning shu nomli suyaklariga ulaydi.</summary>
        static void AttachHair(string path, GameObject model,
            System.Collections.Generic.Dictionary<string, Transform> bones, Material material)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var from in source.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var part = new GameObject(from.name);
                part.transform.SetParent(model.transform, false);
                var to = part.AddComponent<SkinnedMeshRenderer>();
                to.sharedMesh = from.sharedMesh;
                to.bones = from.bones.Select(b => bones[b.name]).ToArray();
                to.rootBone = from.rootBone != null ? bones[from.rootBone.name] : bones["Head"];
                to.localBounds = from.localBounds;
                to.sharedMaterials = from.sharedMaterials.Select(_ => material).ToArray();
            }
        }

        static string TexturePath(string name) => $"{Root}/Textures/{name}.png";

        static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(name));

        static Texture2D NormalMap(string name)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath(name));
            if (importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
            return Texture(name);
        }

        static Material Lit(string name, Texture2D albedo, Texture2D normal, Color tint, float smoothness)
        {
            string path = $"{Generated}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", smoothness);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 0.6f);
                material.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
