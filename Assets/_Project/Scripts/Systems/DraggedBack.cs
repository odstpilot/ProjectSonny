using System.Collections;
using UnityEngine;

// player caught and dragged back
public static class DraggedBack
{
    const float DragSeconds = 2.4f;
    const int Pulls = 3;
    const float LyingAngle = 90f;
    const float BotGap = 0.9f;
    const float BotLookAhead = 6f;
    const string Look = "Hauler";

    public static bool Running { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Running = false;

    static AudioClip scrape, clank;

    public const float Seconds = DragSeconds + 4.5f;

    public static void Blackout() => ScreenFade.Get().SetAlpha(1f);

    public static IEnumerator Run(PlayerHealthHandler player, Vector3 to, Sprite[] botLook = null, bool held = false)
    {
        if (Running || player == null) yield break;
        Running = true;
        yield return Drag(player, to, botLook, held);
    }

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

        MakeSounds();
        float gap = DragSeconds / Pulls;
        for (int i = 0; i < Pulls; i++)
        {
            fade.PlaySound("Dragged Scrape", scrape);
            CameraShake.Shake(0.08f);
            for (float t = 0f; t < gap; t += Time.unscaledDeltaTime) yield return null;
        }
        fade.PlaySound("Dragged Clank", clank);

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

        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime) yield return null;
        yield return fade.FadeTo(0.55f, 0.35f);
        yield return fade.FadeTo(1f, 0.25f);
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) yield return null;
        if (bot != null) bot.GetComponent<BotLeaving>().Go(away);
        yield return fade.FadeTo(0f, 0.9f);

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
            if (Mathf.Abs(direction.x) > 0.5f) clear += 0.01f;
            if (clear > bestClear)
            {
                bestClear = clear;
                best = direction;
            }
        }
        return best;
    }

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
            low = Mathf.Lerp(low, (float)random.NextDouble() * 2f - 1f, 0.12f);
            data[i] = low * envelope * 0.9f;
        }
        AudioClip clip = AudioClip.Create("Dragged Scrape", samples, 1, TitleUI.SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

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
