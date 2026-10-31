using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// what the player carries, bottom left
public class InventoryHud : MonoBehaviour
{
    const int SortingOrder = 82;
    const float Margin = 40f;
    const float SlotSize = 64f, SlotGap = 8f;
    const float ChipWidth = 92f, ChipHeight = 40f, RowGap = 10f;
    const float NameSeconds = 1.6f;
    const float FlySeconds = 0.6f;
    const float FadeSpeed = 5f;

    public static readonly Color RackColor = GameUI.Amber;
    public static readonly Color PackColor = GameUI.Cyan;
    static readonly Color SlotEdge = new Color(0.45f, 0.6f, 0.7f, 0.55f);

    static InventoryHud instance;

    class Slot
    {
        public RectTransform rect;
        public Image brackets, icon, drain;
        public TextMeshProUGUI key, name;
        public WeaponData weapon;
        public float width = SlotSize;
        public float pop;
        public float named;
        public float flash;
        public float lastReadiness = 1f;
    }

    class Chip
    {
        public RectTransform rect;
        public Image icon;
        public TextMeshProUGUI count;
        public int pending;
        public float bump;
    }

    class Flyer
    {
        public RectTransform rect;
        public Vector3 from;
        public RectTransform to;
        public float t;
        public System.Action landed;
    }

    private WeaponHotbar hotbar;
    private PlayerCombat combat;
    private RangedCombat ranged;
    private PlayerController player;
    private Canvas canvas;
    private CanvasGroup group, pouchGroup;
    private RectTransform root, rack, pouch, tabKey;
    private readonly List<Slot> slots = new List<Slot>();
    private readonly Dictionary<Inventory.Material, Chip> chips = new Dictionary<Inventory.Material, Chip>();
    private readonly List<Flyer> flyers = new List<Flyer>();
    private readonly HashSet<WeaponData> seen = new HashSet<WeaponData>();
    private WeaponData held;
    private bool blockedLastFrame = true;
    private bool everShown;

    public static InventoryHud Get()
    {
        if (instance == null)
        {
            instance = new GameObject("InventoryHud", typeof(RectTransform)).AddComponent<InventoryHud>();
            instance.Build();
        }
        return instance;
    }

    public void Track(WeaponHotbar tracked)
    {
        hotbar = tracked;
        combat = tracked.GetComponent<PlayerCombat>();
        ranged = tracked.GetComponent<RangedCombat>();
        player = tracked.GetComponent<PlayerController>();
        held = combat != null ? combat.CurrentWeapon : null;
        seen.Clear();
        foreach (WeaponData weapon in tracked.slots) seen.Add(weapon);
    }

    public static void Collected(Inventory.Material material, int amount, Vector3 worldPoint)
    {
        InventoryHud hud = Get();
        if (!SuitFeatures.Has(SuitFeatures.Feature.Inventory))
        {
            SuitFeatures.Unlock(SuitFeatures.Feature.Inventory);
            if (SuitHelper.Exists) SuitHelper.Get().Tell(PipManager.Lines.firstScrap);
        }
        if (!hud.chips.TryGetValue(material, out Chip chip)) return;
        Camera view = Camera.main;
        if (view == null) return;
        chip.pending += amount;
        hud.Fly(ScrapPickup.Make(material), view.WorldToScreenPoint(worldPoint), chip.icon.rectTransform, 44f, () =>
        {
            chip.pending = Mathf.Max(0, chip.pending - amount);
            chip.bump = 1f;
        });
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (hotbar == null)
        {
            group.alpha = 0f;
            return;
        }

        bool anyParts = AnyParts();
        if (!SuitFeatures.Has(SuitFeatures.Feature.Inventory) && anyParts) SuitFeatures.Unlock(SuitFeatures.Feature.Inventory);

        bool showing = (hotbar.slots.Count > 0 || anyParts) && !TutorialHud.Letterboxed && !TutorialHud.ScreenCovered
                       && !DialogueBox.IsOpen && !InventoryScreen.IsOpen;
        group.alpha = Mathf.MoveTowards(group.alpha, showing ? 1f : 0f, dt * FadeSpeed);
        pouchGroup.alpha = Mathf.MoveTowards(pouchGroup.alpha, anyParts || flyers.Count > 0 ? 1f : 0f, dt * FadeSpeed);

        UpdateRack(dt);
        UpdatePouch(dt);
        UpdateFlyers(dt);

        if (!blockedLastFrame && !InventoryScreen.IsOpen && SuitFeatures.Has(SuitFeatures.Feature.Inventory)
            && (Input.GetKeyDown(InventoryScreen.Key) || Input.GetKeyDown(InventoryScreen.OtherKey)))
        {
            StartCoroutine(InventoryScreen.Show(hotbar));
        }
    }

    void LateUpdate() => blockedLastFrame = Blocked();

    bool Blocked()
    {
        if (Time.timeScale <= 0f || InventoryScreen.IsOpen) return true;
        if (DialogueBox.Busy || TutorialHud.ScreenCovered || TutorialHud.Letterboxed || Locker.IsPlayerHidden) return true;
        if (MapScreen.IsOpen || CraftingScreen.IsOpen || PipLog.IsOpen || TipCard.IsOpen || PauseMenu.IsPaused) return true;
        if (player != null && (player.IsScripted || !player.enabled)) return true;
        Health health = hotbar != null ? hotbar.GetComponent<Health>() : null;
        return health != null && health.IsDead;
    }

    static bool AnyParts()
    {
        foreach (Inventory.Material material in Inventory.Materials)
            if (Inventory.Count(material) > 0) return true;
        return false;
    }

    void UpdateRack(float dt)
    {
        while (slots.Count < hotbar.slots.Count) slots.Add(NewSlot());
        WeaponData inHand = combat != null ? combat.CurrentWeapon : null;
        if (inHand != held)
        {
            held = inHand;
            Slot now = slots.Find(s => s.weapon == held && held != null);
            if (now != null) now.named = NameSeconds;
            foreach (Slot other in slots)
                if (other != now) other.named = 0f;
        }

        bool watching = Time.timeScale > 0f && group.alpha > 0.9f;
        if (!everShown) foreach (WeaponData weapon in hotbar.slots) seen.Add(weapon);
        everShown |= group.alpha >= 1f;
        float x = 0f;
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            WeaponData weapon = i < hotbar.slots.Count ? hotbar.slots[i] : null;
            slot.rect.gameObject.SetActive(weapon != null);
            if (weapon == null) continue;

            if (watching && seen.Add(weapon))
            {
                slot.pop = 1f;
                Camera view = Camera.main;
                if (view != null) Fly(WeaponIcon(weapon), view.WorldToScreenPoint(hotbar.transform.position), slot.icon.rectTransform, 56f, null);
            }
            if (slot.weapon != weapon)
            {
                slot.weapon = weapon;
                slot.icon.sprite = WeaponIcon(weapon);
                slot.name.text = weapon.displayName.ToUpperInvariant();
            }
            slot.key.text = i < 9 ? (i + 1).ToString() : "";

            bool inHandNow = weapon == held;
            slot.named = Mathf.Max(0f, slot.named - dt);
            float nameWidth = slot.named > 0f ? slot.name.preferredWidth + 14f : 0f;
            slot.width = Mathf.Lerp(slot.width, SlotSize + nameWidth, 1f - Mathf.Exp(-dt * 14f));
            slot.name.alpha = Mathf.Clamp01((slot.width - SlotSize) / Mathf.Max(1f, slot.name.preferredWidth));
            slot.rect.sizeDelta = new Vector2(slot.width, SlotSize);
            slot.rect.anchoredPosition = new Vector2(x, SlotSize * 0.5f);
            x += slot.width + SlotGap;

            float readiness = weapon is RangedWeaponData shooter && ranged != null ? ranged.Readiness(shooter) : 1f;
            if (readiness >= 1f && slot.lastReadiness < 1f) slot.flash = 1f;
            slot.lastReadiness = readiness;
            slot.drain.rectTransform.anchorMin = new Vector2(0f, readiness);
            slot.drain.enabled = readiness < 1f;

            slot.pop = Mathf.MoveTowards(slot.pop, 0f, dt * 2.5f);
            slot.flash = Mathf.MoveTowards(slot.flash, 0f, dt * 3f);
            float pop = slot.pop * slot.pop;
            slot.rect.localScale = Vector3.one * (1f + pop * 0.35f);
            Color edge = inHandNow ? RackColor : SlotEdge;
            slot.brackets.color = Color.Lerp(edge, Color.white, Mathf.Max(pop, slot.flash));
            slot.icon.color = inHandNow ? Color.white : new Color(0.78f, 0.82f, 0.86f, 0.85f);
            slot.key.color = inHandNow ? RackColor : GameUI.Dim;
        }
    }

    void UpdatePouch(float dt)
    {
        foreach (KeyValuePair<Inventory.Material, Chip> pair in chips)
        {
            Chip chip = pair.Value;
            int have = Mathf.Max(0, Inventory.Count(pair.Key) - chip.pending);
            chip.count.text = have.ToString();
            chip.count.color = Color.Lerp(have > 0 ? GameUI.Text : GameUI.Dim, PackColor, chip.bump);
            chip.icon.color = have > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            chip.bump = Mathf.MoveTowards(chip.bump, 0f, dt * 2.5f);
            chip.rect.localScale = Vector3.one * (1f + chip.bump * chip.bump * 0.2f);
        }
        bool on = SuitFeatures.Has(SuitFeatures.Feature.Inventory);
        tabKey.gameObject.SetActive(on);
    }

    void Fly(Sprite sprite, Vector3 from, RectTransform to, float size, System.Action landed)
    {
        Image image = GameUI.NewRect("Flying", transform).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.rectTransform.sizeDelta = new Vector2(size, size);
        image.rectTransform.position = from;
        flyers.Add(new Flyer { rect = image.rectTransform, from = from, to = to, landed = landed });
    }

    void UpdateFlyers(float dt)
    {
        float lift = 140f * canvas.scaleFactor;
        for (int i = flyers.Count - 1; i >= 0; i--)
        {
            Flyer flyer = flyers[i];
            flyer.t += dt / FlySeconds;
            float p = Mathf.SmoothStep(0f, 1f, flyer.t);
            Vector3 to = flyer.to != null ? flyer.to.TransformPoint(flyer.to.rect.center) : flyer.from;
            flyer.rect.position = Vector3.Lerp(flyer.from, to, p) + Vector3.up * Mathf.Sin(p * Mathf.PI) * lift;
            flyer.rect.localScale = Vector3.one * Mathf.Lerp(1.4f, 0.8f, p);
            if (flyer.t < 1f) continue;
            flyer.landed?.Invoke();
            Destroy(flyer.rect.gameObject);
            flyers.RemoveAt(i);
        }
    }

    void Build()
    {
        canvas = PromptBadge.MakeCanvas(gameObject, SortingOrder);
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = group.blocksRaycasts = false;

        root = GameUI.NewRect("Wrist", transform);
        SuitDisplay.Attach(root);
        root.anchorMin = root.anchorMax = root.pivot = Vector2.zero;
        root.anchoredPosition = new Vector2(Margin, Margin);
        root.sizeDelta = new Vector2(600f, SlotSize + RowGap + ChipHeight);

        rack = GameUI.NewRect("Rack", root);
        rack.anchorMin = rack.anchorMax = rack.pivot = Vector2.zero;
        rack.sizeDelta = new Vector2(600f, SlotSize);

        pouch = GameUI.NewRect("Pouch", root);
        pouch.anchorMin = pouch.anchorMax = pouch.pivot = Vector2.zero;
        pouch.anchoredPosition = new Vector2(0f, SlotSize + RowGap);
        pouch.sizeDelta = new Vector2(600f, ChipHeight);
        pouchGroup = pouch.gameObject.AddComponent<CanvasGroup>();
        pouchGroup.alpha = 0f;

        for (int i = 0; i < Inventory.Materials.Length; i++)
        {
            Inventory.Material material = Inventory.Materials[i];
            var chip = new Chip { rect = GameUI.NewRect("Chip", pouch) };
            chip.rect.anchorMin = chip.rect.anchorMax = Vector2.zero;
            chip.rect.pivot = new Vector2(0.5f, 0.5f);
            chip.rect.sizeDelta = new Vector2(ChipWidth, ChipHeight);
            chip.rect.anchoredPosition = new Vector2(i * (ChipWidth + 6f) + ChipWidth * 0.5f, ChipHeight * 0.5f);
            Image back = GameUI.Sliced("Back", chip.rect, GameUI.SoftPanelSprite, Color.white);
            back.pixelsPerUnitMultiplier = 1f / GameUI.SoftPixelSize;
            GameUI.Fill(back.rectTransform);
            chip.icon = GameUI.NewRect("Icon", chip.rect).gameObject.AddComponent<Image>();
            chip.icon.sprite = ScrapPickup.Make(material);
            chip.icon.preserveAspect = true;
            chip.icon.raycastTarget = false;
            chip.icon.rectTransform.anchorMin = chip.icon.rectTransform.anchorMax = chip.icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            chip.icon.rectTransform.sizeDelta = new Vector2(34f, 32f);
            chip.icon.rectTransform.anchoredPosition = new Vector2(6f, 0f);
            chip.count = GameUI.Label("Count", chip.rect, "0", 32f, GameUI.Text, TextAlignmentOptions.MidlineRight, GameUI.Readout);
            chip.count.fontSharedMaterial = GameUI.Shadow(chip.count.font);
            GameUI.Anchor(chip.count.rectTransform, 0f, 0f, 1f, 1f, 40f, 0f, 10f, 0f);
            chips[material] = chip;
        }

        Image tab = GameUI.KeyCap(pouch, "TAB", 22f, out _);
        tabKey = tab.rectTransform;
        tabKey.anchorMin = tabKey.anchorMax = tabKey.pivot = Vector2.zero;
        tabKey.sizeDelta = new Vector2(GameUI.KeyCapWidth("TAB", ChipHeight), ChipHeight);
        tabKey.anchoredPosition = new Vector2(Inventory.Materials.Length * (ChipWidth + 6f) + 4f, 0f);
    }

    Slot NewSlot()
    {
        var slot = new Slot { rect = GameUI.NewRect("Slot", rack) };
        slot.rect.anchorMin = slot.rect.anchorMax = Vector2.zero;
        slot.rect.pivot = new Vector2(0f, 0.5f);
        slot.rect.sizeDelta = new Vector2(SlotSize, SlotSize);

        Image back = GameUI.Sliced("Back", slot.rect, GameUI.SoftPanelSprite, Color.white);
        back.pixelsPerUnitMultiplier = 1f / GameUI.SoftPixelSize;
        GameUI.Fill(back.rectTransform);
        var mask = back.gameObject.AddComponent<RectMask2D>();
        mask.padding = new Vector4(3f, 3f, 3f, 3f);

        slot.drain = GameUI.NewRect("Drain", back.rectTransform).gameObject.AddComponent<Image>();
        slot.drain.color = new Color(0f, 0f, 0f, 0.55f);
        slot.drain.raycastTarget = false;
        slot.drain.rectTransform.anchorMin = Vector2.zero;
        slot.drain.rectTransform.anchorMax = Vector2.one;
        slot.drain.rectTransform.offsetMin = slot.drain.rectTransform.offsetMax = Vector2.zero;

        slot.icon = GameUI.NewRect("Icon", back.rectTransform).gameObject.AddComponent<Image>();
        slot.icon.preserveAspect = true;
        slot.icon.raycastTarget = false;
        slot.icon.rectTransform.anchorMin = slot.icon.rectTransform.anchorMax = slot.icon.rectTransform.pivot = new Vector2(0f, 0.5f);
        slot.icon.rectTransform.sizeDelta = new Vector2(SlotSize - 16f, SlotSize - 16f);
        slot.icon.rectTransform.anchoredPosition = new Vector2(8f, 2f);

        slot.name = GameUI.Label("Name", back.rectTransform, "", 22f, GameUI.Text, TextAlignmentOptions.MidlineLeft, GameUI.Heading);
        slot.name.characterSpacing = 4f;
        slot.name.rectTransform.anchorMin = slot.name.rectTransform.anchorMax = slot.name.rectTransform.pivot = new Vector2(0f, 0.5f);
        slot.name.rectTransform.sizeDelta = new Vector2(240f, SlotSize);
        slot.name.rectTransform.anchoredPosition = new Vector2(SlotSize - 4f, 0f);

        slot.brackets = GameUI.Sliced("Brackets", slot.rect, BracketSprite, SlotEdge);
        GameUI.Fill(slot.brackets.rectTransform);

        slot.key = GameUI.Label("Key", slot.rect, "", 22f, GameUI.Dim, TextAlignmentOptions.TopRight, GameUI.Readout);
        slot.key.fontSharedMaterial = GameUI.Shadow(slot.key.font);
        GameUI.Anchor(slot.key.rectTransform, 0f, 0f, 1f, 1f, 4f, 4f, 7f, 2f);
        return slot;
    }

    static Sprite bracketSprite;
    public static Sprite BracketSprite
    {
        get
        {
            if (bracketSprite != null) return bracketSprite;
            const int Size = 12, Arm = 4;
            Texture2D texture = PixelArt.MakeTexture(Size, Size, (x, y) =>
            {
                int fromRight = Size - 1 - x, fromTop = Size - 1 - y;
                bool nearX = x < Arm || fromRight < Arm, nearY = y < Arm || fromTop < Arm;
                bool edgeX = x == 0 || fromRight == 0, edgeY = y == 0 || fromTop == 0;
                return (edgeX && nearY) || (edgeY && nearX) ? new Color32(255, 255, 255, 255) : default;
            });
            return bracketSprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(5f, 5f, 5f, 5f));
        }
    }

    public static Sprite WeaponIcon(WeaponData weapon)
    {
        if (weapon == null) return null;
        if (weapon.sprite != null) return weapon.sprite;
        if (weapon is EmpWeaponData) return EmpIcon;
        if (weapon is MeleeWeaponData) return BarIcon;
        return GunIcon;
    }

    static Sprite empIcon, barIcon, gunIcon;

    static Sprite EmpIcon => empIcon != null ? empIcon : (empIcon = PixelArt.FromText(new[]
    {
        "....######......",
        "...#cCcCcc#.....",
        "..#cbbbbbbc#....",
        "..#cbBBBBbc#####",
        "..#cbBBBBbcmmMM#",
        "..#cbbbbbbc#####",
        "...#cCcCcc#mm#..",
        "....######.#m#..",
        "...........###..",
    }, Palette, 16f, new Vector2(0.5f, 0.5f)));

    static Sprite BarIcon => barIcon != null ? barIcon : (barIcon = PixelArt.FromText(new[]
    {
        ".###............",
        "#rr#............",
        "#r#.............",
        "#r##############",
        "#rrrrrrrrrrrrrr#",
        ".###############",
    }, Palette, 16f, new Vector2(0.5f, 0.5f)));

    static Sprite GunIcon => gunIcon != null ? gunIcon : (gunIcon = PixelArt.FromText(new[]
    {
        "..###########...",
        ".#MMMMMMMMMMM###",
        "#mmmbbbmmmmmmmb#",
        "#mmmbbbmmmmmm###",
        "#mm#####mmmm#...",
        "#mm#...######...",
        "####............",
    }, Palette, 16f, new Vector2(0.5f, 0.5f)));

    static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
    {
        { '#', new Color32(20, 22, 28, 255) },
        { 'm', new Color32(120, 128, 140, 255) },
        { 'M', new Color32(176, 184, 196, 255) },
        { 'r', new Color32(200, 70, 50, 255) },
        { 'c', new Color32(196, 112, 52, 255) },
        { 'C', new Color32(236, 160, 90, 255) },
        { 'b', new Color32(90, 210, 255, 255) },
        { 'B', new Color32(220, 250, 255, 255) },
    };
}
