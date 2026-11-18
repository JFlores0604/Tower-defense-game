using UnityEngine;

// ============================================================================
//  Projectile.cs — elemental blasts thrown by the ninjas.
//  Real 3D physics: a Rigidbody moving with a fixed velocity and a trigger
//  collider that detects enemy hits. Each element has its own on-hit effect.
// ============================================================================
[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    Tower source;
    Element element;
    float damage;
    float life = 3f;
    bool spent;
    Transform visual;
    Vector3 spin;

    public static Projectile Spawn(Tower source, Vector3 pos, Vector3 velocity)
    {
        var go = new GameObject(source.def.element + "Blast");
        if (Effects.Root) go.transform.SetParent(Effects.Root, true);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(velocity);

        var p = go.AddComponent<Projectile>();
        p.source = source;
        p.element = source.def.element;
        p.damage = source.Damage;
        p.life = source.Range * 1.8f / Mathf.Max(velocity.magnitude, 0.1f) + 0.3f;

        var rb = go.GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = velocity;
#else
        rb.velocity = velocity;
#endif
        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = p.element == Element.Earth || p.element == Element.Energy ? 0.35f : 0.25f;

        p.BuildVisual(source.def.accentColor, source.def.mainColor);
        return p;
    }

    void BuildVisual(Color accent, Color main)
    {
        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        Color trailColor = accent;
        switch (element)
        {
            case Element.Fire:
                BlockBuilder.Block(visual, Vector3.zero, Vector3.one * 0.28f, new Color(1f, 0.45f, 0.05f), 4f);
                BlockBuilder.Block(visual, Vector3.zero, Vector3.one * 0.18f, new Color(1f, 0.9f, 0.3f), 5f).transform.localRotation = Quaternion.Euler(45, 45, 0);
                spin = new Vector3(0f, 0f, 720f);
                trailColor = new Color(1f, 0.5f, 0.1f);
                break;
            case Element.Ice:
                BlockBuilder.Block(visual, Vector3.zero, new Vector3(0.12f, 0.12f, 0.55f), new Color(0.7f, 0.95f, 1f), 2.5f);
                BlockBuilder.Block(visual, new Vector3(0f, 0f, -0.1f), new Vector3(0.22f, 0.04f, 0.22f), Color.white, 2f).transform.localRotation = Quaternion.Euler(0, 45, 0);
                spin = new Vector3(0f, 0f, 540f);
                trailColor = new Color(0.7f, 0.95f, 1f);
                break;
            case Element.Earth:
                BlockBuilder.Brick(visual, new Vector3(0f, -0.2f, 0f), 2, 2, 0.4f, new Color(0.45f, 0.33f, 0.2f), 0.25f, "Boulder");
                spin = new Vector3(400f, 120f, 0f);
                trailColor = new Color(0.55f, 0.45f, 0.3f);
                break;
            case Element.Lightning:
                BlockBuilder.Block(visual, Vector3.zero, new Vector3(0.12f, 0.12f, 0.4f), new Color(1f, 1f, 0.6f), 6f);
                spin = new Vector3(0f, 0f, 900f);
                trailColor = new Color(1f, 0.95f, 0.3f);
                break;
            case Element.Energy:
                BlockBuilder.Block(visual, Vector3.zero, Vector3.one * 0.38f, new Color(0.3f, 1f, 0.45f), 5f);
                BlockBuilder.Block(visual, Vector3.zero, Vector3.one * 0.24f, Color.white, 6f).transform.localRotation = Quaternion.Euler(45, 45, 0);
                spin = new Vector3(300f, 500f, 0f);
                trailColor = new Color(0.45f, 1f, 0.55f);
                break;
        }

        var tr = gameObject.AddComponent<TrailRenderer>();
        tr.sharedMaterial = BlockBuilder.LineMat();
        tr.time = element == Element.Lightning ? 0.12f : 0.2f;
        tr.startWidth = element == Element.Earth || element == Element.Energy ? 0.3f : 0.18f;
        tr.endWidth = 0f;
        tr.startColor = trailColor;
        tr.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (visual) visual.Rotate(spin * Time.deltaTime, Space.Self);
        if (life <= 0f || transform.position.y < -0.5f) Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (spent) return;
        var enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.Dead) return;
        spent = true;
        Hit(enemy);
        Destroy(gameObject);
    }

    void Hit(Enemy enemy)
    {
        Tower src = source;   // may be null if the tower was sold mid-flight
        Vector3 pos = enemy.AimPoint;
        switch (element)
        {
            case Element.Fire:
                enemy.TakeDamage(damage, src);
                if (src != null) enemy.ApplyBurn(src.BurnDps, 2f, src);
                Effects.HitSpark(pos, new Color(1f, 0.5f, 0.1f), 6);
                break;

            case Element.Ice:
                enemy.TakeDamage(damage, src);
                enemy.ApplySlow(src != null ? src.SlowAmount : 0.35f, 1.6f);
                Effects.HitSpark(pos, new Color(0.75f, 0.95f, 1f), 6);
                Effects.Play("ice_hit", 0.35f);
                break;

            case Element.Earth:
            {
                float radius = src != null ? src.SplashRadius : 1.8f;
                enemy.TakeDamage(damage, src);
                var list = Enemy.All.ToArray();
                foreach (var e in list)
                {
                    if (e == null || e == enemy || e.Dead) continue;
                    Vector3 d = e.transform.position - enemy.transform.position; d.y = 0f;
                    if (d.magnitude <= radius) e.TakeDamage(damage * 0.6f, src);
                }
                Effects.Shockwave(enemy.transform.position, radius, new Color(0.6f, 0.45f, 0.25f));
                Effects.BrickExplosion(pos, new Color(0.45f, 0.33f, 0.2f), new Color(0.35f, 0.6f, 0.25f), 4, 0.7f);
                Effects.Play("earth_hit", 0.6f);
                break;
            }

            case Element.Lightning:
            {
                enemy.TakeDamage(damage, src);
                Effects.HitSpark(pos, new Color(1f, 0.95f, 0.3f), 6);
                int chains = src != null ? src.ChainCount : 2;
                float dmg = damage;
                Enemy current = enemy;
                var hit = new System.Collections.Generic.HashSet<Enemy> { enemy };
                for (int i = 0; i < chains; i++)
                {
                    Enemy next = null;
                    float best = 3.5f * 3.5f;
                    foreach (var e in Enemy.All)
                    {
                        if (e == null || e.Dead || hit.Contains(e)) continue;
                        float d = (e.transform.position - current.transform.position).sqrMagnitude;
                        if (d < best) { best = d; next = e; }
                    }
                    if (next == null) break;
                    dmg *= 0.7f;
                    Effects.Bolt(current.AimPoint, next.AimPoint, new Color(1f, 0.95f, 0.3f));
                    hit.Add(next);
                    current = next;
                    next.TakeDamage(dmg, src);
                }
                break;
            }

            case Element.Energy:
            {
                float radius = src != null ? src.SplashRadius * 0.8f : 1.5f;
                enemy.TakeDamage(damage, src);
                foreach (var e in Enemy.All.ToArray())
                {
                    if (e == null || e == enemy || e.Dead) continue;
                    Vector3 d = e.transform.position - enemy.transform.position; d.y = 0f;
                    if (d.magnitude <= radius) e.TakeDamage(damage * 0.5f, src);
                }
                Effects.Shockwave(enemy.transform.position, radius, new Color(0.35f, 1f, 0.5f));
                Effects.HitSpark(pos, new Color(0.5f, 1f, 0.6f), 10);
                Effects.Play("energy_hit", 0.6f);
                break;
            }
        }
    }
}
