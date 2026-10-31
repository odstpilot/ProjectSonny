using UnityEngine;
using UnityEngine.Tilemaps;

// wall that gets blown open
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
