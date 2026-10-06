using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// A first-person picture of the level from one spot, drawn into the UI. LockerView puts it behind the locker door, so
// the gaps between the slats look out onto the real hallway.
// The hallway is rebuilt from the scene's tilemaps: tiles on a tilemap with a collider are walls, other tiles are
// floor, and anywhere with no tiles counts as wall so the edge of the map never shows. It is ray cast once when shown,
// Wolfenstein style, into a low-resolution texture so it matches the pixel art, lit by ceiling lamps every few cells.
// Characters and props are drawn over it every frame as flat cut-outs of their current sprite, placed, sized, and
// darkened by distance and hidden behind walls, so robots can be watched walking past. The station's cameras show up
// on the walls with their lights on, and the light they throw sweeps across the floor (StationCamera.LightAt).
// Assumes the usual Grid: square cells one world unit across, not rotated.
[RequireComponent(typeof(RawImage))]
public class FirstPersonView : MonoBehaviour
{
    // Heights in world units. The player sprite is about 1.3 tall, so their eyes are near the top of it.
    const float EyeHeight = 1.15f;
    const float WallHeight = 2.4f;
    const float MaxDistance = 14f;
    const float NearClip = 0.3f;
    const int MapRadius = 16;               // cells read from the tilemaps around the eye, a little past MaxDistance
    const float TexelSize = 5f;             // canvas pixels per texel: chunky, like the rest of the art
    const float RefreshInterval = 0.5f;     // how often to look again for characters and props

    const float Ambient = 0.5f;
    const float CutoutAmbient = 0.65f;      // characters are kept a bit brighter than the walls so they read
    const int LampSpacing = 3;              // a lamp over every third cell, each way
    const float LampReach = 2.8f;
    const float LampStrength = 0.95f;
    const float FogDensity = 0.15f;

    static readonly Color FloorColor = new Color32(70, 75, 82, 255);
    static readonly Color FloorAltColor = new Color32(63, 68, 75, 255);
    static readonly Color GroutColor = new Color32(34, 37, 42, 255);
    static readonly Color WallColor = new Color32(92, 100, 112, 255);
    static readonly Color WallSeamColor = new Color32(56, 62, 70, 255);
    static readonly Color WallTrimColor = new Color32(150, 42, 38, 255);  // a red warning stripe at eye level
    static readonly Color BaseboardColor = new Color32(38, 42, 48, 255);
    static readonly Color CeilingColor = new Color32(40, 44, 50, 255);
    static readonly Color LampColor = new Color32(214, 226, 236, 255);
    static readonly Color LightTint = new Color(0.85f, 0.92f, 1f);   // cold station lighting
    static readonly Color FogColor = new Color32(5, 6, 8, 255);

    // The cameras.
    const float CameraHeight = 2f;          // up near the top of the wall
    const float HousingSize = 0.34f;
    const float LensSize = 0.1f;
    const float CameraLightStrength = 0.45f;
    static readonly Color HousingColor = new Color32(22, 24, 29, 255);
    static readonly Vector2 NoFloor = new Vector2(float.NaN, float.NaN);

    struct Cutout
    {
        public Sprite sprite;
        public Vector2 feet;     // world position of its bottom middle
        public Vector2 size;     // world units
        public bool flipX;
        public Color color;
        public float lift;       // world height of its bottom edge: 0 stands on the floor
        public bool glows;       // a light: lamps don't shade it
        public bool onWall;      // on the face of a wall, so the wall it's on doesn't hide it
        public float depth;
        public float x;          // texel column of its middle
    }

    static readonly System.Comparison<Cutout> FarthestFirst = (a, b) => b.depth.CompareTo(a.depth);

    private RectTransform rect;
    private RawImage image;
    private RectTransform cutoutLayer;
    private Image flicker;
    private Texture2D texture;
    private Color32[] pixels;
    private Color32[] basePixels;            // the hallway as rendered, before any camera light
    private Vector2[] floorAt;               // the world point of floor each texel shows, or NoFloor
    private bool cameraLit;                  // whether there's camera light on the texture now
    private float[] columnDepth;             // how far away the wall is in each column, for hiding cut-outs behind it
    private readonly List<Image> cutoutImages = new List<Image>();
    private readonly List<SpriteRenderer> standing = new List<SpriteRenderer>();
    private readonly List<Cutout> visible = new List<Cutout>();
    private readonly List<StationCamera> cameras = new List<StationCamera>();

    private bool[,] walls;
    private int mapMinX, mapMinY;
    private Vector2 origin;                  // world position of the corner of cell (0, 0)
    private Vector2 eye, forward, right;
    private float eyeLevel;                  // texel row
    private float focal;                     // texels
    private int standingLayer;
    private Transform ignored;
    private float refreshTimer;
    private float flickerTimer;

    void Awake()
    {
        rect = (RectTransform)transform;
        image = GetComponent<RawImage>();
        image.raycastTarget = false;
        cutoutLayer = NewLayer("Cutouts");
        flicker = NewLayer("Flicker").gameObject.AddComponent<Image>();
        flicker.raycastTarget = false;
        flicker.enabled = false;
    }

    void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }

    // eyePosition and lookDirection are in world space. eyeLevel01 is where eye level falls, as a fraction of this
    // rect's height, and focalPixels sets the field of view, in canvas pixels. Sprites on standingSortingLayer (the
    // layer characters and props are on) are drawn as cut-outs, except ones under ignore.
    public void Show(Vector2 eyePosition, Vector2 lookDirection, float eyeLevel01, float focalPixels, int standingSortingLayer, Transform ignore)
    {
        eye = eyePosition;
        forward = lookDirection.normalized;
        right = new Vector2(forward.y, -forward.x);
        standingLayer = standingSortingLayer;
        ignored = ignore;

        Vector2 size = rect.rect.size;
        int width = Mathf.Max(8, Mathf.RoundToInt(size.x / TexelSize));
        int height = Mathf.Max(8, Mathf.RoundToInt(size.y / TexelSize));
        if (texture == null || texture.width != width || texture.height != height)
        {
            if (texture != null) Destroy(texture);
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            pixels = new Color32[width * height];
            basePixels = new Color32[width * height];
            floorAt = new Vector2[width * height];
            columnDepth = new float[width];
            image.texture = texture;
        }
        eyeLevel = eyeLevel01 * height;
        focal = focalPixels * height / size.y;

        ReadMap();
        Render();
        cameraLit = false;
        FindStanding();
        refreshTimer = RefreshInterval;
        flickerTimer = Random.Range(1f, 3f);
        UpdateCutouts();
    }

    public void Hide()
    {
        standing.Clear();
        cameras.Clear();
        foreach (Image cutout in cutoutImages)
            cutout.enabled = false;
        flicker.enabled = false;
    }

    void LateUpdate()
    {
        if (texture == null) return;

        float deltaTime = Time.unscaledDeltaTime;
        refreshTimer -= deltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = RefreshInterval;
            FindStanding();
        }
        UpdateCameraLight();
        UpdateCutouts();
        UpdateFlicker(deltaTime);
    }

    // --- The hallway ---

    // Reads which cells near the eye are walls.
    void ReadMap()
    {
        int size = MapRadius * 2 + 1;
        if (walls == null) walls = new bool[size, size];
        System.Array.Clear(walls, 0, walls.Length);
        var floor = new bool[size, size];

        Tilemap[] tilemaps = FindObjectsByType<Tilemap>();
        origin = tilemaps.Length > 0 ? (Vector2)tilemaps[0].layoutGrid.CellToWorld(Vector3Int.zero) : Vector2.zero;
        mapMinX = Mathf.FloorToInt(eye.x - origin.x) - MapRadius;
        mapMinY = Mathf.FloorToInt(eye.y - origin.y) - MapRadius;

        bool anyFloor = false;
        foreach (Tilemap tilemap in tilemaps)
        {
            bool solid = tilemap.TryGetComponent(out TilemapCollider2D tileCollider) && tileCollider.enabled;
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    if (!tilemap.HasTile(new Vector3Int(mapMinX + i, mapMinY + j, 0))) continue;
                    if (solid)
                    {
                        walls[i, j] = true;
                    }
                    else
                    {
                        floor[i, j] = true;
                        anyFloor = true;
                    }
                }
            }
        }

        // With no floor tiles there's no map to go by, so leave it open. Otherwise, off the floor is wall.
        if (!anyFloor) return;
        for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                if (!floor[i, j]) walls[i, j] = true;
    }

    void Render()
    {
        int width = texture.width;
        int height = texture.height;
        Vector2 eyeLocal = eye - origin;

        for (int x = 0; x < width; x++)
        {
            // The ray moves one unit forward per unit along it, so distance along it is depth straight ahead: no fisheye.
            Vector2 ray = forward + right * ((x + 0.5f - width * 0.5f) / focal);
            float depth = Cast(eyeLocal, ray, out Vector2 hit, out bool facesX);
            columnDepth[x] = depth;
            float wallTop = eyeLevel + (WallHeight - EyeHeight) / depth * focal;
            float wallBottom = eyeLevel - EyeHeight / depth * focal;

            for (int y = 0; y < height; y++)
            {
                float row = y + 0.5f;
                Color32 color;
                floorAt[y * width + x] = NoFloor;
                if (row < wallBottom)
                {
                    float distance = EyeHeight * focal / (eyeLevel - row);
                    color = FloorPixel(eyeLocal + ray * distance, distance);
                    floorAt[y * width + x] = eye + ray * distance;
                }
                else if (row > wallTop)
                {
                    float distance = (WallHeight - EyeHeight) * focal / (row - eyeLevel);
                    color = CeilingPixel(eyeLocal + ray * distance, distance);
                }
                else if (depth >= MaxDistance)
                {
                    color = FogColor;
                }
                else
                {
                    float wallHeight = EyeHeight + (row - eyeLevel) * depth / focal;
                    color = WallPixel(hit, wallHeight, facesX, depth);
                }
                pixels[y * width + x] = color;
            }
        }

        System.Array.Copy(pixels, basePixels, pixels.Length);
        texture.SetPixels32(pixels);
        texture.Apply(false);
    }

    // The cameras' light on the floor, redrawn every frame while any of them is throwing some, as they sweep.
    void UpdateCameraLight()
    {
        bool any = false;
        foreach (StationCamera watcher in cameras)
            if (watcher != null && watcher.Lit) any = true;
        if (!any && !cameraLit) return;

        System.Array.Copy(basePixels, pixels, pixels.Length);
        if (any)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Vector2 point = floorAt[i];
                if (float.IsNaN(point.x)) continue;
                Color added = Color.clear;
                foreach (StationCamera watcher in cameras)
                {
                    if (watcher == null) continue;
                    float strength = watcher.LightAt(point);
                    if (strength > 0f) added += watcher.LightColor * (strength * CameraLightStrength);
                }
                if (added.maxColorComponent <= 0f) continue;

                float fog = Mathf.Exp(-FogDensity * Vector2.Dot(point - eye, forward));
                Color lit = (Color)pixels[i] + added * fog;
                lit.a = 1f;
                pixels[i] = lit;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false);
        cameraLit = any;
    }

    // Steps the ray from cell to cell until it enters a wall. Returns the depth, or MaxDistance if nothing is that close.
    float Cast(Vector2 from, Vector2 ray, out Vector2 hit, out bool facesX)
    {
        int cx = Mathf.FloorToInt(from.x);
        int cy = Mathf.FloorToInt(from.y);
        int stepX = ray.x < 0f ? -1 : 1;
        int stepY = ray.y < 0f ? -1 : 1;
        float deltaX = ray.x != 0f ? Mathf.Abs(1f / ray.x) : Mathf.Infinity;
        float deltaY = ray.y != 0f ? Mathf.Abs(1f / ray.y) : Mathf.Infinity;
        float nextX = ray.x == 0f ? Mathf.Infinity : (ray.x < 0f ? from.x - cx : cx + 1f - from.x) * deltaX;
        float nextY = ray.y == 0f ? Mathf.Infinity : (ray.y < 0f ? from.y - cy : cy + 1f - from.y) * deltaY;

        float t = 0f;
        facesX = false;
        while (t < MaxDistance)
        {
            if (nextX < nextY)
            {
                t = nextX;
                nextX += deltaX;
                cx += stepX;
                facesX = true;
            }
            else
            {
                t = nextY;
                nextY += deltaY;
                cy += stepY;
                facesX = false;
            }

            if (IsWall(cx, cy))
            {
                t = Mathf.Clamp(t, NearClip, MaxDistance);
                hit = from + ray * t;
                return t;
            }
        }

        hit = from + ray * MaxDistance;
        return MaxDistance;
    }

    bool IsWall(int cx, int cy)
    {
        int i = cx - mapMinX;
        int j = cy - mapMinY;
        if (i < 0 || j < 0 || i >= walls.GetLength(0) || j >= walls.GetLength(1)) return true;
        return walls[i, j];
    }

    Color32 FloorPixel(Vector2 p, float distance)
    {
        int cx = Mathf.FloorToInt(p.x);
        int cy = Mathf.FloorToInt(p.y);
        float u = p.x - cx;
        float v = p.y - cy;
        Color surface = u < 0.05f || v < 0.05f ? GroutColor : ((cx + cy) & 1) == 0 ? FloorColor : FloorAltColor;
        return Shade(surface, LampLight(p, 0f), distance);
    }

    Color32 CeilingPixel(Vector2 p, float distance)
    {
        int cx = Mathf.FloorToInt(p.x);
        int cy = Mathf.FloorToInt(p.y);
        float u = p.x - cx;
        float v = p.y - cy;
        bool lamp = Mod(cx, LampSpacing) == 1 && Mod(cy, LampSpacing) == 1;
        if (lamp && Mathf.Abs(u - 0.5f) < 0.3f && Mathf.Abs(v - 0.5f) < 0.14f)
            return Fog(LampColor, distance); // lamps glow, so they aren't shaded

        Color surface = u < 0.04f || v < 0.04f ? GroutColor : CeilingColor;
        return Shade(surface, LampLight(p, WallHeight), distance);
    }

    Color32 WallPixel(Vector2 hit, float height, bool facesX, float distance)
    {
        float along = facesX ? hit.y : hit.x; // position along the wall, for the panel seams
        Color surface;
        if (height < 0.16f || height > WallHeight - 0.14f)
            surface = BaseboardColor;
        else if (Mathf.Abs(height - 1.1f) < 0.05f)
            surface = WallTrimColor;
        else if (Mathf.Repeat(along, 0.5f) < 0.035f)
            surface = WallSeamColor;
        else
            surface = WallColor;

        // Walls facing along x are a little darker, so corners read.
        if (facesX) surface *= 0.8f;
        return Shade(surface, LampLight(hit, height), distance);
    }

    // Light from the lamps around a point. Lamps hang over every third cell each way, wherever that cell isn't wall.
    float LampLight(Vector2 p, float height)
    {
        int lampX = Mathf.FloorToInt((p.x - 1.5f) / LampSpacing);
        int lampY = Mathf.FloorToInt((p.y - 1.5f) / LampSpacing);
        float dz = (WallHeight - height) * 0.35f;
        float light = 0f;

        // The lamps on either side, each way.
        for (int i = 0; i <= 1; i++)
        {
            for (int j = 0; j <= 1; j++)
            {
                int cx = (lampX + i) * LampSpacing + 1;
                int cy = (lampY + j) * LampSpacing + 1;
                if (IsWall(cx, cy)) continue;

                float dx = p.x - (cx + 0.5f);
                float dy = p.y - (cy + 0.5f);
                float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy + dz * dz) / LampReach);
                light += falloff * falloff;
            }
        }
        return LampStrength * light;
    }

    static Color32 Shade(Color surface, float light, float distance)
    {
        return Fog(surface * Ambient + surface * LightTint * light, distance);
    }

    static Color32 Fog(Color color, float distance)
    {
        Color fogged = Color.Lerp(color, FogColor, 1f - Mathf.Exp(-FogDensity * distance));
        fogged.a = 1f;
        return fogged;
    }

    static int Mod(int value, int divisor)
    {
        return (value % divisor + divisor) % divisor;
    }

    // --- Characters, props, and cameras ---

    // Everything drawn on the same sorting layer as the characters, near enough to matter, and the cameras near enough
    // for their light to reach.
    void FindStanding()
    {
        cameras.Clear();
        foreach (StationCamera watcher in StationCamera.All)
        {
            float lightReach = MaxDistance + watcher.range;
            if (((Vector2)watcher.transform.position - eye).sqrMagnitude < lightReach * lightReach) cameras.Add(watcher);
        }

        standing.Clear();
        float reach = (MaxDistance + 2f) * (MaxDistance + 2f);
        foreach (SpriteRenderer sprite in FindObjectsByType<SpriteRenderer>())
        {
            if (sprite.sortingLayerID != standingLayer) continue;
            if (ignored != null && sprite.transform.IsChildOf(ignored)) continue;
            if (((Vector2)sprite.transform.position - eye).sqrMagnitude > reach) continue;
            standing.Add(sprite);
        }
    }

    void UpdateCutouts()
    {
        visible.Clear();
        foreach (SpriteRenderer sprite in standing)
        {
            if (sprite == null || !sprite.enabled || sprite.sprite == null || !sprite.gameObject.activeInHierarchy) continue;

            // A sprite stands where its bottom edge is: that's its feet in the top-down view.
            Bounds bounds = sprite.bounds;
            AddCutout(new Cutout
            {
                sprite = sprite.sprite,
                feet = new Vector2(bounds.center.x, bounds.min.y),
                size = bounds.size,
                flipX = sprite.flipX,
                color = sprite.color
            });
        }

        // The cameras: a dark housing up on the wall, its light on the front, a hair nearer so it draws over it.
        foreach (StationCamera watcher in cameras)
        {
            if (watcher == null || !watcher.isActiveAndEnabled) continue;
            Vector2 at = watcher.transform.position;
            AddCutout(new Cutout
            {
                feet = at,
                size = new Vector2(HousingSize, HousingSize * 0.7f),
                lift = CameraHeight - HousingSize * 0.35f,
                color = HousingColor,
                onWall = true
            });
            AddCutout(new Cutout
            {
                feet = at + (eye - at).normalized * 0.02f,
                size = new Vector2(LensSize, LensSize),
                lift = CameraHeight - LensSize * 0.5f,
                color = watcher.LensColor,
                glows = true,
                onWall = true
            });
        }

        // Far to near, so nearer ones draw on top.
        visible.Sort(FarthestFirst);

        Vector2 texel = new Vector2(rect.rect.width / texture.width, rect.rect.height / texture.height);
        for (int i = 0; i < visible.Count; i++)
        {
            Cutout cutout = visible[i];
            Image cut = CutoutImage(i);
            cut.enabled = true;
            cut.sprite = cutout.sprite;

            float light = cutout.glows ? 1f : Mathf.Min(1f, CutoutAmbient + LampLight(cutout.feet - origin, EyeHeight));
            Color shaded = Color.Lerp(cutout.color * light, FogColor, 1f - Mathf.Exp(-FogDensity * cutout.depth));
            shaded.a = cutout.color.a;
            cut.color = shaded;

            float scale = focal / cutout.depth;
            float feetRow = eyeLevel + (cutout.lift - EyeHeight) * scale;
            RectTransform box = cut.rectTransform;
            box.anchoredPosition = new Vector2(cutout.x * texel.x, feetRow * texel.y);
            box.sizeDelta = new Vector2(cutout.size.x * scale * texel.x, cutout.size.y * scale * texel.y);
            box.localScale = new Vector3(cutout.flipX ? -1f : 1f, 1f, 1f);
        }

        for (int i = visible.Count; i < cutoutImages.Count; i++)
            cutoutImages[i].enabled = false;
    }

    // Works out where a cut-out lands, and keeps it if it's in view and not behind a wall.
    void AddCutout(Cutout cutout)
    {
        Vector2 toFeet = cutout.feet - eye;
        cutout.depth = Vector2.Dot(toFeet, forward);
        if (cutout.depth < NearClip || cutout.depth > MaxDistance) return;

        cutout.x = texture.width * 0.5f + Vector2.Dot(toFeet, right) / cutout.depth * focal;
        float halfWidth = cutout.size.x * 0.5f / cutout.depth * focal;
        if (cutout.x + halfWidth < 0f || cutout.x - halfWidth > texture.width) return;

        // Hidden if the wall behind its middle is nearer than it is (a little nearer, for something on that wall).
        int column = Mathf.FloorToInt(cutout.x);
        float slack = cutout.onWall ? 0.5f : 0f;
        if (column >= 0 && column < columnDepth.Length && columnDepth[column] < cutout.depth - slack) return;

        visible.Add(cutout);
    }

    Image CutoutImage(int index)
    {
        while (cutoutImages.Count <= index)
        {
            var cut = new GameObject("Cutout", typeof(RectTransform)).AddComponent<Image>();
            RectTransform box = cut.rectTransform;
            box.SetParent(cutoutLayer, false);
            box.anchorMin = box.anchorMax = Vector2.zero; // positioned in pixels from the bottom left
            box.pivot = new Vector2(0.5f, 0f);            // at its feet
            cut.raycastTarget = false;
            cutoutImages.Add(cut);
        }
        return cutoutImages[index];
    }

    // The hallway lights stutter now and then: a short dip, sometimes twice in a row.
    void UpdateFlicker(float deltaTime)
    {
        flickerTimer -= deltaTime;
        if (flickerTimer > 0f) return;

        flicker.enabled = !flicker.enabled;
        if (flicker.enabled)
        {
            flicker.color = new Color(0f, 0f, 0f, Random.Range(0.3f, 0.55f));
            flickerTimer = Random.Range(0.04f, 0.1f);
        }
        else
        {
            flickerTimer = Random.value < 0.35f ? 0.06f : Random.Range(2f, 6f);
        }
    }

    RectTransform NewLayer(string name)
    {
        var layer = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(transform, false);
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = layer.offsetMax = Vector2.zero;
        return layer;
    }
}
