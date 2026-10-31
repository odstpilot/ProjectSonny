using System.Collections;
using UnityEngine;

// tool crate in the storage room
public class ToolCrate : Terminal
{
    const float PixelsPerUnit = 16f;
    const int Width = 26, Height = 22;

    public event System.Action Used;
    public bool IsOpen { get; private set; }

    [Tooltip("What the prompt says over it, before and after it's been searched.")]
    public string promptText = "SEARCH TOOL CRATE";
    public string openPromptText = "TOOL CRATE";
    [SoundName] public string openSound = "Crate Open";
    [Tooltip("Already open when the scene starts, as Chapter 1 left it.")]
    public bool startOpen;

    private bool inUse;
    private SpriteRenderer art;

    public override bool InUse => inUse;
    protected override string PromptText => IsOpen ? openPromptText : promptText;
    protected override Vector3 PromptPoint => transform.position + new Vector3(0f, Height / PixelsPerUnit - DepthSort.FeetToCenter + 0.2f, 0f);

    protected override void Awake()
    {
        base.Awake();
        art = new GameObject("Crate").AddComponent<SpriteRenderer>();
        art.transform.SetParent(transform, false);
        art.transform.localPosition = new Vector3(0f, -DepthSort.FeetToCenter, 0f);
        IsOpen = startOpen;
        art.sprite = MakeCrate(IsOpen);
        DepthSort.Group(gameObject);
    }

    protected override void Use(Transform player)
    {
        if (Used == null) return;
        inUse = true;
        Used.Invoke();
    }

    public void Done() => inUse = false;

    public void Occupy() => inUse = true;

    public void OpenNow()
    {
        IsOpen = true;
        art.sprite = MakeCrate(true);
    }

    public IEnumerator Open()
    {
        if (IsOpen) yield break;
        ScreenFade.Get().PlaySound(openSound, OpenNoise);
        Vector3 home = art.transform.localPosition;
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            art.transform.localPosition = home + new Vector3(0f, Mathf.Sin(t / 0.25f * Mathf.PI) * 0.06f, 0f);
            yield return null;
        }
        art.transform.localPosition = home;
        IsOpen = true;
        art.sprite = MakeCrate(true);
    }

    static AudioClip openNoise;
    static AudioClip OpenNoise => openNoise != null ? openNoise : (openNoise = TitleUI.Crackle("Crate Open", 0.3f, 0.5f));

    static Sprite MakeCrate(bool open)
    {
        var metal = new Color32(84, 108, 124, 255);
        var metalLit = new Color32(122, 148, 164, 255);
        var outline = new Color32(34, 40, 48, 255);
        var stripe = new Color32(230, 180, 60, 255);
        var foam = new Color32(28, 30, 36, 255);
        var hole = new Color32(12, 12, 16, 255);
        var paper = new Color32(236, 228, 200, 255);
        var ink = new Color32(90, 90, 110, 255);

        const int BoxTop = 11;
        Texture2D texture = PixelArt.MakeTexture(Width, Height, (x, y) =>
        {
            bool edge = x == 0 || x == Width - 1;
            if (y <= BoxTop)
            {
                if (edge || y == 0) return outline;
                if (y >= 3 && y <= 4) return (x / 2) % 2 == 0 ? stripe : outline;
                if (!open && y >= BoxTop - 1) return y == BoxTop ? outline : metalLit;
                if (open && y >= BoxTop - 2)
                {
                    if (x >= 16 && x <= 21 && y >= BoxTop - 2) return (x + y) % 3 == 0 ? ink : paper;
                    if ((y == BoxTop - 1 && x >= 4 && x <= 12) || (x >= 11 && x <= 13 && y >= BoxTop - 2)) return hole;
                    return foam;
                }
                return x == 1 || y == BoxTop - 2 ? metalLit : metal;
            }
            if (!open) return default;
            if (x < 2 || x > Width - 3) return default;
            if (x == 2 || x == Width - 3 || y == Height - 1) return outline;
            return y == BoxTop + 1 ? outline : metal;
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0f));
    }
}
