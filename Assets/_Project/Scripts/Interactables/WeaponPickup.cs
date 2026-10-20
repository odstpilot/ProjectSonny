using UnityEngine;
using UnityEngine.Rendering.Universal;

// A weapon lying about the station, found rather than made: walk up to it and press E to take it. It goes on the hotbar
// and in hand, the technician says what it is, and Pip says what it's for. It glows a faint cold blue like the scrap,
// so it catches the eye in the dark.
// Taken once, it's gone for good, saves and all: it's counted as made (Inventory.Grant, by its id), and whatever gives
// back what's been made on a save (Workshop) puts it back on the hotbar.
// Made in code by Workshop for the crowbar, so there's nothing to set up; the picture is the weapon's own sprite.
[RequireComponent(typeof(BoxCollider2D))]
public class WeaponPickup : Terminal
{
    static readonly Color GlowColor = new Color(0.4f, 0.85f, 1f);

    [Tooltip("What it counts as once taken (Inventory.Made), like Workshop.CrowbarId.")]
    public string id = "";
    public WeaponData weapon;
    [Tooltip("What it looks like on the floor if the weapon has no sprite of its own.")]
    public Sprite picture;
    [Tooltip("What the technician says, picking it up.")]
    [TextArea] public string technicianLine = "";
    [Tooltip("Pip, after. ~ opens a line with static; ^ says it happily.")]
    [TextArea] public string[] pipLines = new string[0];

    public event System.Action Taken;

    private SpriteRenderer art;
    private Light2D glow;

    public override bool InUse => false;
    protected override string PromptText => $"TAKE {(weapon != null ? weapon.displayName : id).ToUpperInvariant()}";
    protected override Vector3 PromptPoint => transform.position + new Vector3(0f, 0.8f, 0f);

    // Put down at a spot in the world, unless it's been taken already.
    public static WeaponPickup Place(Vector2 at, string id, WeaponData weapon, Sprite picture, string technicianLine, string[] pipLines)
    {
        var holder = new GameObject($"{id} Pickup");
        holder.transform.position = at;
        var box = holder.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(0.7f, 0.4f);
        WeaponPickup pickup = holder.AddComponent<WeaponPickup>();
        pickup.id = id;
        pickup.weapon = weapon;
        pickup.picture = picture;
        pickup.technicianLine = technicianLine;
        pickup.pipLines = pipLines ?? new string[0];
        pickup.interactRange = 0.7f;
        pickup.ShowArt();
        return pickup;
    }

    protected override void Awake()
    {
        base.Awake();
        Inventory.Changed += CheckTaken;
        art = new GameObject("Weapon").AddComponent<SpriteRenderer>();
        art.transform.SetParent(transform, false);
        // Lying on the floor, on its side.
        art.transform.localRotation = Quaternion.Euler(0f, 0f, 70f);
        DepthSort.Group(gameObject);

        glow = new GameObject("Glow").AddComponent<Light2D>();
        glow.transform.SetParent(transform, false);
        glow.lightType = Light2D.LightType.Point;
        glow.color = GlowColor;
        glow.pointLightInnerRadius = 0f;
        glow.pointLightOuterRadius = 1f;
        glow.falloffIntensity = 0.6f;
        glow.shadowsEnabled = false;
    }

    // Listening the whole time it exists, hidden or not, so a load that puts it back on the floor brings it back.
    void OnDestroy() => Inventory.Changed -= CheckTaken;

    protected override void Start()
    {
        base.Start();
        ShowArt();
        CheckTaken();
    }

    void ShowArt()
    {
        if (art != null) art.sprite = weapon != null && weapon.sprite != null ? weapon.sprite : picture;
    }

    // Off the floor while it's in hand, and back if a save from before it was taken is loaded.
    void CheckTaken()
    {
        if (this != null && !string.IsNullOrEmpty(id)) gameObject.SetActive(!Inventory.Made(id));
    }

    protected override void Update()
    {
        base.Update();
        glow.intensity = 0.35f + 0.12f * Mathf.Sin(Time.time * 3.1f);
        // A glint now and then.
        if (Random.value < Time.deltaTime * 0.3f)
            HitEffects.Sparks((Vector2)transform.position + new Vector2(0.15f, 0.15f), Vector2.up, 2, 1.2f, 90f, Color.white, GlowColor);
    }

    protected override void Use(Transform player)
    {
        if (weapon != null)
        {
            if (player.TryGetComponent(out WeaponHotbar hotbar) && !hotbar.slots.Contains(weapon)) hotbar.slots.Add(weapon);
            if (player.TryGetComponent(out PlayerCombat combat)) combat.Equip(weapon);
        }
        if (!string.IsNullOrEmpty(technicianLine)) TechnicianVoice.Say(technicianLine);
        if (pipLines.Length > 0 && SuitHelper.Exists) SuitHelper.Get().Tell(pipLines);
        LightFlash.Spawn(transform.position, GlowColor, 1.2f, 0.8f, 0.2f);
        Taken?.Invoke();
        ControlHints.TeachSwitching(player.gameObject);
        Inventory.Grant(id);     // takes it off the floor, through CheckTaken
        if (string.IsNullOrEmpty(id)) gameObject.SetActive(false);
    }
}
