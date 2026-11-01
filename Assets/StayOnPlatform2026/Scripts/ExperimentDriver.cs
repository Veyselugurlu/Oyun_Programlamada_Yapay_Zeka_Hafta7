using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Command-line switches for a standalone build; without them the component does nothing.
///   -experiment &lt;csv&gt;    append one row per generation to &lt;csv&gt; and quit after -generations
///   -condition id  -seed 2026  -mutation 0.1  -fitnessWeight 0.55  -inherit 1  -generations 30
///   -botsCsv &lt;csv&gt;       also append one row per bot (genes, time alive, distance, fitness)
///   -recordFrames &lt;dir&gt;  save every -recordEvery-th frame as PNG, -recordCount frames in total
/// Game time advances by a fixed step per frame (Time.captureDeltaTime), so a run is reproducible
/// for a given seed and an experiment runs as fast as the machine allows.
/// </summary>
[DefaultExecutionOrder(-100)] // configure PopulationManager before its Awake seeds Random
public sealed class ExperimentDriver : MonoBehaviour
{
    [SerializeField] private PopulationManager manager;

    private string csvPath, botsCsvPath, condition;
    private int generations = 30;
    private readonly StringBuilder rows = new StringBuilder();
    private readonly StringBuilder botRows = new StringBuilder();

    private const string Header =
        "condition,seed,mutation,fitness_weight,inherit,generation,mean_fitness,std_fitness,best_fitness," +
        "survivors,mean_time_alive,mean_distance,best_dna,n00,n01,n02,n10,n11,n12,n20,n21,n22";
    private const string BotHeader = "condition,seed,generation,g0,g1,alive,time_alive,distance,fitness";

    private void Awake()
    {
        string[] args = Environment.GetCommandLineArgs();
        csvPath = Value(args, "-experiment");
        string recordDir = Value(args, "-recordFrames");
        if (string.IsNullOrEmpty(csvPath) && string.IsNullOrEmpty(recordDir))
        {
            enabled = false;
            return;
        }
        if (manager == null) manager = FindAnyObjectByType<PopulationManager>();
        if (manager == null) { Debug.LogError("ExperimentDriver: no PopulationManager in the scene.", this); return; }

        if (TryInt(Value(args, "-seed"), out int seed)) { manager.fixedSeed = true; manager.seed = seed; }
        if (TryFloat(Value(args, "-mutation"), out float mutation)) manager.mutationRate = mutation;
        if (TryInt(Value(args, "-inherit"), out int inherit)) manager.inheritGenes = inherit != 0;
        // Each bot is a copy of the prefab, so the weight is set once on the prefab object loaded in memory.
        if (TryFloat(Value(args, "-fitnessWeight"), out float weight))
            manager.botPrefab.GetComponent<Brain>().movingFitnessWeight = weight;
        if (TryInt(Value(args, "-generations"), out int g)) generations = Mathf.Max(1, g);
        condition = Value(args, "-condition") ?? "default";
        botsCsvPath = Value(args, "-botsCsv");

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        if (!string.IsNullOrEmpty(recordDir))
        {
            Time.captureDeltaTime = 1f / 30f; // smooth video at 30 fps of game time, however slow the capture
            int every = TryInt(Value(args, "-recordEvery"), out int e) ? e : 6;
            int count = TryInt(Value(args, "-recordCount"), out int c) ? c : 400;
            StartCoroutine(Record(recordDir, every, count));
        }
        else
        {
            Time.captureDeltaTime = Time.fixedDeltaTime; // exactly one physics step per frame
        }
        if (!string.IsNullOrEmpty(csvPath)) manager.GenerationEvaluated += OnGeneration;
    }

    private void OnDestroy()
    {
        if (manager != null) manager.GenerationEvaluated -= OnGeneration;
    }

    private void OnGeneration(int generation, IReadOnlyList<Brain> sorted)
    {
        float weight = sorted[0].movingFitnessWeight;
        float[] fitness = sorted.Select(b => b.Fitness).ToArray();
        float mean = fitness.Average();
        float std = Mathf.Sqrt(fitness.Select(f => (f - mean) * (f - mean)).Average());
        var counts = new int[9];
        foreach (Brain b in sorted) counts[b.dna.GetGene(0) * 3 + b.dna.GetGene(1)]++;

        rows.Append(string.Join(",", condition, manager.seed, F(manager.mutationRate), F(weight),
            manager.inheritGenes ? 1 : 0, generation, F(mean), F(std), F(fitness[0]),
            sorted.Count(b => b.isAlive), F(sorted.Average(b => b.timeAlive)), F(sorted.Average(b => b.distanceMoved)),
            sorted[0].dna.GetGene(0) + "-" + sorted[0].dna.GetGene(1), string.Join(",", counts))).Append('\n');

        if (!string.IsNullOrEmpty(botsCsvPath))
            foreach (Brain b in sorted)
                botRows.Append(string.Join(",", condition, manager.seed, generation, b.dna.GetGene(0), b.dna.GetGene(1),
                    b.isAlive ? 1 : 0, F(b.timeAlive), F(b.distanceMoved), F(b.Fitness))).Append('\n');

        if (generation < generations) return;
        Append(csvPath, Header, rows);
        if (!string.IsNullOrEmpty(botsCsvPath)) Append(botsCsvPath, BotHeader, botRows);
        Debug.Log($"ExperimentDriver: {condition} seed {manager.seed} finished after {generation} generations");
        Application.Quit();
    }

    // One append per run, after the last generation: an interrupted run leaves no partial rows.
    private static void Append(string path, string header, StringBuilder body)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        Directory.CreateDirectory(dir);
        string text = (File.Exists(path) ? "" : header + "\n") + body;
        File.AppendAllText(path, text, new UTF8Encoding(false));
    }

    private static IEnumerator Record(string dir, int every, int count)
    {
        Directory.CreateDirectory(dir);
        for (int saved = 0, frame = 0; saved < count; frame++)
        {
            yield return new WaitForEndOfFrame();
            if (frame % every != 0) continue;
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, $"frame_{saved:0000}.png"), shot.EncodeToPNG());
            Destroy(shot);
            saved++;
        }
        Application.Quit();
    }

    private static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    private static bool TryFloat(string s, out float v) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    private static bool TryInt(string s, out int v) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);

    private static string Value(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
