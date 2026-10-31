using System.Collections.Generic;
using UnityEngine;

// which part of furniture is solid
static class FurnitureFootprint
{
    public struct Fit
    {
        public float inset;
        public float bottom;
        public float depth;
        public Fit(float inset, float bottom, float depth) { this.inset = inset; this.bottom = bottom; this.depth = depth; }
    }

    static readonly Fit Default = new Fit(0.15f, 0.1f, 0.8f);
    static readonly Dictionary<string, Fit> Fits = new Dictionary<string, Fit>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "chair", new Fit(0.25f, 0.05f, 0.45f) },
        { "bench", new Fit(0.2f, 0.35f, 0.85f) },
        { "sofa", new Fit(0.25f, 0.1f, 0.95f) },
        { "beds", new Fit(0.1f, 0.05f, 1f) },
        { "pod", new Fit(0.15f, 0.05f, 1.35f) },
        { "console bank", new Fit(0.3f, 0.25f, 0.7f) },
        { "long console bank", new Fit(0.3f, 0.25f, 0.7f) },
        { "counter", new Fit(0.1f, 0.2f, 0.75f) },
        { "monitor bank", new Fit(0.15f, 0.1f, 0.6f) },
        { "control desk", new Fit(0.15f, 0.1f, 0.6f) },
        { "wall pipe", new Fit(0.3f, 0f, 0.45f) },
        { "railing", new Fit(0f, 0.3f, 0.35f) },
    };

    public static List<(int start, int end, int lowest)> Runs(int[][] rows, int wallRows = 0)
    {
        int width = 0;
        foreach (int[] row in rows) width = Mathf.Max(width, row.Length);

        var lowest = new int[width];
        for (int x = 0; x < width; x++)
        {
            lowest[x] = -1;
            for (int y = rows.Length - 1; y >= wallRows; y--)
            {
                if (x >= rows[y].Length || rows[y][x] < 0) continue;
                lowest[x] = rows.Length - 1 - y;
                break;
            }
        }

        var runs = new List<(int, int, int)>();
        int start = 0;
        while (start < width)
        {
            if (lowest[start] < 0) { start++; continue; }
            int end = start;
            while (end + 1 < width && lowest[end + 1] == lowest[start]) end++;
            runs.Add((start, end, lowest[start]));
            start = end + 1;
        }
        return runs;
    }

    public static List<Rect> Boxes(string name, int[][] rows, int wallRows = 0)
    {
        Fit fit = Fits.TryGetValue(name, out Fit found) ? found : Default;
        int height = rows.Length - wallRows;
        var boxes = new List<Rect>();
        foreach ((int start, int end, int lowest) in Runs(rows, wallRows))
        {
            float left = start + fit.inset, right = end + 1 - fit.inset;
            float floorY = lowest + fit.bottom;
            float depth = Mathf.Min(fit.depth, height - floorY);
            if (right > left && depth > 0.05f) boxes.Add(new Rect(left, floorY, right - left, depth));
        }
        return boxes;
    }

    public static void AddColliders(GameObject piece, string name, int[][] rows, int wallRows, float lift)
    {
        foreach (Rect box in Boxes(name, rows, wallRows))
        {
            var foot = piece.AddComponent<BoxCollider2D>();
            foot.size = box.size;
            foot.offset = new Vector2(box.center.x, box.center.y - lift);
        }
    }
}
