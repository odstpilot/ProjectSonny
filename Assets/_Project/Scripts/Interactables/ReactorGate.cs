using UnityEngine;
using UnityEngine.Rendering.Universal;

// The containment round the back of the reactor core, and the blast gates in it. Three kinds:
//   Fence      a run of fence facing the camera: posts, mesh, a hazard-striped rail along the top
//   SideFence  one cell of fence seen edge-on, running away up the screen: a post and its rail
//   Gate       a gate in a frame, its shutter sliding up into the header to open, a lamp over it red while it's shut
//              and green while it's open. A solid gate blocks the way while it's shut (the lockdown's, across the
//              far side of the reactor); the fence's own are just how the bots come out (ReactorLockdown).
// It stands on the floor and sorts like the furniture (DepthSort): this object sits at the middle of its foot. Drawn
// in code at the tileset's 32 pixels to a cell. Placed by ChapterOneBuilder (PlaceReactorGauntlet).
public class ReactorGate : MonoBehaviour
{
    public enum Kind { Fence, SideFence, Gate }

    const float PixelsPerUnit = 32f;
    const int FenceHeight = 46;         // pixels up from its foot to the top rail
    const int GateHeight = 70;
    const int Header = 10;

    static readonly Color32 Outline = new Color32(24, 18, 18, 255);
    static readonly Color32 Steel = new Color32(92, 78, 68, 255);
    static readonly Color32 SteelLit = new Color32(140, 118, 96, 255);
    static readonly Color32 SteelDark = new Color32(50, 40, 36, 255);
    static readonly Color32 Mesh = new Color32(70, 58, 50, 255);
    static readonly Color32 Hazard = new Color32(236, 176, 40, 255);
    static readonly Color32 HazardDark = new Color32(34, 28, 28, 255);
    static readonly Color ShutLamp = new Color(1f, 0.2f, 0.12f);
    static readonly Color OpenLamp = new Color(0.35f, 1f, 0.45f);

    public Kind kind = Kind.Fence;
    [Tooltip("How long it runs, in cells: across for a fence or gate, up the screen for a side fence.")]
    public float length = 3f;
    [Tooltip("A gate: solid while it's shut.")]
    public bool solid;
    public bool startsOpen;
    [Tooltip("Seconds to open or shut all the way.")]
    public float moveTime = 0.6f;
    [SoundName] public string moveSound = "Blast Door";

    public bool IsOpen => progress >= 1f;
    public bool IsShut => progress <= 0f;

    private Transform art;
    private SpriteRenderer shutter, lamp;
    private Light2D lampLight;
    private BoxCollider2D block;
    private int shutterHeight;
    private float progress;
    private bool wantOpen;

    void Awake()
    {
        art = new GameObject("Art").transform;
        art.SetParent(transform, false);
        art.localPosition = new Vector3(0f, DepthSort.FeetToCenter, 0f);
        DepthSort.Group(art.gameObject);
        Vector3 foot = new Vector3(0f, -DepthSort.FeetToCenter, 0f);
        int width = Mathf.Max(4, Mathf.RoundToInt(length * PixelsPerUnit));

        switch (kind)
        {
            case Kind.Fence:
                Piece("Fence", PixelArt.ToSprite(DrawFence(width), PixelsPerUnit, new Vector2(0.5f, 0f)), 0).transform.localPosition = foot;
                break;
            case Kind.SideFence:
                Piece("Fence", PixelArt.ToSprite(DrawSide(width), PixelsPerUnit, new Vector2(0.5f, 0f)), 0).transform.localPosition = foot;
                break;
            default:
                Piece("Frame", PixelArt.ToSprite(DrawFrame(width), PixelsPerUnit, new Vector2(0.5f, 0f)), 0).transform.localPosition = foot;
                shutterHeight = GateHeight - Header;
                // The shutter hangs from the header, so shrinking it from the bottom lifts it up into it.
                shutter = Piece("Shutter", PixelArt.ToSprite(DrawShutter(width - 8, shutterHeight), PixelsPerUnit, new Vector2(0.5f, 1f)), 1);
                shutter.transform.localPosition = foot + new Vector3(0f, shutterHeight / PixelsPerUnit, 0f);
                lamp = Piece("Lamp", PixelArt.ToSprite(PixelArt.MakeTexture(4, 3, (x, y) => new Color32(255, 255, 255, 255)), PixelsPerUnit, new Vector2(0.5f, 0.5f)), 3);
                lamp.transform.localPosition = foot + new Vector3(0f, (GateHeight - Header * 0.5f) / PixelsPerUnit, 0f);
                if (CombatSprites.EffectMaterial != null) lamp.sharedMaterial = CombatSprites.EffectMaterial;
                var holder = new GameObject("Lamp Light");
                holder.transform.SetParent(transform, false);
                holder.transform.localPosition = new Vector3(0f, GateHeight / PixelsPerUnit, 0f);
                lampLight = holder.AddComponent<Light2D>();
                lampLight.lightType = Light2D.LightType.Point;
                lampLight.pointLightOuterRadius = 1.8f;
                lampLight.falloffIntensity = 0.6f;
                lampLight.intensity = 0.8f;
                lampLight.shadowsEnabled = false;
                if (solid)
                {
                    block = gameObject.AddComponent<BoxCollider2D>();
                    block.size = new Vector2(length, 0.7f);
                    block.offset = new Vector2(0f, 0.35f);
                }
                break;
        }
        wantOpen = startsOpen;
        progress = startsOpen ? 1f : 0f;
        Apply();
    }

    SpriteRenderer Piece(string pieceName, Sprite sprite, int order)
    {
        var piece = new GameObject(pieceName).AddComponent<SpriteRenderer>();
        piece.transform.SetParent(art, false);
        piece.sprite = sprite;
        piece.sortingLayerName = DepthSort.Layer;
        piece.sortingOrder = order;
        return piece;
    }

    public void Open()
    {
        if (kind != Kind.Gate || wantOpen) return;
        wantOpen = true;
        Sfx.At(moveSound, transform.position, 0.8f, 1.1f);
    }

    public void Close()
    {
        if (kind != Kind.Gate || !wantOpen) return;
        wantOpen = false;
        Sfx.At(moveSound, transform.position, 0.9f, 0.9f);
    }

    void Update()
    {
        if (kind != Kind.Gate) return;
        float target = wantOpen ? 1f : 0f;
        if (progress == target) return;
        // It never comes down on anyone: a solid gate waits till its doorway's clear.
        if (!wantOpen && solid && Occupied()) return;
        progress = Mathf.MoveTowards(progress, target, Time.deltaTime / Mathf.Max(0.01f, moveTime));
        Apply();
    }

    void Apply()
    {
        if (kind != Kind.Gate) return;
        // In whole pixels, so it stays crisp as it moves.
        int showing = Mathf.Max(2, Mathf.RoundToInt(shutterHeight * (1f - progress)));
        shutter.transform.localScale = new Vector3(1f, showing / (float)shutterHeight, 1f);
        Color color = progress >= 1f ? OpenLamp : ShutLamp;
        lamp.color = color;
        lampLight.color = color;
        if (block != null) block.enabled = progress < 0.85f;
    }

    bool Occupied()
    {
        Vector2 center = (Vector2)transform.position + new Vector2(0f, 0.35f);
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, new Vector2(length, 0.7f), 0f))
            if (!hit.isTrigger && hit != block && hit.attachedRigidbody != null && !hit.transform.IsChildOf(transform)) return true;
        return false;
    }

    // --- Drawing ---

    static bool Edge(int x, int y, int w, int h) => x == 0 || y == 0 || x == w - 1 || y == h - 1;

    // Posts every cell, mesh between, a striped rail along the top, a kick plate along the bottom.
    static Texture2D DrawFence(int width)
    {
        int h = FenceHeight + 2;
        return PixelArt.MakeTexture(width, h, (x, y) =>
        {
            bool post = x % 32 < 3 || x >= width - 3;
            if (y >= h - 5) return Edge(x, y, width, h) || y == h - 5 ? Outline : (x / 4 + y) % 2 == 0 ? Hazard : HazardDark;
            if (post) return x % 32 == 0 || x == width - 1 ? Outline : x % 32 == 1 ? SteelLit : Steel;
            if (y <= 5) return y == 0 || y == 5 ? Outline : y == 4 ? SteelLit : Steel;
            // Diamond mesh.
            return ((x + y) % 5 == 0 || (x - y + 100) % 5 == 0) ? Mesh : default;
        });
    }

    // Seen edge-on, running away up the screen: a post at the near end, the mesh between as a thin strip, and the
    // striped rail on top climbing toward the next post, run pixels further up.
    static Texture2D DrawSide(int run)
    {
        int w = 7, h = run + FenceHeight + 2;
        return PixelArt.MakeTexture(w, h, (x, y) =>
        {
            if (y <= FenceHeight && x >= 2 && x <= 4) return x == 2 ? SteelLit : x == 4 ? Outline : Steel;
            if (y >= FenceHeight - 4 && y <= FenceHeight + run && x >= 1 && x <= 5)
            {
                if (x == 1 || x == 5 || y == FenceHeight - 4 || y == FenceHeight + run) return Outline;
                return (y / 3) % 2 == 0 ? Hazard : HazardDark;
            }
            if (x == 3 && y <= FenceHeight + run && y % 3 != 0) return Mesh;
            return default;
        });
    }

    // The gate's frame: posts either side, a header across the top with hazard stripes, a sill at the foot.
    static Texture2D DrawFrame(int width)
    {
        return PixelArt.MakeTexture(width, GateHeight, (x, y) =>
        {
            bool post = x < 4 || x >= width - 4;
            if (y >= GateHeight - Header)
            {
                if (Edge(x, y, width, GateHeight) || y == GateHeight - Header) return Outline;
                if (Mathf.Abs(x - width / 2) <= 4 && y >= GateHeight - Header + 2 && y <= GateHeight - 3) return HazardDark;   // the lamp's housing
                return (x / 4 + y) % 2 == 0 ? Hazard : HazardDark;
            }
            if (post) return x == 0 || x == width - 1 ? Outline : x == 1 || x == width - 4 ? SteelLit : Steel;
            if (y <= 2) return y == 0 ? Outline : SteelDark;
            return default;
        });
    }

    // The shutter: heavy slats, a striped edge along its foot.
    static Texture2D DrawShutter(int width, int height)
    {
        return PixelArt.MakeTexture(width, height, (x, y) =>
        {
            if (x == 0 || x == width - 1) return Outline;
            if (y <= 4) return y == 0 ? Outline : (x / 4 + y) % 2 == 0 ? Hazard : HazardDark;
            int slat = (y - 5) % 6;
            if (slat == 0) return Outline;
            if (slat == 1) return SteelLit;
            return x % 16 == 8 && slat == 3 ? SteelDark : Steel;
        });
    }
}
