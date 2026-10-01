using UnityEngine;

// A weakened panel in a wall: cracked right through, the cracks glowing where the heat's getting in from behind, and
// a slow pulse to them so it catches the eye. Anything that can hurt it can break it (the scrap gun from across the
// room, the wrench up close): its Health runs out, and Broken is raised for whoever put it there (TutorialDirector,
// which blows the wall in). Its collider sits along the foot of the wall, a little proud of it, so shots fired at the
// panel hit it before they hit the wall.
// Its position is the middle of the panel, on the wall face. The art is made in code until it has its own.
[RequireComponent(typeof(Health))]
public class WeakWall : MonoBehaviour
{
    const float PixelsPerUnit = 16f;
    const int Size = 30;

    public event System.Action Broken;
    public bool IsBroken { get; private set; }

    [Tooltip("How fast the cracks pulse.")]
    public float pulseRate = 1.6f;

    private Health health;
    private SpriteRenderer panel;
    private SpriteRenderer glow;

    void Awake()
    {
        health = GetComponent<Health>();
        health.Died += OnDied;

        panel = new GameObject("Cracked Panel").AddComponent<SpriteRenderer>();
        panel.transform.SetParent(transform, false);
        panel.sprite = MakePanel(false);
        panel.sortingLayerName = "Collision";
        panel.sortingOrder = 5;

        glow = new GameObject("Cracks").AddComponent<SpriteRenderer>();
        glow.transform.SetParent(transform, false);
        glow.sprite = MakePanel(true);
        glow.sortingLayerName = "Collision";
        glow.sortingOrder = 6;
        if (CombatSprites.EffectMaterial != null) glow.sharedMaterial = CombatSprites.EffectMaterial;

        health.flashRenderers = new[] { panel };
    }

    void OnDestroy()
    {
        if (health != null) health.Died -= OnDied;
    }

    void Update()
    {
        if (IsBroken) return;
        float hurt = 1f - health.CurrentHealth / Mathf.Max(0.01f, health.maxHealth);
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * pulseRate * Mathf.PI * 2f);
        glow.color = new Color(1f, 0.55f + 0.3f * hurt, 0.2f, Mathf.Lerp(0.5f, 1f, hurt) * pulse);
    }

    // Broken some other way (the director's fallback): the panel's gone either way.
    public void Break()
    {
        if (IsBroken) return;
        health.Kill(gameObject);
    }

    void OnDied()
    {
        if (IsBroken) return;
        IsBroken = true;
        panel.enabled = glow.enabled = false;
        Broken?.Invoke();
    }

    // A darker, dented plate with a frame of hazard marks at its corners; or, for the glow, just the cracks.
    static Sprite MakePanel(bool cracksOnly)
    {
        var plate = new Color32(70, 64, 60, 255);
        var plateDark = new Color32(46, 42, 40, 255);
        var hazard = new Color32(230, 180, 60, 255);
        var crack = new Color32(255, 170, 80, 255);

        // Cracks: a few jagged lines out from a point off the middle.
        bool Crack(int x, int y)
        {
            float cx = 13f, cy = 15f;
            float[] angles = { 0.3f, 1.4f, 2.5f, 3.6f, 4.8f, 5.7f };
            foreach (float a in angles)
            {
                for (float r = 0f; r < 13f; r += 0.5f)
                {
                    float wobble = Mathf.Sin(r * 1.7f + a * 3f) * 1.2f;
                    int px = Mathf.RoundToInt(cx + Mathf.Cos(a) * r + Mathf.Cos(a + 1.57f) * wobble);
                    int py = Mathf.RoundToInt(cy + Mathf.Sin(a) * r + Mathf.Sin(a + 1.57f) * wobble);
                    if (px == x && py == y) return true;
                }
            }
            return false;
        }

        Texture2D texture = PixelArt.MakeTexture(Size, Size, (x, y) =>
        {
            if (cracksOnly) return Crack(x, y) ? crack : default;
            bool edge = x == 0 || y == 0 || x == Size - 1 || y == Size - 1;
            if (edge) return plateDark;
            bool corner = (x < 5 || x > Size - 6) && (y < 5 || y > Size - 6);
            if (corner) return (x + y) % 4 < 2 ? hazard : plateDark;
            if (Crack(x, y)) return plateDark;
            return (x * 7 + y * 3) % 17 == 0 ? plateDark : plate;
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0.5f));
    }
}
