using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Chapter 2, the upper maintenance deck: scavenging scrap and making something to fight back with. Arriving up the
// stairs (BlockedWay), Pip spots the workbench and the objective becomes the parts for an EMP, counted as they're found
// ("Find parts for an EMP (1/4)"): a battery, two circuit boards, and wire. There's more scrap on the deck than that
// (ScrapPickup), scattered to its far corners among the bots and cameras: metal sheets too, enough for a crowbar. At the
// workbench (CraftingBench) the crafting screen opens (CraftingScreen), each thing with the parts it takes.
// Made, a thing goes in the technician's hand and on the hotbar. The EMP shorts out every patrol bot and camera in its
// cone for a few seconds (EmpPulse) and recharges between pulses; Pip says so, and the prompt shows how to fire it
// until they do. Then the objective is the comms ring, pinned on the map. The crowbar's a swing (melee) for a bot that
// gets too close. ChapterTwoDirector picks up from any of it on a save (Owns, Resume); the scrap and what's been made
// come back with the Inventory.
// Built by ChapterOneBuilder for ChapterTwoBuilder.
public class Workshop : MonoBehaviour
{
    public const string EmpId = "EMP";
    public const string CrowbarId = "Crowbar";

    public PlayerController player;
    public CraftingBench bench;
    [Tooltip("The EMP it makes (Data/Weapons/EMP), and the crowbar (Data/Weapons/Crowbar).")]
    public EmpWeaponData emp;
    public WeaponData crowbar;
    public Inventory.Stack[] empCost =
    {
        new Inventory.Stack(Inventory.Material.Battery, 1),
        new Inventory.Stack(Inventory.Material.Circuit, 2),
        new Inventory.Stack(Inventory.Material.Wire, 1),
    };
    public Inventory.Stack[] crowbarCost = { new Inventory.Stack(Inventory.Material.MetalSheet, 2) };

    [Header("Objectives")]
    [Tooltip("Shown with how many of the EMP's parts have been found, like \"Find parts for an EMP (1/4)\".")]
    public string scavengeObjective = "Find parts for an EMP";
    public string craftObjective = "Make an EMP at the workbench";
    public string afterObjective = "Cross the deck to the comms ring";
    [Tooltip("The room pinned on the map once the EMP's made, by its marker in the layout, and what the pin says.")]
    public char afterRoom = 'g';
    public string afterLabel = "Comms Ring";

    [Header("Lines")]
    [Tooltip("Pip, once there's everything for the EMP. ~ opens a line with static; ^ says it happily; [E] shows a key.")]
    [TextArea] public string[] pipEnoughScrap = { "^That's everything for the EMP. Get it to the workbench." };
    [Tooltip("Pip, once the EMP's made.")]
    [TextArea] public string[] pipEmpMade =
    {
        "^It works! One pulse shorts out any bot or camera in front of you for a few seconds.",
        "~It needs a moment to recharge between pulses. Make them count.",
        "~Now, the comms ring, through the far door. Every system on the station talks through it. If I can get in, maybe I can get you into the control room.",
    };
    [Tooltip("Pip, once the crowbar's made.")]
    [TextArea] public string[] pipCrowbarMade = { "~A crowbar. If a bot gets too close, swing first. [1] and [2] switch between what you're holding." };

    [Header("Respawn and testing")]
    [Tooltip("Where the player comes back after dying, while they're on the deck (the top of the stairs).")]
    public Vector2 respawnAt;
    [Tooltip("Where the player stands for a tester's jump to after the EMP's made: in front of the bench.")]
    public Vector2 benchStandAt;

    private bool toldEnough, teaching;
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
            description = "Two metal sheets, rolled and bent. Something to swing at a bot that gets too close.",
        },
    };

    void OnBench() => StartCoroutine(UseBench());

    IEnumerator UseBench()
    {
        madeAtBench.Clear();
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
        }
    }

    void OnCrafted(CraftingScreen.Recipe recipe)
    {
        madeAtBench.Add(recipe);
        Give(recipe.id, true);
        if (bench != null) bench.Burst();
    }

    // On the hotbar, and in hand if asked.
    void Give(string id, bool equip)
    {
        WeaponData weapon = id == EmpId ? emp : id == CrowbarId ? crowbar : null;
        if (weapon == null || player == null) return;
        if (player.TryGetComponent(out WeaponHotbar hotbar) && !hotbar.slots.Contains(weapon)) hotbar.slots.Add(weapon);
        if (equip && player.TryGetComponent(out PlayerCombat combat)) combat.Equip(weapon);
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
        if (Inventory.Made(CrowbarId)) Give(CrowbarId, false);
        if (Inventory.Made(EmpId)) Give(EmpId, true);
        toldEnough = Inventory.Has(empCost);
        if (player != null && player.TryGetComponent(out PlayerHealthHandler handler))
            handler.RespawnPoint = new Vector3(respawnAt.x, respawnAt.y, player.transform.position.z);
        if (objective == afterObjective) Next();
        else TutorialHud.Get().SetObjective(objective);
    }

    // --- Pictures, made in code ---

    static Sprite empPicture, crowbarPicture;

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
