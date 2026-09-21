using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The one-off effects the tutorial's set pieces are made of, made from code so they need no assets: sounds synthesized
// on the spot, the smeared, dark picture of coming round, a wall being blown in, and the arrow that points the way.
// TutorialDirector decides when each happens.
public static class TutorialSetPieces
{
    const int SampleRate = 44100;
    static readonly Color WallGrey = new Color(0.42f, 0.44f, 0.48f);
    static readonly Color SparkBright = new Color(1f, 0.9f, 0.6f);
    static readonly Color SparkHot = new Color(1f, 0.5f, 0.15f);

    static readonly Color32 Ink = new Color32(18, 20, 26, 255);
    static readonly Color32 Void = new Color32(8, 8, 10, 255);
    static readonly Color32 Rib = new Color32(46, 50, 56, 255);
    static readonly Color32 Plating = new Color32(206, 210, 214, 255);
    static readonly Color32 Insulation = new Color32(208, 164, 66, 255);
    static readonly Color32 HotEdge = new Color32(255, 150, 60, 255);
    static readonly Color32 Arrow = new Color32(255, 196, 120, 255);

    // --- Sounds ---

    // A thin high whine with a slow beat in it, like ears ringing after a blast. Loops cleanly.
    public static AudioClip Ringing()
    {
        return Synthesize("Ringing", 2f, t =>
            0.5f * Mathf.Sin(2f * Mathf.PI * 3150f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 3152f * t));
    }

    // One deep tone that swells in and rings away for a few seconds, for the cut to black.
    public static AudioClip LowTone()
    {
        return Synthesize("Low Tone", 5f, t =>
        {
            float envelope = Mathf.Clamp01(t / 0.08f) * Mathf.Exp(-t / 1.6f);
            float tone = Mathf.Sin(2f * Mathf.PI * 41f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 82f * t) + 0.2f * Mathf.Sin(2f * Mathf.PI * 123.5f * t);
            return 0.55f * envelope * tone;
        });
    }

    static AudioClip Synthesize(string clipName, float seconds, System.Func<float, float> wave)
    {
        int count = Mathf.RoundToInt(seconds * SampleRate);
        var samples = new float[count];
        for (int i = 0; i < count; i++)
            samples[i] = Mathf.Clamp(wave((float)i / SampleRate), -1f, 1f);
        AudioClip clip = AudioClip.Create(clipName, count, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // A sound that plays on through AudioListener.volume, so it can be heard over a silence the level has made.
    public static AudioSource Speaker(GameObject host, AudioClip clip, bool loop)
    {
        AudioSource source = host.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerVolume = true;
        return source;
    }

    // --- Coming round ---

    // The picture as it looks to someone just waking: smeared at the edges, colour washed out, dark, and warped.
    // Fade it out by lowering the volume's weight, then destroy it.
    public static Volume WakeEffects()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.Add<ChromaticAberration>().intensity.Override(1f);
        profile.Add<LensDistortion>().intensity.Override(-0.4f);
        ColorAdjustments grade = profile.Add<ColorAdjustments>();
        grade.saturation.Override(-65f);
        grade.postExposure.Override(-0.7f);
        grade.contrast.Override(-20f);
        Vignette vignette = profile.Add<Vignette>();
        vignette.intensity.Override(0.6f);
        vignette.smoothness.Override(0.8f);
        vignette.color.Override(Color.black);
        Bloom bloom = profile.Add<Bloom>();
        bloom.threshold.Override(0.4f);
        bloom.intensity.Override(3f);
        bloom.scatter.Override(0.9f);

        var volume = new GameObject("Wake Effects").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 60f;      // over StationRumble's screen effects
        volume.weight = 1f;
        volume.sharedProfile = profile;
        return volume;
    }

    // --- The wall coming in ---

    // A blast through the wall at point: the plating tears open, chunks and sparks fly into the room, dust rolls out,
    // and wreckage is left on the floor in front of the hole.
    public static void BlowInWall(Vector2 point)
    {
        CameraShake.Kick(Vector2.down, 0.35f);
        CameraShake.Shake(0.9f);
        StationRumble.Punch(1f);
        StationRumble.PlayImpact(point);
        StationRumble.PlayImpact(point);

        HitEffects.Explosion(point, Vector2.down, WallGrey, 26);
        HitEffects.Dust(point + Vector2.down * 1.2f, WallGrey, 2.2f);
        HitEffects.Sparks(point, Vector2.down, 30, 7f, 150f, SparkBright, SparkHot);
        HitEffects.Ring(point, new Color(1f, 0.85f, 0.6f, 0.7f), 3f);

        var hole = new GameObject("Breach").AddComponent<SpriteRenderer>();
        hole.transform.position = point;
        hole.sprite = DrawBreach(new System.Random()).ToSprite(new Vector2(0.5f, 0.5f));
        hole.sortingLayerName = "Collision";
        hole.sortingOrder = 6;      // over the wall's tiles and the lamps on it

        // Wreckage thrown out onto the floor below.
        Vector2 floor = point + Vector2.down * 2.1f;
        for (int i = 0; i < 4; i++)
        {
            var debris = new GameObject("Breach Rubble").AddComponent<FallingDebris>();
            debris.transform.position = floor + new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(-1.2f, 0f));
            debris.startLanded = true;
            debris.rubbleLifetime = 0f;
            debris.size = Random.Range(0.7f, 1f);
        }
    }

    // A ragged hole blown through the wall, the station's layers showing at its edge: the outer plating peeled back in
    // petals, then gold insulation, then metal still glowing where it tore, and inside, the dark between the walls with a
    // rib of the frame and a cut cable hanging across it.
    static PixelCanvas DrawBreach(System.Random dice)
    {
        const int width = 72, height = 64;
        var canvas = new PixelCanvas(width, height);
        Vector2 middle = new Vector2(width * 0.5f, height * 0.5f);

        // How far the hole reaches at each angle round it: ragged, not round.
        var reach = new float[24];
        for (int i = 0; i < reach.Length; i++) reach[i] = 0.72f + 0.28f * (float)dice.NextDouble();
        System.Func<float, float> edgeAt = angle =>
        {
            float at = Mathf.Repeat(angle / (Mathf.PI * 2f), 1f) * reach.Length;
            int i = Mathf.FloorToInt(at);
            return Mathf.Lerp(reach[i % reach.Length], reach[(i + 1) % reach.Length], at - i);
        };

        // Petals of plating bent outward, between the hole and the wall.
        for (int i = 0; i < 7; i++)
        {
            float angle = (i + (float)dice.NextDouble() * 0.6f) / 7f * Mathf.PI * 2f;
            Vector2 way = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.85f);
            Vector2 side = new Vector2(-way.y, way.x);
            Vector2 root = middle + Vector2.Scale(way, new Vector2(22f, 20f));
            float length = 8f + (float)dice.NextDouble() * 7f, spread = 4f + (float)dice.NextDouble() * 3f;
            canvas.FillPolygon(new[] { root - side * spread, root + side * spread, root + way * length }, (x, y) => PixelCanvas.Scale(Plating, 0.8f + 0.2f * y / height));
        }
        canvas.Bevel(1.15f, 0.7f);
        canvas.Outline(Ink);

        // The hole itself, layer by layer from the edge in.
        var dark = new bool[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 offset = new Vector2(x + 0.5f, y + 0.5f) - middle;
                float angle = Mathf.Atan2(offset.y / 0.85f, offset.x);
                float distance = new Vector2(offset.x, offset.y / 0.85f).magnitude / (26f * edgeAt(angle));
                if (distance > 1f) continue;
                if (distance > 0.93f) canvas.Set(x, y, Plating);
                else if (distance > 0.86f) canvas.Set(x, y, PixelCanvas.Scale(Insulation, 0.85f + 0.3f * (float)dice.NextDouble()));
                else if (distance > 0.8f) canvas.Set(x, y, PixelCanvas.Mix(HotEdge, Void, (0.86f - distance) / 0.06f * 0.5f));
                else
                {
                    canvas.Set(x, y, PixelCanvas.Mix(Void, Rib, Mathf.Clamp01((distance - 0.55f) * 0.6f)));
                    dark[y * width + x] = true;
                }
            }
        }

        // A rib of the frame across the dark, and a cut cable hanging in front of it.
        int ribX = Mathf.RoundToInt(middle.x + (float)dice.NextDouble() * 12f - 6f);
        for (int y = 0; y < height; y++)
            for (int x = ribX; x < ribX + 4; x++)
                if (dark[y * width + x]) canvas.Set(x, y, x == ribX ? PixelCanvas.Scale(Rib, 1.4f) : Rib);
        float sag = 6f + (float)dice.NextDouble() * 6f;
        for (int x = 18; x < width - 18; x++)
        {
            int y = Mathf.RoundToInt(middle.y + 10f - sag * Mathf.Sin((x - 18f) / (width - 36f) * Mathf.PI));
            if (canvas.Inside(x, y) && dark[y * width + x]) canvas.Set(x, y, new Color32(186, 54, 44, 255));
            if (canvas.Inside(x, y - 1) && dark[(y - 1) * width + x]) canvas.Set(x, y - 1, new Color32(120, 34, 28, 255));
        }
        return canvas;
    }

    // --- The way on ---

    public static SpriteRenderer GuideArrow()
    {
        // A chevron pointing right, at the tileset's pixel size.
        var chevron = new PixelCanvas(12, 16);
        chevron.Line(2, 14, 8, 8, Arrow, 3);
        chevron.Line(8, 8, 2, 2, Arrow, 3);

        var arrow = new GameObject("Guide Arrow").AddComponent<SpriteRenderer>();
        arrow.sprite = chevron.ToSprite(new Vector2(0.5f, 0.5f));
        arrow.sortingLayerName = "Top";
        arrow.sortingOrder = 30;
        if (CombatSprites.EffectMaterial != null) arrow.sharedMaterial = CombatSprites.EffectMaterial;
        arrow.enabled = false;
        return arrow;
    }
}
