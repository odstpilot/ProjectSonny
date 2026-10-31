using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// scrap and crafting on the upper deck
public class Workshop : MonoBehaviour
{
    public const string EmpId = "EMP";
    public const string CrowbarId = "Crowbar";
    public const string BlasterId = "Blaster";
    public const string ServoId = "Servo Braces";
    public const string PlatingId = "Armor Plating";

    public PlayerController player;
    public CraftingBench bench;
    [Tooltip("The EMP it makes (Data/Weapons/EMP), and the crowbar (Data/Weapons/Crowbar).")]
    public EmpWeaponData emp;
    public WeaponData crowbar;
    [Tooltip("The blaster it makes (Data/Weapons/Blaster): hold to charge, let go to fire.")]
    public WeaponData blaster;
    public Inventory.Stack[] empCost =
    {
        new Inventory.Stack(Inventory.Material.Battery, 1),
        new Inventory.Stack(Inventory.Material.Circuit, 1),
        new Inventory.Stack(Inventory.Material.Wire, 1),
    };
    public Inventory.Stack[] crowbarCost = { new Inventory.Stack(Inventory.Material.MetalSheet, 2) };
    [Tooltip("Whether the crowbar's made at the bench. Off, it's found instead, lying at crowbarAt.")]
    public bool crowbarOnBench = false;
    [Tooltip("Where the crowbar lies, in the world. Zero puts it just outside the caved-in hallway (the BlockedWay with stage crowbarNear), on the way to the control room.")]
    public Vector2 crowbarAt;
    public string crowbarNear = "hallway-caved";
    [Tooltip("The technician, picking the crowbar up.")]
    public string crowbarFound = "A crowbar... yeah, I'm taking that";
    public Inventory.Stack[] blasterCost =
    {
        new Inventory.Stack(Inventory.Material.Circuit, 2),
        new Inventory.Stack(Inventory.Material.Battery, 1),
        new Inventory.Stack(Inventory.Material.Wire, 1),
    };
    public Inventory.Stack[] servoCost =
    {
        new Inventory.Stack(Inventory.Material.Wire, 1),
        new Inventory.Stack(Inventory.Material.Battery, 1),
        new Inventory.Stack(Inventory.Material.MetalSheet, 1),
    };
    public Inventory.Stack[] platingCost =
    {
        new Inventory.Stack(Inventory.Material.MetalSheet, 2),
        new Inventory.Stack(Inventory.Material.Circuit, 1),
    };

    [Tooltip("Everything on the bench but the EMP stays locked until the player has this credential (the comms ring's relay hands it over, CommsRing). Empty for nothing locked.")]
    public string unlockCredential = "reactor_core";
    public string lockedText = "LOCKED  COMMS RING";

    [Header("Upgrades")]
    [Tooltip("Servo braces: how much faster the technician moves, walking and running.")]
    public float servoSpeedBoost = 1.2f;
    [Tooltip("Armor plating: how much more health they have.")]
    public float platingHealth = 2f;

    [Header("Objectives")]
    [Tooltip("Shown with how many of the EMP's parts have been found, like \"Find parts for an EMP (1/4)\".")]
    public string scavengeObjective = "Find parts for an EMP";
    public string craftObjective = "Make an EMP at the workbench";
    [Tooltip("The objective once the EMP's made: the comms ring's (CommsRing.arriveObjective), which starts it on getting there.")]
    public string afterObjective = "Reactivate your badge in the comms ring";
    [Tooltip("The room pinned on the map once the EMP's made, by its marker in the layout, and what the pin says.")]
    public char afterRoom = 'g';
    public string afterLabel = "Comms Ring";

    [Header("Lines")]

    [Header("Respawn and testing")]
    [Tooltip("Where the player comes back after dying, while they're on the deck (the top of the stairs).")]
    public Vector2 respawnAt;
    [Tooltip("Where the player stands for a tester's jump to after the EMP's made: in front of the bench.")]
    public Vector2 benchStandAt;

    private bool toldEnough, teaching, toldHowTo, toldUnlocked;

    public bool Unlocked => string.IsNullOrEmpty(unlockCredential) || AccessCredentials.Has(unlockCredential);
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
        if (!crowbarOnBench && crowbar != null) PlaceCrowbar();
    }

    void PlaceCrowbar()
    {
        Vector2 at = crowbarAt;
        if (at == Vector2.zero)
        {
            BlockedWay near = System.Array.Find(FindObjectsByType<BlockedWay>(), w => w.stage == crowbarNear);
            if (near != null && near.setsRespawn) at = near.respawnAt;
            else if (near != null && near.spots.Length > 0) at = near.spots[0];
            else
            {
                Debug.LogWarning($"{name}: there's nowhere set for the crowbar (crowbarAt), and no blocked way \"{crowbarNear}\" to put it by, so it's back on the bench.", this);
                crowbarOnBench = true;
                return;
            }
        }
        WeaponPickup.Place(at, CrowbarId, crowbar, CrowbarPicture, crowbarFound, PipManager.Lines.crowbarMade);
    }

    void Update()
    {
        if (bench != null) bench.promptText = "USE WORKBENCH";
        if (!toldUnlocked && Inventory.Made(EmpId) && Unlocked && !string.IsNullOrEmpty(unlockCredential))
        {
            toldUnlocked = true;
            if (SuitHelper.Exists) SuitHelper.Get().Tell(PipManager.Lines.benchUnlocked);
        }
        if (Inventory.Made(EmpId)) return;
        TutorialHud hud = TutorialHud.Get();
        if (!hud.Objective.StartsWith(scavengeObjective) && hud.Objective != craftObjective) return;
        bool enough = Inventory.Has(empCost);
        string want = enough ? craftObjective : ScavengeText(Inventory.Toward(empCost));
        if (hud.Objective == want) return;
        if (enough && !toldEnough && SuitHelper.Exists) SuitHelper.Get().Tell(PipManager.Lines.enoughScrap);
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

    public CraftingScreen.Recipe[] Recipes
    {
        get
        {
            CraftingScreen.Recipe[] recipes = crowbarOnBench ? AllRecipes : System.Array.FindAll(AllRecipes, r => r.id != CrowbarId);
            if (!Unlocked)
                foreach (CraftingScreen.Recipe recipe in recipes)
                    if (recipe.id != EmpId) recipe.lockedText = lockedText;
            return recipes;
        }
    }

    CraftingScreen.Recipe[] AllRecipes => new[]
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

    static readonly TipCard.Row[] BenchTips =
    {
        new TipCard.Row(TipCard.Picture.None, "Scrap",
            "The blue glows are scrap: walk over them. Every piece is a part, a metal sheet, a circuit board, a battery, or wire."),
        new TipCard.Row("Make things", "Pick something and make it. Each lists the parts it takes, green when you've got enough.", "A", "D", "E"),
        new TipCard.Row("Weapons", "The EMP shorts out bots and cameras. The crowbar swings; the blaster shoots. Switch with the number keys.",
            "1", "2", "3"),
        new TipCard.Row(TipCard.Picture.None, "Upgrades",
            "Servo braces make you faster; armor plating lets you take more hits. Once made, they're yours for good."),
        new TipCard.Row(TipCard.Picture.None, "EMP first",
            "Only the EMP can be made for now. The rest of the bench unlocks once you've got a unit's access from the comms ring."),
    };

    void OnBench() => StartCoroutine(UseBench());

    IEnumerator UseBench()
    {
        madeAtBench.Clear();
        if (!toldHowTo)
        {
            toldHowTo = true;
            TipCard.Log("The workbench", BenchTips);
        }
        yield return CraftingScreen.Show(Recipes);
        if (bench != null) bench.Done();
        foreach (CraftingScreen.Recipe made in madeAtBench)
        {
            if (made.id == EmpId)
            {
                if (SuitHelper.Exists) SuitHelper.Get().Tell(PipManager.Lines.empMade);
                Teach();
                Next();
            }
            else if (made.id == CrowbarId && SuitHelper.Exists) SuitHelper.Get().Tell(PipManager.Lines.crowbarMade);
            else if (made.id == BlasterId && SuitHelper.Exists) SuitHelper.Get().Tell(PipManager.Lines.blasterMade);
            else if (made.id == ServoId && SuitHelper.Exists) SuitHelper.Get().Tell(PipManager.Lines.servoMade);
            else if (made.id == PlatingId && SuitHelper.Exists) SuitHelper.Get().Tell(PipManager.Lines.platingMade);
        }
        if (madeAtBench.Count > 0 && player != null) ControlHints.TeachSwitching(player.gameObject);
    }

    void OnCrafted(CraftingScreen.Recipe recipe)
    {
        madeAtBench.Add(recipe);
        if (recipe.id == ServoId || recipe.id == PlatingId) Upgrade(recipe.id);
        else Give(recipe.id, true);
        if (bench != null) bench.Burst();
    }

    void Give(string id, bool equip)
    {
        WeaponData weapon = id == EmpId ? emp : id == CrowbarId ? crowbar : id == BlasterId ? blaster : null;
        if (weapon == null || player == null) return;
        if (player.TryGetComponent(out WeaponHotbar hotbar) && !hotbar.slots.Contains(weapon)) hotbar.slots.Add(weapon);
        if (equip && player.TryGetComponent(out PlayerCombat combat)) combat.Equip(weapon);
    }

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

    void GiveBackMade()
    {
        foreach (string id in new[] { CrowbarId, BlasterId })
            if (Inventory.Made(id)) Give(id, false);
        foreach (string id in new[] { ServoId, PlatingId })
            if (Inventory.Made(id)) Upgrade(id);
    }

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

    public bool Owns(string objective) =>
        !string.IsNullOrEmpty(objective) && (objective.StartsWith(scavengeObjective) || objective == craftObjective || objective == afterObjective);

    public void Resume(string objective)
    {
        if (objective == afterObjective && !Inventory.Made(EmpId)) Inventory.Grant(EmpId);
        GiveBackMade();
        if (Inventory.Made(EmpId)) Give(EmpId, true);
        toldEnough = Inventory.Has(empCost);
        if (player != null && player.TryGetComponent(out PlayerHealthHandler handler))
            handler.RespawnPoint = new Vector3(respawnAt.x, respawnAt.y, player.transform.position.z);
        if (objective == afterObjective) Next();
        else TutorialHud.Get().SetObjective(objective);
    }

    public void ResumePast()
    {
        if (!Inventory.Made(EmpId)) Inventory.Grant(EmpId);
        GiveBackMade();
        Give(EmpId, true);
        toldEnough = true;
        if (player != null && player.TryGetComponent(out PlayerHealthHandler handler))
            handler.RespawnPoint = new Vector3(respawnAt.x, respawnAt.y, player.transform.position.z);
    }

    public void SkipUnlockedLine()
    {
        if (Unlocked) toldUnlocked = true;
    }

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
