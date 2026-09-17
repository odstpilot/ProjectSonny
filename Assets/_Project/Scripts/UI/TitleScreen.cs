using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The title screen: a view out into space, painted and animated in code. Nebula drifts, stars twinkle, meteors streak
// past, a planet turns, asteroids tumble, a station leans out of a rock, and nearer things shift more as the mouse moves.
// It boots like a monitor: the picture opens out of a bright line and a scan beam sweeps the view in, PROJECT decodes,
// SONNY flickers on letter by letter like a neon tube, and the loading bar fills. Then it waits for any button, which
// opens the menu row (START, SETTINGS, QUIT). START switches the screen off like an old monitor and loads the next scene.
// A button during the boot skips it.
// Everything is built when the scene starts (TitleScreen.Build.cs, with the art from TitleArt), so the scene holds only
// this and a camera.
public partial class TitleScreen : MonoBehaviour
{
    [Tooltip("Loaded by START. It has to be in the build settings.")]
    public string nextScene = "Tutorial";
    [Tooltip("SONNY, the prompt, the menu, and settings.")]
    public Font font;
    [Tooltip("PROJECT. Uses the font above if left empty.")]
    public Font headingFont;
    [Tooltip("Loops quietly under the title.")]
    public AudioClip ambience;
    [Tooltip("Played when START is chosen.")]
    public AudioClip confirmSound;

    const float AmbienceVolume = 0.35f;
    const string PromptText = "PRESS ANY BUTTON TO BEGIN";

    // The boot, in seconds.
    const float LineOpenEnd = 0.35f;
    const float RevealStart = 0.25f, RevealEnd = 1.25f;
    const float ProjectStart = 1.1f, ProjectEnd = 1.7f;
    const float SonnyStart = 1.5f, SonnyEnd = 2.4f;
    const float BarStart = 2.3f, BarEnd = 3.9f;
    const float PromptStart = 4.1f, IntroEnd = 4.7f;
    // Switching off, in seconds from the choice.
    const float CollapseStart = 0.55f, CollapseEnd = 0.8f, ShrinkEnd = 0.95f, LeaveEnd = 1.35f;

    static readonly Color FrameCyan = new Color(0.25f, 0.95f, 0.95f);
    static readonly Color HudTeal = new Color(0.22f, 0.62f, 0.6f);
    static readonly Color SonnyColor = new Color(0.9f, 1f, 1f);
    static readonly Color SonnyGlow = new Color(0.3f, 0.95f, 1f, 0.75f);
    static readonly Color ProjectColor = new Color(0.86f, 0.91f, 0.93f);
    static readonly Color PromptColor = new Color(0.72f, 0.96f, 0.95f);
    static readonly Color BootColor = new Color(0.45f, 0.72f, 0.72f);
    static readonly Color SegmentLit = new Color(0.16f, 0.6f, 0.56f);
    static readonly Color SegmentUnlit = new Color(0.5f, 0.55f, 0.56f, 0.75f);
    static readonly Color SegmentBright = new Color(0.6f, 1f, 0.95f);
    static readonly Color EngineColor = new Color(0.5f, 0.95f, 1f);
    static readonly Color ScanColor = new Color(0.55f, 1f, 1f);

    enum State { Booting, Waiting, Menu, Leaving }

    State state = State.Booting;
    float introTime, leaveTime, startTime, waitingSince;
    float nextGlitch, glitchEnd, nextMeteors, nextSheen, nextDividerShuffle;
    float projectSheenAt = -10f, sonnySheenAt = -10f;
    int lastBlipSegment;
    Vector2 parallax;

    TMP_FontAsset fontAsset, headingAsset;
    RectTransform monitor, sky, shade;
    RawImage scanBeam;
    readonly List<Layer> layers = new List<Layer>();
    readonly List<Rock> rocks = new List<Rock>();
    readonly List<Twinkle> twinkles = new List<Twinkle>();
    readonly List<Meteor> meteors = new List<Meteor>();
    readonly List<Mote> motes = new List<Mote>();
    readonly List<Beacon> beacons = new List<Beacon>();
    RawImage nebulaBack, nebulaFront, nebulaLight, atmosphere;
    SpinningPlanet planet;
    RectTransform station, ship;
    RawImage engineGlow;

    RawImage sonnyHalo, divider;
    TextMeshProUGUI project, sonny, ghostRed, ghostCyan, prompt;
    Material sonnyMaterial;
    Texture2D dividerTexture;
    Color32[] dividerPixels;
    CanvasGroup bar;
    HudShape[] segments;

    RawImage scanlines, grain, staticBurst, flash, lastLight;
    AudioSource ambienceSource, sfx;
    AudioClip blipClip, staticClip;
    TitleMenu menu;

    void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Build();
    }

    void Start()
    {
        startTime = Time.unscaledTime;
        nextGlitch = startTime + IntroEnd + Random.Range(3f, 6f);
        nextMeteors = startTime + 1.6f;
        nextSheen = startTime + IntroEnd + 2f;
    }

    void Update()
    {
        float now = Time.unscaledTime;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // so a slow first frame doesn't skip half the boot

        switch (state)
        {
            case State.Booting:
                introTime += dt;
                if (Input.anyKeyDown) introTime = Mathf.Max(introTime, IntroEnd);
                if (introTime >= IntroEnd) EnterWaiting(now, true);
                break;
            case State.Waiting:
                menu.Tick(now, dt, false);     // so the row finishes fading out after BACK
                if (Input.anyKeyDown && now - waitingSince > 0.15f)
                {
                    state = State.Menu;
                    menu.Open(now);
                    sfx.PlayOneShot(blipClip, 0.35f);
                    glitchEnd = now + 0.12f;
                }
                break;
            case State.Menu:
                TitleChoice choice = menu.Tick(now, dt);
                if (choice == TitleChoice.Start) StartCoroutine(Leave(false));
                else if (choice == TitleChoice.Quit) StartCoroutine(Leave(true));
                else if (choice == TitleChoice.Back)
                {
                    menu.Close(now);
                    EnterWaiting(now, false);
                }
                break;
            case State.Leaving:
                menu.Tick(now, dt, false);
                break;
        }

        UpdateParallax(now, dt);
        AnimateBoot();
        AnimateSky(now, dt);
        AnimateTitle(now);
        AnimateBar(now);
        AnimatePrompt(now);
        AnimateScreen(now);
        if (state == State.Leaving) AnimateLeaving();
        AnimateSound(now);
    }

    void EnterWaiting(float now, bool fromBoot)
    {
        state = State.Waiting;
        waitingSince = fromBoot ? now - 1f : now;   // from the boot, the prompt has already decoded
    }

    IEnumerator Leave(bool quit)
    {
        state = State.Leaving;
        leaveTime = 0f;

        AsyncOperation loading = null;
        if (!quit)
        {
            sfx.PlayOneShot(confirmSound != null ? confirmSound : blipClip, 0.8f);
            if (Application.CanStreamedLevelBeLoaded(nextScene))
            {
                loading = SceneManager.LoadSceneAsync(nextScene);
                loading.allowSceneActivation = false;
            }
            else
            {
                Debug.LogWarning($"TitleScreen: there's no scene called \"{nextScene}\" in the build settings to load.", this);
            }
        }

        bool crackled = false;
        while (leaveTime < LeaveEnd)
        {
            yield return null;
            leaveTime += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (!crackled && leaveTime >= CollapseStart)
            {
                sfx.PlayOneShot(staticClip, 0.25f);
                crackled = true;
            }
        }

        if (quit)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            yield break;
        }
        if (loading != null)
        {
            loading.allowSceneActivation = true;
            yield break;
        }

        // Nowhere to go, so switch back on and wait in the menu again.
        lastLight.color = Color.clear;
        state = State.Menu;
    }

    // --- Animation ---

    void UpdateParallax(float now, float dt)
    {
        Vector2 target = new Vector2(Mathf.Sin(now * 0.13f), Mathf.Cos(now * 0.09f)) * 0.3f;
        if (Input.mousePresent && Screen.width > 0 && Screen.height > 0)
        {
            Vector3 mouse = Input.mousePosition;
            target += new Vector2(Mathf.Clamp(mouse.x / Screen.width * 2f - 1f, -1f, 1f),
                                  Mathf.Clamp(mouse.y / Screen.height * 2f - 1f, -1f, 1f)) * 0.7f;
        }
        parallax = Vector2.Lerp(parallax, target, 1f - Mathf.Exp(-dt * 2f));
    }

    // The monitor opening, the scan beam sweeping the view in, and each layer of space fading and sliding into place.
    void AnimateBoot()
    {
        float t = introTime;
        float across = TitleUI.EaseOut(TitleUI.Phase(t, 0f, 0.12f));
        float down = TitleUI.EaseOut(TitleUI.Phase(t, 0.1f, LineOpenEnd));
        monitor.localScale = new Vector3(Mathf.Lerp(0.03f, 1f, across), Mathf.Lerp(0.004f, 1f, down), 1f);

        float reveal = TitleUI.EaseInOut(TitleUI.Phase(t, RevealStart, RevealEnd));
        float shadeHeight = 1300f * (1f - reveal);
        shade.sizeDelta = new Vector2(shade.sizeDelta.x, shadeHeight);
        scanBeam.rectTransform.anchoredPosition = new Vector2(0f, -650f + shadeHeight);
        scanBeam.color = TitleUI.Fade(ScanColor, reveal > 0f && reveal < 1f ? 0.9f : 0f);

        foreach (Layer layer in layers)
        {
            float shown = TitleUI.EaseOut(TitleUI.Phase(t, layer.fadeStart, layer.fadeEnd));
            layer.group.alpha = shown;
            layer.rect.anchoredPosition = layer.slideFrom * (1f - shown) - parallax * layer.depth;
        }
    }

    void AnimateSky(float now, float dt)
    {
        nebulaBack.uvRect = new Rect(0.06f + 0.04f * Mathf.Sin(now * 0.021f), 0.06f + 0.04f * Mathf.Cos(now * 0.017f), 0.88f, 0.88f);
        nebulaFront.uvRect = new Rect(0.06f + 0.05f * Mathf.Cos(now * 0.029f + 1f), 0.06f + 0.04f * Mathf.Sin(now * 0.024f), 0.88f, 0.88f);
        nebulaFront.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(now * 0.4f));
        nebulaLight.color = new Color(0.2f, 0.6f, 0.65f, 0.1f + 0.04f * Mathf.Sin(now * 0.3f));

        planet.Turn(dt);
        atmosphere.color = new Color(0.35f, 0.6f, 1f, 0.32f + 0.1f * Mathf.Sin(now * 0.6f));

        foreach (Twinkle star in twinkles)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(now * star.speed + star.phase);
            wave = wave * wave * wave;
            star.image.color = TitleUI.Fade(star.color, star.color.a * (0.15f + 0.85f * wave));
            float size = star.size * (0.6f + 0.6f * wave);
            star.image.rectTransform.sizeDelta = new Vector2(size, size);
        }

        foreach (Mote mote in motes)
        {
            mote.position += mote.velocity * dt;
            if (mote.position.x > 1000f) mote.position.x -= 2000f;
            if (mote.position.x < -1000f) mote.position.x += 2000f;
            if (mote.position.y > 600f) mote.position.y -= 1200f;
            if (mote.position.y < -600f) mote.position.y += 1200f;
            mote.image.rectTransform.anchoredPosition = mote.position + new Vector2(Mathf.Sin(now * 0.5f + mote.phase) * 6f, 0f);
            mote.image.color = new Color(1f, 1f, 1f, mote.alpha * (0.6f + 0.4f * Mathf.Sin(now * 0.8f + mote.phase)));
        }

        foreach (Rock rock in rocks)
        {
            rock.angle += rock.spin * dt;
            rock.rect.localRotation = Quaternion.Euler(0f, 0f, rock.angle);
            rock.rect.anchoredPosition = rock.home + new Vector2(0f, Mathf.Sin(now * rock.bobSpeed + rock.phase) * rock.bob);
        }

        station.localRotation = Quaternion.Euler(0f, 0f, StationLean + 1.2f * Mathf.Sin(now * 0.35f));
        foreach (Beacon beacon in beacons)
        {
            float cycle = Mathf.Repeat(now + beacon.phase, beacon.period) / beacon.period;
            beacon.image.color = TitleUI.Fade(beacon.color, cycle < beacon.duty ? 1f : 0.08f);
        }

        float shipTime = Mathf.Repeat(now - startTime + ShipHeadStart, ShipPeriod);
        ship.anchoredPosition = Vector2.LerpUnclamped(ShipFrom, ShipTo, shipTime / ShipCrossing);
        engineGlow.color = TitleUI.Fade(EngineColor, 0.35f + 0.55f * Mathf.PerlinNoise(now * 12f, 0.5f));

        if (now >= nextMeteors)
        {
            SpawnMeteors();
            nextMeteors = now + Random.Range(2.5f, 5.5f);
        }
        foreach (Meteor meteor in meteors)
        {
            if (!meteor.active) continue;
            meteor.age += dt;
            if (meteor.age >= meteor.life)
            {
                meteor.active = false;
                meteor.group.alpha = 0f;
                continue;
            }
            if (meteor.age < 0f) continue;
            meteor.position += meteor.velocity * dt;
            meteor.rect.anchoredPosition = meteor.position;
            meteor.group.alpha = TitleUI.Phase(meteor.age, 0f, 0.12f) * (1f - TitleUI.Phase(meteor.age, meteor.life - 0.35f, meteor.life));
        }
    }

    // A few meteors close together, high on the left, streaking down to the right.
    void SpawnMeteors()
    {
        int count = Random.Range(1, 4);
        Vector2 origin = new Vector2(Random.Range(-900f, -350f), Random.Range(330f, 540f));
        float delay = 0f;
        foreach (Meteor meteor in meteors)
        {
            if (count == 0) break;
            if (meteor.active) continue;
            count--;
            meteor.active = true;
            meteor.age = -delay;
            delay += Random.Range(0.08f, 0.25f);
            meteor.life = Random.Range(0.7f, 1.1f);
            meteor.position = origin + new Vector2(Random.Range(-90f, 90f), Random.Range(-60f, 60f));
            meteor.velocity = MeteorDirection * Random.Range(520f, 820f);
            meteor.rect.sizeDelta = new Vector2(Random.Range(140f, 280f), Random.Range(14f, 22f));
            meteor.rect.anchoredPosition = meteor.position;
            meteor.group.alpha = 0f;
        }
    }

    void AnimateTitle(float now)
    {
        float t = introTime;

        // PROJECT decodes, and the dotted line under it spreads out from the middle.
        project.text = TitleUI.Decode("PROJECT", TitleUI.Phase(t, ProjectStart, ProjectEnd), now);
        TintLetters(project, (i, x) => Color.Lerp(ProjectColor, Color.white, Sheen(project, x, projectSheenAt, now)));

        float opened = TitleUI.EaseOut(TitleUI.Phase(t, ProjectStart + 0.1f, ProjectEnd + 0.2f));
        divider.rectTransform.sizeDelta = new Vector2(DividerWidth * opened, divider.rectTransform.sizeDelta.y);
        divider.uvRect = new Rect(0.5f - opened * 0.5f, 0f, opened, 1f);
        if (opened > 0f && now >= nextDividerShuffle)
        {
            ShuffleDivider(now);
            nextDividerShuffle = now + 0.08f;
        }

        bool idle = state == State.Waiting || state == State.Menu;
        if (idle && now >= nextSheen)
        {
            projectSheenAt = now;
            sonnySheenAt = now + 0.35f;
            nextSheen = now + Random.Range(7f, 11f);
        }
        if (idle && now >= nextGlitch)
        {
            glitchEnd = now + Random.Range(0.15f, 0.3f);
            nextGlitch = now + Random.Range(4f, 9f);
            sfx.PlayOneShot(staticClip, 0.07f);
        }
        bool glitching = now < glitchEnd || (state == State.Leaving && leaveTime < CollapseStart);

        // Each letter of SONNY catches on its own like a failing tube, the first ones first.
        float on = TitleUI.Phase(t, SonnyStart, SonnyEnd);
        int tick = Mathf.FloorToInt(t * 22f);
        TintLetters(sonny, (i, x) =>
        {
            float letter = TitleUI.Phase(on, i * 0.08f, i * 0.08f + 0.6f);
            float alpha = letter >= 1f ? 1f : letter <= 0f ? 0f : TitleUI.Hash(tick * 5 + i * 17) < letter * 1.2f - 0.1f ? 1f : 0.06f;
            if (glitching && Random.value < 0.15f) alpha *= 0.3f;
            return TitleUI.Fade(Color.Lerp(SonnyColor, Color.white, Sheen(sonny, x, sonnySheenAt, now)), alpha);
        });

        float breathe = 0.5f + 0.5f * Mathf.Sin(now * 1.4f);
        float surge = on > 0f && on < 1f ? TitleUI.Hash(tick) * 0.5f : 0f;
        sonnyMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, TitleUI.Fade(SonnyGlow, SonnyGlow.a * (0.7f + 0.3f * breathe + surge)));
        sonnyHalo.color = new Color(0.3f, 0.9f, 1f, (0.08f + 0.06f * breathe) * on);

        // A glitch: the letters jump and split into red and cyan.
        if (glitching)
        {
            float jump = Random.Range(-8f, 8f);
            sonny.rectTransform.anchoredPosition = SonnyHome + new Vector2(jump, 0f);
            ghostRed.rectTransform.anchoredPosition = SonnyHome + new Vector2(jump - Random.Range(6f, 16f), Random.Range(-3f, 3f));
            ghostCyan.rectTransform.anchoredPosition = SonnyHome + new Vector2(jump + Random.Range(6f, 16f), Random.Range(-3f, 3f));
            ghostRed.alpha = Random.Range(0.35f, 0.7f);
            ghostCyan.alpha = Random.Range(0.35f, 0.7f);
        }
        else
        {
            sonny.rectTransform.anchoredPosition = SonnyHome;
            ghostRed.alpha = 0f;
            ghostCyan.alpha = 0f;
        }
    }

    // Colors each letter of a label separately. tint gets the letter's index and the middle of it across the label.
    static void TintLetters(TMP_Text label, System.Func<int, float, Color> tint)
    {
        label.ForceMeshUpdate();
        TMP_TextInfo info = label.textInfo;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo letter = info.characterInfo[i];
            if (!letter.isVisible) continue;
            Color32 color = tint(i, (letter.bottomLeft.x + letter.topRight.x) * 0.5f);
            Color32[] colors = info.meshInfo[letter.materialReferenceIndex].colors32;
            for (int v = 0; v < 4; v++) colors[letter.vertexIndex + v] = color;
        }
        label.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    // How much a sheen sweeping across a label, started at startedAt, lights a letter whose middle is at x.
    static float Sheen(TMP_Text label, float x, float startedAt, float now)
    {
        float sweep = TitleUI.Phase(now, startedAt, startedAt + 0.9f);
        if (sweep <= 0f || sweep >= 1f) return 0f;
        float half = label.textBounds.extents.x + 120f;
        float position = label.textBounds.center.x + Mathf.Lerp(-half, half, TitleUI.EaseInOut(sweep));
        float distance = (x - position) / 70f;
        return Mathf.Exp(-distance * distance);
    }

    // Two rows of dashes that crawl and flicker like data.
    void ShuffleDivider(float now)
    {
        float drift = now * 1.5f;
        for (int x = 0; x < DividerLength; x++)
        {
            bool top = Mathf.PerlinNoise(x * 0.3f + drift, 3.7f) > 0.48f;
            bool bottom = Mathf.PerlinNoise(x * 0.45f - drift * 0.6f + 50f, 11.2f) > 0.55f;
            if (Random.value < 0.03f) top = !top;
            dividerPixels[2 * DividerLength + x] = top ? new Color32(255, 255, 255, 255) : default;
            dividerPixels[x] = bottom ? new Color32(255, 255, 255, 180) : default;
        }
        dividerTexture.SetPixels32(dividerPixels);
        dividerTexture.Apply(false);
    }

    // Fills in fits and starts, flashes when it's full, then a highlight runs along it now and then.
    void AnimateBar(float now)
    {
        float t = introTime;
        bar.alpha = TitleUI.Phase(t, BarStart - 0.3f, BarStart);

        float fill = TitleUI.Stutter(TitleUI.Phase(t, BarStart, BarEnd)) * SegmentCount;
        int full = Mathf.FloorToInt(fill);
        float complete = TitleUI.Phase(t, BarEnd, BarEnd + 0.5f);
        float whiteness = complete > 0f && complete < 1f ? 1f - complete : 0f;
        if (state == State.Leaving) whiteness = Mathf.Repeat(leaveTime * 14f, 1f) < 0.5f ? 1f : 0.3f;
        float sweep = Mathf.Repeat((now - startTime) * 0.7f, 2f) * SegmentCount - SegmentCount * 0.5f;

        for (int i = 0; i < SegmentCount; i++)
        {
            Color color = i < full ? SegmentLit : i == full ? Color.Lerp(SegmentUnlit, SegmentBright, fill - full) : SegmentUnlit;
            if (t >= BarEnd)
            {
                float distance = i - sweep;
                color = Color.Lerp(color, SegmentBright, 0.75f * Mathf.Exp(-distance * distance / 6f));
            }
            segments[i].color = Color.Lerp(color, Color.white, whiteness);
        }

        if (state == State.Booting && full > lastBlipSegment)
        {
            if (full % 3 == 0) sfx.PlayOneShot(blipClip, 0.12f);
            lastBlipSegment = full;
        }
    }

    void AnimatePrompt(float now)
    {
        float t = introTime;
        if (state == State.Menu || state == State.Leaving || t < BarStart)
        {
            prompt.alpha = 0f;
            return;
        }

        if (state == State.Booting && t < PromptStart)
        {
            int percent = Mathf.FloorToInt(TitleUI.Stutter(TitleUI.Phase(t, BarStart, BarEnd)) * 100f);
            prompt.fontSize = 30f;
            prompt.text = $"INITIALIZING SONNY  {percent:00}%";
            prompt.color = TitleUI.Fade(BootColor, 0.75f * TitleUI.Phase(t, BarStart, BarStart + 0.2f) * (1f - TitleUI.Phase(t, PromptStart - 0.15f, PromptStart)));
            return;
        }

        prompt.fontSize = 44f;
        float decode = state == State.Booting ? TitleUI.Phase(t, PromptStart, IntroEnd) : TitleUI.Phase(now, waitingSince, waitingSince + 0.5f);
        prompt.text = TitleUI.Decode(PromptText, decode, now);
        float pulse = decode < 1f ? 1f : 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Cos((now - waitingSince) * 2.8f));
        prompt.color = TitleUI.Fade(PromptColor, pulse);
    }

    void AnimateScreen(float now)
    {
        scanlines.enabled = grain.enabled = GameSettings.ScreenEffects;
        grain.uvRect = new Rect(Random.value, Random.value, TitleUI.ReferenceResolution.x / 256f, TitleUI.ReferenceResolution.y / 256f);
        float noise = 0.35f * (1f - TitleUI.Phase(introTime, 0.05f, 0.5f));
        if (now < glitchEnd) noise = Mathf.Max(noise, 0.06f);
        SetStatic(noise);
        flash.color = new Color(0.85f, 1f, 1f, 0.9f * (1f - TitleUI.EaseOut(TitleUI.Phase(introTime, 0.08f, 0.6f))));
    }

    // Squeezes the picture into a bright line, then a dot, which fades out.
    void AnimateLeaving()
    {
        float t = leaveTime;
        float collapse = TitleUI.EaseIn(TitleUI.Phase(t, CollapseStart, CollapseEnd));
        float shrink = TitleUI.EaseIn(TitleUI.Phase(t, CollapseEnd - 0.03f, ShrinkEnd));
        monitor.localScale = new Vector3(Mathf.Lerp(1f, 0f, shrink), Mathf.Lerp(1f, 0.004f, collapse), 1f);
        flash.color = new Color(0.85f, 1f, 1f, TitleUI.Phase(t, CollapseStart - 0.05f, CollapseEnd) * (1f - TitleUI.Phase(t, ShrinkEnd - 0.05f, ShrinkEnd)));
        if (t > 0.45f && t < CollapseEnd) SetStatic(0.3f);

        float glow = TitleUI.Phase(t, ShrinkEnd - 0.08f, ShrinkEnd) * (1f - TitleUI.Phase(t, ShrinkEnd, LeaveEnd - 0.1f));
        lastLight.color = new Color(0.85f, 1f, 1f, glow);
        float size = Mathf.Lerp(90f, 12f, TitleUI.Phase(t, ShrinkEnd, LeaveEnd));
        lastLight.rectTransform.sizeDelta = new Vector2(size * 1.6f, size);
    }

    void AnimateSound(float now)
    {
        sfx.volume = GameSettings.EffectsVolume;
        if (ambienceSource.clip == null) return;
        float level = TitleUI.Phase(now - startTime, 0f, 2.5f);
        if (state == State.Leaving) level *= 1f - TitleUI.Phase(leaveTime, 0.3f, LeaveEnd);
        ambienceSource.volume = AmbienceVolume * level * GameSettings.MusicVolume;
    }

    void SetStatic(float alpha)
    {
        staticBurst.enabled = alpha > 0.001f;
        if (!staticBurst.enabled) return;
        staticBurst.uvRect = new Rect(Random.value, Random.value, 1.5f, 0.85f);
        staticBurst.color = new Color(1f, 1f, 1f, alpha * Random.Range(0.6f, 1f));
    }
}
