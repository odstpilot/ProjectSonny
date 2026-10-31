using UnityEngine;

// anything combat can hurt
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}

public struct DamageInfo
{
    public float amount;
    public Vector2 direction;
    public float knockback;
    public GameObject source;
    public Vector2? point;

    public DamageInfo(float amount, Vector2 direction, float knockback, GameObject source, Vector2? point = null)
    {
        this.amount = amount;
        this.direction = direction;
        this.knockback = knockback;
        this.source = source;
        this.point = point;
    }
}

public static class Damageable
{
    public static IDamageable FromCollider(Collider2D collider)
    {
        Health health = collider.isTrigger ? collider.GetComponent<Health>() : collider.GetComponentInParent<Health>();
        if (health != null) return health;

        return collider.isTrigger
            ? collider.GetComponent<IDamageable>()
            : collider.GetComponentInParent<IDamageable>();
    }
}
