using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OnlyUp.EditorTools
{
    public static class TestSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/TestScene.unity";
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

        [MenuItem("Only Up/Sinov sahnasini qayta yaratish")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var ground = Material("Ground", new Color(0.35f, 0.3f, 0.25f));
            var block = Material("Block", new Color(0.75f, 0.5f, 0.25f));
            var wall = Material("Wall", new Color(0.55f, 0.22f, 0.17f));

            var level = new GameObject("Level").transform;
            Box(level, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f), ground);

            // Zinapoya: har biri 0.8 m balandroq.
            Box(level, "Step1", new Vector3(0f, 0.4f, 6f), new Vector3(2.5f, 0.8f, 2.5f), block);
            Box(level, "Step2", new Vector3(0f, 0.8f, 9.5f), new Vector3(2f, 1.6f, 2f), block);
            Box(level, "Step3", new Vector3(0f, 2.2f, 12.8f), new Vector3(2f, 0.4f, 2f), block);

            // Havodagi platformalar: orasidagi masofa asta-sekin kattalashadi.
            Box(level, "Air1", new Vector3(3.5f, 3f, 13f), new Vector3(2f, 0.4f, 2f), block);
            Box(level, "Air2", new Vector3(7.5f, 3.6f, 12f), new Vector3(2f, 0.4f, 2f), block);
            Box(level, "Air3", new Vector3(10f, 4.4f, 8.4f), new Vector3(2f, 0.4f, 2f), block);
            Box(level, "Beam", new Vector3(10f, 4.6f, 3.4f), new Vector3(0.5f, 0.3f, 6f), block);

            // Kamera to'qnashuvini sinash uchun devor.
            Box(level, "Wall", new Vector3(-8f, 3f, 0f), new Vector3(1f, 6f, 14f), wall);

            BuildParkourArea(level, block, wall);

            AddPlayerAndCamera();

            EditorSceneManager.SaveScene(scene, ScenePath);
            ProjectSetup.UpdateBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("OnlyUp: sinov sahnasi yaratildi");
        }

        /// <summary>Player prefabini (0,0,0) ga qo'yib, kamerani unga bog'laydi.</summary>
        internal static void AddPlayerAndCamera()
        {
            var prefab = BuildPlayerPrefab();
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.transform.position = new Vector3(0f, 0.05f, 0f);

            var camera = Camera.main;
            camera.fieldOfView = 65f;
            camera.nearClipPlane = 0.1f;
            var orbit = camera.gameObject.AddComponent<ThirdPersonCamera>();
            orbit.Bind(player.transform, player.GetComponent<PlayerInputReader>());
            player.GetComponent<PlayerMotor>().CameraTransform = camera.transform;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 2.7f, -4.3f), Quaternion.Euler(15f, 0f, 0f));

            new GameObject("GameAudio").AddComponent<GameAudio>()
                .Configure(SoundBuilder.LoadOrBuild(), player.GetComponent<PlayerMotor>());
        }

        static void BuildParkourArea(Transform level, Material block, Material wall)
        {
            var moving = Material("Moving", new Color(0.2f, 0.45f, 0.7f));
            var bounce = Material("Bounce", new Color(0.2f, 0.65f, 0.3f));
            var metal = Material("Pipe", new Color(0.75f, 0.65f, 0.15f));

            // Qirraga osilib chiqiladigan blok: oddiy sakrash bilan yetib bo'lmaydi.
            Box(level, "LedgeBlock", new Vector3(-4f, 1.3f, 8f), new Vector3(3f, 2.6f, 2f), wall);

            // Tik qiyalik.
            var slope = Box(level, "Slope", new Vector3(-11f, 2f, 10f), new Vector3(3f, 0.4f, 6f), block);
            slope.transform.rotation = Quaternion.Euler(-60f, 0f, 0f);

            // Batut va unga mos baland platforma.
            Box(level, "BouncePad", new Vector3(-4f, 0.15f, -7f), new Vector3(2f, 0.3f, 2f), bounce)
                .AddComponent<BouncePad>();
            Box(level, "BounceTarget", new Vector3(-4f, 3.8f, -10f), new Vector3(3f, 0.4f, 2f), block);

            var slider = Box(level, "MovingPlatform", new Vector3(3f, 0.5f, -7f), new Vector3(2.5f, 0.3f, 2.5f), moving);
            slider.isStatic = false;
            slider.AddComponent<MovingPlatform>().travel = new Vector3(6f, 2f, 0f);

            var spinner = Box(level, "RotatingPlatform", new Vector3(12f, 0.5f, -7f), new Vector3(4f, 0.3f, 1.5f), moving);
            spinner.isStatic = false;
            spinner.AddComponent<RotatingPlatform>();

            // Ingichka quvur.
            var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = "Pipe";
            pipe.transform.SetParent(level, false);
            pipe.transform.SetPositionAndRotation(new Vector3(12f, 0.8f, 2f), Quaternion.Euler(90f, 0f, 0f));
            pipe.transform.localScale = new Vector3(0.4f, 4f, 0.4f);
            pipe.GetComponent<Renderer>().sharedMaterial = metal;
        }

        static GameObject BuildPlayerPrefab()
        {
            var root = new GameObject("Player") { tag = "Player" };
            // Ignore Raycast qatlami: personaj o'z nurlariga xalaqit bermaydi.
            root.layer = LayerMask.NameToLayer("Ignore Raycast");

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 48f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f;

            var reader = root.AddComponent<PlayerInputReader>();
            reader.Actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            root.AddComponent<PlayerMotor>();
            root.AddComponent<FallReset>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            root.AddComponent<PlayerCharacter>().Configure(CharacterBuilder.LoadOrBuildRoster(), visual);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        internal static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = size;
            box.isStatic = true;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        internal static Material Material(string name, Color color)
        {
            string path = $"Assets/Art/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
