using TMPro;
using UnityEngine;

// What the player picked from the title menu.
public enum TitleChoice { None, Start, Quit, Back }

// The row of choices that replaces "press any button": START, SETTINGS, and QUIT, with brackets that slide over to the
// one selected. SETTINGS opens the settings panel over the whole screen. TitleScreen calls Tick every frame.
public class TitleMenu
{
    static readonly string[] Items = { "START", "SETTINGS", "QUIT" };
    const float Spacing = 300f;
    static readonly Color Bright = new Color(0.82f, 1f, 0.98f);
    static readonly Color Dim = new Color(0.42f, 0.66f, 0.66f);
    static readonly Color Cyan = new Color(0.25f, 0.95f, 0.95f);

    readonly CanvasGroup group;
    readonly TextMeshProUGUI[] labels = new TextMeshProUGUI[Items.Length];
    readonly float[] scales = new float[Items.Length];
    readonly HudShape leftBracket, rightBracket, underline;
    readonly TitleSettings settings;
    readonly MenuInput input = new MenuInput();
    readonly AudioSource sfx;
    readonly AudioClip moveClip, selectClip, backClip;

    int selected;
    float bracketX, bracketWidth = 200f;
    float openedAt = -10f, closedAt = -10f, chosenAt = -10f;

    public bool IsOpen { get; private set; }

    // parent holds the row, at position; screen is the whole canvas, which the settings panel covers.
    public TitleMenu(RectTransform parent, Vector2 position, RectTransform screen, TMP_FontAsset font, AudioSource sfx)
    {
        this.sfx = sfx;
        moveClip = TitleUI.Tone("Move", 990f, 0.06f, 0.35f);
        selectClip = TitleUI.Tone("Select", 1320f, 0.09f, 0.45f);
        backClip = TitleUI.Tone("Back", 620f, 0.08f, 0.4f);

        RectTransform row = TitleUI.Place(TitleUI.NewRect("Menu", parent), position, new Vector2(1000f, 90f));
        group = row.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        Material glow = TitleUI.GlowMaterial(font, TitleUI.Fade(Cyan, 0.4f), 0.8f, 0.3f);
        for (int i = 0; i < Items.Length; i++)
        {
            labels[i] = TitleUI.Label(Items[i], row, font, 46f, Dim, new Vector2((i - 1) * Spacing, 0f), new Vector2(280f, 70f));
            labels[i].characterSpacing = 10f;
            labels[i].fontSharedMaterial = glow;
            scales[i] = 1f;
        }

        leftBracket = TitleUI.Shape("Left Bracket", row, Cyan, new[] { new Vector2(12f, -26f), new Vector2(0f, -26f), new Vector2(0f, 26f), new Vector2(12f, 26f) },
            thickness: 3f, feather: 6f, closed: false);
        rightBracket = TitleUI.Shape("Right Bracket", row, Cyan, new[] { new Vector2(-12f, -26f), new Vector2(0f, -26f), new Vector2(0f, 26f), new Vector2(-12f, 26f) },
            thickness: 3f, feather: 6f, closed: false);
        underline = TitleUI.Shape("Underline", row, TitleUI.Fade(Cyan, 0.6f), new[] { Vector2.zero, Vector2.right }, thickness: 2f, feather: 4f, closed: false);

        settings = new TitleSettings(screen, font, sfx, moveClip, selectClip, backClip);
    }

    public void Open(float now)
    {
        IsOpen = true;
        openedAt = now;
        selected = 0;
        bracketX = labels[0].rectTransform.anchoredPosition.x;
        input.Read(now);    // so a direction already held to open the menu doesn't also move it
    }

    public void Close(float now)
    {
        IsOpen = false;
        closedAt = now;
    }

    // Runs the menu for a frame and returns what was chosen, if anything. Without acceptInput it only animates.
    public TitleChoice Tick(float now, float dt, bool acceptInput = true)
    {
        input.Read(now);
        TitleChoice choice = TitleChoice.None;

        if (settings.IsOpen)
        {
            if (acceptInput) settings.Tick(now, input);
        }
        else if (acceptInput && IsOpen && now - openedAt > 0.15f)
        {
            int step = input.StepX != 0 ? input.StepX : input.StepY;
            if (step != 0) Select((selected + step + Items.Length) % Items.Length);
            if (input.MouseMoved)
            {
                for (int i = 0; i < Items.Length; i++)
                    if (i != selected && MenuInput.Over(labels[i].rectTransform)) Select(i);
            }

            if (input.Submit || (input.Click && MenuInput.Over(labels[selected].rectTransform)))
            {
                choice = Choose(now);
            }
            else if (input.Cancel)
            {
                sfx.PlayOneShot(backClip, 0.4f);
                choice = TitleChoice.Back;
            }
        }

        Animate(now, dt);
        settings.Animate(now);
        return choice;
    }

    void Select(int index)
    {
        selected = index;
        sfx.PlayOneShot(moveClip, 0.3f);
    }

    TitleChoice Choose(float now)
    {
        chosenAt = now;
        switch (selected)
        {
            case 0:
                return TitleChoice.Start;    // TitleScreen plays the start sound
            case 1:
                sfx.PlayOneShot(selectClip, 0.45f);
                settings.Open(now);
                return TitleChoice.None;
            default:
                sfx.PlayOneShot(selectClip, 0.45f);
                return TitleChoice.Quit;
        }
    }

    void Animate(float now, float dt)
    {
        float shown = IsOpen ? TitleUI.EaseOut(TitleUI.Phase(now, openedAt, openedAt + 0.3f)) : 1f - TitleUI.Phase(now, closedAt, closedAt + 0.2f);
        group.alpha = shown * (settings.IsOpen ? 0.3f : 1f);
        if (shown <= 0f) return;

        float follow = 1f - Mathf.Exp(-dt * 18f);
        for (int i = 0; i < Items.Length; i++)
        {
            float start = openedAt + i * 0.08f;
            labels[i].text = IsOpen ? TitleUI.Decode(Items[i], TitleUI.Phase(now, start, start + 0.35f), now) : Items[i];

            bool isSelected = i == selected;
            float alpha = 1f;
            if (isSelected && now - chosenAt < 0.6f) alpha = Mathf.Repeat((now - chosenAt) * 14f, 1f) < 0.5f ? 1f : 0.3f;
            labels[i].color = TitleUI.Fade(isSelected ? Bright : Dim, alpha);
            scales[i] = Mathf.Lerp(scales[i], isSelected ? 1.12f : 1f, follow);
            labels[i].rectTransform.localScale = Vector3.one * scales[i];
        }

        TextMeshProUGUI current = labels[selected];
        bracketX = Mathf.Lerp(bracketX, current.rectTransform.anchoredPosition.x, follow);
        bracketWidth = Mathf.Lerp(bracketWidth, current.preferredWidth * scales[selected] + 40f, follow);
        float breathe = 4f * (0.5f + 0.5f * Mathf.Sin(now * 5f));
        leftBracket.rectTransform.anchoredPosition = new Vector2(bracketX - bracketWidth * 0.5f - breathe, 0f);
        rightBracket.rectTransform.anchoredPosition = new Vector2(bracketX + bracketWidth * 0.5f + breathe, 0f);
        underline.SetPoints(new[] { new Vector2(bracketX - bracketWidth * 0.35f, -34f), new Vector2(bracketX + bracketWidth * 0.35f, -34f) });
    }
}

// Menu input read the same way from the keyboard, a controller, and the mouse: steps that repeat while a direction is
// held, confirm and cancel, and what the mouse did this frame.
public class MenuInput
{
    const float Threshold = 0.5f;
    const float RepeatDelay = 0.4f;
    const float RepeatRate = 0.12f;

    int heldX, heldY;
    float nextX, nextY;
    Vector3 lastMouse;

    public int StepX { get; private set; }      // -1 left, 1 right
    public int StepY { get; private set; }      // -1 up, 1 down the list
    public bool Submit { get; private set; }
    public bool Cancel { get; private set; }
    public bool MouseMoved { get; private set; }
    public bool Click { get; private set; }
    public bool MouseHeld { get; private set; }

    public void Read(float now)
    {
        StepX = Step(Input.GetAxisRaw("Horizontal"), ref heldX, ref nextX, now);
        StepY = -Step(Input.GetAxisRaw("Vertical"), ref heldY, ref nextY, now);
        Submit = Input.GetButtonDown("Submit");
        Cancel = Input.GetButtonDown("Cancel") || Input.GetMouseButtonDown(1);

        Vector3 mouse = Input.mousePosition;
        MouseMoved = Input.mousePresent && (mouse - lastMouse).sqrMagnitude > 1f;
        lastMouse = mouse;
        Click = Input.GetMouseButtonDown(0);
        MouseHeld = Input.GetMouseButton(0);
    }

    static int Step(float axis, ref int held, ref float next, float now)
    {
        int direction = axis > Threshold ? 1 : axis < -Threshold ? -1 : 0;
        if (direction == 0)
        {
            held = 0;
            return 0;
        }
        if (direction != held)
        {
            held = direction;
            next = now + RepeatDelay;
            return direction;
        }
        if (now < next) return 0;
        next = now + RepeatRate;
        return direction;
    }

    public static bool Over(RectTransform rect)
    {
        return Input.mousePresent && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
    }

    // Where the mouse is across a rect: 0 at its left edge, 1 at its right.
    public static float Across(RectTransform rect)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, null, out Vector2 local);
        return Mathf.Clamp01((local.x - rect.rect.xMin) / rect.rect.width);
    }
}
