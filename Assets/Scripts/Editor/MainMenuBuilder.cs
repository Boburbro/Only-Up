using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace OnlyUp.EditorTools
{
    public static class MainMenuBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Only Up/Bosh menyuni qayta yaratish")]
        public static void Build()
        {
            var roster = CharacterBuilder.LoadOrBuildRoster();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var camera = Camera.main;
            camera.fieldOfView = 35f;
            // Kamera chapga surilgan, shunda personaj ekranning o'ng tomonida ko'rinadi.
            camera.transform.SetPositionAndRotation(new Vector3(-1.05f, 1.25f, -4.2f), Quaternion.Euler(4f, 0f, 0f));

            var sun = Object.FindAnyObjectByType<Light>();
            sun.transform.rotation = Quaternion.Euler(35f, 25f, 0f);
            sun.color = new Color(1f, 0.87f, 0.7f);
            sun.shadows = LightShadows.Soft;

            ArtKit.Reset();
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.position = new Vector3(0f, -0.15f, 0f);
            pedestal.transform.localScale = new Vector3(1.5f, 0.15f, 1.5f);
            pedestal.GetComponent<Renderer>().sharedMaterial =
                ArtKit.Tiled("MenuDeck", "weathered_brown_planks", new Color(0.85f, 0.8f, 0.75f), 0.9f);

            var spot = new GameObject("PreviewSpot").transform;
            spot.rotation = Quaternion.Euler(0f, 180f, 0f);

            new GameObject("MainMenu").AddComponent<UI.MainMenu>().Configure(roster, spot);

            MenuArt(camera, spot);
            new GameObject("GameAudio").AddComponent<GameAudio>().Configure(SoundBuilder.LoadOrBuild(), null);

            EditorSceneManager.SaveScene(scene, ScenePath);
            ProjectSetup.UpdateBuildScenes();
            Debug.Log("OnlyUp: bosh menyu yaratildi");
        }

        /// <summary>Menyu orqasidagi manzara: qoya ustidagi supacha, yashiklar, dengiz va osmon.</summary>
        static void MenuArt(Camera camera, Transform spot)
        {
            var art = new GameObject("MenuArt").transform;
            ArtKit.Fit(art, "rock_moss_set_01", "rock03", new Vector3(0f, -1.75f, 0.15f), new Vector3(3.1f, 3f, 2.7f),
                Quaternion.Euler(0f, 20f, 0f));
            ArtKit.Fit(art, "wooden_crate_02", null, new Vector3(1.35f, 0.28f, 1.1f), new Vector3(1f, 0.55f, 0.65f),
                Quaternion.Euler(0f, 25f, 0f));
            ArtKit.Fit(art, "Barrel_01", null, new Vector3(-0.95f, 0.45f, 1.5f), new Vector3(0.62f, 0.95f, 0.62f),
                Quaternion.identity);

            // Uzoqdagi uchib turgan narsalar.
            var items = new (string model, string part, Vector3 position, float size)[]
            {
                ("rock_moss_set_01", "rock01", new Vector3(5.5f, 3.6f, 14f), 3f),
                ("rock_moss_set_01", "rock05", new Vector3(-9f, 6f, 30f), 7f),
                ("rock_moss_set_01", "rock02", new Vector3(14f, 0.5f, 42f), 9f),
                ("wooden_crate_01", null, new Vector3(2.4f, 2.3f, 5f), 0.8f),
                ("wooden_crate_02", null, new Vector3(3.1f, 3.4f, 8f), 1.1f),
                ("barrel_03", null, new Vector3(-2.6f, 3f, 9f), 0.9f)
            };
            for (int i = 0; i < items.Length; i++)
                ArtKit.Fit(art, items[i].model, items[i].part, items[i].position,
                    new Vector3(items[i].size, items[i].size * 0.7f, items[i].size * 0.8f),
                    Quaternion.Euler(8f * (i % 3 - 1), 47f * i, 6f * (i % 2)));

            WorldArt.BuildSea(art);
            Atmosphere.Apply(camera, 0.0036f);

            // Orqa fondagi manzara.
            Landscape.Mountains(art, new Bounds(new Vector3(0f, 0f, 30f), new Vector3(120f, 1f, 120f)), 25f, 9);
            Landscape.Lighthouse(art, new Vector3(38f, 0f, 105f));
            Landscape.Boats(art, new Vector3(0f, 0f, 20f), 5, 48f, 100f);
            Landscape.Balloons(art, new[]
                { new Vector3(-34f, 3f, 95f), new Vector3(20f, 16f, 120f), new Vector3(-10f, 24f, 150f) });
            Landscape.Flock(art, new Vector3(0f, 11f, 60f), 30f, 7, 8f);
            WorldArt.AddButterflies(spot, 9, 2.4f, 0.55f);
        }
    }
}
