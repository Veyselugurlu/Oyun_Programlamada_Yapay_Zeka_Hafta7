using UnityEngine;

/// <summary>
/// Visual only: colours a bot by the strategy its genes [g0, g1] encode and turns it grey when it dies.
/// Gene values: 0 = forward, 1 = left, 2 = right.
/// </summary>
[RequireComponent(typeof(Brain))]
public sealed class GenomeColors : MonoBehaviour
{
    public static readonly string[] Labels = { "Edge turner", "Always forward", "Spinner", "Turn, step off" };
    public static readonly string[] Genes = { "[0, 1|2]", "[0, 0]", "[1|2, 1|2]", "[1|2, 0]" };

    public static readonly Color[] Colors =
    {
        new Color32(0x1b, 0xaf, 0x7a, 0xff), // same hues as the report figures
        new Color32(0xe3, 0x49, 0x48, 0xff),
        new Color32(0xed, 0xa1, 0x00, 0xff),
        new Color32(0x62, 0x50, 0xd6, 0xff),
    };

    private static readonly Color DeadColor = new Color(0.35f, 0.35f, 0.35f);
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    private Brain brain;
    private Renderer bodyRenderer;
    private MaterialPropertyBlock block;
    private bool shownAlive;

    public static int StrategyOf(DNA dna)
    {
        bool forwardOnGround = dna.GetGene(0) == 0;
        bool forwardAtEdge = dna.GetGene(1) == 0;
        if (forwardOnGround) return forwardAtEdge ? 1 : 0;
        return forwardAtEdge ? 3 : 2;
    }

    private void Awake()
    {
        brain = GetComponent<Brain>();
        bodyRenderer = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
    }

    private void Start() => Apply(); // Brain.Init runs right after Instantiate, before Start

    private void Update()
    {
        if (brain.isAlive != shownAlive) Apply();
    }

    private void Apply()
    {
        if (brain.dna == null || bodyRenderer == null) return;
        shownAlive = brain.isAlive;
        block.SetColor(BaseColor, shownAlive ? Colors[StrategyOf(brain.dna)] : DeadColor);
        bodyRenderer.SetPropertyBlock(block);
    }
}
