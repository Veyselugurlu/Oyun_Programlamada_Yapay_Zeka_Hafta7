using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks the DNA operators without Play mode (menu: Tools / AI Games Week 3 / Run DNA Checks,
/// batch: -executeMethod DnaChecks.RunBatch) and writes docs/week07/data/dna_checks.json:
///   crossover of A = [0,1] and B = [2,2]; outcomes of mutating [0,2] at p = 0, 0.10, 0.30 and 1.
/// </summary>
public static class DnaChecks
{
    private const string OutputPath = "docs/week07/data/dna_checks.json";
    private const int Trials = 100000;

    [MenuItem("Tools/AI Games Week 3/Run DNA Checks")]
    public static void Run()
    {
        Random.InitState(2026);
        var json = new StringBuilder("{\n");
        DNA a = new DNA(0, 1), b = new DNA(2, 2);
        json.Append($"  \"cross_A01_B22\": \"{DNA.Cross(a, b)}\",\n");
        json.Append($"  \"cross_B22_A01\": \"{DNA.Cross(b, a)}\",\n");
        json.Append($"  \"mutation_trials\": {Trials},\n  \"mutate_02\": {{\n");

        float[] rates = { 0f, 0.10f, 0.30f, 1f };
        for (int r = 0; r < rates.Length; r++)
        {
            var counts = new int[9];
            for (int t = 0; t < Trials; t++)
            {
                var dna = new DNA(0, 2);
                dna.Mutate(rates[r]);
                counts[dna.GetGene(0) * 3 + dna.GetGene(1)]++;
            }
            string cells = string.Join(", ", Enumerable.Range(0, 9).Where(i => counts[i] > 0)
                .Select(i => $"\"[{i / 3}, {i % 3}]\": {((float)counts[i] / Trials).ToString("0.0000", CultureInfo.InvariantCulture)}"));
            json.Append($"    \"{rates[r].ToString("0.00", CultureInfo.InvariantCulture)}\": {{ {cells} }}");
            json.Append(r < rates.Length - 1 ? ",\n" : "\n");
        }
        json.Append("  }\n}\n");

        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        File.WriteAllText(OutputPath, json.ToString(), new UTF8Encoding(false));
        Debug.Log("StayOnPlatform: DNA checks written -> " + OutputPath + "\n" + json);
    }

    public static void RunBatch()
    {
        int code = 0;
        try { Run(); }
        catch (System.Exception e) { Debug.LogException(e); code = 1; }
        EditorApplication.Exit(code);
    }
}
