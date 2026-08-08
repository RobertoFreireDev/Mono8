# Mono8 — Golf

The state of the game in [src/game/](.). **Every class documents itself** — the XML doc comment on
each type says what it owns and why it works the way it does, and the tricky invariants are commented
at the line. This file holds only what reading one file cannot tell you: the contracts that span
files, what the developer has authored so far, and what is known broken.

**Nothing is tuned in code.** Sprite ids, speeds, hit boxes, spawns, sfx and HUD geometry are read
through `gjson` in `Init()`. When you need a new number, name the group/object/field for the
developer to author rather than hardcoding it.

---

## What the game is

A side-on golf platformer, one hole per screen. The player walks, jumps and climbs stairs around a
room cut out of the map sheet, addresses the ball, swings, and tries to sink it in the cup the flag
marks. A stroke counter counts down from the room's `HITMAX`; running out restarts the level. Sinking
the ball closes an iris onto the player and brings the next level up behind it. The game opens on a
**level select** grid, which is also where the run ends.

| Button | Key | Does |
|---|---|---|
| 0 / 1 | ← → | Walk (and walk the menu cursor) |
| 2 / 3 | ↑ ↓ | Take a stair / climb it |
| 4 | Z | Jump — also picks a level on the menu |
| 5 | X | Swing, one press per state: address → pull back → swing through — also picks |
| 6 | C | Back out of a swing not yet taken |
| 7 | V | Next club |

No mouse anywhere: `Init` calls `API.mouse(false)`.

### Pause menu

The engine allows five entries and lists them in index order, so **the index is the row**. Each
belongs to whoever owns the action, and taking one down is `menuitem(index)` with no label:

| Index | Entry | Owner | Up on |
|---|---|---|---|
| 0 | `DEBUG: ON/OFF` | `Debug` | a room |
| 1 | `LEVELS` | `LevelSelect` | a room |
| 2 | `RESTART LEVEL` | `YourGame` | a room |
| 3 | `DELETE SAVE` | `Save` | both screens |

`LevelSelect.Show`/`Close` raise and lower the first three together — on the menu there is nothing to
overlay, go back to or restart. `DELETE SAVE` is registered once in `Save.Init` and never taken down.

---

## Frame flow

`YourGame` forwards to either the level select or the one `Room` instance; `LevelSelect.Active` is
the switch and only one runs per frame. **Every room entry goes through `YourGame.Enter`** — which is
why `RESTART LEVEL` lives there and why the menu cursor never falls behind the level being played.

```
Init()    Debug → Levels → Save → Wipe → LevelSelect      (Levels first: the grid and the save slots
                                                           are both indexed by room NUMBER)

Update()  menu up:   LevelSelect.Update; a pick → YourGame.Enter(name)
          menu down: Room.Update  (Player → Ball → Flag → Club → Clouds → out-of-bounds / spent strokes)
                     Ball.Holed?  → Save.Complete, then Wipe.Start — once
                     Wipe.Update(player, in screen pixels)
                     Wipe.Closed? → YourGame.Advance: next level, or back to the menu

Draw()    menu up:   LevelSelect.Draw
          menu down: map(BACKPOS) → camera(origin) → Sun → map(CELLPOS) → Flag → Player → Ball
                     → Moon → Clouds → Night → camera() → Meter → Club → Hud
          both:      Wipe.Draw, then Debug.Draw over everything
```

`Room.Enter` order matters in three places, and each is commented at the call: `Club` before the ball
can leave a club face, `Sun` before `Player` (no sun, no shadow), `Moon` after `Sun` (it measures
itself against the sun's sky), `Ball` before `Player` (the swing reads it on frame 1).

A room does not update the frame it is entered — its first frame is the next one. `Wipe` is the one
thing that outlives a room, so it is `YourGame`'s and nothing inside a room resets it; it runs
*after* the room rather than instead of it, so a body still falls while the screen closes.

### The two spaces

A room is exactly one screen (`Room.CellW` × `CellH` = 32×18 cells) cut out of the map sheet.
Everything inside a room lives in **map-sheet pixels**; the HUD, the backdrop, the `Wipe` and the
level select live in **screen pixels**.

The authored spawns (`PLYRPOS`, `BALLPOS`, `FLAGPOS`) are map-sheet pixels taken exactly as written —
`CELLPOS` says which screenful the room is and nothing more, so **moving a room means moving all four
fields together**.

`Room.OriginX`/`OriginY` is `CELLPOS × 8`, and **world minus origin is screen** — the one conversion
anything outside a room needs. All four sky bodies — `Sun`, `Moon`, `Clouds`, `Night` — are authored
and measured in screen pixels and add the origin themselves, since they are drawn with the room's
camera up: `Sun` bakes it into `X`/`Y` at `Init`, the other three cache the corner and add it at draw.

---

## Files

| File | Owns |
|---|---|
| [YourGame.cs](YourGame.cs) | Entry point; forwards to the menu or `_room`, owns the `Wipe` and `RESTART LEVEL`, is the one place a room is entered |
| [LevelSelect.cs](LevelSelect.cs) | The grid, the cursor, the sliding preview of the level under it, and what "the next level" means |
| [Levels.cs](Levels.cs) | Which room is which level — indexes `ROOMS` by `NUMBER` once at `Init` |
| [Wipe.cs](Wipe.cs) | The `ovalinv` iris between levels, and the switch the player's controls are off behind |
| [Room.cs](Room.cs) | One room from `ROOMS/<name>`: its cut of the sheet, spawns, edges, `HITMAX`, `NUMBER`; restarts the level on a loss |
| [Player.cs](Player.cs) | Walk, jump, stair climb, pixel-stepped collision, address/align to the ball, the sun's shadow |
| [Ball.cs](Ball.cs) | Ball physics, bounce, roll, sinking into the cup. Drawn as a blinking rect, not a sprite |
| [Swing.cs](Swing.cs) | The four-state swing machine and the power reading |
| [Meter.cs](Meter.cs) | The strength bar |
| [Club.cs](Club.cs) | The bag: selection, what it does to the shot, the swapping label |
| [Terrain.cs](Terrain.cs) | The map read as terrain — solid, stair columns. Stateless |
| [Flag.cs](Flag.cs) | The flag sprite and its wave clip. The cup is measured off it |
| [Sun.cs](Sun.cs) | The sun by the hour, its halo, and **the sky's geometry** (`Margin`, `Tiles`, `Span`) the moon shares |
| [Moon.cs](Moon.cs) | The moon by the day of the month. Sprite only — the dark is `Night`'s |
| [Night.cs](Night.cs) | The hours that are night and the one dim over the room's screenful. `Dim` is what the moon asks whether it is out |
| [Clouds.cs](Clouds.cs) | The clouds crossing a room's sky |
| [Hud.cs](Hud.cs) | Strokes left, two zero-padded digits; `Taken` is what a sunk hole records, `RightX` where the club label starts |
| [Save.cs](Save.cs) | The levels finished, one persistence slot each. Owns `DELETE SAVE` |
| [Dust.cs](Dust.cs) | Foot dust pool, fixed size |
| [Steps.cs](Steps.cs) | Footstep sfx on an interval while walking |
| [Anim.cs](Anim.cs) | Sprite flipbook from an `ANIM/<name>` object. Instance |
| [SfxList.cs](SfxList.cs) | A sfx array played one at a time at random. Instance |
| [Motion.cs](Motion.cs) | The pixel-stepped travel and gravity clamp the player and the ball share |
| [Btn.cs](Btn.cs) | Button indices by name |
| [Font.cs](Font.cs) | Font metrics and `PrintOutlined`, the one call every caption is drawn with |
| [Debug.cs](Debug.cs) | The `Enabled` switch every overlay reads. Boxes belong to whoever owns them |
| [API_REFERENCE.md](API_REFERENCE.md) | Full `IMono8API` reference. Documentation, not game code |

Most of the game is `static` — one player, one ball, one swing. `Room`, `Anim` and `SfxList` are the
exceptions.

**Every type here is in scope engine-wide** — [src/GlobalUsings.cs](../GlobalUsings.cs) carries
`global using mono8.game`, so a game type whose name collides with a MonoGame one breaks the
*engine's* build. That is why the buttons are `Btn` and not `Buttons`.

---

## Contracts across files

- **Level N is the room whose `NUMBER` is N**, whatever the object is called. There is no list of
  levels anywhere else: authoring a room with `NUMBER: 7` is what makes level 7 exist, and a number
  with no room behind it is a gap the grid skips and the cursor steps over. `Levels` turns a name
  into a number and back.
- **`Save` slot N is level N.** Slot 0 is `Debug`'s (offset by one: `1` off, `2` on, so a fresh save
  reads off). Slots 1-63 hold the strokes a hole was sunk in, or `-1` for never finished — `0` means
  *nothing written yet*, which `Save.Init` maps to `-1`. `Levels.MaxNumber` is 63 because that is the
  last persistence slot there is. Only a sunk hole writes; losing, leaving or backing out never does.
- **Flag `0` is a stair** alongside the project's solid flag `1`. A tile carrying both caps a stair —
  floor to anyone walking over it, a doorway to anyone on it. `Terrain.Blocked` is solid *minus*
  stair and is what a climb passes through.
- **Sfx ids fixed in code:** `0` club on ball, `1` ball into the cup. Everything else is authored as
  a json list and read through `SfxList`. **Sprite ids fixed in code: none.**
- **Both bodies move one pixel at a time**, x and y separately, with a fractional remainder carried
  between frames (`Motion.Pixels`) — at these speeds a frame is several pixels and stepping is what
  stops tunnelling through thin walls and lands stops flush against quadrant-precise autotile edges.
  What a blocked step *means* is each body's own: the player stops, the ball bounces.
- **The cup is read off the flag, not the map** — `BALL/HOLEPOS`/`HOLESIZE` is a rect from the flag
  sprite's top-left. No `FLAGPOS`, no flag, and so no cup.
- **`Sun.Margin`, `Sun.Tiles` and `Sun.Span` are the sky's, not the sun's.** There is one sky and it
  is authored once, on `DAYCYCLE/SUN`; the moon crosses the same line and authors no size of its own.
- **The player's shadow is gated on `Sun.Present`**, and the level select's preview draws neither sun
  nor moon — both are drawn from inside a room.

---

## data.json map

| Group / object | Read by | Holds |
|---|---|---|
| `MENU/GRID` | `LevelSelect` | `COLS` `ROWS` (default 5×4), `CELL` (px, default `(32, 20)`), `TITLE`. `PAD` is authored but **no longer read** |
| `ROOMS/<name>` | `Room`, `Levels`, `LevelSelect` | `CELLPOS` (cells), `BACKPOS` (cells, absolute, default `(256, 0)` = map layer 2), `PLYRPOS` `BALLPOS` `FLAGPOS` (map-sheet px, absolute), `HITMAX` (default 5; ≤0 = unlimited), `NUMBER` (`1`-`63`) |
| `GAME/WIPE` | `Wipe` | `WAITSEC` `OUTSEC` `INSEC` `COLOR` `DITHER` — **not authored; defaults run**. `COLOR` and `DITHER` are a pair: the dither sprite's holes are white, so the draw is wrapped in `palt(7, true)` and the mask colour must be the sprite's other colour |
| `DAYCYCLE/SUN` | `Sun`, and `Moon` for the sky | `SPR` `TILES`, `MARGIN`, `DAWNHR` `DUSKHR` (outside them, no sun at all), `GLOWRAD` `GLOWCOL` (paired by index, widest last) `GLOWOPA` |
| `DAYCYCLE/NIGHT` | `Moon` and `Night`, a half each | **`Moon`:** `SPR`, `MONTHDAY`. **`Night`:** `DEEPFROM` `DEEPTO` (wraps midnight, so `DEEPFROM` > `DEEPTO` is the expected shape) `DEEPOPA`, `DUSKFROM` `DAWNTO` `TWILOPA`. Hours between the bands are undimmed and moonless |
| `CLOUDS/<name>` | `Clouds` | One *kind* of cloud: `SPRIDX` `TILESX` `TILESY`. Every object but `CONFIG` is one |
| `CLOUDS/CONFIG` | `Clouds` | `MINCLOUD` `MAXCLOUD`, `STRTPOSX` `STRTPOSY` (`[min, max]` bands), `SPEED` (px/s, rightward), `MINDISTX` `MINDISTY` (clearance, a box not a radius) |
| `PLAYER/STATS` | `Player` | `SPR` `SPRSIZE` `HITPOS` `HITSIZE` `SPEED` `CLIMB` `GRAVITY` `JUMP` `MAXFALL` `CLUBX` `REACH` `FAILTXT` `FAILY` |
| `BALL/STATS` | `Ball` | `SIZE` `GRAVITY` `MAXFALL` `BOUNCE` `FRICTION` `HITX` `HITY` `BLINK` `REST` `HOLEPOS` `HOLESIZE` `HOLESPD` `SINKDEP` `SINKSPD`. **`HITX`/`HITY` are a speed and a launch angle**, not two velocities, so a club lofts or shortens the shot without either being re-authored |
| `SWING/POWER` | `Swing`, `Meter` | `SWEEP` `MISS` `MINHIT` |
| `SWING/CLUB` | `Swing` | `SPR` `PRESS` `HITSEC` `FAILSEC` |
| `CLUBS/ORDER` | `Club` | `LIST` (club objects in swap order) `SFX` |
| `CLUBS/<name>` | `Club` | `NAME` `ANGLE` (degrees) `DIST` `GNDPWR` (at or under it the shot stays flat — the putter) |
| `HUD/METER` | `Meter` | `MARGIN` `BARW` `BARH` `BORDER` |
| `HUD/HITS` | `Hud` | `MARGIN` |
| `HUD/CLUB` | `Club` | `GAP` `SWAPSEC` `SWAPX` `SWAPY` |
| `ANIM/<name>` | `Anim` | `ID` (sprite ids as Text) `SPEED` (fps) `MODE` — `FW` / `BW` / `RV` / `PP` |
| `ANIM/PLRWALK` | also `Dust`, `Steps` | `PRTMAX` `PRTRATE` `PRTLIFE` `PRTPOS` `PRTVEL` `PRTGRAV` `PRTBIG`; `SFX` `SFXSEC` |

`GAME/START` is dead — the level select replaced the fixed opening room. It can be deleted.

### Authored so far

- **Twenty rooms**, `ROOMS/01`-`20`, numbered `1`-`20`. Only `01`-`05` have a cut of the map sheet of
  their own, along the top row (`CELLPOS` `(0, 0)`, `(32, 0)`, `(64, 0)`, `(96, 0)`, `(128, 0)`);
  `06`-`20` all point back at `02`'s `CELLPOS` with their own spawns — the numbers exist, the levels
  do not.
- **Clips:** `FLAG`, `GOLFPULL`, `GOLFHIT`, `PLRWALK`, `PLRSTAIR`.
- **Clubs:** `DRIVER`, `WEDGE`, `PUTTER`.
- **Clouds:** three kinds (`O1`-`O3`), `CONFIG` fully authored.
- **`DAYCYCLE`** fully authored, `DUSKHR` retuned to `15`.
- **Unauthored:** `GAME/WIPE` only.

---

## Conventions on top of CLAUDE.md's

- **Every loader zeroes its fields before reading**, so a half-authored object still runs.
- **Read degenerate authoring rather than failing on it.** A band authored backwards is swapped, a
  size under 1 is taken as 1, a zero or negative speed/radius is dropped, an empty list is an empty
  feature.
- **Guard the degenerate draw.** An unauthored `SIZE`/`HITSIZE`/`BARW` is 0, and `rect` with an empty
  extent draws inverted — every debug and HUD rect checks first.
- **One home per shared number.** `Terrain.TileSize`, `Font`, `Btn` — a literal `8`, `4` or `5` in
  game code is a bug waiting to drift.

---

## Not done yet

- **No scorecard.** `Save` holds the stroke count and nothing draws it: the grid shows *whether* a
  hole is done, not what it was done in. No par, no per-hole result screen, nothing at the end of a
  run but the level select coming back up.
- **Fifteen placeholder rooms** (`06`-`20`) waiting for a map.
- **Running out of strokes restarts the room with no notice** — no caption, no pause, no sound.
- **`GAME/WIPE` is unauthored**; the wipe runs on code defaults.
