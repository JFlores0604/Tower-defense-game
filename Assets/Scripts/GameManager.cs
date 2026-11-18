using System.Collections.Generic;
using UnityEngine;

public enum GameState { Building, WaveActive, GameOver, Victory }

// ============================================================================
//  GameManager.cs — owns the game loop: gold, lives, waves, tower placement,
//  selection, selling, pause / speed and restart.
//  The on-screen UI lives in GameHUD.cs.
// ============================================================================
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("State (read-only at runtime)")]
    public GameState state = GameState.Building;
    public int gold;
    public int lives;
    public int wave;                 // waves started so far (0 before the first)
    public float countdown;
    public int totalKills;

    [Header("Toggles")]
    public bool ShowAimDebug;        // P — shows each ninja's predicted aim point
    public bool showHelp = true;     // H — the "how to place towers" overlay
    public bool paused;
    public float gameSpeed = 1f;

    public int EnemiesRemaining => spawnQueue.Count + Enemy.All.Count;
    public bool IsOver => state == GameState.GameOver || state == GameState.Victory;

    public EnemyPath Path { get; private set; }
    public CameraController Cam { get; private set; }
    public Camera MainCam { get; private set; }

    // placement / selection
    public int buildIndex = -1;      // which ninja is being placed (-1 = none)
    public Tower ghost;
    public bool ghostValid;
    public string ghostReason = "";
    public Tower selected;
    GameObject ghostPad;

    // waves
    readonly Queue<EnemyDef> spawnQueue = new Queue<EnemyDef>();
    readonly Queue<bool> bossQueue = new Queue<bool>();
    float spawnTimer, spawnInterval;
    public int waveEnemiesTotal;
    public float bannerTimer;
    public string bannerText = "";

    Transform worldRoot, enemyRoot, towerRoot, fxRoot;
    AudioSource music;
    public bool musicOn = true;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // ---------- camera ----------
        MainCam = Camera.main;
        if (MainCam == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            MainCam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
        }
        MainCam.clearFlags = CameraClearFlags.SolidColor;
        MainCam.backgroundColor = new Color(0.55f, 0.75f, 0.95f);
        Cam = MainCam.GetComponent<CameraController>();
        if (Cam == null) Cam = MainCam.gameObject.AddComponent<CameraController>();

        // ---------- sun ----------
        Light sun = FindAnyObjectByType<Light>();   // the scene's Directional Light (scenery lights don't exist yet)
        if (sun == null || sun.type != LightType.Directional)
        {
            sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        sun.intensity = 1.25f;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);

        // ---------- audio ----------
        Effects.Sfx = gameObject.AddComponent<AudioSource>();
        Effects.Sfx.playOnAwake = false;
        music = gameObject.AddComponent<AudioSource>();
        music.clip = Resources.Load<AudioClip>("Audio/music");
        music.loop = true;
        music.volume = 0.28f;
        music.ignoreListenerPause = true;
        if (music.clip != null) music.Play();

        BlockBuilder.Warmup();
        gameObject.AddComponent<GameHUD>();
        NewGame();
    }

    // ------------------------------------------------------------------ setup / restart
    public void NewGame()
    {
        if (worldRoot != null) Destroy(worldRoot.gameObject);
        Enemy.All.Clear();
        Tower.All.Clear();
        Effects.Clear();
        CancelBuild();
        selected = null;

        worldRoot = new GameObject("World").transform;
        enemyRoot = new GameObject("Enemies").transform; enemyRoot.SetParent(worldRoot);
        towerRoot = new GameObject("Ninjas").transform; towerRoot.SetParent(worldRoot);
        fxRoot = new GameObject("FX").transform; fxRoot.SetParent(worldRoot);
        Effects.Root = fxRoot;

        Path = new EnemyPath();
        MapBuilder.Build(worldRoot, Path);

        gold = GameData.StartingGold;
        lives = GameData.StartingLives;
        wave = 0;
        totalKills = 0;
        state = GameState.Building;
        countdown = 0f;   // wave 1 starts when the player is ready
        spawnQueue.Clear(); bossQueue.Clear();
        paused = false;
        gameSpeed = 1f;
        ApplyTimeScale();
        Banner("Defend the Temple!");
    }

    // ------------------------------------------------------------------ main loop
    void Update()
    {
        Effects.Tick(Time.deltaTime);
        if (bannerTimer > 0f) bannerTimer -= Time.unscaledDeltaTime;

        switch (state)
        {
            case GameState.Building:
                if (wave == 0) break;            // wave 1 waits for the player (Space / Start Wave)
                countdown -= Time.deltaTime;
                if (countdown <= 0f) StartNextWave();
                break;

            case GameState.WaveActive:
                if (spawnQueue.Count > 0)
                {
                    spawnTimer -= Time.deltaTime;
                    if (spawnTimer <= 0f)
                    {
                        var def = spawnQueue.Dequeue();
                        bool boss = bossQueue.Dequeue();
                        var en = Enemy.Spawn(enemyRoot, Path, def, wave, boss);
                        if (def == GameData.Emperor)
                        {
                            en.isEmperor = true;
                            Banner("The SHADOW EMPEROR resists all but the Jade Ninja!");
                            Effects.Play("gong", 1f, 0f);
                        }
                        spawnTimer = boss ? spawnInterval * 3f : spawnInterval;
                    }
                }
                else if (Enemy.All.Count == 0)
                {
                    WaveCleared();
                }
                break;
        }

        UpdateGhost();
    }

    // ------------------------------------------------------------------ waves
    public void StartNextWave()
    {
        if (state != GameState.Building) return;
        // Starting early pays a small bonus
        if (wave > 0 && countdown > 1f)
        {
            int bonus = Mathf.RoundToInt(countdown);
            gold += bonus;
            Effects.Text(MapBuilder.TemplePosition + Vector3.up * 4f, "Early start +" + bonus + "g", new Color(1f, 0.85f, 0.2f));
        }
        wave++;
        state = GameState.WaveActive;
        BuildWave(wave);
        spawnTimer = 0.2f;
        bool bossWave = wave % 5 == 0;
        if (IsEmperorWave(wave)) Banner("WAVE " + wave + " - THE SHADOW EMPEROR");
        else Banner(bossWave ? "WAVE " + wave + " - BOSS WAVE!" : "WAVE " + wave);
        Effects.Play("gong", 0.8f, 0f);
    }

    void BuildWave(int n)
    {
        spawnQueue.Clear(); bossQueue.Clear();
        if (IsEmperorWave(n))
        {
            // the Shadow Emperor leads the final wave
            spawnQueue.Enqueue(GameData.Emperor);
            bossQueue.Enqueue(true);
        }
        var list = new List<EnemyDef>();
        int imps = 4 + Mathf.RoundToInt(n * 1.4f);
        int samurai = n >= 2 ? 1 + Mathf.RoundToInt(n * 0.9f) : 0;
        int golems = n >= 4 ? Mathf.RoundToInt((n - 2) * 0.6f) : 0;

        // interleave the types so waves feel mixed
        int maxCount = Mathf.Max(imps, Mathf.Max(samurai, golems));
        for (int i = 0; i < maxCount; i++)
        {
            if (i < imps) list.Add(GameData.Enemies[0]);
            if (i < samurai) list.Add(GameData.Enemies[1]);
            if (i < golems) list.Add(GameData.Enemies[2]);
        }
        foreach (var d in list) { spawnQueue.Enqueue(d); bossQueue.Enqueue(false); }
        if (n % 5 == 0)
        {
            spawnQueue.Enqueue(GameData.Boss);
            bossQueue.Enqueue(true);
        }

        waveEnemiesTotal = spawnQueue.Count;
        spawnInterval = Mathf.Max(0.35f, 0.95f - n * 0.04f);
    }

    void WaveCleared()
    {
        int bonus = GameData.WaveClearBonus(wave);
        gold += bonus;
        Effects.Play("coin", 0.8f, 0f);
        if (wave >= GameData.TotalWaves)
        {
            state = GameState.Victory;
            Banner("VICTORY!");
            Effects.Play("victory", 1f, 0f);
            return;
        }
        state = GameState.Building;
        countdown = GameData.TimeBetweenWaves;
        Banner("Wave " + wave + " cleared!  +" + bonus + " gold");
    }

    public static bool IsEmperorWave(int n) => GameData.FinalWaveEmperor && n == GameData.TotalWaves;

    public void Banner(string text)
    {
        bannerText = text;
        bannerTimer = 2.5f;
    }

    // ------------------------------------------------------------------ enemy callbacks
    public void OnEnemyKilled(Enemy e, Tower killer)
    {
        gold += e.def.goldReward;
        totalKills++;
        Effects.Text(e.transform.position + Vector3.up * (1.6f * e.def.size), "+" + e.def.goldReward + "g", new Color(1f, 0.85f, 0.2f));
        Effects.Play(e.isBoss ? "boss_die" : "brick_break", e.isBoss ? 1f : 0.5f);
        if (e.isEmperor) Banner("THE SHADOW EMPEROR HAS FALLEN!");
        else if (e.isBoss) Banner("Warlord defeated!");
    }

    public void OnEnemyLeaked(Enemy e)
    {
        if (IsOver) return;
        lives -= e.isBoss ? 5 : 1;
        if (e.isEmperor) Banner("The Shadow Emperor conquered the temple...");
        Effects.Play("hurt", 0.9f, 0f);
        Effects.Text(MapBuilder.TemplePosition + Vector3.up * 4f, e.def.name + " reached the temple!", new Color(1f, 0.3f, 0.3f), 2f);
        if (lives <= 0)
        {
            lives = 0;
            state = GameState.GameOver;
            Banner("GAME OVER");
            Effects.Play("gameover", 1f, 0f);
            CancelBuild();
        }
    }

    // ------------------------------------------------------------------ building
    public void BeginBuild(int index, bool toggle = true)
    {
        if (IsOver) return;
        if (buildIndex == index) { if (toggle) CancelBuild(); return; }
        CancelBuild();
        Deselect();
        buildIndex = index;
        ghost = Tower.Create(worldRoot, index, new Vector3(0f, 0f, 0f), true);
        ghostPad = BlockBuilder.MeshObject(ghost.transform, BlockBuilder.CylinderMesh, BlockBuilder.Mat(Color.green, 1.5f), "GhostPad");
        ghostPad.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        ghostPad.transform.localScale = new Vector3(1.7f, 0.02f, 1.7f);
        Effects.Play("click", 0.6f, 0f);
    }

    public void CancelBuild()
    {
        buildIndex = -1;
        if (ghost != null) Destroy(ghost.gameObject);
        ghost = null;
        ghostPad = null;
    }

    public bool MouseGround(out Vector3 point)
    {
        point = Vector3.zero;
        if (MainCam == null) return false;
        Ray ray = MainCam.ScreenPointToRay(GameInput.MouseScreen);
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float enter)) { point = ray.GetPoint(enter); return true; }
        return false;
    }

    public void UpdateGhost()
    {
        if (ghost == null) return;
        if (!MouseGround(out Vector3 p)) return;
        p.x = Mathf.Floor(p.x) + 0.5f;
        p.z = Mathf.Floor(p.z) + 0.5f;
        p.y = 0f;
        ghost.transform.position = p;
        ghostValid = CanPlace(p, GameData.Towers[buildIndex], out ghostReason);
        Color c = ghostValid ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.2f, 0.2f);
        if (ghostPad != null) ghostPad.GetComponent<Renderer>().sharedMaterial = BlockBuilder.Mat(c, 1.5f);
        ghost.SetSelected(true);
        ghost.SetRangeColor(c);
    }

    public bool CanPlace(Vector3 p, TowerDef def, out string reason)
    {
        reason = "";
        if (Tower.All.Count >= GameData.MaxTowers) { reason = "Ninja limit reached (" + GameData.MaxTowers + ")"; return false; }
        if (gold < def.cost) { reason = "Not enough gold (" + def.cost + "g)"; return false; }
        if (p.x < MapBuilder.BoardMinX || p.x > MapBuilder.BoardMaxX || p.z < MapBuilder.BoardMinZ || p.z > MapBuilder.BoardMaxZ)
        { reason = "Outside the board"; return false; }
        if (Path.DistanceTo(p) < EnemyPath.HalfWidth + 0.75f) { reason = "Too close to the path"; return false; }
        var hits = Physics.OverlapBox(p + Vector3.up * 0.5f, new Vector3(0.6f, 0.45f, 0.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            if (h.GetComponentInParent<Blocker>() != null) { reason = "Something is in the way"; return false; }
        return true;
    }

    public void TryPlace()
    {
        if (ghost == null) return;
        UpdateGhost();   // re-check at the exact click position (not last frame's)
        if (!ghostValid)
        {
            Effects.Play("error", 0.6f, 0f);
            return;
        }
        var def = GameData.Towers[buildIndex];
        gold -= def.cost;
        var t = Tower.Create(towerRoot, buildIndex, ghost.transform.position, false);
        Effects.BrickExplosion(t.transform.position + Vector3.up * 0.3f, def.mainColor, def.accentColor, 8, 0.6f);
        Effects.Play("place", 0.8f, 0f);
        int idx = buildIndex;
        bool keep = GameInput.Held(KeyCode.LeftShift) || GameInput.Held(KeyCode.RightShift);
        CancelBuild();
        if (keep && gold >= def.cost) BeginBuild(idx);
    }

    // ------------------------------------------------------------------ selection
    public void TrySelectAtMouse()
    {
        if (MainCam == null) return;
        Ray ray = MainCam.ScreenPointToRay(GameInput.MouseScreen);
        Tower hitTower = null;
        if (Physics.Raycast(ray, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore))
            hitTower = hit.collider.GetComponentInParent<Tower>();
        if (hitTower != null && !hitTower.isGhost) Select(hitTower);
        else Deselect();
    }

    public void Select(Tower t)
    {
        if (selected != null && selected != t) selected.SetSelected(false);
        selected = t;
        t.SetSelected(true);
        Effects.Play("click", 0.5f, 0f);
    }

    public void Deselect()
    {
        if (selected != null) selected.SetSelected(false);
        selected = null;
    }

    public void SellSelected()
    {
        if (selected == null) return;
        int value = selected.SellValue;
        gold += value;
        Effects.Text(selected.transform.position + Vector3.up * 2.5f, "Sold +" + value + "g", new Color(1f, 0.85f, 0.2f));
        Effects.BrickExplosion(selected.transform.position + Vector3.up * 0.8f, selected.def.mainColor, selected.def.accentColor, 14, 0.8f);
        Effects.Play("brick_break", 0.8f);
        Destroy(selected.gameObject);
        selected = null;
    }

    // ------------------------------------------------------------------ debug helpers (for demos / testing)
    /// N — jump ahead so the next wave you start is the final wave.
    public void DebugSkipToFinalWave()
    {
        if (state != GameState.Building) return;
        wave = GameData.TotalWaves - 1;
        countdown = GameData.TimeBetweenWaves;
        Banner("DEBUG: skipped to wave " + wave);
    }

    /// K — destroy every enemy on the board and cancel the rest of the current wave.
    public void DebugKillAll()
    {
        if (state != GameState.WaveActive) return;
        spawnQueue.Clear(); bossQueue.Clear();
        foreach (var e in Enemy.All.ToArray())
            if (e != null && !e.Dead) e.TakeDamage(e.hp + 1f, null, true, true);   // force: even the Emperor
    }

    // ------------------------------------------------------------------ time controls
    public void TogglePause() { paused = !paused; ApplyTimeScale(); }

    public void CycleSpeed()
    {
        gameSpeed = gameSpeed >= 3f ? 1f : gameSpeed + 1f;
        ApplyTimeScale();
    }

    void ApplyTimeScale() { Time.timeScale = paused ? 0f : gameSpeed; }

    public void ToggleMusic()
    {
        musicOn = !musicOn;
        if (music != null) { if (musicOn) music.UnPause(); else music.Pause(); }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;
    }
}
