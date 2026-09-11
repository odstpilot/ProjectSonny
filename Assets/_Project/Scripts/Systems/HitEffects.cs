using UnityEngine;

// Pooled particle effects for combat: sparks, impact rings, debris, and smoke.
// One particle system per kind handles every burst in the scene; they're created the first time an effect plays.
// Call the static methods from any script, e.g. HitEffects.Sparks(point, direction, ...).
public class HitEffects : MonoBehaviour
{
    // Drawn over the characters.
    const string SortingLayerName = "Player";
    const int SortingOrder = 50;

    static HitEffects instance;

    private ParticleSystem sparks;
    private ParticleSystem rings;
    private ParticleSystem debris;
    private ParticleSystem smoke;

    static HitEffects Instance
    {
        get
        {
            if (instance == null && CombatSprites.EffectMaterial != null)
            {
                instance = new GameObject("[HitEffects]").AddComponent<HitEffects>();
                instance.Build();
            }
            return instance;
        }
    }

    // Short bright streaks spraying out along direction, spread across spreadAngle degrees.
    public static void Sparks(Vector2 point, Vector2 direction, int count, float speed, float spreadAngle, Color colorA, Color colorB)
    {
        HitEffects effects = Instance;
        if (effects == null) return;

        var particle = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(-0.5f, 0.5f) * spreadAngle;
            particle.position = point;
            particle.velocity = Quaternion.Euler(0f, 0f, angle) * direction * (speed * Random.Range(0.4f, 1.2f));
            particle.startLifetime = Random.Range(0.12f, 0.3f);
            particle.startSize = Random.Range(0.04f, 0.08f);
            particle.startColor = Color.Lerp(colorA, colorB, Random.value);
            effects.sparks.Emit(particle, 1);
        }
    }

    // A ring that pops outward and fades where a hit landed.
    public static void Ring(Vector2 point, Color color, float size)
    {
        HitEffects effects = Instance;
        if (effects == null) return;

        var particle = new ParticleSystem.EmitParams
        {
            position = point,
            velocity = Vector3.zero,
            startLifetime = 0.18f,
            startSize = size,
            startColor = color
        };
        effects.rings.Emit(particle, 1);
    }

    // Something mechanical coming apart: a big ring, sparks in every direction, tumbling debris, and smoke.
    public static void Explosion(Vector2 point, Vector2 pushDirection, Color debrisColor, int debrisCount)
    {
        HitEffects effects = Instance;
        if (effects == null) return;

        Ring(point, new Color(1f, 0.8f, 0.45f), 2.2f);
        Sparks(point, Vector2.up, 24, 7f, 360f, new Color(1f, 0.9f, 0.5f), new Color(1f, 0.4f, 0.1f));

        Color darkDebris = new Color(debrisColor.r * 0.55f, debrisColor.g * 0.55f, debrisColor.b * 0.55f, debrisColor.a);
        var particle = new ParticleSystem.EmitParams();
        for (int i = 0; i < debrisCount; i++)
        {
            // Flung mostly the way the final hit pushed, with plenty of scatter.
            Vector2 direction = (pushDirection * 0.8f + Random.insideUnitCircle).normalized;
            particle.position = point + Random.insideUnitCircle * 0.2f;
            particle.velocity = direction * Random.Range(1.5f, 5f);
            particle.startLifetime = Random.Range(0.5f, 0.9f);
            particle.startSize = Random.Range(0.06f, 0.14f);
            particle.rotation = Random.Range(0f, 360f);
            particle.angularVelocity = Random.Range(-720f, 720f);
            particle.startColor = Color.Lerp(debrisColor, darkDebris, Random.value);
            effects.debris.Emit(particle, 1);
        }

        for (int i = 0; i < 6; i++)
        {
            particle.position = point + Random.insideUnitCircle * 0.3f;
            particle.velocity = Random.insideUnitCircle * 0.6f;
            particle.startLifetime = Random.Range(0.6f, 1f);
            particle.startSize = Random.Range(0.5f, 0.9f);
            particle.rotation = 0f;
            particle.angularVelocity = 0f;
            particle.startColor = new Color(0.25f, 0.25f, 0.28f, 0.6f);
            effects.smoke.Emit(particle, 1);
        }
    }

    // Something heavy landing: chips of it thrown out along the floor and a slow puff of dust. size 1 is about a tile.
    public static void Dust(Vector2 point, Color chipColor, float size)
    {
        HitEffects effects = Instance;
        if (effects == null) return;

        Color darkChips = new Color(chipColor.r * 0.55f, chipColor.g * 0.55f, chipColor.b * 0.55f, chipColor.a);
        var particle = new ParticleSystem.EmitParams();
        int chipCount = Mathf.Max(4, Mathf.RoundToInt(12 * size));
        for (int i = 0; i < chipCount; i++)
        {
            particle.position = point + Random.insideUnitCircle * 0.25f * size;
            particle.velocity = Random.insideUnitCircle.normalized * Random.Range(1f, 3.5f) * size;
            particle.startLifetime = Random.Range(0.35f, 0.7f);
            particle.startSize = Random.Range(0.05f, 0.12f) * Mathf.Sqrt(size);
            particle.rotation = Random.Range(0f, 360f);
            particle.angularVelocity = Random.Range(-540f, 540f);
            particle.startColor = Color.Lerp(chipColor, darkChips, Random.value);
            effects.debris.Emit(particle, 1);
        }

        particle.rotation = 0f;
        particle.angularVelocity = 0f;
        for (int i = 0; i < 6; i++)
        {
            particle.position = point + Random.insideUnitCircle * 0.4f * size;
            particle.velocity = Random.insideUnitCircle * 0.8f;
            particle.startLifetime = Random.Range(0.7f, 1.2f);
            particle.startSize = Random.Range(0.6f, 1.1f) * size;
            particle.startColor = new Color(0.42f, 0.41f, 0.4f, 0.45f);
            effects.smoke.Emit(particle, 1);
        }
    }

    void Build()
    {
        // Sparks stretch along their velocity so they read as streaks.
        sparks = CreateSystem("Sparks", CombatSprites.Square.texture, ParticleSystemRenderMode.Stretch,
            AnimationCurve.Linear(0f, 1f, 1f, 0.3f), 1f, drag: 4f);
        // Rings grow fast, then ease to full size.
        rings = CreateSystem("Rings", CombatSprites.RingTexture, ParticleSystemRenderMode.Billboard,
            new AnimationCurve(new Keyframe(0f, 0.2f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f)), 1f, drag: 0f);
        debris = CreateSystem("Debris", CombatSprites.Square.texture, ParticleSystemRenderMode.Billboard,
            AnimationCurve.Linear(0f, 1f, 1f, 0.6f), 1f, drag: 3f);
        // Smoke swells as it fades.
        smoke = CreateSystem("Smoke", CombatSprites.SoftCircleTexture, ParticleSystemRenderMode.Billboard,
            AnimationCurve.Linear(0f, 0.45f, 1f, 1f), 1.4f, drag: 2f);
    }

    ParticleSystem CreateSystem(string systemName, Texture texture, ParticleSystemRenderMode renderMode,
        AnimationCurve sizeOverLife, float sizeMultiplier, float drag)
    {
        var child = new GameObject(systemName);
        child.transform.SetParent(transform, false);

        var system = child.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Particles only come from Emit calls, so the system just needs to keep running.
        ParticleSystem.MainModule main = system.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.startLifetime = 1f;
        main.startSize = 1f;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1000;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        // Everything fades out over the second half of its life.
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = fade;

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(sizeMultiplier, sizeOverLife);

        if (drag > 0f)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule slowDown = system.limitVelocityOverLifetime;
            slowDown.enabled = true;
            slowDown.limit = 1000f; // no speed cap, just drag
            slowDown.drag = drag;
        }

        var particleRenderer = child.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = renderMode;
        if (renderMode == ParticleSystemRenderMode.Stretch)
        {
            particleRenderer.velocityScale = 0.04f;
            particleRenderer.lengthScale = 1f;
        }
        particleRenderer.sharedMaterial = new Material(CombatSprites.EffectMaterial) { mainTexture = texture };
        particleRenderer.sortingLayerName = SortingLayerName;
        particleRenderer.sortingOrder = SortingOrder;

        system.Play();
        return system;
    }
}
