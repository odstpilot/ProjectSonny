using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the pack screen
public class InventoryScreen : MonoBehaviour
{
    public const KeyCode Key = KeyCode.Tab;
    public const KeyCode OtherKey = KeyCode.I;
    const int SortingOrder = 110;
    const float Width = 1680f, Height = 840f;
    const float FadeTime = 0.16f;
    const int MaxRack = 6;
    const float DragDistance = 12f;
    static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.07f, 0.97f);
    static readonly Color RowColor = new Color(1f, 1f, 1f, 0.9f);
    static readonly Color Edge = new Color(0.45f, 0.6f, 0.7f, 0.5f);
    static readonly Color RackColor = InventoryHud.RackColor;
    static readonly Color PackColor = InventoryHud.PackColor;
    static readonly Color SuitColor = SuitHelper.PipColor;

    public static bool IsOpen { get; private set; }

    class RackRow
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Image brackets, icon, charge;
        public TextMeshProUGUI key, name, use, inHand;
        public float flash;
    }

    class PartRow
    {
        public Inventory.Material material;
        public RectTransform rect;
        public Image brackets;
        public TextMeshProUGUI count;
    }

    class Marker
    {
        public Image dot, line;
        public TextMeshProUGUI label;
        public Color color;
    }

    private WeaponHotbar hotbar;
    private PlayerCombat combat;
    private RangedCombat ranged;
    private RectTransform panel, rackColumn, packColumn;
    private RawImage scanlines;
    private float fit = 1f;
    private readonly List<RackRow> rack = new List<RackRow>();
    private readonly List<PartRow> parts = new List<PartRow>();
    private Marker rackMarker, packMarker;

    private int overRow = -1, pressedRow = -1;
    private PartRow overPart;
    private bool dragging;
    private Vector3 pressedAt;
    private Image ghost;

    public static IEnumerator Show(WeaponHotbar hotbar)
    {
        var screen = new GameObject("InventoryScreen", typeof(RectTransform)).AddComponent<InventoryScreen>();
        yield return screen.Run(hotbar);
        Destroy(screen.gameObject);
    }

    IEnumerator Run(WeaponHotbar tracked)
    {
        IsOpen = true;
        float savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        hotbar = tracked;
        combat = hotbar != null ? hotbar.GetComponent<PlayerCombat>() : null;
        ranged = hotbar != null ? hotbar.GetComponent<RangedCombat>() : null;
        PlayerController player = hotbar != null ? hotbar.GetComponent<PlayerController>() : null;
        bool held = player != null && !player.IsScripted;
        if (held) player.SetScriptedInput(Vector2.zero, false);

        CanvasGroup group = Build(player);
        Refresh();
        for (float t = 0f; t < FadeTime; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / FadeTime;
            panel.localScale = new Vector3(fit, fit * Mathf.Lerp(0.04f, 1f, Mathf.SmoothStep(0f, 1f, t / FadeTime)), 1f);
            yield return null;
        }
        group.alpha = 1f;
        panel.localScale = Vector3.one * fit;
        yield return null;

        while (!TutorialHud.ScreenCovered)
        {
            bool clickedOutside = Input.GetMouseButtonDown(0) && !RectTransformUtility.RectangleContainsScreenPoint(panel, Input.mousePosition, null);
            if (!dragging && (Input.GetKeyDown(Key) || Input.GetKeyDown(OtherKey) || Input.GetKeyDown(KeyCode.Escape) || clickedOutside)) break;
            Point();
            Refresh();
            yield return null;
        }

        if (ghost != null) Destroy(ghost.gameObject);
        for (float t = 0f; t < FadeTime; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / FadeTime;
            yield return null;
        }
        if (Time.timeScale == 0f) Time.timeScale = savedTimeScale;
        if (held && player != null) player.ClearScriptedInput();
        IsOpen = false;
    }

    void OnDestroy() => IsOpen = false;

    void Point()
    {
        Vector3 mouse = Input.mousePosition;
        overRow = rack.FindIndex(row => row.rect.gameObject.activeSelf && RectangleHas(row.rect, mouse));
        overPart = parts.Find(row => RectangleHas(row.rect, mouse));
        int count = Mathf.Min(hotbar != null ? hotbar.slots.Count : 0, MaxRack);

        if (Input.GetMouseButtonDown(0) && overRow >= 0 && overRow < count)
        {
            pressedRow = overRow;
            pressedAt = mouse;
        }
        if (pressedRow >= 0 && !dragging && Input.GetMouseButton(0) && (mouse - pressedAt).magnitude > DragDistance) StartDrag();
        if (ghost != null) ghost.rectTransform.position = mouse;
        if (pressedRow >= 0 && Input.GetMouseButtonUp(0))
        {
            if (dragging && overRow >= 0 && overRow < count) MoveSlot(pressedRow, overRow);
            else if (!dragging && overRow == pressedRow) Hold(pressedRow);
            pressedRow = -1;
            dragging = false;
            if (ghost != null) Destroy(ghost.gameObject);
        }

        for (int i = 0; i < count && i < 9; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i) && !Input.GetKeyDown(KeyCode.Keypad1 + i)) continue;
            if (overRow >= 0 && overRow < count && !dragging) MoveSlot(overRow, i);
            else Hold(i);
        }
    }

    static bool RectangleHas(RectTransform rect, Vector3 point) => RectTransformUtility.RectangleContainsScreenPoint(rect, point, null);

    void StartDrag()
    {
        dragging = true;
        ghost = GameUI.NewRect("Dragged", transform).gameObject.AddComponent<Image>();
        ghost.sprite = InventoryHud.WeaponIcon(hotbar.slots[pressedRow]);
        ghost.preserveAspect = true;
        ghost.raycastTarget = false;
        ghost.color = new Color(1f, 1f, 1f, 0.85f);
        ghost.rectTransform.sizeDelta = new Vector2(110f, 72f) * fit;
    }

    void MoveSlot(int from, int to)
    {
        if (from == to) return;
        hotbar.Move(from, to);
        rack[from].flash = rack[to].flash = 1f;
    }

    void Hold(int slot)
    {
        if (combat == null || hotbar == null || slot >= hotbar.slots.Count) return;
        WeaponData weapon = hotbar.slots[slot];
        if (weapon != null && weapon == combat.CurrentWeapon) combat.Unequip();
        else combat.Equip(weapon);
        rack[slot].flash = 1f;
    }

    void Refresh()
    {
        float dt = Time.unscaledDeltaTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);

        int count = hotbar != null ? hotbar.slots.Count : 0;
        WeaponData inHand = combat != null ? combat.CurrentWeapon : null;
        for (int i = 0; i < rack.Count; i++)
        {
            RackRow row = rack[i];
            WeaponData weapon = i < count ? hotbar.slots[i] : null;
            row.rect.gameObject.SetActive(weapon != null);
            if (weapon == null) continue;
            bool holding = weapon == inHand;
            row.icon.sprite = InventoryHud.WeaponIcon(weapon);
            row.icon.enabled = row.icon.sprite != null;
            row.name.text = weapon.displayName.ToUpperInvariant();
            row.use.text = HowItsUsed(weapon);
            row.key.text = (i + 1).ToString();
            row.inHand.enabled = holding;
            row.flash = Mathf.MoveTowards(row.flash, 0f, dt * 3f);
            bool target = dragging && i == overRow && i != pressedRow;
            Color edge = holding ? RackColor : Edge;
            if (i == overRow && !dragging) edge = Color.Lerp(edge, Color.white, 0.35f);
            if (target) edge = Color.Lerp(RackColor, Color.white, pulse);
            row.brackets.color = Color.Lerp(edge, Color.white, row.flash);
            row.group.alpha = dragging && i == pressedRow ? 0.4f : 1f;
            bool recharges = weapon is EmpWeaponData && ranged != null;
            row.charge.transform.parent.gameObject.SetActive(recharges);
            if (recharges) row.charge.rectTransform.anchorMax = new Vector2(ranged.Readiness((RangedWeaponData)weapon), 1f);
        }

        foreach (PartRow row in parts)
        {
            int have = Inventory.Count(row.material);
            row.count.text = $"x{have}";
            row.count.color = have > 0 ? GameUI.Text : GameUI.Dim;
            row.brackets.color = row == overPart ? PackColor : Edge;
        }

        LightUp(rackMarker, RectangleHas(rackColumn, Input.mousePosition), pulse);
        LightUp(packMarker, RectangleHas(packColumn, Input.mousePosition), pulse);
        scanlines.uvRect = new Rect(0f, Time.unscaledTime * 0.2f, 1f, scanlines.rectTransform.rect.height / 6f);
    }

    static void LightUp(Marker marker, bool on, float pulse)
    {
        if (marker == null) return;
        Color color = on ? Color.Lerp(marker.color, Color.white, pulse * 0.6f) : marker.color;
        marker.dot.color = color;
        marker.line.color = new Color(color.r, color.g, color.b, on ? 0.9f : 0.45f);
        marker.label.color = on ? Color.white : marker.color;
        marker.dot.rectTransform.localScale = Vector3.one * (on ? 1.3f + pulse * 0.3f : 1f);
    }

    static string HowItsUsed(WeaponData weapon) => weapon switch
    {
        EmpWeaponData _ => "Pulse: stuns bots and cameras",
        BlasterWeaponData _ => "Hold to charge, let go to fire",
        RangedWeaponData _ => "Point and click to shoot",
        MeleeWeaponData _ => "Swing, up close",
        _ => "",
    };

    CanvasGroup Build(PlayerController player)
    {
        PromptBadge.MakeCanvas(gameObject, SortingOrder);
        RectTransform root = GameUI.Fill((RectTransform)transform);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = group.blocksRaycasts = false;

        Image backdrop = GameUI.NewRect("Backdrop", root).gameObject.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.62f);
        backdrop.raycastTarget = false;
        GameUI.Fill(backdrop.rectTransform);

        Image frame = GameUI.Sliced("Panel", root, GameUI.PanelSprite, PanelColor);
        panel = frame.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(Width, Height);
        fit = Mathf.Min(1f, 1840f / Width, 1040f / Height);
        var edge = frame.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(0.1f, 0.35f, 0.45f);
        edge.effectDistance = new Vector2(4f, -4f);

        TextMeshProUGUI title = GameUI.Label("Title", panel, "PACK", 40f, PackColor, TextAlignmentOptions.TopLeft, GameUI.Heading);
        title.characterSpacing = 8f;
        Place(title.rectTransform, 60f, 36f, 600f, 50f);
        TextMeshProUGUI device = GameUI.Label("Device", panel, "TECHNICIAN SUIT  /  DIAGNOSTIC", 20f, GameUI.Dim, TextAlignmentOptions.TopRight, GameUI.Heading);
        device.characterSpacing = 6f;
        Place(device.rectTransform, Width - 660f, 48f, 600f, 30f);
        Image rule = GameUI.NewRect("Rule", panel).gameObject.AddComponent<Image>();
        rule.color = new Color(0.3f, 0.45f, 0.55f, 0.5f);
        rule.raycastTarget = false;
        Place(rule.rectTransform, 60f, 98f, Width - 120f, GameUI.PixelSize);

        TextMeshProUGUI hint = GameUI.Label("Hint", panel, "CLICK  HOLD IT      DRAG  SWAP      TAB  CLOSE",
            26f, GameUI.Text, TextAlignmentOptions.Bottom);
        GameUI.Anchor(hint.rectTransform, 0f, 0f, 1f, 0f, 60f, 26f, 60f, -60f);

        BuildSuit(player);
        BuildRack();
        BuildPack();
        return group;
    }

    RectTransform Column(string columnName, float x, float width, string heading, string about, Color color)
    {
        RectTransform column = Place(GameUI.NewRect(columnName, panel), x, 120f, width, Height - 120f - 80f);
        Image diamond = GameUI.NewRect("Diamond", column).gameObject.AddComponent<Image>();
        diamond.color = color;
        diamond.raycastTarget = false;
        Place(diamond.rectTransform, 4f, 10f, 12f, 12f).localRotation = Quaternion.Euler(0f, 0f, 45f);
        TextMeshProUGUI name = GameUI.Label("Heading", column, heading, 26f, color, TextAlignmentOptions.TopLeft, GameUI.Heading);
        name.characterSpacing = 8f;
        Place(name.rectTransform, 28f, 0f, width - 28f, 32f);
        TextMeshProUGUI under = GameUI.Label("About", column, about, 20f, GameUI.Dim, TextAlignmentOptions.TopLeft);
        Place(under.rectTransform, 28f, 32f, width - 28f, 26f);
        return column;
    }

    void BuildSuit(PlayerController player)
    {
        const float x = 60f, width = 380f;
        RectTransform suitColumn = Column("Suit", x, width, "SUIT", "Where everything goes", SuitColor);

        Image box = Row("Hologram", suitColumn, 0f, 70f, width, 400f);
        var mask = box.gameObject.AddComponent<RectMask2D>();
        mask.padding = new Vector4(4f, 4f, 4f, 4f);
        SpriteRenderer body = player != null ? player.spriteRenderer : null;
        if (body == null && player != null) body = player.GetComponentInChildren<SpriteRenderer>();
        Image figure = GameUI.NewRect("Technician", box.rectTransform).gameObject.AddComponent<Image>();
        figure.sprite = body != null ? body.sprite : null;
        figure.enabled = figure.sprite != null;
        figure.preserveAspect = true;
        figure.raycastTarget = false;
        figure.color = new Color(SuitColor.r, SuitColor.g, SuitColor.b, 0.8f);
        GameUI.Anchor(figure.rectTransform, 0.08f, 0.06f, 0.62f, 0.94f);
        scanlines = GameUI.NewRect("Scanlines", box.rectTransform).gameObject.AddComponent<RawImage>();
        scanlines.texture = GameUI.Scanlines;
        scanlines.raycastTarget = false;
        GameUI.Fill(scanlines.rectTransform);

        packMarker = Mark(box.rectTransform, figure.rectTransform, new Vector2(0.4f, 0.66f), "PACK", PackColor);
        rackMarker = Mark(box.rectTransform, figure.rectTransform, new Vector2(0.6f, 0.46f), "RACK", RackColor);
    }

    Marker Mark(RectTransform box, RectTransform figure, Vector2 onFigure, string name, Color color)
    {
        var marker = new Marker { color = color };
        RectTransform spot = GameUI.NewRect(name, figure);
        spot.anchorMin = spot.anchorMax = onFigure;
        spot.sizeDelta = Vector2.zero;
        Vector3 local = box.InverseTransformPoint(spot.position);
        float y = local.y - box.rect.yMax;
        float startX = local.x - box.rect.xMin;
        const float labelWidth = 86f;
        float endX = box.rect.width - labelWidth - 10f;

        marker.line = GameUI.NewRect("Line", box).gameObject.AddComponent<Image>();
        marker.line.raycastTarget = false;
        RectTransform line = marker.line.rectTransform;
        line.anchorMin = line.anchorMax = line.pivot = new Vector2(0f, 1f);
        line.sizeDelta = new Vector2(Mathf.Max(0f, endX - startX), 2f);
        line.anchoredPosition = new Vector2(startX, y + 1f);
        marker.dot = GameUI.NewRect("Dot", box).gameObject.AddComponent<Image>();
        marker.dot.raycastTarget = false;
        RectTransform dot = marker.dot.rectTransform;
        dot.anchorMin = dot.anchorMax = new Vector2(0f, 1f);
        dot.pivot = new Vector2(0.5f, 0.5f);
        dot.sizeDelta = new Vector2(12f, 12f);
        dot.anchoredPosition = new Vector2(startX, y);
        dot.localRotation = Quaternion.Euler(0f, 0f, 45f);
        marker.label = GameUI.Label("Label", box, name, 20f, color, TextAlignmentOptions.MidlineLeft, GameUI.Heading);
        marker.label.characterSpacing = 6f;
        RectTransform label = marker.label.rectTransform;
        label.anchorMin = label.anchorMax = new Vector2(0f, 1f);
        label.pivot = new Vector2(0f, 0.5f);
        label.sizeDelta = new Vector2(labelWidth, 28f);
        label.anchoredPosition = new Vector2(endX + 8f, y);
        return marker;
    }

    void BuildRack()
    {
        const float x = 480f, width = 520f, rowHeight = 86f, gap = 10f;
        rackColumn = Column("Tool Rack", x, width, "TOOL RACK", "Number keys hold what's here", RackColor);
        for (int i = 0; i < MaxRack; i++)
        {
            var row = new RackRow();
            Image back = Row("Slot", rackColumn, 0f, 70f + i * (rowHeight + gap), width, rowHeight);
            row.rect = back.rectTransform;
            row.group = back.gameObject.AddComponent<CanvasGroup>();
            row.brackets = Brackets(row.rect);

            Image cap = GameUI.KeyCap(row.rect, "", 30f, out row.key);
            Place(cap.rectTransform, 16f, (rowHeight - 50f) * 0.5f, 46f, 50f);
            row.icon = Picture(row.rect, null, 76f, 12f, 110f, rowHeight - 24f);

            row.name = GameUI.Label("Name", row.rect, "", 28f, GameUI.Text, TextAlignmentOptions.TopLeft, GameUI.Heading);
            row.name.characterSpacing = 5f;
            Place(row.name.rectTransform, 202f, 14f, 210f, 34f);
            row.use = GameUI.Label("Use", row.rect, "", 22f, GameUI.Dim, TextAlignmentOptions.TopLeft);
            row.use.overflowMode = TextOverflowModes.Ellipsis;
            Place(row.use.rectTransform, 202f, 46f, width - 220f, 26f);
            row.inHand = GameUI.Label("In Hand", row.rect, "IN HAND", 18f, RackColor, TextAlignmentOptions.TopRight, GameUI.Heading);
            row.inHand.characterSpacing = 4f;
            Place(row.inHand.rectTransform, width - 136f, 18f, 120f, 24f);

            Image track = GameUI.NewRect("Charge", row.rect).gameObject.AddComponent<Image>();
            track.color = new Color(0f, 0f, 0f, 0.5f);
            track.raycastTarget = false;
            Place(track.rectTransform, 202f, rowHeight - 10f, width - 220f, 5f);
            row.charge = GameUI.NewRect("Fill", track.rectTransform).gameObject.AddComponent<Image>();
            row.charge.color = PackColor;
            row.charge.raycastTarget = false;
            row.charge.rectTransform.anchorMin = Vector2.zero;
            row.charge.rectTransform.anchorMax = Vector2.one;
            row.charge.rectTransform.offsetMin = row.charge.rectTransform.offsetMax = Vector2.zero;
            rack.Add(row);
        }

        TextMeshProUGUI empty = GameUI.Label("Empty", rackColumn, "Nothing on the rack yet", 24f, GameUI.Dim, TextAlignmentOptions.TopLeft);
        Place(empty.rectTransform, 28f, 80f, width - 28f, 30f);
        empty.enabled = hotbar == null || hotbar.slots.Count == 0;
    }

    void BuildPack()
    {
        const float x = 1040f, width = 580f, rowHeight = 96f, gap = 12f;
        packColumn = Column("Pack", x, width, "PACK", "Scrap you've picked up", PackColor);
        for (int i = 0; i < Inventory.Materials.Length; i++)
        {
            Inventory.Material material = Inventory.Materials[i];
            var row = new PartRow { material = material };
            Image back = Row("Part", packColumn, 0f, 70f + i * (rowHeight + gap), width, rowHeight);
            row.rect = back.rectTransform;
            row.brackets = Brackets(row.rect);
            Picture(row.rect, ScrapPickup.Make(material), 16f, 14f, 68f, 68f);
            TextMeshProUGUI name = GameUI.Label("Name", row.rect, Inventory.Name(material).ToUpperInvariant(), 26f, GameUI.Text, TextAlignmentOptions.MidlineLeft, GameUI.Heading);
            name.characterSpacing = 4f;
            Place(name.rectTransform, 104f, 0f, 300f, rowHeight);

            row.count = GameUI.Label("Count", row.rect, "", 44f, GameUI.Text, TextAlignmentOptions.MidlineRight, GameUI.Readout);
            Place(row.count.rectTransform, width - 156f, 0f, 136f, rowHeight);
            parts.Add(row);
        }
    }

    static RectTransform Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    static Image Row(string rowName, RectTransform parent, float x, float y, float width, float height)
    {
        Image back = GameUI.Sliced(rowName, parent, GameUI.SoftPanelSprite, RowColor);
        back.pixelsPerUnitMultiplier = 1f / GameUI.SoftPixelSize;
        Place(back.rectTransform, x, y, width, height);
        return back;
    }

    static Image Brackets(RectTransform parent)
    {
        Image brackets = GameUI.Sliced("Brackets", parent, InventoryHud.BracketSprite, Edge);
        GameUI.Fill(brackets.rectTransform);
        return brackets;
    }

    static Image Picture(RectTransform parent, Sprite sprite, float x, float y, float width, float height)
    {
        Image picture = GameUI.NewRect("Picture", parent).gameObject.AddComponent<Image>();
        picture.sprite = sprite;
        picture.enabled = sprite != null;
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        Place(picture.rectTransform, x, y, width, height);
        return picture;
    }
}
