using TMPro;
using UnityEngine;

// A line of speech over someone's head (a crew member, CrewMember.Say; the technician, TechnicianVoice; a bot): on the
// same slim dark glass tag as the prompts (GameUI.WorldSoftPanelSprite), with a small notch pointing down at them. It
// fades in, holds, and fades out, and always stays up long enough to read, however briefly it was asked for. The text
// has a thin dark outline so it stays sharp over the level. Made from code the first time they speak, and kept out of
// their hierarchy so their sorting group can't bury it behind someone standing in front of them. It follows them about,
// and goes when they do. It never covers another line or a prompt: if one's in the way, it rises above it
// (OverheadLayout), gliding there, and its notch stays pointing down toward whoever's speaking.
public class CrewSpeech : MonoBehaviour, OverheadLayout.IItem
{
    const float Glide = 14f;                // how quickly it slides to where the layout puts it
    const float Height = 0.72f;             // from the speaker's middle to the bottom of the backing, in world units
    const float FontSize = 3f;
    const float MaxWidth = 4.6f;
    const float FadeTime = 0.2f;
    const float ReadBase = 1.2f;            // the least a line stays up: this, and a little more per letter
    const float ReadPerLetter = 0.055f;
    static readonly Vector2 Padding = new Vector2(0.22f, 0.12f);
    const string SortingLayer = "UI";

    private Transform speaker;
    private TextMeshPro text;
    private SpriteRenderer backing;
    private SpriteRenderer tail;
    private float showFor;
    private float lift;                     // from the bottom of the backing to its middle
    private float alpha;
    private Vector2 offset;                 // from where it would be, to where the layout's put it
    private bool placedYet;
    private float since;                    // when it came up, for the layout

    public static CrewSpeech Create(Transform speaker)
    {
        var speech = new GameObject($"{speaker.name} Speech").AddComponent<CrewSpeech>();
        speech.speaker = speaker;
        speech.Build();
        OverheadLayout.Add(speech);
        return speech;
    }

    public void Show(string line, float seconds)
    {
        text.text = line;
        Vector2 size = text.GetPreferredValues(line, MaxWidth, 0f);
        size.x = Mathf.Min(size.x, MaxWidth);
        text.rectTransform.sizeDelta = size;
        backing.size = new Vector2(size.x + Padding.x * 2f, size.y + Padding.y * 2f);
        tail.transform.localPosition = new Vector3(0f, -backing.size.y * 0.5f - 0.04f, 0f);
        lift = size.y * 0.5f + Padding.y;
        if (alpha <= 0f) since = Time.unscaledTime;
        showFor = Mathf.Max(seconds, ReadBase + line.Length * ReadPerLetter);
    }

    void Build()
    {
        backing = new GameObject("Backing").AddComponent<SpriteRenderer>();
        backing.transform.SetParent(transform, false);
        backing.sprite = GameUI.WorldSoftPanelSprite;
        backing.drawMode = SpriteDrawMode.Sliced;
        backing.sortingLayerName = SortingLayer;
        backing.sortingOrder = 10;

        // The notch's top edge tucks under the tag's bottom edge, so it reads as one shape.
        tail = new GameObject("Tail").AddComponent<SpriteRenderer>();
        tail.transform.SetParent(transform, false);
        tail.sprite = GameUI.WorldSoftTailSprite;
        tail.sortingLayerName = SortingLayer;
        tail.sortingOrder = 9;

        text = new GameObject("Text").AddComponent<TextMeshPro>();
        text.transform.SetParent(transform, false);
        text.font = GameUI.Body;
        text.fontSharedMaterial = GameUI.Shadow(text.font);
        text.fontSize = FontSize;
        text.color = GameUI.Text;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.lineSpacing = 6f;
        text.sortingLayerID = UnityEngine.SortingLayer.NameToID(SortingLayer);
        text.sortingOrder = 11;

        SetAlpha(0f);
    }

    // Gone at once, for when they start talking to the player instead.
    public void Hide()
    {
        showFor = 0f;
        SetAlpha(0f);
    }

    void LateUpdate()
    {
        if (speaker == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = Home + (Vector3)offset;
        showFor -= Time.deltaTime;
        float target = showFor > 0f ? 1f : 0f;
        if (!Mathf.Approximately(alpha, target)) SetAlpha(Mathf.MoveTowards(alpha, target, Time.deltaTime / FadeTime));
    }

    // Where it would be with nothing in the way: just over the speaker's head.
    Vector3 Home => speaker.position + new Vector3(0f, Height + lift, 0f);

    // --- Keeping out of the way (OverheadLayout) ---

    bool OverheadLayout.IItem.Alive => this != null && speaker != null;
    bool OverheadLayout.IItem.Visible => alpha > 0f && speaker != null;
    int OverheadLayout.IItem.Priority => 2;
    float OverheadLayout.IItem.Since => since;

    Rect OverheadLayout.IItem.Wanted
    {
        get
        {
            Vector2 center = Home;
            float tailDrop = 0.12f;
            return new Rect(center.x - backing.size.x * 0.5f, center.y - backing.size.y * 0.5f - tailDrop,
                            backing.size.x, backing.size.y + tailDrop);
        }
    }

    void OverheadLayout.IItem.Place(Vector2 target)
    {
        // Straight to its spot when it first shows; after that it glides, so nothing jumps about.
        offset = placedYet ? Vector2.Lerp(offset, target, 1f - Mathf.Exp(-Glide * Time.unscaledDeltaTime)) : target;
        placedYet = true;
        transform.position = Home + (Vector3)offset;
    }

    void OnDestroy() => OverheadLayout.Remove(this);

    void SetAlpha(float value)
    {
        if (value <= 0f) placedYet = false;
        alpha = value;
        text.alpha = value;
        backing.color = new Color(1f, 1f, 1f, value);
        tail.color = new Color(1f, 1f, 1f, value);
        text.enabled = backing.enabled = tail.enabled = value > 0f;
    }
}
