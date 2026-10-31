using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// flat ui shapes
[RequireComponent(typeof(CanvasRenderer))]
public class HudShape : MaskableGraphic
{
    public List<Vector2> points = new List<Vector2>();
    public bool filled;
    [Tooltip("Joins the last point back to the first.")]
    public bool closed = true;
    public float thickness = 2f;
    [Tooltip("How far a line fades out past each edge.")]
    public float feather;
    [Range(0f, 1f)] public float drawStart;
    [Range(0f, 1f)] public float drawAmount = 1f;

    public float DrawStart
    {
        get => drawStart;
        set
        {
            value = Mathf.Clamp01(value);
            if (drawStart == value) return;
            drawStart = value;
            SetVerticesDirty();
        }
    }

    public float DrawAmount
    {
        get => drawAmount;
        set
        {
            value = Mathf.Clamp01(value);
            if (drawAmount == value) return;
            drawAmount = value;
            SetVerticesDirty();
        }
    }

    public void SetPoints(IEnumerable<Vector2> newPoints)
    {
        points.Clear();
        points.AddRange(newPoints);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points.Count < (filled ? 3 : 2)) return;
        if (filled) Fill(vh);
        else Stroke(vh);
    }

    void Fill(VertexHelper vh)
    {
        Vector2 middle = Vector2.zero;
        foreach (Vector2 point in points) middle += point;
        middle /= points.Count;

        Color32 tint = color;
        vh.AddVert(middle, tint, Vector4.zero);
        foreach (Vector2 point in points) vh.AddVert(point, tint, Vector4.zero);
        for (int i = 0; i < points.Count; i++)
            vh.AddTriangle(0, 1 + i, 1 + (i + 1) % points.Count);
    }

    void Stroke(VertexHelper vh)
    {
        int segmentCount = closed ? points.Count : points.Count - 1;
        float length = 0f;
        for (int i = 0; i < segmentCount; i++)
            length += Vector2.Distance(points[i], points[(i + 1) % points.Count]);

        float startAt = length * Mathf.Min(drawStart, drawAmount);
        float endAt = length * drawAmount;
        float half = thickness * 0.5f;
        Color32 solid = color;
        Color32 clear = solid;
        clear.a = 0;

        float travelled = 0f;
        for (int i = 0; i < segmentCount && travelled < endAt; i++)
        {
            Vector2 corner = points[i];
            Vector2 next = points[(i + 1) % points.Count];
            float segmentLength = Vector2.Distance(corner, next);
            float segmentStart = travelled;
            travelled += segmentLength;
            if (segmentLength < 0.001f || travelled <= startAt) continue;
            Vector2 along = (next - corner) / segmentLength;
            Vector2 from = corner + along * Mathf.Max(0f, startAt - segmentStart);
            Vector2 to = corner + along * Mathf.Min(segmentLength, endAt - segmentStart);

            from -= along * half;
            to += along * half;
            Vector2 side = new Vector2(-along.y, along.x);
            Vector2 inner = side * half;
            Vector2 outer = side * (half + feather);
            Quad(vh, from - inner, to - inner, to + inner, from + inner, solid, solid);
            if (feather > 0f)
            {
                Quad(vh, from + inner, to + inner, to + outer, from + outer, solid, clear);
                Quad(vh, from - inner, to - inner, to - outer, from - outer, solid, clear);
            }
        }
    }

    static void Quad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color32 near, Color32 far)
    {
        int start = vh.currentVertCount;
        vh.AddVert(p0, near, Vector4.zero);
        vh.AddVert(p1, near, Vector4.zero);
        vh.AddVert(p2, far, Vector4.zero);
        vh.AddVert(p3, far, Vector4.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
