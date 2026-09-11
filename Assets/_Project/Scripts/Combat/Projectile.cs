using UnityEngine;

// Flies straight and sweeps a circle cast each frame, so fast shots can't skip through thin walls or small enemies.
public class Projectile : MonoBehaviour
{
    private float damage;
    private float knockback;
    private float speed;
    private float radius;
    private float remainingRange;
    private Vector2 direction;
    private GameObject owner;
    private bool finished;

    // Starts at the shooter's center and immediately travels out to the muzzle, so firing while
    // pressed against a wall hits the wall instead of spawning on the far side of it.
    public static Projectile Spawn(RangedWeaponData data, Vector3 origin, Vector2 direction, float muzzleDistance,
        GameObject owner, SpriteRenderer sortingSource)
    {
        var go = new GameObject(data.displayName + " Projectile");
        go.transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));

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

        var projectile = go.AddComponent<Projectile>();
        projectile.damage = data.damage;
        projectile.knockback = data.knockback;
        projectile.speed = data.projectileSpeed;
        projectile.radius = data.projectileRadius;
        projectile.remainingRange = data.projectileRange;
        projectile.direction = direction.normalized;
        projectile.owner = owner;
        projectile.Advance(muzzleDistance);
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

            IDamageable target = Damageable.FromCollider(hit.collider);
            if (target != null)
            {
                target.TakeDamage(new DamageInfo(damage, direction, knockback, owner, hit.point));
                Finish();
                return;
            }

            // Fly through trigger zones; stop on anything solid.
            if (!hit.collider.isTrigger)
            {
                Finish();
                return;
            }
        }

        transform.position += (Vector3)(direction * distance);
        remainingRange -= distance;
        if (remainingRange <= 0f) Finish();
    }

    void Finish()
    {
        finished = true;
        Destroy(gameObject);
    }
}
