using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Takes the Unity Editor screenshots used in the report (menu: Tools / AI Games Week 3 / Take Report Screenshots,
/// or create Temp/report_capture.request while the editor is open). Walks through the steps below, enters Play mode
/// where needed and saves the main editor window as PNG. The scene is never saved.
/// State survives the domain reload of entering Play mode through SessionState.
/// </summary>
[InitializeOnLoad]
public static class ReportScreenshots
{
    private const string RequestFile = "Temp/report_capture.request";
    private const string DoneFile = "Temp/report_capture.done";
    private const string OutDir = "docs/week07/images/editor";
    private const string StepKey = "SopShots.Step", TimeKey = "SopShots.T0", EnteredKey = "SopShots.Entered";

    // seconds to wait in each step (after Play/Stop transitions have finished)
    private static readonly float[] Wait = { 0f, 3f, 3f, 3f, 0.5f, 40f, 3f, 3f };

    static ReportScreenshots()
    {
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/AI Games Week 3/Take Report Screenshots")]
    public static void Begin()
    {
        Directory.CreateDirectory(OutDir);
        if (File.Exists(DoneFile)) File.Delete(DoneFile);
        SessionState.SetInt(StepKey, 1);
        SessionState.SetBool(EnteredKey, false);
    }

    private static double Now => EditorApplication.timeSinceStartup;

    private static void Tick()
    {
        if (File.Exists(RequestFile))
        {
            File.Delete(RequestFile);
            Begin();
        }

        int step = SessionState.GetInt(StepKey, 0);
        if (step == 0) return;

        if (step == 1 && EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false; // start from Edit mode
            return;
        }

        if (!SessionState.GetBool(EnteredKey, false))
        {
            SessionState.SetBool(EnteredKey, true);
            SessionState.SetFloat(TimeKey, (float)Now);
            try { Enter(step); } catch (Exception e) { Fail(e); }
            return;
        }

        bool wantsPlay = step == 4 || step == 5 || step == 6;
        bool wantsStop = step == 7;
        if ((wantsPlay && !EditorApplication.isPlaying) || (wantsStop && EditorApplication.isPlayingOrWillChangePlaymode)
            || EditorApplication.isCompiling)
        {
            SessionState.SetFloat(TimeKey, (float)Now);
            return;
        }
        // Entering Play mode takes a few seconds: wait for game time, not editor time, before the first shot
        if (step == 4 && Time.timeSinceLevelLoad < 4.5f)
        {
            SessionState.SetFloat(TimeKey, (float)Now);
            return;
        }
        if (step == 5 && EditorApplication.isPlaying) Time.timeScale = 5f; // fast-forward the generations
        if (EditorApplication.isPlaying) InternalEditorUtility.RepaintAllViews(); // keep the Game view current

        if (Now - SessionState.GetFloat(TimeKey, 0f) < Wait[step]) return;

        try { Leave(step); } catch (Exception e) { Fail(e); return; }
        int next = step + 1 < Wait.Length ? step + 1 : 0;
        SessionState.SetInt(StepKey, next);
        SessionState.SetBool(EnteredKey, false);
        if (next == 0)
        {
            File.WriteAllText(DoneFile, "ok");
            Debug.Log("StayOnPlatform: report screenshots saved to " + OutDir);
        }
    }

    private static void Enter(int step)
    {
        switch (step)
        {
            case 1: // scene hierarchy + PopulationManager settings
                EditorApplication.ExecuteMenuItem("Window/Layouts/Default"); // Hierarchy, Scene/Game, Inspector, Project/Console
                ClearConsole();
                EditorSceneManager.OpenScene(StayOnPlatformSceneBuilder.ScenePath, OpenSceneMode.Single);
                Selection.activeGameObject = GameObject.Find("PopulationManager");
                FrameSceneView();
                break;
            case 2: // Bot prefab: Rigidbody (frozen X/Z rotation) and Brain; other components folded
                PrefabStage stage = PrefabStageUtility.OpenPrefab(StayOnPlatformSceneBuilder.PrefabPath);
                GameObject root = stage.prefabContentsRoot;
                SetExpanded(root, c => c is Rigidbody || c is Brain);
                var constraints = new SerializedObject(root.GetComponent<Rigidbody>()).FindProperty("m_Constraints");
                if (constraints != null) constraints.isExpanded = true; // editor UI state only
                Selection.activeGameObject = root;
                ActiveEditorTracker.sharedTracker.ForceRebuild();
                FrameBot(root);
                break;
            case 3: // Eyes probe inside the prefab: position, no collider
                GameObject bot = PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot;
                GameObject eyes = bot.transform.Find("Eyes_ProbeOrigin").gameObject;
                SetExpanded(eyes, _ => true);
                Selection.activeGameObject = eyes;
                ActiveEditorTracker.sharedTracker.ForceRebuild();
                FrameBot(bot);
                break;
            case 4: // first generation in the Game view
                StageUtility.GoToMainStage();
                EditorSceneManager.OpenScene(StayOnPlatformSceneBuilder.ScenePath, OpenSceneMode.Single);
                Selection.activeGameObject = GameObject.Find("PopulationManager");
                FocusGameView();
                EditorApplication.isPlaying = true;
                break;
            case 5: // later generation: Scene view with the green/red sensor rays
                FrameSceneView();
                break;
            case 6: // per-generation logs
                EditorApplication.ExecuteMenuItem("Window/General/Console");
                break;
            case 7:
                EditorApplication.isPlaying = false;
                break;
        }
    }

    private static void Leave(int step)
    {
        switch (step)
        {
            case 1: Capture("01_scene_population_manager.png"); break;
            case 2: Capture("02_bot_prefab.png"); break;
            case 3:
                Capture("03_eyes_probe.png");
                // foldouts are remembered per component type: leave them all open again
                SetExpanded(PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot, _ => true);
                break;
            case 4: Capture("04_play_generation1.png"); break;
            case 5: Capture("05_scene_view_sensor_rays.png"); break;
            case 6: Capture("06_console_generation_logs.png"); break;
            case 7:
                EditorSceneManager.OpenScene(StayOnPlatformSceneBuilder.ScenePath, OpenSceneMode.Single);
                FocusGameView();
                break;
        }
    }

    /// <summary>Opens or folds the Inspector sections of a GameObject's components (editor UI state only).</summary>
    private static void SetExpanded(GameObject go, Func<Component, bool> expanded)
    {
        foreach (Component c in go.GetComponents<Component>())
            InternalEditorUtility.SetIsInspectorExpanded(c, expanded(c));
    }

    private static void FrameBot(GameObject bot)
    {
        SceneView view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
        view.Focus();
        view.LookAtDirect(bot.transform.position + new Vector3(0f, 0.3f, 0f), Quaternion.Euler(20f, -50f, 0f), 2.2f);
        view.Repaint();
    }

    private static void FrameSceneView()
    {
        SceneView view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
        view.Focus();
        view.LookAtDirect(new Vector3(0f, -0.5f, 0f), Quaternion.Euler(40f, -30f, 0f), 6f);
        view.Repaint();
    }

    private static void FocusGameView()
    {
        var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType == null) return;
        EditorWindow.GetWindow(gameViewType, false, null, true);
    }

    private static void ClearConsole()
    {
        Type.GetType("UnityEditor.LogEntries,UnityEditor")?.GetMethod("Clear")?.Invoke(null, null);
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);

    /// <summary>ReadScreenPixel copies whatever is on screen, so the editor must be the foreground window.</summary>
    private static bool EditorIsForeground()
    {
        uint own = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        GetWindowThreadProcessId(GetForegroundWindow(), out uint fg);
        if (fg == own) return true;
        IntPtr main = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
        ShowWindow(main, 3); // maximize
        SetForegroundWindow(main);
        GetWindowThreadProcessId(GetForegroundWindow(), out fg);
        return fg == own;
    }

    private static void Capture(string file)
    {
        if (!EditorIsForeground())
            throw new InvalidOperationException("Unity is not the foreground window; screenshot not taken: " + file);
        Rect r = EditorGUIUtility.GetMainWindowPosition();
        int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
        Color[] pixels = InternalEditorUtility.ReadScreenPixel(r.position, w, h);
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, file), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        Debug.Log("StayOnPlatform: captured " + file);
    }

    private static void Fail(Exception e)
    {
        SessionState.SetInt(StepKey, 0);
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        File.WriteAllText(DoneFile, "error: " + e);
        Debug.LogException(e);
    }
}
