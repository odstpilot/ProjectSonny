using UnityEngine;

// Anything combat can hurt: robots, the player, breakable props.
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}

public struct DamageInfo
{
    public float amount;
    public Vector2 direction;   // points away from the attacker
    public float knockback;     // how far the target gets pushed, in world units
    public GameObject source;

    public DamageInfo(float amount, Vector2 direction, float knockback, GameObject source)
    {
        this.amount = amount;
        this.direction = direction;
        this.knockback = knockback;
        this.source = source;
    }
}

public static class Damageable
{
    // Solid colliders count for their whole object hierarchy. Trigger colliders only count for their
    // own object, so something like a robot's detection trigger doesn't soak up hits meant for its body.
    public static IDamageable FromCollider(Collider2D collider)
    {
        return collider.isTrigger
            ? collider.GetComponent<IDamageable>()
            : collider.GetComponentInParent<IDamageable>();
    }
}
