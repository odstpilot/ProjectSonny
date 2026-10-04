using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Chapter 2, the upper maintenance deck: scavenging scrap and making something to fight back with. Arriving up the
// stairs (BlockedWay), Pip spots the workbench and the objective becomes the parts for an EMP, counted as they're found
// ("Find parts for an EMP (3/7)"): two batteries, three circuit boards, and two lots of wire. Scrap's all over the station
// (ScrapPickup), so some of it may be in hand already, and there's more than the EMP takes: metal sheets for a crowbar,
// and plenty of everything for the scrap gun, the dearest thing on the bench. At the workbench (CraftingBench) the
// crafting screen opens (CraftingScreen), each thing with the parts it takes.
// Made, a thing goes in the technician's hand and on the hotbar. The EMP shorts out every patrol bot and camera in its
// cone for a few seconds (EmpPulse) and recharges between pulses; Pip says so, and the prompt shows how to fire it
// until they do. Further along the bench: the blaster (Data/Weapons/Blaster), which charges up before it fires, and two
// upgrades to the suit, servo braces (faster on their feet) and armor plating (more health), for good once made. The
// first time the bench is used, a card says how it all works (TipCard, kept in Pip's log to read again).
// Then the objective is finding the control room, pinned on the map (its door is badge-only:
// BadgeLockout, which sends them on to the comms ring, CommsRing). The crowbar's a swing (melee) for a bot that
// gets too close, and the scrap gun fires bits of scrap at range. ChapterTwoDirector picks up from any of it on a save (Owns, Resume); the scrap and what's been made
// come back with the Inventory.
// Built by ChapterOneBuilder for ChapterTwoBuilder.
public class Workshop : MonoBehaviour
{
    public const string EmpId = "EMP";
    public const string CrowbarId = "Crowbar";
    public const string ScrapGunId = "Scrap Gun";
    public const string BlasterId = "Blaster";
    public const string ServoId = "Servo Braces";
    public const string PlatingId = "Armor Plating";

    public PlayerController player;
    public CraftingBench bench;
    [Tooltip("The EMP it makes (Data/Weapons/EMP), the crowbar (Data/Weapons/Crowbar), and the scrap gun (Data/Weapons/ScrapGun).")]
    public EmpWeaponData emp;
    public WeaponData crowbar;
    public WeaponData scrapGun;
    [Tooltip("The blaster it makes (Data/Weapons/Blaster): hold to charge, let go to fire.")]
    public WeaponData blaster;
    public Inventory.Stack[] empCost =
    {
        new Inventory.Stack(Inventory.Material.Battery, 2),
        new Inventory.Stack(Inventory.Material.Circuit, 3),
        new Inventory.Stack(Inventory.Material.Wire, 2),
    };
    public Inventory.Stack[] crowbarCost = { new Inventory.Stack(Inventory.Material.MetalSheet, 4) };
    public Inventory.Stack[] scrapGunCost =
    {
        new Inventory.Stack(Inventory.Material.MetalSheet, 5),
        new Inventory.Stack(Inventory.Material.Circuit, 3),
        new Inventory.Stack(Inventory.Material.Battery, 2),
        new Inventory.Stack(Inventory.Material.Wire, 3),
    };

    public Inventory.Stack[] blasterCost =
    {
        new Inventory.Stack(Inventory.Material.Circuit, 3),
        new Inventory.Stack(Inventory.Material.Battery, 2),
        new Inventory.Stack(Inventory.Material.Wire, 2),
    };
    public Inventory.Stack[] servoCost =
    {
        new Inventory.Stack(Inventory.Material.Wire, 2),
        new Inventory.Stack(Inventory.Material.Battery, 1),
        new Inventory.Stack(Inventory.Material.MetalSheet, 2),
    };
    public Inventory.Stack[] platingCost =
    {
        new Inventory.Stack(Inventory.Material.MetalSheet, 4),
        new Inventory.Stack(Inventory.Material.Circuit, 1),
    };

    [Header("Upgrades")]
    [Tooltip("Servo braces: how much faster the technician moves, walking and running.")]
    public float servoSpeedBoost = 1.2f;
    [Tooltip("Armor plating: how much more health they have.")]
    public float platingHealth = 2f;

    [Header("Objectives")]
    [Tooltip("Shown with how many of the EMP's parts have been found, like \"Find parts for an EMP (1/4)\".")]
    public string scavengeObjective = "Find parts for an EMP";
    public string craftObjective = "Make an EMP at the workbench";
    public string afterObjective = "Find the control room";
    [Tooltip("The room pinned on the map once the EMP's made, by its marker in the layout, and what the pin says.")]
    public char afterRoom = 'o';
    public string afterLabel = "Control Room";

    [Header("Lines")]
    [Tooltip("Pip, once there's everything for the EMP. ~ opens a line with static; ^ says it happily; [E] shows a key.")]
    [TextArea] public string[] pipEnoughScrap = { "^That's everything for the EMP. Get it to the workbench." };
    [Tooltip("Pip, once the EMP's made.")]
    [TextArea] public string[] pipEmpMade =
    {
        "^It works! One pulse shorts out any bot or camera in front of you for a few seconds.",
        "~It needs a moment to recharge between pulses. Make them count.",
        "~Now the control room. The reactor core has stairs down to it.",
    };
    [Tooltip("Pip, once the crowbar's made.")]
    [TextArea] public string[] pipCrowbarMade = { "~A crowbar. If a bot gets too close, swing first. The number keys switch what you're holding." };
    [Tooltip("Pip, once the scrap gun's made.")]
    [TextArea] public string[] pipScrapGunMade = { "^A scrap gun! Point and click. Now we can hit back from a distance." };
    [TextArea] public string[] pipBlasterMade = { "^The blaster. Hold to charge it, let go when it's full." };
    [TextArea] public string[] pipServoMade = { "^Servo braces. You'll move faster now." };
    [TextArea] public string[] pipPlatingMade = { "^Armor plating. You can take more hits." };

    [Header("Respawn and testing")]
    [Tooltip("Where the player comes back after dying, while they're on the deck (the top of the stairs).")]
    public Vector2 respawnAt;
    [Tooltip("Where the player stands for a tester's jump to after the EMP's made: in front of the bench.")]
    public Vector2 benchStandAt;

    private bool toldEnough, teaching, toldHowTo;
    private readonly HashSet<string> upgraded = new HashSet<string>();
    private readonly List<CraftingScreen.Recipe> madeAtBench = new List<CraftingScreen.Recipe>();

    void OnEnable()
    {
        if (bench != null) bench.Used += OnBench;
        EmpPulse.Fired += OnFired;
        CraftingScreen.Crafted += OnCrafted;
    }

    void OnDisable()
    {
        if (bench != null) bench.Used -= OnBench;
        EmpPulse.Fired -= OnFired;
        CraftingScreen.Crafted -= OnCrafted;
    }

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
    }

    // Keeps the objective in step with the scrap: counting up to the EMP, then making it.
    void Update()
    {
        if (bench != null) bench.promptText = "USE WORKBENCH";
        if (Inventory.Made(EmpId)) return;
        TutorialHud hud = TutorialHud.Get();
        if (!hud.Objective.StartsWith(scavengeObjective) && hud.Objective != craftObjective) return;
        bool enough = Inventory.Has(empCost);
        string want = enough ? craftObjective : ScavengeText(Inventory.Toward(empCost));
        if (hud.Objective == want) return;
        if (enough && !toldEnough && SuitHelper.Exists) SuitHelper.Get().Tell(pipEnoughScrap);
        toldEnough |= enough;
        hud.SetObjective(want);
    }

    public string ScavengeText(int found) => $"{scavengeObjective} ({found}/{PartsNeeded})";

    int PartsNeeded
    {
        get
        {
            int parts = 0;
            foreach (Inventory.Stack need in empCost) parts += need.count;
            return parts;
        }
    }

    public CraftingScreen.Recipe[] Recipes => new[]
    {
        new CraftingScreen.Recipe
        {
            id = EmpId, name = "EMP", cost = empCost, picture = EmpPicture,
            description = "A pulse that shorts out every bot and camera in front of you for a few seconds.",
        },
        new CraftingScreen.Recipe
        {
            id = CrowbarId, name = "Crowbar", cost = crowbarCost, picture = CrowbarPicture,
            description = "Four metal sheets, rolled and bent. Something to swing at a bot that gets too close.",
        },
        new CraftingScreen.Recipe
        {
            id = ScrapGunId, name = "Scrap Gun", cost = scrapGunCost, picture = scrapGun != null && scrapGun.sprite != null ? scrapGun.sprite : CrowbarPicture,
            description = "Fires bits of scrap, hard. Takes a lot of parts, but it hits a bot from across a room.",
        },
        new CraftingScreen.Recipe
        {
            id = BlasterId, name = "Blaster", cost = blasterCost, picture = blaster != null && blaster.sprite != null ? blaster.sprite : EmpPicture,
            description = "Hold to charge a ball of energy, let go when it's full. It blows up on whatever it hits.",
        },
        new CraftingScreen.Recipe
        {
            id = ServoId, name = "Servo Braces", cost = servoCost, picture = ServoPicture,
            description = "Motors strapped to the suit's legs. Faster on your feet, walking and running. For good.",
        },
        new CraftingScreen.Recipe
        {
            id = PlatingId, name = "Armor Plating", cost = platingCost, picture = PlatingPicture,
            description = "Metal sheets riveted over the suit. You can take more hits. For good.",
        },
    };

    // How the bench works, the first time it's used.
    static readonly TipCard.Row[] BenchTips =
    {
        new TipCard.Row(TipCard.Picture.None, "Scrap",
            "The blue glows are scrap: walk over them. Every piece is a part, a metal sheet, a circuit board, a battery, or wire."),
        new TipCard.Row("Make things", "Pick something and make it. Each lists the parts it takes, green when you've got enough.", "A", "D", "E"),
        new TipCard.Row("Weapons", "The EMP shorts out bots and cameras. The crowbar swings; the scrap gun and the blaster shoot. Switch with the number keys.",
            "1", "2", "3"),
        new TipCard.Row(TipCard.Picture.None, "Upgrades",
            "Servo braces make you faster; armor plating lets you take more hits. Once made, they're yours for good."),
    };

    void OnBench() => StartCoroutine(UseBench());

    IEnumerator UseBench()
    {
        madeAtBench.Clear();
        if (!toldHowTo)
        {
            toldHowTo = true;
            yield return TipCard.Show("The workbench", BenchTips);
        }
        yield return CraftingScreen.Show(Recipes);
        if (bench != null) bench.Done();
        // What was made, once the screen's away: in hand, and Pip on what it does.
        foreach (CraftingScreen.Recipe made in madeAtBench)
        {
            if (made.id == EmpId)
            {
                if (SuitHelper.Exists) SuitHelper.Get().Tell(pipEmpMade);
                Teach();
                Next();
            }
            else if (made.id == CrowbarId && SuitHelper.Exists) SuitHelper.Get().Tell(pipCrowbarMade);
            else if (made.id == ScrapGunId && SuitHelper.Exists) SuitHelper.Get().Tell(pipScrapGunMade);
            else if (made.id == BlasterId && SuitHelper.Exists) SuitHelper.Get().Tell(pipBlasterMade);
            else if (made.id == ServoId && SuitHelper.Exists) SuitHelper.Get().Tell(pipServoMade);
            else if (made.id == PlatingId && SuitHelper.Exists) SuitHelper.Get().Tell(pipPlatingMade);
        }
    }

    void OnCrafted(CraftingScreen.Recipe recipe)
    {
        madeAtBench.Add(recipe);
        if (recipe.id == ServoId || recipe.id == PlatingId) Upgrade(recipe.id);
        else Give(recipe.id, true);
        if (bench != null) bench.Burst();
    }

    // On the hotbar, and in hand if asked.
    void Give(string id, bool equip)
    {
        WeaponData weapon = id == EmpId ? emp : id == CrowbarId ? crowbar : id == ScrapGunId ? scrapGun : id == BlasterId ? blaster : null;
        if (weapon == null || player == null) return;
        if (player.TryGetComponent(out WeaponHotbar hotbar) && !hotbar.slots.Contains(weapon)) hotbar.slots.Add(weapon);
        if (equip && player.TryGetComponent(out PlayerCombat combat)) combat.Equip(weapon);
    }

    // An upgrade on the suit, once: faster, or tougher.
    void Upgrade(string id)
    {
        if (player == null || !upgraded.Add(id)) return;
        if (id == ServoId) player.moveSpeed *= servoSpeedBoost;
        else if (id == PlatingId && player.TryGetComponent(out Health health))
        {
            health.maxHealth += platingHealth;
            health.Heal(platingHealth);
        }
    }

    // Everything made but the EMP back on the hotbar, and the upgrades back on the suit (the Inventory's been loaded).
    void GiveBackMade()
    {
        foreach (string id in new[] { CrowbarId, ScrapGunId, BlasterId })
            if (Inventory.Made(id)) Give(id, false);
        foreach (string id in new[] { ServoId, PlatingId })
            if (Inventory.Made(id)) Upgrade(id);
    }

    // How to fire the EMP, until they do.
    void Teach()
    {
        teaching = true;
        int slot = player != null && player.TryGetComponent(out WeaponHotbar hotbar) ? hotbar.slots.IndexOf(emp) + 1 : 1;
        TutorialHud.Get().ShowPromptWithHint("Fire the EMP", $"[{slot}] to hold it. Shorts out bots and cameras", "LEFT CLICK");
    }

    void OnFired()
    {
        if (!teaching) return;
        teaching = false;
        TutorialHud.Get().CompletePrompt();
    }

    void Next()
    {
        TutorialHud.Get().SetObjective(afterObjective);
        MapScreen.SetTarget(afterRoom, afterLabel);
    }

    // --- Picking up from a save (ChapterTwoDirector) ---

    // One of this deck's objectives.
    public bool Owns(string objective) =>
        !string.IsNullOrEmpty(objective) && (objective.StartsWith(scavengeObjective) || objective == craftObjective || objective == afterObjective);

    // What was made back on the hotbar (the Inventory's been loaded already), the EMP in hand, and the objective.
    public void Resume(string objective)
    {
        if (objective == afterObjective && !Inventory.Made(EmpId)) Inventory.Grant(EmpId);     // a tester's jump past it
        GiveBackMade();
        if (Inventory.Made(EmpId)) Give(EmpId, true);
        toldEnough = Inventory.Has(empCost);
        if (player != null && player.TryGetComponent(out PlayerHealthHandler handler))
            handler.RespawnPoint = new Vector3(respawnAt.x, respawnAt.y, player.transform.position.z);
        if (objective == afterObjective) Next();
        else TutorialHud.Get().SetObjective(objective);
    }

    // Picking up from somewhere past the deck: whatever was made back on the hotbar, the EMP in hand (granted, for a
    // tester's jump that skipped making it), and the respawn point at the top of the stairs until a later stage moves it.
    // The later stage sets the objective.
    public void ResumePast()
    {
        if (!Inventory.Made(EmpId)) Inventory.Grant(EmpId);
        GiveBackMade();
        Give(EmpId, true);
        toldEnough = true;
        if (player != null && player.TryGetComponent(out PlayerHealthHandler handler))
            handler.RespawnPoint = new Vector3(respawnAt.x, respawnAt.y, player.transform.position.z);
    }

    // --- Pictures, made in code ---

    static Sprite empPicture, crowbarPicture, servoPicture, platingPicture;

    static Sprite ServoPicture => servoPicture != null ? servoPicture : (servoPicture = PixelArt.FromText(new[]
    {
        "....######........",
        "...#MMMMMM#.......",
        "...#MmmmmM#.......",
        "...#mbbbbm#.......",
        "...#mmmmmm#.......",
        "....#mmmm#........",
        "....#rrrr#........",
        "....#mmmm#........",
        "...#MmmmmM#.......",
        "...#mbbbbm#.......",
        "...#mmmmmm#.......",
        "....#mmmm#........",
        "....#m##m####.....",
        "....#mmmmmmmm#....",
        ".....#########....",
    }, Palette, 16f, new Vector2(0.5f, 0.5f)));

    static Sprite PlatingPicture => platingPicture != null ? platingPicture : (platingPicture = PixelArt.FromText(new[]
    {
        "..##############..",
        ".#MMMMMMMMMMMMMM#.",
        ".#M#mmmmmmmmmm#M#.",
        ".#MmmmmmmmmmmmmM#.",
        ".#MmmmmmmmmmmmmM#.",
        ".#MmmmmrrrrmmmmM#.",
        ".#MmmmmrrrrmmmmM#.",
        "..#MmmmmmmmmmmM#..",
        "..#MmmmmmmmmmmM#..",
        "...#M#mmmmmm#M#...",
        "....#MmmmmmmM#....",
        ".....#MMMMMM#.....",
        "......######......",
    }, Palette, 16f, new Vector2(0.5f, 0.5f)));

    static Sprite EmpPicture => empPicture != null ? empPicture : (empPicture = PixelArt.FromText(new[]
    {
        "......bb..........",
        ".....b..b.........",
        "....b.##.b........",
        "...#######........",
        "..#MMMMMMM#.......",
        ".#MmmmmmmmM####...",
        ".#mCcCcCcmmmmmm#..",
        ".#mCcCcCcmmbbbb#..",
        ".#mmmmmmmmmmmmm#..",
        "..#mmmmmm######...",
        "...#mmmm#.........",
        "...#m##m#.........",
        "...#mm#m#.........",
        "....####..........",
    }, Palette, 16f, new Vector2(0.5f, 0.5f)));

    static Sprite CrowbarPicture => crowbarPicture != null ? crowbarPicture : (crowbarPicture = PixelArt.FromText(new[]
    {
        "..............##..",
        ".............#rr#.",
        "............#rr#..",
        "...........#rr#...",
        "..........#rr#....",
        ".........#rr#.....",
        "........#rr#......",
        ".......#rr#.......",
        "......#rr#........",
        ".....#rr#.........",
        "..##rrr#..........",
        ".#rr###...........",
        ".#r#..............",
        "..#...............",
    }, Palette, 16f, new Vector2(0.5f, 0.5f)));

    static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
    {
        { '#', new Color32(20, 22, 28, 255) },
        { 'm', new Color32(120, 128, 140, 255) },
        { 'M', new Color32(176, 184, 196, 255) },
        { 'c', new Color32(196, 112, 52, 255) },
        { 'C', new Color32(236, 160, 90, 255) },
        { 'b', new Color32(90, 210, 255, 255) },
        { 'r', new Color32(176, 44, 38, 255) },
    };
}
