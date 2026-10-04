using System.IO;
using LivingWorld.Game.Bridge;
using LivingWorld.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LivingWorld.Game.World.Editor
{
    /// <summary>Builds a reproducible licensed-art shop scene without editing scene YAML.</summary>
    public static class SliceSetup
    {
        public const string ScenePath = "Assets/_Game/World/Scenes/AppleShopSlice.unity";
        private const string Generated = "Assets/_Game/World/Generated";

        [MenuItem("Living World/Create Apple Shop Slice")]
        public static void Create()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh();
            ConfigureProduct();
            ConfigurePipeline();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var runner = new GameObject("Living World simulation").AddComponent<WorldRunner>();
            var hud = new GameObject("Shop interface").AddComponent<ShopHud>();
            hud.Bind(runner);

            var camera = new GameObject("Village camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(9, 10, -14);
            camera.transform.LookAt(new Vector3(0, 1, 1));
            camera.fieldOfView = 42;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.61f, 0.77f, 0.83f);
            camera.gameObject.AddComponent<AudioListener>();
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var sun = new GameObject("Afternoon sun (preview lighting)").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.63f, 0.68f, 0.68f);
            var ground = Block("Village clearing", new Vector3(0, -0.15f, 0), new Vector3(30, 0.3f, 25), new Color(0.37f, 0.49f, 0.30f));
            Block("Market path", new Vector3(0, 0.015f, -1), new Vector3(19, 0.035f, 4), new Color(0.68f, 0.60f, 0.43f));
            var shop = Model("Assets/Packs/Kenney/FantasyTownKit/stall-green.glb", "Apple shop", new Vector3(0, 0, 1), 3f);
            var collider = shop.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 0.6f, 0);
            collider.size = new Vector3(1.7f, 1.2f, 1.3f);
            var interaction = shop.AddComponent<ShopInteraction>();
            interaction.Hud = hud;
            interaction.ViewCamera = camera;
            for (var i = 0; i < 5; i++)
                Model("Assets/Packs/Kenney/NatureKit/tree_simple.glb", "Tree " + i,
                    new Vector3(-9 + i * 4, 0, 7 + (i % 2) * 2), 2.5f);
            Model("Assets/Packs/Kenney/FantasyTownKit/stall.glb", "Neighbour market stall", new Vector3(-6, 0, 3), 2.3f);
            Model("Assets/Packs/Kenney/FantasyTownKit/fountain-square-detail.glb", "Village fountain", new Vector3(6, 0, 4), 2f);
            for (var i = 0; i < 6; i++)
                Model("Assets/Packs/Kenney/FoodKit/Models/GLB format/apple.glb", "Decorative apple (not inventory)",
                    new Vector3(-1 + i * 0.4f, 1.7f, 0), 0.25f);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Apple shop slice generated. Preview lighting only; movement, NPC navigation and day/night are not implemented.");
        }

        private static void ConfigureProduct(bool building = false)
        {
            PlayerSettings.productName = "Living World";
            PlayerSettings.companyName = "Geltrax";
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var inputHandler = settings.FindProperty("activeInputHandler");
            if (inputHandler == null) throw new System.InvalidOperationException("Cannot verify Active Input Handling in this Unity version.");
            var changed = inputHandler.intValue == 0;
            if (changed)
            {
                inputHandler.intValue = 2; // Both preserves editor tooling while enabling touch via Input System.
                settings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                if (building) throw new System.InvalidOperationException("Input System was enabled. Restart Unity before building so input compilation defines match PlayerSettings.");
                Debug.LogWarning("Input System enabled. Restart Unity before playing or building the slice.");
            }
#if !ENABLE_INPUT_SYSTEM
            if (building) throw new System.InvalidOperationException("Input System compilation is inactive. Restart Unity after enabling Active Input Handling before building.");
#endif
        }

        private static void ConfigurePipeline()
        {
            var rendererPath = Generated + "/SliceRenderer.asset";
            var pipelinePath = Generated + "/SlicePipeline.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static GameObject Model(string path, string name, Vector3 position, float scale)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new FileNotFoundException("Kenney model has not imported. Install glTFast and reimport before generating the slice.", path);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = name;
            model.transform.position = position;
            model.transform.localScale = Vector3.one * scale;
            return model;
        }

        private static GameObject Block(string name, Vector3 position, Vector3 scale, Color color)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
            var path = Generated + "/" + name.Replace(" ", "") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = color;
                AssetDatabase.CreateAsset(material, path);
            }
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

        /// <summary>Builds the generated slice as a local Mac smoke-test application.</summary>
        public static void BuildMac()
        {
            ConfigureProduct(true);
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/LivingWorld.app",
                BuildTarget.StandaloneOSX, BuildOptions.Development);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.InvalidOperationException("Mac slice build failed: " + report.summary.result);
        }

        [MenuItem("Living World/Capture Village Camera")]
        public static void Capture()
        {
            var camera = Camera.main;
            if (camera == null) throw new System.InvalidOperationException("Open the generated scene first.");
            var target = new RenderTexture(1366, 1024, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                Directory.CreateDirectory("TestResults");
                File.WriteAllBytes("TestResults/apple-shop-camera.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
            }
        }
    }
}
