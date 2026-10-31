using System.Collections.Generic;
using UnityEngine;

// arcs showing who sees the player
public class StealthMeter : MonoBehaviour
{
    const string SortingLayerName = "Player";
    const int SortingOrder = -1;
    const int ArcPoints = 24;
    const int MaxArcs = 8;
    const int SignSortingOrder = 60;
    const float SignPixelsPerUnit = 26f;
    const float SignPopTime = 0.18f;
    const float SignLift = 0.28f;
    const float SignAt = 0.35f;

    const float Radius = 1.15f;
    const float GroundSquash = 0.5f;
    const float GroundDrop = -0.15f;
    const float NarrowArc = 9f;
    const float WideArc = 30f;
    const float FadeSpeed = 5f;
    const float CrouchedSize = 0.72f;
    const float SprintingSize = 1.3f;
    const float ResizeSpeed = 4f;

    static readonly Color CalmColor = new Color(0.85f, 0.92f, 1f);
    static readonly Color WarningColor = new Color(1f, 0.75f, 0.25f);
    static readonly Color AlarmColor = new Color(1f, 0.25f, 0.15f);

    static readonly string[] AlertArt = { "###", "###", "###", "###", "###", "...", "###", "###" };
    static readonly string[] SearchArt = { ".###.", "##.##", "...##", "..##.", "..#..", "..#..", ".....", "..#.." };

    static StealthMeter instance;
    static Material arcMaterial;
    static Sprite alertSign;
    static Sprite searchSign;

    static Sprite AlertSign => alertSign != null ? alertSign : (alertSign = MakeSign(AlertArt));
    static Sprite SearchSign => searchSign != null ? searchSign : (searchSign = MakeSign(SearchArt));

    class Arc
    {
        public Vector2 watcherPosition;
        public float progress;
        public bool spotted;
        public float alpha;
        public int lastFrame;
        public LineRenderer line;
        public SpriteRenderer sign;
        public Sprite shownSign;
        public float signAge;
    }

    private readonly Dictionary<Object, Arc> arcs = new Dictionary<Object, Arc>();
    private readonly List<Object> finished = new List<Object>();
    private readonly Stack<LineRenderer> spare = new Stack<LineRenderer>();
    private readonly Vector3[] arcPoints = new Vector3[ArcPoints];
    private readonly Stack<SpriteRenderer> spareSigns = new Stack<SpriteRenderer>();
    private AnimationCurve taper;
    private Transform player;
    private PlayerController playerController;
    private float size = 1f;
    private int linesMade;
    private int signsMade;

    public static void Report(Object watcher, Vector2 watcherPosition, float progress, bool spotted)
    {
        if (watcher == null || !Application.isPlaying) return;

        StealthMeter meter = Instance;
        if (meter == null) return;

        if (!meter.arcs.TryGetValue(watcher, out Arc arc))
        {
            arc = new Arc();
            meter.arcs[watcher] = arc;
        }

        arc.watcherPosition = watcherPosition;
        arc.progress = Mathf.Clamp01(progress);
        arc.spotted = spotted;
        arc.lastFrame = Time.frameCount;
    }

    public static void Clear(Object watcher)
    {
        if (watcher == null || instance == null) return;
        if (instance.arcs.TryGetValue(watcher, out Arc arc)) arc.progress = 0f;
    }

    static StealthMeter Instance
    {
        get
        {
            if (instance == null && CombatSprites.EffectMaterial != null)
            {
                instance = new GameObject("[StealthMeter]").AddComponent<StealthMeter>();
                instance.Build();
            }
            return instance;
        }
    }

    Transform Player
    {
        get
        {
            if (player == null)
            {
                GameObject found = GameObject.FindGameObjectWithTag("Player");
                if (found != null)
                {
                    player = found.transform;
                    playerController = found.GetComponent<PlayerController>();
                }
            }
            return player;
        }
    }

    void Build()
    {
        if (arcMaterial == null)
            arcMaterial = new Material(CombatSprites.EffectMaterial) { mainTexture = MakeSoftBandTexture() };

        taper = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f));
    }

    void LateUpdate()
    {
        Transform target = Player;
        if (target == null) return;

        Vector2 center = (Vector2)target.position - (playerController != null ? playerController.PoseOffset : Vector2.zero);
        float targetSize = 1f;
        if (playerController != null)
            targetSize = playerController.IsCrouching ? CrouchedSize : playerController.IsSprinting ? SprintingSize : 1f;
        size = Mathf.MoveTowards(size, targetSize, ResizeSpeed * Time.deltaTime);
        finished.Clear();

        foreach (KeyValuePair<Object, Arc> pair in arcs)
        {
            Arc arc = pair.Value;
            bool live = arc.lastFrame >= Time.frameCount - 1 && arc.progress > 0f;
            arc.alpha = Mathf.MoveTowards(arc.alpha, live ? 1f : 0f, FadeSpeed * Time.deltaTime);

            if (arc.alpha <= 0f)
            {
                Release(arc);
                finished.Add(pair.Key);
                continue;
            }

            Draw(arc, center);
        }

        foreach (Object watcher in finished) arcs.Remove(watcher);
    }

    void Draw(Arc arc, Vector2 center)
    {
        if (arc.line == null)
        {
            arc.line = Take();
            if (arc.line == null) return;
        }

        Vector2 toWatcher = arc.watcherPosition - center;
        float bearing = toWatcher.sqrMagnitude > 0.0001f ? Mathf.Atan2(toWatcher.y, toWatcher.x) * Mathf.Rad2Deg : 90f;
        float half = Mathf.Lerp(NarrowArc, WideArc, arc.progress);

        for (int i = 0; i < ArcPoints; i++)
        {
            float across = i / (ArcPoints - 1f);
            float angle = (bearing + Mathf.Lerp(-half, half, across)) * Mathf.Deg2Rad;
            arcPoints[i] = PointOnRing(center, angle);
        }

        Color color = arc.progress < 0.5f
            ? Color.Lerp(CalmColor, WarningColor, arc.progress * 2f)
            : Color.Lerp(WarningColor, AlarmColor, (arc.progress - 0.5f) * 2f);
        float pulse = arc.spotted ? 0.75f + 0.25f * Mathf.Sin(Time.time * 12f) : 1f;
        color.a = Mathf.Lerp(0.35f, 1f, arc.progress) * arc.alpha * pulse;

        arc.line.enabled = true;
        arc.line.SetPositions(arcPoints);
        arc.line.startColor = color;
        arc.line.endColor = color;
        arc.line.widthMultiplier = Mathf.Lerp(0.05f, 0.13f, arc.progress) * (arc.spotted ? pulse : 1f);

        DrawSign(arc, center, bearing, color);
    }

    void DrawSign(Arc arc, Vector2 center, float bearing, Color color)
    {
        Sprite wanted = arc.spotted || arc.progress >= 1f ? AlertSign
            : arc.progress >= SignAt ? SearchSign
            : null;

        if (wanted == null)
        {
            if (arc.sign != null) arc.sign.enabled = false;
            arc.shownSign = null;
            return;
        }

        if (arc.sign == null)
        {
            arc.sign = TakeSign();
            if (arc.sign == null) return;
        }

        if (arc.shownSign != wanted)
        {
            arc.shownSign = wanted;
            arc.sign.sprite = wanted;
            arc.signAge = 0f;
        }
        arc.signAge += Time.deltaTime;

        float pop = arc.signAge / SignPopTime;
        float scale = pop >= 1f ? 1f
            : pop < 0.5f ? Mathf.Lerp(0.4f, 1.35f, pop * 2f)
            : Mathf.Lerp(1.35f, 1f, (pop - 0.5f) * 2f);

        Vector3 at = PointOnRing(center, bearing * Mathf.Deg2Rad);
        at.y += SignLift;

        Color signColor = color;
        signColor.a = Mathf.Clamp01(color.a * 1.6f);

        arc.sign.enabled = true;
        arc.sign.transform.position = at;
        arc.sign.transform.localScale = Vector3.one * scale;
        arc.sign.color = signColor;
    }

    Vector3 PointOnRing(Vector2 center, float angleRadians)
    {
        return new Vector3(
            center.x + Mathf.Cos(angleRadians) * Radius * size,
            center.y + Mathf.Sin(angleRadians) * Radius * size * GroundSquash + GroundDrop,
            0f);
    }

    LineRenderer Take()
    {
        if (spare.Count > 0) return spare.Pop();
        if (linesMade >= MaxArcs) return null;

        linesMade++;
        return NewLine(ArcPoints, false);
    }

    SpriteRenderer TakeSign()
    {
        if (spareSigns.Count > 0) return spareSigns.Pop();
        if (signsMade >= MaxArcs) return null;

        signsMade++;
        var sign = new GameObject("Sign").AddComponent<SpriteRenderer>();
        sign.transform.SetParent(transform, false);
        sign.sortingLayerName = SortingLayerName;
        sign.sortingOrder = SignSortingOrder;
        if (CombatSprites.EffectMaterial != null) sign.sharedMaterial = CombatSprites.EffectMaterial;
        sign.enabled = false;
        return sign;
    }

    void Release(Arc arc)
    {
        if (arc.sign != null)
        {
            arc.sign.enabled = false;
            spareSigns.Push(arc.sign);
            arc.sign = null;
            arc.shownSign = null;
        }

        if (arc.line == null) return;
        arc.line.enabled = false;
        spare.Push(arc.line);
        arc.line = null;
    }

    LineRenderer NewLine(int pointCount, bool closed)
    {
        var line = new GameObject(closed ? "Ring" : "Arc").AddComponent<LineRenderer>();
        line.transform.SetParent(transform, false);
        line.useWorldSpace = true;
        line.positionCount = pointCount;
        line.loop = closed;
        line.textureMode = LineTextureMode.Stretch;
        line.sharedMaterial = arcMaterial;
        line.sortingLayerName = SortingLayerName;
        line.sortingOrder = SortingOrder;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        if (!closed) line.widthCurve = taper;
        line.enabled = false;
        return line;
    }

    static Sprite MakeSign(string[] rows)
    {
        int artWidth = 0;
        foreach (string row in rows)
            artWidth = Mathf.Max(artWidth, row.Length);
        int width = artWidth + 2;
        int height = rows.Length + 2;

        bool Filled(int x, int y) => y >= 0 && y < rows.Length && x >= 0 && x < rows[y].Length && rows[y][x] == '#';

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int artX = x - 1;
                int artY = y - 1;
                Color32 color = default;
                if (Filled(artX, artY))
                {
                    color = new Color32(255, 255, 255, 255);
                }
                else
                {
                    for (int dy = -1; dy <= 1 && color.a == 0; dy++)
                        for (int dx = -1; dx <= 1 && color.a == 0; dx++)
                            if (Filled(artX + dx, artY + dy)) color = new Color32(0, 0, 0, 255);
                }
                pixels[(height - 1 - y) * width + x] = color;
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false);
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), SignPixelsPerUnit);
    }

    static Texture2D MakeSoftBandTexture()
    {
        const int height = 32;
        var texture = new Texture2D(1, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < height; y++)
        {
            float fromMiddle = Mathf.Abs(y / (height - 1f) * 2f - 1f);
            texture.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - fromMiddle) * 1.6f)));
        }
        texture.Apply();
        return texture;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
