using UnityEngine;
using UnityEngine.Rendering.Universal;

// The workbench on the upper maintenance deck, where the technician builds the EMP (EmpCrafting): a steel bench with a
// vise, a soldering iron in its stand, a rack of tools on the pegboard behind, and a work lamp that throws a little pool
// of warm light. Walk up and press E; what that does is up to EmpCrafting (Used), which also says what the prompt says.
// While it's being worked at, the lamp flickers and it throws sparks (Working).
// The art is made in code until it has its own. Built by ChapterOneBuilder for Chapter 2.
public class CraftingBench : Terminal
{
    const float PixelsPerUnit = 16f;
    const int Width = 40, Height = 30;

    [Tooltip("What the prompt says. EmpCrafting changes it as the EMP comes together.")]
    public string promptText = "USE WORKBENCH";

    public event System.Action Used;
    public bool Working { get; set; }

    private bool inUse;
    private Light2D lamp;
    private static Sprite sprite;

    public override bool InUse => inUse;
    protected override string PromptText => promptText;
    protected override Vector3 PromptPoint => transform.position + new Vector3(0f, Height / PixelsPerUnit - DepthSort.FeetToCenter + 0.2f, 0f);

    // The middle of the bench top, where the work happens.
    public Vector2 WorkPoint => (Vector2)transform.position + new Vector2(0f, 14f / PixelsPerUnit - DepthSort.FeetToCenter);

    protected override void Awake()
    {
        base.Awake();
        var art = new GameObject("Bench").AddComponent<SpriteRenderer>();
        art.transform.SetParent(transform, false);
        art.transform.localPosition = new Vector3(0f, -DepthSort.FeetToCenter, 0f);
        if (sprite == null) sprite = Make();
        art.sprite = sprite;
        DepthSort.Group(gameObject);

        lamp = new GameObject("Work Lamp").AddComponent<Light2D>();
        lamp.transform.SetParent(transform, false);
        lamp.transform.localPosition = new Vector3(0.7f, 22f / PixelsPerUnit - DepthSort.FeetToCenter, 0f);
        lamp.lightType = Light2D.LightType.Point;
        lamp.color = new Color(1f, 0.85f, 0.6f);
        lamp.pointLightInnerRadius = 0.2f;
        lamp.pointLightOuterRadius = 2.6f;
        lamp.falloffIntensity = 0.55f;
        lamp.intensity = 0.8f;
    }

    protected override void Update()
    {
        base.Update();
        if (!Working)
        {
            lamp.intensity = 0.8f;
            return;
        }
        lamp.intensity = Random.value < 0.15f ? Random.Range(0.3f, 1.4f) : 0.9f;
        if (Random.value < Time.deltaTime * 14f)
            HitEffects.Sparks(WorkPoint + new Vector2(Random.Range(-0.4f, 0.4f), 0.05f), Vector2.up, 4, 3f, 140f, Color.white, new Color(1f, 0.8f, 0.4f));
    }

    protected override void Use(Transform player)
    {
        if (Used == null) return;
        inUse = true;
        Used.Invoke();
    }

    // Lets go of it, for whoever took it on Used.
    public void Done() => inUse = false;

    // Something's just been made on it: a flash and a shower of sparks.
    public void Burst()
    {
        LightFlash.Spawn(WorkPoint, new Color(0.4f, 0.85f, 1f), 2.2f, 1.4f, 0.35f);
        HitEffects.Sparks(WorkPoint, Vector2.up, 18, 4f, 160f, Color.white, new Color(1f, 0.8f, 0.4f));
    }

    // A steel bench: pegboard with tools behind, the top with a vise and a soldering iron, drawers and legs below.
    static Sprite Make()
    {
        var outline = new Color32(24, 26, 32, 255);
        var peg = new Color32(70, 62, 52, 255);
        var pegHole = new Color32(44, 38, 32, 255);
        var steel = new Color32(118, 126, 138, 255);
        var steelLit = new Color32(168, 176, 188, 255);
        var steelDark = new Color32(74, 80, 90, 255);
        var hazard = new Color32(230, 180, 60, 255);
        var tool = new Color32(196, 70, 60, 255);
        var copper = new Color32(206, 128, 64, 255);
        var lampShade = new Color32(236, 200, 120, 255);

        Texture2D texture = PixelArt.MakeTexture(Width, Height, (x, y) =>
        {
            // Pegboard, rows 16 to 29, set in a little from the ends.
            if (y >= 16)
            {
                if (x < 3 || x > Width - 4) return default;
                if (x == 3 || x == Width - 4 || y == Height - 1) return outline;
                // Tools hanging on it: a wrench, pliers, a hammer.
                if (x >= 7 && x <= 8 && y >= 19 && y <= 26) return steelLit;
                if (x >= 6 && x <= 9 && y >= 25 && y <= 26) return steelLit;
                if (x >= 13 && x <= 14 && y >= 18 && y <= 24) return tool;
                if (x >= 16 && x <= 17 && y >= 18 && y <= 24) return tool;
                if (x >= 21 && x <= 22 && y >= 18 && y <= 24) return copper;
                if (x >= 19 && x <= 24 && y >= 25 && y <= 27) return steelDark;
                // The work lamp's arm and shade, top right.
                if (x == 30 && y >= 19 && y <= 26) return steelDark;
                if (x >= 29 && x <= 34 && y >= 22 && y <= 24) return lampShade;
                return (x + y) % 3 == 0 && x % 2 == 0 ? pegHole : peg;
            }
            // The top: rows 12 to 15, with the vise on the left and the iron on the right.
            if (y >= 12)
            {
                if (x == 0 || x == Width - 1 || y == 12) return outline;
                if (x >= 5 && x <= 10 && y >= 13) return y == 15 ? steelLit : steelDark;       // vise
                if (x >= 27 && x <= 33 && y == 14) return copper;                                // soldering iron
                return y == 15 ? steelLit : steel;
            }
            // Below: a hazard-striped apron, drawers, and legs.
            if (y >= 9)
            {
                if (x == 0 || x == Width - 1 || y == 9) return outline;
                return ((x + y) / 2) % 2 == 0 ? hazard : outline;
            }
            bool leg = x <= 2 || x >= Width - 3;
            if (leg) return x == 0 || x == Width - 1 ? outline : steelDark;
            if (x >= 14 && x <= 25)
            {
                if (x == 14 || x == 25 || y == 0 || y == 4 || y == 8) return outline;
                return (y == 2 || y == 6) && x >= 18 && x <= 21 ? steelLit : steel;
            }
            return default;
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0f));
    }
}
