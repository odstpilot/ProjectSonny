using System.Collections;
using UnityEngine;

// Being caught and hauled back, instead of the game just putting the player somewhere else. Sonny's orders are to send
// any human they find back, so that's what the bots do. In the dark (ScreenFade): the scrape of being dragged along the
// deck, a few heavy pulls, a clank. Then the technician's eyes open twice on the floor wherever they were dumped, and
// the bot that dragged them there is rolling away into the dark without looking back. They get up off the floor
// slowly, and only then is the game theirs again. They can't be hurt until it's over.
// PlayerHealthHandler runs it after Sonny's monitor, in place of cutting straight back to the checkpoint, and the
// weeping angel (WeepingAngel) runs it after its jumpscare, in place of throwing them back in a blink.
// The sounds are the scene's own when it has them (SoundManager: "Dragged Scrape", "Dragged Clank"); until then they're
// made in code. Unscaled time throughout. Only one at a time: Running.
public static class DraggedBack
{
    const float DragSeconds = 2.4f;         // in the dark, being dragged
    const int Pulls = 3;                    // heaves along the floor in that time
    const float LyingAngle = 90f;
    const float BotGap = 0.9f;              // how far from the player the bot is when they come to
    const float BotLookAhead = 6f;          // how far it looks for open floor to roll off into
    const string Look = "Hauler";           // its clamps are what drags them (Resources/Bots)

    public static bool Running { get; private set; }

    // Entering Play mode without reloading scripts keeps statics.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Running = false;

    static AudioClip scrape, clank;

    // How long the whole thing takes, from the dark to back on their feet, give or take: for anything that should
    // leave them alone meanwhile.
    public const float Seconds = DragSeconds + 4.5f;

    // Puts the screen to black now, if it isn't already: for the moment they're caught, before Run takes over.
    public static void Blackout() => ScreenFade.Get().SetAlpha(1f);

    // From black (it blacks the screen out first if it isn't): dragged, dumped at `to`, and back on their feet.
    // botLook: the frames of the bot that's walking away (two, to roll between), or empty for a turned hauler's.
    // held: the player's already frozen by whoever called this (dying does that), so it's left to them to let go.
    public static IEnumerator Run(PlayerHealthHandler player, Vector3 to, Sprite[] botLook = null, bool held = false)
    {
        if (Running || player == null) yield break;
        Running = true;
        yield return Drag(player, to, botLook, held);
    }

    // Caught by something that jumpscares them first (AngelJumpscare): held where they are, black once its face is
    // gone, then dragged. It runs on the player, so it finishes even if what caught them is switched off meanwhile.
    public static void AfterJumpscare(PlayerHealthHandler player, Vector3 to, Sprite[] botLook = null)
    {
        if (Running || player == null) return;
        Running = true;
        player.StartCoroutine(JumpscareThenDrag(player, to, botLook));
    }

    static IEnumerator JumpscareThenDrag(PlayerHealthHandler player, Vector3 to, Sprite[] botLook)
    {
        player.Hold(true);
        for (float t = 0f; t < AngelJumpscare.ToBlack && AngelJumpscare.Playing; t += Time.unscaledDeltaTime) yield return null;
        Blackout();
        while (AngelJumpscare.Playing) yield return null;
        yield return Drag(player, to, botLook, true);
        player.Hold(false);
    }

    static IEnumerator Drag(PlayerHealthHandler player, Vector3 to, Sprite[] botLook, bool held)
    {
        ScreenFade fade = ScreenFade.Get();
        fade.SetAlpha(1f);
        if (!held) player.Hold(true);
        if (player.TryGetComponent(out Health health)) health.MakeInvincible(Seconds + 1.5f);

        // Dragged, in the dark.
        MakeSounds();
        float gap = DragSeconds / Pulls;
        for (int i = 0; i < Pulls; i++)
        {
            fade.PlaySound("Dragged Scrape", scrape);
            CameraShake.Shake(0.08f);
            for (float t = 0f; t < gap; t += Time.unscaledDeltaTime) yield return null;
        }
        fade.PlaySound("Dragged Clank", clank);

        // Dumped there, on the floor, with the bot beside them.
        Transform body = player.transform;
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        body.position = to;
        if (rb != null)
        {
            rb.position = to;
            rb.linearVelocity = Vector2.zero;
        }
        body.rotation = Quaternion.Euler(0f, 0f, LyingAngle);
        Camera cam = Camera.main;
        if (cam != null && cam.TryGetComponent(out CameraFallow follow)) follow.SnapToPlayer();
        Vector2 away = OpenFloor(to, rb);
        var drawn = player.GetComponentInChildren<SpriteRenderer>();
        GameObject bot = SpawnBot((Vector2)to + away * BotGap, away, botLook, drawn != null ? drawn.sharedMaterial : null);

        // Eyes opening, twice: a glimpse, gone again, then for real, the bot already leaving.
        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime) yield return null;
        yield return fade.FadeTo(0.55f, 0.35f);
        yield return fade.FadeTo(1f, 0.25f);
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) yield return null;
        if (bot != null) bot.GetComponent<BotLeaving>().Go(away);
        yield return fade.FadeTo(0f, 0.9f);

        // A moment on the floor, then up, slowly.
        for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime) yield return null;
        for (float t = 0f; t < 1.1f; t += Time.unscaledDeltaTime)
        {
            body.rotation = Quaternion.Euler(0f, 0f, LyingAngle * (1f - Mathf.SmoothStep(0f, 1f, t / 1.1f)));
            yield return null;
        }
        body.rotation = Quaternion.identity;
        CameraShake.Shake(0.1f);

        if (!held) player.Hold(false);
        Running = false;
    }

    // The way with the most open floor from here, out of eight, for the bot to roll off along: so it doesn't drive
    // into a wall, and leaves through the room rather than past the player.
    static Vector2 OpenFloor(Vector2 from, Rigidbody2D ignore)
    {
        Vector2 best = Vector2.right;
        float bestClear = -1f;
        var hits = new RaycastHit2D[8];
        var filter = new ContactFilter2D { useTriggers = false };
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float clear = BotLookAhead;
            int count = Physics2D.Raycast(from, direction, filter, hits, BotLookAhead);
            for (int h = 0; h < count; h++)
            {
                if (ignore != null && hits[h].rigidbody == ignore) continue;
                clear = Mathf.Min(clear, hits[h].distance);
            }
            // Ties go to sideways, so it's seen leaving rather than heading straight up or down the screen.
            if (Mathf.Abs(direction.x) > 0.5f) clear += 0.01f;
            if (clear > bestClear)
            {
                bestClear = clear;
                best = direction;
            }
        }
        return best;
    }

    // material: the player's, so the station's lights fall on it the same.
    static GameObject SpawnBot(Vector2 at, Vector2 away, Sprite[] look, Material material)
    {
        if (look == null || look.Length == 0 || look[0] == null)
            look = new[] { Resources.Load<Sprite>($"Bots/Bot_{Look}_0"), Resources.Load<Sprite>($"Bots/Bot_{Look}_1") };
        if (look[0] == null) return null;

        var bot = new GameObject("Bot Leaving");
        bot.transform.position = new Vector3(at.x, at.y, 0f);
        DepthSort.Group(bot);
        var art = new GameObject("Body");
        art.transform.SetParent(bot.transform, false);
        var sprite = art.AddComponent<SpriteRenderer>();
        sprite.sprite = look[0];
        sprite.flipX = away.x < 0f;
        if (material != null) sprite.sharedMaterial = material;
        bot.AddComponent<BotLeaving>().frames = look;
        return bot;
    }

    static void MakeSounds()
    {
        if (scrape == null) scrape = Scrape();
        if (clank == null) clank = Clank();
    }

    // A body hauled a little way over metal plating: a rough, low scrape that swells and drags off.
    static AudioClip Scrape()
    {
        const float seconds = 0.7f;
        int samples = Mathf.CeilToInt(seconds * TitleUI.SampleRate);
        var data = new float[samples];
        var random = new System.Random(11);
        float low = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            float envelope = Mathf.Sin(Mathf.Pow(t, 0.6f) * Mathf.PI) * (0.6f + 0.4f * Mathf.Sin(t * 70f));
            low = Mathf.Lerp(low, (float)random.NextDouble() * 2f - 1f, 0.12f);  // noise, filtered down to a rumble
            data[i] = low * envelope * 0.9f;
        }
        AudioClip clip = AudioClip.Create("Dragged Scrape", samples, 1, TitleUI.SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Metal set down on metal: a dull knock and a short ring.
    static AudioClip Clank()
    {
        const float seconds = 0.6f;
        int samples = Mathf.CeilToInt(seconds * TitleUI.SampleRate);
        var data = new float[samples];
        var random = new System.Random(5);
        for (int i = 0; i < samples; i++)
        {
            float time = i / (float)TitleUI.SampleRate;
            float knock = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-time * 60f);
            float ring = (Mathf.Sin(2f * Mathf.PI * 220f * time) + 0.5f * Mathf.Sin(2f * Mathf.PI * 587f * time)) * Mathf.Exp(-time * 9f);
            data[i] = (knock * 0.7f + ring * 0.35f) * 0.8f;
        }
        AudioClip clip = AudioClip.Create("Dragged Clank", samples, 1, TitleUI.SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
