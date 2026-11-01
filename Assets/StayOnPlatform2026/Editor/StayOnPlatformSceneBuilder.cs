using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the Stay on Platform scene without external assets
/// (menu: Tools / AI Games Week 3 / Create Stay On Platform Scene).
///   SafePlatform      Plane + MeshCollider
///   DeathFloor        Cube + BoxCollider + DeathZone
///   Bot (prefab)      Capsule + CapsuleCollider + Rigidbody (X/Z rotation frozen) + Brain
///     Eyes_ProbeOrigin  Cube without collider, local (0, 0.52, 0.40)
///   PopulationManager empty object + PopulationManager (+ StrategyLegend, ExperimentDriver)
///   Main Camera, Directional Light
/// </summary>
public static class StayOnPlatformSceneBuilder
{
    private const string Root = "Assets/StayOnPlatform2026";
    public const string ScenePath = Root + "/Scenes/StayOnPlatform.unity";
    public const string PrefabPath = Root + "/Prefabs/Bot.prefab";
    private const string MaterialDir = Root + "/Materials";

    // 5 x 5 platform: spawnHalfExtent (1.7) + capsule radius stays inside it
    private static readonly Vector3 PlatformScale = new Vector3(0.5f, 1f, 0.5f);

    [MenuItem("Tools/AI Games Week 3/Create Stay On Platform Scene")]
    public static void Build()
    {
        Directory.CreateDirectory(MaterialDir);
        Material platformMat = CreateMaterial("Platform", new Color(0.55f, 0.78f, 0.55f));
        Material deathMat = CreateMaterial("DeathFloor", new Color(0.30f, 0.10f, 0.11f));
        Material botMat = CreateMaterial("Bot", new Color(0.25f, 0.45f, 0.85f));
        Material eyesMat = CreateMaterial("Eyes", new Color(0.08f, 0.08f, 0.08f));

        CreateBotPrefab(botMat, eyesMat);
        CreateScene(platformMat, deathMat);
        Debug.Log("StayOnPlatform: scene and prefab ready -> " + ScenePath);
    }

    /// <summary>Batch-mode entry point (-executeMethod StayOnPlatformSceneBuilder.BuildBatch).</summary>
    public static void BuildBatch()
    {
        int code = 0;
        try { Build(); }
        catch (System.Exception e) { Debug.LogException(e); code = 1; }
        EditorApplication.Exit(code);
    }

    [MenuItem("Tools/AI Games Week 3/Build Windows Player")]
    public static void BuildPlayer() => TryBuildPlayer();

    private static bool TryBuildPlayer()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/StayOnPlatform/StayOnPlatform.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        Debug.Log("StayOnPlatform: build " + report.summary.result);
        return report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
    }

    /// <summary>Batch-mode entry point (-executeMethod StayOnPlatformSceneBuilder.BuildSceneAndPlayerBatch).</summary>
    public static void BuildSceneAndPlayerBatch()
    {
        int code = 0;
        try { Build(); if (!TryBuildPlayer()) code = 1; }
        catch (System.Exception e) { Debug.LogException(e); code = 1; }
        EditorApplication.Exit(code);
    }

    private static Material CreateMaterial(string name, Color color)
    {
        string path = $"{MaterialDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void CreateBotPrefab(Material botMat, Material eyesMat)
    {
        Directory.CreateDirectory(Root + "/Prefabs");
        GameObject bot = GameObject.CreatePrimitive(PrimitiveType.Capsule); // CapsuleCollider: radius 0.5, height 2
        bot.name = "Bot";
        bot.GetComponent<MeshRenderer>().sharedMaterial = botMat;

        Rigidbody rb = bot.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        GameObject eyes = GameObject.CreatePrimitive(PrimitiveType.Cube);
        eyes.name = "Eyes_ProbeOrigin";
        // A collider on the probe would take part in the bot's physics and could be hit by rays.
        Object.DestroyImmediate(eyes.GetComponent<BoxCollider>());
        eyes.GetComponent<MeshRenderer>().sharedMaterial = eyesMat;
        eyes.transform.SetParent(bot.transform, false);
        eyes.transform.localPosition = new Vector3(0f, 0.52f, 0.40f);
        eyes.transform.localScale = new Vector3(0.6f, 0.2f, 0.2f);

        Brain brain = bot.AddComponent<Brain>();
        brain.eyes = eyes.transform;
        bot.AddComponent<GenomeColors>();

        PrefabUtility.SaveAsPrefabAsset(bot, PrefabPath);
        Object.DestroyImmediate(bot);
        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
    }

    private static void CreateScene(Material platformMat, Material deathMat)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // NewScene unloads unused assets, so the prefab is loaded after it
        GameObject botPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (botPrefab == null || botPrefab.GetComponent<Brain>() == null)
            throw new System.InvalidOperationException("Bot prefab with Brain not found: " + PrefabPath);

        var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
        Camera camera = cameraGo.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0f, 11f, -7f), Quaternion.Euler(57f, 0f, 0f));
        camera.fieldOfView = 45f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.13f, 0.16f);

        var lightGo = new GameObject("Directional Light");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Plane);
        platform.name = "SafePlatform";
        platform.transform.localScale = PlatformScale;
        platform.GetComponent<MeshRenderer>().sharedMaterial = platformMat;

        GameObject deathFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        deathFloor.name = "DeathFloor";
        deathFloor.transform.position = new Vector3(0f, -3f, 0f);
        deathFloor.transform.localScale = new Vector3(30f, 0.5f, 30f);
        deathFloor.GetComponent<MeshRenderer>().sharedMaterial = deathMat;
        deathFloor.AddComponent<DeathZone>(); // recognised by component, no custom tag needed

        var managerGo = new GameObject("PopulationManager");
        managerGo.transform.position = new Vector3(0f, 1.02f, 0f); // capsule centre just above the plane
        PopulationManager manager = managerGo.AddComponent<PopulationManager>();
        manager.botPrefab = botPrefab;
        manager.safePlatform = platform.GetComponent<Collider>();
        managerGo.AddComponent<StrategyLegend>();
        var driver = new SerializedObject(managerGo.AddComponent<ExperimentDriver>()); // idle without command-line switches
        driver.FindProperty("manager").objectReferenceValue = manager;
        driver.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(Root + "/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        if (File.Exists("Assets/Scenes/SampleScene.unity"))
        {
            AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
            if (Directory.GetFiles("Assets/Scenes").Length == 0) AssetDatabase.DeleteAsset("Assets/Scenes");
        }
    }
}
