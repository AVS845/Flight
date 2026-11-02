using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class LocalBullet2D : MonoBehaviour
{
    public bool dealsDamage = true;
    private Rigidbody2D rb;
    private CircleCollider2D bulletCollider;
    private bool isOwnerBullet;
    private float damage;
    private float knockbackForce;
    private PlayerController shooterScript;
    private Despawn despawner;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bulletCollider = GetComponent<CircleCollider2D>();
        despawner = GetComponent<Despawn>();
    }

    public void Initialize(Vector2 velocity, bool isOwner, float dmg, float knockback, Collider2D shooterCollider, PlayerController shooter)
    {
        rb.linearVelocity = velocity;
        isOwnerBullet = isOwner;
        damage = dmg;
        knockbackForce = knockback;
        shooterScript = shooter;

        if (shooterCollider != null)
        {
            Physics2D.IgnoreCollision(shooterCollider, bulletCollider, true);
        }

        despawner.StartLife();
        dealsDamage = true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (dealsDamage)
        {
            if (isOwnerBullet)
            {
                if (collision.gameObject.TryGetComponent(out NetworkObject targetNetObj))
                {
                    Vector2 knockbackDir = rb.linearVelocity.normalized;
                    
                    shooterScript.ReportHitRpc(targetNetObj.NetworkObjectId, damage, knockbackDir * knockbackForce);
                }
            }

            despawner.StartDespawn();

            dealsDamage = false;
        }
    }

    private void Despawn()
    {
        StopAllCoroutines();
        gameObject.SetActive(false); 
    }
}
