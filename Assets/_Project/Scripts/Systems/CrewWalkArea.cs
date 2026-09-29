using System.Collections.Generic;
using UnityEngine;

// The floor of one room as a grid of cells the crew can walk on: open floor, clear of walls, furniture, doorways, and
// anyone standing still. Crew wandering the room pick somewhere on it to go and find the shortest way there, stepping
// diagonally only where neither side is blocked, so nobody cuts a corner through a desk. Built by ChapterOneBuilder.
public class CrewWalkArea : MonoBehaviour
{
    [Tooltip("World position of the bottom-left corner of cell (0, 0).")]
    public Vector2 origin;
    public int width;
    public int height;
    [Tooltip("Whether each cell can be walked on, a row at a time from the bottom.")]
    public bool[] walkable = new bool[0];
    [Tooltip("How far above a cell's center someone standing in it is, so their feet are in the cell (their sprite is centered).")]
    public float standHeight = 0.24f;

    static readonly Vector2Int[] Steps =
    {
        Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down,
        new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1)
    };

    private readonly List<Vector2Int> open = new List<Vector2Int>();
    private readonly Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
    private readonly Queue<Vector2Int> frontier = new Queue<Vector2Int>();

    public bool IsWalkable(Vector2Int cell) =>
        cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height && walkable[cell.y * width + cell.x];

    // The cell someone standing here has their feet in, and where they stand to be in a cell.
    public Vector2Int CellAt(Vector2 standing) => Vector2Int.FloorToInt(standing - origin - new Vector2(0f, standHeight));

    public Vector2 CenterOf(Vector2Int cell) => origin + cell + new Vector2(0.5f, 0.5f + standHeight);

    // Somewhere walkable at least minDistance cells from here, or anywhere walkable if nowhere is that far.
    public bool TryPickDestination(Vector2Int from, float minDistance, out Vector2Int destination)
    {
        if (open.Count == 0)
        {
            for (int i = 0; i < walkable.Length; i++)
                if (walkable[i]) open.Add(new Vector2Int(i % width, i / width));
        }
        destination = from;
        if (open.Count == 0) return false;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            destination = open[Random.Range(0, open.Count)];
            if (Vector2Int.Distance(destination, from) >= minDistance) return true;
        }
        return true;
    }

    // The way from one cell to another as world points, one per turn, not counting where it starts. False if there's
    // no way through.
    public bool FindPath(Vector2Int from, Vector2Int to, List<Vector2> path)
    {
        path.Clear();
        if (!IsWalkable(to)) return false;

        cameFrom.Clear();
        frontier.Clear();
        cameFrom[from] = from;
        frontier.Enqueue(from);
        while (frontier.Count > 0 && !cameFrom.ContainsKey(to))
        {
            Vector2Int cell = frontier.Dequeue();
            foreach (Vector2Int step in Steps)
            {
                Vector2Int next = cell + step;
                if (cameFrom.ContainsKey(next) || !IsWalkable(next)) continue;
                bool diagonal = step.x != 0 && step.y != 0;
                if (diagonal && (!IsWalkable(cell + new Vector2Int(step.x, 0)) || !IsWalkable(cell + new Vector2Int(0, step.y)))) continue;
                cameFrom[next] = cell;
                frontier.Enqueue(next);
            }
        }
        if (!cameFrom.ContainsKey(to)) return false;

        // Walk it back, keeping only the cells where the direction changes.
        var cells = new List<Vector2Int>();
        for (Vector2Int cell = to; cell != from; cell = cameFrom[cell]) cells.Add(cell);
        cells.Reverse();
        for (int i = 0; i < cells.Count; i++)
        {
            bool last = i == cells.Count - 1;
            Vector2Int previous = i == 0 ? from : cells[i - 1];
            if (!last && cells[i + 1] - cells[i] == cells[i] - previous) continue;
            path.Add(CenterOf(cells[i]));
        }
        return true;
    }

    void OnDrawGizmosSelected()
    {
        if (walkable == null || walkable.Length != width * height) return;
        Gizmos.color = new Color(0.3f, 0.9f, 0.5f, 0.25f);
        for (int i = 0; i < walkable.Length; i++)
            if (walkable[i]) Gizmos.DrawCube(CenterOf(new Vector2Int(i % width, i / width)), new Vector3(0.9f, 0.9f, 0f));
    }
}
