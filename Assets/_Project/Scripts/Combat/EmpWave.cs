using System.Collections;
using UnityEngine;

// What an EMP pulse looks like: bands of crackling electricity that sweep out across a cone, with forks of lightning
// flickering inside it. Only a visual; CameraFlash decides what gets stunned. EmpWave.Spawn makes one that plays once
// and removes itself.
public class EmpWave : MonoBehaviour
{
    // Drawn over the characters, just under the spark particles.
    const string SortingLayerName = "Player";
    const int SortingOrder = 45;

    const int ArcPoints = 32;
    const int BoltPoints = 8;
    const int BoltCount = 5;
    const float StartRadius = 0.25f;
    const float WaveSpacing = 0.08f;    // seconds between bands
    const float BoltBurst = 0.2f;       // seconds lightning flickers after the pulse
    const float BoltLife = 0.045f;      // seconds a fork of lightning lasts before it jumps somewhere new
    const float GlowWidth = 0.45f;
    const float CoreWidth = 0.07f;

    static Material glowMaterial;
    static Material coreMaterial;
    static AnimationCurve bandWidth;
    static AnimationCurve boltWidth;

    private Vector2 origin;
    private float baseAngle;            // degrees
    private float halfAngle;
    private float radius;
    private Color color;
    private Color coreColor;
    private float waveDuration;
    private LineRenderer[] glows;
    private LineRenderer[] cores;
    private LineRenderer[] bolts;
    private readonly Vector3[] arcPositions = new Vector3[ArcPoints];
    private readonly Vector3[] boltPositions = new Vector3[BoltPoints];

    // direction is the middle of the cone; halfAngle is in degrees either side of it.
    public static EmpWave Spawn(Vector2 origin, Vector2 direction, float halfAngle, float radius,
        Color color, Color coreColor, int waveCount, float waveDuration)
    {
        if (CombatSprites.EffectMaterial == null) return null;

        var wave = new GameObject("[EmpWave]").AddComponent<EmpWave>();
        wave.origin = origin;
        wave.baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        wave.halfAngle = halfAngle;
        wave.radius = Mathf.Max(StartRadius, radius);
        wave.color = color;
        wave.coreColor = coreColor;
        wave.waveDuration = Mathf.Max(0.05f, waveDuration);
        wave.Build(Mathf.Max(1, waveCount));
        return wave;
    }

    // Seconds after the pulse until the first band reaches distance, so whatever it hits can react as it arrives.
    public static float ArrivalTime(float distance, float radius, float waveDuration)
    {
        float travelled = Mathf.InverseLerp(StartRadius, radius, distance);
        return (1f - Mathf.Pow(1f - travelled, 1f / 3f)) * waveDuration;
    }

    void Build(int waveCount)
    {
        if (glowMaterial == null)
            glowMaterial = new Material(CombatSprites.EffectMaterial) { mainTexture = MakeBandTexture(2f) };
        if (coreMaterial == null)
            coreMaterial = new Material(CombatSprites.EffectMaterial) { mainTexture = MakeBandTexture(0.6f) };
        // Bands are thickest in the middle of the cone and thin to nothing at its edges.
        if (bandWidth == null)
            bandWidth = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 0.8f), new Keyframe(0.5f, 1f),
                new Keyframe(0.8f, 0.8f), new Keyframe(1f, 0f));
        if (boltWidth == null)
            boltWidth = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);

        glows = new LineRenderer[waveCount];
        cores = new LineRenderer[waveCount];
        for (int i = 0; i < waveCount; i++)
        {
            glows[i] = NewLine("Glow", ArcPoints, glowMaterial, bandWidth, SortingOrder);
            cores[i] = NewLine("Core", ArcPoints, coreMaterial, bandWidth, SortingOrder + 1);
        }

        bolts = new LineRenderer[BoltCount];
        for (int i = 0; i < BoltCount; i++)
            bolts[i] = NewLine("Bolt", BoltPoints, coreMaterial, boltWidth, SortingOrder + 1);

        StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        float lastBandDone = (glows.Length - 1) * WaveSpacing + waveDuration;
        float nextBolts = 0f;

        for (float elapsed = 0f; elapsed < lastBandDone; elapsed += Time.deltaTime)
        {
            for (int i = 0; i < glows.Length; i++)
                DrawBand(i, (elapsed - i * WaveSpacing) / waveDuration);

            if (elapsed >= BoltBurst)
            {
                foreach (LineRenderer bolt in bolts) bolt.enabled = false;
            }
            else if (elapsed >= nextBolts)
            {
                JumpBolts(elapsed);
                nextBolts = elapsed + BoltLife;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    // t goes 0 to 1 as the band travels from the device out to the edge of the radius.
    void DrawBand(int index, float t)
    {
        LineRenderer glow = glows[index];
        LineRenderer core = cores[index];
        bool visible = t > 0f && t < 1f;
        glow.enabled = visible;
        core.enabled = visible;
        if (!visible) return;

        float spread = EaseOutCubic(t);
        float bandRadius = Mathf.Lerp(StartRadius, radius, spread);
        // Every band fades as it spreads, and each one after the first is a little weaker.
        float strength = (1f - t * t) * (1f - 0.2f * index);
        float crackle = Mathf.Lerp(0.04f, 0.18f, spread);
        float scroll = Time.time * 30f + index * 17.3f;

        for (int p = 0; p < ArcPoints; p++)
        {
            float across = p / (ArcPoints - 1f);
            float angle = (baseAngle + Mathf.Lerp(-halfAngle, halfAngle, across)) * Mathf.Deg2Rad;
            // Rough noise makes the band crackle; a fine ripple on top makes it read as a wave.
            float offset = (Mathf.PerlinNoise(across * 7f, scroll) * 2f - 1f) * crackle
                         + Mathf.Sin(across * 45f - scroll) * 0.025f;
            float r = bandRadius + offset;
            arcPositions[p] = new Vector3(origin.x + Mathf.Cos(angle) * r, origin.y + Mathf.Sin(angle) * r, 0f);
        }

        glow.SetPositions(arcPositions);
        core.SetPositions(arcPositions);
        float thickness = Mathf.Lerp(1f, 0.6f, t);
        glow.widthMultiplier = GlowWidth * thickness;
        core.widthMultiplier = CoreWidth * thickness;
        glow.startColor = glow.endColor = WithAlpha(color, 0.55f * strength);
        core.startColor = core.endColor = WithAlpha(coreColor, strength);
    }

    // Forks of lightning from the device to random spots in the cone, reaching as far as the first band has gone.
    void JumpBolts(float elapsed)
    {
        float reach = Mathf.Lerp(StartRadius, radius, EaseOutCubic(Mathf.Clamp01(elapsed / waveDuration)));
        float fade = 1f - elapsed / BoltBurst;

        foreach (LineRenderer bolt in bolts)
        {
            // A few skip a beat each time, so the lightning flickers.
            bolt.enabled = Random.value < 0.8f;
            if (!bolt.enabled) continue;

            float angle = (baseAngle + Random.Range(-halfAngle, halfAngle) * 0.85f) * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 side = new Vector2(-forward.y, forward.x);
            float length = reach * Random.Range(0.35f, 0.8f);
            float jaggedness = Mathf.Min(0.25f, length * 0.08f);

            for (int p = 0; p < BoltPoints; p++)
            {
                float along = p / (BoltPoints - 1f);
                float jag = p == 0 ? 0f : Random.Range(-1f, 1f) * jaggedness;
                Vector2 point = origin + forward * (StartRadius * 0.5f + length * along) + side * jag;
                boltPositions[p] = point;
            }

            bolt.SetPositions(boltPositions);
            bolt.widthMultiplier = Random.Range(0.025f, 0.05f);
            bolt.startColor = WithAlpha(coreColor, fade);
            bolt.endColor = WithAlpha(color, 0.3f * fade);
        }
    }

    LineRenderer NewLine(string lineName, int pointCount, Material material, AnimationCurve width, int order)
    {
        var line = new GameObject(lineName).AddComponent<LineRenderer>();
        line.transform.SetParent(transform, false);
        line.useWorldSpace = true;
        line.positionCount = pointCount;
        line.widthCurve = width;
        line.textureMode = LineTextureMode.Stretch;
        line.sharedMaterial = material;
        line.sortingLayerName = SortingLayerName;
        line.sortingOrder = order;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    // White, see-through at the top and bottom edges and solid through the middle, so lines have soft sides.
    // Lower sharpness gives a harder edge.
    static Texture2D MakeBandTexture(float sharpness)
    {
        const int height = 32;
        var texture = new Texture2D(1, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < height; y++)
        {
            float fromMiddle = Mathf.Abs(y / (height - 1f) * 2f - 1f);
            texture.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Pow(1f - fromMiddle, sharpness) * 1.5f)));
        }
        texture.Apply();
        return texture;
    }

    static float EaseOutCubic(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    static Color WithAlpha(Color c, float alpha)
    {
        return new Color(c.r, c.g, c.b, alpha);
    }
}
