using System.Collections;
using UnityEngine;

// One of the stalls in the common grounds restroom, against the wall with a toilet in it. Walk up and press E to use it.
// What happens then is up to whoever's listening (RestroomBreak, in Chapter 1): Used is raised, and the toilet stays in
// use until they call Done. With nobody listening it does nothing.
// A stall can be occupied: its door's shut, with a pair of feet showing under it, and E knocks instead ("Occupied!",
// from inside). Whoever's in there comes out when Vacate is called, and the door swings open.
// Once it's broken (Break), it rattles, gurgles, and leaks a puddle onto the floor in front of it.
// Like the furniture, the object sits at its sorting point, DepthSort.FeetToCenter above the base of the stall, and its
// art is placed down from there. The art is made in code until it has its own. Built by ChapterOneBuilder.
public class Toilet : Terminal
{
    const float PixelsPerUnit = 16f;
    const int Width = 32, Height = 36;

    public event System.Action Used;
    public bool IsBroken { get; private set; }

    [Tooltip("What the prompt says over it.")]
    public string promptText = "USE RESTROOM";
    [Tooltip("The scene's sound for it breaking, and how long it rattles for.")]
    [SoundName] public string gurgleSound = "Toilet Gurgle";
    public float rattleSeconds = 1.6f;
    [Tooltip("How big the puddle in front of it grows, in tiles across.")]
    public float puddleSize = 2.4f;
    [Tooltip("Already broken when the scene starts, with its puddle out: the one that's been out of order all week, or all of them, as Chapter 1 left them.")]
    public bool startBroken;
    [Tooltip("Someone's in it: the door's shut, and E knocks.")]
    public bool occupied;
    public string knockPromptText = "KNOCK";
    [Tooltip("Said from inside when they knock, one after another, the last one again and again.")]
    public string[] occupantReplies = { "Occupied!", "Still occupied!", "Go away!" };
    [SoundName] public string knockSound = "Knock";

    private bool inUse;
    private SpriteRenderer art;
    private SpriteRenderer puddle;
    private CrewSpeech speech;
    private int knocks;

    public override bool InUse => inUse;
    protected override string PromptText => IsBroken ? "OUT OF ORDER" : occupied ? knockPromptText : promptText;
    protected override Vector3 PromptPoint => transform.position + new Vector3(0f, Height / PixelsPerUnit - DepthSort.FeetToCenter + 0.2f, 0f);
    protected override bool Available => !IsBroken || occupied;

    protected override void Awake()
    {
        base.Awake();
        art = new GameObject("Stall").AddComponent<SpriteRenderer>();
        art.transform.SetParent(transform, false);
        art.transform.localPosition = new Vector3(0f, -DepthSort.FeetToCenter, 0f);
        art.sprite = MakeStall(occupied);
        DepthSort.Group(gameObject);
    }

    protected override void Start()
    {
        base.Start();
        if (!startBroken) return;
        IsBroken = true;
        StartCoroutine(Leak(0f));
    }

    protected override void Use(Transform player)
    {
        if (occupied)
        {
            Knock();
            return;
        }
        if (Used == null) return;
        inUse = true;
        Used.Invoke();
    }

    // Lets go of it, for whoever took it on Used.
    public void Done() => inUse = false;

    // Takes it as if it had been used, without raising Used, for a script that's using it (until Done).
    public void Occupy() => inUse = true;

    // A line from whoever's inside, over the stall.
    public void SayFromInside(string line, float seconds = 2f)
    {
        if (speech == null) speech = CrewSpeech.Create(transform);
        speech.Show(line, seconds);
    }

    // Whoever was in there comes out: the door swings open.
    public void Vacate()
    {
        occupied = false;
        art.sprite = MakeStall(false);
    }

    void Knock()
    {
        ScreenFade.Get().PlaySound(knockSound, KnockTone);
        if (occupantReplies.Length == 0) return;
        SayFromInside(occupantReplies[Mathf.Min(knocks++, occupantReplies.Length - 1)]);
    }

    // It gives up: a rattle, a gurgle, and water on the floor. Done when it's finished rattling.
    public IEnumerator Break()
    {
        IsBroken = true;
        ScreenFade.Get().PlaySound(gurgleSound, GurgleTone);
        StartCoroutine(Leak(4f));

        Vector3 home = art.transform.localPosition;
        for (float t = 0f; t < rattleSeconds; t += Time.unscaledDeltaTime)
        {
            float strength = 1f - t / rattleSeconds;
            art.transform.localPosition = home + new Vector3(Mathf.Sin(t * 70f) * 0.05f * strength, 0f, 0f);
            yield return null;
        }
        art.transform.localPosition = home;
    }

    // Already given out, the puddle there, for a scene picking up after it did.
    public void BreakNow()
    {
        if (IsBroken) return;
        IsBroken = true;
        StartCoroutine(Leak(0f));
    }

    // The puddle spreading out over this many seconds (none: there already).
    IEnumerator Leak(float seconds)
    {
        puddle = new GameObject("Puddle").AddComponent<SpriteRenderer>();
        // Not under the stall's sorting group, so it lies on the floor with the stains, under everyone.
        puddle.transform.SetParent(transform.parent, false);
        puddle.transform.position =transform.position + new Vector3(0f, -DepthSort.FeetToCenter - 0.3f, 0f);
        puddle.sprite = MakePuddle();
        puddle.sortingLayerName = "FloorObject";
        puddle.sortingOrder = 2;
        float full = puddleSize / (40f / PixelsPerUnit);
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float grown = Mathf.SmoothStep(0.1f, 1f, t / seconds) * full;
            puddle.transform.localScale = new Vector3(grown, grown, 1f);
            yield return null;
        }
        puddle.transform.localScale = new Vector3(full, full, 1f);
    }

    // --- Art ---

    static AudioClip knockTone;
    static AudioClip KnockTone => knockTone != null ? knockTone : (knockTone = TitleUI.Tone("Knock", 180f, 0.12f, 0.6f));
    static AudioClip gurgleTone;
    static AudioClip GurgleTone => gurgleTone != null ? gurgleTone : (gurgleTone = TitleUI.Tone("Toilet Gurgle", 62f, 1.1f, 0.8f));

    // A pale stall: grey side panels with the toilet between them, bowl and tank against the back wall. Shut, a door
    // fills the front, with a red light over the latch and someone's feet showing in the gap underneath.
    static Sprite MakeStall(bool shut)
    {
        var panel = new Color32(118, 128, 140, 255);
        var panelEdge = new Color32(78, 86, 96, 255);
        var porcelain = new Color32(232, 236, 240, 255);
        var shade = new Color32(176, 184, 194, 255);
        var outline = new Color32(40, 44, 52, 255);
        var water = new Color32(120, 190, 230, 255);
        var handle = new Color32(200, 170, 80, 255);
        var door = new Color32(140, 150, 162, 255);
        var doorEdge = new Color32(96, 104, 116, 255);
        var engaged = new Color32(230, 70, 60, 255);
        var shoe = new Color32(34, 36, 42, 255);

        Texture2D texture = PixelArt.MakeTexture(Width, Height, (x, y) =>
        {
            // The side panels, full height, with a darker edge.
            if (x <= 2 || x >= Width - 3) return x == 2 || x == Width - 3 ? panelEdge : panel;
            if (shut)
            {
                // A gap along the floor, with their shoes in it.
                if (y < 3) return (x >= 10 && x <= 13 || x >= 18 && x <= 21) && y <= 1 ? shoe : default;
                if (y > 32) return default;
                if (x == 3 || x == Width - 4 || y == 3 || y == 32) return doorEdge;
                if (y >= 26 && y <= 27 && x >= 22 && x <= 24) return engaged;
                if (y >= 17 && y <= 18 && x >= 23 && x <= 25) return handle;
                return door;
            }
            // The tank: a box high up against the back.
            if (y >= 20 && y <= 30 && x >= 9 && x <= 22)
            {
                if (y == 20 || y == 30 || x == 9 || x == 22) return outline;
                if (y == 27 && x >= 18 && x <= 20) return handle;
                return y <= 22 ? shade : porcelain;
            }
            // The bowl, seen from the front: an oval rim with water in it, on a narrow foot.
            float bowlX = (x - 15.5f) / 7.5f, bowlY = (y - 14f) / 5.5f;
            float bowl = bowlX * bowlX + bowlY * bowlY;
            if (bowl <= 1f)
            {
                if (bowl > 0.8f) return outline;
                float innerX = (x - 15.5f) / 5f, innerY = (y - 15f) / 3f;
                if (innerX * innerX + innerY * innerY <= 1f) return water;
                return y < 13 ? shade : porcelain;
            }
            if (y >= 2 && y < 10 && x >= 12 && x <= 19) return x == 12 || x == 19 || y == 2 ? outline : shade;
            return default;
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0f));
    }

    static Sprite MakePuddle()
    {
        const int Size = 40;
        var water = new Color32(90, 160, 210, 150);
        var glint = new Color32(190, 230, 255, 170);
        Texture2D texture = PixelArt.MakeTexture(Size, Size / 2, (x, y) =>
        {
            float dx = (x - Size * 0.5f) / (Size * 0.5f), dy = (y - Size * 0.25f) / (Size * 0.25f);
            float d = dx * dx + dy * dy + Mathf.Sin(x * 0.9f) * 0.08f;
            if (d > 1f) return default;
            return d < 0.2f && x % 5 == 0 ? glint : water;
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0.5f));
    }
}
