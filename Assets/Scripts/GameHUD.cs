using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  GameHUD.cs — all on-screen UI (drawn with IMGUI so no Canvas setup needed):
//   * wave counter at the top            * gold / lives / ninja count
//   * "how to place towers" text overlay * ninja shop cards (1-4)
//   * Lv + EXP bar under every ninja      * click-a-ninja info bubble
//   * enemy health bars, floating text, banners, pause / game-over screens
//  It also turns IMGUI events into input (keys, mouse, wheel) — see GameInput.
// ============================================================================
public class GameHUD : MonoBehaviour
{
    GameManager gm;
    float s = 1f;             // UI scale (designed for 1080p)
    float W = 1920f;          // virtual width
    const float H = 1080f;    // virtual height

    GUIStyle txt, panelTitle, cardBtn, btn;
    readonly List<Rect> blockers = new List<Rect>();
    Vector2 mouseGui;

    static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
    static readonly Color PanelBg = new Color(0.07f, 0.06f, 0.11f, 0.86f);
    static readonly Color PanelEdge = new Color(0.85f, 0.65f, 0.2f, 0.9f);

    void Awake() { gm = GetComponent<GameManager>(); }

    void OnApplicationFocus(bool focus) { if (!focus) GameInput.ReleaseAll(); }

    // ======================================================================= OnGUI
    void OnGUI()
    {
        if (gm == null || gm.Path == null) return;
        Event e = Event.current;

        GUI.matrix = Matrix4x4.identity;
        CaptureInput(e);

        s = Screen.height / H;
        W = Screen.width / s;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
        mouseGui = new Vector2(GameInput.MouseScreen.x / s, (Screen.height - GameInput.MouseScreen.y) / s);
        EnsureStyles();
        blockers.Clear();

        DrawEnemyBars();
        DrawTowerLabels();
        DrawFloatingText();

        DrawWavePanel();
        DrawStatsPanel();
        DrawControlsPanel();
        if (gm.showHelp) DrawHelpPanel();
        DrawShop();
        DrawBubble();
        DrawGhostTip();
        DrawBanner();
        if (gm.paused && !gm.IsOver) CenterText("PAUSED  (press P)", H * 0.42f, 56, Color.white);
        if (gm.IsOver) DrawEndScreen();

        HandleWorldClick(e);
    }

    // ======================================================================= input
    void CaptureInput(Event e)
    {
        if (e.type == EventType.Repaint || e.isMouse || e.type == EventType.ScrollWheel || e.type == EventType.MouseMove)
            GameInput.MouseScreen = new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y);

        if (e.type == EventType.ScrollWheel)
            GameInput.Scroll += e.delta.y;

        if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
        {
            if (GameInput.Press(e.keyCode)) HandleKey(e.keyCode);
        }
        else if (e.type == EventType.KeyUp && e.keyCode != KeyCode.None)
        {
            GameInput.Release(e.keyCode);
        }
    }

    void HandleKey(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.Alpha1: case KeyCode.Keypad1: gm.BeginBuild(0, false); break;
            case KeyCode.Alpha2: case KeyCode.Keypad2: gm.BeginBuild(1, false); break;
            case KeyCode.Alpha3: case KeyCode.Keypad3: gm.BeginBuild(2, false); break;
            case KeyCode.Alpha4: case KeyCode.Keypad4: gm.BeginBuild(3, false); break;
            case KeyCode.Alpha5: case KeyCode.Keypad5: gm.BeginBuild(4, false); break;
            case KeyCode.Escape: gm.CancelBuild(); gm.Deselect(); break;
            case KeyCode.Space: gm.StartNextWave(); break;
            case KeyCode.H: gm.showHelp = !gm.showHelp; break;
            case KeyCode.V: gm.ShowAimDebug = !gm.ShowAimDebug; break;
            case KeyCode.T: gm.Cam.straightDown = !gm.Cam.straightDown; break;
            case KeyCode.F: gm.CycleSpeed(); break;
            case KeyCode.P: gm.TogglePause(); break;
            case KeyCode.M: gm.ToggleMusic(); break;
            case KeyCode.X: case KeyCode.Delete: gm.SellSelected(); break;
            case KeyCode.Tab: if (gm.selected != null) gm.selected.CycleTargetMode(); break;
            case KeyCode.R: if (gm.IsOver) gm.NewGame(); break;
            case KeyCode.N: gm.DebugSkipToFinalWave(); break;   // DEBUG: skip to final wave
            case KeyCode.K: gm.DebugKillAll(); break;           // DEBUG: destroy all enemies
            case KeyCode.G: gm.gold += 500; Effects.Play("coin", 0.8f, 0f); break;   // DEBUG: free gold for testing/demo
        }
    }

    void HandleWorldClick(Event e)
    {
        if (e.type != EventType.MouseDown || gm.IsOver) return;
        foreach (var r in blockers) if (r.Contains(mouseGui)) return;
        if (e.button == 0)
        {
            if (gm.ghost != null) gm.TryPlace();
            else gm.TrySelectAtMouse();
            e.Use();
        }
        else if (e.button == 1)
        {
            gm.CancelBuild();
            gm.Deselect();
            e.Use();
        }
    }

    // ======================================================================= styles & helpers
    void EnsureStyles()
    {
        if (txt != null) return;
        txt = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false, fontSize = 18 };
        txt.normal.textColor = Color.white;
        panelTitle = new GUIStyle(txt) { fontStyle = FontStyle.Bold, fontSize = 22 };
        cardBtn = new GUIStyle();   // invisible click catcher
        btn = new GUIStyle(GUI.skin.button) { fontSize = 17, fontStyle = FontStyle.Bold, richText = true };
    }

    static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    void Panel(Rect r, Color? edge = null, bool blocks = true)
    {
        Fill(r, PanelBg);
        Color e = edge ?? PanelEdge;
        Fill(new Rect(r.x, r.y, r.width, 2f), e);
        Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), e);
        Fill(new Rect(r.x, r.y, 2f, r.height), e);
        Fill(new Rect(r.xMax - 2f, r.y, 2f, r.height), e);
        if (blocks) blockers.Add(r);
    }

    void Label(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool wrap = false)
    {
        txt.fontSize = size;
        txt.alignment = anchor;
        txt.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        txt.wordWrap = wrap;
        var shadow = new Rect(r.x + 2f, r.y + 2f, r.width, r.height);
        txt.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.8f);
        GUI.Label(shadow, StripColor(text), txt);
        txt.normal.textColor = color;
        GUI.Label(r, text, txt);
    }

    static string StripColor(string t)
    {
        // remove <color=..> tags for the drop shadow
        return System.Text.RegularExpressions.Regex.Replace(t, "<color=[^>]*>|</color>", "");
    }

    void Bar(Rect r, float frac, Color fill, Color back)
    {
        Fill(r, back);
        Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), fill);
    }

    bool Button(Rect r, string text, bool enabled = true)
    {
        blockers.Add(r);
        bool was = GUI.enabled;
        GUI.enabled = enabled;
        bool clicked = GUI.Button(r, text, btn);
        GUI.enabled = was;
        return clicked;
    }

    bool WorldToGui(Vector3 world, out Vector2 gui)
    {
        gui = Vector2.zero;
        if (gm.MainCam == null) return false;
        Vector3 sp = gm.MainCam.WorldToScreenPoint(world);
        if (sp.z < 0f) return false;
        gui = new Vector2(sp.x / s, (Screen.height - sp.y) / s);
        return true;
    }

    void CenterText(string text, float y, int size, Color c)
    {
        Label(new Rect(0f, y, W, size * 1.6f), text, size, c, TextAnchor.MiddleCenter, true);
    }

    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    // ======================================================================= world-space UI
    void DrawEnemyBars()
    {
        foreach (var en in Enemy.All)
        {
            if (en == null || en.Dead) continue;
            if (!WorldToGui(en.transform.position + Vector3.up * (1.75f * en.def.size + 0.2f), out Vector2 p)) continue;
            float w = en.isBoss ? 110f : 46f * en.def.size;
            float h = en.isBoss ? 10f : 6f;
            var r = new Rect(p.x - w / 2f, p.y, w, h);
            Fill(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), Color.black);
            Color fill = en.isEmperor ? new Color(0.65f, 0.2f, 1f)
                       : Color.Lerp(new Color(0.95f, 0.15f, 0.1f), new Color(0.3f, 0.95f, 0.3f), en.HpFraction);
            Bar(r, en.HpFraction, fill, new Color(0.2f, 0.05f, 0.05f));
            if (en.IsSlowed) Fill(new Rect(r.x, r.yMax, r.width, 2f), new Color(0.5f, 0.85f, 1f));
            if (en.IsBurning) Fill(new Rect(r.x, r.y - 3f, r.width, 2f), new Color(1f, 0.5f, 0.1f));
            if (en.isEmperor)
                Label(new Rect(p.x - 180f, p.y - 24f, 360f, 22f), "SHADOW EMPEROR  -  " + Mathf.CeilToInt(en.hp).ToString("N0") + " / " + Mathf.CeilToInt(en.maxHp).ToString("N0"), 16, new Color(0.8f, 0.45f, 1f), TextAnchor.MiddleCenter, true);
            else if (en.isBoss)
                Label(new Rect(p.x - 100f, p.y - 24f, 200f, 22f), en.def.name.ToUpper(), 16, new Color(1f, 0.4f, 0.3f), TextAnchor.MiddleCenter, true);
        }
    }

    void DrawTowerLabels()
    {
        foreach (var t in Tower.All)
        {
            if (t == null) continue;
            if (!WorldToGui(t.transform.position + new Vector3(0f, 0f, -0.75f), out Vector2 p)) continue;
            Color belt = GameData.BeltColors[Mathf.Clamp(t.level - 1, 0, 9)];
            string lv = t.IsMaxLevel ? "Lv 10 MAX" : "Lv " + t.level;
            Fill(new Rect(p.x - 34f, p.y + 2f, 68f, 26f), new Color(0f, 0f, 0f, 0.55f));
            Fill(new Rect(p.x - 34f, p.y + 2f, 4f, 26f), belt);
            Label(new Rect(p.x - 30f, p.y + 1f, 64f, 20f), lv, 14, t.IsMaxLevel ? Gold : Color.white, TextAnchor.MiddleCenter, true);
            float frac = t.IsMaxLevel ? 1f : t.exp / t.ExpNeeded;
            Bar(new Rect(p.x - 28f, p.y + 21f, 58f, 4f), frac, t.IsMaxLevel ? Gold : new Color(0.35f, 0.8f, 1f), new Color(0.15f, 0.15f, 0.2f));
        }
    }

    void DrawFloatingText()
    {
        foreach (var ft in Effects.Texts)
        {
            if (!WorldToGui(ft.worldPos, out Vector2 p)) continue;
            float a = 1f - Mathf.Clamp01((ft.age - ft.life * 0.6f) / (ft.life * 0.4f));
            var c = ft.color; c.a = a;
            int size = Mathf.RoundToInt(20 * ft.size);
            Label(new Rect(p.x - 200f, p.y - 15f, 400f, 30f), ft.text, size, c, TextAnchor.MiddleCenter, true);
        }
    }

    // ======================================================================= panels
    void DrawWavePanel()
    {
        var r = new Rect(W / 2f - 250f, 10f, 500f, 100f);
        Panel(r);
        string title = gm.wave == 0 ? "GET READY" : "WAVE  " + gm.wave + " / " + GameData.TotalWaves;
        Label(new Rect(r.x, r.y + 6f, r.width, 48f), title, 40, Gold, TextAnchor.MiddleCenter, true);

        string sub;
        if (gm.state == GameState.Building && gm.wave == 0)
            sub = "Place your ninjas, then press <color=#ffd34d>SPACE</color> to start Wave 1";
        else if (gm.state == GameState.Building)
        {
            int next = gm.wave + 1;
            sub = "Wave " + next + (GameManager.IsEmperorWave(next) ? " <color=#c066ff>(SHADOW EMPEROR)</color>" : next % 5 == 0 ? " (BOSS)" : "") + " in <color=#ffd34d>" + Mathf.CeilToInt(gm.countdown) + "s</color>  -  press SPACE to start now";
        }
        else if (gm.state == GameState.WaveActive)
            sub = "Enemies remaining: <color=#ff7766>" + gm.EnemiesRemaining + "</color>";
        else sub = gm.state == GameState.Victory ? "All waves cleared!" : "The temple has fallen";
        Label(new Rect(r.x, r.y + 58f, r.width, 30f), sub, 18, Color.white, TextAnchor.MiddleCenter);
    }

    void DrawStatsPanel()
    {
        var r = new Rect(12f, 10f, 300f, 128f);
        Panel(r);
        Fill(new Rect(r.x + 16f, r.y + 16f, 18f, 18f), Gold);
        Label(new Rect(r.x + 44f, r.y + 10f, 250f, 30f), "Gold:  <b>" + gm.gold + "</b>", 22, Gold);
        Fill(new Rect(r.x + 16f, r.y + 50f, 18f, 18f), new Color(0.95f, 0.25f, 0.3f));
        Label(new Rect(r.x + 44f, r.y + 44f, 250f, 30f), "Temple Lives:  <b>" + gm.lives + "</b>", 20, Color.white);
        Fill(new Rect(r.x + 16f, r.y + 84f, 18f, 18f), new Color(0.55f, 0.8f, 1f));
        Label(new Rect(r.x + 44f, r.y + 78f, 250f, 30f),
            "Ninjas:  <b>" + Tower.All.Count + " / " + GameData.MaxTowers + "</b>     Kills: " + gm.totalKills, 20, Color.white);
    }

    void DrawControlsPanel()
    {
        var r = new Rect(W - 262f, 10f, 250f, 286f);
        Panel(r);
        float x = r.x + 12f, y = r.y + 12f, w = r.width - 24f, h = 36f, gap = 8f;
        if (Button(new Rect(x, y, w, h), "Start Wave  [Space]", gm.state == GameState.Building)) gm.StartNextWave();
        y += h + gap;
        if (Button(new Rect(x, y, w, h), "Speed: " + gm.gameSpeed + "x  [F]")) gm.CycleSpeed();
        y += h + gap;
        if (Button(new Rect(x, y, w, h), (gm.paused ? "Resume" : "Pause") + "  [P]")) gm.TogglePause();
        y += h + gap;
        if (Button(new Rect(x, y, w, h), "Aim Prediction: " + (gm.ShowAimDebug ? "ON" : "OFF") + " [V]")) gm.ShowAimDebug = !gm.ShowAimDebug;
        y += h + gap;
        if (Button(new Rect(x, y, w, h), (gm.Cam.straightDown ? "Angled View" : "Top-Down View") + "  [T]")) gm.Cam.straightDown = !gm.Cam.straightDown;
        y += h + gap;
        float half = (w - gap) / 2f;
        if (Button(new Rect(x, y, half, h), "Music [M]")) gm.ToggleMusic();
        if (Button(new Rect(x + half + gap, y, half, h), "Help [H]")) gm.showHelp = !gm.showHelp;
    }

    void DrawHelpPanel()
    {
        var r = new Rect(12f, 148f, 340f, 470f);
        Panel(r);
        Label(new Rect(r.x + 14f, r.y + 10f, r.width - 28f, 30f), "HOW TO PLACE NINJAS", 22, Gold, TextAnchor.UpperLeft, true);
        string body =
            "<b>1.</b> Pick a ninja: click a card at the bottom of the screen or press <b>1 - 5</b>.\n" +
            "<b>2.</b> Move the mouse over the grass. A <color=#55ff66>GREEN</color> ring means the spot is OK, <color=#ff5555>RED</color> means it isn't.\n" +
            "<b>3.</b> <b>Left-click</b> to place it. This costs gold. Hold <b>Shift</b> to place several.\n" +
            "<b>4.</b> <b>Right-click</b> or <b>Esc</b> cancels.\n\n" +
            "You can place up to <b>" + GameData.MaxTowers + "</b> ninjas. Defeat enemies to earn gold.\n" +
            "<b>Click a ninja</b> to see its level and EXP, change its target (Tab), or sell it (X).\n" +
            "Ninjas earn EXP when they hit and defeat enemies, up to <b>Lv 10</b>. Their belt changes color as they level up.\n\n" +
            "<color=#ff7766>If an enemy reaches the temple, the game is over!</color>\n\n" +
            "<color=#aaaaaa>Camera: WASD / Arrows pan, scroll to zoom, T for top-down view. H hides this panel.</color>";
        Label(new Rect(r.x + 14f, r.y + 44f, r.width - 28f, r.height - 50f), body, 16, Color.white, TextAnchor.UpperLeft, false, true);
    }

    void DrawShop()
    {
        int n = GameData.Towers.Length;
        float cw = 236f, ch = 140f, gap = 10f;
        float total = n * cw + (n - 1) * gap;
        float x0 = W / 2f - total / 2f, y = H - ch - 12f;
        string[] special = { "Burn over time", "Slows enemies", "Splash damage", "Chain lightning", "Full dmg vs Emperor" };
        string[] elementName = { "FIRE", "ICE", "EARTH", "LIGHTNING", "ENERGY" };

        for (int i = 0; i < n; i++)
        {
            var d = GameData.Towers[i];
            var r = new Rect(x0 + i * (cw + gap), y, cw, ch);
            bool afford = gm.gold >= d.cost && Tower.All.Count < GameData.MaxTowers;
            bool active = gm.buildIndex == i;
            bool hover = r.Contains(mouseGui);
            Panel(r, active ? Color.white : (hover ? d.accentColor : PanelEdge));
            Fill(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, 8f), d.mainColor);
            float a = afford ? 1f : 0.45f;

            // mini ninja icon
            Fill(new Rect(r.x + 14f, r.y + 22f, 40f, 40f), d.mainColor * new Color(1, 1, 1, a));
            Fill(new Rect(r.x + 18f, r.y + 34f, 32f, 10f), new Color(0.96f, 0.8f, 0.6f, a));
            Fill(new Rect(r.x + 23f, r.y + 37f, 7f, 4f), new Color(0f, 0f, 0f, a));
            Fill(new Rect(r.x + 38f, r.y + 37f, 7f, 4f), new Color(0f, 0f, 0f, a));
            Fill(new Rect(r.x + 14f, r.y + 54f, 40f, 4f), new Color(d.accentColor.r, d.accentColor.g, d.accentColor.b, a));

            Label(new Rect(r.x + 62f, r.y + 16f, 170f, 26f), "<b>[" + (i + 1) + "] " + d.name + "</b>", 18, new Color(1, 1, 1, a));
            Label(new Rect(r.x + 62f, r.y + 40f, 170f, 22f),
                "<color=" + Hex(d.accentColor) + ">" + elementName[i] + "</color>   <color=#ffd34d>" + d.cost + "g</color>", 16, new Color(1, 1, 1, a));
            Label(new Rect(r.x + 14f, r.y + 70f, 220f, 22f),
                "DMG " + d.damage + "   RATE " + d.fireRate.ToString("0.0") + "/s   RNG " + d.range.ToString("0.#"), 14, new Color(0.85f, 0.85f, 0.9f, a));
            Label(new Rect(r.x + 14f, r.y + 90f, 220f, 22f),
                "Blast speed " + d.projectileSpeed + "   Height " + d.pedestalHeight.ToString("0.0"), 14, new Color(0.85f, 0.85f, 0.9f, a));
            Label(new Rect(r.x + 14f, r.y + 110f, 220f, 22f), special[i], 15, new Color(d.accentColor.r, d.accentColor.g, d.accentColor.b, a), TextAnchor.UpperLeft, true);

            if (GUI.Button(r, GUIContent.none, cardBtn) && !gm.IsOver) gm.BeginBuild(i);

            if (hover)
            {
                var tip = new Rect(r.x, r.y - 74f, cw, 66f);
                Panel(tip, d.accentColor, false);
                Label(new Rect(tip.x + 10f, tip.y + 6f, tip.width - 20f, tip.height - 10f),
                    "<b>" + d.title + "</b>\n" + d.description, 14, Color.white, TextAnchor.UpperLeft, false, true);
            }
        }
    }

    void DrawBubble()
    {
        var t = gm.selected;
        if (t == null) return;
        if (!WorldToGui(t.transform.position + Vector3.up * (t.MuzzleHeight + 0.7f), out Vector2 anchor)) return;

        float bw = 330f, bh = 300f;
        var r = new Rect(anchor.x + 30f, anchor.y - bh - 10f, bw, bh);
        r.x = Mathf.Clamp(r.x, 10f, W - bw - 10f);
        r.y = Mathf.Clamp(r.y, 120f, H - bh - 170f);

        // tail of the speech bubble
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = Vector2.Lerp(anchor, new Vector2(r.x + 30f, r.yMax), i / 4f);
            Fill(new Rect(p.x - 4f, p.y - 4f, 8f, 8f), t.def.accentColor);
        }

        Panel(r, t.def.accentColor);
        float x = r.x + 14f, y = r.y + 10f, w = bw - 28f;
        Label(new Rect(x, y, w, 28f), t.def.name, 24, t.def.accentColor, TextAnchor.UpperLeft, true);
        y += 28f;
        Label(new Rect(x, y, w, 22f), "<i>" + t.def.title + "</i>   <color=#888888>(Tab: target, X: sell)</color>", 15, new Color(0.8f, 0.8f, 0.85f));
        y += 26f;

        Color belt = GameData.BeltColors[Mathf.Clamp(t.level - 1, 0, 9)];
        Label(new Rect(x, y, w, 24f), "Level <b>" + t.level + " / " + GameData.MaxLevel + "</b>    <color=" + Hex(belt) + ">" + t.BeltName + " Belt</color>", 19, Color.white);
        y += 26f;
        string expTxt = t.IsMaxLevel ? "EXP:  MAX" : "EXP:  " + Mathf.FloorToInt(t.exp) + " / " + t.ExpNeeded;
        Label(new Rect(x, y, w, 22f), expTxt, 17, new Color(0.6f, 0.9f, 1f));
        y += 22f;
        Bar(new Rect(x, y, w, 10f), t.IsMaxLevel ? 1f : t.exp / t.ExpNeeded, t.IsMaxLevel ? Gold : new Color(0.35f, 0.8f, 1f), new Color(0.15f, 0.15f, 0.2f));
        y += 18f;

        string special;
        switch (t.def.element)
        {
            case Element.Fire: special = "Burn: " + t.BurnDps.ToString("0.0") + " dmg/s for 2s"; break;
            case Element.Ice: special = "Slow: " + Mathf.RoundToInt(t.SlowAmount * 100f) + "% for 1.6s"; break;
            case Element.Earth: special = "Splash radius: " + t.SplashRadius.ToString("0.0"); break;
            case Element.Lightning: special = "Chains to " + t.ChainCount + " more enemies"; break;
            default: special = "Energy splash " + t.SplashRadius.ToString("0.0") + "  -  FULL dmg vs Emperor"; break;
        }
        Label(new Rect(x, y, w, 22f), "Damage: <b>" + t.Damage.ToString("0.#") + "</b>     Fire rate: <b>" + t.FireRate.ToString("0.00") + "/s</b>", 16, Color.white);
        y += 22f;
        Label(new Rect(x, y, w, 22f), "Range: <b>" + t.Range.ToString("0.0") + "</b>     Blast speed: <b>" + t.def.projectileSpeed + "</b>", 16, Color.white);
        y += 22f;
        Label(new Rect(x, y, w, 22f), special, 16, t.def.accentColor);
        y += 22f;
        Label(new Rect(x, y, w, 22f), "Kills: " + t.kills + "     Damage dealt: " + Mathf.RoundToInt(t.totalDamage), 15, new Color(0.8f, 0.8f, 0.85f));
        y += 28f;

        float half = (w - 8f) / 2f;
        if (Button(new Rect(x, y, half, 34f), "Target: " + t.targetMode)) t.CycleTargetMode();
        if (Button(new Rect(x + half + 8f, y, half, 34f), "Sell +" + t.SellValue + "g")) gm.SellSelected();
        if (Button(new Rect(r.xMax - 34f, r.y + 8f, 26f, 26f), "X")) gm.Deselect();
    }

    void DrawGhostTip()
    {
        if (gm.ghost == null) return;
        var d = GameData.Towers[gm.buildIndex];
        string msg = gm.ghostValid
            ? "Left-click to place " + d.name + " (" + d.cost + "g)"
            : gm.ghostReason;
        Color c = gm.ghostValid ? new Color(0.5f, 1f, 0.55f) : new Color(1f, 0.45f, 0.45f);
        Label(new Rect(mouseGui.x + 20f, mouseGui.y + 14f, 420f, 26f), msg, 17, c, TextAnchor.UpperLeft, true);
    }

    void DrawBanner()
    {
        if (gm.bannerTimer <= 0f || string.IsNullOrEmpty(gm.bannerText) || gm.IsOver) return;
        float a = Mathf.Clamp01(gm.bannerTimer / 0.6f);
        CenterText(gm.bannerText, H * 0.22f, 54, new Color(Gold.r, Gold.g, Gold.b, a));
    }

    void DrawEndScreen()
    {
        Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.55f));
        var r = new Rect(W / 2f - 280f, H / 2f - 170f, 560f, 340f);
        bool win = gm.state == GameState.Victory;
        Panel(r, win ? Gold : new Color(1f, 0.3f, 0.3f));
        Label(new Rect(r.x, r.y + 24f, r.width, 70f), win ? "VICTORY!" : "GAME OVER", 64, win ? Gold : new Color(1f, 0.35f, 0.3f), TextAnchor.MiddleCenter, true);
        string body = win
            ? "The temple is safe. All " + GameData.TotalWaves + " waves defeated!"
            : "An enemy reached the temple on wave " + gm.wave + ".";
        Label(new Rect(r.x + 20f, r.y + 110f, r.width - 40f, 30f), body, 20, Color.white, TextAnchor.MiddleCenter);
        int best = 0;
        foreach (var t in Tower.All) if (t != null) best = Mathf.Max(best, t.level);
        Label(new Rect(r.x + 20f, r.y + 150f, r.width - 40f, 30f),
            "Enemies defeated: " + gm.totalKills + "     Highest ninja level: " + best, 18, new Color(0.85f, 0.85f, 0.9f), TextAnchor.MiddleCenter);
        if (Button(new Rect(r.x + r.width / 2f - 120f, r.y + 230f, 240f, 52f), "Play Again  [R]")) gm.NewGame();
    }
}
