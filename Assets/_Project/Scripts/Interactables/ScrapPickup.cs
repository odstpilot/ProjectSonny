using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Scrap lying about the station, each piece a kind of part (Inventory.Material): a sheet of metal, a circuit board, a
// battery, a coil of wire. It bobs a little and gives off a faint cold blue, just enough to catch the eye in the dark,
// and now and then it glints. Walk up to it and press E to take it into the Inventory; the technician says what it is. Parts are made into
// things at a workbench (CraftingScreen), each thing needing its own mix of them.
// Taken once, it's gone for good, saves and all (Inventory.Took, by its id).
// The art is made in code until it has its own. Built by ChapterOneBuilder for Chapter 2.
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

    protected override void OnEnable()
    {
        base.OnEnable();
        Inventory.Changed += CheckTaken;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Inventory.Changed -= CheckTaken;
    }

    protected override void Start()
    {
        base.Start();
        CheckTaken();
    }

    // Already in the technician's pockets (a save picking up after it was taken): not on the floor.
    void CheckTaken()
    {
        if (Inventory.Took(id)) gameObject.SetActive(false);
    }

    protected override void Update()
    {
        base.Update();
        float t = Time.time + phase;
        art.transform.localPosition = new Vector3(0f, 0.05f * Mathf.Sin(t * 2.5f), 0f);
        glow.intensity = 0.28f + 0.1f * Mathf.Sin(t * 3.1f);
        // A glint now and then.
        if (Random.value < Time.deltaTime * 0.25f)
            HitEffects.Sparks((Vector2)transform.position + new Vector2(0.15f, 0.2f), Vector2.up, 2, 1.2f, 90f, Color.white, GlowColor);
    }

    protected override void Use(Transform player)
    {
        if (!string.IsNullOrEmpty(technicianLine)) TechnicianVoice.Say(technicianLine);
        LightFlash.Spawn(transform.position, GlowColor, 1.2f, 0.8f, 0.2f);
        Inventory.Add(id, material, amount);     // takes it off the floor, through CheckTaken
    }

    // --- Art ---

    static readonly Dictionary<Inventory.Material, Sprite> sprites = new Dictionary<Inventory.Material, Sprite>();

    // The part's picture: on the floor, and on the workbench's screen.
    public static Sprite Make(Inventory.Material material)
    {
        if (sprites.TryGetValue(material, out Sprite made) && made != null) return made;
        string[] rows = material switch
        {
            // A bent sheet of hull metal, with rivets along its edge.
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
            // A green circuit board, chips and gold tracks.
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
            // A battery pack, charge showing on its side, terminals on top.
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
            // A coil of copper wire, its end trailing.
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
        { 'c', new Color32(196, 112, 52, 255) },     // copper
        { 'C', new Color32(236, 160, 90, 255) },
        { 'b', new Color32(90, 210, 255, 255) },     // charge
        { 'y', new Color32(230, 190, 60, 255) },     // hazard
        { 'r', new Color32(220, 70, 60, 255) },
        { 'o', new Color32(70, 76, 86, 255) },       // rivet
        { 'g', new Color32(46, 130, 70, 255) },      // circuit board
        { 'G', new Color32(80, 180, 100, 255) },
    };
}
