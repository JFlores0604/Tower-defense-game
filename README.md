# Ninja Temple Defense — CSCI 381 Assignment 2 (Tower Defense)

A 3D top-down tower defense game made in Unity 6 (URP). You place elemental brick-built ninjas
along a winding path to stop waves of enemies before they reach the temple.

**How to run:** open the project in Unity, open `Assets/Scenes/SampleScene`, and press **Play**.
The whole game builds itself at runtime (`GameBootstrap.cs`), so you don't need to set anything up in the scene.

## Controls
| Action | Input |
|---|---|
| Pick a ninja to place | Click a card at the bottom, or press **1 – 5** |
| Place ninja | **Left-click** on grass (green ring = valid). Hold **Shift** to place several |
| Cancel / deselect | **Right-click** or **Esc** |
| Inspect a ninja (name, level, EXP, stats) | **Left-click** the ninja |
| Change targeting (First / Last / Strongest / Closest) | **Tab** or the bubble button |
| Sell selected ninja | **X** or the bubble button |
| Start next wave early (gives bonus gold) | **Space** |
| Game speed 1x/2x/3x · Pause | **F** · **P** |
| Show predicted aim points | **V** |
| Pan · Zoom · Straight top-down view | **WASD / Arrows** · **Mouse wheel** · **T** |
| Toggle help overlay · music | **H** · **M** |
| Debug: +500 gold · skip to final wave · destroy all enemies | **G** · **N** · **K** |

## Rubric checklist (CSCI 381)
**C**
- The player can place towers: `GameManager.BeginBuild / TryPlace`
- Towers fire continuously at a set rate: `Tower.Update` (fire timer = 1 / FireRate)
- Towers aim at an enemy: the ninja turns to face its target (`Tower.Update`)
- Enemies follow a predetermined path: `EnemyPath.cs`, `Enemy.Update`
- The game ends when an enemy finishes the path: `GameManager.OnEnemyLeaked` (`StartingLives = 1` in `GameData.cs`)

**B**
- At least two tower types with different rate of fire, projectile speed and tower height: 5 ninjas in `GameData.Towers` (`fireRate`, `projectileSpeed`, `pedestalHeight`)
- A text overlay explains how to place towers: the "HOW TO PLACE NINJAS" panel plus the cursor tooltip (`GameHUD.DrawHelpPanel / DrawGhostTip`)
- A limited number of towers: `GameData.MaxTowers = 12`, and every ninja costs gold
- Paths are not straight lines: the path is a Catmull-Rom spline (`EnemyPath.ControlPoints`)

**A**
- Multiple enemy types with different speed and HP: Shadow Imp (fast/weak), Stone Samurai (medium), Iron Golem (slow/tanky), and a Warlord boss every 5 waves
- Non-basic assets: custom textures (`Resources/Textures`), 18 custom sound effects plus a music loop (`Resources/Audio`), a procedural path mesh, and brick-built models made in code
- Predictive aiming: `Tower.PredictAim` solves the intercept quadratic using the target's velocity and position (press **V** to see the predicted points)

**A+ (above and beyond)**
- **EXP and leveling (1 – 10):** ninjas earn EXP from damage and kills and get stronger each level. Their belt color changes with rank (White → Gold), they get an aura at Lv 5, and they glow at max level
- Level/EXP shown under every ninja, plus a click bubble with name, level, EXP (e.g. `320 / 1000`) and stats
- Elemental effects: Fire burn (damage over time), Ice slow, Earth splash, Lightning chain
- 15 waves with a wave counter, boss waves, wave-clear bonus, and an early-start bonus
- **The Shadow Emperor:** wave 15 is led by a giant boss with ~20,000 HP who takes **half damage** from every ninja except the **Jade Ninja**
- **The Jade Ninja (500g):** the strongest ninja. Energy blasts with splash, and the only one at full power against the Emperor
- Gold from every kill and selling at 70%
- Targeting modes, game speed, pause, camera pan/zoom/top-down toggle
- Physics brick explosions when enemies die, projectile trails, floating text, lights
- Sound effects and background music

## Tuning
Every number (costs, damage, HP, speeds, waves, EXP curve, max towers, lives) is in **`Assets/Scripts/GameData.cs`**.

## Credits
All code, models, textures and audio were generated for this project (by me, with AI assistance).
