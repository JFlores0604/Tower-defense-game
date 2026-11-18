using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  Enemy.cs — walks the predetermined path, takes damage, burns, slows,
//  explodes into physics bricks when destroyed.
// ============================================================================
[RequireComponent(typeof(Rigidbody))]
public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new List<Enemy>();

    public EnemyDef def;
    public bool isBoss;
    public bool isEmperor;            // Shadow Emperor: takes half damage from all but the Jade Ninja
    float immuneTextTimer;
    public float maxHp;
    public float hp;
    public float distance;            // how far along the path we are
    public bool Dead { get; private set; }

    EnemyPath path;
    Transform model;
    Transform legL, legR;
    float walkCycle;
    float hitPulse;

    // status effects
    float slowFactor, slowTimer;
    float burnDps, burnTimer;
    Tower burnSource;

    public float CurrentSpeed => def.speed * (slowTimer > 0f ? 1f - slowFactor : 1f);
    /// Current world velocity — used by towers to predict where to aim.
    public Vector3 Velocity => Dead ? Vector3.zero : path.TangentAt(distance) * CurrentSpeed;
    public Vector3 AimPoint => transform.position + Vector3.up * (0.6f * def.size);
    public bool IsSlowed => slowTimer > 0f;
    public bool IsBurning => burnTimer > 0f;
    public float HpFraction => Mathf.Clamp01(hp / maxHp);

    public static Enemy Spawn(Transform parent, EnemyPath path, EnemyDef def, int wave, bool boss)
    {
        var go = new GameObject(def.name);
        go.transform.SetParent(parent, false);
        var e = go.AddComponent<Enemy>();
        e.def = def;
        e.isBoss = boss;
        e.path = path;
        e.maxHp = def.maxHp * GameData.HpScale(wave);
        e.hp = e.maxHp;
        e.distance = 0f;
        go.transform.position = path.PositionAt(0f);

        var rb = go.GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0f, 0.6f * def.size, 0f);
        col.size = new Vector3(0.9f, 1.2f, 0.9f) * def.size;

        e.BuildModel();
        All.Add(e);
        return e;
    }

    void OnDestroy() { All.Remove(this); }

    // ------------------------------------------------------------------ model
    void BuildModel()
    {
        model = new GameObject("Model").transform;
        model.SetParent(transform, false);
        model.localScale = Vector3.one * def.size;
        Color body = def.bodyColor, trim = def.trimColor;
        Color dark = BlockBuilder.Darker(body, 0.55f);

        legL = new GameObject("LegL").transform; legL.SetParent(model, false); legL.localPosition = new Vector3(-0.17f, 0.45f, 0f);
        legR = new GameObject("LegR").transform; legR.SetParent(model, false); legR.localPosition = new Vector3(0.17f, 0.45f, 0f);
        BlockBuilder.Block(legL, new Vector3(0f, -0.22f, 0f), new Vector3(0.24f, 0.45f, 0.28f), dark);
        BlockBuilder.Block(legR, new Vector3(0f, -0.22f, 0f), new Vector3(0.24f, 0.45f, 0.28f), dark);

        string n = def.name;
        if (n.Contains("Imp"))
        {
            BlockBuilder.Block(model, new Vector3(0f, 0.65f, 0f), new Vector3(0.55f, 0.45f, 0.45f), body);
            BlockBuilder.Block(model, new Vector3(0f, 1.0f, 0.05f), new Vector3(0.5f, 0.4f, 0.45f), body);
            BlockBuilder.Block(model, new Vector3(-0.12f, 1.02f, 0.28f), new Vector3(0.1f, 0.08f, 0.05f), trim, 4f);
            BlockBuilder.Block(model, new Vector3(0.12f, 1.02f, 0.28f), new Vector3(0.1f, 0.08f, 0.05f), trim, 4f);
            BlockBuilder.Block(model, new Vector3(-0.2f, 1.3f, 0f), new Vector3(0.08f, 0.25f, 0.08f), dark);
            BlockBuilder.Block(model, new Vector3(0.2f, 1.3f, 0f), new Vector3(0.08f, 0.25f, 0.08f), dark);
            BlockBuilder.Block(model, new Vector3(0f, 0.75f, -0.32f), new Vector3(0.1f, 0.1f, 0.3f), dark); // tail
        }
        else if (n.Contains("Samurai"))
        {
            BlockBuilder.Block(model, new Vector3(0f, 0.75f, 0f), new Vector3(0.62f, 0.6f, 0.4f), body);
            BlockBuilder.Block(model, new Vector3(0f, 0.55f, 0f), new Vector3(0.66f, 0.12f, 0.44f), trim);
            BlockBuilder.Block(model, new Vector3(-0.42f, 0.95f, 0f), new Vector3(0.25f, 0.12f, 0.45f), trim);   // shoulder plates
            BlockBuilder.Block(model, new Vector3(0.42f, 0.95f, 0f), new Vector3(0.25f, 0.12f, 0.45f), trim);
            BlockBuilder.Block(model, new Vector3(-0.4f, 0.7f, 0f), new Vector3(0.16f, 0.45f, 0.18f), body);
            BlockBuilder.Block(model, new Vector3(0.4f, 0.7f, 0f), new Vector3(0.16f, 0.45f, 0.18f), body);
            BlockBuilder.Block(model, new Vector3(0f, 1.25f, 0f), new Vector3(0.4f, 0.38f, 0.38f), new Color(0.15f, 0.15f, 0.15f));
            BlockBuilder.Block(model, new Vector3(-0.09f, 1.26f, 0.2f), new Vector3(0.08f, 0.05f, 0.02f), new Color(1f, 0.9f, 0.2f), 4f);
            BlockBuilder.Block(model, new Vector3(0.09f, 1.26f, 0.2f), new Vector3(0.08f, 0.05f, 0.02f), new Color(1f, 0.9f, 0.2f), 4f);
            BlockBuilder.Block(model, new Vector3(0f, 1.47f, 0f), new Vector3(0.6f, 0.08f, 0.55f), body);       // helmet brim
            BlockBuilder.Block(model, new Vector3(0f, 1.55f, 0f), new Vector3(0.42f, 0.14f, 0.42f), body);
            BlockBuilder.Block(model, new Vector3(0f, 1.7f, 0.12f), new Vector3(0.45f, 0.2f, 0.05f), new Color(1f, 0.8f, 0.2f), 0.5f); // crest
            BlockBuilder.Block(model, new Vector3(0.5f, 0.75f, 0.35f), new Vector3(0.05f, 0.05f, 0.9f), new Color(0.85f, 0.85f, 0.9f)); // sword
        }
        else // Golem / Warlord
        {
            BlockBuilder.Block(model, new Vector3(0f, 0.85f, 0f), new Vector3(0.85f, 0.75f, 0.6f), body);
            BlockBuilder.Block(model, new Vector3(0f, 0.85f, 0.31f), new Vector3(0.3f, 0.3f, 0.05f), trim, 3f);   // glowing core
            BlockBuilder.Block(model, new Vector3(-0.58f, 0.75f, 0f), new Vector3(0.3f, 0.8f, 0.35f), dark);
            BlockBuilder.Block(model, new Vector3(0.58f, 0.75f, 0f), new Vector3(0.3f, 0.8f, 0.35f), dark);
            BlockBuilder.Block(model, new Vector3(-0.58f, 0.3f, 0f), new Vector3(0.36f, 0.25f, 0.4f), body);     // fists
            BlockBuilder.Block(model, new Vector3(0.58f, 0.3f, 0f), new Vector3(0.36f, 0.25f, 0.4f), body);
            BlockBuilder.Block(model, new Vector3(0f, 1.38f, 0.05f), new Vector3(0.38f, 0.3f, 0.35f), dark);
            BlockBuilder.Block(model, new Vector3(-0.09f, 1.4f, 0.23f), new Vector3(0.09f, 0.06f, 0.02f), trim, 4f);
            BlockBuilder.Block(model, new Vector3(0.09f, 1.4f, 0.23f), new Vector3(0.09f, 0.06f, 0.02f), trim, 4f);
            if (isBoss)
            {
                var gold = new Color(1f, 0.8f, 0.2f);
                for (int i = -1; i <= 1; i++)
                    BlockBuilder.Block(model, new Vector3(i * 0.13f, 1.65f, 0.05f), new Vector3(0.08f, 0.22f, 0.08f), gold, 1f);
                BlockBuilder.Block(model, new Vector3(0f, 0.85f, -0.36f), new Vector3(0.9f, 0.9f, 0.05f), new Color(0.6f, 0.05f, 0.05f)); // cape
            }
        }
    }

    // ------------------------------------------------------------------ update
    void Update()
    {
        if (Dead) return;
        float dt = Time.deltaTime;

        if (slowTimer > 0f) slowTimer -= dt;
        if (immuneTextTimer > 0f) immuneTextTimer -= dt;
        if (burnTimer > 0f)
        {
            burnTimer -= dt;
            TakeDamage(burnDps * dt, burnSource, false);
            if (Dead) return;
        }

        distance += CurrentSpeed * dt;
        if (distance >= path.TotalLength)
        {
            Dead = true;
            GameManager.Instance.OnEnemyLeaked(this);
            Destroy(gameObject);
            return;
        }

        transform.position = path.PositionAt(distance);
        Vector3 fwd = path.TangentAt(distance);
        if (fwd.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fwd), 10f * dt);

        // simple walk animation: leg swing + bob
        walkCycle += dt * CurrentSpeed * 4f / def.size;
        float swing = Mathf.Sin(walkCycle) * 35f;
        legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
        legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        float bob = Mathf.Abs(Mathf.Sin(walkCycle)) * 0.06f * def.size;
        hitPulse = Mathf.MoveTowards(hitPulse, 0f, dt * 6f);
        model.localPosition = new Vector3(0f, bob, 0f);
        model.localScale = Vector3.one * def.size * (1f + hitPulse * 0.15f);
    }

    /// Returns the damage actually dealt (capped by remaining HP) so towers can earn EXP for it.
    public float TakeDamage(float amount, Tower source, bool pulse = true, bool force = false)
    {
        if (Dead || amount <= 0f) return 0f;
        if (isEmperor && !force && (source == null || source.def.element != Element.Energy))
        {
            amount *= GameData.EmperorResist;          // everyone but the Jade Ninja is halved
            if (immuneTextTimer <= 0f)
            {
                Effects.Text(transform.position + Vector3.up * (2f * def.size), "RESISTED x0.5", new Color(0.8f, 0.4f, 1f), 0.8f, 1f);
                immuneTextTimer = 1.2f;
            }
        }
        float dealt = Mathf.Min(amount, hp);
        hp -= amount;
        if (pulse) hitPulse = 1f;
        if (source != null) { source.totalDamage += dealt; source.AddExp(dealt * GameData.ExpPerDamage); }
        if (hp <= 0f) Die(source);
        return dealt;
    }

    public void ApplySlow(float factor, float duration)
    {
        slowFactor = Mathf.Max(slowFactor * (slowTimer > 0f ? 1f : 0f), factor);
        slowTimer = Mathf.Max(slowTimer, duration);
    }

    public void ApplyBurn(float dps, float duration, Tower source)
    {
        burnDps = Mathf.Max(burnTimer > 0f ? burnDps : 0f, dps);
        burnTimer = Mathf.Max(burnTimer, duration);
        burnSource = source;
    }

    void Die(Tower killer)
    {
        if (Dead) return;
        Dead = true;
        if (killer != null) { killer.AddExp(def.expReward); killer.kills++; }
        GameManager.Instance.OnEnemyKilled(this, killer);
        Effects.BrickExplosion(transform.position + Vector3.up * 0.6f * def.size, def.bodyColor, def.trimColor,
                               isBoss ? 30 : 10, def.size);
        Destroy(gameObject);
    }
}
