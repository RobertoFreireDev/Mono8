# Mono8 — Golf

The current state of the game in [src/game/](.). Read this before changing anything here: it says what
each file owns, how a frame runs, and where every tuning value comes from.

Everything is authored in [src/publishdata/data.json](../publishdata/data.json). **Nothing is tuned in
code** — sprite ids, speeds, hit boxes, spawn points, sfx ids and HUD geometry are all read through
`gjson` in `Init()`. When you need a new number, name the group/object/field the developer has to
author rather than hardcoding it.

---

## What the game is

A side-on golf platformer on one screen. The player walks, jumps and climbs stairs around a room cut
out of the map sheet, walks up to the ball, addresses it, swings, and tries to sink it in the cup the
flag marks. A shot counter in the corner counts every stroke that actually sent the ball.

The game opens on the **level select** — a grid of numbers, one per room, picked with the mouse.

Controls:

| Button | Key | Does |
|---|---|---|
| 0 / 1 | ← → | Walk |
| 2 / 3 | ↑ ↓ | Take a stair / climb it |
| 4 | Z | Jump |
| 5 | X | Swing — one press per state: address → pull back → swing through |
| 6 | C | Back out of a swing that has not been taken yet |
| 7 | V | Next club |

The level select is mouse only: the number under the pointer turns green, and the left button picks it.

Pause menu (Enter) carries the `DEBUG: ON/OFF` toggle, persisted in `dget`/`dset` slot 0, and
`LEVELS`, which goes back to the level select. Restart also lands there, since it re-runs `Init`.

---

## Frame flow

`YourGame` is a forward to either the level select or the current `Room`; the room runs its
occupants. `LevelSelect.Active` is the switch, and only one of the two runs in a frame.

```
Init()   Debug.Init()
         LevelSelect.Init()   MENU/GRID lays the grid out, ROOMS says which numbers are levels

Update() menu up:
         LevelSelect.Update   hover, and the click that picks
         a pick  →  Room.Enter(name)
                      Room.Load(name)      read ROOMS/<name>
                      Club.Init()          the bag first — the ball leaves the club face
                      Ball.Init(room)      before the player: the swing reads it frame 1
                      Player.Init(room)      → Dust.Init, Steps.Init, Swing.Init → Meter.Init
                      Flag.Init(room)
                      Hud.Init()

         menu down:
         Player.Update      walk / climb / gravity, then Dust, Steps, Swing (→ Meter)
         Ball.Update        gravity, bounce, roll, drop into the cup
         Flag.Update        wave clip
         Club.Update        club swap — after the player, so the swing state it checks is this frame's

Draw()   menu up:
         LevelSelect.Draw   cls, the title, the numbers

         menu down:
         map(BACKPOS)       backdrop layer — the screen is never cleared
         map(CELLPOS)       the room itself
         Flag.Draw
         Ball.DrawHoleDebug over the flag it is measured from
         Player.Draw        Dust under the body, body, club sprite over it, miss text
         Ball.Draw
         Meter.Draw / Club.Draw / Hud.Draw     HUD, screen pixels

         Debug.Draw         last, over everything — over the menu too
```

A picked room is entered from `YourGame.Update`, not from the menu, so `Room.Enter` is called from
one place. The room does not update the frame it is entered: its first frame is the next one.

There is no camera: a room is exactly one screen (`Room.CellW` × `Room.CellH` = 32×18 cells), so world
pixels and screen pixels are the same thing. Everything except the HUD works in **map-sheet pixels**
(cell × 8), which is the space `mcol` uses.

---

## Files

| File | Owns |
|---|---|
| [YourGame.cs](YourGame.cs) | Engine entry point. Forwards the three methods to the level select or to `_room`, and enters the room a pick names. |
| [LevelSelect.cs](LevelSelect.cs) | The level grid: which numbers are levels, where each one prints, which one the pointer is on, and the pause-menu entry that comes back to it. Static. |
| [Room.cs](Room.cs) | One room from `ROOMS/<name>`: which cells it cuts out of the sheet, where the backdrop is, and the spawn points. Turns room-relative authored positions into map-sheet pixels once, on entry. `Exists` is what the level select asks. |
| [Player.cs](Player.cs) | Walk, jump, stair climb, pixel-stepped collision, address/align to the ball. Static. |
| [Ball.cs](Ball.cs) | Ball physics, bounce, roll, and sinking into the cup. Drawn as a blinking `SIZE`-square rect, not a sprite. Static. |
| [Swing.cs](Swing.cs) | The swing state machine and the power reading. Owned by the player, drawn over it. Static. |
| [Meter.cs](Meter.cs) | The strength bar that sweeps while the club is back, filled in ten colour bands weakest-first. Static. |
| [Club.cs](Club.cs) | The bag: which club is selected, what it does to the shot, and the swapping label over the meter. Static. |
| [Terrain.cs](Terrain.cs) | The map read as terrain — solid, stair columns. Stateless. |
| [Flag.cs](Flag.cs) | The flag sprite and its wave clip. The cup is measured off it. Static. |
| [Hud.cs](Hud.cs) | Shot counter. Static. |
| [Dust.cs](Dust.cs) | Foot dust particle pool, fixed size, allocated once. Static. |
| [Steps.cs](Steps.cs) | Footstep sfx on a wall-clock interval while walking. Static. |
| [Anim.cs](Anim.cs) | Reusable sprite flipbook from an `ANIM/<name>` object. Instance. |
| [SfxList.cs](SfxList.cs) | A sfx array field played one at a time at random — the footsteps and the club swap. Instance. |
| [Motion.cs](Motion.cs) | The pixel-stepped travel and the gravity clamp the player and the ball both move by. Stateless. |
| [Btn.cs](Btn.cs) | Button indices by name, so no `btn` call carries a bare number. |
| [Font.cs](Font.cs) | The engine font's advance and line height, the string width captions are placed by, and `PrintOutlined` — the one call every caption in the game is drawn with. |
| [Debug.cs](Debug.cs) | The one `Enabled` switch every overlay reads, toggled from the pause menu, persisted in slot 0. Draws the corner readout; the boxes belong to whoever owns them. |
| [API_REFERENCE.md](API_REFERENCE.md) | Full `IMono8API` reference. Documentation, not game code. |

Most of the game is `static` — there is one player, one ball, one swing. `Room`, `Anim` and `SfxList`
are the exceptions: `YourGame` holds a room instance so entering another is just another `Enter`, the
player, the flag and the swing each hold their own clips, and the footsteps and the club swap each
hold their own sounds.

**Every type here is in scope engine-wide** — [src/GlobalUsings.cs](../GlobalUsings.cs) carries
`global using mono8.game`, so a new game type whose name collides with a MonoGame one breaks the
*engine's* build. That is why the buttons are `Btn` and not `Buttons`.

---

## The level select

A grid of level numbers centred on the screen, laid out by `MENU/GRID` and defaulting to the 5×4 of
twenty the game asks for. **Level N is the room authored as the object named `N` under `ROOMS`** —
there is no list of levels anywhere else, so authoring `ROOMS/7` is what makes level 7 exist. A number
with no room behind it is not drawn at all: the gap in the grid is the disabled state.

Each number prints through `Font.PrintOutlined` — white with a one-pixel black outline, green while the
pointer is on it. The hover box is the number itself grown by `PAD` on every side, not the whole cell,
so the numbers stay separately pickable however wide the cells are. The pick is taken on the mouse
*press*: the menu is gone before the button comes back up, and the release would otherwise land in the
room it just opened.

The whole grid — the level each number stands for, whether it is authored, where it prints — is
measured once in `Init`, so a frame of the menu allocates nothing and asks json nothing.

The menu is where the game starts and where the pause menu's `LEVELS` entry goes back to. That entry
is registered only while a room is running, and the menu clears it while it is up.

`GAME/START` is no longer read: the level select replaced the fixed opening room, so the object can be
deleted or repurposed.

---

## The swing

`Swing` is a four-state machine, one press per state, with `PRESS` seconds minimum between presses and
the button re-armed only by letting go — it cannot be mashed through.

```
Idle ──X──> Ready ──X──> Pull ──X──> Hit ──(HITSEC / FAILSEC)──> Idle
             │            │
             └────C───────┘  cancel: club away, meter dropped
```

- **Idle → Ready** only if `Player.CanStartSwing()` — both feet on the ground, the ball still in play
  (one already dropping into the cup is not), and the ball inside the `CLUBX` + `REACH` window.
  `Player.AlignToBall()` then slides the player a pixel at a time so the club head lands on the ball,
  stopping flush if a wall is in the way. `Meter.Show()` puts the empty bar up.
- **Ready → Pull** plays `ANIM/GOLFPULL` (one-shot) and starts the meter sweeping.
- **Pull → Hit** waits for the pull clip to finish (`Clip.Done` — there is no swinging through a club
  still going back), then reads `Meter.Value` **once**, right there. Under `MISS` the swing fails
  outright (`Failed`, the player shouts `FAILTXT`, no sfx, no shot counted); otherwise `MISS..1` is
  stretched onto `MINHIT..1` so any connecting swing moves the ball. Plays `ANIM/GOLFHIT`.
- The ball leaves on the **last frame** of the hit clip, not the press — `Clip.Done`, once per hit.

While `Swing.Active` the player is committed: no walking, no jumping, no stair grab, no club swap.

`Ball.Hit(toLeft, power)` reads `BALL/HITX` and `HITY` as a **speed and a launch angle**, not two
velocities, so the club can loft or shorten the shot without either being re-authored:

```
speed = |(HITX, HITY)| * power * Club.Distance
angle = clamp(atan2(HITY, HITX) + Club.Angle, 0, 0.25 turns)   // 0 when power <= Club.GroundPower
```

That last clause is the putter: at or under `GNDPWR` the shot stays flat however lofted the club is.

### The bag

`CLUBS/ORDER.LIST` names the club objects in swap order and V walks them, blocked while `Swing.Active`
so the club addressed is the club that hits. Each swap plays one of `ORDER.SFX` at random. A club named
in `LIST` but never authored is skipped rather than loaded as a zero-distance one, and an empty bag
leaves the ball hitting exactly as `BALL/HITX`/`HITY` say — `Angle` 0, `Distance` 1, `GroundPower` 0.

The label sits over the meter bar and is drawn whether or not the bar is up, so a club can be picked
while walking. Over `HUD/CLUB.SWAPSEC` the outgoing name drops away and the incoming one comes down
from above, both set back by `SWAPX`/`SWAPY` at their extremes so the pair reads as one face turning
rather than two labels sliding past each other. A second press part way through picks the turn up from
whatever is showing.

---

## Animation and the idle still

`Anim` walks an `ANIM/<name>` object: `ID` the sprite ids (authored as Text, parsed once on load),
`SPEED` in frames per second, `MODE` matched without regard to case — `FW` forward, `BW` or `RV`
backward, `PP` ping-pong. A clip loops unless it is loaded with `loop: false`, which makes it a
one-shot that holds the frame it ends on and reports `Done` — that flag is what the swing times both of
its clips on. An unauthored `SPEED` or a single-frame list can never reach an end on its own, so a
one-shot over one is `Done` the moment it starts.

The player runs one clip at a time and only while actually travelling, so a walk into a wall does not
march on the spot. Off a stair, stopping rewinds `PLRWALK` and the body falls back to
`PLAYER/STATS.SPR` — the idle still, which is also what an unauthored clip falls back to. On a stair
the frame is held instead, since a rung is a place to stand. Crossing between the two clips rewinds the
one being taken up, and addressing the ball is that same idle still with the club sprite swinging over
it.

---

## Terrain and collision

Two sprite flags, both authored in the sprite editor:

| Flag | Meaning |
|---|---|
| `1` | **Solid** — nothing passes through it. `Terrain.Solid`, and what `Ball` collides against. |
| `0` | **Stair** — a climbable column. A tile carrying both caps a stair: plain floor to anyone walking over it, a doorway to anyone on it. |

`Terrain.Blocked` is that second reading — solid *minus* stair tiles — and is what `Player.SolidAt`
switches to while `Climbing`. That branch is the whole stair feature: a climb passes through the cap
tiles while a real ceiling or floor still stops it.

Both the player and the ball move **one pixel at a time**, x and y separately, with a fractional
remainder carried between frames (`Motion.Pixels`). At these speeds a frame of travel is several
pixels and stepping is what keeps anything from tunnelling through a thin wall; it also lands the
stop flush against quadrant-precise autotile edges, which have no tile boundary to snap to. What a
blocked step means is each body's own: the player stops, the ball bounces.

Stairs: ↑ takes any stair the body already stands in; ↓ takes the one under its feet (so a stair
capping a platform is entered from above), gated on there being floor there to leave. The grab centres
the body on the column. A climb is released when the body has cleared every stair tile, or when it
lands on floor while not pressing up.

---

## The cup

The hole is read off the **flag**, not the map: `BALL/HOLEPOS` and `HOLESIZE` are a rect in pixels from
the flag sprite's top-left. The ball drops in when it is on the ground, moving slower than `HOLESPD` on
both axes, and its centre is inside that rect. It then sinks `SINKDEP` pixels straight down at
`SINKSPD`, ignoring terrain (the cup is a hole in ground the map still reads as solid), plays sfx 1,
and clears `Present` / sets `Holed`.

`Holed` stays set until the room is re-entered. **Nothing currently reacts to it** — there is no
next-room, win screen or score tally yet.

---

## The debug overlay

`Debug.Enabled` is off by default and toggled from the pause menu. It persists in `dget`/`dset` slot 0
offset by one — `1` off, `2` on — so a fresh save reading `0` lands on off rather than on.

`Debug.Draw` itself only puts FPS, `Swing.State` and the power reading in the top-left corner. Every
box is drawn by whoever owns it, each guarded against an unauthored size that `rect` would draw
inverted:

| Drawn by | Shows |
|---|---|
| `Player.Draw` | the `HITPOS`/`HITSIZE` rect `SolidAt` actually tests — yellow while climbing, red otherwise |
| `Player.DrawAddressDebug` | the `CLUBX` + `REACH` window, green once the ball is inside it |
| `Ball.DrawHoleDebug` | the cup rect, over the flag it is measured from so it stays readable |

---

## data.json map

| Group / object | Read by | Holds |
|---|---|---|
| `MENU/GRID` | `LevelSelect` | `COLS` `ROWS` (grid, default 5×4), `CELL` (cell size in pixels, default `(32, 20)`), `PAD` (hover box margin, default 3), `TITLE` (caption over the grid, none by default) — **not authored yet; the defaults run without it** |
| `ROOMS/<name>` | `Room`, `LevelSelect` | `CELLPOS` (map cells), `BACKPOS` (backdrop cells, absolute — defaults to `(256, 0)`, the start of map layer 2), `PLYRPOS` `BALLPOS` `FLAGPOS` (pixels within the room). The object name is the level number the menu shows |
| `PLAYER/STATS` | `Player` | `SPR` `SPRSIZE` `HITPOS` `HITSIZE` `SPEED` `CLIMB` `GRAVITY` `JUMP` `MAXFALL` `CLUBX` `REACH` `FAILTXT` `FAILY` |
| `BALL/STATS` | `Ball` | `SIZE` `GRAVITY` `MAXFALL` `BOUNCE` `FRICTION` `HITX` `HITY` `BLINK` `REST` `HOLEPOS` `HOLESIZE` `HOLESPD` `SINKDEP` `SINKSPD` |
| `SWING/POWER` | `Swing`, `Meter` | `SWEEP` (seconds for one out-and-back), `MISS`, `MINHIT` |
| `SWING/CLUB` | `Swing` | `SPR` (club at rest), `PRESS`, `HITSEC`, `FAILSEC` |
| `CLUBS/ORDER` | `Club` | `LIST` — the club objects in swap order; `SFX` — the swap sounds to pick from |
| `CLUBS/<name>` | `Club` | `NAME`, `ANGLE` (degrees), `DIST`, `GNDPWR` |
| `HUD/METER` | `Meter` | `MARGIN` `BARW` `BARH` `BORDER` |
| `HUD/HITS` | `Hud` | `LABEL` `MARGIN` |
| `HUD/CLUB` | `Club` | `GAP` `SWAPSEC` `SWAPX` `SWAPY` |
| `ANIM/<name>` | `Anim` | `ID` (sprite ids as Text), `SPEED` (fps), `MODE` — `FW` / `BW` / `RV` / `PP` |
| `ANIM/PLRWALK` | also `Dust`, `Steps` | `PRTMAX` `PRTRATE` `PRTLIFE` `PRTPOS` `PRTVEL` `PRTGRAV` `PRTBIG`; `SFX` `SFXSEC` |

An unknown room, or one missing a field, loads as an empty room at the top-left of the sheet rather
than failing. A room without `FLAGPOS` has no flag — and with no flag to measure from, no cup. Only
`ROOMS/1` is authored, so the menu currently shows one number out of twenty.

Clips currently authored: `FLAG`, `GOLFPULL`, `GOLFHIT`, `PLRWALK`, `PLRSTAIR`.

Sfx ids fixed in code: `0` club on ball, `1` ball into the cup. Everything else (footsteps, club swap)
is authored as a list in json and read through `SfxList`, which drops a negative or wrong-typed entry
rather than loading it.

---

## Conventions this code follows

- **Re-read every `Init()`.** Ctrl+S in the JSON editor rebuilds the data without a restart, and the
  rebuild makes *new* objects — a cached `Mono8JsonObject` from before the save is orphaned. Every
  loader zeroes its fields first, then reads, so a half-authored object still runs.
- **Never throw.** Null-check the `gjson` result, pass a real fallback to each getter, gate `GetXY` on
  `Has` where `(0, 0)` would be wrong. An exception freezes the engine with a message rather than
  crashing, so failures are quiet.
- **Guard the degenerate draw.** An unauthored `SIZE`/`HITSIZE`/`BARW` is 0, and `rect` with an empty
  extent draws inverted — every debug and HUD rect checks first.
- **No per-frame allocation.** Fixed pools (`Dust`, `Club`, `SfxList`), captions rebuilt only when the
  number moves (`Hud.SetHits`), `Swing.State` returns literals.
- **One home per shared number.** The cell size is `Terrain.TileSize`, the font metrics are `Font`,
  the button indices are `Btn` — a literal `8`, `4` or `5` in game code is a bug waiting to drift.
- **Comments say why, not what.** The density here is deliberate: the tricky invariants (the one-pixel
  step-in on a stair grab, reading the meter before anything else can move it, the ball leaving on the
  clip's last frame) are commented; the obvious is not.

---

## Not done yet

- `Ball.Holed` is set and then ignored — no hole-complete and no scorecard. Sinking the ball leaves
  the room where it is; the pause menu's `LEVELS` is the only way out.
- One room authored. The level select can open any of the twenty, but nineteen of the numbers have no
  `ROOMS` object behind them and so are not drawn.
- Nothing carries between levels: the shot counter starts again at every `Room.Enter`, and no level
  is recorded as cleared.
