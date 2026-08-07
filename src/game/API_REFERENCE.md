# Mono8 API Reference

Every function on `IMono8API` — what it is, when to use it, its parameters and its limits.
Written from the engine source, so where behaviour differs from intuition it is noted.

Reach the API from `YourGame.API`, or take an `IMono8API` in your class constructor.

**Console at a glance**

| | |
|---|---|
| Screen | 256 × 144 px, palette indices 0–31 |
| Frame rate | fixed 60 FPS (`elapsedSeconds` ≈ 0.0167) |
| Sprite sheet | 256 × 240 px = 32 × 30 tiles of 8 × 8 → sprite ids **0–959** |
| Map | 512 × 576 cells |
| Audio | 64 sfx, 64 music patterns, 4 channels |
| Persistence | 64 int slots |

Two conventions that catch people out:

- **`colorOpaqueness`** is alpha, `0f` (invisible) … `1f` (opaque). Nothing clamps it — stay in range.
- **Angles are turns** (0–1), not radians, and `sin` is negated to match the downward y-axis.

---

## Table of contents

- [System](#system) — `time` `stat` `menuitem` `menurestart`
- [Graphics](#graphics) — `cls` `pixel` `line` `rect` `rectfill` `circ` `circfill` `oval` `ovalfill` `rectinv` `ovalinv` `spr` `sspr` `sprr` `ssprr` `print` `icon` `camera` `pal` `palt`
- [Map](#map) — `mget` `mset` `map`
- [Tile collision](#tile-collision) — `mcol`
- [Sprite flags](#sprite-flags) — `fget` `fset`
- [Input](#input) — `btn` `btnp` `btnr` `mouse*`
- [Audio](#audio) — `sfx` `music`
- [Random](#random) — `rnd` `srand`
- [Math](#math) — `abs` `atan2` `cos` `sin` `sqrt` `min` `max` `mid` `flr` `ceil` `round` `sgn`
- [Persistence](#persistence) — `dget` `dset`
- [JSON data](#json-data) — `gjson` `gjsoncount` `gjsonobj` `sjson` + `Mono8JsonObject`
- [Not available to game code](#not-available-to-game-code)

---

## System

### `double time()`

Seconds elapsed since midnight, from the system wall clock.

**Use it for** a coarse timestamp or a seed. **Do not use it to measure a frame** — use the
`elapsedSeconds` argument of `Update` instead. It is wall time, so it jumps backwards at midnight
and drifts with the OS clock.

| Parameter | Meaning |
|---|---|
| — | |

Constraints: not a monotonic game clock; not paused when the game pauses, nor while the window is
unfocused and your `Update` is not running.

### `int stat(int id)`

One engine statistic by id.

| `id` | Returns |
|---|---|
| `7` | current FPS |
| anything else | `0` |

Use it for an on-screen FPS readout while tuning. Every other id is reserved and reads `0` today.

### `void menuitem(int index, string label, Action callback)`

Adds a custom entry to the pause menu (opened with Enter / gamepad Start), between *Continue* and
*Restart*.

Enter / Start opens the menu and then confirms within it — it does not toggle, so the player leaves
through *Continue* rather than by pressing it again. The selection starts on *Continue*, which is why
a custom entry is never the thing a stray second press lands on.

| Parameter | Meaning | Constraints |
|---|---|---|
| `index` | slot the entry occupies | `0`–`2`; anything else is ignored |
| `label` | text shown | truncated to 16 chars; drawn upper-case |
| `callback` | run when the player confirms with **X** or **Start/Enter** | the menu closes right after it runs |

Use it for "restart level", "toggle sound", "back to title". Register in `Init()` — items survive
across frames but `Init()` runs again on Restart, so re-registering there keeps it consistent.

### `void menuitem(int index)`

Removes the custom entry at `index`. Out-of-range indices are ignored. Use it when an option stops
being valid (e.g. a "continue run" item after the run ends).

### `void menurestart(bool visible)`

Shows or hides the pause menu's built-in *Restart* entry. Visible until you say otherwise.

| Parameter | Meaning | Constraints |
|---|---|---|
| `visible` | whether *Restart* is listed | `false` leaves only *Continue*, the custom entries and *Exit* |

Restart re-runs `Init()`, so hide it on the screen `Init()` itself lands on — a title or a level
select, where the entry does nothing visible. The setting is engine state, not game state: it
survives `Init()` exactly as `menuitem` does, so set it from wherever the screen changes rather than
assuming a restart put it back.

---

## Graphics

Draw order is painter's order: later calls cover earlier ones. Typical `Draw()` is
`cls` → `camera(worldX, worldY)` → background `map` → entities → foreground `map` → `camera()` → HUD.

### `void cls(int colorIndex = 0)`

Fills the whole 256 × 144 screen with one colour. First call of nearly every `Draw()`.

| Parameter | Meaning | Constraints |
|---|---|---|
| `colorIndex` | fill colour | `0`–`31`, use `Constants.Colors.*`; outside that range nothing is drawn |

⚠️ `cls` is drawn through the same camera transform as everything else, so calling it while a
camera offset is active clears a *shifted* rectangle and leaves edges uncleared. Call `cls` before
you set the camera, or reset with `camera()` first.

### `void pixel(int x, int y, int color, float colorOpaqueness = 1f)`

One pixel. For particles, stars, sparks — anything too small to be a sprite.

| Parameter | Meaning | Constraints |
|---|---|---|
| `x`, `y` | position, in screen pixels (camera applies) | off-screen is clipped, not an error |
| `color` | palette index | `0`–`31` |
| `colorOpaqueness` | alpha | `0f`–`1f` |

Cost: one draw call each. Hundreds per frame is fine; tens of thousands is not.

### `void line(int x0, int y0, int x1, int y1, int color)`

Bresenham line from one point to the other, both endpoints included. Order of the points does not
matter. Use it for debug rays, aim lines, wireframes.

| Parameter | Meaning | Constraints |
|---|---|---|
| `x0`,`y0` → `x1`,`y1` | endpoints | any values; drawn pixel by pixel, so a very long off-screen line still costs its full length |
| `color` | palette index | `0`–`31` |

No opacity parameter — lines draw fully opaque.

### `void rect(int x0, int y0, int x1, int y1, int color, float colorOpaqueness = 1f)`
### `void rectfill(int x0, int y0, int x1, int y1, int color, float colorOpaqueness = 1f)`

Rectangle outline / filled rectangle, given two **inclusive** corners. `rect` is a 1 px border.

| Parameter | Meaning | Constraints |
|---|---|---|
| `x0`,`y0`,`x1`,`y1` | opposite corners, both included | any order — they are normalised, so `x1 < x0` still draws |
| `color` | palette index | `0`–`31` |
| `colorOpaqueness` | alpha | `0f`–`1f` |

A single point (`x0 == x1 && y0 == y1`) draws one pixel. Use `rectfill` for HUD panels, health bars,
fades (a full-screen `rectfill` at low opacity); `rect` for selection boxes and debug hitboxes.

### `void circ(int x, int y, int radius, int color, float colorOpaqueness = 1f)`
### `void circfill(int x, int y, int radius, int color, float colorOpaqueness = 1f)`

Circle outline / filled circle centred on (`x`, `y`).

| Parameter | Meaning | Constraints |
|---|---|---|
| `x`,`y` | centre | camera applies |
| `radius` | in pixels | **must be ≥ 0**. `circ` with a negative radius draws nothing; `circfill` with a negative radius throws and freezes the game |
| `color` | palette index | `0`–`31` |
| `colorOpaqueness` | alpha | `0f`–`1f` |

Use for explosion flashes, shadows, radius indicators. `circfill` allocates two temporary arrays per
call — fine occasionally, avoid it in a hot loop of hundreds.

### `void oval(int x0, int y0, int x1, int y1, int color, float colorOpaqueness = 1f)`
### `void ovalfill(int x0, int y0, int x1, int y1, int color, float colorOpaqueness = 1f)`

Ellipse inscribed in the box given by two **inclusive** corners. Use when you need a non-circular
round shape — a drop shadow under a character, a bubble.

| Parameter | Meaning | Constraints |
|---|---|---|
| `x0`,`y0`,`x1`,`y1` | opposite corners of the bounding box | inclusive |
| `color` | palette index | `0`–`31` |
| `colorOpaqueness` | alpha | `0f`–`1f` |

### `void rectinv(int x, int y, int w, int h, int color, int ditherSpriteId = 0, float colorOpaqueness = 1f)`
### `void ovalinv(int x, int y, int w, int h, int color, int ditherSpriteId = 0, float colorOpaqueness = 1f)`

The inverse of `rectfill` / `ovalfill`: they paint the **whole screen** in `color` *except* the shape,
so what is left is a clipping mask with a hole in it. Use for a spotlight following the player, an
iris in/out transition, a "look here" highlight over a dimmed scene.

| Parameter | Meaning | Constraints |
|---|---|---|
| `x`, `y` | top-left of the hole | camera applies to the hole |
| `w`, `h` | size of the hole **in pixels** | `≤ 0` fills the screen with no hole at all |
| `color` | palette index of the mask | `0`–`31` |
| `ditherSpriteId` | 8×8 sprite tiled over the ring just outside the hole | `0` (or an id out of range) skips the ring and leaves a hard edge |
| `colorOpaqueness` | alpha, applied to the fill *and* the ring | `0f`–`1f` |

Unlike every other shape call, the **fill follows the camera** rather than the world — it always
covers the viewport, wherever `camera(x, y)` currently points. The hole and the ring are in world
space like everything else, so the hole tracks whatever it is cut around. There is no need to reset
the camera first.

`ovalinv`'s hole is exactly the oval `ovalfill(x, y, x + w - 1, y + h - 1, …)` would paint, so the
two line up pixel for pixel if you want to fill the hole afterwards.

**The dither ring.** With a `ditherSpriteId`, the mask stops one tile (8 px) short of the hole on
every side and that ring is stippled with the sprite instead, so the mask fades into the hole rather
than cutting. The sprite tiles on a lattice anchored on the hole's centre — tiles meet exactly once,
never overlap and never leave a gap, and a tile hanging over the ring is trimmed to a sub-rect rather
than being skipped, so the pattern keeps its phase all the way round. Author the sprite in the same
`color` as the mask, with colour `0` for the holes in the pattern; `pal` and `palt` apply to it as
they do to `spr`.

Cost scales with the ring, not the screen: a hard-edged full-screen oval is a few hundred fills, one
with a dither ring a few hundred more — the same order as a single `map` call. `rectinv` is far
cheaper than `ovalinv` (four fills, whatever the size).

### `void spr(int spriteId, int x, int y, int width = 1, int height = 1, float scale = 1f, bool flipX = false, bool flipY = false, float colorOpaqueness = 1f)`

Draws a sprite (or a rectangular block of sprites) from the sheet. The main way to draw anything.

| Parameter | Meaning | Constraints |
|---|---|---|
| `spriteId` | top-left tile of the block | `0`–`959`. Sprite `0` is the reserved empty tile and is always blank |
| `x`, `y` | screen position of the block's top-left | camera applies |
| `width`, `height` | size **in 8×8 tiles**, not pixels | the block reads across the sheet's 32-tile rows: `spr(64, x, y, 2, 2)` uses sprites 64, 65, 96, 97 |
| `scale` | uniform scale | clamped to `0.125`–`8` |
| `flipX`, `flipY` | mirror horizontally / vertically | |
| `colorOpaqueness` | alpha | `0f`–`1f` |

`spr` draws one pass **per palette colour**, which is what makes `pal` and `palt` work on it. That
also makes it the slower of the two sprite calls — use `sprr` when you need no palette tricks.

Keep ids in named constants so a re-authored sheet is a one-line change:

```csharp
private const int SprPlayer = 64;   // 2x2
API.spr(SprPlayer, x, y, 2, 2, flipX: facingLeft);
```

### `void sspr(int sx, int sy, int sw, int sh, int dx, int dy, int dw = -1, int dh = -1, bool flipX = false, bool flipY = false, float colorOpaqueness = 1f)`

Draws an arbitrary **pixel** rectangle of the sheet to an arbitrary destination rectangle. Use it
when what you want to draw is not tile-aligned, or when you need a non-uniform stretch (a squashed
sprite, a health bar cut from a strip).

| Parameter | Meaning | Constraints |
|---|---|---|
| `sx`, `sy` | source top-left, in sheet pixels | sheet is 256 × 240 |
| `sw`, `sh` | source size, in pixels | |
| `dx`, `dy` | destination top-left, screen pixels | camera applies |
| `dw`, `dh` | destination size | `-1` (default) means "same as source" — i.e. unscaled |
| `flipX`, `flipY` | mirror | |
| `colorOpaqueness` | alpha | `0f`–`1f` |

Stretching is arbitrary and may be non-uniform; unlike `spr`'s `scale`, nothing is clamped.
Obeys `pal` / `palt`.

### `void sprr(...)` — same parameters as `spr`
### `void ssprr(...)` — same parameters as `sspr`

The single-pass fast paths. Identical geometry and arguments to `spr` / `sspr`, but they draw from
one pre-baked texture instead of one pass per colour.

Differences that matter:

- **`pal` and `palt` are ignored.** Colour `0` is still transparent (baked in), and
  `colorOpaqueness` still works.
- Much cheaper. Use these for tilesets, bullets, particles — anything numerous that needs no
  recolouring. Use `spr` / `sspr` when you need a palette swap (damage flash, team colours) or a
  custom transparent colour.

### `void print(string text, int x, int y, int color = 7, float colorOpaqueness = 1f)`

Draws text with the built-in 5 × 7 font.

| Parameter | Meaning | Constraints |
|---|---|---|
| `text` | string to draw | drawn **in the case you pass it** — the font has both |
| `x`, `y` | top-left of the first character | drawn 1 px above `y`; camera applies |
| `color` | palette index | `0`–`31`, default `7` (White) |
| `colorOpaqueness` | alpha | `0f`–`1f` |

Metrics and behaviour:

- Each character advances **4 px**; a string is `text.Length * 4` px wide.
- `\n` starts a new line, **9 px** below the previous one.
- `\t` advances 4 character widths; `\r` is skipped.
- Characters the font has no glyph for print as `?`. Available: `0-9 A-Z a-z , . : ; [ ] { } | # $ % ( ) ! ? " ' _ + - = * / \ < > space ~`.
- No word wrap.
- Case is preserved. The engine's own text — the pause menu's `menuitem` labels, every editor label —
  is still folded to upper case; `print` is the one path that is not.

Building the string is your cost, not the engine's: `print($"SCORE {score}")` once per frame is
fine, a hundred interpolations in a loop is not.

### `void icon(int n, int x, int y)`

Draws one 8 × 8 icon from the engine's built-in icon sheet (the art the editors use). Always 1 tile,
always scale 1 — there are no size arguments.

| Parameter | Meaning | Constraints |
|---|---|---|
| `n` | icon index | icon sheet is 256 × 24 px = 32 × 3 tiles → `0`–`95` |
| `x`, `y` | screen position | camera applies |

Use it for quick placeholder UI before your own sprites exist. It obeys `pal` and `palt`.

### `void camera(float x = 0, float y = 0)`

Sets the world position the screen is looking at. Every draw call afterwards is offset by
`-x, -y`, so a world object at (`x`, `y`) lands at the top-left of the screen.

| Parameter | Meaning | Constraints |
|---|---|---|
| `x`, `y` | camera position in world pixels | truncated to whole pixels; call `camera()` with no arguments to reset to (0, 0) |

Use `camera(scrollX, scrollY)` before drawing the world, then `camera()` before drawing the HUD so
the HUD stays put. Notes:

- The offset applies to *everything*, including `cls` and `print`.
- It does **not** affect `mousexy()`, which is always in screen pixels — add the camera position
  yourself to get world coordinates.
- Each call ends and restarts the sprite batch, so calling it a few times per frame is normal but
  calling it per entity is wasteful.

### `void pal()`

Resets **both** colour remapping and transparency: every colour draws as itself, and colour `0`
goes back to being the only transparent one.

### `void pal(int c0, int c1)`

Remaps colour `c0` so it draws as colour `c1`.

| Parameter | Meaning | Constraints |
|---|---|---|
| `c0` | colour as authored | `0`–`31`; out-of-range calls are ignored |
| `c1` | colour actually drawn | `0`–`31` |

Use it for damage flashes (`pal(Constants.Colors.Red, Constants.Colors.White)`), team tints, night
palettes. The remap stays until you change it or call `pal()` — always reset when done, or the rest
of the frame inherits it.

Scope: the remap applies to every draw that names a colour index — `cls`, `pixel`, `line`, `rect`,
`circ`, `oval`, `print`, `icon`, `spr`, `sspr`. **`sprr`, `ssprr` and `map` ignore it** (they draw
from one pre-baked texture).

### `void palt()`

Resets transparency to the default: colour `0` transparent, everything else opaque.

### `void palt(int colorIndex)`

Makes `colorIndex` transparent (shorthand for `palt(colorIndex, true)`).

### `void palt(int colorIndex, bool transparent)`

Sets whether `colorIndex` is skipped when drawing.

| Parameter | Meaning | Constraints |
|---|---|---|
| `colorIndex` | palette index | `0`–`31`; out-of-range calls are ignored |
| `transparent` | `true` to skip it, `false` to draw it | |

Use it when a sprite's background colour is not `0`, or to knock out a colour for an effect
(silhouettes: make everything but one colour transparent).

Scope: `palt` only affects the per-colour drawing pass — `spr`, `sspr` and `icon`. Shapes and
`print` name a single colour and are unaffected; `sprr`, `ssprr` and `map` ignore it, though
colour `0` is transparent there regardless.

---

## Map

The map is one 512 × 576 cell sheet. The Map Editor presents it as four 256 × 288 layers:

| Layer | Cell origin |
|---|---|
| 1 | (0, 0) |
| 2 | (256, 0) |
| 3 | (0, 288) |
| 4 | (256, 288) |

Keep the origin of the layer you use in a `const` and add it to every cell coordinate.

### `int mget(int cellX, int cellY)`

The sprite id stored in a map cell. Reading outside the map returns `0`, never throws.

| Parameter | Meaning | Constraints |
|---|---|---|
| `cellX` | cell column | `0`–`511`; out of range → `0` |
| `cellY` | cell row | `0`–`575`; out of range → `0` |

Use it to ask what a tile is — "is this a coin?", "which door did I touch?". Coordinates are
**cells**, not pixels: divide a world pixel position by 8.

### `void mset(int cellX, int cellY, int spriteId)`

Writes a sprite id into a map cell, at runtime, in memory.

| Parameter | Meaning | Constraints |
|---|---|---|
| `cellX`, `cellY` | cell to write | out of range → ignored |
| `spriteId` | sprite to place, `0` clears the cell | `0`–`959`; outside that → ignored |

Use it for destructible terrain, collected pickups, opened doors. The change lasts until the game is
restarted — it never touches the authored `data.map` file, and `Init()` does **not** undo it, so if
a level must reset you have to rewrite the cells yourself.

### `void map(int cellX, int cellY, int screenX, int screenY, int cellWidth = 40, int cellHeight = 23, float scale = 1f, float colorOpaqueness = 1f, int layerMax = 0)`

Draws a rectangular region of the map.

| Parameter | Meaning | Constraints |
|---|---|---|
| `cellX`, `cellY` | top-left **cell** of the region | cells outside the map are skipped |
| `screenX`, `screenY` | where its top-left lands, in screen pixels | camera applies |
| `cellWidth`, `cellHeight` | how many cells to draw | default 40 × 23 ≈ one screen at scale 1 |
| `scale` | whole-region scale | **only `0.5`, `1` and `2`**; any other value snaps to the nearest of the three |
| `colorOpaqueness` | alpha | `0f`–`1f` |
| `layerMax` | sprite-flag **bitmask** filter, not a layer index | `0` draws every tile; otherwise a tile draws only when `fget(tile) & layerMax != 0` |

Cells holding sprite `0` are skipped, so the background shows through. `map` ignores `pal`/`palt`.

Layering pattern — flag 0 on background tiles, flag 1 on foreground tiles:

```csharp
API.map(0, 0, 0, 0, layerMax: 1);   // behind the player  (bit 0)
DrawEntities();
API.map(0, 0, 0, 0, layerMax: 2);   // in front           (bit 1)
```

Note `layerMax` is a mask: flag 0 → `1`, flag 1 → `2`, flag 2 → `4`, and `3` means "flag 0 or 1".

---

## Tile collision

Both overloads take **pixels over the whole map sheet** — cell coordinates × 8. Apply your camera
and layer offsets *before* calling.

### `bool mcol(int x, int y, int flag = 0)`

Whether the tile under a point carries a sprite flag.

| Parameter | Meaning | Constraints |
|---|---|---|
| `x`, `y` | point, in map-sheet pixels | negative → `false` (off the map) |
| `flag` | bit index to test | `0`–`7`; outside that → `false` |

The meaning of each flag is entirely your game's: one for solid, another for ice, another for
hazards — ask the developer which is which. **This project uses flag `1` for solid ground.** Cells
holding sprite `0` never collide, however that sprite is flagged.

### `bool mcol(int x, int y, int w, int h, int flag = 0)`

Whether **any** tile under a rectangle carries the flag. This is the one you want for an entity.

| Parameter | Meaning | Constraints |
|---|---|---|
| `x`, `y` | rectangle top-left | |
| `w`, `h` | size; the rect runs to (`x + w - 1`, `y + h - 1`) | either side ≤ 0 → `false` |
| `flag` | bit index | `0`–`7` |

The rectangle is clipped to the map, so an oversized one costs no more than covering the map. Cost
grows with the cells covered — a 16 × 16 hitbox touches at most 9 cells, which is nothing.

Standard axis-separated movement:

```csharp
const int FlagSolid = 1;
x += vx * elapsedSeconds;
if (API.mcol((int)x, (int)y, w, h, FlagSolid)) { /* undo x, zero vx */ }
y += vy * elapsedSeconds;
if (API.mcol((int)x, (int)y, w, h, FlagSolid)) { /* undo y, zero vy */ }
```

Terrain painted with an **autotile** brush is read the same way, and there is no separate call for
it: the brush swaps a cell between the forty-seven pieces of its 8 × 6 block as the neighbours
change, so the developer sets one flag across the whole block and `mcol` answers wherever the brush
went. Ask which flag a terrain uses rather than assuming.

---

## Sprite flags

8 bits per sprite (0–7), authored in the Sprite Editor and mirrored to `data.gff`. What each bit
means is your game's business.

### `int fget(int spriteId)`

All eight flags of a sprite as one byte-wide bitmask. Out-of-range ids read `0`.

Use it to test several flags at once: `(API.fget(t) & 0b0000_0011) != 0`.

### `bool fget(int spriteId, int flag)`

Whether one flag bit is set.

| Parameter | Meaning | Constraints |
|---|---|---|
| `spriteId` | sprite | `0`–`959`; out of range → `false` |
| `flag` | bit index | `0`–`7` |

The everyday form: `if (API.fget(tile, FlagSolid))`.

### `void fset(int spriteId, int flag, bool value)`

Sets or clears one flag bit at runtime.

| Parameter | Meaning | Constraints |
|---|---|---|
| `spriteId` | sprite | out of range → ignored |
| `flag` | bit index `0`–`7` | |
| `value` | `true` sets, `false` clears | |

### `void fset(int spriteId, int value)`

Replaces all eight flags with `value` (only the low byte is kept).

Both `fset` forms change the **live sprite sheet in memory** — every tile using that sprite is
affected at once, and the change survives `Init()`, so a restart does not put it back. Prefer
authoring flags in the editor; use `fset` only for deliberate global switches (e.g. making an ice
tileset solid during one boss phase) and set them back yourself.

---

## Input

Buttons are indices. Player 0 uses `0`–`7`, player 1 uses `8`–`15`; `btn(b, p)` is exactly
`btn(p * 8 + b)`.

| 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| Left | Right | Up | Down | A (Z) | B (X) | X (C) | Y (V) |

Player 1 keyboard: arrows + Z X C V. Player 2 keyboard: W A S D + G H J K. Gamepads: D-pad and
left stick (deadzone 0.5) for directions, face buttons A B X Y for 4–7. Keyboard and gamepad are
OR-ed together — either source presses the button.

### `bool btn(int button)` / `bool btn(int button, int player)`

Whether the button is **held right now**. Use for movement, aiming, anything continuous.

| Parameter | Meaning | Constraints |
|---|---|---|
| `button` | index | one-arg form: `0`–`15`; two-arg form: `0`–`7`. Out of range → `false` |
| `player` | `0` or `1` | |

### `bool btnp(int button)` / `bool btnp(int button, int player)`

Whether the button **went down this frame**. No auto-repeat — one `true` per press.

Use for jumps, menu navigation, confirming — anything that must fire once per press.

### `bool btnr(int button)`

Whether the button **came up this frame**. Takes the raw index `0`–`15` only (no player overload).

Use for charge-and-release mechanics, or variable-height jumps (cut the jump short on release).

### `bool mousel()` / `bool mouselp()` / `bool mouselr()`

Left mouse button: held / pressed this frame / released this frame.

### `bool mouser()` / `bool mouserp()` / `bool mouserr()`

Right mouse button: held / pressed this frame / released this frame.

### `bool mouseup()` / `bool mousedown()`

Whether the wheel scrolled up / down since last frame. One `true` per notch of movement; there is no
magnitude.

### `(int x, int y) mousexy()`

Mouse position in **virtual screen pixels** — (0, 0) to (255, 143) inside the game view.

Constraints: not affected by `camera()`. To get world coordinates, add your camera position
yourself. Values can fall outside the screen when the pointer leaves the game area, so clamp or
range-check before using them as indices.

```csharp
var (mx, my) = API.mousexy();
int worldX = mx + camX;
```

### While the window is unfocused

Clicking away from the window dims the screen 30%, holds the last frame and stops calling your
`Update` entirely. Input still samples underneath, so the press and release edges are current the
moment focus returns rather than a frame stale.

The click that raises the window back is **swallowed**: both mouse buttons stay suppressed until they
are seen released on two consecutive frames, so `mousel`, `mouselp` and `mouselr` (and their right
counterparts) all read `false` for it — the player's *next* click is the first one your game sees.
Position and wheel are never suppressed. Fullscreen counts as focused and never dims.

Because `Update` does not run across that gap while `time()` keeps advancing, never derive elapsed
game time by differencing `time()` — accumulate `elapsedSeconds` instead.

---

## Audio

4 channels, numbered `0`–`3`. 64 sfx slots and 64 music patterns, authored in the Sfx and Music
editors. **Ask the developer for indices — never invent one.**

### `void sfx(int sfxId, int channel = -1, int offset = 0, int length = -1)`

Plays a sound effect, or stops sounds.

| Parameter | Meaning | Constraints |
|---|---|---|
| `sfxId` | sfx to play | `0`–`63`. `-1` stops **all** channels. `-2` stops the channel given in `channel`. An unauthored id plays nothing (no error) |
| `channel` | channel to play on | `0`–`3`. `-1` (default) stops any channel already playing this sfx, then takes the first free one — so the sound restarts instead of layering |
| `offset` | first note to play, within the 32-note sfx | clamped into range |
| `length` | how many notes to play | `-1` (default) plays to the end; otherwise clamped to what remains |

If no channel is free, the engine steals the one furthest through its sound.

Use `offset`/`length` to pack several short sounds into one sfx slot, or to play a footstep variant
from the middle of a pattern.

```csharp
private const int SfxJump = 3;
if (API.btnp(4)) API.sfx(SfxJump);
API.sfx(-2, 2);   // silence channel 2
API.sfx(-1);      // silence everything
```

### `void music(int musicId, int fadeLength = 0, int channelMask = 0)`

Starts music at a pattern and plays forward through the sequence, honouring the loop-start /
loop-end / stop marks authored in the Music editor.

| Parameter | Meaning | Constraints |
|---|---|---|
| `musicId` | pattern to start from | `0`–`63`. **Any negative value stops the music** |
| `fadeLength` | intended fade-in | **currently ignored by the engine** |
| `channelMask` | intended channel reservation | **currently ignored by the engine** |

Music takes over the channels its patterns use, so a busy track leaves fewer channels for `sfx`.
Call it once when a scene starts — calling it every frame restarts the track every frame.

---

## Random

### `float rnd(float max = 1f)` / `double rnd(double max)` / `int rnd(int max)`

A random number in `[0, max)` — `max` itself never comes back.

| Overload | Returns | Notes |
|---|---|---|
| `rnd()` / `rnd(float)` | `float` | default `max` is `1f` |
| `rnd(double)` | `double` | |
| `rnd(int)` | `int` | whole numbers `0`…`max-1`; `max <= 0` returns `0` |

The overload is chosen by the argument's type: `rnd(10)` gives an int, `rnd(10f)` a float. A
negative `max` on the float/double forms yields values in `(max, 0]`.

```csharp
int lane = API.rnd(4);                  // 0,1,2,3
float angle = API.rnd(1f);              // a full turn
bool coin = API.rnd(1f) < 0.5f;
```

### `void srand(int seed)`

Reseeds the generator, so the same seed produces the same sequence. Use it for reproducible level
generation or a daily-challenge seed. It replaces the generator globally — reseeding mid-game
resets *all* randomness, not just yours.

---

## Math

All take and return `double` unless noted — cast to `float`/`int` where you need one. These exist so
game code does not have to reach for `System.Math`; use them for consistency (and because `sin`,
`cos` and `atan2` here use the engine's angle convention).

**Angles are turns, `0`–`1`, not radians. `sin` is negated** so that positive angles rotate the way
the downward y-axis expects — PICO-8 convention.

| Function | Returns | Use it for |
|---|---|---|
| `abs(v)` | magnitude of `v` | distance without direction |
| `atan2(dy, dx)` | angle **in turns** from the origin to (`dx`, `dy`) | facing a target; feed straight back into `cos`/`sin` |
| `cos(a)` | cosine, `a` in turns (`0.25` = quarter turn) | x component of a direction |
| `sin(a)` | **negated** sine, `a` in turns | y component of a direction, already screen-oriented |
| `sqrt(v)` | square root | distances; negative input gives `NaN` |
| `min(a, b)` | smaller | clamping a floor |
| `max(a, b)` | larger | clamping a ceiling |
| `mid(a, b, c)` | the middle of three | one-call clamp: `mid(lo, value, hi)` |
| `flr(v)` | round down | pixel-snapping positions (`-1.2` → `-2`) |
| `ceil(v)` | round up | |
| `round(v)` | nearest, halves away from zero | `2.5` → `3`, `-2.5` → `-3` |
| `sgn(v)` | `int`: `-1`, `0` or `1` | direction of movement; note `0` returns `0`, not `1` |

```csharp
double a = API.atan2(targetY - y, targetX - x);
x += (float)API.cos(a) * speed * elapsedSeconds;
y += (float)API.sin(a) * speed * elapsedSeconds;   // already correct for screen y
```

---

## Persistence

64 integer slots that survive quitting the game, stored in `data.save`.

### `int dget(int index)`

Reads slot `index`. Out-of-range indices and never-written slots read `0`.

| Parameter | Meaning | Constraints |
|---|---|---|
| `index` | slot | `0`–`63`; outside → `0` |

Because a fresh save reads all zeros, design your encoding so `0` means "nothing yet" (store
`level + 1`, not `level`).

### `void dset(int index, int value)`

Writes slot `index` and **writes the whole save file to disk immediately**.

| Parameter | Meaning | Constraints |
|---|---|---|
| `index` | slot | `0`–`63`; outside → ignored |
| `value` | any `int` | ints only — pack floats yourself (e.g. store `x * 100`) |

Use it for high scores, unlocked levels, settings. Do **not** call it every frame: each call is a
file write. Save at checkpoints, on death, on pause.

---

## JSON data

Tuning values — speeds, spawn points, hitboxes, animation timings — belong in `data.json` rather
than in code, so they can be retuned in the editor without a rebuild.

The file is three fixed levels — **group → object → field** — with the field's type as a one-char
suffix on its key. The suffix is *not* part of the name:

```json
{ "ENEMY": { "SLIME": { "HP:i": 12, "SPD:d": 1.25, "SPAWN:p": [40, 88] } } }
```

| Suffix | Type | JSON form | Getter | C# type |
|---|---|---|---|---|
| `:t` | Text | `"GREEN SLIME"` | `GetStr` | `string` |
| `:i` | Int | `12` | `GetInt` | `int` |
| `:d` | Decimal | `1.25` | `GetDec` | `double` |
| `:m` | Money | `"3.50"` | `GetMoney` | `decimal` |
| `:p` | PosXY | `[40, 88]` | `GetXY` | `(int x, int y)` |
| `:b` | Bool | `true` | `GetBool` | `bool` |

Authoring limits: ≤ 16 groups, ≤ 64 objects per group, ≤ 16 fields per object, ≤ 16 items per array;
names ≤ 8 chars and upper-case. You cannot add fields from code — ask for them to be authored.

### `Mono8JsonObject gjson(string group, string obj)`

Looks up one authored object. **Returns `null`** when either name is unknown — always null-check.

| Parameter | Meaning | Constraints |
|---|---|---|
| `group` | group name | matched case-insensitively |
| `obj` | object name | matched case-insensitively |

Two dictionary hits, allocates nothing — calling it from `Update`/`Draw` is fine when you want the
live value.

**Fetch it in `Init()`.** Ctrl+S in the JSON editor rebuilds the runtime data and makes *new*
objects, so a `Mono8JsonObject` cached before the save is orphaned rather than updated. Holding one
in a field is fine as long as `Init()` fetches it again.

```csharp
private const string JsonGroup = "PLAYER";
private const string JsonObject = "STATS";
private const float DefaultSpeed = 60f;

var stats = API.gjson(JsonGroup, JsonObject);
if (stats != null)
{
    Speed = (float)stats.GetDec("SPEED", 0, DefaultSpeed);
    if (stats.Has("SPAWN")) (SpawnX, SpawnY) = stats.GetXY("SPAWN");
}
```

#### Reading from the object it returns

Nothing here throws. A missing field, a wrong-typed getter or an index past the end returns the
fallback — a `GetInt` on a `:d` field gives you the fallback, not a truncated number.

| Member | Returns |
|---|---|
| `GetInt(field, i = 0, fallback = 0)` | `int` |
| `GetDec(field, i = 0, fallback = 0)` | `double` |
| `GetMoney(field, i = 0, fallback = 0)` | `decimal` |
| `GetBool(field, i = 0, fallback = false)` | `bool` |
| `GetStr(field, i = 0, fallback = "")` | `string` |
| `GetXY(field, i = 0)` | `(int x, int y)` — **no fallback parameter**; missing reads `(0, 0)`, which is a legal coordinate, so gate on `Has(field)` when `(0, 0)` would be wrong |
| `Has(field)` | is the field declared |
| `TypeOf(field)` | `DataValueType`; an absent field reads as `Text`, so pair with `Has` |
| `IsArray(field)` | authored as a list rather than a scalar |
| `Count(field)` | items: `1` for a scalar, `0` when absent |
| `IntArray(field)` / `DecArray(field)` / `BoolArray(field)` | `ReadOnlySpan<T>` straight onto storage — no copy, no allocation; empty when missing or wrong-typed |

Any field can hold an array instead of a scalar. Watch `:p`: `[40, 88]` is **one** position,
`[[40, 88], [72, 16]]` is two.

```csharp
int n = data.Count("SPAWN");
for (int i = 0; i < n; i++) { var (sx, sy) = data.GetXY("SPAWN", i); }

foreach (int hp in data.IntArray("HP")) { }
```

### `int gjsoncount(string group)` / `string gjsonobj(string group, int index)`

Walks a group whose object names the game does not know in advance — a set of levels keyed on a
field rather than on the object name, say, where renaming the object must not renumber the game.

| Call | Returns |
|---|---|
| `gjsoncount(group)` | how many objects the group holds; `0` for an unknown group |
| `gjsonobj(group, index)` | the name of the object at `index`, in the order `data.json` authors them; `null` for an unknown group or an index past the end |

Feed the name back to `gjson` to read the object. Neither call allocates, but the walk is `Init()`
work: build your own index once and keep it, rather than scanning a group per frame.

```csharp
int objects = API.gjsoncount("ROOMS");
for (int i = 0; i < objects; i++)
{
    string name = API.gjsonobj("ROOMS", i);
    var room = API.gjson("ROOMS", name);
    if (room == null) continue;

    int number = room.GetInt("NUMBER", 0, 0);
    if (number >= 1 && number <= 63) Rooms[number] = name;
}
```

### `bool sjson(string group, string obj, string field, <value>, int index = 0)`

Writes one value into an existing field. Six overloads, picked by the value's C# type:

| Value type passed | Field type written |
|---|---|
| `int` (`20`) | Int |
| `double` (`1.5`) | Decimal |
| `decimal` (`3.50m`) | Money |
| `bool` (`true`) | Bool |
| `string` (`"HI"`) | Text |
| `(int, int)` (`(40, 88)`) | PosXY — one tuple argument, so it cannot be mistaken for the int overload's `value, index` |

| Parameter | Meaning | Constraints |
|---|---|---|
| `group`, `obj`, `field` | what to write to | case-insensitive; unknown → returns `false`, changes nothing |
| `<value>` | the new value | must match the field's declared type, or it returns `false` |
| `index` | item within an array field | past the end → `false` |

Returns `false` and changes nothing on any mismatch. **Never throws, never creates a field, and
never writes `data.json` back to disk** — the write lands in the running snapshot and is gone on
restart. It is for live tweaks; use `dset` for anything that must survive.

---

## Not available to game code

`IEditorAPI` — `SetPixel`, `SetPixelDithered`, `SetRectFill`, `SetRect`, `SetOval`, `SetOvalFill`,
`SetPaintBucket` — mutates the sprite sheet and exists only for the built-in editors. Game code
receives `IMono8API` and cannot paint over the sheet it is drawing from.

Also off-limits from `src/game/`: MonoGame types, engine internals (`SpriteSheet`, `MapSheet`,
`SfxEngine`, `Screen`, `ColorPalette`, …), `System.IO`, `Console`, threads, timers, `DateTime`,
`System.Linq`, and anything that allocates per frame in `Update`/`Draw`. `Constants.Colors.*` and
`Constants.Screen.*` are fine — they are constants, not engine state.
