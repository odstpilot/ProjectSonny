using UnityEngine;
using UnityEngine.Rendering.Universal;

// weapon lying on the floor to pick up
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
    [System.NonSerialized] public string[] pipLines = new string[0];

    public event System.Action Taken;

    private SpriteRenderer art;
    private Light2D glow;

    public override bool InUse => false;
    protected override string PromptText => $"TAKE {(weapon != null ? weapon.displayName : id).ToUpperInvariant()}";
    protected override Vector3 PromptPoint => transform.position + new Vector3(0f, 0.8f, 0f);

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

    void CheckTaken()
    {
        if (this != null && !string.IsNullOrEmpty(id)) gameObject.SetActive(!Inventory.Made(id));
    }

    protected override void Update()
    {
        base.Update();
        glow.intensity = 0.35f + 0.12f * Mathf.Sin(Time.time * 3.1f);
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
        Inventory.Grant(id);
        if (string.IsNullOrEmpty(id)) gameObject.SetActive(false);
    }
}
