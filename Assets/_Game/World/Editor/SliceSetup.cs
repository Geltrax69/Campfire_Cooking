using System.IO;
using System.Collections.Generic;
using LivingWorld.Game.Player;
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
            camera.transform.position = new Vector3(4, 6, -15);
            camera.transform.LookAt(new Vector3(4, 1.5f, -7));
            camera.fieldOfView = 42;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.61f, 0.77f, 0.83f);
            camera.gameObject.AddComponent<AudioListener>();
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var sun = new GameObject("Afternoon sun (preview lighting)").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = VillageLighting.NoonIntensity;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = VillageLighting.DayAmbient;
            BuildVillage(runner, hud, camera);
            var lighting = sun.gameObject.AddComponent<VillageLighting>();
            lighting.Runner = runner;
            lighting.Sun = sun;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Town walkthrough generated. NPCs are static workplace previews, not NavMesh schedules.");
        }

        [System.Serializable] private sealed class Position { public float x; public float y; }
        [System.Serializable] private sealed class Location { public string id; public string name; public string art; public Position position; public string type; }
        [System.Serializable] private sealed class Locations { public Location[] locations; }
        [System.Serializable] private sealed class Npc { public string id; public string name; public string model; public string workplace; }
        [System.Serializable] private sealed class Npcs { public Npc[] npcs; }

        private static void BuildVillage(WorldRunner runner, ShopHud hud, Camera camera)
        {
            Block("Village ground", new Vector3(0, -0.2f, 0), new Vector3(64, 0.4f, 64), new Color(0.37f, 0.49f, 0.30f));
            var locations = JsonUtility.FromJson<Locations>(File.ReadAllText("Content/world/locations.json"));
            var preview = new Dictionary<string, Vector3>();
            var landmarkRadius = new Dictionary<string, float>();
            foreach (var location in locations.locations)
            {
                if (Mathf.Abs(location.position.x) > 52 || Mathf.Abs(location.position.y) > 36) continue;
                if (location.id == "loc_home_fenn" || location.id == "loc_home_guard" || location.id == "loc_home_bray") continue;
                var point = new Vector3(location.position.x * 0.45f, 0, location.position.y * 0.45f);
                preview.Add(location.id, point);
                if (location.id == "loc_square")
                {
                    var fountain = Model("Assets/Packs/Kenney/FantasyTownKit/fountain-round.glb", location.name, point, 2);
                    if (Mathf.Max(BoundsOf(fountain).size.x, BoundsOf(fountain).size.z) > 4.5f)
                        throw new System.InvalidOperationException("The plaza fountain exceeds the 4.5 metre preview footprint.");
                    AddBoundsCollider(fountain);
                    var fountainBounds = BoundsOf(fountain);
                    landmarkRadius[location.id] = Mathf.Max(fountainBounds.size.x, fountainBounds.size.z) * 0.5f + 1.2f;
                }
                else if (location.id == "loc_well") continue; // Too close to the square for a second blocking landmark.
                else if (location.id == "loc_apple_stall")
                {
                    var shop = Model(location.art, location.name, point, 1);
                    FitHeight(shop, 2.6f);
                    AddBoundsCollider(shop);
                    var interaction = shop.AddComponent<ShopInteraction>();
                    interaction.Hud = hud; interaction.ViewCamera = camera;
                }
                else BuildHouse(location.name, point, location.type == "tavern" ? 4.5f : 3.2f);
                Label(location.name, point + new Vector3(0, 2.9f, -1.9f), 0.035f);
                // Roads follow approved relative positions, meeting at the square.
                var road = Block("Path " + location.id, point * 0.5f + Vector3.up * 0.012f,
                    new Vector3(1.6f, 0.025f, Mathf.Max(1, point.magnitude)), new Color(0.68f, 0.60f, 0.43f));
                road.transform.rotation = Quaternion.LookRotation(point == Vector3.zero ? Vector3.forward : point);
                Object.DestroyImmediate(road.GetComponent<Collider>());
            }
            for (var i = 0; i < 12; i++)
            {
                var point = new Vector3(-27 + (i % 6) * 10, 0, i < 6 ? 26 : -26);
                var tree = Model("Assets/Packs/Kenney/NatureKit/tree_simple.glb", "Village tree", point, 1);
                FitHeight(tree, 4 + i % 3);
                AddBoundsCollider(tree);
            }
            Boundary(new Vector3(-31, 2, 0), new Vector3(1, 4, 64));
            Boundary(new Vector3(31, 2, 0), new Vector3(1, 4, 64));
            Boundary(new Vector3(0, 2, -31), new Vector3(64, 4, 1));
            Boundary(new Vector3(0, 2, 31), new Vector3(64, 4, 1));
            var npcs = JsonUtility.FromJson<Npcs>(File.ReadAllText("Content/npcs/npcs.json"));
            var counts = new Dictionary<string, int>();
            foreach (var npc in npcs.npcs)
            {
                if (!preview.TryGetValue(npc.workplace, out var point)) continue;
                counts.TryGetValue(npc.workplace, out var count); counts[npc.workplace] = count + 1;
                var npcPoint = point + new Vector3(-1 + count * 1.3f, 0, -2.6f);
                if (landmarkRadius.TryGetValue(npc.workplace, out var radius))
                    npcPoint = point + Quaternion.Euler(0, -25 + count * 35, 0) * Vector3.back * radius;
                var view = Model(npc.model, npc.name + " (workplace preview)", npcPoint, 1);
                FitHeight(view, 1.75f);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(npc.model))
                    if (asset is AnimationClip clip && clip.name.ToLowerInvariant().Contains("idle"))
                    {
                        view.AddComponent<IdlePose>().Clip = clip;
                        break;
                    }
                var collider = view.AddComponent<CapsuleCollider>();
                collider.center = view.transform.InverseTransformPoint(view.transform.position + Vector3.up * 0.85f);
                collider.height = 1.7f / view.transform.lossyScale.y;
                collider.radius = 0.35f / view.transform.lossyScale.x;
                var interaction = view.AddComponent<NpcInteraction>();
                interaction.NpcId = npc.id; interaction.ModelPath = npc.model;
                interaction.Runner = runner; interaction.Hud = hud; interaction.ViewCamera = camera;
                interaction.NameLabel = Label(npc.name, view.transform.position + Vector3.up * 2.1f, 0.032f);
            }
            var player = new GameObject("Village walker");
            player.transform.position = new Vector3(4, 0.15f, -7);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = 0.3f; controller.center = new Vector3(0, 0.9f, 0);
            var avatar = Model("Assets/Packs/Kenney/MiniCharacters/character-male-a.glb", "You", player.transform.position, 1);
            FitHeight(avatar, 1.75f);
            avatar.transform.SetParent(player.transform, true);
            var walker = player.AddComponent<TownWalker>();
            walker.Bind(camera);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Packs/Kenney/MiniCharacters/character-male-a.glb"))
                if (asset is AnimationClip clip)
                {
                    if (clip.name.ToLowerInvariant() == "idle") walker.IdleClip = clip;
                    else if (clip.name.ToLowerInvariant() == "walk") walker.WalkClip = clip;
                }
        }

        private static void BuildHouse(string name, Vector3 point, float width)
        {
            var body = Block(name, point + Vector3.up * 1.2f, new Vector3(width, 2.4f, 3), new Color(0.79f, 0.72f, 0.57f));
            var facade = Model("Assets/Packs/Kenney/FantasyTownKit/wall-wood-window-shutters.glb", name + " Kenney frontage", point + new Vector3(0, 0, -1.55f), 1);
            FitHeight(facade, 2.4f);
            var roof = Model("Assets/Packs/Kenney/FantasyTownKit/roof-gable.glb", name + " roof", point + Vector3.up * 2.4f, 1);
            var bounds = BoundsOf(roof);
            roof.transform.localScale *= width / Mathf.Max(bounds.size.x, 0.01f);
            var door = Block(name + " door", point + new Vector3(0, 0.85f, -1.51f), new Vector3(0.9f, 1.7f, 0.08f), new Color(0.31f, 0.22f, 0.13f));
            Object.DestroyImmediate(door.GetComponent<Collider>());
            for (var side = -1; side <= 1; side += 2)
            {
                var window = Block(name + " window " + side, point + new Vector3(side * width * 0.32f, 1.35f, -1.52f), new Vector3(0.55f, 0.65f, 0.08f), new Color(0.22f, 0.33f, 0.37f));
                Object.DestroyImmediate(window.GetComponent<Collider>());
            }
        }
        private static Bounds BoundsOf(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        private static void FitHeight(GameObject model, float height)
        {
            model.transform.localScale *= height / Mathf.Max(BoundsOf(model).size.y, 0.01f);
            var bounds = BoundsOf(model);
            model.transform.position += Vector3.up * (model.transform.position.y - bounds.min.y);
        }
        private static void AddBoundsCollider(GameObject model)
        {
            var bounds = BoundsOf(model);
            var collider = model.AddComponent<BoxCollider>();
            collider.center = model.transform.InverseTransformPoint(bounds.center);
            collider.size = new Vector3(bounds.size.x / model.transform.lossyScale.x, bounds.size.y / model.transform.lossyScale.y, bounds.size.z / model.transform.lossyScale.z);
        }
        private static TextMesh Label(string text, Vector3 point, float size)
        {
            var label = new GameObject("Sign " + text).AddComponent<TextMesh>();
            label.transform.position = point; label.text = text; label.fontSize = 48;
            label.characterSize = size; label.anchor = TextAnchor.MiddleCenter;
            label.color = new Color(1, 0.96f, 0.85f);
            var back = new GameObject("Label shadow").AddComponent<TextMesh>();
            back.transform.SetParent(label.transform, false);
            back.transform.localPosition = new Vector3(0.018f, -0.018f, 0.012f);
            back.text = text; back.fontSize = label.fontSize; back.characterSize = size;
            back.anchor = label.anchor; back.color = new Color(0.08f, 0.06f, 0.04f);
            label.gameObject.AddComponent<FacingLabel>();
            return label;
        }
        private static void Boundary(Vector3 position, Vector3 scale)
        {
            var boundary = new GameObject("Walkthrough boundary");
            boundary.transform.position = position;
            boundary.AddComponent<BoxCollider>().size = scale;
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
