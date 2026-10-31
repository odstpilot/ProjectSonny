using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// the suit map
public class MapScreen : MonoBehaviour
{
    const int SortingOrder = 108;
    const float CellSize = 24f;
    const int PixelsPerCell = 4;
    const float DefaultZoom = 1.6f;
    const float MaxZoom = 3.5f;
    const float PanSpeed = 1100f;
    const float OpenTime = 0.38f;
    const float CloseTime = 0.2f;
    const float ScreenOnFrom = 0.55f;
    static readonly Vector2 Lowered = new Vector2(-260f, -760f);
    const float LoweredTilt = -14f;
    const float NoPowerLineGap = 4f;
    const float GridMargin = 60f;
    const KeyCode MapKey = KeyCode.M;

    static readonly Color Backdrop = new Color(0.01f, 0.02f, 0.05f, 0.82f);
    static readonly Color Casing = new Color(0.14f, 0.16f, 0.19f);
    static readonly Color SuitWhite = new Color(0.84f, 0.86f, 0.88f);
    static readonly Color SuitShade = new Color(0.62f, 0.65f, 0.7f);
    static readonly Color SuitRed = new Color(0.78f, 0.2f, 0.18f);
    static readonly Color ScreenColor = new Color(0.02f, 0.05f, 0.11f, 1f);
    static readonly Color GridColor = new Color(0.25f, 0.5f, 0.9f, 0.09f);
    static readonly Color32 FloorColor = new Color32(16, 36, 72, 255);
    static readonly Color32 HereColor = new Color32(26, 62, 112, 255);
    static readonly Color32 EdgeColor = new Color32(90, 180, 255, 255);
    static readonly Color32 DoorwayColor = new Color32(48, 110, 190, 255);
    static readonly Color32 DoorColor = new Color32(120, 205, 255, 255);
    static readonly Color32 LockedColor = new Color32(255, 82, 70, 255);
    static readonly Color32 StairColor = new Color32(210, 235, 255, 255);
    static readonly Color32 StairShade = new Color32(60, 110, 170, 255);
    static readonly Color32 SonnyColor = new Color32(235, 245, 255, 255);
    static readonly Color TargetColor = GameUI.Amber;
    static readonly Color YouColor = new Color(1f, 0.42f, 0.34f);
    static readonly Color LabelColor = new Color(0.55f, 0.78f, 1f, 0.8f);

    static MapScreen instance;
    static char targetMarker;
    static string targetLabel = "";

    [Tooltip("What the technician says trying to open the map with the suit's power gone.")]
    public string noPowerLine = "Map's dead... no power to it";

    public static bool IsOpen => instance != null && instance.open;
    public static event System.Action Opened;
    public static event System.Action Closed;

    private bool built;
    private bool open;
    private int openedFrame;
    private float savedTimeScale = 1f;
    private PlayerController player;
    private bool heldPlayer;

    private CanvasGroup group;
    private RectTransform device;
    private Vector2 devicePlace;
    private Image deviceLight;
    private float noPowerSaidAt = -10f;
    private RectTransform viewport;
    private RectTransform content;
    private RawImage plan;
    private Texture2D planTexture;
    private RectTransform grid;
    private RectTransform labels;
    private readonly List<(TextMeshProUGUI label, StationMap.Room room)> roomLabels = new List<(TextMeshProUGUI, StationMap.Room)>();
    private RectTransform target;
    private Image targetFill;
    private RectTransform targetPin;
    private TextMeshProUGUI targetText;
    private float pinTop;
    private RectTransform you;
    private RawImage youRing;
    private Image youDot;
    private TextMeshProUGUI floorLabel;
    private TextMeshProUGUI hereLabel;
    private StationMap drawnFrom;

    private Vector2 look;
    private Vector2 youAt;
    private float zoom = DefaultZoom;
    private Vector2 lastMouse;
    private bool dragging;
    private Coroutine recenter;

    public static MapScreen Get()
    {
        if (instance == null) instance = FindAnyObjectByType<MapScreen>();
        if (instance == null) instance = new GameObject("MapScreen", typeof(RectTransform)).AddComponent<MapScreen>();
        return instance;
    }

    public static void SetTarget(char marker, string label)
    {
        targetMarker = marker;
        targetLabel = label ?? "";
        ObjectiveHighlight.Follow(marker);
        if (instance != null && instance.open) instance.Redraw();
    }

    public static void ClearTarget() => SetTarget('\0', "");

    void Awake()
    {
        if (instance == null) instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (open) Release();
        if (planTexture != null) Destroy(planTexture);
    }

    bool CanOpen()
    {
        if (!SuitFeatures.Has(SuitFeatures.Feature.Map) || StationMap.Current == null) return false;
        if (Time.timeScale <= 0f || DialogueBox.Busy || TutorialHud.ScreenCovered || Locker.IsPlayerHidden) return false;
        GameObject found = GameObject.FindWithTag("Player");
        if (found == null) return false;
        player = found.GetComponent<PlayerController>();
        if (player != null && (player.IsScripted || !player.enabled)) return false;
        Health health = found.GetComponent<Health>();
        return health == null || !health.IsDead;
    }

    public void Open()
    {
        if (open) return;
        if (!SuitDisplay.Powered && SuitFeatures.Has(SuitFeatures.Feature.Map))
        {
            if (Time.unscaledTime - noPowerSaidAt > NoPowerLineGap)
            {
                noPowerSaidAt = Time.unscaledTime;
                TechnicianVoice.Say(noPowerLine, 1.8f);
            }
            return;
        }
        if (!CanOpen()) return;
        Build();
        Canvas.ForceUpdateCanvases();
        open = true;
        openedFrame = Time.frameCount;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        heldPlayer = player != null;
        if (heldPlayer) player.SetScriptedInput(Vector2.zero, false);

        Redraw();
        zoom = Mathf.Clamp(DefaultZoom, MinZoom(), MaxZoom);
        look = youAt;
        ClampLook();
        Place();

        StopAllCoroutines();
        StartCoroutine(Fade(true));
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!open) return;
        open = false;
        dragging = false;
        Release();
        StopAllCoroutines();
        recenter = null;
        StartCoroutine(Fade(false));
        Closed?.Invoke();
    }

    void Release()
    {
        if (Time.timeScale == 0f) Time.timeScale = savedTimeScale;
        if (heldPlayer && player != null) player.ClearScriptedInput();
        heldPlayer = false;
    }

    IEnumerator Fade(bool opening)
    {
        float seconds = opening ? OpenTime : CloseTime;
        float from = group.alpha, to = opening ? 1f : 0f;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / seconds);
            group.alpha = opening ? Mathf.Lerp(from, to, Mathf.Clamp01(p * 3f)) : Mathf.Lerp(from, to, Mathf.Clamp01((p - 0.4f) / 0.6f));
            Raise(opening ? TitleUI.EaseOut(Mathf.Clamp01(t / (seconds * ScreenOnFrom))) : 1f - p);
            float screen = opening ? Mathf.InverseLerp(ScreenOnFrom, 1f, p) : 1f - Mathf.Clamp01(p * 2.5f);
            viewport.localScale = new Vector3(1f, Mathf.Lerp(0.02f, 1f, screen), 1f);
            yield return null;
        }
        group.alpha = to;
        Raise(opening ? 1f : 0f);
        viewport.localScale = Vector3.one;
    }

    void Raise(float up)
    {
        device.anchoredPosition = devicePlace + Vector2.Lerp(Lowered, Vector2.zero, up);
        device.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(LoweredTilt, 0f, up));
    }

    void Update()
    {
        if (!open)
        {
            if (Input.GetKeyDown(MapKey)) Open();
            return;
        }
        if (Time.frameCount != openedFrame &&
            (Input.GetKeyDown(MapKey) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape)))
        {
            Close();
            return;
        }
        if (TutorialHud.ScreenCovered)
        {
            Close();
            return;
        }

        float dt = Time.unscaledDeltaTime;
        float perCell = zoom * CellSize;
        deviceLight.color = TitleUI.Fade(GameUI.Green, Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.8f ? 1f : 0.25f);

        var keys = new Vector2(
            (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
            (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f));
        if (keys != Vector2.zero)
        {
            StopRecenter();
            float speed = PanSpeed * (Input.GetKey(KeyCode.LeftShift) ? 2f : 1f);
            look += keys.normalized * speed * dt / perCell;
        }

        Vector2 mouse = Input.mousePosition;
        if (Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(viewport, mouse, null))
        {
            dragging = true;
            StopRecenter();
        }
        if (!Input.GetMouseButton(0)) dragging = false;
        if (dragging)
        {
            Vector2 moved = (mouse - lastMouse) / CanvasScale();
            look += new Vector2(-moved.x, moved.y) / perCell;
        }
        lastMouse = mouse;

        float zoomBy = Input.mouseScrollDelta.y * 0.15f;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus)) zoomBy += dt * 1.5f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus)) zoomBy -= dt * 1.5f;
        if (zoomBy != 0f) ZoomAt(zoom * Mathf.Exp(zoomBy), Input.mouseScrollDelta.y != 0f ? mouse : (Vector2?)null);

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.C))
        {
            StopRecenter();
            recenter = StartCoroutine(Recenter());
        }

        ClampLook();
        Place();
        Animate();
    }

    void ZoomAt(float wanted, Vector2? screenPoint)
    {
        float next = Mathf.Clamp(wanted, MinZoom(), MaxZoom);
        Vector2 offset = Vector2.zero;
        if (screenPoint.HasValue && RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPoint.Value, null, out Vector2 local))
            offset = local - viewport.rect.center;
        Vector2 under = look + new Vector2(offset.x, -offset.y) / (zoom * CellSize);
        zoom = next;
        look = under - new Vector2(offset.x, -offset.y) / (zoom * CellSize);
    }

    IEnumerator Recenter()
    {
        Vector2 from = look;
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            look = Vector2.Lerp(from, youAt, Mathf.SmoothStep(0f, 1f, t / 0.3f));
            yield return null;
        }
        look = youAt;
        recenter = null;
    }

    void StopRecenter()
    {
        if (recenter != null) StopCoroutine(recenter);
        recenter = null;
    }

    float MinZoom()
    {
        StationMap map = StationMap.Current;
        if (map == null || viewport == null) return 0.5f;
        Vector2 size = viewport.rect.size;
        return Mathf.Min(size.x / (map.width * CellSize), size.y / (map.height * CellSize)) * 0.92f;
    }

    void ClampLook()
    {
        StationMap map = StationMap.Current;
        if (map == null) return;
        look = new Vector2(Mathf.Clamp(look.x, 0f, map.width), Mathf.Clamp(look.y, 0f, map.height));
    }

    float CanvasScale()
    {
        Canvas canvas = GetComponent<Canvas>();
        return canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
    }

    void Place()
    {
        content.localScale = new Vector3(zoom, zoom, 1f);
        content.anchoredPosition = new Vector2(-look.x * CellSize * zoom, look.y * CellSize * zoom);
        float counter = 1f / zoom;
        foreach ((TextMeshProUGUI label, StationMap.Room _) in roomLabels)
            label.rectTransform.localScale = new Vector3(counter, counter, 1f);
        you.localScale = new Vector3(counter, counter, 1f);
        targetPin.localScale = new Vector3(counter, counter, 1f);
    }

    void Animate()
    {
        float time = Time.unscaledTime;
        float ring = Mathf.Repeat(time, 1.2f) / 1.2f;
        youRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 2.4f, ring);
        youRing.color = TitleUI.Fade(YouColor, 0.7f * (1f - ring));
        youDot.enabled = Mathf.Repeat(time, 0.8f) < 0.6f;

        if (target.gameObject.activeSelf)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 4f);
            targetFill.color = TitleUI.Fade(TargetColor, Mathf.Lerp(0.06f, 0.2f, pulse));
            targetPin.anchoredPosition = new Vector2(targetPin.anchoredPosition.x, pinTop + Mathf.Round(pulse * 2f) * 3f * (1f / zoom));
        }
    }

    void Redraw()
    {
        StationMap map = StationMap.Current;
        if (map == null) return;

        StationMap.Room here = null;
        GameObject found = GameObject.FindWithTag("Player");
        if (found != null && map.Locate(found.transform.position, out Vector2 onPlan, out StationMap.Room inRoom))
        {
            youAt = onPlan;
            here = inRoom;
        }
        StationMap.Room goal = targetMarker != '\0' ? map.RoomByMarker(targetMarker) : null;
        RectInt? stairs = null;
        bool up = false;
        if (goal == null && targetMarker != '\0')
        {
            Stairway stair = ObjectiveHighlight.StairsOnTheWay(map, out up);
            if (stair != null && map.Locate(stair.transform.position, out Vector2 stairAt, out _)) stairs = StairsAround(map, stairAt);
        }

        if (drawnFrom != map) BuildRooms(map);
        PaintPlan(map, here, goal);

        you.anchoredPosition = new Vector2(youAt.x * CellSize, -youAt.y * CellSize);
        you.gameObject.SetActive(here != null);
        foreach ((TextMeshProUGUI label, StationMap.Room room) in roomLabels)
            label.color = room == here ? GameUI.Text : room == goal ? TargetColor : LabelColor;

        RectInt? picked = goal != null ? goal.drawn : stairs;
        target.gameObject.SetActive(picked.HasValue);
        targetPin.gameObject.SetActive(picked.HasValue);
        if (picked.HasValue)
        {
            RectInt area = picked.Value;
            target.anchoredPosition = new Vector2(area.x * CellSize, -area.y * CellSize);
            target.sizeDelta = new Vector2(area.width * CellSize, area.height * CellSize);
            pinTop = -area.y * CellSize + 6f;
            targetPin.anchoredPosition = new Vector2((area.x + area.width * 0.5f) * CellSize, pinTop);
            targetText.text = (goal != null ? targetLabel : $"STAIRS {(up ? "UP" : "DOWN")} TO {targetLabel}").ToUpperInvariant();
        }

        floorLabel.text = "//  " + map.floorName.ToUpperInvariant();
        hereLabel.text = here != null ? "YOU ARE IN  <color=#ffffff>" + here.name.ToUpperInvariant() + "</color>" : "";
    }

    static RectInt? StairsAround(StationMap map, Vector2 onPlan)
    {
        Vector2Int nearest = default;
        float best = float.MaxValue;
        for (int row = 0; row < map.height; row++)
            for (int x = 0; x < map.width; x++)
            {
                if (map.MarkAt(x, row) != StationMap.Mark.Stairs) continue;
                float distance = (new Vector2(x + 0.5f, row + 0.5f) - onPlan).sqrMagnitude;
                if (distance < best) { best = distance; nearest = new Vector2Int(x, row); }
            }
        if (best == float.MaxValue) return null;

        var seen = new HashSet<Vector2Int> { nearest };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(nearest);
        int xMin = nearest.x, xMax = nearest.x, yMin = nearest.y, yMax = nearest.y;
        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            xMin = Mathf.Min(xMin, cell.x); xMax = Mathf.Max(xMax, cell.x);
            yMin = Mathf.Min(yMin, cell.y); yMax = Mathf.Max(yMax, cell.y);
            foreach (Vector2Int step in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
            {
                Vector2Int next = cell + step;
                if (map.MarkAt(next.x, next.y) == StationMap.Mark.Stairs && seen.Add(next)) queue.Enqueue(next);
            }
        }
        return new RectInt(xMin - 1, yMin - 1, xMax - xMin + 3, yMax - yMin + 3);
    }

    void BuildRooms(StationMap map)
    {
        drawnFrom = map;
        content.sizeDelta = new Vector2(map.width * CellSize, map.height * CellSize);
        grid.anchoredPosition = new Vector2(-GridMargin * CellSize, GridMargin * CellSize);
        grid.sizeDelta = new Vector2((map.width + GridMargin * 2f) * CellSize, (map.height + GridMargin * 2f) * CellSize);
        grid.GetComponent<RawImage>().uvRect = new Rect(0f, 0f, (map.width + GridMargin * 2f) / 2f, (map.height + GridMargin * 2f) / 2f);

        foreach ((TextMeshProUGUI label, StationMap.Room _) in roomLabels) Destroy(label.gameObject);
        roomLabels.Clear();
        foreach (StationMap.Room room in map.rooms)
        {
            TextMeshProUGUI label = GameUI.Label(room.name, labels, ShortName(room.name), 20f, LabelColor, TextAlignmentOptions.Center, GameUI.Heading);
            label.characterSpacing = 6f;
            label.fontSharedMaterial = GameUI.Shadow(label.font);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 30f);
            rect.anchoredPosition = new Vector2(room.DrawnCenter.x * CellSize, -room.DrawnCenter.y * CellSize);
            roomLabels.Add((label, room));
        }
    }

    static string ShortName(string name)
    {
        int bracket = name.IndexOf('(');
        string shortName = bracket > 0 ? name.Substring(bracket + 1).TrimEnd(')') : name;
        return shortName.Replace(", ", "\n").ToUpperInvariant();
    }

    void PaintPlan(StationMap map, StationMap.Room here, StationMap.Room goal)
    {
        int w = map.width * PixelsPerCell, h = map.height * PixelsPerCell;
        if (planTexture == null || planTexture.width != w || planTexture.height != h)
        {
            if (planTexture != null) Destroy(planTexture);
            planTexture = PixelArt.MakeTexture(w, h, (x, y) => default);
        }
        int hereValue = here != null ? map.rooms.IndexOf(here) + 1 : -1;
        int goalValue = goal != null ? map.rooms.IndexOf(goal) + 1 : -1;
        Color32 goalEdge = TargetColor;

        bool Floor(int x, int row) => map.CellAt(x, row) != StationMap.Empty;

        var pixels = new Color32[w * h];
        for (int row = 0; row < map.height; row++)
        {
            for (int x = 0; x < map.width; x++)
            {
                byte cell = map.CellAt(x, row);
                if (cell == StationMap.Empty) continue;
                bool doorway = cell == StationMap.DoorGap;
                Color32 fill = doorway ? DoorwayColor : cell == hereValue ? HereColor : FloorColor;
                Color32 edge = cell == goalValue ? goalEdge : EdgeColor;
                StationMap.Mark mark = map.MarkAt(x, row);

                for (int py = 0; py < PixelsPerCell; py++)
                {
                    for (int px = 0; px < PixelsPerCell; px++)
                    {
                        Color32 color = fill;
                        switch (mark)
                        {
                            case StationMap.Mark.Door: color = DoorColor; break;
                            case StationMap.Mark.Locked: color = LockedColor; break;
                            case StationMap.Mark.Stairs: color = py % 2 == 0 ? StairColor : StairShade; break;
                            case StationMap.Mark.Sonny:
                                if (px >= 1 && px <= 2 && py >= 1 && py <= 2) color = SonnyColor;
                                break;
                        }
                        bool edgeHere = (px == 0 && !Floor(x - 1, row)) || (px == PixelsPerCell - 1 && !Floor(x + 1, row)) ||
                                        (py == 0 && !Floor(x, row - 1)) || (py == PixelsPerCell - 1 && !Floor(x, row + 1));
                        if (edgeHere && mark != StationMap.Mark.Locked) color = edge;
                        int tx = x * PixelsPerCell + px;
                        int ty = h - 1 - (row * PixelsPerCell + py);
                        pixels[ty * w + tx] = color;
                    }
                }
            }
        }
        planTexture.SetPixels32(pixels);
        planTexture.Apply(false);
        plan.texture = planTexture;
    }

    void Build()
    {
        if (built) return;
        built = true;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = GameUI.ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = group.blocksRaycasts = false;

        Image back = GameUI.Fill(GameUI.NewRect("Backdrop", transform)).gameObject.AddComponent<Image>();
        back.color = Backdrop;
        back.raycastTarget = false;

        BuildDevice();
        BuildHeader();
        BuildViewport();
        BuildFooter();
        devicePlace = device.anchoredPosition;
        Raise(0f);
    }

    void BuildDevice()
    {
        device = GameUI.Anchor(GameUI.NewRect("Wrist Unit", transform), 0f, 0f, 1f, 1f, 110f, 120f, 90f, 40f);
        device.pivot = Vector2.zero;
        RectTransform sleeve = GameUI.Anchor(GameUI.NewRect("Sleeve", device), 0.2f, 0f, 0.8f, 0f, 0f, -520f, 0f, -30f);
        Image cloth = sleeve.gameObject.AddComponent<Image>();
        cloth.color = SuitWhite;
        cloth.raycastTarget = false;
        Image shade = GameUI.Anchor(GameUI.NewRect("Shade", sleeve), 0.8f, 0f, 1f, 1f).gameObject.AddComponent<Image>();
        shade.color = SuitShade;
        shade.raycastTarget = false;
        Image stripe = GameUI.Anchor(GameUI.NewRect("Stripe", sleeve), 0.12f, 0f, 0.17f, 1f).gameObject.AddComponent<Image>();
        stripe.color = SuitRed;
        stripe.raycastTarget = false;
        foreach (float x in new[] { 0.28f, 0.66f })
        {
            Image strap = GameUI.Anchor(GameUI.NewRect("Strap", sleeve), x, 0f, x + 0.07f, 1f).gameObject.AddComponent<Image>();
            strap.color = Casing;
            strap.raycastTarget = false;
        }

        Image casing = GameUI.Sliced("Casing", device, GameUI.PanelSprite, Casing);
        GameUI.Fill(casing.rectTransform, -24f);
        casing.raycastTarget = false;
        foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
        {
            RectTransform screw = GameUI.NewRect("Screw", device);
            screw.anchorMin = screw.anchorMax = corner;
            screw.pivot = new Vector2(0.5f, 0.5f);
            screw.anchoredPosition = new Vector2(corner.x > 0f ? 12f : -12f, corner.y > 0f ? 12f : -12f);
            screw.sizeDelta = Vector2.one * GameUI.PixelSize * 3f;
            Image head = screw.gameObject.AddComponent<Image>();
            head.color = new Color(0.32f, 0.35f, 0.4f);
            head.raycastTarget = false;
        }
        RectTransform light = GameUI.NewRect("Status", device);
        light.anchorMin = light.anchorMax = light.pivot = Vector2.one;
        light.anchoredPosition = new Vector2(-34f, -14f);
        light.sizeDelta = Vector2.one * GameUI.PixelSize * 3f;
        deviceLight = light.gameObject.AddComponent<Image>();
        deviceLight.color = GameUI.Green;
        deviceLight.raycastTarget = false;
        TextMeshProUGUI badge = GameUI.Label("Badge", device, "SUIT NAV  //  PIP", 16f, GameUI.Dim, TextAlignmentOptions.Right, GameUI.Heading);
        badge.characterSpacing = 10f;
        GameUI.Anchor(badge.rectTransform, 1f, 1f, 1f, 1f).pivot = new Vector2(1f, 1f);
        badge.rectTransform.anchoredPosition = new Vector2(-52f, -4f);
        badge.rectTransform.sizeDelta = new Vector2(400f, 28f);
    }

    void BuildHeader()
    {
        TextMeshProUGUI title = GameUI.Label("Title", device, "STATION MAP", 40f, GameUI.Cyan, TextAlignmentOptions.BottomLeft, GameUI.Heading);
        title.characterSpacing = 14f;
        title.fontSharedMaterial = TitleUI.GlowMaterial(title.font, TitleUI.Fade(GameUI.Cyan, 0.35f), 0.6f, 0.25f);
        GameUI.Anchor(title.rectTransform, 0f, 1f, 0f, 1f).pivot = new Vector2(0f, 0f);
        title.rectTransform.anchoredPosition = new Vector2(70f, -96f);
        title.rectTransform.sizeDelta = new Vector2(520f, 60f);

        floorLabel = GameUI.Label("Floor", device, "", 24f, GameUI.Dim, TextAlignmentOptions.BottomLeft, GameUI.Heading);
        floorLabel.characterSpacing = 10f;
        GameUI.Anchor(floorLabel.rectTransform, 0f, 1f, 0f, 1f).pivot = new Vector2(0f, 0f);
        floorLabel.rectTransform.anchoredPosition = new Vector2(510f, -93f);
        floorLabel.rectTransform.sizeDelta = new Vector2(400f, 40f);
    }

    void BuildViewport()
    {
        viewport = GameUI.Anchor(GameUI.NewRect("Screen", device), 0f, 0f, 1f, 1f, 60f, 96f, 60f, 110f);
        Image screen = viewport.gameObject.AddComponent<Image>();
        screen.color = ScreenColor;
        screen.raycastTarget = false;

        RectTransform window = GameUI.Fill(GameUI.NewRect("Window", viewport));
        window.gameObject.AddComponent<RectMask2D>();

        content = GameUI.NewRect("Plan", window);
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0f, 1f);

        grid = GameUI.NewRect("Grid", content);
        grid.anchorMin = grid.anchorMax = grid.pivot = new Vector2(0f, 1f);
        RawImage gridImage = grid.gameObject.AddComponent<RawImage>();
        Texture2D gridTexture = PixelArt.MakeTexture(16, 16, (x, y) => x == 0 || y == 15 ? (Color32)Color.white : default);
        gridTexture.wrapMode = TextureWrapMode.Repeat;
        gridTexture.filterMode = FilterMode.Bilinear;
        gridImage.texture = gridTexture;
        gridImage.color = GridColor;
        gridImage.raycastTarget = false;

        plan = GameUI.Fill(GameUI.NewRect("Rooms", content)).gameObject.AddComponent<RawImage>();
        plan.raycastTarget = false;

        target = GameUI.NewRect("Target", content);
        target.anchorMin = target.anchorMax = target.pivot = new Vector2(0f, 1f);
        targetFill = target.gameObject.AddComponent<Image>();
        targetFill.raycastTarget = false;

        labels = GameUI.Fill(GameUI.NewRect("Labels", content));

        targetPin = GameUI.NewRect("Pin", content);
        targetPin.anchorMin = targetPin.anchorMax = new Vector2(0f, 1f);
        targetPin.pivot = new Vector2(0.5f, 0f);
        targetPin.sizeDelta = new Vector2(10f, 10f);
        Image arrow = GameUI.Sliced("Arrow", targetPin, GameUI.ArrowSprite, TargetColor);
        arrow.type = Image.Type.Simple;
        RectTransform arrowRect = arrow.rectTransform;
        arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(0.5f, 0f);
        arrowRect.pivot = new Vector2(0.5f, 0f);
        arrowRect.sizeDelta = new Vector2(7f, 5f) * GameUI.PixelSize;
        arrowRect.anchoredPosition = Vector2.zero;
        Image tab = GameUI.Sliced("Tab", targetPin, GameUI.TabSprite, TargetColor);
        RectTransform tabRect = tab.rectTransform;
        tabRect.anchorMin = tabRect.anchorMax = new Vector2(0.5f, 0f);
        tabRect.pivot = new Vector2(0.5f, 0f);
        tabRect.anchoredPosition = new Vector2(0f, 5f * GameUI.PixelSize - 3f);
        var tabFit = tab.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabFit.padding = new RectOffset(14, 14, 6, 8);
        tabFit.childControlWidth = tabFit.childControlHeight = true;
        var tabSize = tab.gameObject.AddComponent<ContentSizeFitter>();
        tabSize.horizontalFit = tabSize.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        targetText = GameUI.Label("Label", tab.transform, "", 20f, GameUI.Ink, TextAlignmentOptions.Center, GameUI.Heading);
        targetText.characterSpacing = 8f;

        you = GameUI.NewRect("You", content);
        you.anchorMin = you.anchorMax = new Vector2(0f, 1f);
        you.sizeDelta = new Vector2(20f, 20f);
        youRing = GameUI.Fill(GameUI.NewRect("Ring", you), -6f).gameObject.AddComponent<RawImage>();
        youRing.texture = CombatSprites.SoftCircleTexture;
        youRing.raycastTarget = false;
        youDot = GameUI.Sliced("Dot", you, GameUI.KeyCapSprite, YouColor);
        youDot.type = Image.Type.Simple;
        GameUI.Fill(youDot.rectTransform);
        TextMeshProUGUI youLabel = GameUI.Label("Label", you, "YOU", 20f, YouColor, TextAlignmentOptions.Top, GameUI.Heading);
        youLabel.characterSpacing = 8f;
        youLabel.fontSharedMaterial = GameUI.Shadow(youLabel.font);
        RectTransform youRect = youLabel.rectTransform;
        youRect.anchorMin = youRect.anchorMax = new Vector2(0.5f, 0f);
        youRect.pivot = new Vector2(0.5f, 1f);
        youRect.sizeDelta = new Vector2(120f, 28f);
        youRect.anchoredPosition = new Vector2(0f, -6f);

        RawImage lines = GameUI.Fill(GameUI.NewRect("Scanlines", viewport)).gameObject.AddComponent<RawImage>();
        lines.texture = GameUI.Scanlines;
        lines.uvRect = new Rect(0f, 0f, 1f, 1080f / 3f);
        lines.color = new Color(1f, 1f, 1f, 0.6f);
        lines.raycastTarget = false;

        foreach ((Vector2 min, Vector2 max, Vector2 size) in new[]
        {
            (new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 2f)),
            (new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f)),
            (new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2f, 0f)),
            (new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(2f, 0f)),
        })
        {
            Image line = GameUI.NewRect("Edge", viewport).gameObject.AddComponent<Image>();
            line.rectTransform.anchorMin = min;
            line.rectTransform.anchorMax = max;
            line.rectTransform.sizeDelta = size;
            line.color = TitleUI.Fade(GameUI.Cyan, 0.35f);
            line.raycastTarget = false;
        }
        foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
        {
            for (int i = 0; i < 2; i++)
            {
                Image bracket = GameUI.NewRect("Corner", viewport).gameObject.AddComponent<Image>();
                RectTransform r = bracket.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = corner;
                r.sizeDelta = i == 0 ? new Vector2(36f, 6f) : new Vector2(6f, 36f);
                bracket.color = GameUI.Cyan;
                bracket.raycastTarget = false;
            }
        }
    }

    void BuildFooter()
    {
        hereLabel = GameUI.Label("Here", device, "", 26f, LabelColor, TextAlignmentOptions.Left, GameUI.Heading);
        hereLabel.characterSpacing = 8f;
        GameUI.Anchor(hereLabel.rectTransform, 0f, 0f, 0.5f, 0f).pivot = new Vector2(0f, 0.5f);
        hereLabel.rectTransform.anchoredPosition = new Vector2(64f, 48f);
        hereLabel.rectTransform.sizeDelta = new Vector2(0f, 40f);

        RectTransform keys = GameUI.NewRect("Keys", device);
        keys.anchorMin = keys.anchorMax = keys.pivot = new Vector2(1f, 0f);
        keys.anchoredPosition = new Vector2(-60f, 24f);
        var row = keys.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 8f;
        row.childAlignment = TextAnchor.MiddleRight;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        var fit = keys.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Hint(keys, "LOOK", "W", "A", "S", "D");
        Hint(keys, "ZOOM", "SCROLL");
        Hint(keys, "BACK TO YOU", "SPACE");
        Hint(keys, "CLOSE", "M");
    }

    void Hint(Transform row, string action, params string[] keysFor)
    {
        const float height = 54f;
        foreach (string key in keysFor)
        {
            Image cap = GameUI.KeyCap(row, key, 30f, out TextMeshProUGUI _);
            var size = cap.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;
            size.minWidth = size.preferredWidth = GameUI.KeyCapWidth(key, height);
        }
        TextMeshProUGUI label = GameUI.Label(action, row, action, 32f, GameUI.Text, TextAlignmentOptions.Left);
        label.characterSpacing = 4f;
        var spacing = label.gameObject.AddComponent<LayoutElement>();
        spacing.minWidth = label.preferredWidth + 28f;
    }
}
