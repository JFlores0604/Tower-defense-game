using UnityEngine;

// ============================================================================
//  GameData.cs  —  ALL the tuning numbers live here.
//  Change costs, damage, speeds, HP, wave sizes etc. without touching logic.
// ============================================================================

public enum Element { Fire, Ice, Earth, Lightning, Energy }

public enum TargetMode { First, Last, Strongest, Closest }

[System.Serializable]
public class TowerDef
{
    public string name;          // display name
    public string title;         // short flavour title shown in the bubble
    public Element element;
    public int cost;
    public float damage;         // damage per projectile at level 1
    public float fireRate;       // shots per second at level 1
    public float projectileSpeed;// units per second
    public float range;          // targeting radius
    public float pedestalHeight; // tower height (rubric: different heights)
    public Color mainColor;      // ninja gi colour
    public Color accentColor;    // trim / emblem / projectile colour
    public string description;
}

[System.Serializable]
public class EnemyDef
{
    public string name;
    public float speed;      // units per second along the path
    public float maxHp;      // HP at wave 1 (scaled up every wave)
    public int goldReward;   // gold for the kill
    public int expReward;    // bonus EXP for the ninja that lands the kill
    public float size;       // overall model scale
    public Color bodyColor;
    public Color trimColor;
}

public static class GameData
{
    // ---------------- Economy & rules ----------------
    public const int StartingGold = 200;
    public const int StartingLives = 1;       // Rubric: game ends when an enemy finishes the path. Raise for an easier game.
    public const int MaxTowers = 12;          // Rubric: limited number of towers
    public const int TotalWaves = 15;
    public const float TimeBetweenWaves = 12f;
    public const float SellRefund = 0.7f;
    public const int MaxLevel = 10;

    // ---------------- Leveling ----------------
    // EXP needed to go from `level` to `level + 1`.  L1->2 = 150 ... L8->9 = 990 (≈ the "320/1000" style)
    public static int ExpToNext(int level) => 150 + 120 * (level - 1);
    public const float ExpPerDamage = 0.5f;   // EXP earned per point of damage dealt

    // Stat multipliers per level (level 1 = x1.0)
    public static float DamageMult(int level)   => 1f + 0.15f * (level - 1);   // L10 = x2.35
    public static float FireRateMult(int level) => 1f + 0.06f * (level - 1);   // L10 = x1.54
    public static float RangeBonus(int level)   => 0.15f * (level - 1);        // L10 = +1.35 units

    // Belt colours by level — the ninja's sash changes as it ranks up.
    public static readonly string[] BeltNames = {
        "White", "Yellow", "Orange", "Green", "Blue", "Purple", "Brown", "Red", "Black", "Gold" };
    public static readonly Color[] BeltColors = {
        new Color(0.95f,0.95f,0.95f), new Color(1f,0.9f,0.15f), new Color(1f,0.55f,0.1f),
        new Color(0.2f,0.75f,0.25f),  new Color(0.2f,0.45f,1f), new Color(0.6f,0.25f,0.85f),
        new Color(0.45f,0.28f,0.12f), new Color(0.9f,0.12f,0.12f), new Color(0.08f,0.08f,0.08f),
        new Color(1f,0.8f,0.2f) };

    // ---------------- Towers (cheapest -> most expensive / most powerful) ----------------
    public static readonly TowerDef[] Towers =
    {
        new TowerDef {
            name = "Ember Ninja", title = "Master of Fire", element = Element.Fire,
            cost = 50, damage = 12f, fireRate = 2.0f, projectileSpeed = 16f, range = 6.0f, pedestalHeight = 0.5f,
            mainColor = new Color(0.95f,0.42f,0.08f), accentColor = new Color(1f,0.75f,0.1f),
            description = "Rapid fireballs that set enemies ablaze (burn damage over time)." },
        new TowerDef {
            name = "Frost Ninja", title = "Master of Ice", element = Element.Ice,
            cost = 100, damage = 14f, fireRate = 1.25f, projectileSpeed = 13f, range = 7.0f, pedestalHeight = 1.1f,
            mainColor = new Color(0.55f,0.85f,0.95f), accentColor = new Color(0.85f,1f,1f),
            description = "Ice shards that slow enemies down." },
        new TowerDef {
            name = "Boulder Ninja", title = "Master of Earth", element = Element.Earth,
            cost = 175, damage = 42f, fireRate = 0.6f, projectileSpeed = 10f, range = 6.5f, pedestalHeight = 1.7f,
            mainColor = new Color(0.42f,0.3f,0.18f), accentColor = new Color(0.45f,0.7f,0.25f),
            description = "Heavy boulders that smash everything in a radius (splash)." },
        new TowerDef {
            name = "Volt Ninja", title = "Master of Lightning", element = Element.Lightning,
            cost = 275, damage = 30f, fireRate = 1.6f, projectileSpeed = 28f, range = 8.5f, pedestalHeight = 2.4f,
            mainColor = new Color(0.45f,0.25f,0.85f), accentColor = new Color(1f,0.95f,0.25f),
            description = "Lightning bolts that chain to nearby enemies." },
        new TowerDef {
            name = "Jade Ninja", title = "The Chosen One - Master of Energy", element = Element.Energy,
            cost = 500, damage = 100f, fireRate = 1.5f, projectileSpeed = 24f, range = 9.5f, pedestalHeight = 3.0f,
            mainColor = new Color(0.12f,0.72f,0.3f), accentColor = new Color(0.55f,1f,0.6f),
            description = "The strongest ninja. Energy blasts with splash, and FULL damage to the Shadow Emperor." },
    };

    // ---------------- Enemies (rubric A: different speed and HP) ----------------
    public static readonly EnemyDef[] Enemies =
    {
        new EnemyDef { name = "Shadow Imp",    speed = 4.0f, maxHp = 50f,  goldReward = 4,  expReward = 15,
                       size = 0.75f, bodyColor = new Color(0.22f,0.12f,0.32f), trimColor = new Color(0.9f,0.2f,0.9f) },
        new EnemyDef { name = "Stone Samurai", speed = 3.0f, maxHp = 150f, goldReward = 7,  expReward = 30,
                       size = 1.0f,  bodyColor = new Color(0.5f,0.5f,0.55f),  trimColor = new Color(0.8f,0.15f,0.15f) },
        new EnemyDef { name = "Iron Golem",    speed = 1.75f, maxHp = 475f, goldReward = 15, expReward = 60,
                       size = 1.35f, bodyColor = new Color(0.3f,0.33f,0.36f), trimColor = new Color(1f,0.5f,0.1f) },
    };

    // Boss that shows up every 5th wave
    public static readonly EnemyDef Boss =
        new EnemyDef { name = "Warlord",       speed = 1.4f, maxHp = 2000f, goldReward = 100, expReward = 300,
                       size = 2.0f, bodyColor = new Color(0.15f,0.15f,0.18f), trimColor = new Color(1f,0.15f,0.1f) };

    // ---------------- The Shadow Emperor (final wave) ----------------
    // Wave 15 is led by the Shadow Emperor: huge HP (5700 x wave-15 scaling = ~20,000),
    // and every ninja except the Jade Ninja only does EmperorResist (half) damage to him.
    public const bool FinalWaveEmperor = true;
    public const float EmperorResist = 0.5f;
    public static readonly EnemyDef Emperor =
        new EnemyDef { name = "Shadow Emperor", speed = 1.3f, maxHp = 5700f, goldReward = 250, expReward = 500,
                       size = 2.6f, bodyColor = new Color(0.08f,0.05f,0.12f), trimColor = new Color(0.75f,0.2f,1f) };

    // HP grows each wave so the game keeps getting harder
    public static float HpScale(int wave) => 1f + 0.18f * (wave - 1);
    public static int WaveClearBonus(int wave) => 20 + 5 * wave;
}
