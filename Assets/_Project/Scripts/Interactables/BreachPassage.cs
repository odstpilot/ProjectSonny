using UnityEngine;
using UnityEngine.Tilemaps;

// A stretch of wall that gets blown through, leaving a way on that wasn't there before: until Open, its cells are wall
// like any other; opened, they're floor, the walls and solid black on them cleared, so the player (and anything after
// them) can walk through. What does the blowing through is up to whoever opens it (TutorialDirector: a robot punches
// through from the other side when the door's jammed).
// Built by TutorialLevelBuilder from the # cells in the layout, which it builds as wall.
public class BreachPassage : MonoBehaviour
{
    public Tilemap floor;
    [Tooltip("The tilemaps to clear the cells from: the walls, the solid black past them, anything overhead.")]
    public Tilemap[] cleared = new Tilemap[0];
    public Vector3Int[] cells = new Vector3Int[0];
    [Tooltip("The floor laid on each cell once it's open, in the same order as cells.")]
    public TileBase[] floorTiles = new TileBase[0];

    public bool IsOpen { get; private set; }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        for (int i = 0; i < cells.Length; i++)
        {
            foreach (Tilemap map in cleared)
                if (map != null) map.SetTile(cells[i], null);
            if (floor != null && i < floorTiles.Length && floorTiles[i] != null) floor.SetTile(cells[i], floorTiles[i]);
        }
    }
}
