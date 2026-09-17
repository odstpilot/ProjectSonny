using UnityEngine;
using UnityEngine.UI;

// Builds the title screen: the view of space in layers, the title, the loading bar, and the screen effects over it all.
// Positions are pixels on the 1920x1080 canvas, measured from the middle; the canvas always covers the whole screen.
public partial class TitleScreen
{
    static readonly Vector2 PlanetCenter = new Vector2(880f, -520f);
    const float PlanetRadius = 720f;

    static readonly Vector2 ProjectHome = new Vector2(8f, 172f);
    static readonly Vector2 SonnyHome = new Vector2(0f, 20f);
    const float SonnySize = 230f;
    static readonly Vector2 DividerHome = new Vector2(0f, 118f);
    const float DividerWidth = 635f;
    const int DividerLength = 200;
    static readonly Vector2 BarCenter = new Vector2(0f, -165f);
    const int SegmentCount = 24;
    static readonly Vector2 PromptPosition = new Vector2(0f, -285f);

    static readonly Vector2 MeteorDirection = new Vector2(0.82f, -0.57f).normalized;
    static readonly Vector2 ShipFrom = new Vector2(-600f, -650f);
    static readonly Vector2 ShipTo = new Vector2(1150f, 150f);
    const float ShipCrossing = 45f, ShipPeriod = 60f, ShipHeadStart = 18.5f;
    const float StationLean = 16f;
    const float StationScale = 2.6f;

    // Lights on the station, in its pixels: x, y, and 0 for red or 1 for cyan.
    static readonly Vector3[] StationLights =
    {
        new Vector3(55f, 171f, 0f), new Vector3(2f, 111f, 1f), new Vector3(108f, 111f, 1f),
        new Vector3(41f, 22f, 0f), new Vector3(18f, 157f, 1f), new Vector3(92f, 157f, 0f)
    };

    // The asteroid cluster, upper right: x, y, and size.
    static readonly Vector3[] ClusterRocks =
    {
        new Vector3(410f, 330f, 78f), new Vector3(480f, 355f, 44f), new Vector3(520f, 300f, 58f), new Vector3(585f, 330f, 36f),
        new Vector3(610f, 270f, 70f), new Vector3(660f, 215f, 52f), new Vector3(700f, 300f, 30f), new Vector3(455f, 255f, 28f),
        new Vector3(560f, 230f, 24f), new Vector3(740f, 240f, 40f), new Vector3(650f, 360f, 22f)
    };

    // A layer of the view: faded and slid in during the boot, and moved against the mouse, more the nearer it is.
    class Layer
    {
        public RectTransform rect;
        public CanvasGroup group;
        public float depth, fadeStart, fadeEnd;
        public Vector2 slideFrom;
    }

    class Rock
    {
        public RectTransform rect;
        public RawImage image;
        public Vector2 home;
        public float angle, spin, bob, bobSpeed, phase;
    }

    class Twinkle
    {
        public RawImage image;
        public Color color;
        public float size, speed, phase;
    }

    class Meteor
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 position, velocity;
        public float age, life;
        public bool active;
    }

    class Mote
    {
        public RawImage image;
        public Vector2 position, velocity;
        public float alpha, phase;
    }

    class Beacon
    {
        public RawImage image;
        public Color color;
        public float period, phase, duty;
    }

    void Build()
    {
        fontAsset = TitleUI.MakeFontAsset(font);
        headingAsset = headingFont != null ? TitleUI.MakeFontAsset(headingFont) : fontAsset;

        RectTransform root = TitleUI.NewRect("Title Canvas", transform);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = TitleUI.ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;   // the view always covers the screen

        TitleUI.StretchedPicture("Black", root, Texture2D.whiteTexture).color = Color.black;
        monitor = TitleUI.Stretch(TitleUI.NewRect("Monitor", root));
        sky = TitleUI.Stretch(TitleUI.NewRect("Sky", monitor));
        TitleUI.StretchedPicture("Space", sky, Texture2D.whiteTexture).color = new Color(0.01f, 0.03f, 0.04f);

        BuildSound();
        BuildSky();
        BuildShade();
        BuildTitle();
        BuildBar();

        prompt = TitleUI.Label("Prompt", monitor, fontAsset, 44f, PromptColor, PromptPosition, new Vector2(1400f, 70f));
        prompt.characterSpacing = 12f;
        prompt.fontSharedMaterial = TitleUI.GlowMaterial(fontAsset, new Color(0.2f, 0.9f, 1f, 0.45f), 0.8f, 0.3f);
        prompt.alpha = 0f;
        menu = new TitleMenu(monitor, PromptPosition, root, fontAsset, sfx);

        BuildOverlays(root);
    }

    Layer AddLayer(string layerName, float depth, float fadeStart, float fadeEnd, Vector2 slideFrom)
    {
        RectTransform rect = TitleUI.Place(TitleUI.NewRect(layerName, sky), Vector2.zero, TitleUI.ReferenceResolution);
        var layer = new Layer
        {
            rect = rect,
            group = rect.gameObject.AddComponent<CanvasGroup>(),
            depth = depth,
            fadeStart = fadeStart,
            fadeEnd = fadeEnd,
            slideFrom = slideFrom
        };
        layer.group.alpha = 0f;
        layers.Add(layer);
        return layer;
    }

    void BuildSky()
    {
        Layer nebula = AddLayer("Nebula", 8f, 0.25f, 1.6f, Vector2.zero);
        nebulaBack = TitleUI.Picture("Nebula Back", nebula.rect, TitleArt.NebulaTexture(3.1f, false), Vector2.zero, new Vector2(2200f, 1240f));
        nebulaFront = TitleUI.Picture("Nebula Front", nebula.rect, TitleArt.NebulaTexture(47.9f, true), new Vector2(-140f, 60f), new Vector2(2300f, 1300f));
        nebulaLight = TitleUI.Picture("Nebula Light", nebula.rect, CombatSprites.SoftCircleTexture, new Vector2(-520f, 260f), new Vector2(1900f, 1150f), glow: true);

        Layer stars = AddLayer("Stars", 14f, 0.35f, 1.6f, Vector2.zero);
        TitleUI.Picture("Starfield", stars.rect, TitleArt.StarfieldTexture(), Vector2.zero, new Vector2(2100f, 1220f));
        for (int added = 0, tries = 0; added < 64 && tries < 400; tries++)
        {
            var position = new Vector2(Random.Range(-980f, 980f), Random.Range(-560f, 560f));
            if (Vector2.Distance(position, PlanetCenter) < PlanetRadius + 20f) continue;
            float size = Random.Range(10f, 28f);
            twinkles.Add(new Twinkle
            {
                image = TitleUI.Picture("Twinkle", stars.rect, TitleUI.SparkleTexture, position, new Vector2(size, size), glow: true),
                color = Random.value < 0.3f ? new Color(0.6f, 1f, 1f, Random.Range(0.5f, 0.9f)) : new Color(1f, 1f, 1f, Random.Range(0.5f, 0.9f)),
                size = size,
                speed = Random.Range(0.6f, 2.2f),
                phase = Random.Range(0f, 10f)
            });
            added++;
        }

        Layer meteorLayer = AddLayer("Meteors", 18f, 1.2f, 1.6f, Vector2.zero);
        for (int i = 0; i < 8; i++)
        {
            RawImage streak = TitleUI.Picture("Meteor", meteorLayer.rect, TitleUI.StreakTexture, new Vector2(-3000f, 0f), new Vector2(200f, 18f), glow: true);
            RectTransform rect = streak.rectTransform;
            rect.pivot = new Vector2(1f, 0.5f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(MeteorDirection.y, MeteorDirection.x) * Mathf.Rad2Deg);
            RawImage head = TitleUI.Picture("Head", rect, CombatSprites.SoftCircleTexture, Vector2.zero, new Vector2(34f, 34f), glow: true);
            head.rectTransform.anchorMin = head.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            head.color = new Color(0.75f, 1f, 1f, 0.9f);
            var meteor = new Meteor { rect = rect, group = rect.gameObject.AddComponent<CanvasGroup>() };
            meteor.group.alpha = 0f;
            meteors.Add(meteor);
        }

        // The planet only works out the pixels that can reach the screen, allowing for its slide in and the mouse.
        Layer planetLayer = AddLayer("Planet", 26f, 0.45f, 2.2f, new Vector2(200f, -80f));
        atmosphere = TitleUI.Picture("Atmosphere", planetLayer.rect, TitleArt.AtmosphereTexture(), PlanetCenter + new Vector2(-34f, 18f), Vector2.one * PlanetRadius * 2.36f, glow: true);
        planet = new SpinningPlanet(PlanetCenter, PlanetRadius, Rect.MinMaxRect(-1000f, -590f, 1000f, 650f));
        TitleUI.Picture("Planet", planetLayer.rect, planet.Texture, PlanetCenter, Vector2.one * PlanetRadius * 2f);

        Layer shipLayer = AddLayer("Ship", 30f, 1.6f, 2.6f, Vector2.zero);
        ship = TitleUI.Place(TitleUI.NewRect("Ship", shipLayer.rect), ShipFrom, new Vector2(84f, 54f));
        ship.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(ShipTo.y - ShipFrom.y, ShipTo.x - ShipFrom.x) * Mathf.Rad2Deg);
        engineGlow = TitleUI.Picture("Engine", ship, CombatSprites.SoftCircleTexture, new Vector2(-44f, 0f), new Vector2(44f, 26f), glow: true);
        Image hull = TitleUI.Place(TitleUI.NewRect("Hull", ship), Vector2.zero, new Vector2(84f, 54f)).gameObject.AddComponent<Image>();
        hull.sprite = TitleArt.ShipSprite();
        hull.raycastTarget = false;

        Layer cluster = AddLayer("Asteroids", 38f, 0.8f, 2.3f, new Vector2(110f, 70f));
        var rockTextures = new Texture2D[6];
        for (int i = 0; i < rockTextures.Length; i++)
            rockTextures[i] = TitleArt.RockTexture(96, 96, 101 + i * 17);
        for (int i = 0; i < ClusterRocks.Length; i++)
            AddRock(cluster.rect, rockTextures[i % rockTextures.Length], ClusterRocks[i], ClusterRocks[i].z * 1.3f, Random.Range(4f, 14f), Random.Range(3f, 8f));

        Layer foreground = AddLayer("Station", 50f, 0.6f, 2.1f, new Vector2(-60f, -150f));
        Rock bigRock = AddRock(foreground.rect, TitleArt.RockTexture(320, 200, 7), new Vector2(-690f, -440f), 0f, 0f, 5f);
        bigRock.rect.sizeDelta = new Vector2(780f, 490f);
        bigRock.angle = 0f;
        BuildStation(bigRock.rect);

        Layer dust = AddLayer("Dust", 70f, 0.9f, 2.4f, Vector2.zero);
        for (int i = 0; i < 30; i++)
        {
            bool blurry = i < 8;
            float size = blurry ? Random.Range(14f, 34f) : Random.Range(4f, 9f);
            var mote = new Mote
            {
                position = new Vector2(Random.Range(-1000f, 1000f), Random.Range(-600f, 600f)),
                velocity = new Vector2(Random.Range(-12f, 18f), Random.Range(4f, 20f)),
                alpha = blurry ? Random.Range(0.1f, 0.25f) : Random.Range(0.35f, 0.8f),
                phase = Random.Range(0f, 10f)
            };
            mote.image = TitleUI.Picture("Mote", dust.rect, blurry ? CombatSprites.SoftCircleTexture : CombatSprites.DiscTexture, mote.position, new Vector2(size, size));
            motes.Add(mote);
        }

        // Two rocks drifting close past the view, out of focus and in shadow.
        Layer near = AddLayer("Near", 110f, 1f, 2.6f, new Vector2(-90f, 70f));
        Texture2D blurryRock = TitleArt.RockTexture(40, 40, 211);
        AddRock(near.rect, blurryRock, new Vector2(-930f, 500f), 320f, 3f, 10f).image.color = new Color(0.22f, 0.25f, 0.28f);
        AddRock(near.rect, blurryRock, new Vector2(1010f, 560f), 230f, -4f, 8f).image.color = new Color(0.18f, 0.2f, 0.23f);

        TitleUI.Picture("Vignette", sky, CombatSprites.VignetteTexture, Vector2.zero, new Vector2(2200f, 1240f)).color = new Color(0f, 0f, 0f, 0.75f);
    }

    Rock AddRock(RectTransform parent, Texture2D texture, Vector2 home, float size, float spin, float bob)
    {
        RawImage image = TitleUI.Picture("Rock", parent, texture, home, new Vector2(size, size));
        var rock = new Rock
        {
            rect = image.rectTransform,
            image = image,
            home = home,
            angle = spin != 0f ? Random.Range(0f, 360f) : 0f,
            spin = Random.value < 0.5f ? spin : -spin,
            bob = bob,
            bobSpeed = Random.Range(0.3f, 0.7f),
            phase = Random.Range(0f, 10f)
        };
        rocks.Add(rock);
        return rock;
    }

    // The station, leaning out of the top of the big rock, lights blinking.
    void BuildStation(RectTransform rock)
    {
        Texture2D texture = TitleArt.StationTexture();
        RawImage image = TitleUI.Picture("Station", rock, texture, new Vector2(-40f, 160f), new Vector2(texture.width, texture.height) * StationScale);
        station = image.rectTransform;
        station.pivot = new Vector2(0.5f, 0f);

        foreach (Vector3 light in StationLights)
        {
            var position = new Vector2((light.x + 0.5f - texture.width * 0.5f) * StationScale, (light.y + 0.5f - texture.height * 0.5f) * StationScale);
            beacons.Add(new Beacon
            {
                image = TitleUI.Picture("Light", station, CombatSprites.SoftCircleTexture, position, new Vector2(28f, 28f), glow: true),
                color = light.z < 0.5f ? new Color(1f, 0.3f, 0.3f) : new Color(0.4f, 1f, 1f),
                period = Random.Range(1.4f, 3.2f),
                phase = Random.Range(0f, 3f),
                duty = 0.25f
            });
        }
    }

    // Darkness over the view, whose top edge sweeps down during the boot with a beam of light along it.
    void BuildShade()
    {
        shade = TitleUI.Place(TitleUI.NewRect("Shade", monitor), new Vector2(0f, -650f), new Vector2(2300f, 1300f));
        shade.pivot = new Vector2(0.5f, 0f);
        RawImage cover = shade.gameObject.AddComponent<RawImage>();
        cover.color = new Color(0f, 0f, 0f, 0.94f);
        cover.raycastTarget = false;
        scanBeam = TitleUI.Picture("Scan Beam", monitor, CombatSprites.SoftCircleTexture, new Vector2(0f, 650f), new Vector2(2400f, 80f), glow: true);
    }

    void BuildTitle()
    {
        sonnyHalo = TitleUI.Picture("Title Glow", monitor, CombatSprites.SoftCircleTexture, SonnyHome + new Vector2(0f, 20f), new Vector2(1300f, 440f), glow: true);
        sonnyHalo.color = Color.clear;

        project = TitleUI.Label("Project", monitor, headingAsset, 92f, ProjectColor, ProjectHome, new Vector2(1000f, 130f));
        project.characterSpacing = 18f;
        project.fontStyle = TMPro.FontStyles.Bold;
        project.fontSharedMaterial = TitleUI.GlowMaterial(headingAsset, new Color(0f, 0.05f, 0.08f, 0.85f), 0.25f, 0.1f, new Vector2(0.5f, -0.5f));

        dividerTexture = PixelArt.MakeTexture(DividerLength, 3, (x, y) => default);
        dividerPixels = dividerTexture.GetPixels32();
        divider = TitleUI.Picture("Divider", monitor, dividerTexture, DividerHome, new Vector2(DividerWidth, 9f));
        divider.color = new Color(0.6f, 0.85f, 0.88f, 0.75f);

        ghostRed = TitleUI.Label("Sonny Red", monitor, fontAsset, SonnySize, new Color(1f, 0.22f, 0.4f), SonnyHome, new Vector2(1500f, 300f));
        ghostCyan = TitleUI.Label("Sonny Cyan", monitor, fontAsset, SonnySize, new Color(0.2f, 1f, 1f), SonnyHome, new Vector2(1500f, 300f));
        sonny = TitleUI.Label("Sonny", monitor, fontAsset, SonnySize, SonnyColor, SonnyHome, new Vector2(1500f, 300f));
        sonny.fontSharedMaterial = sonnyMaterial = TitleUI.GlowMaterial(fontAsset, SonnyGlow, 0.9f, 0.55f);
        foreach (TMPro.TextMeshProUGUI label in new[] { ghostRed, ghostCyan, sonny })
        {
            label.characterSpacing = 20f;
            label.text = "SONNY";
        }
        ghostRed.alpha = ghostCyan.alpha = 0f;
    }

    void BuildBar()
    {
        RectTransform rect = TitleUI.Place(TitleUI.NewRect("Loading Bar", monitor), BarCenter, new Vector2(800f, 80f));
        bar = rect.gameObject.AddComponent<CanvasGroup>();
        bar.alpha = 0f;

        const float halfWidth = 291f, halfHeight = 27.5f;
        Vector2[] capsule =
        {
            new Vector2(-halfWidth, 0f), new Vector2(-halfWidth + halfHeight, halfHeight), new Vector2(halfWidth - halfHeight, halfHeight),
            new Vector2(halfWidth, 0f), new Vector2(halfWidth - halfHeight, -halfHeight), new Vector2(-halfWidth + halfHeight, -halfHeight)
        };
        TitleUI.Shape("Inside", rect, new Color(0.02f, 0.08f, 0.09f, 0.7f), capsule, filled: true);
        TitleUI.Shape("Glow", rect, TitleUI.Fade(FrameCyan, 0.18f), capsule, thickness: 3f, feather: 14f);
        TitleUI.Shape("Outline", rect, TitleUI.Fade(HudTeal, 0.95f), capsule, thickness: 3f, feather: 1.5f);
        TitleUI.Shape("Left Wire", rect, TitleUI.Fade(HudTeal, 0.9f), new[] { new Vector2(-380f, 0f), new Vector2(-halfWidth - 4f, 0f) }, thickness: 2.5f, feather: 1.5f, closed: false);
        TitleUI.Shape("Right Wire", rect, TitleUI.Fade(HudTeal, 0.9f), new[] { new Vector2(halfWidth + 4f, 0f), new Vector2(380f, 0f) }, thickness: 2.5f, feather: 1.5f, closed: false);

        // Slanted segments, like the notches on a gauge.
        segments = new HudShape[SegmentCount];
        float slot = 522f / SegmentCount;
        for (int i = 0; i < SegmentCount; i++)
        {
            var center = new Vector2(-261f + slot * (i + 0.5f), 0f);
            segments[i] = TitleUI.Shape($"Segment {i + 1}", rect, SegmentUnlit, TitleUI.Slant(center, 13f, 30f, 6f), filled: true);
        }
    }

    void BuildOverlays(RectTransform root)
    {
        Texture2D scanTexture = PixelArt.MakeTexture(1, 3, (x, y) => y == 0 ? new Color32(0, 0, 0, 255) : default);
        scanTexture.wrapMode = TextureWrapMode.Repeat;
        scanlines = TitleUI.StretchedPicture("Scanlines", monitor, scanTexture);
        scanlines.color = new Color(1f, 1f, 1f, 0.18f);
        scanlines.uvRect = new Rect(0f, 0f, 1f, TitleUI.ReferenceResolution.y / 3f);

        grain = TitleUI.StretchedPicture("Grain", monitor, TitleUI.NoiseTexture(256, 256));
        grain.color = new Color(1f, 1f, 1f, 0.04f);
        staticBurst = TitleUI.StretchedPicture("Static", monitor, TitleUI.NoiseTexture(192, 108));
        staticBurst.enabled = false;
        flash = TitleUI.StretchedPicture("Flash", monitor, Texture2D.whiteTexture);
        flash.color = Color.clear;

        lastLight = TitleUI.Picture("Last Light", root, CombatSprites.SoftCircleTexture, Vector2.zero, new Vector2(144f, 90f));
        lastLight.color = Color.clear;
    }

    void BuildSound()
    {
        ambienceSource = gameObject.AddComponent<AudioSource>();
        ambienceSource.playOnAwake = false;
        ambienceSource.loop = true;
        ambienceSource.spatialBlend = 0f;
        ambienceSource.volume = 0f;
        ambienceSource.clip = ambience;
        if (ambience != null) ambienceSource.Play();

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        blipClip = TitleUI.Tone("Blip", 1320f, 0.08f, 0.5f);
        staticClip = TitleUI.Crackle("Static", 0.35f, 0.6f);
    }
}
