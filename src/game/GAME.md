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
flag marks. A counter in the corner holds the strokes the room allows and counts them off — every
stroke that actually sent the ball takes one, and running out restarts the level.

Sinking the ball ends the hole: the controls go dead, an iris closes onto the player, and the next
level comes up behind it. Run out of levels and it puts you back on the menu.

The game opens on the **level select** — a grid of numbers, one per room, walked with the d-pad.

Controls:

| Button | Key | Does |
|---|---|---|
| 0 / 1 | ← → | Walk |
| 2 / 3 | ↑ ↓ | Take a stair / climb it |
| 4 | Z | Jump |
| 5 | X | Swing — one press per state: address → pull back → swing through |
| 6 | C | Back out of a swing that has not been taken yet |
| 7 | V | Next club |

The level select takes no mouse: ← → ↑ ↓ walk the cursor around the grid, and Z or X picks the level
it is on.

Pause menu (Enter opens it, and confirms once it is open — it does not toggle, so the way out is
*Continue*) carries four entries of the game's own, between the engine's *Continue* and its
*Restart Game* / *Exit*:

| Entry | Does | Up on |
|---|---|---|
| `DEBUG: ON/OFF` | the overlay switch, persisted in `dget`/`dset` slot 0 | a room |
| `LEVELS` | back to the level select | a room |
| `RESTART LEVEL` | the level over again — spawns, strokes and all | a room |
| `DELETE SAVE` | every persistence slot back to empty | both screens |

The first three are a room's: on the level select there is nothing to overlay, nothing to go back to
and no level to restart, so `LevelSelect.Show`/`Close` take them down and put them back up together.
`DELETE SAVE` is the exception — it is registered once in `Save.Init` and never taken down, since the
grid is where progress is looked at and so the menu is the screen a wipe is most wanted on. The
engine's own *Restart Game* re-runs `Init`, which lands on the level select.

Each index lives with whatever owns the action — `Debug` 0, `LevelSelect` 1, `YourGame` 2, `Save` 3 —
and the engine lists them in index order, so the index is also the row. The engine allows five.

`RESTART LEVEL` is `YourGame`'s because every room entry goes through `YourGame.Enter`, and it drops
the `Wipe` on the way: a hole sunk and then restarted is no longer on its way to the next level, and
an iris left closing would advance straight past the level just restarted.

---

## Frame flow

`YourGame` is a forward to either the level select or the current `Room`; the room runs its
occupants. `LevelSelect.Active` is the switch, and only one of the two runs in a frame.

```
Init()   API.mouse(false)     nothing is aimed — the pointer would only be in the way
         Debug.Init()
         Levels.Init()        walk ROOMS, read each NUMBER — which room is which level
         Save.Init()          the levels finished so far, read out of data.save
         Wipe.Init()          nothing on screen — a restart lands on the menu
         LevelSelect.Init()   MENU/GRID lays the grid out, Levels says which numbers are levels

Update() menu up:
         LevelSelect.Update   the d-pad walks the cursor, and the Z that picks
         a pick  →  YourGame.Enter(name)
                      LevelSelect.Focus    the cursor follows the level being played
                      Room.Enter(name)
                        Room.Load(name)    read ROOMS/<name>
                        Club.Init()        the bag first — the ball leaves the club face
                        Sun.Init(room)     before the player: the shadow only falls where there is a sun
                                           the hour places it; the room only lends its corner
                        Moon.Init()        after the sun: the night hangs off the sky the sun authors
                        Ball.Init(room)    before the player: the swing reads it frame 1
                        Player.Init(room)    → Dust.Init, Steps.Init, Swing.Init → Meter.Init
                        Flag.Init(room)
                        Hud.Init(HITMAX)   the room's strokes, counted down

         menu down:
         Player.Update      walk / climb / gravity, then Dust, Steps, Swing (→ Meter)
         Ball.Update        gravity, bounce, roll, drop into the cup
         Flag.Update        wave clip
         Club.Update        club swap — after the player, so the swing state it checks is this frame's
         out of bounds?     player or ball off the room  →  Room.Enter(Name), the level over again
                            skipped while the wipe is up
         Ball.Holed?        →  Save.Complete(room, Hud.Taken), then Wipe.Start() — once
         Wipe.Update        the iris, focused on the player wherever the frame left them
         Wipe.Closed?       →  YourGame.Advance(): the next level, or back to the menu

Draw()   menu up:
         LevelSelect.Draw   cls, the previewed level (sliding), the title, the discs and numbers

         menu down:
         map(BACKPOS)       backdrop layer, screen pixels — the screen is never cleared
         camera(origin)     the room's corner onto (0, 0)
         Sun.Draw           between the maps — sky, so the room's own cells pass in front of it
         map(CELLPOS)       the room itself
         Flag.Draw
         Ball.DrawHoleDebug over the flag it is measured from
         Player.Draw        shadow, Dust under the body, body, club sprite over it, miss text
         Ball.Draw
         Moon.Draw          the moon, then the hour's dim — inside the room, so under the HUD
         camera()
         Meter.Draw / Club.Draw / Hud.Draw     HUD, screen pixels

         Wipe.Draw          over the room and its HUD, whichever screen is up
         Debug.Draw         last, over everything — over the menu too
```

Every room entry goes through `YourGame.Enter`, whether the level select picked it or the last hole
advanced onto it, so `Room.Enter` is called from one place and the menu's cursor is moved from one
place. The room does not update the frame it is entered: its first frame is the next one.

`Wipe` is the one thing that spans two rooms, so it is `YourGame`'s and nothing inside a room resets
it. It runs *after* the room rather than instead of it: while it is up the room keeps moving — the
body falls, the club finishes the swing it was mid-way through — and only the controls are gone.

A room is exactly one screen (`Room.CellW` × `Room.CellH` = 32×18 cells) cut out of the map sheet
wherever its `CELLPOS` says, so there are two spaces and one conversion between them:

- **map-sheet pixels** (cell × 8) — everything in the room. Positions, hit boxes, terrain queries;
  it is the space `mcol` works in, so nothing has to be translated to ask the map a question.
- **screen pixels** — the HUD, the backdrop, the `Wipe`, the level select.

`Room.Draw` sets `camera(OriginX, OriginY)` for the room layer and resets it for the HUD, so a room
anywhere on the sheet lands on the screen the same way. `Room.OriginX` / `OriginY` is `CELLPOS × 8`,
and world minus origin is screen — the one conversion anything outside a room needs, which is how
`YourGame` hands the player's position to the `Wipe`.

The authored spawns (`PLYRPOS`, `BALLPOS`, `FLAGPOS`) are map-sheet pixels too, taken exactly as
written — `CELLPOS` says which screenful of the sheet the room is, and nothing more. So a spawn is
authored with the coordinate the map editor shows for that tile, and moving a room means moving all
four fields together.

---

## Files

| File | Owns |
|---|---|
| [YourGame.cs](YourGame.cs) | Engine entry point. Forwards the three methods to the level select or to `_room`, owns the `Wipe` that carries one room into the next, and is the one place a room is entered — which is why the pause menu's `RESTART LEVEL` is here too. |
| [LevelSelect.cs](LevelSelect.cs) | The level grid: which numbers are levels, which have been sunk, where each one prints, where the cursor can walk, and the pause-menu entry that comes back to it. Also the preview of the level the cursor is on, and the slide from one to the next. Also what "the next level" means. Static. |
| [Levels.cs](Levels.cs) | Which room is which level. Reads every object under `ROOMS` once at `Init` and indexes them by their `NUMBER`, so the object name stays the developer's and the number is what the grid and the save slots key on. Static. |
| [Wipe.cs](Wipe.cs) | The iris between levels — the `ovalinv` mask closing onto the player and opening back out, and the switch the player's controls are off behind. Static. |
| [Room.cs](Room.cs) | One room from `ROOMS/<name>`: which cells it cuts out of the sheet, where the backdrop is, and the spawn points. Its spawns are authored in map-sheet pixels and taken as written. Owns the room's edges — `Left`/`Right` are public, since the player walks into them — and its `HITMAX` and `NUMBER`, and restarts the level when a body leaves the edges or the strokes run out. |
| [Player.cs](Player.cs) | Walk, jump, stair climb, pixel-stepped collision, address/align to the ball. Takes the room's sides as walls, so the walk never leaves the screen sideways. Static. |
| [Ball.cs](Ball.cs) | Ball physics, bounce, roll, and sinking into the cup. Drawn as a blinking `SIZE`-square rect, not a sprite. Static. |
| [Swing.cs](Swing.cs) | The swing state machine and the power reading. Owned by the player, drawn over it. Static. |
| [Meter.cs](Meter.cs) | The strength bar that sweeps while the club is back, filled in ten colour bands weakest-first. Static. |
| [Club.cs](Club.cs) | The bag: which club is selected, what it does to the shot, and the swapping label over the meter. Static. |
| [Terrain.cs](Terrain.cs) | The map read as terrain — solid, stair columns. Stateless. |
| [Flag.cs](Flag.cs) | The flag sprite and its wave clip. The cup is measured off it. Static. |
| [Moon.cs](Moon.cs) | The night: the moon placed across the sky by the day of the month, and the hour's dim over it, both authored under `DAYCYCLE/NIGHT`. Drawn from inside the room, after the ball and before the HUD. Reads the clock and its own object and nothing else — no room, which is also why it is in the wrong space at its call site (see [The moon and the night](#the-moon-and-the-night)). Static. |
| [Sun.cs](Sun.cs) | The sun, placed across the screen by the local hour off `stat(4)` — drawn between the backdrop and the room's cells, and none at all outside daylight. Owns `DAYCYCLE/SUN`, which is the sky itself: `Margin`, `Tiles` and the `Span` derived from them are public because the moon crosses the same line. `Present` is also what says whether the player casts a shadow. Static. |
| [Hud.cs](Hud.cs) | The strokes left, counted down from the room's `HITMAX` and drawn as two zero-padded digits at the left end of the row over the meter. `OutOfShots` is what loses the level, `Taken` is what a sunk hole is recorded as, `RightX` is where the `Club` label starts. Static. |
| [Save.cs](Save.cs) | The levels finished, one `dget`/`dset` slot each — the strokes a hole was sunk in, or `-1` for one never finished. Read once at `Init`, written only by a hole dropping in. Owns the pause menu's `DELETE SAVE`, which puts every slot back to empty. Static. |
| [Dust.cs](Dust.cs) | Foot dust particle pool, fixed size, allocated once. Static. |
| [Steps.cs](Steps.cs) | Footstep sfx on a wall-clock interval while walking. Static. |
| [Anim.cs](Anim.cs) | Reusable sprite flipbook from an `ANIM/<name>` object. Instance. |
| [SfxList.cs](SfxList.cs) | A sfx array field played one at a time at random — the footsteps and the club swap. Instance. |
| [Motion.cs](Motion.cs) | The pixel-stepped travel and the gravity clamp the player and the ball both move by. Stateless. |
| [Btn.cs](Btn.cs) | Button indices by name, so no `btn` call carries a bare number. |
| [Font.cs](Font.cs) | The engine font's advance, glyph height and ink middle, the string width captions are placed by, and `PrintOutlined` — the one call every caption in the game is drawn with, its `plain` argument the marker-free string its outline passes use. `Height` is the 7-pixel glyph, which is what centring measures; the engine's own `\n` advance is 9 and nothing here prints multi-line. |
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

A grid of level numbers centred on the screen, laid out by `MENU/GRID` — authored as the 5×4 of
twenty the game asks for, and falling back to that same 5×4 if the object goes missing. **Level N is the room whose `NUMBER` is N**, whatever the developer called
the object — `Levels` is what finds it. There is no list of levels anywhere else, so authoring a room
with `NUMBER: 7` is what makes level 7 exist. A number with no room behind it is not drawn at all: the
gap in the grid is the disabled state.

Each number prints through `Font.PrintOutlined` with a one-pixel black outline, in the colour that
says what it is: **yellow for a hole already sunk, white for one still to play**, read out of `Save`
when the menu comes up rather than measured with the grid — the hole just finished is one of them.

Every number sits on a **disc**, radius 8, centred on the caption's own ink (`Font.Middle` and half
the string width, not the cell) so one digit and two are both centred in it, and ringed by a black
disc one pixel wider behind it. The disc is what makes a number readable now that a whole level is
drawn behind the grid.

Two things say where the cursor is, and both of them are colour:

- **The disc turns white** under it, dark green everywhere else. That is what finds it at a glance —
  a filled shape changing is far louder than the digit on it changing.
- **The number takes the warmer half of its own pair**, so it never has to choose between saying
  where the cursor is and saying whether the hole is done:

| | Not sunk | Sunk |
|---|---|---|
| Cursor elsewhere | White | Yellow |
| **Under the cursor** | **Green** | **Orange** |

← → ↑ ↓ walk the cursor one cell per press, no key repeat. It **clamps** rather than wraps: at the edge
of the grid the press does nothing. Numbers with no room behind them are stepped over — the cursor
keeps going in the direction pressed until it finds an authored one, and stays put if it does not,
since a cursor on an undrawn number would be a cursor that vanished. It opens on the lowest authored
level and is left where it was when the pause menu comes back here, so returning from a level lands on
that level.

`Z` (`Btn.Jump`) or `X` (`Btn.Swing`) picks — either of the two buttons a room answers, since there is
nothing to tell the player which one opens a level. `btnp`, not `btn`, and the room reads both of them
with `btnp` too, so the press that picked cannot go on to be the jump or the address the room's first
frame sees.

The grid is also what "the next level" means. `Next(name)` takes the room the run is on and hands back
the room one number up, so a gap in the numbering is stepped over exactly as the cursor steps over it,
and `null` — no level above this one — is what sends the game back to the menu. `Focus(name)` moves the
cursor onto a level, called on every room entry so a run that has walked from 1 to 4 comes back here
on 4. Both take the **room's object name**, which is what a room carries around; `Levels.Number` turns
it back into the cell it belongs in.

The whole grid — which room each number stands for, whether it is authored, where it prints, where its
disc is centred — is measured once in `Init`, so a frame of the menu allocates nothing and asks json
nothing.

### The preview

Behind the grid is the level the cursor is on, drawn the way `Room.Draw` draws it: the backdrop, the
room's cells, and the flag on its wave clip. The menu reads **only what the picture needs** out of
`ROOMS/<name>` — `CELLPOS`, `BACKPOS`, `FLAGPOS`, and nothing else, since spawns, strokes and the
level number are things a room being *played* needs. That is `LevelSelect.Preview`, a struct, loaded
on a cursor move and never per frame. The camera is the room's own — `CELLPOS × 8` — so the cells and
the flag standing on them land on screen exactly where the level will show them.

Moving the cursor **crosses one picture over the other in half a second** (`SlideSeconds`), in the
direction the move was made on the grid: a press right carries the old level off to the left and
brings the new one in from the right, a press down carries it up. Both slide a full screen and both
fade — the outgoing one from opaque to gone, the incoming one from gone to opaque. The offset is
taken off the *camera* rather than added to each draw, so a picture never comes apart while it moves.
A press part way through a slide drops whatever was still on its way out and starts again from what is
showing, so the picture never lags the cursor.

`Focus` and `Show` **settle** instead: the cursor was put there rather than walked there, and the menu
is not on screen when `Focus` is called. `Show` also re-reads the picture and re-loads the flag clip,
for the same reason it re-reads the results — a Ctrl+S while a level was up rebuilt the objects.

The menu **no longer clears to dark green** — the preview is the backdrop now. It still clears, to
black: mid-slide the two pictures do not cover the screen between them, and a fade over an uncleared
frame smears rather than crossing over.

The menu is where the game starts and where the pause menu's `LEVELS` entry goes back to. That entry,
the `DEBUG` toggle and `RESTART LEVEL` are registered only while a room is running, and the menu
clears all three while it is up.

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

The label sits over the meter bar, set in past the strokes count that shares the row (`Hud.RightX`, one
character of air, plus `SWAPX` so a label mid-turn cannot ride over the number), and is drawn whether or
not the bar is up, so a club can be picked while walking. Over `HUD/CLUB.SWAPSEC` the outgoing name drops away and the incoming one comes down
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

`Holed` stays set until the room is re-entered, and it is what starts the wipe onto the next level.

---

## The sun and the shadow

The sun is hung by the clock, not by the room. `Sun.Init` reads the local hour off `stat(4)` and
places it across the screen: `MARGIN` pixels of clearance at the left, the right and the top, swept
left to right over the daylight hours so `DAWNHR` is against the left margin and `DUSKHR` against the
right. Outside those two hours there is no sun at all, and that is an overcast hole — nothing else
about it changes. As authored that is two tiles of margin and `06:00`–`18:00`.

The room contributes only its `CELLPOS` corner, as the offset that turns that screen position into
the map-sheet pixels everything else in a room is measured in.

The hour is sampled once per `Room.Enter`, so the sun moves between levels rather than during one.
`DAYCYCLE/SUN` is re-read on the same call, so a Ctrl+S retune lands on the next room entry.

The sprite is one block for every room — `SPR`, drawn `TILES` square, authored as sprite `1` at `2×2`
— because every room's sun is the same sun. It draws **between the backdrop and the room's own
cells**, so it sits in the sky the backdrop paints and the terrain passes in front of it rather than
being lit through.

Over the sprite go the translucent discs of `GLOWRAD`, centred on it and drawn in list order so the
widest is last and they layer into a halo rather than one flat wash — as authored, `16`/`20`/`24`
pixels in the matching `GLOWCOL` (`BrightOrange`/`Orange`/`Yellow`) at `GLOWOPA` `0.2`. `GLOWRAD` and
`GLOWCOL` are read as a pair by index; a radius of 0 or less is dropped rather than loaded, and a
radius with no colour beside it draws `Yellow`. Those are the midday radii — they are scaled by how
far into the day it is, `0` at dawn and dusk and `1` at the hour midway between, so the glow opens up
towards noon and is gone entirely at either end. A disc smaller than a tile is skipped rather than
drawn: it would be lost inside the sprite anyway, and it keeps a negative radius — which `circfill`
throws on — from ever reaching the call.

The sky's own geometry is authored on the **sun** and nothing else: `Sun.Margin`, `Sun.Tiles` and the
`Sun.Span` derived from the two (`ResolutionX - 2 × MARGIN - TILES × 8`, floored at 0) are the line
the moon crosses as well. There is one sky, so it is authored once. Degenerate authoring is read
rather than thrown on — a `TILES` under 1 is taken as 1, a `DUSKHR` at or before `DAWNHR` is a day
with no hours in it and reads as overcast.

`Sun.Present` is also the switch on the player's shadow: a black smear one pixel tall at
`ShadowOpacity`, centred on the body, drawn first of everything `Player.Draw` puts down — under the
dust as well as the body.

It is not gated on `OnGround`. Instead the ground is looked for a pixel at a time under the body,
with the same `SolidAt` test `RefreshOnGround` uses, and how far down it is found is what the shadow
is made of:

| Ground under the feet | Width |
|---|---|
| 1px (standing on it) | 6 |
| 2px | 4 |
| 3px | 2 |
| 4px or more | none — the search stops at `ShadowMaxDrop` |

A pixel off each side per pixel of air, so a jump pulls the shadow in to nothing over its first
few pixels rather than blinking it out on the frame it starts. `ShadowMaxDrop` is `ShadowWidth / 2`
rather than a number of its own: at a drop of 4 the two sides would have met, so there is nothing
past it worth probing for. The row the shadow draws on follows the ground down with it.

Two things shape it:

- **It leans away from the sun**, `-2` to `+2` pixels (`ShadowLean`). The lean is the horizontal
  distance from `Sun.X` — the sun's own left edge, not the middle of the sprite it draws — to the
  body's centre, scaled over half a screen and clamped. A sun over the body casts straight down, one
  at the far side of the room casts at the limit, so the lean swings from morning to evening. It says which side the light is on; it
  is not an attempt to trace where the shadow would really fall.
- **It is clipped to the ground it lands on.** Each of its six columns has to be a *surface* at the
  shadow's own row: solid there, and open one pixel above. Solid fails over a drop — a body on the
  lip of a platform loses the part hanging over nothing rather than smearing it across mid-air — and
  open-above fails inside a step or a wall the lean has carried the shadow into, where the row is
  buried in a tile rather than lying on one and the shadow would be painted up its face. Contiguous
  lit columns go out as one `rect`, so flat ground is still a single call.

The level select's preview draws neither sun nor moon — it reads only `CELLPOS`, `BACKPOS` and
`FLAGPOS`, and both bodies are drawn from inside a room.

### The moon and the night

The other half of the same clock. `Moon.Draw` is both halves of the night in one pass: the moon, then
the dark over it.

The dim is the hour's, and the bands are `DAYCYCLE/NIGHT`'s:

| Hours | Dim | Authored as |
|---|---|---|
| `DEEPFROM`–`DEEPTO` | `DEEPOPA` | `22:00`–`02:00` at `0.4` |
| `DUSKFROM`–`DEEPFROM`, `DEEPTO`–`DAWNTO` | `TWILOPA` | `18:00`–`20:00`, `04:00`–`06:00` at `0.2` |
| anything else | nothing drawn at all — no dark, and no moon |  |

Deep night is the one band that runs past midnight, so it is read as two halves where the twilights
are read as one — which is also why `DEEPFROM` above `DEEPTO` is the shape the code expects rather
than a mistake. The hours between the bands — `20:00`–`22:00` and `02:00`–`04:00` as authored — are
undimmed and moonless.

The moon is `SPR` (sprite `129`), drawn at the sun's `TILES`, crossing the same sky the sun does:
`Sun.Margin` down from the top, and `Sun.Margin` to `Sun.Margin + Sun.Span` across, which is why
those are the sun's public geometry. **The night authors no size and no margin of its own** — `Span`
is measured off the sun's `TILES`, so a moon of another size would overrun the line it shares.

What moves it is the **day of the month** (`stat(3)`) rather than the hour, over `MONTHDAY` days —
authored as `31`, the longest month, so the first of a month is against the left margin and the 31st
against the right and it shifts a little each night instead of tracking across an evening. A short
month simply stops before the right margin, and a `MONTHDAY` of 1 or less pins it at the left one
rather than dividing by zero.

Both go on **inside the room**, called from `Room.Draw` between `Ball.Draw` and the `camera()` reset
that starts the HUD. So the moon is in front of the terrain and the ball where the sun is behind the
room's own cells, and the dim falls over all of that — but under the HUD, under the `Wipe` and under
the debug readout, which all draw after it. The level select does **not** call it, so the menu never
falls dark even though its preview is the same outdoors.

Unlike the sun's hour, the clock is read every frame rather than at `Init`: it is two `stat` calls
and nothing else, so the night can fall under a player who stays on one hole. `Moon.Init` exists only
to read the object — the tuning is taken on `Room.Enter` like everything else's, and it is called
after `Sun.Init` because the sky it measures itself against is the sun's.

> **Known mismatch.** `Moon.Draw` measures both the sprite and the dim in **screen** pixels —
> `Sun.Margin`/`Sun.Span` across, and `rectfill(0, 0, ResolutionX - 1, ResolutionY - 1)` — but its
> call site has the camera at the room's origin, which is world space. The two only agree for a room
> whose `CELLPOS` is `(0, 0)`. On every other room the whole night is offset by the origin and lands
> off screen: level 1 dims, levels 2 and up do not. Either the call moves after `camera()` (which
> also puts the dim back over the HUD) or the two coordinates take the room's origin the way the sun
> does — the sun's `Init` already adds `room.OriginX`/`OriginY` for exactly this reason.

---

## Between levels

Sinking the ball is the end of the level, so `YourGame` closes the screen on it. `Wipe` is a five-state
run of one `ovalinv` call, and it is the only thing in the game that outlives a room — which is why it
is `YourGame`'s and why nothing a room does to itself resets it.

```
None ──Ball.Holed──> Wait ──WAITSEC──> Close ──OUTSEC──> Held ──> Open ──INSEC──> None
                                                           │
                                                           └── no next level: Stop(), straight to None
```

- **Wait** is the room still fully on screen, and is what leaves the ball in the cup long enough to be
  seen going in rather than the screen shutting on the frame it lands.
- **Close** shrinks the hole from an oval that just swallows the screen down to nothing, centred on the
  player. The focus is re-read every frame, so a body still falling keeps the iris on it.
- **Held** is the one frame the screen is covered, which is the one frame a room can be swapped
  without it being seen. `YourGame.Advance` reads it: `LevelSelect.Next` names the level, and the
  room is entered right there. With no next level the mask is dropped outright and the level select
  comes up — the menu is its own screen and there is nothing behind it to reveal.
- **Open** runs the same oval the other way, around whoever is standing in the room that came up.

The hole is an ellipse in the screen's own proportions, so it closes evenly instead of pinching. It
starts at the size that just swallows the furthest corner from the focus, which is why the reveal is
clean wherever in the room the player is standing. `ovalinv` reads a hole of nothing as the covered
screen, so `Held` needs no case of its own.

The ring around the hole is the dither sprite `DITHER` tiled one tile deep. That sprite's holes are
authored in **white**, not colour 0, so the draw is wrapped in `palt(7, true)` — and the mask colour
has to be the sprite's other colour or the band reads as a stripe rather than as the mask thinning
out. `COLOR` and `DITHER` are a pair, not two independent choices.

**While the wipe is up the player has no controls.** `Player.Controlled` is the one switch: no walk,
no jump, no stair grab, no new swing. Gravity is deliberately not part of it — a body caught in the
air still settles while the screen closes on it — and a swing already mid-flight still plays itself
out. `Room`'s out-of-bounds restart is off for the same stretch: the hole is already won, and
restarting a level nobody is going to see again would only undo the level about to be loaded.

---

## Strokes

`ROOMS/<name>.HITMAX` is how many strokes the room allows. `Hud` takes it at `Room.Enter`, prints it
through `Font.PrintOutlined`, and counts it down — `Swing.Launch` calls `CountHit` only for a stroke
that actually sent the ball, so a whiff is free. The caption is rebuilt only when the number moves, and
the count is floored at zero.

The count is the **number alone** — no label — at the left end of the row over the meter bar, inset by
`HUD/HITS.MARGIN`, with the `Club` label set in beside it and both on `Club.LabelY`. It is always
**two digits, zero-padded**: `01`, `04`, `20`, and a room authored above `99` reads `99` rather than
printing a third digit into the label. So the slot is a fixed two characters, `Hud.RightX` is a
constant offset from `MARGIN`, and nothing on the row is placed off the number of the moment.

The last strokes are called out in colour, since a number down in the corner is easy to miss while
lining up a shot: **two left is yellow, one is red**, and zero stays red — the frames before the room
restarts are the most urgent the count ever is, not the least. Anything above two is white. The colour
rides on `print`'s inline marker rather than on the call's colour argument: the caption is `#085`, `#`
plus the palette index as two digits. `Hud` caches the marker-free `HitPlain` beside it, because that
is what `Font.PrintOutlined` must draw the four black passes with — a marker would recolour the outline
too — and what any measurement of the caption has to go by, since a marker draws nothing.

Spent strokes lose the level, but not on the stroke itself: the count runs out as the last ball leaves
the club, and that shot is still the one that can drop in. `Room.Update` waits for `Ball.AtRest` — down,
stopped and still in play — and then re-enters the room. A ball that is sinking is not at rest, so a
final putt that goes in wins the hole instead.

`Ball.AtRest` reads the ground contact and the roll (`VelX == 0f`, which `Rolled` and `Bounced` both
settle to exactly), not both velocities: gravity puts a little back into `VelY` every frame the ball is
standing there, and only a step that meets the ground takes it away again.

A room that authors no `HITMAX` gets `Room.DefaultHitMax` (5), and a `HITMAX` of 0 or less is read as
unlimited rather than as a level lost on its first frame.

---

## Progress

`Save` is the run kept across runs: the engine's 64 persistence slots, **one per level**, read once in
`Init` and cached in an array so asking after a level costs nothing.

| Slot | Holds |
|---|---|
| `0` | `Debug.Enabled`, offset by one |
| `1`-`63` | level N's result — the strokes it was sunk in, or `-1` for a level never finished |

**Level N is slot N**, and N is the room's `NUMBER` — not its object name, which is the developer's to
rename at will. `Room.Number` is what `Save.Complete` is handed, and `Levels.Exists` is what `Save.Init`
asks. A room that authors no `NUMBER`, or one outside `1`-`63`, has no slot and is never recorded, which
is what keeps a `ROOMS` object that is not a level out of the save.

A fresh save reads `0` in every slot, so `0` is *nothing written here yet* rather than a hole finished
in no strokes. `Save.Init` maps it to `-1`, and writes that `-1` back for every level that has a room
behind it — so the file says "not played" in its own terms rather than by omission. Slots with no
level behind them are only mapped in memory: there is nothing there to record.

**Only a sunk hole writes.** `YourGame.Update` calls `Save.Complete` on the frame `Ball.Holed` starts
the wipe, which is the one frame a hole is finished on — losing the room to spent strokes, walking out
of bounds and backing out to the level select never reach it, so those leave the slot as it was. The
count written is `Hud.Taken`, counted up as the strokes are struck rather than read off the count
left, which floors at zero and says nothing at all in a room that allows unlimited strokes. Replaying
a level to the same score does not write again — `dset` rewrites the whole file on every call.

`LevelSelect` is what reads the results back: a level `Save.Played` says is done prints yellow instead
of white. It takes them in `Show`, not in `Layout` — the menu can only come back up through `Show`,
and the hole that was just sunk is one of the numbers about to be drawn. The stroke count itself is
stored but not shown anywhere.

`DELETE SAVE` is the one thing that unwrites it. **Every** slot goes, not only the levels: slot 0 is
`Debug`'s and is persistence like any other, and a deleted save that still remembered the toggle would
not have been deleted. Zeroing is followed by the same `Read` `Init` does, so what is left says "not
played" in the terms a fresh save does rather than in terms of its own, and the two places holding a
copy of what went are caught up behind it — `Debug.Clear` for the flag slot 0 carried, and
`LevelSelect.Refresh` for the grid, which caches the results in `Show` and may well be the screen the
entry was chosen from. A slot already reading `0` is left alone, since `dset` rewrites the whole file
on every call.

---

## Out of bounds

A room is one screen and there is nothing outside it, so a body that leaves is never coming back —
the hole is unplayable from there. `Room.Update` asks last, on the positions the frame settled on,
and a loss re-enters the room: same level, everything back at its spawn, the strokes back at `HITMAX`.

- **Left, right and bottom** lose a body. The **top is open**: a lofted shot arcs over the screen and
  gravity brings it back, and that is the shot the game is about.
- **The player cannot reach the sides.** `Room.Left` / `Room.Right` are walls to the walk:
  `Player.BlockedAt` is the map's solid *plus* those two, so a body stops flush against the edge of
  the screen exactly as it stops against terrain, and the alignment slide into a ball by the wall
  stops there too. Losing a still-playable hole to a stray step was never the point of the test —
  what it is there for is the fall out of the bottom, which is untouched, and the ball, which is
  free to leave any of the three. The wall is tested by the **direction** of the step rather than
  by where it lands, so a `PLYRPOS` authored outside its own room can still walk back in instead of
  being pinned; the out-of-bounds test below is still what catches one authored past the bottom.
- The test is *every* pixel past one edge, so clipping a corner on the way past is not a loss.
- The player is measured by `SPRSIZE`, the ball by `SIZE`; an unauthored one reads as 1 rather than
  0, which would take a body flush against the left edge as already gone.
- A ball dropping into the cup is exempt (`Ball.InPlay`) — it is leaving on purpose, and a cup at the
  bottom of the screen could sink it past the edge.
- The whole test is off while the `Wipe` is up: the hole is already won and the room is on its way
  out, so a player left in mid-air when the ball dropped falls out of it harmlessly.

A room whose `PLYRPOS` or `BALLPOS` puts a body outside its own cells restarts every frame. That is
an authoring error rather than a crash, and it shows as one.

---

## The debug overlay

`Debug.Enabled` is off by default and toggled from the pause menu, from the entry `LevelSelect` puts
up with the room (`Debug.Show`/`Hide`; `Debug.Init` only reads the saved value). It persists in
`dget`/`dset` slot 0 offset by one — `1` off, `2` on — so a fresh save reading `0` lands on off
rather than on. `Debug.Clear` is what `DELETE SAVE` calls to put it back, and the reason `Debug` tracks
whether its entry is up at all: the label carries the state, so a reset has to rewrite it — but only on
a screen that has the entry, and the save can be deleted from the level select, which does not.

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
| `MENU/GRID` | `LevelSelect` | `COLS` `ROWS` (grid, default 5×4), `CELL` (cell size in pixels, default `(32, 20)`), `TITLE` (caption over the grid, none by default). Authored as the 5×4 of twenty with `SELECT LEVEL` over it. `PAD` is authored but **no longer read**: it sized the mouse hover box, and the cursor is a colour now |
| `ROOMS/<name>` | `Room`, `Levels`, `LevelSelect` | `CELLPOS` (map cells), `BACKPOS` (backdrop cells, absolute — defaults to `(256, 0)`, the start of map layer 2), `PLYRPOS` `BALLPOS` `FLAGPOS` (map-sheet pixels, absolute — inside the room `CELLPOS` cuts out), `HITMAX` (strokes allowed, default 5), `NUMBER` (which level it is, `1`-`63`). **`NUMBER` is what the menu shows and what the save slot is** — the object name is free, and the next number up with a room behind it is the level sinking this one advances to |
| `GAME/WIPE` | `Wipe` | `WAITSEC` (hold on the sunk ball, default `0.5`), `OUTSEC` `INSEC` (close and open, default `0.6` each), `COLOR` (mask palette index, default `17`), `DITHER` (ring sprite, default `117`) — **not authored yet; the defaults run without it**. Read on every close rather than at `Init`, so a Ctrl+S retune lands on the next hole |
| `DAYCYCLE/SUN` | `Sun`, and `Moon` for the sky | `SPR` `TILES` (the body, in 8×8 tiles), `MARGIN` (clearance at the left, right and top, in screen pixels), `DAWNHR` `DUSKHR` (whole hours; outside them, no sun), `GLOWRAD` `GLOWCOL` (the halo discs, read as a pair by index, widest last), `GLOWOPA`. **`TILES` and `MARGIN` are the sky's, not the sun's** — `Sun.Span` is measured off them and the moon crosses the same line |
| `DAYCYCLE/NIGHT` | `Moon` | `SPR`, `MONTHDAY` (days the moon crosses the sky over), `DEEPFROM` `DEEPTO` (deep night, wrapping midnight) `DEEPOPA`, `DUSKFROM` `DAWNTO` (the twilights either side) `TWILOPA`. No size or margin of its own — those are `DAYCYCLE/SUN`'s |
| `PLAYER/STATS` | `Player` | `SPR` `SPRSIZE` `HITPOS` `HITSIZE` `SPEED` `CLIMB` `GRAVITY` `JUMP` `MAXFALL` `CLUBX` `REACH` `FAILTXT` `FAILY` |
| `BALL/STATS` | `Ball` | `SIZE` `GRAVITY` `MAXFALL` `BOUNCE` `FRICTION` `HITX` `HITY` `BLINK` `REST` `HOLEPOS` `HOLESIZE` `HOLESPD` `SINKDEP` `SINKSPD` |
| `SWING/POWER` | `Swing`, `Meter` | `SWEEP` (seconds for one out-and-back), `MISS`, `MINHIT` |
| `SWING/CLUB` | `Swing` | `SPR` (club at rest), `PRESS`, `HITSEC`, `FAILSEC` |
| `CLUBS/ORDER` | `Club` | `LIST` — the club objects in swap order; `SFX` — the swap sounds to pick from |
| `CLUBS/<name>` | `Club` | `NAME`, `ANGLE` (degrees), `DIST`, `GNDPWR` |
| `HUD/METER` | `Meter` | `MARGIN` `BARW` `BARH` `BORDER` |
| `HUD/HITS` | `Hud` | `MARGIN` — the count itself is the room's `HITMAX`, and the corner shows the number alone, so there is nothing else to author |
| `HUD/CLUB` | `Club` | `GAP` `SWAPSEC` `SWAPX` `SWAPY` |
| `ANIM/<name>` | `Anim` | `ID` (sprite ids as Text), `SPEED` (fps), `MODE` — `FW` / `BW` / `RV` / `PP` |
| `ANIM/PLRWALK` | also `Dust`, `Steps` | `PRTMAX` `PRTRATE` `PRTLIFE` `PRTPOS` `PRTVEL` `PRTGRAV` `PRTBIG`; `SFX` `SFXSEC` |

An unknown room, or one missing a field, loads as an empty room at the top-left of the sheet rather
than failing. A room without `FLAGPOS` has no flag — and with no flag to measure from, no cup.
Twenty rooms are authored, `ROOMS/01` to `ROOMS/20`, numbered `1`-`20`, so the grid is full and every
sunk hole advances onto the next. Only `01`-`05` have a cut of the map sheet of their own, laid out
left to right along the top row (`CELLPOS` `(0, 0)`, `(32, 0)`, `(64, 0)`, `(96, 0)`, `(128, 0)`);
`06`-`20` are placeholders, every one of them pointing back at `02`'s `CELLPOS` with its own spawns —
so the numbers exist, and the levels behind them do not yet.

Clips currently authored: `FLAG`, `GOLFPULL`, `GOLFHIT`, `PLRWALK`, `PLRSTAIR`.

Sprite ids fixed in code: none. The sun and the moon were the last two — both are `DAYCYCLE`'s now,
and their old ids (`1` and `129`, 2×2) are the fallbacks the code runs on until the group is authored.

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
  number moves (`Hud.SetLeft`), `Swing.State` returns literals.
- **One home per shared number.** The cell size is `Terrain.TileSize`, the font metrics are `Font`,
  the button indices are `Btn` — a literal `8`, `4` or `5` in game code is a bug waiting to drift.
- **Comments say why, not what.** The density here is deliberate: the tricky invariants (the one-pixel
  step-in on a stair grab, reading the meter before anything else can move it, the ball leaving on the
  clip's last frame) are commented; the obvious is not.

---

## Not done yet

- Sinking the ball advances the level, but there is no **scorecard**: no par, no per-hole result
  screen, and nothing at the end of the run but the level select coming back up.
- Twenty rooms authored but only five drawn: `06`-`20` all carry `02`'s `CELLPOS`, so the grid is
  full and fifteen of its numbers open the same room. They are placeholders waiting for a map.
- The night is drawn in the wrong space for every room but the first — see
  [The moon and the night](#the-moon-and-the-night). `Moon.Draw` is called with the room's camera up
  and measures itself in screen pixels, so levels `2` and above get neither moon nor dim.
- The level select shows *whether* a hole is done, not **what it was done in**: `Save` holds the
  stroke count and nothing draws it, so there is still no scorecard and no par. The strokes themselves
  reset to `HITMAX` at every `Room.Enter`.
- Running out of strokes restarts the room with no notice of any kind: no "out of shots" caption, no
  pause, no sound — the level is simply back at its spawns the frame the ball stops.
- `GAME/WIPE` is not authored. The wipe runs on its code defaults until it is, and `GAME/START` is
  still the dead object the level select replaced.
- `DAYCYCLE/SUN` and `DAYCYCLE/NIGHT` are not authored either. Both run on the code defaults, which
  are exactly the numbers that used to be `const` in the two files, so nothing about the sky has
  changed until the group is pasted in.
