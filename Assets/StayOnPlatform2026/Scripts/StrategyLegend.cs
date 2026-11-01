using UnityEngine;

/// <summary>Draws the colour legend of GenomeColors in the lower-left corner of the Game view.</summary>
public sealed class StrategyLegend : MonoBehaviour
{
    private GUIStyle labelStyle;

    private void OnGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            labelStyle.normal.textColor = Color.white;
        }
        int rows = GenomeColors.Labels.Length;
        float top = Screen.height - 12 - (rows * 22 + 34);
        GUI.Box(new Rect(12, top, 310, rows * 22 + 34), "DNA [ground, edge]   0 fwd · 1 left · 2 right");
        for (int i = 0; i < rows; i++)
        {
            float y = top + 26 + i * 22;
            Color previous = GUI.color;
            GUI.color = GenomeColors.Colors[i];
            GUI.DrawTexture(new Rect(22, y + 3, 14, 14), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(44, y, 150, 22), GenomeColors.Labels[i], labelStyle);
            GUI.Label(new Rect(200, y, 115, 22), GenomeColors.Genes[i], labelStyle);
        }
    }
}
