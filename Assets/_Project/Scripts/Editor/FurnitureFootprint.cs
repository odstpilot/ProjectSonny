using System.Collections.Generic;
using UnityEngine;

// Where a piece of furniture is solid, for both level builders (FloorOneBuilder, TutorialLevelBuilder): only the part of
// it that stands on the floor, as it's drawn, so the player (and the crew) can walk right up to it, round it, and
// behind the top of anything tall, and are never stopped by empty floor inside its tiles.
//
// It's worked out from the piece's tiles, a column at a time. Each column is solid on the lowest tile it has: a band
// from a little above that tile's bottom (Fit.bottom) up to Fit.depth. So a desk against the wall, whose bottom row is
// empty knee space where the chairs go, is solid only along the desk itself; and its angled end pieces, which reach
// the floor, are solid down there. Columns next to each other at the same height are one box, pulled in a little at
// each end (Fit.inset) so the corners of the art don't snag.
// Boxes come back in tile units, from the piece's bottom-left corner: x to the right, y up from its base.
static class FurnitureFootprint
{
    public struct Fit
    {
        public float inset;     // pulled in at each end of a run of columns
        public float bottom;    // from the bottom of the lowest tile up to where the solid part starts
        public float depth;     // how deep the solid part is, front to back
        public Fit(float inset, float bottom, float depth) { this.inset = inset; this.bottom = bottom; this.depth = depth; }
    }

    // Measured against the art (with the capture tool's "colliders" overlay). Anything not listed gets Default.
    static readonly Fit Default = new Fit(0.15f, 0.1f, 0.8f);
    static readonly Dictionary<string, Fit> Fits = new Dictionary<string, Fit>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "chair", new Fit(0.25f, 0.05f, 0.45f) },
        // The glass-topped desk: its top runs from 0.4 to 1.4 above its base; the chair tucks in under the front.
        { "bench", new Fit(0.2f, 0.35f, 0.85f) },
        // The pedestal table: its foot and the front of its top. The back of the top can be walked behind.
        { "sofa", new Fit(0.25f, 0.1f, 0.95f) },
        { "beds", new Fit(0.1f, 0.05f, 1f) },
        // The pods: the lower part; the top of the dome can be walked behind.
        { "pod", new Fit(0.15f, 0.05f, 1.35f) },
        // Desks against the wall: the desk is the tile row above the knee space, and the band is on it.
        { "console bank", new Fit(0.3f, 0.25f, 0.7f) },
        { "long console bank", new Fit(0.3f, 0.25f, 0.7f) },
        { "counter", new Fit(0.1f, 0.2f, 0.75f) },
        { "monitor bank", new Fit(0.15f, 0.1f, 0.6f) },
        { "control desk", new Fit(0.15f, 0.1f, 0.6f) },
        { "wall pipe", new Fit(0.3f, 0f, 0.45f) },
        { "railing", new Fit(0f, 0.3f, 0.35f) },
    };

    // The piece's columns in runs that reach the floor at the same height: first and last column, and how far up from
    // the base (in tiles) their lowest tile is. rows as the builders keep them: top row first, -1 where there's nothing;
    // wallRows at the top hang on the wall and aren't counted.
    public static List<(int start, int end, int lowest)> Runs(int[][] rows, int wallRows = 0)
    {
        int width = 0;
        foreach (int[] row in rows) width = Mathf.Max(width, row.Length);

        // For each column, how far up from the base its lowest tile is (in tiles), or -1 for none.
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

    // Puts the boxes on the piece as colliders. lift is how far its sorting point sits above its base (the piece's
    // origin is at its bottom-left corner, lifted that far).
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
