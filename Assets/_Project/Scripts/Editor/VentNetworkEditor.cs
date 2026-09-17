using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// The VentNetwork inspector: the duct map as a grid of squares to paint, instead of a block of text.
// Pick a brush and click or drag over the grid; right-click paints wall. Width and Height grow or shrink the map from
// its bottom right corner. Under the grid, anything wrong with the map is listed: grates that can't be reached,
// squeezes with room to turn in them, and grates on the map with no Vent Grate in the scene to match.
// The map is still kept as text in the layout field, one character per cell, so nothing else had to change.
[CustomEditor(typeof(VentNetwork))]
public class VentNetworkEditor : Editor
{
    const int MinSize = 3;
    const int MaxSize = 60;
    const float MinCellPixels = 10f;
    const float MaxCellPixels = 26f;

    enum Brush { Wall, Duct, Dent, Squeeze, Hatch, Grate }

    static readonly GUIContent[] BrushLabels =
    {
        new GUIContent("Wall", "Solid. Everything off the map counts as wall too."),
        new GUIContent("Duct", "Open duct to crawl through."),
        new GUIContent("Dent", "A dented panel: bangs when crawled over, unless the player crawls carefully."),
        new GUIContent("Squeeze", "A tight squeeze: one key at a time to get through. Keep squeezes to straight runs."),
        new GUIContent("Hatch", "Where the drone comes out when the noise fills up."),
        new GUIContent("Grate", "A way in and out. It connects to the Vent Grate in the scene with the same number."),
    };

    static readonly Color WallColor = new Color(0.13f, 0.14f, 0.16f);
    static readonly Color DuctColor = new Color(0.72f, 0.75f, 0.78f);
    static readonly Color DentColor = new Color(0.86f, 0.56f, 0.26f);
    static readonly Color SqueezeColor = new Color(0.95f, 0.8f, 0.25f);
    static readonly Color HatchColor = new Color(0.8f, 0.22f, 0.2f);
    static readonly Color GrateColor = new Color(0.36f, 0.74f, 0.4f);
    static readonly Color GridLineColor = new Color(0.05f, 0.05f, 0.06f);
    static readonly Color ButtonColor = new Color(0f, 0f, 0f, 0.15f);
    static readonly Color SelectedColor = new Color(0.24f, 0.48f, 0.9f, 0.55f);

    // Static, so the brush stays picked after selecting something else and coming back.
    static Brush brush = Brush.Duct;
    static int grateNumber = 1;

    private SerializedProperty layoutProperty;
    private int paintGroup = -1;
    private GUIStyle cellLabel;
    private GUIStyle brushLabel;

    void OnEnable()
    {
        layoutProperty = serializedObject.FindProperty("layout");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        MakeStyles();

        char[,] map = Parse(layoutProperty.stringValue);
        EditorGUILayout.LabelField("Ducts", EditorStyles.boldLabel);
        DrawBrushes();
        map = DrawSize(map);
        DrawGrid(map);
        DrawProblems(map);

        EditorGUILayout.Space();
        DrawPropertiesExcluding(serializedObject, "m_Script", "layout");
        serializedObject.ApplyModifiedProperties();
    }

    // --- Brushes ---

    void DrawBrushes()
    {
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < BrushLabels.Length; i++)
        {
            var kind = (Brush)i;
            Rect button = GUILayoutUtility.GetRect(BrushLabels[i], EditorStyles.miniButton, GUILayout.Height(40f), GUILayout.MinWidth(48f));
            if (GUI.Button(button, new GUIContent("", BrushLabels[i].tooltip), GUIStyle.none)) brush = kind;

            if (Event.current.type != EventType.Repaint) continue;
            EditorGUI.DrawRect(button, brush == kind ? SelectedColor : ButtonColor);
            var swatch = new Rect(button.center.x - 9f, button.y + 4f, 18f, 18f);
            char mark = MarkFor(kind);
            EditorGUI.DrawRect(swatch, ColorOf(mark));
            DrawCellLabel(swatch, mark, 11);
            GUI.Label(new Rect(button.x, button.y + 22f, button.width, 16f), BrushLabels[i].text, brushLabel);
        }
        EditorGUILayout.EndHorizontal();

        if (brush == Brush.Grate)
            grateNumber = EditorGUILayout.IntSlider(new GUIContent("Grate Number", "Which Vent Grate in the scene this grate connects to."), grateNumber, 1, 9);
        EditorGUILayout.LabelField("Click or drag to paint. Right-click paints wall.", EditorStyles.miniLabel);
    }

    static char MarkFor(Brush kind)
    {
        switch (kind)
        {
            case Brush.Duct: return '.';
            case Brush.Dent: return 'd';
            case Brush.Squeeze: return 's';
            case Brush.Hatch: return 'r';
            case Brush.Grate: return (char)('0' + grateNumber);
            default: return '#';
        }
    }

    // --- Size ---

    char[,] DrawSize(char[,] map)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        EditorGUILayout.BeginHorizontal();
        float labelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 44f;
        int newWidth = Mathf.Clamp(EditorGUILayout.DelayedIntField("Width", width), MinSize, MaxSize);
        int newHeight = Mathf.Clamp(EditorGUILayout.DelayedIntField("Height", height), MinSize, MaxSize);
        EditorGUIUtility.labelWidth = labelWidth;

        if (GUILayout.Button(new GUIContent("Clear", "Make every cell wall."), EditorStyles.miniButton, GUILayout.Width(50f))
            && EditorUtility.DisplayDialog("Clear the vent map?", "Every cell becomes wall.", "Clear", "Cancel"))
        {
            map = Filled(width, height);
            Store(map);
        }
        if (GUILayout.Button(new GUIContent("Example", "Replace the map with the example maze."), EditorStyles.miniButton, GUILayout.Width(62f))
            && EditorUtility.DisplayDialog("Use the example maze?", "The map is replaced with the example maze.", "Replace", "Cancel"))
        {
            map = Parse(VentNetwork.ExampleLayout);
            Store(map);
        }
        EditorGUILayout.EndHorizontal();

        if (newWidth == map.GetLength(0) && newHeight == map.GetLength(1)) return map;

        // Keeps what's already painted from the top left, and fills anything new with wall.
        char[,] resized = Filled(newWidth, newHeight);
        for (int x = 0; x < Mathf.Min(newWidth, map.GetLength(0)); x++)
            for (int row = 0; row < Mathf.Min(newHeight, map.GetLength(1)); row++)
                resized[x, row] = map[x, row];
        Store(resized);
        return resized;
    }

    // --- The grid ---

    void DrawGrid(char[,] map)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);
        float available = EditorGUIUtility.currentViewWidth - 40f;
        float cell = Mathf.Clamp(Mathf.Floor(available / width), MinCellPixels, MaxCellPixels);

        Rect area = GUILayoutUtility.GetRect(width * cell, height * cell, GUILayout.ExpandWidth(false));
        area.width = width * cell;
        area.height = height * cell;

        Event current = Event.current;
        int control = GUIUtility.GetControlID(FocusType.Passive);
        switch (current.GetTypeForControl(control))
        {
            case EventType.MouseDown when area.Contains(current.mousePosition) && (current.button == 0 || current.button == 1):
                // One drag is one undo.
                GUIUtility.hotControl = control;
                Undo.IncrementCurrentGroup();
                paintGroup = Undo.GetCurrentGroup();
                Paint(map, area, cell, current);
                current.Use();
                break;

            case EventType.MouseDrag when GUIUtility.hotControl == control:
                Paint(map, area, cell, current);
                current.Use();
                break;

            case EventType.MouseUp when GUIUtility.hotControl == control:
                GUIUtility.hotControl = 0;
                if (paintGroup >= 0) Undo.CollapseUndoOperations(paintGroup);
                paintGroup = -1;
                current.Use();
                break;

            case EventType.Repaint:
                EditorGUI.DrawRect(area, GridLineColor);
                int fontSize = Mathf.RoundToInt(cell * 0.55f);
                for (int row = 0; row < height; row++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var square = new Rect(area.x + x * cell, area.y + row * cell, cell - 1f, cell - 1f);
                        EditorGUI.DrawRect(square, ColorOf(map[x, row]));
                        if (cell >= 12f) DrawCellLabel(square, map[x, row], fontSize);
                    }
                }
                break;
        }
    }

    void Paint(char[,] map, Rect area, float cell, Event current)
    {
        int x = Mathf.FloorToInt((current.mousePosition.x - area.x) / cell);
        int row = Mathf.FloorToInt((current.mousePosition.y - area.y) / cell);
        if (x < 0 || row < 0 || x >= map.GetLength(0) || row >= map.GetLength(1)) return;

        char mark = current.button == 1 ? '#' : MarkFor(brush);
        if (map[x, row] == mark) return;
        map[x, row] = mark;
        Store(map);
        GUI.changed = true;
    }

    static Color ColorOf(char mark)
    {
        switch (mark)
        {
            case '.': return DuctColor;
            case 'd': return DentColor;
            case 's': return SqueezeColor;
            case 'r': return HatchColor;
            default: return mark >= '1' && mark <= '9' ? GrateColor : WallColor;
        }
    }

    // A letter on the cells that aren't plain wall or duct, so they read without relying on colour alone.
    void DrawCellLabel(Rect square, char mark, int fontSize)
    {
        string text = mark == 'd' ? "D" : mark == 's' ? "S" : mark == 'r' ? "R" : mark >= '1' && mark <= '9' ? mark.ToString() : null;
        if (text == null) return;
        cellLabel.fontSize = fontSize;
        cellLabel.normal.textColor = mark == 'r' ? Color.white : new Color(0.08f, 0.08f, 0.1f);
        GUI.Label(square, text, cellLabel);
    }

    // --- Problems ---

    void DrawProblems(char[,] map)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);
        var warnings = new List<string>();
        var notes = new List<string>();

        var grates = new SortedDictionary<int, Vector2Int>();
        var hatches = new List<Vector2Int>();
        for (int row = 0; row < height; row++)
        {
            for (int x = 0; x < width; x++)
            {
                char mark = map[x, row];
                if (mark == 'r') hatches.Add(new Vector2Int(x, row));
                if (mark < '1' || mark > '9') continue;

                int number = mark - '0';
                if (grates.ContainsKey(number)) warnings.Add($"There are two grate {number}s on the map. Only one of them is used.");
                else grates[number] = new Vector2Int(x, row);
            }
        }

        if (grates.Count == 0)
        {
            warnings.Add("There's no grate on the map, so there's no way in. Paint one with the Grate brush.");
        }
        else
        {
            int first = 0;
            foreach (int number in grates.Keys)
            {
                first = number;
                break;
            }
            bool[,] reached = Flood(map, grates[first]);
            foreach (KeyValuePair<int, Vector2Int> grate in grates)
                if (!reached[grate.Value.x, grate.Value.y]) warnings.Add($"Grate {grate.Key} can't be reached from grate {first}.");
            foreach (Vector2Int hatch in hatches)
                if (!reached[hatch.x, hatch.y]) warnings.Add($"The hatch at {Where(hatch)} can't be reached from grate {first}, so the drone can't get out of it.");
        }

        for (int row = 0; row < height; row++)
        {
            for (int x = 0; x < width; x++)
            {
                if (map[x, row] != 's') continue;
                bool left = IsOpen(map, x - 1, row), right = IsOpen(map, x + 1, row);
                bool up = IsOpen(map, x, row - 1), down = IsOpen(map, x, row + 1);
                bool straight = (left && right && !up && !down) || (up && down && !left && !right);
                if (!straight) warnings.Add($"The squeeze at {Where(new Vector2Int(x, row))} should be in a straight run: open on two opposite sides, wall on the other two.");
            }
        }

        if (hatches.Count == 0) notes.Add("No drone hatches: when the noise fills up, the drone comes out of the dark somewhere a few cells away.");
        FindSceneProblems(grates, warnings, notes);

        foreach (string warning in warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        foreach (string note in notes) EditorGUILayout.HelpBox(note, MessageType.Info);
    }

    // Grates on the map with no Vent Grate in the scene, and Vent Grates in the scene with no grate on the map.
    void FindSceneProblems(SortedDictionary<int, Vector2Int> grates, List<string> warnings, List<string> notes)
    {
        var network = (VentNetwork)target;
        if (EditorUtility.IsPersistent(network)) return;    // a prefab asset has no scene to look in

        bool onlyNetwork = FindObjectsByType<VentNetwork>().Length == 1;
        var inScene = new HashSet<int>();
        foreach (VentGrate grate in FindObjectsByType<VentGrate>())
        {
            bool linked = grate.network == network || (grate.network == null && onlyNetwork);
            if (!linked) continue;
            inScene.Add(grate.number);
            if (!grates.ContainsKey(grate.number))
                warnings.Add($"{grate.name} in the scene is number {grate.number}, but there's no grate {grate.number} on the map, so it leads nowhere.");
        }
        foreach (int number in grates.Keys)
            if (!inScene.Contains(number))
                notes.Add($"There's no Vent Grate {number} in the scene yet. Climbing out at grate {number} will say it's bolted shut.");
    }

    static bool[,] Flood(char[,] map, Vector2Int from)
    {
        var reached = new bool[map.GetLength(0), map.GetLength(1)];
        var queue = new Queue<Vector2Int>();
        reached[from.x, from.y] = true;
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            Vector2Int at = queue.Dequeue();
            foreach (Vector2Int step in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
            {
                Vector2Int next = at + step;
                if (!IsOpen(map, next.x, next.y) || reached[next.x, next.y]) continue;
                reached[next.x, next.y] = true;
                queue.Enqueue(next);
            }
        }
        return reached;
    }

    static bool IsOpen(char[,] map, int x, int row) =>
        x >= 0 && row >= 0 && x < map.GetLength(0) && row < map.GetLength(1) && map[x, row] != '#';

    // Counting from 1 at the top left, the way the grid reads.
    static string Where(Vector2Int cell) => $"column {cell.x + 1}, row {cell.y + 1}";

    // --- The text underneath ---

    // The layout text as a grid, [column, row] with row 0 at the top. Short rows are filled out with wall, and any
    // character the map doesn't use becomes wall.
    static char[,] Parse(string text)
    {
        var rows = new List<string>((text ?? "").Replace("\r", "").Split('\n'));
        while (rows.Count > 0 && rows[rows.Count - 1].Trim().Length == 0)
            rows.RemoveAt(rows.Count - 1);

        int height = Mathf.Max(MinSize, rows.Count);
        int width = MinSize;
        foreach (string row in rows)
            width = Mathf.Max(width, row.Length);

        char[,] map = Filled(width, height);
        for (int row = 0; row < rows.Count; row++)
        {
            for (int x = 0; x < rows[row].Length; x++)
            {
                char mark = rows[row][x];
                bool known = mark == '.' || mark == 'd' || mark == 's' || mark == 'r' || (mark >= '1' && mark <= '9');
                map[x, row] = known ? mark : '#';
            }
        }
        return map;
    }

    static char[,] Filled(int width, int height)
    {
        var map = new char[width, height];
        for (int x = 0; x < width; x++)
            for (int row = 0; row < height; row++)
                map[x, row] = '#';
        return map;
    }

    void Store(char[,] map)
    {
        var text = new StringBuilder();
        for (int row = 0; row < map.GetLength(1); row++)
        {
            if (row > 0) text.Append('\n');
            for (int x = 0; x < map.GetLength(0); x++)
                text.Append(map[x, row]);
        }
        layoutProperty.stringValue = text.ToString();
    }

    void MakeStyles()
    {
        if (cellLabel != null) return;
        cellLabel = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset() };
        brushLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
    }
}
