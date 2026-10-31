using UnityEngine;
using UnityEngine.Rendering.Universal;

// search a dead crew member for parts
public class BodyLoot : Terminal
{
    static readonly Color GlowColor = new Color(0.4f, 0.85f, 1f);

    [Tooltip("Unique in the scene: how the Inventory remembers it's been searched.")]
    public string id = "";
    public Inventory.Material[] parts = new Inventory.Material[0];
    [Tooltip("What the technician says, searching it.")]
    [TextArea] public string technicianLine = "";

    private Light2D glow;
    private float phase;

    public override bool InUse => false;
    protected override string PromptText => "SEARCH";
    protected override Vector3 PromptPoint => transform.position + new Vector3(0f, 1f, 0f);
    protected override bool Available => !Searched;

    bool Searched => parts.Length == 0 || Inventory.Took(PartId(0));
    string PartId(int i) => $"{id}-{i}";

    protected override void Awake()
    {
        base.Awake();
        phase = Random.value * 10f;
        glow = new GameObject("Glint").AddComponent<Light2D>();
        glow.transform.SetParent(transform, false);
        glow.lightType = Light2D.LightType.Point;
        glow.color = GlowColor;
        glow.pointLightInnerRadius = 0f;
        glow.pointLightOuterRadius = 0.7f;
        glow.falloffIntensity = 0.7f;
    }

    protected override void Update()
    {
        bool searched = Searched;
        glow.enabled = !searched;
        if (searched)
        {
            InteractPrompt.Hide(this);
            return;
        }
        float t = Time.time + phase;
        glow.intensity = 0.12f + 0.08f * Mathf.Sin(t * 2.2f);
        if (Random.value < Time.deltaTime * 0.15f)
            HitEffects.Sparks((Vector2)transform.position + Random.insideUnitCircle * 0.3f, Vector2.up, 1, 0.8f, 90f, Color.white, GlowColor);
        base.Update();
    }

    protected override void Use(Transform player)
    {
        if (Searched) return;
        if (!string.IsNullOrEmpty(technicianLine)) TechnicianVoice.Say(technicianLine);
        for (int i = 0; i < parts.Length; i++)
        {
            Inventory.Add(PartId(i), parts[i], 1);
            InventoryHud.Collected(parts[i], 1, transform.position);
        }
        LightFlash.Spawn(transform.position, GlowColor, 1f, 0.6f, 0.2f);
    }
}
