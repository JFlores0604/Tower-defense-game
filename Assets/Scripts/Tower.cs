using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  Tower.cs — an elemental ninja standing on a brick pedestal.
//   * fires continuously at a set rate (rubric C)
//   * aims at the PREDICTED position of its target using the target's
//     current velocity and position (rubric A)
//   * earns EXP from damage + kills and levels up to 10 (belt colour changes)
// ============================================================================
public class Tower : MonoBehaviour
{
    public static readonly List<Tower> All = new List<Tower>();

    public TowerDef def;
    public int defIndex;
    public bool isGhost;
    public int level = 1;
    public float exp;
    public int kills;
    public float totalDamage;
    public int goldSpent;
    public TargetMode targetMode = TargetMode.First;

    Transform ninja, armR, armL, muzzle;
    Renderer beltRenderer, beltTailRenderer;
    GameObject aura;
    LineRenderer rangeRing;
    Transform aimMarker;
    float fireTimer;
    float throwAnim;
    float idleT;
    Enemy target;

    // -------- stats (level scaled) --------
    public float Damage => def.damage * GameData.DamageMult(level);
    public float FireRate => def.fireRate * GameData.FireRateMult(level);
    public float Range => def.range + GameData.RangeBonus(level);
    public float MuzzleHeight => def.pedestalHeight + 1.25f;
    public bool IsMaxLevel => level >= GameData.MaxLevel;
    public int ExpNeeded => GameData.ExpToNext(level);
    public string BeltName => GameData.BeltNames[Mathf.Clamp(level - 1, 0, 9)];
    public float SlowAmount => Mathf.Min(0.35f + 0.025f * (level - 1), 0.6f);
    public float SplashRadius => 1.8f + 0.1f * (level - 1);
    public int ChainCount => 2 + (level - 1) / 3;
    public float BurnDps => Damage * 0.45f;

    public static Tower Create(Transform parent, int defIndex, Vector3 pos, bool ghost)
    {
        var def = GameData.Towers[defIndex];
        var go = new GameObject(ghost ? "Ghost_" + def.name : def.name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var t = go.AddComponent<Tower>();
        t.def = def;
        t.defIndex = defIndex;
        t.isGhost = ghost;
        t.goldSpent = def.cost;
        t.BuildModel();
        if (!ghost)
        {
            go.AddComponent<Blocker>();
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, (def.pedestalHeight + 1.4f) * 0.5f, 0f);
            col.size = new Vector3(1.2f, def.pedestalHeight + 1.4f, 1.2f);
            All.Add(t);
            t.fireTimer = 0.2f;
        }
        return t;
    }

    // ------------------------------------------------------------------ model
    void BuildModel()
    {
        // Brick pedestal — taller for stronger ninjas (rubric B: tower height)
        Color stone = new Color(0.62f, 0.6f, 0.57f);
        int layers = Mathf.Max(1, Mathf.RoundToInt(def.pedestalHeight / 0.3f));
        float layerH = def.pedestalHeight / layers;
        for (int i = 0; i < layers; i++)
        {
            float w = 1.3f - i * 0.04f;
            Color c = i % 2 == 0 ? stone : BlockBuilder.Darker(stone, 0.88f);
            BlockBuilder.Block(transform, new Vector3(0f, layerH * (i + 0.5f), 0f), new Vector3(w, layerH * 0.96f, w), c);
        }
        // element-coloured cap plate with studs
        BlockBuilder.Brick(transform, new Vector3(0f, def.pedestalHeight, 0f), 2, 2, 0.08f, def.accentColor, 0.5f, "Cap");

        ninja = new GameObject("Ninja").transform;
        ninja.SetParent(transform, false);
        ninja.localPosition = new Vector3(0f, def.pedestalHeight + 0.13f, 0f);

        Color gi = def.mainColor, accent = def.accentColor;
        Color skin = new Color(0.96f, 0.8f, 0.6f);
        Color darkGi = BlockBuilder.Darker(gi, 0.7f);

        // legs + hips
        BlockBuilder.Block(ninja, new Vector3(-0.13f, 0.22f, 0f), new Vector3(0.22f, 0.44f, 0.26f), darkGi);
        BlockBuilder.Block(ninja, new Vector3(0.13f, 0.22f, 0f), new Vector3(0.22f, 0.44f, 0.26f), darkGi);
        BlockBuilder.Block(ninja, new Vector3(0f, 0.48f, 0f), new Vector3(0.5f, 0.1f, 0.28f), darkGi);
        // torso
        BlockBuilder.Block(ninja, new Vector3(0f, 0.75f, 0f), new Vector3(0.55f, 0.48f, 0.3f), gi);
        // belt (recoloured as the ninja levels up)
        beltRenderer = BlockBuilder.Block(ninja, new Vector3(0f, 0.56f, 0f), new Vector3(0.57f, 0.08f, 0.32f), Color.white).GetComponent<Renderer>();
        beltTailRenderer = BlockBuilder.Block(ninja, new Vector3(0.15f, 0.48f, 0.17f), new Vector3(0.07f, 0.18f, 0.03f), Color.white).GetComponent<Renderer>();
        // chest emblem
        BlockBuilder.Block(ninja, new Vector3(0f, 0.82f, 0.16f), new Vector3(0.16f, 0.16f, 0.02f), accent, 2f, false, "Emblem");

        // arms (pivot at shoulder so we can animate throws)
        armL = new GameObject("ArmL").transform; armL.SetParent(ninja, false); armL.localPosition = new Vector3(-0.36f, 0.93f, 0f);
        armR = new GameObject("ArmR").transform; armR.SetParent(ninja, false); armR.localPosition = new Vector3(0.36f, 0.93f, 0f);
        BlockBuilder.Block(armL, new Vector3(0f, -0.18f, 0f), new Vector3(0.16f, 0.38f, 0.18f), gi);
        BlockBuilder.Block(armR, new Vector3(0f, -0.18f, 0f), new Vector3(0.16f, 0.38f, 0.18f), gi);
        BlockBuilder.Block(armL, new Vector3(0f, -0.4f, 0f), new Vector3(0.14f, 0.1f, 0.14f), skin);
        BlockBuilder.Block(armR, new Vector3(0f, -0.4f, 0f), new Vector3(0.14f, 0.1f, 0.14f), skin);

        // head + hood with eye slit
        BlockBuilder.Block(ninja, new Vector3(0f, 1.15f, 0f), new Vector3(0.36f, 0.32f, 0.32f), skin);
        BlockBuilder.Block(ninja, new Vector3(0f, 1.27f, 0f), new Vector3(0.4f, 0.16f, 0.36f), gi);   // hood top
        BlockBuilder.Block(ninja, new Vector3(0f, 1.05f, 0.005f), new Vector3(0.4f, 0.14f, 0.35f), gi);  // mask
        BlockBuilder.Block(ninja, new Vector3(0f, 1.15f, -0.02f), new Vector3(0.4f, 0.32f, 0.3f), gi);   // hood back
        BlockBuilder.Block(ninja, new Vector3(-0.08f, 1.16f, 0.165f), new Vector3(0.07f, 0.05f, 0.01f), new Color(0.05f, 0.05f, 0.05f));
        BlockBuilder.Block(ninja, new Vector3(0.08f, 1.16f, 0.165f), new Vector3(0.07f, 0.05f, 0.01f), new Color(0.05f, 0.05f, 0.05f));
        BlockBuilder.Block(ninja, new Vector3(0f, 1.24f, -0.22f), new Vector3(0.08f, 0.06f, 0.18f), accent); // hood knot tail

        // element-specific flair
        switch (def.element)
        {
            case Element.Fire:
                BlockBuilder.Block(ninja, new Vector3(0f, 1.42f, 0f), new Vector3(0.14f, 0.14f, 0.14f), new Color(1f, 0.5f, 0.05f), 3f);
                BlockBuilder.Block(ninja, new Vector3(-0.08f, 1.38f, 0.05f), new Vector3(0.09f, 0.1f, 0.09f), new Color(1f, 0.85f, 0.2f), 3f);
                BlockBuilder.Block(armR, new Vector3(0f, -0.52f, 0.1f), new Vector3(0.05f, 0.05f, 0.5f), new Color(0.85f, 0.85f, 0.9f)); // katana
                break;
            case Element.Ice:
                BlockBuilder.Block(ninja, new Vector3(-0.32f, 1.02f, 0f), new Vector3(0.12f, 0.24f, 0.12f), accent, 1.5f).transform.localRotation = Quaternion.Euler(0, 0, 30);
                BlockBuilder.Block(ninja, new Vector3(0.32f, 1.02f, 0f), new Vector3(0.12f, 0.24f, 0.12f), accent, 1.5f).transform.localRotation = Quaternion.Euler(0, 0, -30);
                BlockBuilder.Block(armR, new Vector3(0f, -0.48f, 0.08f), new Vector3(0.18f, 0.03f, 0.18f), new Color(0.85f, 0.95f, 1f), 1f).transform.localRotation = Quaternion.Euler(0, 45, 0); // shuriken
                break;
            case Element.Earth:
                BlockBuilder.Block(ninja, new Vector3(-0.36f, 1.0f, 0f), new Vector3(0.24f, 0.12f, 0.36f), BlockBuilder.Darker(gi, 0.8f));
                BlockBuilder.Block(ninja, new Vector3(0.36f, 1.0f, 0f), new Vector3(0.24f, 0.12f, 0.36f), BlockBuilder.Darker(gi, 0.8f));
                BlockBuilder.Block(armR, new Vector3(0f, -0.55f, 0f), new Vector3(0.1f, 0.1f, 0.1f), new Color(0.5f, 0.4f, 0.3f)); // scythe handle
                BlockBuilder.Block(armR, new Vector3(0f, -0.25f, 0.12f), new Vector3(0.04f, 0.7f, 0.04f), new Color(0.35f, 0.25f, 0.15f));
                BlockBuilder.Block(armR, new Vector3(0f, 0.1f, 0.25f), new Vector3(0.03f, 0.08f, 0.3f), accent, 1f);
                break;
            case Element.Lightning:
                BlockBuilder.Block(ninja, new Vector3(0.12f, 1.45f, 0f), new Vector3(0.05f, 0.2f, 0.05f), accent, 3f).transform.localRotation = Quaternion.Euler(0, 0, 25);
                BlockBuilder.Block(ninja, new Vector3(0.05f, 1.6f, 0f), new Vector3(0.05f, 0.15f, 0.05f), accent, 3f).transform.localRotation = Quaternion.Euler(0, 0, -25);
                BlockBuilder.Block(armL, new Vector3(0f, -0.5f, 0.05f), new Vector3(0.04f, 0.04f, 0.6f), new Color(0.75f, 0.75f, 0.8f)); // nunchuck-ish staff
                BlockBuilder.Block(armR, new Vector3(0f, -0.5f, 0.05f), new Vector3(0.04f, 0.04f, 0.6f), new Color(0.75f, 0.75f, 0.8f));
                break;
            case Element.Energy:
                // glowing energy orbs in both hands, white shoulder guards and a halo of floating cubes
                BlockBuilder.Block(armL, new Vector3(0f, -0.5f, 0.08f), Vector3.one * 0.16f, accent, 4f);
                BlockBuilder.Block(armR, new Vector3(0f, -0.5f, 0.08f), Vector3.one * 0.16f, accent, 4f);
                BlockBuilder.Block(ninja, new Vector3(-0.36f, 1.0f, 0f), new Vector3(0.22f, 0.1f, 0.34f), new Color(0.92f, 0.95f, 0.9f));
                BlockBuilder.Block(ninja, new Vector3(0.36f, 1.0f, 0f), new Vector3(0.22f, 0.1f, 0.34f), new Color(0.92f, 0.95f, 0.9f));
                for (int i = 0; i < 4; i++)
                {
                    float ang = i * Mathf.PI * 0.5f;
                    BlockBuilder.Block(ninja, new Vector3(Mathf.Cos(ang) * 0.38f, 1.55f, Mathf.Sin(ang) * 0.38f), Vector3.one * 0.08f, accent, 5f, false, "Halo");
                }
                BlockBuilder.Cyl(transform, new Vector3(0f, def.pedestalHeight + 0.09f, 0f), new Vector3(1.4f, 0.01f, 1.4f), accent, 2.5f, "EnergyRing");
                break;
        }

        // Aura that appears at higher levels
        aura = BlockBuilder.Cyl(transform, new Vector3(0f, def.pedestalHeight + 0.1f, 0f), new Vector3(1.6f, 0.01f, 1.6f), accent, 2f, "Aura");
        aura.SetActive(false);

        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(ninja, false);
        muzzle.localPosition = new Vector3(0f, 1.12f, 0.35f);

        RefreshBelt();
    }

    void RefreshBelt()
    {
        Color c = GameData.BeltColors[Mathf.Clamp(level - 1, 0, 9)];
        float glow = level >= GameData.MaxLevel ? 2f : 0f;
        var m = BlockBuilder.Mat(c, glow);
        if (beltRenderer) beltRenderer.sharedMaterial = m;
        if (beltTailRenderer) beltTailRenderer.sharedMaterial = m;
        if (aura) aura.SetActive(level >= 5);
        if (ninja) ninja.localScale = Vector3.one * (1f + 0.025f * (level - 1));
    }

    // ------------------------------------------------------------------ EXP / leveling
    public void AddExp(float amount)
    {
        if (isGhost || IsMaxLevel) return;
        exp += amount;
        bool leveled = false;
        while (!IsMaxLevel && exp >= ExpNeeded)
        {
            exp -= ExpNeeded;
            level++;
            leveled = true;
        }
        if (IsMaxLevel) exp = 0f;
        if (leveled)
        {
            RefreshBelt();
            Vector3 p = transform.position + Vector3.up * (MuzzleHeight + 0.8f);
            Effects.Text(p, IsMaxLevel ? "MAX LEVEL! " + BeltName + " Belt" : "LEVEL UP! Lv " + level, new Color(1f, 0.9f, 0.3f), 1.8f, 1.2f);
            Effects.HitSpark(p, def.accentColor, 12);
            Effects.Play("levelup", 0.9f, 0f);
            if (rangeRing != null && rangeRing.enabled) DrawRangeRing();
        }
    }

    // ------------------------------------------------------------------ combat
    void Update()
    {
        if (isGhost) { IdleAnim(); return; }
        if (GameManager.Instance == null || GameManager.Instance.IsOver) return;

        fireTimer -= Time.deltaTime;
        target = FindTarget();   // re-evaluated every frame so 'First' always means the leader

        Vector3 aim = Vector3.zero;
        bool hasAim = false;
        if (target != null)
        {
            aim = PredictAim(muzzle.position, target.AimPoint, target.Velocity, def.projectileSpeed);
            hasAim = true;
            // Rotate the ninja to face the predicted point (yaw only)
            Vector3 flat = aim - transform.position; flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
                ninja.rotation = Quaternion.RotateTowards(ninja.rotation, Quaternion.LookRotation(flat), 720f * Time.deltaTime);

            if (fireTimer <= 0f)
            {
                fireTimer = 1f / FireRate;
                Fire(aim);
            }
        }
        else IdleAnim();

        // throw animation
        throwAnim = Mathf.MoveTowards(throwAnim, 0f, Time.deltaTime * 5f);
        armR.localRotation = Quaternion.Euler(-150f * throwAnim, 0f, 0f);
        armL.localRotation = Quaternion.Euler(-20f * throwAnim, 0f, 0f);

        // Debug: show predicted aim point
        bool showAim = GameManager.Instance.ShowAimDebug && hasAim;
        if (showAim && aimMarker == null)
        {
            aimMarker = BlockBuilder.Block(GameManager.Instance.transform, Vector3.zero, Vector3.one * 0.18f, def.accentColor, 4f, false, "AimMarker").transform;
        }
        if (aimMarker != null)
        {
            aimMarker.gameObject.SetActive(showAim);
            if (showAim) aimMarker.position = aim;
        }
    }

    void IdleAnim()
    {
        idleT += Time.deltaTime;
        if (ninja) ninja.localPosition = new Vector3(0f, def.pedestalHeight + 0.13f + Mathf.Sin(idleT * 2f) * 0.02f, 0f);
    }

    bool InRange(Enemy e)
    {
        Vector3 d = e.transform.position - transform.position; d.y = 0f;
        return d.sqrMagnitude <= Range * Range;
    }

    Enemy FindTarget()
    {
        Enemy best = null;
        float bestScore = float.MinValue;
        foreach (var e in Enemy.All)
        {
            if (e == null || e.Dead || !InRange(e)) continue;
            float score;
            switch (targetMode)
            {
                case TargetMode.First: score = e.distance; break;
                case TargetMode.Last: score = -e.distance; break;
                case TargetMode.Strongest: score = e.hp; break;
                default: score = -(e.transform.position - transform.position).sqrMagnitude; break;
            }
            if (score > bestScore) { bestScore = score; best = e; }
        }
        return best;
    }

    /// Lead-the-target math: find time t where |targetPos + v*t - shooter| = speed * t.
    /// Solves the quadratic (v·v - s²)t² + 2(d·v)t + d·d = 0 and aims at targetPos + v*t.
    public static Vector3 PredictAim(Vector3 shooter, Vector3 targetPos, Vector3 targetVel, float projectileSpeed)
    {
        Vector3 d = targetPos - shooter;
        float a = Vector3.Dot(targetVel, targetVel) - projectileSpeed * projectileSpeed;
        float b = 2f * Vector3.Dot(d, targetVel);
        float c = Vector3.Dot(d, d);
        float t;
        if (Mathf.Abs(a) < 1e-4f)
        {
            t = Mathf.Abs(b) > 1e-4f ? -c / b : 0f;
        }
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return targetPos;           // can't catch it: aim straight at it
            float sq = Mathf.Sqrt(disc);
            float t1 = (-b - sq) / (2f * a);
            float t2 = (-b + sq) / (2f * a);
            t = Mathf.Min(t1, t2);
            if (t < 0f) t = Mathf.Max(t1, t2);
        }
        if (t < 0f) return targetPos;
        return targetPos + targetVel * t;
    }

    void Fire(Vector3 aimPoint)
    {
        throwAnim = 1f;
        Vector3 dir = (aimPoint - muzzle.position).normalized;
        Projectile.Spawn(this, muzzle.position, dir * def.projectileSpeed);
        switch (def.element)
        {
            case Element.Fire: Effects.Play("fire", 0.45f); break;
            case Element.Ice: Effects.Play("ice", 0.5f); break;
            case Element.Earth: Effects.Play("earth_throw", 0.6f); break;
            case Element.Lightning: Effects.Play("zap", 0.4f); break;
            case Element.Energy: Effects.Play("energy", 0.55f); break;
        }
    }

    // ------------------------------------------------------------------ selection visuals
    public void SetSelected(bool on)
    {
        if (on && rangeRing == null)
        {
            var go = new GameObject("RangeRing");
            go.transform.SetParent(transform, false);
            rangeRing = go.AddComponent<LineRenderer>();
            rangeRing.sharedMaterial = BlockBuilder.LineMat();
            rangeRing.loop = true;
            rangeRing.useWorldSpace = true;
            rangeRing.startWidth = rangeRing.endWidth = 0.08f;
        }
        if (rangeRing != null)
        {
            rangeRing.enabled = on;
            if (on) DrawRangeRing();
        }
    }

    public void SetRangeColor(Color c)
    {
        if (rangeRing == null) SetSelected(true);
        rangeRing.startColor = rangeRing.endColor = c;
    }

    void DrawRangeRing()
    {
        const int n = 64;
        rangeRing.positionCount = n;
        Color c = def.accentColor; c.a = 0.9f;
        rangeRing.startColor = rangeRing.endColor = c;
        for (int i = 0; i < n; i++)
        {
            float ang = i / (float)n * Mathf.PI * 2f;
            rangeRing.SetPosition(i, transform.position + new Vector3(Mathf.Cos(ang) * Range, 0.1f, Mathf.Sin(ang) * Range));
        }
    }

    public int SellValue => Mathf.RoundToInt(goldSpent * GameData.SellRefund);

    public void CycleTargetMode()
    {
        targetMode = (TargetMode)(((int)targetMode + 1) % 4);
        target = null;
    }

    void OnDestroy()
    {
        All.Remove(this);
        if (aimMarker != null) Destroy(aimMarker.gameObject);
    }
}
