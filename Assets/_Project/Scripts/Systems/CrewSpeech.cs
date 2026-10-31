using TMPro;
using UnityEngine;

// speech bubble over someone
public class CrewSpeech : MonoBehaviour, OverheadLayout.IItem
{
    const float Glide = 14f;
    const float Height = 0.72f;
    const float FontSize = 4f;
    const float MaxWidth = 6f;
    const float FadeTime = 0.2f;
    const float ReadBase = 1.2f;
    const float ReadPerLetter = 0.055f;
    static readonly Vector2 Padding = new Vector2(0.22f, 0.12f);
    const string SortingLayer = "UI";

    private Transform speaker;
    private TextMeshPro text;
    private SpriteRenderer backing;
    private SpriteRenderer tail;
    private float showFor;
    private float lift;
    private float alpha;
    private Vector2 offset;
    private bool placedYet;
    private float since;

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

    Vector3 Home => speaker.position + new Vector3(0f, Height + lift, 0f);

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
