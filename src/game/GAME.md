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

Controls:

| Button | Key | Does |
|---|---|---|
| 0 / 1 | ← → | Walk |
| 2 / 3 | ↑ ↓ | Take a stair / climb it |
| 4 | Z | Jump |
| 5 | X | Swing — one press per state: address → pull back → swing through |
| 6 | C | Back out of a swing that has not been taken yet |
| 7 | V | Next club |

Pause menu (Enter) carries the `DEBUG: ON/OFF` toggle, which is persisted in `dget`/`dset` slot 0.

---

## Frame flow

`YourGame` is a forward to the current `Room`; the room runs its occupants.

```
Init()   Debug.Init()
         gjson(GAME/START).ROOM  →  Room.Enter(name)
                                      Room.Load(name)      read ROOMS/<name>
                                      Club.Init()          the bag first — the ball leaves the club face
                                      Ball.Init(room)      before the player: the swing reads it frame 1
                                      Player.Init(room)      → Dust.Init, Steps.Init, Swing.Init → Meter.Init
                                      Flag.Init(room)
                                      Hud.Init()

Update() Player.Update      walk / climb / gravity, then Dust, Steps, Swing (→ Meter)
         Ball.Update        gravity, bounce, roll, drop into the cup
         Flag.Update        wave clip
         Club.Update        club swap — after the player, so the swing state it checks is this frame's

Draw()   map(BACKPOS)       backdrop layer — the screen is never cleared
         map(CELLPOS)       the room itself
         Flag.Draw
         Ball.DrawHoleDebug over the flag it is measured from
         Player.Draw        Dust under the body, body, club sprite over it, miss text
         Ball.Draw
         Meter.Draw / Club.Draw / Hud.Draw     HUD, screen pixels
         Debug.Draw         last, over everything
```

There is no camera: a room is exactly one screen (`Room.CellW` × `Room.CellH` = 32×18 cells), so world
pixels and screen pixels are the same thing. Everything except the HUD works in **map-sheet pixels**
(cell × 8), which is the space `mcol` uses.

---

## Files

| File | Owns |
|---|---|
| [YourGame.cs](YourGame.cs) | Engine entry point. Reads `GAME/START.ROOM` and forwards the three methods to `_room`. |
| [Room.cs](Room.cs) | One room from `ROOMS/<name>`: which cells it cuts out of the sheet, where the backdrop is, and the spawn points. Turns room-relative authored positions into map-sheet pixels once, on entry. |
| [Player.cs](Player.cs) | Walk, jump, stair climb, pixel-stepped collision, address/align to the ball. Static. |
| [Ball.cs](Ball.cs) | Ball physics, bounce, roll, and sinking into the cup. Drawn as a blinking 2×2 rect, not a sprite. Static. |
| [Swing.cs](Swing.cs) | The swing state machine and the power reading. Owned by the player, drawn over it. Static. |
| [Meter.cs](Meter.cs) | The strength bar that sweeps while the club is back. Static. |
| [Club.cs](Club.cs) | The bag: which club is selected, what it does to the shot, and the label over the meter. Static. |
| [Terrain.cs](Terrain.cs) | The map read as terrain — solid, stair columns. Stateless. |
| [Flag.cs](Flag.cs) | The flag sprite and its wave clip. The cup is measured off it. Static. |
| [Hud.cs](Hud.cs) | Shot counter, plus `PrintOutlined` which every HUD caption uses. Static. |
| [Dust.cs](Dust.cs) | Foot dust particle pool, fixed size, allocated once. Static. |
| [Steps.cs](Steps.cs) | Footstep sfx on a wall-clock interval while walking. Static. |
| [Anim.cs](Anim.cs) | Reusable sprite flipbook from an `ANIM/<name>` object. The one instance class. |
| [Debug.cs](Debug.cs) | The one `Enabled` switch every overlay reads, toggled from the pause menu, persisted in slot 0. |
| [API_REFERENCE.md](API_REFERENCE.md) | Full `IMono8API` reference. Documentation, not game code. |

Most of the game is `static` — there is one player, one ball, one swing. `Anim` is the exception,
since the player, the flag and the swing each hold their own.

---

## The swing

`Swing` is a four-state machine, one press per state, with `PRESS` seconds minimum between presses and
the button re-armed only by letting go — it cannot be mashed through.

```
Idle ──X──> Ready ──X──> Pull ──X──> Hit ──(HITSEC / FAILSEC)──> Idle
             │            │
             └────C───────┘  cancel: club away, meter dropped
```

- **Idle → Ready** only if `Player.CanStartSwing()` — both feet on the ground and the ball inside the
  `CLUBX` + `REACH` window. `Player.AlignToBall()` then slides the player a pixel at a time so the club
  head lands on the ball, stopping flush if a wall is in the way. `Meter.Show()` puts the empty bar up.
- **Ready → Pull** plays `ANIM/GOLFPULL` (one-shot) and starts the meter sweeping.
- **Pull → Hit** reads `Meter.Value` **once**, right there. Under `MISS` the swing fails outright
  (`Failed`, the player shouts `FAILTXT`, no sfx, no shot counted); otherwise `MISS..1` is stretched
  onto `MINHIT..1` so any connecting swing moves the ball. Plays `ANIM/GOLFHIT`.
- The ball leaves on the **last frame** of the hit clip, not the press — `Clip.Done`, once per hit.

While `Swing.Active` the player is committed: no walking, no jumping, no stair grab, no club swap.

`Ball.Hit(toLeft, power)` reads `BALL/HITX` and `HITY` as a **speed and a launch angle**, not two
velocities, so the club can loft or shorten the shot without either being re-authored:

```
speed = |(HITX, HITY)| * power * Club.Distance
angle = clamp(atan2(HITY, HITX) + Club.Angle, 0, 0.25 turns)   // 0 when power <= Club.GroundPower
```

That last clause is the putter: at or under `GNDPWR` the shot stays flat however lofted the club is.

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
remainder carried between frames. At these speeds a frame of travel is several pixels and stepping is
what keeps anything from tunnelling through a thin wall; it also lands the stop flush against
quadrant-precise autotile edges, which have no tile boundary to snap to.

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

## data.json map

| Group / object | Read by | Holds |
|---|---|---|
| `GAME/START` | `YourGame` | `ROOM` — which `ROOMS` object to open on |
| `ROOMS/<name>` | `Room` | `CELLPOS` (map cells), `BACKPOS` (backdrop cells, absolute), `PLYRPOS` `BALLPOS` `FLAGPOS` (pixels within the room) |
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

Clips currently authored: `FLAG`, `GOLFPULL`, `GOLFHIT`, `PLRWALK`, `PLRSTAIR`.

Sfx ids fixed in code: `0` club on ball, `1` ball into the cup. Everything else (footsteps, club swap)
is authored as a list in json.

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
- **No per-frame allocation.** Fixed pools (`Dust`, `Club`, `Steps`), captions rebuilt only when the
  number moves (`Hud.Count`), `Swing.State` returns literals.
- **Comments say why, not what.** The density here is deliberate: the tricky invariants (the one-pixel
  step-in on a stair grab, reading the meter before anything else can move it, the ball leaving on the
  clip's last frame) are commented; the obvious is not.

---

## Not done yet

- `Ball.Holed` is set and then ignored — no hole-complete, no room progression, no scorecard.
- One room. `Room.Enter` can be called again with another name, but nothing calls it.
