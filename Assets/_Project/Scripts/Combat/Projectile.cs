using System.Collections.Generic;
using UnityEngine;

// Flies straight and sweeps a circle cast each frame, so fast shots can't skip through thin walls or small enemies.
// Blaster shots are balls of energy that blow up where they land, hurting everything around the impact.
public class Projectile : MonoBehaviour
{
    private float damage;
    private float knockback;
    private float speed;
    private float radius;
    private float remainingRange;
    private Vector2 direction;
    private GameObject owner;
    private BlasterWeaponData blaster;
    private bool finished;

    // Starts at the shooter's center and immediately travels out to the muzzle, so firing while
    // pressed against a wall hits the wall instead of spawning on the far side of it.
    public static Projectile Spawn(RangedWeaponData data, Vector3 origin, Vector2 direction, float muzzleDistance,
        GameObject owner, SpriteRenderer sortingSource)
    {
        var go = new GameObject(data.displayName + " Projectile");
        go.transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));

        var blaster = data as BlasterWeaponData;
        EnergyOrb orb = null;
        if (blaster != null)
        {
            orb = EnergyOrb.Create("Energy Ball", go.transform, blaster, sortingSource, true);
        }
        else
        {
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            if (data.projectileSprite != null)
            {
                spriteRenderer.sprite = data.projectileSprite;
            }
            else
            {
                spriteRenderer.sprite = CombatSprites.Square;
                spriteRenderer.color = data.projectileColor;
                go.transform.localScale = new Vector3(data.projectileSize.x, data.projectileSize.y, 1f);
            }

            Material material = CombatSprites.EffectMaterial;
            if (material != null) spriteRenderer.sharedMaterial = material;
            else if (sortingSource != null) spriteRenderer.sharedMaterial = sortingSource.sharedMaterial;
            if (sortingSource != null)
            {
                spriteRenderer.sortingLayerID = sortingSource.sortingLayerID;
                spriteRenderer.sortingOrder = sortingSource.sortingOrder + 1;
            }
        }

        var projectile = go.AddComponent<Projectile>();
        projectile.damage = data.damage;
        projectile.knockback = data.knockback;
        projectile.speed = data.projectileSpeed;
        projectile.radius = data.projectileRadius;
        projectile.remainingRange = data.projectileRange;
        projectile.direction = direction.normalized;
        projectile.owner = owner;
        projectile.blaster = blaster;
        projectile.Advance(muzzleDistance);
        if (orb != null) orb.ClearTrail();
        return projectile;
    }

    void Update()
    {
        Advance(speed * Time.deltaTime);
    }

    void Advance(float distance)
    {
        if (finished) return;

        Vector2 position = transform.position;
        foreach (RaycastHit2D hit in Physics2D.CircleCastAll(position, radius, direction, distance))
        {
            if (owner != null && hit.collider.transform.IsChildOf(owner.transform)) continue;

            // Fly through trigger zones; stop on anything solid or anything that can be hurt.
            IDamageable target = Damageable.FromCollider(hit.collider);
            if (target == null && hit.collider.isTrigger) continue;

            Land(hit.centroid, hit.point, target);
            return;
        }

        transform.position += (Vector3)(direction * distance);
        remainingRange -= distance;
        if (remainingRange <= 0f)
        {
            if (blaster != null) Dissipate(transform.position);
            Finish();
        }
    }

    // center is where the projectile was when it touched; point is the spot on the surface it touched.
    void Land(Vector2 center, Vector2 point, IDamageable target)
    {
        if (target != null)
            target.TakeDamage(new DamageInfo(damage, direction, knockback, owner, point));

        if (blaster != null)
            Explode(center, target);
        Finish();
    }

    // Everything in the blast radius that isn't behind a wall gets hurt, less toward the edge.
    void Explode(Vector2 center, IDamageable directHit)
    {
        bool hurtSomething = directHit != null;

        if (blaster.blastRadius > 0f)
        {
            var hurt = new HashSet<IDamageable>();
            if (directHit != null) hurt.Add(directHit);

            foreach (Collider2D col in Physics2D.OverlapCircleAll(center, blaster.blastRadius))
            {
                if (owner != null && col.transform.IsChildOf(owner.transform)) continue;

                IDamageable target = Damageable.FromCollider(col);
                if (target == null || hurt.Contains(target)) continue;

                Vector2 closest = col.ClosestPoint(center);
                if (IsShielded(center, closest, col)) continue;

                hurt.Add(target);
                float edge = Mathf.Clamp01(Vector2.Distance(center, closest) / blaster.blastRadius);
                Vector2 push = (Vector2)col.bounds.center - center;
                if (push.sqrMagnitude < 0.0001f) push = direction;
                float amount = damage * Mathf.Lerp(1f, blaster.blastEdgeDamage, edge);
                target.TakeDamage(new DamageInfo(amount, push.normalized, knockback, owner, closest));
                hurtSomething = true;
            }
        }

        float size = Mathf.Max(blaster.blastRadius, blaster.ballSize);
        HitEffects.Ring(center, blaster.energyColor, size * 2.6f);
        HitEffects.Ring(center, blaster.coreColor, size * 1.3f);
        HitEffects.Sparks(center, Vector2.up, 30, 9f, 360f, blaster.coreColor, blaster.energyColor);
        LightFlash.Spawn(center, blaster.energyColor, size * 3f, blaster.glowIntensity * 2.5f, 0.35f);
        CameraShake.Shake(blaster.blastShake);
        if (hurtSomething) HitStop.Freeze(blaster.blastHitStop);
    }

    // Out of range: the ball comes apart harmlessly.
    void Dissipate(Vector2 at)
    {
        Color faded = new Color(blaster.energyColor.r, blaster.energyColor.g, blaster.energyColor.b, 0.6f);
        HitEffects.Ring(at, faded, blaster.ballSize * 2f);
        HitEffects.Sparks(at, direction, 10, 3f, 360f, blaster.coreColor, blaster.energyColor);
        LightFlash.Spawn(at, blaster.energyColor, blaster.glowRadius, blaster.glowIntensity, 0.2f);
    }

    bool IsShielded(Vector2 from, Vector2 to, Collider2D target)
    {
        foreach (RaycastHit2D hit in Physics2D.LinecastAll(from, to, blaster.blastObstacleMask))
        {
            if (hit.collider == target) return false;
            if (hit.collider.isTrigger) continue;
            if (owner != null && hit.collider.transform.IsChildOf(owner.transform)) continue;
            if (Damageable.FromCollider(hit.collider) != null) continue; // enemies don't shield each other
            return true;
        }
        return false;
    }

    void Finish()
    {
        finished = true;
        Destroy(gameObject);
    }
}
