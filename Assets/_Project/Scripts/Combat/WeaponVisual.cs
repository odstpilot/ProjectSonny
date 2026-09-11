using System.Collections;
using UnityEngine;

// Code-driven weapon animation, so attacks read clearly with or without weapon art.
// Melee: the weapon sits in the player's hand (HandPositions), rests at an angle set per facing direction,
//        and chops from its Raised angle to its End angle. It never flips. Facing up or down, the chop goes
//        toward or away from the camera instead of sweeping sideways (see SwingAngles.arcWidth).
// Ranged: hidden until you shoot; then it points at the shot, kicks back, and flashes.
// PlayerCombat creates this at runtime as a child of the player named WeaponPivot.
public class WeaponVisual : MonoBehaviour
{
    const float FollowThroughHold = 0.06f;
    const float ReturnDuration = 0.1f;
    const float ChargedOverswing = 20f;
    const float RecoilDuration = 0.1f;
    const float RecoilTilt = 10f;
    const float MuzzleFlashDuration = 0.05f;
    static readonly Color ChargedColor = new Color(1f, 0.9f, 0.4f);

    public SpriteRenderer WeaponRenderer { get; private set; }
    public float MuzzleDistance => ranged != null ? ranged.holdDistance + ranged.Length : 0f;

    private Transform owner;
    private SpriteRenderer ownerRenderer;
    private HandPositions hand;
    private Vector2 aimOffset;
    private Transform weaponTransform;
    private TrailRenderer trail;
    private SpriteRenderer muzzleFlash;

    private MeleeWeaponData melee;
    private RangedWeaponData ranged;
    private Vector3 baseScale = Vector3.one;
    private Color baseColor = Color.white;

    private Vector2 facing = Vector2.down;
    private float aimAngle;          // ranged: exact aim, in degrees
    private float poseAngle;         // melee: weapon angle while a swing or charge is driving it
    private bool swinging;
    private int lastChargeFrame = -10;
    private float scaleMultiplier = 1f;
    private Vector2 kick;            // recoil and charge shake, along the weapon
    private float tilt;              // ranged: recoil rotation
    private float gunVisibleUntil;
    private Coroutine motion;

    private bool IsCharging => lastChargeFrame >= Time.frameCount - 1;

    public static WeaponVisual Create(Transform owner, Vector2 aimOffset, SpriteRenderer ownerRenderer, HandPositions hand)
    {
        var pivot = new GameObject("WeaponPivot").transform;
        pivot.SetParent(owner, false);

        var visual = pivot.gameObject.AddComponent<WeaponVisual>();
        visual.owner = owner;
        visual.ownerRenderer = ownerRenderer;
        visual.hand = hand;
        visual.aimOffset = aimOffset;
        visual.WeaponRenderer = CreateChild<SpriteRenderer>("Weapon", pivot);
        visual.weaponTransform = visual.WeaponRenderer.transform;

        visual.trail = CreateChild<TrailRenderer>("SwingTrail", pivot);
        visual.trail.emitting = false;
        visual.trail.time = 0.1f;
        visual.trail.minVertexDistance = 0.02f;
        visual.trail.widthMultiplier = 0.12f;
        visual.trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        visual.trail.startColor = new Color(1f, 1f, 1f, 0.7f);
        visual.trail.endColor = new Color(1f, 1f, 1f, 0f);

        visual.muzzleFlash = CreateChild<SpriteRenderer>("MuzzleFlash", pivot);
        visual.muzzleFlash.sprite = CombatSprites.Square;
        visual.muzzleFlash.color = new Color(1f, 0.95f, 0.7f);
        visual.muzzleFlash.transform.localScale = new Vector3(0.18f, 0.18f, 1f);
        visual.muzzleFlash.enabled = false;

        // The weapon is lit like the player; the trail and flash glow so they read in dark rooms.
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

        return visual;
    }

    public void Equip(WeaponData data)
    {
        StopMotion();
        melee = data as MeleeWeaponData;
        ranged = data as RangedWeaponData;
        lastChargeFrame = -10;
        scaleMultiplier = 1f;
        gunVisibleUntil = 0f;
        trail.Clear();
        if (data == null) return;

        bool placeholder = data.sprite == null;
        WeaponRenderer.sprite = placeholder ? CombatSprites.Square : data.sprite;
        baseColor = placeholder ? data.placeholderColor : Color.white;
        baseScale = placeholder
            ? new Vector3(data.placeholderSize.x, data.placeholderSize.y, 1f)
            : Vector3.one * data.spriteScale;
        WeaponRenderer.color = baseColor;
    }

    // The way the player's body faces. Melee poses come from this, and it decides when a gun flips.
    public void SetFacing(Vector2 direction)
    {
        facing = PlayerController.SnapToFourWay(direction);
    }

    // Exact aim for a ranged weapon.
    public void Aim(Vector2 direction)
    {
        aimAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    // Called every frame while charging a melee swing. percent goes 0 to 1; at 1 the weapon flashes.
    public void ShowCharge(float percent, float windupAngle)
    {
        if (melee == null) return;
        StopMotion();
        lastChargeFrame = Time.frameCount;

        // Lift from the resting angle up past Raised, away from the way the swing will travel.
        SwingAngles angles = melee.AnglesFor(facing);
        float windup = angles.raised - SwingDirection(angles) * windupAngle;
        poseAngle = Mathf.Lerp(angles.rest, windup, EaseOutCubic(percent));

        bool full = percent >= 1f;
        scaleMultiplier = 1f + 0.15f * percent;
        kick = Random.insideUnitCircle * (full ? 0.03f : 0.015f * percent);
        WeaponRenderer.color = full && Mathf.Repeat(Time.time * 10f, 1f) > 0.5f ? ChargedColor : baseColor;
    }

    public void PlaySwing(float duration, bool heavy)
    {
        if (melee == null) return;

        // A swing released from a charge starts wherever the windup got to; a quick swing starts at Raised.
        float? startAngle = IsCharging ? poseAngle : (float?)null;
        StopMotion();
        motion = StartCoroutine(SwingRoutine(duration, heavy, startAngle));
    }

    public void PlayRecoil(float distance, float showFor)
    {
        if (ranged == null) return;

        StopMotion();
        gunVisibleUntil = Time.time + showFor;
        motion = StartCoroutine(RecoilRoutine(distance));
        StartCoroutine(MuzzleFlashRoutine());
    }

    void LateUpdate()
    {
        ApplyTransforms();
    }

    void ApplyTransforms()
    {
        if (melee == null && ranged == null)
        {
            WeaponRenderer.enabled = false;
            return;
        }

        // Undo the player's scale so everything under the pivot is measured in world units.
        Vector3 ownerScale = owner.lossyScale;
        transform.localScale = new Vector3(1f / ownerScale.x, 1f / ownerScale.y, 1f);

        Vector2 offset;
        float angle;
        bool behind;

        if (melee != null)
        {
            // The handle sits in the hand and the weapon extends out from it. It never flips.
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

            // The weapon travels around a circle squashed sideways by arcWidth. At 1 (left/right) that's a plain
            // rotation. Below 1 (up/down) the weapon shortens as it passes the middle, so the chop reads as coming
            // toward or going away from the camera instead of sweeping across the player.
            Vector2 pointing = new Vector2(Mathf.Cos(swingAngle) * angles.arcWidth, Mathf.Sin(swingAngle));
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
            // Held out from the attack origin toward the aim, and only shown right after a shot.
            offset = aimOffset;
            angle = aimAngle + tilt;
            behind = Mathf.Sin(angle * Mathf.Deg2Rad) > 0.2f;

            Vector3 scale = baseScale * scaleMultiplier;
            if (facing.x < 0f) scale.y = -scale.y; // keeps gun art upright; only changes when the body turns
            weaponTransform.localScale = scale;
            weaponTransform.localPosition = new Vector3(ranged.holdDistance + ranged.Length * 0.5f + kick.x, kick.y, 0f);
            muzzleFlash.transform.localPosition = new Vector3(MuzzleDistance + 0.06f, 0f, 0f);
            WeaponRenderer.enabled = ranged.showHeldSprite && Time.time < gunVisibleUntil;
        }

        transform.localPosition = new Vector3(offset.x / ownerScale.x, offset.y / ownerScale.y, 0f);
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);

        int baseOrder = ownerRenderer != null ? ownerRenderer.sortingOrder : 0;
        WeaponRenderer.sortingOrder = behind ? baseOrder - 1 : baseOrder + 1;
        trail.sortingOrder = WeaponRenderer.sortingOrder;
        muzzleFlash.sortingOrder = baseOrder + 2;
    }

    IEnumerator SwingRoutine(float duration, bool heavy, float? startAngle)
    {
        SwingAngles angles = melee.AnglesFor(facing);
        float from = startAngle ?? angles.raised;
        float to = angles.end + (heavy ? SwingDirection(angles) * ChargedOverswing : 0f);

        swinging = true;
        WeaponRenderer.color = baseColor;
        scaleMultiplier = heavy ? 1.2f : 1f;
        trail.widthMultiplier = heavy ? 0.2f : 0.12f;
        poseAngle = from;
        ApplyTransforms(); // snap to the start first so the trail doesn't streak from the resting pose
        trail.Clear();
        trail.emitting = true;

        yield return Tween(duration, t => poseAngle = Mathf.Lerp(from, to, EaseOutCubic(t)));

        trail.emitting = false;
        yield return new WaitForSeconds(FollowThroughHold);

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
        // Tilt the barrel up on screen. A gun facing left is flipped, so it tilts the other way.
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

    // +1 if the swing turns counterclockwise from Raised to End, -1 if clockwise.
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

// Placeholder art, effect textures, and materials generated at runtime, so combat works before real art exists.
public static class CombatSprites
{
    private static Sprite square;
    private static Material effectMaterial;
    private static Material flashMaterial;
    private static Texture2D ringTexture;
    private static Texture2D softCircleTexture;
    private static Texture2D vignetteTexture;

    // A white square, 1 world unit across. Tint and scale it into any shape.
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

    // Unlit, so trails, flashes, and bullets stay visible in dark rooms. Null if no such shader is available.
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

    // Draws a sprite as a solid silhouette in the renderer's color. Null if the Sonny/Sprite Flash shader is missing.
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

    // A thin soft-edged ring, for impact pops.
    public static Texture2D RingTexture =>
        ringTexture != null ? ringTexture : (ringTexture = MakeRadialTexture(64, r => Mathf.Clamp01(1f - Mathf.Abs(r - 0.78f) / 0.14f)));

    // A blurry dot, for smoke.
    public static Texture2D SoftCircleTexture =>
        softCircleTexture != null ? softCircleTexture : (softCircleTexture = MakeRadialTexture(64, r => Mathf.Pow(Mathf.Clamp01(1f - r), 2f)));

    // Clear in the middle, solid toward the edges, for full-screen hurt flashes.
    public static Texture2D VignetteTexture =>
        vignetteTexture != null ? vignetteTexture : (vignetteTexture = MakeRadialTexture(128, r => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1.3f, r))));

    // A white texture whose alpha depends on distance from the center (0 in the middle, 1 at the edge's midpoint).
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
