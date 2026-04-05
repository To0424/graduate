# Graduation — Tower Defense Game Guide
## (Group 23 Game Proposal Implementation)

---

## Game Overview (from Proposal)

- **Title**: Graduation
- **Genre**: Tower Defense with progression elements
- **Platform**: PC first (mouse/keyboard), mobile later
- **Art**: 32x32 pixel art
- **Theme**: Year 4 HKU student vs. rogue SIS system. "Course monsters" attack through HKU buildings.
- **Goal**: Clear levels across HKU faculty buildings → earn **credits per level** → reach **240 credits** to graduate
- **Inspiration**: Warcraft III custom TD maps + Bloons TD (fixed paths, powerful towers)

### Key Mechanics
| Mechanic | Description |
|----------|-------------|
| **Fixed path maps** | Enemies follow a predetermined route (like Bloons TD). Towers placed on designated slots beside the path |
| **Pre-built path patterns** | Each difficulty tier has a pool of pre-designed paths. Easy = 1 spawn point. Hard = up to 3 spawn points. Randomly assigned per level |
| **3 basic tower types** | Short range / fast, Medium / balanced, Long range / slow |
| **Professor towers** | The STRONGEST tower type. 1 per major/faculty. Unlocked by clearing all levels of that faculty building |
| **Skill tree** | Players earn skill points alongside credits. Buffs ALL towers. Themed as extracurriculars: Social, Internship, Part-time Work, Certifications. Each section has different buffs (damage, speed, multi-target, etc.) |
| **Course difficulty** | Course code = difficulty (ELEC1001 = easy, ELEC4001 = hard). Each level is a course |
| **Faculty buildings** | Each building = a major (EEE, FBE, ART, etc.). 3-5 levels per building. Clear all = unlock Professor tower |
| **Credit progression** | Earn credits per level cleared (first clear only — replaying gives no extra). 240 total to graduate (win the game) |
| **Retry freely** | Losing a level just means try again. Replay completed levels for practice but no duplicate rewards |

### Team Roles
| Member | Role |
|--------|------|
| Preston (Wong Hei Nam) | Lead Programmer — core architecture, enemy pathing, tower mechanics |
| Ho Lam (Tang) | Game Designer — Professor abilities, synergy system, difficulty balancing |
| **Davis (Wu Kwun To)** | Level Designer & Pixel Artist — pixel art, HKU building path maps |
| Oscar (Wong Kai Ching) | Backend — cloud saves (UGS/Firebase), UI framework, random encounters |

---

## Architecture Overview

```
GameManager (singleton, persists across scenes)
│
├── ProgressManager           — tracks total credits (240 to graduate), completed levels
│   └── SkillTreeManager      — skill points, unlocked buffs (Social/Internship/Part-time/Certs)
│
├── OverworldManager          — HKU top-view pixel map, building selection
│   └── FacultyData[]         — each building = a major, 3-5 course levels, professor unlock
│
├── LevelManager              — loads a specific course level
│   ├── PathManager           — picks a random pre-built path pattern for this difficulty tier
│   │   └── Waypoints         — ordered Transform points (supports multiple spawn points)
│   ├── TowerSlots            — predefined positions beside the path where towers can be placed
│   └── WaveSpawner           — spawns 3-5 rounds of course monsters
│
├── Towers
│   ├── Tower (base)          — placed on a tower slot, targets & shoots enemies
│   │   ├── RapidTower        — short range, fast fire rate, low damage
│   │   ├── BalancedTower     — medium range, medium stats
│   │   ├── SniperTower       — long range, slow fire rate, high damage
│   │   └── ProfessorTower    — strongest type, 1 per faculty, unlocked by clearing building
│   ├── TowerPlacement        — handles click-to-build on valid tower slots
│   └── Projectile            — fired by towers, damages enemies
│
├── Enemies
│   ├── Enemy                 — follows fixed waypoint path, has HP bar, gives gold on death
│   └── EnemyGimmick          — special behaviors for harder courses
│
├── Economy
│   ├── CurrencyManager       — gold for buying towers in a level (resets each level)
│   ├── CreditManager         — credits earned per level cleared (first clear only), 240 total to graduate
│   └── SkillPointManager     — skill points earned alongside credits (first clear only), spent in skill tree
│
└── UI
    ├── HUD                   — gold, lives, round counter, enemies left, round timer, credits
    ├── TowerShopUI            — tower selection buttons (click to select → click slot to build)
    ├── SkillTreeUI            — 4 sections: Social, Internship, Part-time, Certifications
    ├── OverworldUI            — HKU pixel map with buildings to select
    └── GameOverUI             — win/lose/graduation screen
```

---

## Folder Structure

Create these in `Assets/`:

```
Assets/
├── Scripts/
│   ├── Core/           GameManager, ProgressManager, SkillTreeManager
│   ├── Map/            Waypoints, TowerSlot, PathManager, PathPatternData
│   ├── Enemies/        Enemy, EnemyGimmick
│   ├── Towers/         Tower, RapidTower, SniperTower, ProfessorTower, TowerPlacement, Projectile
│   ├── Waves/          WaveSpawner, WaveData
│   ├── Economy/        CurrencyManager, CreditManager, SkillPointManager
│   └── UI/             HUDManager, TowerShopUI, SkillTreeUI, OverworldUI, etc.
├── Prefabs/
│   ├── Enemies/
│   ├── Towers/
│   └── Projectiles/
├── ScriptableObjects/
│   ├── EnemyData/
│   ├── TowerData/
│   ├── WaveData/
│   ├── FacultyData/
│   ├── PathPatterns/     (pre-built path layouts grouped by difficulty)
│   └── SkillTreeData/
├── Scenes/
│   ├── MainMenu
│   ├── Overworld         (HKU top-view pixel map)
│   └── Gameplay
├── Sprites/              (32x32 pixel art)
└── Audio/
```

---

## Step-by-Step Implementation Plan

Each step tells you **what to do in Unity Editor** (🎮) and **what to code** (💻).
Follows the proposal's MVP approach: one tower, one professor, one enemy first.

---

### STEP 1: Project Setup & Folders

🎮 **In Unity:**
1. Open the project (already created with URP 2D)
2. In Project panel → create ALL the folders listed above
3. Delete the default `SampleScene` if you want, or keep it as your gameplay scene
4. Create 3 scenes in `Assets/Scenes/`: **MainMenu**, **Overworld**, **Gameplay**

💻 **No code yet.**

---

### STEP 2: Path & Tower Slots Setup (Bloons TD Style)

Enemies follow a **fixed path**, towers placed on **predefined slots** beside it.

🎮 **In Unity (Gameplay scene):**
1. Create an **Empty GameObject** → name it "Path"
2. As children, create **Empty GameObjects**: "SpawnPoint0", "Waypoint1", "Waypoint2", ... "Exit"
   - For easy levels: just 1 "SpawnPoint0" at the start
   - For harder levels: "SpawnPoint0", "SpawnPoint1", "SpawnPoint2" (enemies come from multiple entrances!)
3. Position them in the Scene view to form a winding route
   - Tip: space them evenly, smooth curves look better
4. Create another Empty GameObject → name it "TowerSlots"
5. As children, create Empty GameObjects: "Slot0", "Slot1", "Slot2", etc.
   - Position them beside the path (not on it!)
6. This path layout = one "path pattern". You'll make many of these as prefabs/data, grouped by difficulty:
   - **Easy patterns** (for 1xxx courses): 1 spawn, simple winding path, many tower slots
   - **Medium patterns** (for 2xxx-3xxx courses): 1-2 spawns, longer paths, fewer slots
   - **Hard patterns** (for 4xxx courses): 2-3 spawns, complex paths converging, tight slot placement
7. Each level randomly picks one pattern from the appropriate difficulty pool

💻 **Code: `Waypoints.cs`** (attached to "Path" object)
- `Transform[] spawnPoints` — 1 or more entry points (harder levels = more spawns)
- `Transform exitPoint` — the exit
- `Transform[] allPoints` — ordered waypoint chain from spawn to exit
- `Vector3 GetPositionAtDistance(float dist)` — smooth enemy movement

💻 **Code: `TowerSlot.cs`** (attached to each slot)
- `bool isOccupied`
- `Tower currentTower`
- `void PlaceTower(Tower t)` / `void RemoveTower()`
- Visual feedback: highlight when player is in placement mode

💻 **Code: `PathPatternData.cs`** (ScriptableObject)
- `Vector3[] waypointPositions` — the path shape
- `Vector3[] spawnPointPositions` — entry points
- `Vector3 exitPosition`
- `Vector3[] towerSlotPositions` — where slots go
- `int difficultyTier` — 1 (easy) to 4 (hard), matches course code

💻 **Code: `PathManager.cs`** (MonoBehaviour)
- `PathPatternData[] easyPatterns, mediumPatterns, hardPatterns`
- On level load → pick random pattern from the correct tier
- Instantiate waypoints and tower slots from the pattern data

---

### STEP 3: Enemy System (One Basic Enemy)

🎮 **In Unity:**
1. Create a new **Sprite** GameObject → name it "BasicEnemy"
   - Use a red circle/square placeholder (0.5 x 0.5 scale)
   - Add components: `Enemy` script, `Rigidbody2D` (Kinematic), `BoxCollider2D`
2. Drag into `Prefabs/Enemies/`
3. Create a ScriptableObject asset:
   - Right-click `ScriptableObjects/EnemyData/` → Create → EnemyData → name "BasicCourseMonster"
   - Set: speed=2, maxHP=100, goldReward=10

💻 **Code: `EnemyData.cs`** (ScriptableObject)
```
- string enemyName          ("ELEC1001 Bug")
- float moveSpeed
- int maxHealth
- int goldReward
- Sprite sprite
- int courseTier             (1=easy, 4=hard — scales with course code)
```

💻 **Code: `Enemy.cs`** (MonoBehaviour)
```
- Spawns at first waypoint (entrance)
- Moves along the waypoint path: Waypoint0 → Waypoint1 → ... → WaypointN
- Smooth movement between waypoints using MoveTowards or lerp
- Has currentHealth, takes damage
- On death → CurrencyManager.AddGold(goldReward)
- On reaching final waypoint (exit) → LivesManager.LoseLife(1) → Destroy self
```

---

### STEP 4: Tower System (One Basic Tower)

🎮 **In Unity:**
1. Create a **Sprite** GameObject → blue square placeholder (0.8 x 0.8)
   - Add: `Tower` script, child empty "FirePoint"
2. Make it a prefab in `Prefabs/Towers/`
3. Create ScriptableObject: "BasicTower" — range=3, fireRate=1, damage=25, cost=50

💻 **Code: `TowerData.cs`** (ScriptableObject)
```
- string towerName
- TowerType type             (Rapid / Balanced / Sniper / Professor)
- int cost
- float range               (in world units)
- float fireRate             (shots per second)
- int damage
- Sprite sprite
- GameObject projectilePrefab
- bool isProfessorTower      (if true, only available after clearing a building)
- string requiredFaculty     (which building must be cleared to unlock, for professor towers)
```

💻 **Code: `Tower.cs`** (MonoBehaviour — base class)
```
- Lives on a TowerSlot
- Every fireRate interval → find closest enemy within range → fire projectile
- Stats can be buffed by skill tree bonuses
- Virtual methods for subclass overrides
```

💻 **Code: Tower subclasses:**
- `RapidTower.cs` — short range, fast fire, low damage per hit
- `BalancedTower.cs` — medium everything
- `SniperTower.cs` — long range, slow fire, high damage
- `ProfessorTower.cs` — strongest, unique per faculty (e.g., EEE Professor tower has chain lightning)

💻 **Code: `TowerPlacement.cs`** (MonoBehaviour — on a manager object)
```
- Listens for mouse click input (PC first)
- Click tower button in shop → enters placement mode
- Tower slots highlight (visual feedback)
- Click a TowerSlot → validate: slot not occupied, player can afford
- On confirm → instantiate tower prefab on the slot, deduct gold
```

---

### STEP 5: Projectile System

🎮 **In Unity:**
1. Yellow circle sprite (0.15 scale) → add `Projectile` script, `Rigidbody2D` (Kinematic), `CircleCollider2D` (Trigger)
2. Prefab in `Prefabs/Projectiles/`

💻 **Code: `Projectile.cs`**
```
- Set target enemy + damage on spawn
- Move toward target each frame
- OnTriggerEnter2D → if hit target → deal damage → Destroy self
- If target dies mid-flight → Destroy self
- Auto-destroy after X seconds (safety)
```

---

### STEP 6: Wave Spawner

🎮 **In Unity:**
1. Empty GameObject "WaveSpawner" in Gameplay scene
2. Create WaveData ScriptableObjects for testing:
   - Wave 1: 5x BasicCourseMonster, 1s interval
   - Wave 2: 8x BasicCourseMonster, 0.8s interval
   - Wave 3: 12x BasicCourseMonster, 0.6s interval

💻 **Code: `WaveData.cs`** (ScriptableObject)
```csharp
[System.Serializable]
public class EnemyGroup {
    public EnemyData enemyType;
    public int count;
    public float spawnInterval;
}

// WaveData fields:
// EnemyGroup[] groups
// float delayBetweenGroups
```

💻 **Code: `WaveSpawner.cs`**
```
- Takes WaveData[] for the current level
- Spawns enemies one by one at a spawn point (if multiple spawn points, alternates or splits)
- Between rounds: brief pause
- Tracks active enemies count
- All rounds done + 0 enemies alive → Level Complete → award credits + skill points
- All lives lost → Level Failed (player can retry)
```

---

### STEP 7: GameManager & Economy

🎮 **In Unity:**
1. Create Empty "GameManager" → attach script → set to DontDestroyOnLoad

💻 **Code: `GameManager.cs`** (singleton)
```
- Game states: MainMenu, Overworld, Playing, Paused, LevelWon, LevelLost, Graduated
- Tracks which levels are completed, which faculty buildings are fully cleared
- References to all managers
```

💻 **Code: `CurrencyManager.cs`** — in-level gold
```
- int gold (resets each level)
- AddGold(int) / SpendGold(int) / CanAfford(int)
- event OnGoldChanged
```

💻 **Code: `LivesManager.cs`** — player lives per level
```
- int lives (e.g., starts at 20)
- LoseLife(int) → if lives <= 0 → Level Failed (retry)
- event OnLivesChanged
```

💻 **Code: `CreditManager.cs`** — overall progression
```
- int totalCredits (persistent, saved to disk)
- On level complete → AddCredits(amount based on course)
- If totalCredits >= 240 → GRADUATED! (you win the game)
```

💻 **Code: `SkillPointManager.cs`** — earned alongside credits
```
- int skillPoints (persistent, saved to disk)
- On level complete → AddSkillPoints(amount)
- Spent in the skill tree UI between levels
```

---

### STEP 8: Skill Tree System

🎮 **In Unity:**
1. Design the skill tree UI as a separate screen accessible from the Overworld
2. 4 sections, each themed as an extracurricular activity:
   - **Social** — e.g., +tower range, +gold earned
   - **Internship** — e.g., +tower damage, +fire rate
   - **Part-time Work** — e.g., +starting gold, +lives
   - **Certifications** — e.g., multi-target, special projectiles
3. Each section has a tree of nodes. Spend skill points to unlock nodes.

💻 **Code: `SkillTreeData.cs`** (ScriptableObject)
```
- SkillSection[] sections      (Social, Internship, Part-time, Certs)
- Each section has SkillNode[]
- Each node: name, description, cost (skill points), prerequisite nodes, BuffEffect
```

💻 **Code: `SkillTreeManager.cs`** (MonoBehaviour)
```
- Tracks which nodes are unlocked (persistent, saved to disk)
- UnlockNode(node) → spend skill points, apply buff
- GetTotalBuffs() → aggregated buffs from all unlocked nodes
- Buffs apply to ALL towers globally (damage%, range%, fireRate%, etc.)
```

💻 **Code: `BuffEffect.cs`** (data class)
```
- float damageMultiplier
- float rangeMultiplier
- float fireRateMultiplier
- int bonusStartGold
- int bonusLives
- bool multiTarget
```

---

### STEP 9: Overworld — HKU Map & Faculty Buildings

🎮 **In Unity:**
1. Create the **Overworld** scene
2. Design a top-down pixel art map of HKU campus
3. Each building is clickable → opens that faculty's level list
4. Each faculty shows 3-5 levels (course codes), with completion status
5. Show a lock icon on building or badge on Professor tower when building is fully cleared

💻 **Code: `FacultyData.cs`** (ScriptableObject)
```
- string facultyName         ("EEE" / "FBE" / "ART")
- string fullName             ("Electrical & Electronic Engineering")
- Sprite buildingSprite
- LevelData[] courses         (3-5 levels, e.g., ELEC1001, ELEC2001, ELEC3001...)
- TowerData professorTower    (the professor tower unlocked by clearing this building)
- bool isCleared              (all courses completed?)
```

💻 **Code: `LevelData.cs`** (ScriptableObject)
```
- string courseCode            ("ELEC1001")
- int courseTier               (1-4, difficulty)
- int creditsReward
- int skillPointsReward
- WaveData[] rounds            (3-5 rounds per level)
- Sprite classroomBackground   (unique background per level)
- int pathDifficultyTier       (determines which pool of path patterns to pick from)
```

💻 **Code: `OverworldManager.cs`**
```
- Shows the HKU map, handles building clicks
- Opens level select panel for the clicked faculty
- Tracks completion state of each building & course
- When all courses in a building cleared → unlock Professor tower + notify player
```

---

### STEP 10: UI (PC First)

🎮 **In Unity:**
1. Canvas (Screen Space - Overlay, Canvas Scaler = "Scale With Screen Size", ref 1920x1080 for PC)
2. **Gameplay HUD** (top): Gold, Lives, Round # / total rounds, Enemies Left, Round Timer, Credits
3. **Tower shop** (left side): tower buttons with costs, color-coded by tower type. Click to select → click slot to build.
4. **Overworld**: Pixel art HKU map with clickable buildings → level list per building
5. **Skill tree screen**: Accessible from overworld, 4 sections with node trees
6. **Level complete / failed** popup — shows rewards earned for first clear, "Already Completed" for replays
7. **Graduation** screen — shown when 240 credits reached

💻 **Code:** `HUDManager.cs`, `TowerShopUI.cs`, `SkillTreeUI.cs`, `OverworldUI.cs`, `GameOverUI.cs`
- Subscribe to events from CurrencyManager, LivesManager, WaveSpawner, CreditManager
- Tower buttons: click to select → click tower slot to build
- Skill tree: click nodes to unlock, show costs and dependencies

---

## Implementation Priority (MVP First)

| Phase | What | Proposal Week |
|-------|------|---------------|
| **MVP** | 1 fixed path + tower slots + 1 enemy + 1 tower (balanced) + projectile + basic rounds | Week 1 |
| **Core+** | Gold economy, lives, tower placement, wave spawner, level win/lose/retry | Week 1-2 |
| **3 Tower types** | Rapid, Balanced, Sniper towers with different stats | Week 2 |
| **Credits+Skills** | Credit system, skill point system, basic skill tree (1-2 sections) | Week 2 |
| **Overworld** | HKU pixel map, building selection, level list per faculty | Week 3 |
| **Path patterns** | Multiple pre-built paths per difficulty tier, random selection | Week 3 |
| **Professor towers** | 1 Professor tower unlocked by clearing first building | Week 3 |
| **Full skill tree** | All 4 sections (Social/Internship/Part-time/Certs) | Week 4 |
| **More content** | More buildings, levels, enemy types, classroom backgrounds | Week 4 |
| **Polish** | Balance, audio, VFX, playtesting, cloud saves | Week 5 |

---

## Key Unity Concepts for Beginners

| Concept | What It Means |
|---------|---------------|
| **Waypoints** | Empty GameObjects positioned in the scene that define a path for enemies |
| **LineRenderer** | Component that draws a line through points — useful to visualize the path |
| **ScriptableObject** | Data-only asset (enemy stats, tower stats, wave data) — edit in Inspector |
| **Prefab** | Reusable template for GameObjects (enemies, towers, projectiles) |
| **Singleton** | A pattern where only one instance of a class exists (GameManager) |
| **Event / Action** | C# events let scripts communicate without direct references |
| **Canvas Scaler** | Makes UI scale properly across different phone screen sizes |
| **DontDestroyOnLoad** | Keeps a GameObject alive when loading new scenes |

---

## Next Steps

We'll build this in order. Start with:
1. **You create the folders** in Unity (Step 1)
2. **I'll write all the C# scripts** starting from the Grid system
3. **I'll tell you exactly what to do** in Unity Editor for each step

Tell me when you're ready to start coding!

---

## Currently Implemented Features

### Core Systems
- **GameManager** — persistent singleton, game state machine, level transitions, save/load via PlayerPrefs
- **GameBootstrap** — ensures persistent managers exist when testing from any scene
- **GameplayAutoSetup** — auto-creates ALL gameplay objects at runtime (no manual scene building required)
- **MainMenuSetup / OverworldSetup** — auto-build menu and overworld scenes at runtime
- **ProjectSetupWizard** — Editor tool (Graduation → Setup Starter Data) generates all prefabs and ScriptableObjects

### Gameplay
- **Fixed waypoint paths** with random pattern selection per difficulty tier (3 easy patterns included)
- **Wave spawner** with multi-round support, auto-start button, round completion detection
- **Tower placement** — click tower in shop → click slot to build, preview ghost follows mouse
- **4 tower types** — Rapid (cyan), Balanced (blue), Sniper (purple), Professor (gold), color-coded
- **Projectiles** — homing toward target, auto-destroy on miss
- **Enemies** with HP bars (green → red gradient), follow waypoint paths
- **Level completion** — only triggers when ALL enemies of the final round are dead
- **Professor tower unlock** — requires clearing all courses in a faculty

### Economy & Progression
- **Gold** — per-level currency for tower purchases (resets each level), skill tree bonuses applied
- **Credits** — persistent across sessions, earned on first clear ONLY (replays give zero), 240 to graduate
- **Skill Points** — earned on first clear ONLY, spent in skill tree
- **Skill tree** — 4 sections (Social, Internship, Part-time, Certifications), buff all towers globally
- **New Game** — resets all progress (credits, skill points, skill tree, completed courses)

### HUD & UI
- **Top bar** — Gold, Lives, Round counter, Credits, Enemies Left, Round Timer
- **Tower shop** (left panel) — color-coded buttons per tower type
- **Pause toggle** — overlay with Resume / Quit to Map
- **Level Won panel** — shows "+X Credits / +X Skill Points" on first clear, "Already Completed" on replay
- **Level Lost panel** — Retry / Back to Map
- **Graduation panel** — shown when 240 credits reached
- **Main Menu** — Continue / New Game / Quit

### Art-Swap Ready Architecture
All visual elements are designed for easy art replacement:
- **Enemy sprites**: Assign custom `Sprite` in `EnemyData` ScriptableObject → automatically used instead of placeholder
- **Tower sprites**: Assign custom `Sprite` in `TowerData` ScriptableObject → automatically used
- **Projectile sprites**: Assign custom sprite on the prefab's SpriteRenderer
- **Level backgrounds**: Assign `classroomBackground` sprite in `LevelData` → rendered behind gameplay, auto-scaled to camera
- **Path visuals**: Modify `PathManager.BuildPathFromPattern()` to use custom tile/line sprites
- **HP bars**: Already procedural (green→red gradient), can be replaced with custom UI prefab
- **All placeholders use `RuntimeSprite`** — procedurally generated white square/circle, only used when no art is assigned

### Round Timer (Professor Tower Unlock Condition)
- Timer starts when a round begins and stops when all enemies are cleared
- Displayed in HUD as "Time: Xs" — can be used as unlock condition for professor towers
- To implement timed unlock: compare `roundTimer` against a threshold in the level-won handler
