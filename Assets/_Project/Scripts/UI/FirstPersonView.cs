using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// first-person view from one spot
[RequireComponent(typeof(RawImage))]
public class FirstPersonView : MonoBehaviour
{
    const float EyeHeight = 1.15f;
    const float WallHeight = 2.4f;
    const float LampHeight = 2.1f;
    const float MaxDistance = 14f;
    const float NearClip = 0.3f;
    const int MapRadius = 16;
    const float TexelSize = 5f;
    const float RefreshInterval = 0.5f;
    const float RelightInterval = 0.2f;

    const float AmbientScale = 0.9f;
    const float LightScale = 1.1f;
    const float FogDensity = 0.15f;
    const float LightMapCell = 0.5f;
    const int LightMapSize = (MapRadius * 2 + 1) * 2;
    static readonly float[] LightMapHeights = { 0f, EyeHeight, WallHeight };
    const float WallDecalBias = 0.04f;

    static readonly Color FloorColor = new Color32(70, 75, 82, 255);
    static readonly Color FloorAltColor = new Color32(63, 68, 75, 255);
    static readonly Color GroutColor = new Color32(34, 37, 42, 255);
    static readonly Color WallColor = new Color32(92, 100, 112, 255);
    static readonly Color WallSeamColor = new Color32(56, 62, 70, 255);
    static readonly Color WallTrimColor = new Color32(150, 42, 38, 255);
    static readonly Color BaseboardColor = new Color32(38, 42, 48, 255);
    static readonly Color CeilingColor = new Color32(40, 44, 50, 255);
    static readonly Color LampColor = new Color32(214, 226, 236, 255);
    static readonly Color FogColor = new Color32(5, 6, 8, 255);

    const float CameraHeight = 2f;
    const float HousingSize = 0.34f;
    const float LensSize = 0.1f;
    const float CameraLightStrength = 0.45f;
    static readonly Color HousingColor = new Color32(22, 24, 29, 255);

    static readonly Color SpiderMetal = new Color32(46, 50, 58, 255);
    const float SpiderLegWidth = 0.05f;
    const float SpiderHubWidth = 0.34f, SpiderHubHeight = 0.2f;
    const float SpiderEyeSize = 0.075f;
    const float SpiderBeamStrength = 0.6f;

    class Prop
    {
        public GameObject owner;
        public List<SpriteRenderer> art;
        public Bounds drawn;
        public Rect footprint;
        public float height;
        public float topShare;
        public Color light;
        public float enter, leave;
        public int face;
    }

    struct Standee
    {
        public SpriteRenderer sprite;
        public Sprite art;
        public Vector2 feet;
        public float lift;
        public Vector2 size;
        public bool flip;
        public bool glows;
        public float bias;
    }

    struct PointLight
    {
        public Vector2 at;
        public float height, reach;
        public Color color;
        public bool lamp;
    }

    struct Pixels
    {
        public Color32[] data;
        public int width;
    }

    static readonly Dictionary<Texture2D, Pixels> spritePixels = new Dictionary<Texture2D, Pixels>();

    private RectTransform rect;
    private RawImage image;
    private Image flicker;
    private Texture2D texture;
    private int width, height;
    private Color32[] pixels, basePixels;
    private float[] depth, baseDepth;
    private Vector2[] floorAt;
    private readonly List<int> floorTexels = new List<int>();

    private bool[,] walls;
    private int mapMinX, mapMinY;
    private Vector2 eye, forward, right;
    private float eyeLevel;
    private float focal;
    private int standingLayer;
    private Transform ignored;

    private readonly List<Prop> props = new List<Prop>();
    private readonly List<SpriteRenderer> lying = new List<SpriteRenderer>();
    private readonly Dictionary<Vector2Int, List<SpriteRenderer>> lyingIn = new Dictionary<Vector2Int, List<SpriteRenderer>>();
    private readonly List<SpriteRenderer> about = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> hung = new List<SpriteRenderer>();
    private readonly List<StationCamera> cameras = new List<StationCamera>();
    private readonly List<CameraSpider> spiders = new List<CameraSpider>();
    private readonly List<PointLight> lights = new List<PointLight>();
    private readonly List<Light2D> sceneLights = new List<Light2D>();
    private readonly List<Standee> standees = new List<Standee>();
    private readonly List<Prop> hits = new List<Prop>();
    private Color ambient;
    private Color[] lightMap;
    private Vector2 lightMapCorner;
    private float lightSignature;
    private float refreshTimer, relightTimer, flickerTimer;
    private bool shown;

    void Awake()
    {
        rect = (RectTransform)transform;
        image = GetComponent<RawImage>();
        image.raycastTarget = false;
        var layer = new GameObject("Flicker", typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(transform, false);
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = layer.offsetMax = Vector2.zero;
        flicker = layer.gameObject.AddComponent<Image>();
        flicker.raycastTarget = false;
        flicker.enabled = false;
    }

    void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }

    public void Show(Vector2 eyePosition, Vector2 lookDirection, float eyeLevel01, float focalPixels, int standingSortingLayer, Transform ignore)
    {
        eye = eyePosition;
        forward = lookDirection.normalized;
        right = new Vector2(forward.y, -forward.x);
        standingLayer = standingSortingLayer;
        ignored = ignore;

        Vector2 size = rect.rect.size;
        width = Mathf.Max(8, Mathf.RoundToInt(size.x / TexelSize));
        height = Mathf.Max(8, Mathf.RoundToInt(size.y / TexelSize));
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
            depth = new float[width * height];
            baseDepth = new float[width * height];
            floorAt = new Vector2[width * height];
            image.texture = texture;
        }
        eyeLevel = eyeLevel01 * height;
        focal = focalPixels * height / size.y;

        shown = true;
        ReadMap();
        FindThings();
        GatherLights();
        Render();
        refreshTimer = RefreshInterval;
        relightTimer = RelightInterval;
        flickerTimer = Random.Range(1f, 3f);
        DrawFrame();
    }

    public void Hide()
    {
        shown = false;
        props.Clear();
        lying.Clear();
        lyingIn.Clear();
        about.Clear();
        hung.Clear();
        cameras.Clear();
        spiders.Clear();
        sceneLights.Clear();
        flicker.enabled = false;
    }

    void LateUpdate()
    {
        if (!shown || texture == null) return;

        float deltaTime = Time.unscaledDeltaTime;
        refreshTimer -= deltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = RefreshInterval;
            FindThings();
        }
        relightTimer -= deltaTime;
        if (relightTimer <= 0f)
        {
            relightTimer = RelightInterval;
            float was = lightSignature;
            GatherLights();
            if (!Mathf.Approximately(was, lightSignature)) Render();
        }
        DrawFrame();
        UpdateFlicker(deltaTime);
    }

    void ReadMap()
    {
        int size = MapRadius * 2 + 1;
        if (walls == null) walls = new bool[size, size];
        System.Array.Clear(walls, 0, walls.Length);
        var floor = new bool[size, size];

        mapMinX = Mathf.FloorToInt(eye.x) - MapRadius;
        mapMinY = Mathf.FloorToInt(eye.y) - MapRadius;

        bool anyFloor = false;
        foreach (Tilemap tilemap in FindObjectsByType<Tilemap>())
        {
            bool solid = tilemap.TryGetComponent(out TilemapCollider2D tileCollider) && tileCollider.enabled;
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                {
                    Vector3Int cell = tilemap.WorldToCell(new Vector3(mapMinX + i + 0.5f, mapMinY + j + 0.5f, 0f));
                    if (!tilemap.HasTile(cell)) continue;
                    if (solid) walls[i, j] = true;
                    else
                    {
                        floor[i, j] = true;
                        anyFloor = true;
                    }
                }
        }

        if (!anyFloor) return;
        for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                if (!floor[i, j]) walls[i, j] = true;
    }

    void FindThings()
    {
        props.Clear();
        lying.Clear();
        about.Clear();
        hung.Clear();
        var furniture = new Dictionary<GameObject, List<SpriteRenderer>>();
        var bodies = new HashSet<Transform>();
        foreach (CrewAftermath aftermath in FindObjectsByType<CrewAftermath>())
            foreach (CrewAftermath.Body body in aftermath.bodies)
                if (body.body != null) bodies.Add(body.body);

        float reach = (MaxDistance + 2f) * (MaxDistance + 2f);
        foreach (SpriteRenderer sprite in FindObjectsByType<SpriteRenderer>())
        {
            if (sprite.sortingLayerID != standingLayer || sprite.sprite == null) continue;
            if (ignored != null && sprite.transform.IsChildOf(ignored)) continue;
            if (((Vector2)sprite.transform.position - eye).sqrMagnitude > reach) continue;
            if (sprite.GetComponentInParent<CameraSpider>() != null) continue;
            bool shadow = sprite.name == "Shadow";

            CrewMember crew = sprite.GetComponentInParent<CrewMember>();
            if (crew != null && (bodies.Contains(crew.transform) || crew.name == "Crew Body"))
            {
                lying.Add(sprite);
                continue;
            }
            if (crew != null || sprite.GetComponentInParent<PlaceholderRobot>() != null || sprite.GetComponentInParent<MaintenanceBot>() != null
                || sprite.GetComponentInParent<WeepingAngel>() != null || sprite.GetComponentInParent<PlayerController>() != null)
            {
                if (!shadow) about.Add(sprite);
                continue;
            }
            if (shadow)
            {
                lying.Add(sprite);
                continue;
            }
            GameObject owner = SolidOwner(sprite);
            if (owner != null)
            {
                if (!furniture.TryGetValue(owner, out List<SpriteRenderer> art)) furniture[owner] = art = new List<SpriteRenderer>();
                art.Add(sprite);
                continue;
            }
            if (IsWall(Cell(sprite.bounds.min))) hung.Add(sprite);
            else about.Add(sprite);
        }
        lyingIn.Clear();
        foreach (SpriteRenderer flat in lying)
        {
            Bounds b = flat.bounds;
            for (int cx = Mathf.FloorToInt(b.min.x); cx <= Mathf.FloorToInt(b.max.x); cx++)
                for (int cy = Mathf.FloorToInt(b.min.y); cy <= Mathf.FloorToInt(b.max.y); cy++)
                {
                    var cell = new Vector2Int(cx, cy);
                    if (!lyingIn.TryGetValue(cell, out List<SpriteRenderer> here)) lyingIn[cell] = here = new List<SpriteRenderer>();
                    here.Add(flat);
                }
        }
        foreach (KeyValuePair<GameObject, List<SpriteRenderer>> piece in furniture)
        {
            piece.Value.Sort((a, b) => b.sortingOrder.CompareTo(a.sortingOrder));
            Bounds drawn = piece.Value[0].bounds;
            foreach (SpriteRenderer part in piece.Value) drawn.Encapsulate(part.bounds);
            foreach (Collider2D collider in piece.Key.GetComponents<Collider2D>())
                if (Solid(collider)) props.Add(MakeProp(piece.Key, piece.Value, drawn, collider.bounds));
        }

        cameras.Clear();
        foreach (StationCamera watcher in StationCamera.All)
        {
            float lightReach = MaxDistance + watcher.range;
            if (((Vector2)watcher.transform.position - eye).sqrMagnitude < lightReach * lightReach) cameras.Add(watcher);
        }
        spiders.Clear();
        foreach (CameraSpider spider in CameraSpider.All)
        {
            float lightReach = MaxDistance + spider.BeamReach;
            if (((Vector2)spider.transform.position - eye).sqrMagnitude < lightReach * lightReach) spiders.Add(spider);
        }
    }

    static GameObject SolidOwner(SpriteRenderer sprite)
    {
        for (Transform at = sprite.transform; at != null; at = at.parent)
            foreach (Collider2D collider in at.GetComponents<Collider2D>())
                if (Solid(collider)) return at.gameObject;
        return null;
    }

    static bool Solid(Collider2D collider) =>
        collider.enabled && !collider.isTrigger && !(collider is TilemapCollider2D) && !(collider is CompositeCollider2D);

    static Prop MakeProp(GameObject owner, List<SpriteRenderer> art, Bounds drawn, Bounds footprint)
    {
        float front = Mathf.Clamp(drawn.size.y - footprint.size.y, 0.25f, WallHeight);
        return new Prop
        {
            owner = owner,
            art = art,
            drawn = drawn,
            footprint = new Rect(footprint.min.x, footprint.min.y, footprint.size.x, footprint.size.y),
            height = front,
            topShare = Mathf.Clamp01(1f - front / Mathf.Max(0.01f, drawn.size.y))
        };
    }

    void GatherLights()
    {
        if (sceneLights.Count == 0) sceneLights.AddRange(FindObjectsByType<Light2D>());
        lights.Clear();
        ambient = Color.black;
        float signature = 0f;
        foreach (Light2D light in sceneLights)
        {
            if (light == null || !light.isActiveAndEnabled) continue;
            if (light.lightType == Light2D.LightType.Global)
            {
                Color given = light.color * light.intensity;
                if (given.maxColorComponent > ambient.maxColorComponent) ambient = given;
                signature += Mathf.Round(light.intensity * 50f) * 7f;
                continue;
            }
            if (light.lightType != Light2D.LightType.Point || light.pointLightOuterAngle < 300f) continue;
            if (light.GetComponentInParent<StationCamera>() != null || light.GetComponentInParent<CameraSpider>() != null) continue;
            StationLight station = light.GetComponentInParent<StationLight>();
            float intensity = station != null ? station.SteadyIntensity(light) : light.intensity;
            if (intensity <= 0.01f) continue;
            Vector2 at = light.transform.position;
            float lightReach = light.pointLightOuterRadius;
            if ((at - eye).sqrMagnitude > (MaxDistance + lightReach) * (MaxDistance + lightReach)) continue;
            lights.Add(new PointLight
            {
                at = at,
                height = LampHeight,
                reach = Mathf.Max(0.5f, lightReach),
                color = light.color * intensity,
                lamp = station != null
            });
            if (station != null) signature += intensity * (at.x * 0.37f + at.y * 0.61f + 1f);
        }
        lightSignature = signature;
        foreach (Prop prop in props) prop.light = LightAt(new Vector2(prop.footprint.center.x, prop.footprint.yMin), prop.height * 0.5f);
    }

    Color LightAt(Vector2 point, float atHeight)
    {
        Color total = ambient * AmbientScale;
        foreach (PointLight light in lights)
        {
            Vector2 to = point - light.at;
            float up = (light.height - atHeight) * 0.5f;
            float distance = Mathf.Sqrt(to.sqrMagnitude + up * up);
            if (distance >= light.reach) continue;
            float falloff = 1f - distance / light.reach;
            total += light.color * (falloff * falloff * LightScale);
        }
        return total;
    }

    void BuildLightMap()
    {
        int bands = LightMapHeights.Length;
        if (lightMap == null) lightMap = new Color[bands * LightMapSize * LightMapSize];
        lightMapCorner = new Vector2(mapMinX, mapMinY);
        for (int band = 0; band < bands; band++)
            for (int gy = 0; gy < LightMapSize; gy++)
                for (int gx = 0; gx < LightMapSize; gx++)
                {
                    Vector2 point = lightMapCorner + new Vector2(gx + 0.5f, gy + 0.5f) * LightMapCell;
                    lightMap[(band * LightMapSize + gy) * LightMapSize + gx] = LightAt(point, LightMapHeights[band]);
                }
    }

    Color MappedLight(Vector2 point, float atHeight)
    {
        float fx = Mathf.Clamp((point.x - lightMapCorner.x) / LightMapCell - 0.5f, 0f, LightMapSize - 1.001f);
        float fy = Mathf.Clamp((point.y - lightMapCorner.y) / LightMapCell - 0.5f, 0f, LightMapSize - 1.001f);
        int x = (int)fx, y = (int)fy;
        float tx = fx - x, ty = fy - y;
        int below = 0;
        while (below < LightMapHeights.Length - 2 && atHeight > LightMapHeights[below + 1]) below++;
        float th = Mathf.Clamp01((atHeight - LightMapHeights[below]) / (LightMapHeights[below + 1] - LightMapHeights[below]));
        return Color.Lerp(Band(below, x, y, tx, ty), Band(below + 1, x, y, tx, ty), th);
    }

    Color Band(int band, int x, int y, float tx, float ty)
    {
        int row = (band * LightMapSize + y) * LightMapSize;
        Color bottom = Color.Lerp(lightMap[row + x], lightMap[row + x + 1], tx);
        Color top = Color.Lerp(lightMap[row + LightMapSize + x], lightMap[row + LightMapSize + x + 1], tx);
        return Color.Lerp(bottom, top, ty);
    }

    Color32 Lit(Color surface, Color light, float distance)
    {
        Color lit = surface * light;
        lit = Color.Lerp(lit, FogColor, 1f - Mathf.Exp(-FogDensity * distance));
        lit.a = 1f;
        return lit;
    }

    void Render()
    {
        BuildLightMap();
        floorTexels.Clear();

        for (int x = 0; x < width; x++)
        {
            Vector2 ray = forward + right * ((x + 0.5f - width * 0.5f) / focal);
            float wallDepth = Cast(eye, ray, out Vector2 hit, out bool facesX);
            float wallTop = eyeLevel + (WallHeight - EyeHeight) / wallDepth * focal;
            float wallBottom = eyeLevel - EyeHeight / wallDepth * focal;

            for (int y = 0; y < height; y++)
            {
                float row = y + 0.5f;
                int i = y * width + x;
                Color32 color;
                float distance;
                if (row < wallBottom)
                {
                    distance = EyeHeight * focal / (eyeLevel - row);
                    Vector2 point = eye + ray * distance;
                    color = FloorPixel(point, distance);
                    floorAt[i] = point;
                    floorTexels.Add(i);
                }
                else if (row > wallTop)
                {
                    distance = (WallHeight - EyeHeight) * focal / (row - eyeLevel);
                    color = CeilingPixel(eye + ray * distance, distance);
                }
                else if (wallDepth >= MaxDistance)
                {
                    distance = MaxDistance;
                    color = FogColor;
                }
                else
                {
                    distance = wallDepth;
                    float wallHeight = EyeHeight + (row - eyeLevel) * wallDepth / focal;
                    color = WallPixel(hit, wallHeight, facesX, wallDepth);
                }
                pixels[i] = color;
                depth[i] = distance;
            }

            DrawProps(x, ray, wallDepth);
        }
        floorTexels.RemoveAll(i => depth[i] < Vector2.Dot(floorAt[i] - eye, forward) - 0.01f);

        System.Array.Copy(pixels, basePixels, pixels.Length);
        System.Array.Copy(depth, baseDepth, depth.Length);
    }

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

            if (IsWallLocal(cx, cy))
            {
                t = Mathf.Clamp(t, NearClip, MaxDistance);
                hit = from + ray * t;
                return t;
            }
        }

        hit = from + ray * MaxDistance;
        return MaxDistance;
    }

    static Vector2Int Cell(Vector2 world) => Vector2Int.FloorToInt(world);

    bool IsWall(Vector2Int cell) => IsWallLocal(cell.x, cell.y);

    bool IsWallLocal(int cx, int cy)
    {
        int i = cx - mapMinX;
        int j = cy - mapMinY;
        if (i < 0 || j < 0 || i >= walls.GetLength(0) || j >= walls.GetLength(1)) return true;
        return walls[i, j];
    }

    Color32 FloorPixel(Vector2 world, float distance)
    {
        int cx = Mathf.FloorToInt(world.x);
        int cy = Mathf.FloorToInt(world.y);
        float fu = world.x - cx;
        float fv = world.y - cy;
        Color surface = fu < 0.05f || fv < 0.05f ? GroutColor : ((cx + cy) & 1) == 0 ? FloorColor : FloorAltColor;

        if (!lyingIn.TryGetValue(new Vector2Int(cx, cy), out List<SpriteRenderer> here)) return Lit(surface, MappedLight(world, 0f), distance);
        foreach (SpriteRenderer body in here)
        {
            if (body == null || !body.enabled) continue;
            Bounds b = body.bounds;
            if (world.x < b.min.x || world.x > b.max.x || world.y < b.min.y || world.y > b.max.y) continue;
            float u = (world.x - b.min.x) / b.size.x, v = (world.y - b.min.y) / b.size.y;
            Color painted = (Color)Sample(body.sprite, body.flipX ? 1f - u : u, v) * body.color;
            if (painted.a < 0.05f) continue;
            surface = Color.Lerp(surface, painted, painted.a);
        }
        return Lit(surface, MappedLight(world, 0f), distance);
    }

    Color32 CeilingPixel(Vector2 world, float distance)
    {
        foreach (PointLight light in lights)
        {
            if (!light.lamp) continue;
            Vector2 off = world - light.at;
            if (Mathf.Abs(off.x) < 0.3f && Mathf.Abs(off.y) < 0.14f)
            {
                Color glow = LampColor * Mathf.Clamp01(light.color.maxColorComponent);
                return Lit(glow, Color.white, distance);
            }
        }
        float u = world.x - Mathf.Floor(world.x);
        float v = world.y - Mathf.Floor(world.y);
        Color surface = u < 0.04f || v < 0.04f ? GroutColor : CeilingColor;
        return Lit(surface, MappedLight(world, WallHeight), distance);
    }

    Color32 WallPixel(Vector2 hit, float atHeight, bool facesX, float distance)
    {
        float along = facesX ? hit.y : hit.x;
        Color surface;
        if (atHeight < 0.16f || atHeight > WallHeight - 0.14f)
            surface = BaseboardColor;
        else if (Mathf.Abs(atHeight - 1.1f) < 0.05f)
            surface = WallTrimColor;
        else if (Mathf.Repeat(along, 0.5f) < 0.035f)
            surface = WallSeamColor;
        else
            surface = WallColor;

        if (facesX) surface *= 0.8f;
        return Lit(surface, MappedLight(hit, atHeight), distance);
    }

    void DrawProps(int x, Vector2 ray, float wallDepth)
    {
        hits.Clear();
        foreach (Prop prop in props)
        {
            if (prop.owner == null || !prop.owner.activeInHierarchy) continue;
            if (!Enter(prop, ray) || prop.enter < NearClip || prop.enter >= wallDepth) continue;
            hits.Add(prop);
        }
        if (hits.Count == 0) return;
        hits.Sort((a, b) => b.enter.CompareTo(a.enter));

        foreach (Prop prop in hits)
        {
            float t = prop.enter;
            float bottom = eyeLevel - EyeHeight / t * focal;
            float top = eyeLevel + (prop.height - EyeHeight) / t * focal;
            Vector2 point = eye + ray * t;
            int from = Mathf.Max(0, Mathf.CeilToInt(bottom - 0.5f)), to = Mathf.Min(height - 1, Mathf.FloorToInt(top - 0.5f));
            for (int y = from; y <= to; y++)
            {
                float h = EyeHeight + (y + 0.5f - eyeLevel) * t / focal;
                Plot(x, y, t, SideColor(prop, point, h), prop.light);
            }
            if (prop.height >= EyeHeight) continue;

            float far = Mathf.Min(prop.leave, wallDepth);
            float rowNear = eyeLevel - (EyeHeight - prop.height) / t * focal;
            float rowFar = eyeLevel - (EyeHeight - prop.height) / far * focal;
            from = Mathf.Max(0, Mathf.CeilToInt(rowNear - 0.5f));
            to = Mathf.Min(height - 1, Mathf.FloorToInt(rowFar - 0.5f));
            for (int y = from; y <= to; y++)
            {
                float along = (EyeHeight - prop.height) * focal / (eyeLevel - (y + 0.5f));
                Vector2 onTop = eye + ray * along;
                Rect f = prop.footprint;
                float u = (onTop.x - prop.drawn.min.x) / prop.drawn.size.x;
                float v = 1f - prop.topShare + Mathf.Clamp01((onTop.y - f.yMin) / f.height) * prop.topShare;
                if (!SampleArt(prop, u, v, out Color painted)) continue;
                Plot(x, y, along, painted, prop.light);
            }
        }
    }

    bool Enter(Prop prop, Vector2 ray)
    {
        Rect f = prop.footprint;
        float tx1 = ray.x != 0f ? (f.xMin - eye.x) / ray.x : float.NegativeInfinity;
        float tx2 = ray.x != 0f ? (f.xMax - eye.x) / ray.x : float.PositiveInfinity;
        float ty1 = ray.y != 0f ? (f.yMin - eye.y) / ray.y : float.NegativeInfinity;
        float ty2 = ray.y != 0f ? (f.yMax - eye.y) / ray.y : float.PositiveInfinity;
        if (ray.x == 0f && (eye.x < f.xMin || eye.x > f.xMax)) return false;
        if (ray.y == 0f && (eye.y < f.yMin || eye.y > f.yMax)) return false;
        float inX = Mathf.Min(tx1, tx2), outX = Mathf.Max(tx1, tx2);
        float inY = Mathf.Min(ty1, ty2), outY = Mathf.Max(ty1, ty2);
        prop.enter = Mathf.Max(inX, inY);
        prop.leave = Mathf.Min(outX, outY);
        if (prop.enter > prop.leave || prop.leave <= 0f) return false;
        prop.face = inX > inY ? (ray.x > 0f ? 2 : 3) : (ray.y > 0f ? 0 : 1);
        return true;
    }

    Color SideColor(Prop prop, Vector2 point, float h)
    {
        Bounds drawn = prop.drawn;
        float v = Mathf.Clamp01(h / prop.height) * (1f - prop.topShare);
        float u, shade;
        switch (prop.face)
        {
            case 0: u = (point.x - drawn.min.x) / drawn.size.x; shade = 1f; break;
            case 1: u = 1f - (point.x - drawn.min.x) / drawn.size.x; shade = 0.7f; break;
            case 2: u = 0.06f; shade = 0.8f; break;
            default: u = 0.94f; shade = 0.8f; break;
        }
        Color surface = SampleArt(prop, u, v, out Color painted) ? painted : HousingColor;
        return surface * shade;
    }

    static bool SampleArt(Prop prop, float u, float v, out Color painted)
    {
        Vector2 point = new Vector2(prop.drawn.min.x + Mathf.Clamp01(u) * prop.drawn.size.x, prop.drawn.min.y + Mathf.Clamp01(v) * prop.drawn.size.y);
        foreach (SpriteRenderer part in prop.art)
        {
            if (part == null || !part.enabled) continue;
            Bounds b = part.bounds;
            if (point.x < b.min.x || point.x > b.max.x || point.y < b.min.y || point.y > b.max.y) continue;
            float pu = (point.x - b.min.x) / b.size.x, pv = (point.y - b.min.y) / b.size.y;
            Color32 texel = Sample(part.sprite, part.flipX ? 1f - pu : pu, pv);
            if (texel.a < 128) continue;
            painted = (Color)texel * part.color;
            return true;
        }
        painted = default;
        return false;
    }

    void Plot(int x, int y, float at, Color surface, Color light)
    {
        int i = y * width + x;
        if (at >= depth[i]) return;
        depth[i] = at;
        pixels[i] = Lit(surface, light, at);
    }

    void DrawFrame()
    {
        System.Array.Copy(basePixels, pixels, pixels.Length);
        System.Array.Copy(baseDepth, depth, depth.Length);
        SweepingLight();
        DrawStandees();
        foreach (CameraSpider spider in spiders)
            if (spider != null && spider.Shown) DrawSpider(spider);
        DrawCameras();
        texture.SetPixels32(pixels);
        texture.Apply(false);
    }

    void SweepingLight()
    {
        bool any = false;
        foreach (StationCamera watcher in cameras)
            if (watcher != null && watcher.Lit) any = true;
        foreach (CameraSpider spider in spiders)
            if (spider != null && spider.Shown) any = true;
        if (!any) return;

        foreach (int i in floorTexels)
        {
            Vector2 point = floorAt[i];
            Color added = Color.clear;
            foreach (StationCamera watcher in cameras)
            {
                if (watcher == null) continue;
                float strength = watcher.LightAt(point);
                if (strength > 0f) added += watcher.LightColor * (strength * CameraLightStrength);
            }
            foreach (CameraSpider spider in spiders)
            {
                if (spider == null || !spider.Shown) continue;
                float reach = spider.BeamReach;
                if (((Vector2)spider.Hub - point).sqrMagnitude > reach * reach) continue;
                for (int e = 0; e < spider.EyeTotal; e++)
                {
                    spider.EyeAt(e, out Vector3 at, out Vector2 look, out Color color, out float beam);
                    if (beam <= 0f) continue;
                    Vector2 to = point - (Vector2)at;
                    float distance = to.magnitude;
                    if (distance >= reach || Vector2.Angle(look, to) > spider.BeamHalfAngle) continue;
                    added += color * (beam * SpiderBeamStrength * (1f - distance / reach));
                }
            }
            if (added.maxColorComponent <= 0f) continue;
            float fog = Mathf.Exp(-FogDensity * Vector2.Dot(point - eye, forward));
            Color lit = (Color)pixels[i] + added * fog;
            lit.a = 1f;
            pixels[i] = lit;
        }
    }

    void DrawStandees()
    {
        standees.Clear();
        foreach (SpriteRenderer sprite in about)
        {
            if (!Showing(sprite)) continue;
            Bounds b = sprite.bounds;
            AsSeen(sprite, out Sprite art, out bool flip);
            standees.Add(new Standee
            {
                sprite = sprite,
                art = art,
                feet = new Vector2(b.center.x, b.min.y),
                size = b.size,
                flip = flip
            });
        }
        foreach (SpriteRenderer sprite in hung)
        {
            if (!Showing(sprite)) continue;
            Bounds b = sprite.bounds;
            Vector2Int cell = Cell(b.min);
            int steps = 0;
            while (IsWall(cell) && steps++ < 4) cell.y--;
            float foot = cell.y + 1f;
            StationLight lamp = sprite.GetComponentInParent<StationLight>();
            standees.Add(new Standee
            {
                sprite = sprite,
                art = sprite.sprite,
                feet = new Vector2(b.center.x, foot),
                lift = b.min.y - foot,
                size = b.size,
                flip = sprite.flipX,
                glows = lamp != null && lamp.IsOn,
                bias = WallDecalBias + 0.5f
            });
        }
        standees.Sort((a, b) => Vector2.Dot(b.feet - eye, forward).CompareTo(Vector2.Dot(a.feet - eye, forward)));
        foreach (Standee standee in standees) DrawStandee(standee);
    }

    bool Showing(SpriteRenderer sprite) =>
        sprite != null && sprite.enabled && sprite.sprite != null && sprite.gameObject.activeInHierarchy;

    void AsSeen(SpriteRenderer sprite, out Sprite art, out bool flip)
    {
        art = sprite.sprite;
        flip = Turned(sprite);
        Animator animator = sprite.GetComponentInParent<Animator>();
        if (animator == null || !animator.isActiveAndEnabled || !Faces(animator)) return;
        Vector2 facing = new Vector2(animator.GetFloat(PlayerAnimParams.FaceX), animator.GetFloat(PlayerAnimParams.FaceY));
        if (facing.sqrMagnitude < 0.01f) return;
        bool sided = Has(animator, PlayerAnimParams.FaceSide);
        float side = sided ? animator.GetFloat(PlayerAnimParams.FaceSide) : 0f;

        Vector2 seen = PlayerController.SnapToEightWay(new Vector2(Vector2.Dot(facing, right), Vector2.Dot(facing, forward)));
        animator.SetFloat(PlayerAnimParams.FaceX, seen.x);
        animator.SetFloat(PlayerAnimParams.FaceY, seen.y);
        if (sided && seen.x != 0f) animator.SetFloat(PlayerAnimParams.FaceSide, Mathf.Sign(seen.x));
        animator.Update(0f);
        art = sprite.sprite;
        flip = sprite.flipX;

        animator.SetFloat(PlayerAnimParams.FaceX, facing.x);
        animator.SetFloat(PlayerAnimParams.FaceY, facing.y);
        if (sided) animator.SetFloat(PlayerAnimParams.FaceSide, side);
        animator.Update(0f);
    }

    static readonly Dictionary<RuntimeAnimatorController, HashSet<int>> parameters = new Dictionary<RuntimeAnimatorController, HashSet<int>>();

    static bool Faces(Animator animator) => Has(animator, PlayerAnimParams.FaceX) && Has(animator, PlayerAnimParams.FaceY);

    static bool Has(Animator animator, int parameter)
    {
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller == null) return false;
        if (!parameters.TryGetValue(controller, out HashSet<int> known))
        {
            known = new HashSet<int>();
            foreach (AnimatorControllerParameter given in animator.parameters) known.Add(given.nameHash);
            parameters[controller] = known;
        }
        return known.Contains(parameter);
    }

    bool Turned(SpriteRenderer sprite)
    {
        PlaceholderRobot robot = sprite.GetComponentInParent<PlaceholderRobot>();
        Vector2 facing = robot != null ? robot.Facing : new Vector2(sprite.flipX ? -1f : 1f, 0f);
        float across = Vector2.Dot(facing, right);
        return Mathf.Abs(across) > 0.05f ? across < 0f : sprite.flipX;
    }

    void DrawStandee(Standee standee)
    {
        Vector2 toFeet = standee.feet - eye;
        float at = Vector2.Dot(toFeet, forward);
        if (at < NearClip || at > MaxDistance) return;
        float scale = focal / at;
        float middle = width * 0.5f + Vector2.Dot(toFeet, right) * scale;
        float left = middle - standee.size.x * 0.5f * scale, across = standee.size.x * scale;
        float bottom = eyeLevel + (standee.lift - EyeHeight) * scale, up = standee.size.y * scale;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(left)), x1 = Mathf.Min(width - 1, Mathf.CeilToInt(left + across));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(bottom)), y1 = Mathf.Min(height - 1, Mathf.CeilToInt(bottom + up));
        if (x0 > x1 || y0 > y1) return;

        Color light = standee.glows ? Color.white : LightAt(standee.feet, standee.lift + standee.size.y * 0.5f);
        float drawnAt = at - standee.bias;
        SpriteRenderer sprite = standee.sprite;
        for (int x = x0; x <= x1; x++)
        {
            float u = (x + 0.5f - left) / across;
            if (u < 0f || u > 1f) continue;
            if (standee.flip) u = 1f - u;
            for (int y = y0; y <= y1; y++)
            {
                float v = (y + 0.5f - bottom) / up;
                if (v < 0f || v > 1f) continue;
                int i = y * width + x;
                if (drawnAt >= depth[i]) continue;
                Color32 painted = Sample(standee.art, u, v);
                if (painted.a < 128) continue;
                depth[i] = drawnAt;
                pixels[i] = Lit((Color)painted * sprite.color, light, at);
            }
        }
    }

    void DrawSpider(CameraSpider spider)
    {
        Vector3 hub = spider.Hub;
        Color light = LightAt(hub, hub.z) + spider.CoreColor * 0.25f;
        for (int i = 0; i < spider.LegTotal; i++)
        {
            spider.LegAt(i, out Vector3 hip, out Vector3 knee, out Vector3 foot);
            Line(hip, knee, SpiderLegWidth, SpiderMetal, light);
            Line(knee, foot, SpiderLegWidth * 0.8f, SpiderMetal, light);
        }
        Blob(hub, SpiderHubWidth, SpiderHubHeight, SpiderMetal, light, false);
        Blob(hub + new Vector3(0f, 0f, 0.02f), SpiderHubWidth * 0.35f, SpiderHubHeight * 0.4f, spider.CoreColor, Color.white, true);
        for (int e = 0; e < spider.EyeTotal; e++)
        {
            spider.EyeAt(e, out Vector3 at, out Vector2 look, out Color color, out float beam);
            Vector2 toEye = eye - (Vector2)at;
            float facing = beam > 0f && Vector2.Angle(look, toEye) < spider.BeamHalfAngle * 1.5f ? 2.2f : 1f;
            Blob(at, SpiderEyeSize * facing, SpiderEyeSize * facing, color, Color.white, true);
        }
    }

    bool Project(Vector3 point, out Vector2 screen, out float at)
    {
        Vector2 to = (Vector2)point - eye;
        at = Vector2.Dot(to, forward);
        screen = default;
        if (at < NearClip) return false;
        screen = new Vector2(width * 0.5f + Vector2.Dot(to, right) / at * focal, eyeLevel + (point.z - EyeHeight) / at * focal);
        return true;
    }

    void Line(Vector3 from, Vector3 to, float thickness, Color surface, Color light)
    {
        if (!Project(from, out Vector2 a, out float atA) || !Project(to, out Vector2 b, out float atB)) return;
        int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y))) + 1;
        if (steps > 2000) return;
        for (int s = 0; s <= steps; s++)
        {
            float t = s / (float)steps;
            float at = Mathf.Lerp(atA, atB, t);
            if (at > MaxDistance) continue;
            float radius = Mathf.Max(0.5f, thickness * focal / at * 0.5f);
            Dot(Vector2.Lerp(a, b, t), radius, radius, at, Lit(surface, light, at));
        }
    }

    void Blob(Vector3 center, float across, float tall, Color surface, Color light, bool glows)
    {
        if (!Project(center, out Vector2 at2, out float at) || at > MaxDistance) return;
        Color32 color = glows ? Lit(surface, Color.white, at * 0.4f) : Lit(surface, light, at);
        Dot(at2, Mathf.Max(0.5f, across * focal / at * 0.5f), Mathf.Max(0.5f, tall * focal / at * 0.5f), at - (glows ? 0.02f : 0f), color);
    }

    void Dot(Vector2 center, float rx, float ry, float at, Color32 color)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - rx)), x1 = Mathf.Min(width - 1, Mathf.FloorToInt(center.x + rx));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - ry)), y1 = Mathf.Min(height - 1, Mathf.FloorToInt(center.y + ry));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = (x + 0.5f - center.x) / rx, dy = (y + 0.5f - center.y) / ry;
                if (dx * dx + dy * dy > 1f) continue;
                int i = y * width + x;
                if (at >= depth[i]) continue;
                depth[i] = at;
                pixels[i] = color;
            }
    }

    void DrawCameras()
    {
        foreach (StationCamera watcher in cameras)
        {
            if (watcher == null || !watcher.isActiveAndEnabled) continue;
            Vector3 at = new Vector3(watcher.transform.position.x, watcher.transform.position.y, CameraHeight);
            if (!Project(at, out Vector2 screen, out float distance) || distance > MaxDistance) continue;
            float scale = focal / distance;
            Dot(screen, HousingSize * 0.5f * scale, HousingSize * 0.35f * scale, distance - 0.5f, Lit(HousingColor, LightAt(at, CameraHeight), distance));
            Dot(screen, LensSize * 0.5f * scale, LensSize * 0.5f * scale, distance - 0.52f, Lit(watcher.LensColor, Color.white, distance * 0.4f));
        }
    }

    static Color32 Sample(Sprite sprite, float u, float v)
    {
        Texture2D source = sprite.texture;
        if (source == null) return new Color32(0, 0, 0, 0);
        if (!spritePixels.TryGetValue(source, out Pixels read))
        {
            read = Read(source);
            spritePixels[source] = read;
        }
        Rect area;
        try { area = sprite.textureRect; }
        catch (System.Exception) { area = sprite.rect; }
        int px = Mathf.Clamp(Mathf.FloorToInt(area.x + Mathf.Clamp01(u) * area.width), 0, read.width - 1);
        int py = Mathf.Clamp(Mathf.FloorToInt(area.y + Mathf.Clamp01(v) * area.height), 0, read.data.Length / read.width - 1);
        return read.data[py * read.width + px];
    }

    static Pixels Read(Texture2D source)
    {
        if (source.isReadable) return new Pixels { data = source.GetPixels32(), width = source.width };
        RenderTexture copy = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, copy);
        RenderTexture was = RenderTexture.active;
        RenderTexture.active = copy;
        var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
        readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readable.Apply(false);
        RenderTexture.active = was;
        RenderTexture.ReleaseTemporary(copy);
        var read = new Pixels { data = readable.GetPixels32(), width = source.width };
        Destroy(readable);
        return read;
    }

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
}
