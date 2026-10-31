using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// scrap to pick up
public class ScrapPickup : Terminal
{
    const float PixelsPerUnit = 16f;
    static readonly Color GlowColor = new Color(0.4f, 0.85f, 1f);

    [Tooltip("Unique in the scene: how the Inventory remembers it's been taken.")]
    public string id = "";
    public Inventory.Material material;
    [Tooltip("How many of the part it is.")]
    public int amount = 1;
    [Tooltip("What the technician says, picking it up.")]
    [TextArea] public string technicianLine = "";

    private SpriteRenderer art;
    private Light2D glow;
    private float phase;

    public override bool InUse => false;
    protected override string PromptText => $"TAKE {Inventory.Name(material).ToUpperInvariant()}";
    protected override Vector3 PromptPoint => transform.position + new Vector3(0f, 0.8f, 0f);

    protected override void Awake()
    {
        base.Awake();
        phase = Random.value * 10f;
        Inventory.Changed += CheckTaken;
        art = new GameObject("Scrap").AddComponent<SpriteRenderer>();
        art.transform.SetParent(transform, false);
        art.sprite = Make(material);
        DepthSort.Group(gameObject);

        glow = new GameObject("Glow").AddComponent<Light2D>();
        glow.transform.SetParent(transform, false);
        glow.lightType = Light2D.LightType.Point;
        glow.color = GlowColor;
        glow.pointLightInnerRadius = 0f;
        glow.pointLightOuterRadius = 0.8f;
        glow.falloffIntensity = 0.6f;
    }

    void OnDestroy() => Inventory.Changed -= CheckTaken;

    protected override void Start()
    {
        base.Start();
        CheckTaken();
    }

    void CheckTaken()
    {
        if (this != null) gameObject.SetActive(!Inventory.Took(id));
    }

    protected override void Update()
    {
        base.Update();
        float t = Time.time + phase;
        art.transform.localPosition = new Vector3(0f, 0.05f * Mathf.Sin(t * 2.5f), 0f);
        glow.intensity = 0.28f + 0.1f * Mathf.Sin(t * 3.1f);
        if (Random.value < Time.deltaTime * 0.25f)
            HitEffects.Sparks((Vector2)transform.position + new Vector2(0.15f, 0.2f), Vector2.up, 2, 1.2f, 90f, Color.white, GlowColor);
    }

    protected override void Use(Transform player)
    {
        if (!string.IsNullOrEmpty(technicianLine)) TechnicianVoice.Say(technicianLine);
        LightFlash.Spawn(transform.position, GlowColor, 1.2f, 0.8f, 0.2f);
        Inventory.Add(id, material, amount);
        InventoryHud.Collected(material, amount, transform.position);
    }

    static readonly Dictionary<Inventory.Material, Sprite> sprites = new Dictionary<Inventory.Material, Sprite>();

    public static Sprite Make(Inventory.Material material)
    {
        if (sprites.TryGetValue(material, out Sprite made) && made != null) return made;
        string[] rows = material switch
        {
            Inventory.Material.MetalSheet => new[]
            {
                "...#########..",
                "..#MMMMMMMMM#.",
                ".#MmMmmmmmMmM#",
                "#mmmmmmmmmmmm#",
                "#mommmmmmmmom#",
                "#mmmmmmmmmmm#.",
                ".#mommmmmmom#.",
                "..##########..",
            },
            Inventory.Material.Circuit => new[]
            {
                "############",
                "#gGgGgggGgg#",
                "#g##g#yyy#g#",
                "#g##g#yyy#g#",
                "#gyyyyyggyg#",
                "#gg#####gyg#",
                "#gg#####ggg#",
                "#GgGgggGgGg#",
                "############",
            },
            Inventory.Material.Battery => new[]
            {
                "..#.....#.",
                ".#M#...#r#",
                "##########",
                "#MMMMMMMM#",
                "#mbbbbbbm#",
                "#mbbbbmmm#",
                "#mmmmmmmm#",
                "#mmmmmmmm#",
                "##########",
            },
            _ => new[]
            {
                "...#####....",
                "..#CcCcC#...",
                ".#Cc###cC#..",
                ".#c#...#c#..",
                ".#Cc###cC#..",
                "..#cCcCc#...",
                "...#####CC..",
                "..........Cc",
            },
        };
        made = PixelArt.FromText(rows, Palette, PixelsPerUnit, new Vector2(0.5f, 0.2f));
        sprites[material] = made;
        return made;
    }

    static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
    {
        { '#', new Color32(20, 22, 28, 255) },
        { 'm', new Color32(120, 128, 140, 255) },
        { 'M', new Color32(176, 184, 196, 255) },
        { 'c', new Color32(196, 112, 52, 255) },
        { 'C', new Color32(236, 160, 90, 255) },
        { 'b', new Color32(90, 210, 255, 255) },
        { 'y', new Color32(230, 190, 60, 255) },
        { 'r', new Color32(220, 70, 60, 255) },
        { 'o', new Color32(70, 76, 86, 255) },
        { 'g', new Color32(46, 130, 70, 255) },
        { 'G', new Color32(80, 180, 100, 255) },
    };
}
