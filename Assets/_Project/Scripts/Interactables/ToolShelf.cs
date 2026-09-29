using System.Collections;
using UnityEngine;

// The storage room's shelf: a metal rack against the wall, stacked with crates, with a wrench that's rolled underneath it.
// Walk up and press E to reach under it. Reaching nudges the rack (Nudge); pulling the wrench out (Grab) clinks it free;
// and something rocking the rack harder and harder tips the crate on top off onto whoever's under it (Topple). What happens when E is pressed is up to whoever's listening
// (RestroomBreak, in Chapter 1): Used is raised, and the shelf stays in use until they call Done. With nobody listening
// it does nothing.
// Like the furniture, the object sits at its sorting point, DepthSort.FeetToCenter above the base of the rack, and its
// art is placed down from there. The art is made in code until it has its own. Built by ChapterOneBuilder.
public class ToolShelf : Terminal
{
    const float PixelsPerUnit = 16f;
    const int Width = 32, Height = 40;

    public event System.Action Used;
    public bool Taken { get; private set; }

    [Tooltip("What the prompt says over it, before and after the wrench is taken.")]
    public string promptText = "REACH UNDER SHELF";
    public string emptyPromptText = "SHELF";
    [Tooltip("The scene's sound for pulling the wrench out.")]
    [SoundName] public string takeSound = "Wrench Pickup";
    [Tooltip("The scene's sound for the rack rattling as it's knocked, and for the crate landing on someone's head.")]
    [SoundName] public string rattleSound = "Shelf Rattle";
    [SoundName] public string hitSound = "Knockout Hit";
    [Tooltip("How long the rack rocks before the crate on top goes, and how far it leans at the worst of it, in degrees.")]
    public float rattleSeconds = 1.3f;
    public float leanDegrees = 5f;
    [Tooltip("How long the crate takes to come down.")]
    public float fallSeconds = 0.42f;
    [Tooltip("Already knocked about when the scene starts, as Chapter 1 left it: the wrench gone from under it, and the crate off the top lying on the floor in front.")]
    public bool startToppled;

    private bool inUse;
    private SpriteRenderer art;

    public override bool InUse => inUse;
    protected override string PromptText => Taken ? emptyPromptText : promptText;
    protected override Vector3 PromptPoint => transform.position + new Vector3(0f, Height / PixelsPerUnit - DepthSort.FeetToCenter + 0.2f, 0f);
    protected override bool Available => !Taken;

    protected override void Awake()
    {
        base.Awake();
        art = new GameObject("Rack").AddComponent<SpriteRenderer>();
        art.transform.SetParent(transform, false);
        art.transform.localPosition = new Vector3(0f, -DepthSort.FeetToCenter, 0f);
        art.sprite = MakeShelf(true);
        DepthSort.Group(gameObject);

        if (!startToppled) return;
        Taken = true;
        art.sprite = MakeShelf(false, false);
        SpriteRenderer crate = MakeFallingCrate();
        Vector2 floor = (Vector2)art.transform.position + new Vector2(1.1f, -0.9f);
        crate.transform.position = new Vector3(floor.x, floor.y, 0f);
        crate.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
        crate.sortingLayerName = DepthSort.Layer;
    }

    protected override void Use(Transform player)
    {
        if (Used == null) return;
        inUse = true;
        Used.Invoke();
    }

    // Lets go of it, for whoever took it on Used.
    public void Done() => inUse = false;

    // Takes it as if it had been used, without raising Used, for a script that's using it (until Done).
    public void Occupy() => inUse = true;

    // An arm under the rack, feeling about: it knocks the rack a little this way and that.
    public IEnumerator Nudge(float strength = 1f)
    {
        const float Seconds = 0.22f;
        for (float t = 0f; t < Seconds; t += Time.unscaledDeltaTime)
        {
            float lean = Mathf.Sin(t / Seconds * Mathf.PI * 2f) * leanDegrees * 0.25f * strength;
            art.transform.localRotation = Quaternion.Euler(0f, 0f, lean);
            yield return null;
        }
        art.transform.localRotation = Quaternion.identity;
    }

    // The wrench found and pulled out from under it: a clink, and it's gone from the gap.
    public IEnumerator Grab()
    {
        if (Taken) yield break;
        Taken = true;
        ScreenFade.Get().PlaySound(takeSound, ClinkTone);
        art.sprite = MakeShelf(false);
        yield return Wait(0.35f);
    }

    // The rack rocks, worse and worse, until the crate on top tips off and comes down on the head at headPoint (pulling
    // the wrench out first, if it's still there). Done the moment it hits; then it bounces off onto the floor beside
    // them (feetY is the floor there) on its own.
    public IEnumerator Topple(Vector2 headPoint, float feetY)
    {
        yield return Grab();
        ScreenFade fade = ScreenFade.Get();

        fade.PlaySound(rattleSound, RattleNoise);
        for (float t = 0f; t < rattleSeconds; t += Time.unscaledDeltaTime)
        {
            float worse = t / rattleSeconds;
            float lean = Mathf.Sin(t * (14f + worse * 16f)) * leanDegrees * worse * worse;
            art.transform.localRotation = Quaternion.Euler(0f, 0f, lean);
            yield return null;
        }
        art.transform.localRotation = Quaternion.identity;

        // Off it comes: the top crate leaves the rack and drops in an arc onto their head.
        art.sprite = MakeShelf(false, false);
        SpriteRenderer crate = MakeFallingCrate();
        Vector2 from = TopCratePoint;
        for (float t = 0f; t < fallSeconds; t += Time.unscaledDeltaTime)
        {
            float p = t / fallSeconds;
            Vector2 at = Vector2.Lerp(from, headPoint, p) + Vector2.up * Mathf.Sin(p * Mathf.PI) * 0.35f;
            crate.transform.position = new Vector3(at.x, at.y - 0.4f * p * p, 0f);
            crate.transform.rotation = Quaternion.Euler(0f, 0f, -200f * p);
            yield return null;
        }
        fade.PlaySound(hitSound, HitTone);
        StartCoroutine(Bounce(crate, headPoint, feetY));
    }

    // Off their head and onto the floor, where it stays.
    IEnumerator Bounce(SpriteRenderer crate, Vector2 from, float feetY)
    {
        Vector2 to = new Vector2(from.x + 0.9f, feetY);
        const float Seconds = 0.4f;
        for (float t = 0f; t < Seconds; t += Time.unscaledDeltaTime)
        {
            float p = t / Seconds;
            Vector2 at = Vector2.Lerp(from, to, p) + Vector2.up * Mathf.Sin(p * Mathf.PI) * 0.6f;
            crate.transform.position = new Vector3(at.x, at.y, 0f);
            crate.transform.rotation = Quaternion.Euler(0f, 0f, -200f - 250f * p);
            yield return null;
        }
        crate.transform.position = new Vector3(to.x, to.y, 0f);
        crate.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
        // Down on the floor with everything else, sorted like it.
        crate.sortingLayerName = DepthSort.Layer;
    }

    // Where the crate on the top shelf sits, in the world.
    Vector2 TopCratePoint => (Vector2)art.transform.position + new Vector2((12.5f - Width * 0.5f) / PixelsPerUnit, 36.5f / PixelsPerUnit);

    SpriteRenderer MakeFallingCrate()
    {
        var crate = new GameObject("Falling Crate").AddComponent<SpriteRenderer>();
        crate.transform.SetParent(transform.parent, false);
        crate.transform.position = TopCratePoint;
        crate.sprite = MakeCrate();
        crate.sortingLayerName = "Top";     // over everyone while it's in the air
        return crate;
    }

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    // --- Art ---

    static AudioClip clinkTone;
    static AudioClip ClinkTone => clinkTone != null ? clinkTone : (clinkTone = TitleUI.Tone("Wrench Pickup", 880f, 0.25f, 0.5f));
    static AudioClip rattleNoise;
    static AudioClip RattleNoise => rattleNoise != null ? rattleNoise : (rattleNoise = TitleUI.Crackle("Shelf Rattle", 1.2f, 0.5f));
    static AudioClip hitTone;
    static AudioClip HitTone => hitTone != null ? hitTone : (hitTone = TitleUI.Tone("Knockout Hit", 70f, 0.5f, 1f));

    // A grey rack: two uprights, three shelves of crates and boxes, and a dark gap along the floor with the wrench's
    // glint in it (while it's there).
    // The crate off the top shelf, on its own.
    static Sprite MakeCrate()
    {
        var crate = new Color32(150, 112, 64, 255);
        var crateDark = new Color32(108, 78, 42, 255);
        Texture2D texture = PixelArt.MakeTexture(9, 5, (x, y) => x == 0 || x == 8 || y == 4 ? crateDark : crate);
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0.5f));
    }

    static Sprite MakeShelf(bool withWrench, bool withTopCrate = true)
    {
        var frame = new Color32(70, 76, 86, 255);
        var frameLit = new Color32(112, 120, 132, 255);
        var gap = new Color32(18, 20, 24, 255);
        var crate = new Color32(150, 112, 64, 255);
        var crateDark = new Color32(108, 78, 42, 255);
        var box = new Color32(96, 130, 150, 255);
        var boxDark = new Color32(66, 92, 108, 255);
        var steel = new Color32(200, 206, 214, 255);

        Texture2D texture = PixelArt.MakeTexture(Width, Height, (x, y) =>
        {
            bool upright = x <= 1 || x >= Width - 2;
            if (upright) return x == 0 || x == Width - 1 ? frame : frameLit;
            // Shelves at these heights; the bottom one sits over the gap.
            if (y == 6 || y == 7 || y == 19 || y == 20 || y == 32 || y == 33) return y % 2 == 0 ? frame : frameLit;
            if (y < 6)
            {
                // The wrench, lying in the dark under the bottom shelf.
                if (withWrench && y >= 2 && y <= 3 && x >= 16 && x <= 26) return steel;
                if (withWrench && y >= 1 && y <= 4 && (x == 25 || x == 26 || x == 27)) return steel;
                return gap;
            }
            // Crates on the bottom shelf, boxes above, a smaller box on top.
            if (y > 7 && y < 17 && x >= 3 && x <= 14) return x == 3 || x == 14 || y == 16 || y == 12 ? crateDark : crate;
            if (y > 7 && y < 14 && x >= 17 && x <= 28) return x == 17 || x == 28 || y == 13 ? boxDark : box;
            if (y > 20 && y < 29 && x >= 5 && x <= 18) return x == 5 || x == 18 || y == 28 ? boxDark : box;
            if (y > 20 && y < 27 && x >= 21 && x <= 28) return x == 21 || x == 28 || y == 26 ? crateDark : crate;
            if (withTopCrate && y > 33 && y < 39 && x >= 8 && x <= 16) return x == 8 || x == 16 || y == 38 ? crateDark : crate;
            return default;
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0f));
    }
}
