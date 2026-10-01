using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The workbench's screen (CraftingBench, Workshop): the parts the technician's carrying (Inventory), a picture and a
// count of each along the top, and what they can be made into, a card each: a picture, what it's for, the parts it
// takes (each with how many there are of how many it needs, green when there's enough, red when not), and whether it's
// made already. Pick one with A/D, the arrow keys, the number keys, or the mouse, and make it with E, Enter, or a click:
// a bar fills with the sound of soldering, and it's done (Crafted). Short of something, and the parts it's short of
// flash red. Esc, Tab, or a click outside it closes it. The game's paused while it's up.
// yield return CraftingScreen.Show(recipes) waits until it's closed.
// Built from code each time it opens. Runs on unscaled time, since it pauses the game.
public class CraftingScreen : MonoBehaviour
{
    const int SortingOrder = 110;
    const float CardWidth = 520f, CardHeight = 500f, CardGap = 40f;
    const float CraftSeconds = 1.2f;
    const float FadeTime = 0.16f;
    static readonly Color PanelColor = new Color(0.05f, 0.05f, 0.06f, 0.97f);
    static readonly Color CardColor = new Color(0.1f, 0.1f, 0.12f, 1f);
    static readonly Color CardPicked = new Color(0.17f, 0.15f, 0.12f, 1f);
    static readonly Color Edge = new Color(0.45f, 0.3f, 0.1f);

    public class Recipe
    {
        public string id;
        public string name;
        public string description;
        public Inventory.Stack[] cost = new Inventory.Stack[0];
        public Sprite picture;
    }

    public static bool IsOpen { get; private set; }

    // Something's just been made at the bench.
    public static event System.Action<Recipe> Crafted;

    class Card
    {
        public Recipe recipe;
        public RectTransform rect;
        public Image back;
        public Outline outline;
        public readonly List<TextMeshProUGUI> costs = new List<TextMeshProUGUI>();
        public TextMeshProUGUI status;
        public RectTransform bar;
    }

    private readonly List<Card> cards = new List<Card>();
    private readonly Dictionary<Inventory.Material, TextMeshProUGUI> carried = new Dictionary<Inventory.Material, TextMeshProUGUI>();
    private RectTransform panel;
    private int picked;
    private bool crafting;
    private AudioSource voice;
    private static AudioClip solder, denied;

    public static IEnumerator Show(params Recipe[] recipes)
    {
        var screen = new GameObject("CraftingScreen", typeof(RectTransform)).AddComponent<CraftingScreen>();
        yield return screen.Run(recipes);
        Destroy(screen.gameObject);
    }

    IEnumerator Run(Recipe[] recipes)
    {
        IsOpen = true;
        float savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        GameObject found = GameObject.FindWithTag("Player");
        PlayerController player = found != null ? found.GetComponent<PlayerController>() : null;
        bool held = player != null && !player.IsScripted;
        if (held) player.SetScriptedInput(Vector2.zero, false);

        CanvasGroup group = Build(recipes);
        picked = Mathf.Max(0, cards.FindIndex(c => !Inventory.Made(c.recipe.id)));
        Refresh();
        for (float t = 0f; t < FadeTime; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / FadeTime;
            panel.localScale = new Vector3(1f, Mathf.Lerp(0.04f, 1f, Mathf.SmoothStep(0f, 1f, t / FadeTime)), 1f);
            yield return null;
        }
        group.alpha = 1f;
        panel.localScale = Vector3.one;
        yield return null;      // so the E that opened it doesn't make something

        while (true)
        {
            if (!crafting)
            {
                bool clickedOutside = Input.GetMouseButtonDown(0) && !RectTransformUtility.RectangleContainsScreenPoint(panel, Input.mousePosition, null);
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) || clickedOutside) break;
                Choose();
            }
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

    // Picking a card, and making what's on it.
    void Choose()
    {
        int before = picked;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) picked = Mathf.Max(0, picked - 1);
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) picked = Mathf.Min(cards.Count - 1, picked + 1);
        bool make = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        for (int i = 0; i < cards.Count && i < 9; i++)
        {
            bool hovered = RectTransformUtility.RectangleContainsScreenPoint(cards[i].rect, Input.mousePosition, null);
            if (hovered && (Input.GetAxisRaw("Mouse X") != 0f || Input.GetAxisRaw("Mouse Y") != 0f)) picked = i;
            if (hovered && Input.GetMouseButtonDown(0))
            {
                picked = i;
                make = true;
            }
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                picked = i;
                make = true;
            }
        }
        if (picked != before) Refresh();
        if (make) StartCoroutine(Make(cards[picked]));
    }

    IEnumerator Make(Card card)
    {
        if (Inventory.Made(card.recipe.id)) yield break;
        if (!Inventory.Has(card.recipe.cost))
        {
            if (denied == null) denied = TitleUI.Crackle("Craft Denied", 0.12f, 0.4f);
            voice.PlayOneShot(denied, 0.6f);
            for (int flash = 0; flash < 3; flash++)
            {
                for (int i = 0; i < card.recipe.cost.Length; i++)
                    if (Inventory.Count(card.recipe.cost[i].material) < card.recipe.cost[i].count) card.costs[i].color = Color.white;
                yield return GameUI.WaitUnscaled(0.08f);
                Refresh();
                yield return GameUI.WaitUnscaled(0.08f);
            }
            yield break;
        }

        crafting = true;
        card.status.text = "MAKING...";
        card.status.color = GameUI.Amber;
        if (solder == null) solder = TitleUI.Crackle("EMP Craft", 0.3f, 0.6f);
        float nextSound = 0f;
        for (float t = 0f; t < CraftSeconds; t += Time.unscaledDeltaTime)
        {
            if (t >= nextSound)
            {
                SoundManager.PlayOneShot(voice, "EMP Craft", 0.7f, Random.Range(0.85f, 1.15f), solder);
                nextSound += 0.3f;
            }
            card.bar.anchorMax = new Vector2(t / CraftSeconds, 1f);
            yield return null;
        }
        card.bar.anchorMax = Vector2.one;
        Inventory.Make(card.recipe.id, card.recipe.cost);
        Crafted?.Invoke(card.recipe);
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            card.back.color = Color.Lerp(GameUI.Green, CardPicked, t / 0.25f);
            yield return null;
        }
        crafting = false;
        Refresh();
    }

    void Refresh()
    {
        foreach (KeyValuePair<Inventory.Material, TextMeshProUGUI> count in carried)
        {
            int have = Inventory.Count(count.Key);
            count.Value.text = $"x{have}";
            count.Value.color = have > 0 ? GameUI.Text : GameUI.Dim;
        }
        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            bool made = Inventory.Made(card.recipe.id);
            bool enough = Inventory.Has(card.recipe.cost);
            card.back.color = i == picked ? CardPicked : CardColor;
            card.outline.effectColor = i == picked ? GameUI.Amber : Edge;
            for (int k = 0; k < card.recipe.cost.Length; k++)
            {
                Inventory.Stack need = card.recipe.cost[k];
                int have = Inventory.Count(need.material);
                card.costs[k].text = made ? $"{need.count}" : $"{Mathf.Min(have, need.count)}/{need.count}";
                card.costs[k].color = made ? GameUI.Dim : have >= need.count ? GameUI.Green : GameUI.Red;
            }
            card.status.text = made ? "MADE" : enough ? $"[{i + 1}]  MAKE IT" : "NEED MORE PARTS";
            card.status.color = made ? GameUI.Green : enough ? GameUI.Text : GameUI.Dim;
            card.bar.anchorMax = new Vector2(made ? 1f : 0f, 1f);
        }
    }

    // --- Building ---

    CanvasGroup Build(Recipe[] recipes)
    {
        PromptBadge.MakeCanvas(gameObject, SortingOrder);
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.ignoreListenerPause = true;
        RectTransform root = GameUI.Fill((RectTransform)transform);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = group.blocksRaycasts = false;

        Image backdrop = GameUI.NewRect("Backdrop", root).gameObject.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.62f);
        backdrop.raycastTarget = false;
        GameUI.Fill(backdrop.rectTransform);

        float width = recipes.Length * CardWidth + (recipes.Length - 1) * CardGap + 120f;
        Image frame = GameUI.Sliced("Panel", root, GameUI.PanelSprite, PanelColor);
        panel = frame.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(width, CardHeight + 240f);
        var edge = frame.gameObject.AddComponent<Outline>();
        edge.effectColor = Edge;
        edge.effectDistance = new Vector2(4f, -4f);

        TextMeshProUGUI title = GameUI.Label("Title", panel, "WORKBENCH", 40f, GameUI.Amber, TextAlignmentOptions.TopLeft, GameUI.Heading);
        title.characterSpacing = 8f;
        GameUI.Anchor(title.rectTransform, 0f, 1f, 1f, 1f, 60f, -90f, 60f, 36f);

        // What's being carried, along the top right: a picture and a count of each part.
        for (int i = 0; i < Inventory.Materials.Length; i++)
        {
            Inventory.Material material = Inventory.Materials[Inventory.Materials.Length - 1 - i];
            float right = 60f + i * 130f;
            TextMeshProUGUI count = GameUI.Label("Count", panel, "", 30f, GameUI.Text, TextAlignmentOptions.MidlineLeft, GameUI.Heading);
            count.rectTransform.anchorMin = count.rectTransform.anchorMax = count.rectTransform.pivot = new Vector2(1f, 1f);
            count.rectTransform.sizeDelta = new Vector2(60f, 50f);
            count.rectTransform.anchoredPosition = new Vector2(-right, -40f);
            carried[material] = count;
            Icon(panel, material, new Vector2(1f, 1f), new Vector2(-right - 64f, -40f), 54f);
        }

        TextMeshProUGUI hint = GameUI.Label("Hint", panel, "A D / MOUSE  PICK      E / CLICK  MAKE      ESC  LEAVE", 22f, GameUI.Dim, TextAlignmentOptions.Bottom);
        GameUI.Anchor(hint.rectTransform, 0f, 0f, 1f, 0f, 60f, 28f, 60f, -64f);

        for (int i = 0; i < recipes.Length; i++)
        {
            Recipe recipe = recipes[i];
            var card = new Card { recipe = recipe };
            card.back = GameUI.NewRect("Card", panel).gameObject.AddComponent<Image>();
            card.back.raycastTarget = false;
            card.rect = card.back.rectTransform;
            card.rect.anchorMin = card.rect.anchorMax = new Vector2(0f, 1f);
            card.rect.pivot = new Vector2(0f, 1f);
            card.rect.sizeDelta = new Vector2(CardWidth, CardHeight);
            card.rect.anchoredPosition = new Vector2(60f + i * (CardWidth + CardGap), -120f);
            card.outline = card.back.gameObject.AddComponent<Outline>();
            card.outline.effectDistance = new Vector2(3f, -3f);

            Image picture = GameUI.NewRect("Picture", card.rect).gameObject.AddComponent<Image>();
            picture.sprite = recipe.picture;
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = picture.rectTransform.pivot = new Vector2(0.5f, 1f);
            picture.rectTransform.sizeDelta = new Vector2(220f, 140f);
            picture.rectTransform.anchoredPosition = new Vector2(0f, -26f);

            TextMeshProUGUI name = GameUI.Label("Name", card.rect, recipe.name.ToUpperInvariant(), 36f, GameUI.Text, TextAlignmentOptions.Top, GameUI.Heading);
            name.characterSpacing = 5f;
            GameUI.Anchor(name.rectTransform, 0f, 1f, 1f, 1f, 24f, -224f, 24f, 180f);
            TextMeshProUGUI about = GameUI.Label("About", card.rect, recipe.description, 24f, GameUI.Dim, TextAlignmentOptions.Top);
            about.textWrappingMode = TextWrappingModes.Normal;
            GameUI.Anchor(about.rectTransform, 0f, 0f, 1f, 1f, 30f, 200f, 30f, 232f);

            // The parts it takes, side by side: a picture of each, and how many of how many.
            const float slot = 112f;
            float left = (CardWidth - recipe.cost.Length * slot) * 0.5f;
            for (int k = 0; k < recipe.cost.Length; k++)
            {
                float x = left + k * slot;
                Icon(card.rect, recipe.cost[k].material, new Vector2(0f, 0f), new Vector2(x + 6f, 142f), 46f);
                TextMeshProUGUI need = GameUI.Label("Need", card.rect, "", 26f, GameUI.Text, TextAlignmentOptions.MidlineLeft, GameUI.Heading);
                need.rectTransform.anchorMin = need.rectTransform.anchorMax = need.rectTransform.pivot = new Vector2(0f, 0f);
                need.rectTransform.sizeDelta = new Vector2(60f, 46f);
                need.rectTransform.anchoredPosition = new Vector2(x + 56f, 120f);
                card.costs.Add(need);
                TextMeshProUGUI label = GameUI.Label("Part", card.rect, Inventory.Name(recipe.cost[k].material).ToUpperInvariant(), 16f, GameUI.Dim,
                    TextAlignmentOptions.Center);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0f, 0f);
                label.rectTransform.sizeDelta = new Vector2(slot, 22f);
                label.rectTransform.anchoredPosition = new Vector2(x, 92f);
            }

            card.status = GameUI.Label("Status", card.rect, "", 28f, GameUI.Text, TextAlignmentOptions.Bottom, GameUI.Heading);
            card.status.characterSpacing = 4f;
            GameUI.Anchor(card.status.rectTransform, 0f, 0f, 1f, 0f, 24f, 30f, 24f, -70f);

            Image track = GameUI.NewRect("Progress", card.rect).gameObject.AddComponent<Image>();
            track.color = new Color(0f, 0f, 0f, 0.4f);
            track.raycastTarget = false;
            GameUI.Anchor(track.rectTransform, 0f, 0f, 1f, 0f, 0f, 0f, 0f, -8f);
            Image fill = GameUI.NewRect("Fill", track.rectTransform).gameObject.AddComponent<Image>();
            fill.color = GameUI.Cyan;
            fill.raycastTarget = false;
            card.bar = fill.rectTransform;
            card.bar.anchorMin = Vector2.zero;
            card.bar.anchorMax = new Vector2(0f, 1f);
            card.bar.offsetMin = card.bar.offsetMax = Vector2.zero;
            cards.Add(card);
        }
        return group;
    }

    // A part's picture, a square this big, its corner at this anchor (and pivot), so far from it.
    static void Icon(RectTransform parent, Inventory.Material material, Vector2 corner, Vector2 at, float size)
    {
        Image icon = GameUI.NewRect("Icon", parent).gameObject.AddComponent<Image>();
        icon.sprite = ScrapPickup.Make(material);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = corner;
        icon.rectTransform.sizeDelta = new Vector2(size, size);
        icon.rectTransform.anchoredPosition = at;
    }
}
