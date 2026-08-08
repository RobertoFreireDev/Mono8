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

The level select takes no mouse: ← → ↑ ↓ walk the cursor around the grid and Z picks the level it is
on.

Pause menu (Enter opens it, and confirms once it is open — it does not toggle, so the way out is
*Continue*) carries the `DEBUG: ON/OFF` toggle, persisted in `dget`/`dset` slot 0, and `LEVELS`,
which goes back to the level select. Restart also lands there, since it re-runs `Init`.

All three are room entries. On the level select the pause menu is down to *Continue* and *Exit*:
there is no room to debug or go back to, and Restart would only re-run `Init` onto the screen already
showing. `LevelSelect.Show`/`Close` put the three up and take them down together — `menuitem` for the
two custom ones, `menurestart(false)` for the engine's built-in Restart.

---

## Frame flow

`YourGame` is a forward to either the level select or the current `Room`; the room runs its
occupants. `LevelSelect.Active` is the switch, and only one of the two runs in a frame.

```
Init()   Debug.Init()
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
         map(CELLPOS)       the room itself
         Flag.Draw
         Ball.DrawHoleDebug over the flag it is measured from
         Player.Draw        Dust under the body, body, club sprite over it, miss text
         Ball.Draw
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
| [YourGame.cs](YourGame.cs) | Engine entry point. Forwards the three methods to the level select or to `_room`, owns the `Wipe` that carries one room into the next, and is the one place a room is entered. |
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
| [Hud.cs](Hud.cs) | The strokes left, counted down from the room's `HITMAX`. `OutOfShots` is what loses the level, `Taken` is what a sunk hole is recorded as. Static. |
| [Save.cs](Save.cs) | The levels finished, one `dget`/`dset` slot each — the strokes a hole was sunk in, or `-1` for one never finished. Read once at `Init`, written only by a hole dropping in. Static. |
| [Dust.cs](Dust.cs) | Foot dust particle pool, fixed size, allocated once. Static. |
| [Steps.cs](Steps.cs) | Footstep sfx on a wall-clock interval while walking. Static. |
| [Anim.cs](Anim.cs) | Reusable sprite flipbook from an `ANIM/<name>` object. Instance. |
| [SfxList.cs](SfxList.cs) | A sfx array field played one at a time at random — the footsteps and the club swap. Instance. |
| [Motion.cs](Motion.cs) | The pixel-stepped travel and the gravity clamp the player and the ball both move by. Stateless. |
| [Btn.cs](Btn.cs) | Button indices by name, so no `btn` call carries a bare number. |
| [Font.cs](Font.cs) | The engine font's advance, line height and ink middle, the string width captions are placed by, and `PrintOutlined` — the one call every caption in the game is drawn with. |
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

Every number sits on a **dark green disc**, radius 8, centred on the caption's own ink (`Font.Middle`
and half the string width, not the cell) so one digit and two are both centred in it. The disc is
what makes a number readable now that a whole level is drawn behind the grid. It does **not** take the
cursor's one-pixel drop — the number shifting inside its own disc is the cue.

The cursor is a colour **within** that pair rather than one over the top of it, so a number never has
to choose which of the two things it is saying:

| | Not sunk | Sunk |
|---|---|---|
| Cursor elsewhere | White | Yellow |
| **Under the cursor** | **Green** | **Orange** |

It is also **drawn one pixel lower** than the rest, which is what actually finds it: breaking the line
the row sits on reads at a glance, where a colour shift is a difference you have to look for — and
with four colours in play, colour alone could no longer carry it.

← → ↑ ↓ walk the cursor one cell per press, no key repeat. It **clamps** rather than wraps: at the edge
of the grid the press does nothing. Numbers with no room behind them are stepped over — the cursor
keeps going in the direction pressed until it finds an authored one, and stays put if it does not,
since a cursor on an undrawn number would be a cursor that vanished. It opens on the lowest authored
level and is left where it was when the pause menu comes back here, so returning from a level lands on
that level.

`Z` (`Btn.Jump`) picks. `btnp`, not `btn`, and the player reads jump with `btnp` too — so the press that
picked cannot go on to be the jump the room's first frame sees.

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

Moving the cursor **crosses one picture over the other in 1 second** (`SlideSeconds`), in the
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
the `DEBUG` toggle and the built-in *Restart* are registered only while a room is running, and the
menu clears all three while it is up.

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

`Holed` stays set until the room is re-entered, and it is what starts the wipe onto the next level.

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
top-right through `Font.PrintOutlined`, and counts it down — `Swing.Launch` calls `CountHit` only for a
stroke that actually sent the ball, so a whiff is free. The caption is rebuilt only when the number
moves, and the count is floored at zero.

The last strokes are called out in colour, since a number in the corner is easy to miss while lining up
a shot: **two left is yellow, one is red**, and zero stays red — the frames before the room restarts
are the most urgent the count ever is, not the least. Anything above two is white.

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
rather than on.

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
| `MENU/GRID` | `LevelSelect` | `COLS` `ROWS` (grid, default 5×4), `CELL` (cell size in pixels, default `(32, 20)`), `TITLE` (caption over the grid, none by default). Authored as the 5×4 of twenty with `SELECT LEVEL` over it. `PAD` is authored but **no longer read**: it sized the mouse hover box, and the cursor is a one-pixel drop now |
| `ROOMS/<name>` | `Room`, `Levels`, `LevelSelect` | `CELLPOS` (map cells), `BACKPOS` (backdrop cells, absolute — defaults to `(256, 0)`, the start of map layer 2), `PLYRPOS` `BALLPOS` `FLAGPOS` (map-sheet pixels, absolute — inside the room `CELLPOS` cuts out), `HITMAX` (strokes allowed, default 5), `NUMBER` (which level it is, `1`-`63`). **`NUMBER` is what the menu shows and what the save slot is** — the object name is free, and the next number up with a room behind it is the level sinking this one advances to |
| `GAME/WIPE` | `Wipe` | `WAITSEC` (hold on the sunk ball, default `0.5`), `OUTSEC` `INSEC` (close and open, default `0.6` each), `COLOR` (mask palette index, default `17`), `DITHER` (ring sprite, default `117`) — **not authored yet; the defaults run without it**. Read on every close rather than at `Init`, so a Ctrl+S retune lands on the next hole |
| `PLAYER/STATS` | `Player` | `SPR` `SPRSIZE` `HITPOS` `HITSIZE` `SPEED` `CLIMB` `GRAVITY` `JUMP` `MAXFALL` `CLUBX` `REACH` `FAILTXT` `FAILY` |
| `BALL/STATS` | `Ball` | `SIZE` `GRAVITY` `MAXFALL` `BOUNCE` `FRICTION` `HITX` `HITY` `BLINK` `REST` `HOLEPOS` `HOLESIZE` `HOLESPD` `SINKDEP` `SINKSPD` |
| `SWING/POWER` | `Swing`, `Meter` | `SWEEP` (seconds for one out-and-back), `MISS`, `MINHIT` |
| `SWING/CLUB` | `Swing` | `SPR` (club at rest), `PRESS`, `HITSEC`, `FAILSEC` |
| `CLUBS/ORDER` | `Club` | `LIST` — the club objects in swap order; `SFX` — the swap sounds to pick from |
| `CLUBS/<name>` | `Club` | `NAME`, `ANGLE` (degrees), `DIST`, `GNDPWR` |
| `HUD/METER` | `Meter` | `MARGIN` `BARW` `BARH` `BORDER` |
| `HUD/HITS` | `Hud` | `LABEL` `MARGIN` — the count itself is the room's `HITMAX` |
| `HUD/CLUB` | `Club` | `GAP` `SWAPSEC` `SWAPX` `SWAPY` |
| `ANIM/<name>` | `Anim` | `ID` (sprite ids as Text), `SPEED` (fps), `MODE` — `FW` / `BW` / `RV` / `PP` |
| `ANIM/PLRWALK` | also `Dust`, `Steps` | `PRTMAX` `PRTRATE` `PRTLIFE` `PRTPOS` `PRTVEL` `PRTGRAV` `PRTBIG`; `SFX` `SFXSEC` |

An unknown room, or one missing a field, loads as an empty room at the top-left of the sheet rather
than failing. A room without `FLAGPOS` has no flag — and with no flag to measure from, no cup.
`ROOMS/A`, `ROOMS/B` and `ROOMS/C` are authored, numbered `1`-`3`, so the menu shows three numbers out
of twenty and sinking the first hole advances onto the second.

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
- Three rooms authored. The level select can open any of the twenty, but seventeen of the numbers have
  no `ROOMS` object claiming them and so are not drawn.
- The level select shows *whether* a hole is done, not **what it was done in**: `Save` holds the
  stroke count and nothing draws it, so there is still no scorecard and no par. The strokes themselves
  reset to `HITMAX` at every `Room.Enter`.
- Running out of strokes restarts the room with no notice of any kind: no "out of shots" caption, no
  pause, no sound — the level is simply back at its spawns the frame the ball stops.
- `GAME/WIPE` is not authored. The wipe runs on its code defaults until it is, and `GAME/START` is
  still the dead object the level select replaced.
