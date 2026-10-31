using System.Collections;
using UnityEngine;

// draws and animates the held weapon
public class WeaponVisual : MonoBehaviour
{
    const float FollowThroughHold = 0.06f;
    const float ReturnDuration = 0.1f;
    const float ChargedOverswing = 20f;
    const float SwingPunch = 1.3f;
    const float HeavySwingPunch = 1.55f;
    const float SwingShake = 0.25f;
    const float HeavySwingShake = 0.45f;
    const float SwingKick = 0.08f;
    const float HeavySwingKick = 0.18f;
    const float SwingTrailWidth = 0.07f;
    const float RecoilDuration = 0.1f;
    const float RecoilTilt = 10f;
    const float MuzzleFlashDuration = 0.05f;
    const float MuzzleFlashSize = 0.18f;
    const float OrbForward = 0.3f;
    static readonly Color ChargedColor = new Color(1f, 0.9f, 0.4f);
    static readonly Color MuzzleFlashColor = new Color(1f, 0.95f, 0.7f);
    const int RingPoints = 40;
    const float RingRadius = 0.42f;
    const float RingSquash = 0.45f;
    const float RingFadeSpeed = 8f;
    static readonly Color RingTrack = new Color(1f, 0.8f, 0.5f, 0.18f);
    static readonly Color RingFilling = new Color(1f, 0.62f, 0.25f, 0.85f);
    static readonly Color RingFull = new Color(1f, 0.95f, 0.7f, 1f);

    public SpriteRenderer WeaponRenderer { get; private set; }
    public float MuzzleDistance => ranged != null ? ranged.holdDistance + ranged.Length : 0f;
    public float LaunchDistance => MuzzleDistance + (blaster != null ? blaster.ballSize * OrbForward : 0f);

    private Transform owner;
    private PlayerController controller;
    private SpriteRenderer ownerRenderer;
    private HandPositions hand;
    private Vector2 aimOffset;
    private Transform weaponTransform;
    private TrailRenderer trail;
    private SpriteRenderer muzzleFlash;

    private MeleeWeaponData melee;
    private RangedWeaponData ranged;
    private BlasterWeaponData blaster;
    private EnergyOrb chargeOrb;
    private Vector3 baseScale = Vector3.one;
    private Color baseColor = Color.white;

    private Vector2 facing = Vector2.down;
    private float aimAngle;
    private float poseAngle;
    private bool swinging;
    private int lastChargeFrame = -10;
    private int lastRangedChargeFrame = -10;
    private bool rangedChargeShown;
    private float scaleMultiplier = 1f;
    private Vector2 kick;
    private float tilt;
    private float gunVisibleUntil;
    private Coroutine motion;
    private LineRenderer ringTrack;
    private LineRenderer ringFill;
    private float ringAlpha;
    private float ringPercent;
    private bool ringWasFull;

    private bool IsCharging => lastChargeFrame >= Time.frameCount - 1;
    private bool IsRangedCharging => lastRangedChargeFrame >= Time.frameCount - 1;

    public static WeaponVisual Create(Transform owner, Vector2 aimOffset, SpriteRenderer ownerRenderer, HandPositions hand)
    {
        var pivot = new GameObject("WeaponPivot").transform;
        pivot.SetParent(owner, false);

        var visual = pivot.gameObject.AddComponent<WeaponVisual>();
        visual.owner = owner;
        visual.ownerRenderer = ownerRenderer;
        visual.hand = hand;
        visual.aimOffset = aimOffset;
        visual.controller = owner.GetComponent<PlayerController>();
        visual.WeaponRenderer = CreateChild<SpriteRenderer>("Weapon", pivot);
        visual.weaponTransform = visual.WeaponRenderer.transform;

        visual.trail = CreateChild<TrailRenderer>("SwingTrail", pivot);
        visual.trail.emitting = false;
        visual.trail.time = 0.045f;
        visual.trail.minVertexDistance = 0.02f;
        visual.trail.widthMultiplier = SwingTrailWidth;
        visual.trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        visual.trail.startColor = new Color(1f, 1f, 1f, 0.55f);
        visual.trail.endColor = new Color(1f, 1f, 1f, 0f);

        visual.muzzleFlash = CreateChild<SpriteRenderer>("MuzzleFlash", pivot);
        visual.muzzleFlash.sprite = CombatSprites.Square;
        visual.muzzleFlash.color = MuzzleFlashColor;
        visual.muzzleFlash.transform.localScale = new Vector3(MuzzleFlashSize, MuzzleFlashSize, 1f);
        visual.muzzleFlash.enabled = false;

        Material effectMaterial = CombatSprites.EffectMaterial;
        if (ownerRenderer != null)
        {
            visual.WeaponRenderer.sharedMaterial = ownerRenderer.sharedMaterial;
            visual.WeaponRenderer.sortingLayerID = ownerRenderer.sortingLayerID;
            visual.trail.sortingLayerID = ownerRenderer.sortingLayerID;
            visual.muzzleFlash.sortingLayerID = ownerRenderer.sortingLayerID;
            if (effectMaterial == null) effectMaterial = ownerRenderer.sharedMaterial;
        }
        visual.trail.sharedMaterial = effectMaterial;
        visual.muzzleFlash.sharedMaterial = effectMaterial;

        visual.ringTrack = visual.CreateRing("ChargeRingTrack", effectMaterial, 0.04f);
        visual.ringFill = visual.CreateRing("ChargeRing", effectMaterial, 0.07f);

        return visual;
    }

    public void Equip(WeaponData data)
    {
        StopMotion();
        melee = data as MeleeWeaponData;
        ranged = data as RangedWeaponData;
        blaster = data as BlasterWeaponData;
        lastChargeFrame = -10;
        lastRangedChargeFrame = -10;
        rangedChargeShown = false;
        scaleMultiplier = 1f;
        gunVisibleUntil = 0f;
        trail.Clear();

        if (chargeOrb != null)
        {
            Destroy(chargeOrb.gameObject);
            chargeOrb = null;
        }
        muzzleFlash.sprite = CombatSprites.Square;
        muzzleFlash.color = MuzzleFlashColor;
        muzzleFlash.transform.localScale = new Vector3(MuzzleFlashSize, MuzzleFlashSize, 1f);

        if (data == null) return;

        bool placeholder = data.sprite == null;
        WeaponRenderer.sprite = placeholder ? CombatSprites.Square : data.sprite;
        baseColor = placeholder ? data.placeholderColor : Color.white;
        baseScale = placeholder
            ? new Vector3(data.placeholderSize.x, data.placeholderSize.y, 1f)
            : Vector3.one * data.spriteScale;
        WeaponRenderer.color = baseColor;

        if (blaster != null)
        {
            chargeOrb = EnergyOrb.Create("ChargeOrb", transform, blaster, WeaponRenderer, false);
            chargeOrb.gameObject.SetActive(false);
            muzzleFlash.sprite = CombatSprites.Glow;
            muzzleFlash.color = blaster.coreColor;
            muzzleFlash.transform.localScale = Vector3.one * (blaster.ballSize * 2.5f);
        }
        else if (data is EmpWeaponData emp)
        {
            muzzleFlash.sprite = CombatSprites.Glow;
            muzzleFlash.color = emp.empCoreColor;
            muzzleFlash.transform.localScale = Vector3.one * 0.8f;
        }
    }

    public void SetFacing(Vector2 direction)
    {
        facing = PlayerController.SnapToFourWay(direction);
    }

    public void Aim(Vector2 direction)
    {
        aimAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    public void ShowCharge(float percent, float windupAngle)
    {
        if (melee == null) return;
        StopMotion();
        lastChargeFrame = Time.frameCount;

        SwingAngles angles = melee.AnglesFor(facing);
        float windup = angles.raised - SwingDirection(angles) * windupAngle;
        poseAngle = Mathf.Lerp(angles.rest, windup, EaseOutCubic(percent));

        bool full = percent >= 1f;
        ringPercent = percent;
        scaleMultiplier = 1f + 0.15f * percent;
        kick = Random.insideUnitCircle * (full ? 0.03f : 0.015f * percent);
        WeaponRenderer.color = full && Mathf.Repeat(Time.time * 10f, 1f) > 0.5f ? ChargedColor : baseColor;
    }

    public void PlaySwing(float duration, bool heavy)
    {
        if (melee == null) return;

        float? startAngle = IsCharging ? poseAngle : (float?)null;
        StopMotion();
        motion = StartCoroutine(SwingRoutine(duration, heavy, startAngle));
    }

    public void ShowRangedCharge(float percent)
    {
        if (blaster == null) return;
        StopMotion();
        lastRangedChargeFrame = Time.frameCount;

        bool full = percent >= 1f;
        kick = Random.insideUnitCircle * (full ? 0.025f : 0.012f * percent);
        chargeOrb.Size = blaster.ballSize * EaseOutCubic(percent);
        chargeOrb.Strength = Mathf.Lerp(0.2f, 1f, percent);
        chargeOrb.Unstable = full;
        chargeOrb.DrawingIn = !full;
        WeaponRenderer.color = full && Mathf.Repeat(Time.time * 10f, 1f) > 0.5f ? blaster.energyColor : baseColor;
    }

    public void PlayFizzle(float showFor)
    {
        if (blaster == null) return;
        EndRangedCharge();
        gunVisibleUntil = Time.time + showFor;

        Vector2 at = chargeOrb.transform.position;
        Color faded = new Color(blaster.energyColor.r, blaster.energyColor.g, blaster.energyColor.b, 0.5f);
        HitEffects.Ring(at, faded, Mathf.Max(0.3f, chargeOrb.Size * 2f));
        HitEffects.Sparks(at, Vector2.down, 6, 2f, 160f, blaster.energyColor, Color.gray);
    }

    public void PlayRecoil(float distance, float showFor)
    {
        if (ranged == null) return;

        EndRangedCharge();
        StopMotion();
        gunVisibleUntil = Time.time + showFor;
        motion = StartCoroutine(RecoilRoutine(distance));
        StartCoroutine(MuzzleFlashRoutine());
    }

    void LateUpdate()
    {
        ApplyTransforms();
        UpdateRing();
    }

    LineRenderer CreateRing(string ringName, Material material, float width)
    {
        LineRenderer ring = CreateChild<LineRenderer>(ringName, transform);
        ring.useWorldSpace = true;
        ring.loop = false;
        ring.widthMultiplier = width;
        ring.numCapVertices = 2;
        ring.sharedMaterial = material;
        if (ownerRenderer != null) ring.sortingLayerID = ownerRenderer.sortingLayerID;
        ring.sortingOrder = (ownerRenderer != null ? ownerRenderer.sortingOrder : 0) - 2;
        ring.enabled = false;
        return ring;
    }

    void UpdateRing()
    {
        bool charging = melee != null && IsCharging;
        ringAlpha = Mathf.MoveTowards(ringAlpha, charging ? 1f : 0f, RingFadeSpeed * Time.deltaTime);
        bool visible = ringAlpha > 0.001f;
        ringTrack.enabled = ringFill.enabled = visible;
        if (!charging) ringWasFull = false;
        if (!visible) return;

        Vector2 feet = owner.position;
        if (ownerRenderer != null) feet = new Vector2(ownerRenderer.bounds.center.x, ownerRenderer.bounds.min.y + 0.06f);

        bool full = ringPercent >= 1f;
        if (full && charging && !ringWasFull)
        {
            ringWasFull = true;
            HitEffects.Ring(feet, RingFull, 1.1f);
        }

        DrawArc(ringTrack, feet, 1f);
        DrawArc(ringFill, feet, Mathf.Max(0.02f, ringPercent));

        Color fill = full ? Color.Lerp(RingFilling, RingFull, 0.5f + 0.5f * Mathf.Sin(Time.time * 20f)) : RingFilling;
        fill.a *= ringAlpha;
        ringFill.startColor = ringFill.endColor = fill;
        ringFill.widthMultiplier = full ? 0.09f : 0.07f;
        Color track = RingTrack;
        track.a *= ringAlpha;
        ringTrack.startColor = ringTrack.endColor = track;
    }

    static void DrawArc(LineRenderer ring, Vector2 center, float amount)
    {
        int count = Mathf.Max(2, Mathf.CeilToInt(RingPoints * amount) + 1);
        ring.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.PI * 0.5f - Mathf.PI * 2f * amount * i / (count - 1);
            ring.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * RingRadius, center.y + Mathf.Sin(angle) * RingRadius * RingSquash, 0f));
        }
    }

    void ApplyTransforms()
    {
        if (melee == null && ranged == null)
        {
            WeaponRenderer.enabled = false;
            return;
        }

        Vector3 ownerScale = owner.lossyScale;
        transform.localScale = new Vector3(1f / ownerScale.x, 1f / ownerScale.y, 1f);

        Vector2 offset;
        float angle;
        bool behind;

        if (melee != null)
        {
            Sprite bodyFrame = ownerRenderer != null ? ownerRenderer.sprite : null;
            float frameTilt = 0f;
            WeaponLayer layer = WeaponLayer.InFront;
            offset = aimOffset;
            if (hand != null)
            {
                offset = hand.PositionFor(facing, bodyFrame, out frameTilt);
                layer = hand.For(facing).layer;
            }

            SwingAngles angles = melee.AnglesFor(facing);
            float swingAngle = (swinging || IsCharging ? poseAngle : angles.rest + frameTilt) * Mathf.Deg2Rad;

            Vector2 pointing = new Vector2(Mathf.Cos(swingAngle) * angles.arcWidth, Mathf.Sin(swingAngle) * angles.arcHeight);
            float reach = pointing.magnitude;
            angle = Mathf.Atan2(pointing.y, pointing.x) * Mathf.Rad2Deg;
            behind = layer == WeaponLayer.Behind
                || (layer == WeaponLayer.BehindWhenRaised && Mathf.Sin(angle * Mathf.Deg2Rad) > 0.2f);

            Vector3 scale = baseScale * scaleMultiplier;
            scale.x *= reach;
            weaponTransform.localScale = scale;
            weaponTransform.localPosition = new Vector3((melee.Length * 0.5f - melee.gripInset) * reach + kick.x, kick.y, 0f);
            trail.transform.localPosition = new Vector3((melee.Length - melee.gripInset) * reach, 0f, 0f);
            WeaponRenderer.enabled = melee.showHeldSprite;
        }
        else
        {
            offset = aimOffset;
            angle = aimAngle + tilt;
            behind = Mathf.Sin(angle * Mathf.Deg2Rad) > 0.2f;
            if (controller != null) angle -= controller.LeanAngle;

            Vector3 scale = baseScale * scaleMultiplier;
            if (facing.x < 0f) scale.y = -scale.y;
            weaponTransform.localScale = scale;
            weaponTransform.localPosition = new Vector3(ranged.holdDistance + ranged.Length * 0.5f + kick.x, kick.y, 0f);
            muzzleFlash.transform.localPosition = new Vector3(LaunchDistance + 0.06f, 0f, 0f);

            bool charging = IsRangedCharging;
            WeaponRenderer.enabled = ranged.showHeldSprite && (charging || Time.time < gunVisibleUntil);
            if (chargeOrb != null)
            {
                chargeOrb.gameObject.SetActive(charging);
                chargeOrb.transform.localPosition = new Vector3(LaunchDistance + kick.x, kick.y, 0f);
            }
            if (rangedChargeShown && !charging) WeaponRenderer.color = baseColor;
            rangedChargeShown = charging;
        }

        if (controller != null && controller.IsCrouching && !swinging && !IsCharging && !IsRangedCharging)
            WeaponRenderer.enabled = false;

        if (controller != null) offset = Vector2.Scale(offset, controller.BodySquash);

        transform.localPosition = new Vector3(offset.x / ownerScale.x, offset.y / ownerScale.y, 0f);
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);

        int baseOrder = ownerRenderer != null ? ownerRenderer.sortingOrder : 0;
        WeaponRenderer.sortingOrder = behind ? baseOrder - 1 : baseOrder + 1;
        trail.sortingOrder = WeaponRenderer.sortingOrder;
        muzzleFlash.sortingOrder = baseOrder + 2;
        if (chargeOrb != null) chargeOrb.SetSorting(WeaponRenderer.sortingLayerID, baseOrder + 2);
    }

    IEnumerator SwingRoutine(float duration, bool heavy, float? startAngle)
    {
        SwingAngles angles = melee.AnglesFor(facing);
        float from = startAngle ?? angles.raised;
        float to = angles.end + (heavy ? SwingDirection(angles) * ChargedOverswing : 0f);

        swinging = true;
        WeaponRenderer.color = baseColor;
        trail.widthMultiplier = heavy ? SwingTrailWidth * 1.6f : SwingTrailWidth;
        poseAngle = from;
        ApplyTransforms();
        trail.Clear();
        trail.emitting = true;
        CameraShake.ShakeAtLeast(heavy ? HeavySwingShake : SwingShake);
        CameraShake.Kick(facing, heavy ? HeavySwingKick : SwingKick);

        float peakScale = heavy ? HeavySwingPunch : SwingPunch;
        yield return Tween(duration, t =>
        {
            poseAngle = Mathf.Lerp(from, to, EaseOutQuint(t));
            float swell = Mathf.Sin(Mathf.Clamp01(t * 1.6f) * Mathf.PI);
            scaleMultiplier = Mathf.Lerp(heavy ? 1.15f : 1f, peakScale, swell);
            WeaponRenderer.color = Color.Lerp(baseColor, Color.white, swell * 0.8f);
        });

        trail.emitting = false;
        WeaponRenderer.color = baseColor;
        yield return new WaitForSeconds(heavy ? FollowThroughHold * 2f : FollowThroughHold);

        float endScale = scaleMultiplier;
        yield return Tween(ReturnDuration, t =>
        {
            poseAngle = Mathf.Lerp(to, melee.AnglesFor(facing).rest, t);
            scaleMultiplier = Mathf.Lerp(endScale, 1f, t);
        });

        swinging = false;
        motion = null;
    }

    IEnumerator RecoilRoutine(float distance)
    {
        float maxTilt = facing.x < 0f ? -RecoilTilt : RecoilTilt;
        scaleMultiplier = 1f;
        WeaponRenderer.color = baseColor;

        yield return Tween(RecoilDuration, t =>
        {
            float strength = 1f - EaseOutCubic(t);
            kick = new Vector2(-distance * strength, 0f);
            tilt = maxTilt * strength;
        });
        motion = null;
    }

    IEnumerator MuzzleFlashRoutine()
    {
        muzzleFlash.enabled = true;
        yield return new WaitForSeconds(MuzzleFlashDuration);
        muzzleFlash.enabled = false;
    }

    void EndRangedCharge()
    {
        lastRangedChargeFrame = -10;
        if (chargeOrb != null) chargeOrb.gameObject.SetActive(false);
    }

    void StopMotion()
    {
        if (motion != null)
        {
            StopCoroutine(motion);
            motion = null;
        }
        swinging = false;
        trail.emitting = false;
        kick = Vector2.zero;
        tilt = 0f;
    }

    static float SwingDirection(SwingAngles angles)
    {
        return Mathf.Sign(angles.end - angles.raised);
    }

    static IEnumerator Tween(float duration, System.Action<float> apply)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            apply(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        apply(1f);
    }

    static float EaseOutQuint(float t)
    {
        float u = 1f - Mathf.Clamp01(t);
        return 1f - u * u * u * u * u;
    }

    static float EaseOutCubic(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    static T CreateChild<T>(string name, Transform parent) where T : Component
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.AddComponent<T>();
    }
}

public static class CombatSprites
{
    private static Sprite square;
    private static Material effectMaterial;
    private static Material flashMaterial;
    private static Texture2D ringTexture;
    private static Texture2D softCircleTexture;
    private static Texture2D vignetteTexture;
    private static Texture2D discTexture;
    private static Sprite glow;
    private static Sprite disc;

    public static Sprite Square
    {
        get
        {
            if (square == null)
            {
                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                texture.SetPixels(pixels);
                texture.Apply();
                square = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            }
            return square;
        }
    }

    public static Material EffectMaterial
    {
        get
        {
            if (effectMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null) effectMaterial = new Material(shader);
            }
            return effectMaterial;
        }
    }

    public static Material FlashMaterial
    {
        get
        {
            if (flashMaterial == null)
            {
                Shader shader = Shader.Find("Sonny/Sprite Flash");
                if (shader != null && shader.isSupported) flashMaterial = new Material(shader);
            }
            return flashMaterial;
        }
    }

    public static Texture2D RingTexture =>
        ringTexture != null ? ringTexture : (ringTexture = MakeRadialTexture(64, r => Mathf.Clamp01(1f - Mathf.Abs(r - 0.78f) / 0.14f)));

    public static Texture2D SoftCircleTexture =>
        softCircleTexture != null ? softCircleTexture : (softCircleTexture = MakeRadialTexture(64, r => Mathf.Pow(Mathf.Clamp01(1f - r), 2f)));

    public static Texture2D DiscTexture =>
        discTexture != null ? discTexture : (discTexture = MakeRadialTexture(64, r => Mathf.Clamp01((1f - r) / 0.2f)));

    public static Sprite Glow => glow != null ? glow : (glow = MakeSprite(SoftCircleTexture));

    public static Sprite Disc => disc != null ? disc : (disc = MakeSprite(DiscTexture));

    public static Texture2D VignetteTexture =>
        vignetteTexture != null ? vignetteTexture : (vignetteTexture = MakeRadialTexture(128, r => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1.3f, r))));

    public static Sprite MakeSprite(Texture2D texture)
    {
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
            texture.width, 0, SpriteMeshType.FullRect);
    }

    public static Texture2D MakeRadialTexture(int size, System.Func<float, float> alphaAtRadius)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alphaAtRadius(r));
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
