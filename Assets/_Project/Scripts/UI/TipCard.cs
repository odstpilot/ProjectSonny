using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A card that pops up over the game to explain how something works, a few rows of it: each with its keys (as key caps)
// or a little picture, a heading, and a line or two under it. The game pauses while it's up; E, Space, Enter, or a
// click puts it away. yield return TipCard.Show(title, rows) waits until it's gone.
// Built from code the first time it's needed. Runs on unscaled time, since it pauses the game.
public class TipCard : MonoBehaviour
{
    const int SortingOrder = 112;
    const float Width = 1180f;
    const float RowHeight = 104f;
    const float FadeTime = 0.18f;
    static readonly Color PanelColor = new Color(0.04f, 0.045f, 0.06f, 0.97f);
    static readonly Color RowColor = new Color(1f, 1f, 1f, 0.04f);

    public enum Picture { None, Eye, Ring, Camera, Locker }

    public class Row
    {
        public string[] keys = new string[0];
        public Picture picture;
        public string heading;
        public string text;

        public Row(string heading, string text, params string[] keys)
        {
            this.heading = heading;
            this.text = text;
            this.keys = keys;
        }

        public Row(Picture picture, string heading, string text)
        {
            this.picture = picture;
            this.heading = heading;
            this.text = text;
        }
    }

    public static bool IsOpen { get; private set; }

    public static IEnumerator Show(string title, params Row[] rows)
    {
        var card = new GameObject("TipCard", typeof(RectTransform)).AddComponent<TipCard>();
        yield return card.Run(title, rows);
        Destroy(card.gameObject);
    }

    IEnumerator Run(string title, Row[] rows)
    {
        IsOpen = true;
        float savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        GameObject found = GameObject.FindWithTag("Player");
        PlayerController player = found != null ? found.GetComponent<PlayerController>() : null;
        bool held = player != null && !player.IsScripted;
        if (held) player.SetScriptedInput(Vector2.zero, false);

        CanvasGroup group = Build(title, rows, out RectTransform panel, out TextMeshProUGUI prompt);
        for (float t = 0f; t < FadeTime; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / FadeTime);
            group.alpha = p;
            panel.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, p);
            yield return null;
        }
        group.alpha = 1f;
        panel.localScale = Vector3.one;

        // A moment before it can be put away, so a key already held doesn't skip it.
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) yield return null;
        prompt.alpha = 1f;
        while (!(Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)
                 || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0)))
        {
            prompt.alpha = 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 1.6f, 1f);
            yield return null;
        }

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

    CanvasGroup Build(string title, Row[] rows, out RectTransform panel, out TextMeshProUGUI prompt)
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

        float height = 200f + rows.Length * (RowHeight + 12f);
        Image frame = GameUI.Sliced("Panel", root, GameUI.PanelSprite, PanelColor);
        panel = frame.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(Width, height);
        var edge = frame.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(0.45f, 0.3f, 0.1f);
        edge.effectDistance = new Vector2(4f, -4f);

        TextMeshProUGUI heading = GameUI.Label("Title", panel, title.ToUpperInvariant(), 40f, GameUI.Amber, TextAlignmentOptions.Top, GameUI.Heading);
        heading.characterSpacing = 8f;
        GameUI.Anchor(heading.rectTransform, 0f, 1f, 1f, 1f, 40f, -86f, 40f, 34f);

        for (int i = 0; i < rows.Length; i++)
        {
            Row row = rows[i];
            Image band = GameUI.NewRect("Row", panel).gameObject.AddComponent<Image>();
            band.color = RowColor;
            band.raycastTarget = false;
            RectTransform rect = band.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(40f, 0f);
            rect.offsetMax = new Vector2(-40f, 0f);
            rect.sizeDelta = new Vector2(-80f, RowHeight);
            rect.anchoredPosition = new Vector2(0f, -110f - i * (RowHeight + 12f));

            // The keys, or the picture, in a column on the left.
            RectTransform badge = GameUI.NewRect("Badge", rect);
            badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(0f, 0.5f);
            badge.anchoredPosition = new Vector2(20f, 0f);
            badge.sizeDelta = new Vector2(220f, RowHeight - 20f);
            if (row.keys.Length > 0)
            {
                var layout = badge.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 8f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                foreach (string key in row.keys)
                {
                    Image cap = GameUI.KeyCap(badge, key, 28f, out _);
                    var size = cap.gameObject.AddComponent<LayoutElement>();
                    size.preferredHeight = 56f;
                    size.preferredWidth = GameUI.KeyCapWidth(key, 56f);
                }
            }
            else if (row.picture != Picture.None)
            {
                Image picture = GameUI.NewRect("Picture", badge).gameObject.AddComponent<Image>();
                picture.sprite = PictureSprite(row.picture);
                picture.raycastTarget = false;
                picture.preserveAspect = true;
                picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                picture.rectTransform.sizeDelta = new Vector2(84f, 72f);
            }

            TextMeshProUGUI name = GameUI.Label("Heading", rect, row.heading.ToUpperInvariant(), 28f, GameUI.Text, TextAlignmentOptions.TopLeft, GameUI.Heading);
            name.characterSpacing = 3f;
            GameUI.Anchor(name.rectTransform, 0f, 0f, 1f, 1f, 270f, 0f, 20f, 14f);
            TextMeshProUGUI body = GameUI.Label("Text", rect, row.text, 24f, GameUI.Dim, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            GameUI.Anchor(body.rectTransform, 0f, 0f, 1f, 1f, 270f, 8f, 20f, 50f);
        }

        prompt = GameUI.Label("Continue", panel, "E  /  CLICK  TO CONTINUE", 24f, GameUI.Amber, TextAlignmentOptions.Bottom);
        prompt.alpha = 0f;
        GameUI.Anchor(prompt.rectTransform, 0f, 0f, 1f, 0f, 40f, 30f, 40f, -70f);
        return group;
    }

    // --- Pictures, made in code ---

    static readonly Dictionary<Picture, Sprite> pictures = new Dictionary<Picture, Sprite>();

    static Sprite PictureSprite(Picture picture)
    {
        if (pictures.TryGetValue(picture, out Sprite made) && made != null) return made;
        var palette = new Dictionary<char, Color32>
        {
            { '#', new Color32(235, 230, 220, 255) },
            { 'a', new Color32(255, 174, 82, 255) },
            { 'r', new Color32(255, 82, 69, 255) },
            { 'd', new Color32(110, 116, 128, 255) },
        };
        string[] rows = picture switch
        {
            Picture.Eye => new[]
            {
                "....######....",
                "..##......##..",
                ".#....aa....#.",
                "#....aaaa....#",
                "#....aaaa....#",
                ".#....aa....#.",
                "..##......##..",
                "....######....",
            },
            Picture.Ring => new[]
            {
                "....aaaa....",
                "..aa....aa..",
                ".a........r.",
                "a..........r",
                "a....##....r",
                "a....##....r",
                "a..........r",
                ".a........r.",
                "..aa....rr..",
                "....rrrr....",
            },
            Picture.Camera => new[]
            {
                "..######....",
                ".#dddddd#...",
                "#dddddddd#..",
                "#ddddddddd##",
                ".#ddddddd#rr",
                "..#ddddd#.rr",
                "...######...",
                "............",
            },
            _ => new[]
            {
                "##########",
                "#dddddddd#",
                "#d######d#",
                "#dddddddd#",
                "#d######d#",
                "#dddddddd#",
                "#dddddda.#",
                "#dddddddd#",
                "#d######d#",
                "#dddddddd#",
                "##########",
            },
        };
        made = PixelArt.FromText(rows, palette, 16f, new Vector2(0.5f, 0.5f));
        pictures[picture] = made;
        return made;
    }
}
