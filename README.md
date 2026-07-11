# Mono8

A PICO-8 style fantasy console built on MonoGame (.NET 8), with built-in sprite, map, SFX and music editors. The screen is 256×144 pixels with a 32-color palette.

## Specs

| | |
|---|---|
| Screen | 256×144 pixels, 32 colors |
| Sprite sheet | 256×240 pixels — 32×30 tiles of 8×8, so sprite ids `0`-`959` |
| Map | 512×576 cells |
| Sound | 64 SFX, 64 music patterns, 4 channels |
| Persistence | 64 integer slots |

Color indices `0`-`31` have names in `Constants.Colors` (`Constants.Colors.DarkBlue` is `1`, and so on). Sprite `0` is the empty sprite: `map` never draws it, and color `0` is transparent by default.

## Building

The project file lives in [src/](src/), so run from the repository root:

```
dotnet build src/mono8.csproj
dotnet publish src/mono8.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Editors

On launch a short splash screen plays, then the Sprite editor opens. The icon buttons at the **top-right of the menu bar** switch between the four editors: **Sprite**, **Map**, **Sfx** and **Music**. The button at the top-left is context-sensitive — it toggles the full-screen map view in the Map editor and the alternate (tracker) view in the SFX editor.

### Global Keys

| Key | Description |
|---|---|
| `F2` | Toggles fullscreen. |
| `Alt+F4` | Quits the application. |

## Project Data

Everything you author in the editors lives in the `data/` folder next to the executable, as plain text you can diff and commit. `Ctrl+S` in any editor writes all of them at once.

| File | Contents |
|---|---|
| `data.gfx` | Sprite sheet pixels. |
| `data.gff` | Per-sprite flag bits. |
| `data.atl` | Which 4×4 sprite blocks are autotiles — 7 lines of 8 `0`/`1` digits. |
| `data.map` | Map cells, as two hex digits per cell. |
| `data.sfx` | The 64 sound effects. |
| `data.music` | The 64 music patterns. |
| `data.icons` | The editors' icon sheet. |
| `data.save` | The 64 `dget`/`dset` slots, rewritten on every `dset`. |

## Running Your Game

Write your game's logic in [src/game/YourGame.cs](src/game/YourGame.cs) (`Init`, `Update`, `Draw`). It ships with a small demo that draws the map at each supported scale; delete the body of `Draw` to start from scratch.

| Key | Description |
|---|---|
| `Ctrl+R` | Runs your game, calling `Init()` and switching out of the editor. |
| `Esc` | Stops the game and returns to whichever editor was active before. |

An exception thrown from your `Init`, `Update` or `Draw` does not crash the process. Audio stops and the message is drawn over a blank screen, where it stays until you restart the application.

### Start (Pause) Menu

While your game is running, pressing `Enter` (keyboard) or `Start` (gamepad) opens a pause menu with **Continue**, **Restart** and **Exit**, plus up to three custom entries set via `menuitem`.

| Key | Description |
|---|---|
| `Enter` / gamepad `Start` | Toggles the pause menu. |
| `Up`/`Down` | Moves the menu selection. |
| `B`/`X` (button 5) | Confirms the selected entry. |

Entries are laid out in this order: **Continue**, then any custom entries, then **Restart** and **Exit**.

- **Continue** resumes the game.
- **Restart** reinitializes the active editor via `Init()`.
- **Exit** quits the application.
- Custom entries added with `menuitem(index, label, callback)` run their callback and close the menu when selected. `index` is `0`-`2`, and labels longer than 16 characters are truncated.

## API Reference

PICO-8 style API. All coordinates are pixel-based unless otherwise noted.

### System

| Function | Parameters | Description |
|---|---|---|
| `time` | — | Returns the wall-clock time of day in seconds (seconds since midnight). |
| `stat` | `id` | Returns a system statistic. Only `id` `7` is implemented (current FPS); any other `id` returns `0`. |
| `menuitem` | `index, label, callback` | Adds/updates a custom menu item (`index` `0`-`2`; `label` truncated to 16 chars). |
| `menuitem` | `index` | Removes the custom menu item at `index`. |

### Graphics

| Function | Parameters | Description |
|---|---|---|
| `cls` | `colorIndex = 0` | Clears the screen with the given color. |
| `pixel` | `x, y, color, colorOpaqueness = 1f` | Sets a single pixel's color. |
| `line` | `x0, y0, x1, y1, color` | Draws a line between two points. |
| `rect` | `x0, y0, x1, y1, color, colorOpaqueness = 1f` | Draws a rectangle outline. |
| `rectfill` | `x0, y0, x1, y1, color, colorOpaqueness = 1f` | Draws a filled rectangle. |
| `circ` | `x, y, radius, color, colorOpaqueness = 1f` | Draws a circle outline. |
| `circfill` | `x, y, radius, color, colorOpaqueness = 1f` | Draws a filled circle. |
| `oval` | `x0, y0, x1, y1, color, colorOpaqueness = 1f` | Draws an oval outline within the given bounds. |
| `ovalfill` | `x0, y0, x1, y1, color, colorOpaqueness = 1f` | Draws a filled oval within the given bounds. |
| `spr` | `spriteId, x, y, width = 1, height = 1, scale = 1f, flipX = false, flipY = false, colorOpaqueness = 1f` | Draws sprite `spriteId` with its top-left corner at `x, y`. `width`/`height` are measured in 8×8 tiles, so `spr(0, 0, 0, 2, 2)` draws a 16×16 block starting at sprite `0`. `scale` is a free float (`0.5f` shrinks, `4f` enlarges), clamped to `0.125`-`8`. |
| `sspr` | `sx, sy, sw, sh, dx, dy, dw = -1, dh = -1, flipX = false, flipY = false, colorOpaqueness = 1f` | Draws the `sw`×`sh` pixel region of the sprite sheet at `sx, sy` into the `dw`×`dh` rectangle at `dx, dy` on screen, stretching it to fit. `dw`/`dh` default to `-1`, meaning "use `sw`/`sh`" — i.e. draw at 1:1 with no scaling. Unlike `spr`, the destination size is arbitrary and is not clamped, so `sspr` can stretch non-uniformly (a different factor horizontally and vertically). |
| `sprr` | same as `spr` | Fast `spr`. Draws in a single pass, so it ignores `pal` and `palt` (see below). |
| `ssprr` | same as `sspr` | Fast `sspr`. Draws in a single pass, so it ignores `pal` and `palt` (see below). |
| `print` | `text, x, y, color = 7, colorOpaqueness = 1f` | Prints text at the given position with the given color. `colorOpaqueness` (`0f`-`1f`) fades the text, useful for blend-in/out effects. |
| `icon` | `n, x, y` | Draws icon `n` at the given position. |
| `camera` | `x = 0, y = 0` | Sets the camera offset applied to subsequent draw calls. |
| `pal` | — | Resets the palette to its default state. |
| `pal` | `c0, c1` | Remaps color `c0` to `c1` for subsequent draw calls. |
| `palt` | — | Resets transparency settings to default. |
| `palt` | `colorIndex` | Toggles transparency for a color index. |
| `palt` | `colorIndex, transparent` | Sets whether a color index is treated as transparent. |

Both `spr` and `sspr` draw one pass per palette color, so they respect the current `pal` color remapping and `palt` transparency (by default color `0` is transparent). Sprite pixels whose color is transparent are skipped entirely, letting whatever was drawn earlier show through.

**`pal` and `palt` do not apply to `sprr`, `ssprr` or `map`.** These three draw from a single pre-baked texture of the sprite sheet in one pass, rather than compositing one pass per color — which is why they are faster, and why the per-color palette state never gets a chance to apply. So a `pal(c0, c1)` remap in effect at draw time is ignored, and `palt` cannot make a color transparent (or make color `0` opaque). The two things that still work are color `0`, which is always transparent because it is baked that way, and `colorOpaqueness`, which tints the whole sprite or tile.

`sprr` and `ssprr` take exactly the same parameters as `spr` and `sspr` and draw the same pixels at the same place; the only difference is the single-pass path. Reach for them when you are drawing many sprites that need no palette tricks — a tilemap-like backdrop, a particle swarm — and keep `spr`/`sspr` for anything you want to recolor or key out with `pal`/`palt`. Likewise, to draw a map tile with palette swaps or custom transparency, read it with `mget` and draw it yourself with `spr`.

### Map

| Function | Parameters | Description |
|---|---|---|
| `mget` | `cellX, cellY` | Gets the sprite id at a map cell. Out-of-range cells read as `0`. |
| `mset` | `cellX, cellY, spriteId` | Sets the sprite id at a map cell. Out-of-range writes are ignored. |
| `map` | `cellX, cellY, screenX, screenY, cellWidth = 40, cellHeight = 23, scale = 1f, colorOpaqueness = 1f, layerMax = 0` | Draws a region of the map to the screen, optionally scaled. `scale` only supports `0.5`, `1` and `2`; other values snap to the nearest. Ignores `pal` and `palt`; color `0` is always transparent (see [Graphics](#graphics)). |

Cells holding sprite `0` are skipped, so the background shows through them.

`layerMax` filters which tiles are drawn, using the sprite flags as layer bits. The default of `0` draws every tile. Any other value is a bitmask: a tile is drawn only if at least one of its flags is set in the mask, i.e. `fget(tile) & layerMax` is non-zero. So if you set flag `0` on your background tiles and flag `1` on your foreground tiles, `map(..., layerMax: 1)` draws just the background and `map(..., layerMax: 2)` just the foreground — call `map` twice, with your sprites drawn in between, to get sprites sandwiched between two map layers.

### Sprite Flags

| Function | Parameters | Description |
|---|---|---|
| `fget` | `spriteId` | Gets all flag bits for a sprite. |
| `fget` | `spriteId, flag` | Gets whether a specific flag is set for a sprite. |
| `fset` | `spriteId, flag, value` | Sets a specific flag on a sprite. |
| `fset` | `spriteId, value` | Sets all flag bits for a sprite. |

Each sprite has 8 flags (`flag` `0`-`7`), free for you to use as collision, terrain type or anything else. `map` also reads them as layer bits when you pass `layerMax` (see [Map](#map)).

### Input

| Function | Parameters | Description |
|---|---|---|
| `btn` | `button` | Returns whether a button is currently held (player 0). |
| `btn` | `button, player` | Returns whether a button is currently held for a given player (`player` `0` or `1`). |
| `btnp` | `button` | Returns whether a button was just pressed this frame (player 0). No key repeat. |
| `btnp` | `button, player` | Returns whether a button was just pressed this frame for a given player. |
| `btnr` | `button` | Returns whether a button was just released. Player 0 only — pass `8`-`15` for player 1. |
| `mouseup` | — | Returns whether the mouse wheel scrolled up this frame. |
| `mousedown` | — | Returns whether the mouse wheel scrolled down this frame. |
| `mouselp` | — | Returns whether the left mouse button was just pressed. |
| `mouselr` | — | Returns whether the left mouse button was just released. |
| `mousel` | — | Returns whether the left mouse button is held. |
| `mouserp` | — | Returns whether the right mouse button was just pressed. |
| `mouserr` | — | Returns whether the right mouse button was just released. |
| `mouser` | — | Returns whether the right mouse button is held. |
| `mousexy` | — | Returns the current mouse position as `(x, y)`. |

#### Button Indices

Indices `0`-`7` are player 0 and `8`-`15` are player 1; `btn(button, player)` is shorthand for `btn(player * 8 + button)`. Out-of-range indices return `false`.

| Index | Button | Player 0 keyboard | Player 1 keyboard | Gamepad |
|---|---|---|---|---|
| `0` | Left | `Left` | `A` | D-Pad / left stick |
| `1` | Right | `Right` | `D` | D-Pad / left stick |
| `2` | Up | `Up` | `W` | D-Pad / left stick |
| `3` | Down | `Down` | `S` | D-Pad / left stick |
| `4` | A (O) | `Z` | `G` | `A` |
| `5` | B (X) | `X` | `H` | `B` |
| `6` | X | `C` | `J` | `X` |
| `7` | Y | `V` | `K` | `Y` |

The left analog stick also drives indices `0`-`3`, with a `0.5` deadzone.

### Sprite Editor

| Function | Parameters | Description |
|---|---|---|
| `SetPixel` | `x, y, colorIndex` | Sets a single pixel in the sprite sheet. |
| `SetRectFill` | `x, y, w, h, colorIndex` | Fills a rectangular region in the sprite sheet. |
| `SetRect` | `x, y, w, h, colorIndex` | Draws a rectangle outline in the sprite sheet. |
| `SetOval` | `x0, y0, x1, y1, colorIndex` | Draws an oval outline in the sprite sheet. |
| `SetOvalFill` | `x0, y0, x1, y1, colorIndex` | Draws a filled oval in the sprite sheet. |
| `SetPaintBucket` | `x, y, regionX, regionY, regionW, regionH, colorIndex` | Flood-fills a region in the sprite sheet starting at a pixel, bounded by a rectangle. |

### Audio

| Function | Parameters | Description |
|---|---|---|
| `sfx` | `sfxId, channel = -1, offset = 0, length = -1` | Plays a sound effect. `channel = -1` restarts the sfx (stopping any channel already playing it) on the first free channel. `offset`/`length` select a note range; `length = -1` plays to the end. |
| `music` | `musicId, fadeLength = 0, channelMask = 0` | Plays a music pattern, with optional fade-in and channel mask. |

There are 4 audio channels (`0`-`3`). `sfx(-1)` stops every channel, `sfx(-2, channel)` stops just that channel, and a negative `musicId` stops the music.

### Random

| Function | Parameters | Description |
|---|---|---|
| `rnd` | `max = 1f` (float) | Returns a random float in `[0, max)`. |
| `rnd` | `max` (double) | Returns a random double in `[0, max)`. |
| `rnd` | `max` (int) | Returns a random int in `[0, max)`. |
| `srand` | `seed` | Seeds the random number generator. |

### Math

Following PICO-8, angles are measured in **turns** (`0` to `1`), not radians, and `sin` is negated to match the screen's downward y-axis.

| Function | Parameters | Description |
|---|---|---|
| `abs` | `value` | Returns the absolute value. |
| `atan2` | `dy, dx` | Returns the angle of the vector `(dx, dy)`, in turns. |
| `cos` | `angle` | Returns the cosine of an angle given in turns. |
| `sin` | `angle` | Returns the *negated* sine of an angle given in turns. |
| `sqrt` | `value` | Returns the square root. |
| `min` | `a, b` | Returns the smaller of two values. |
| `max` | `a, b` | Returns the larger of two values. |
| `mid` | `a, b, c` | Returns the middle value of three. |
| `flr` | `value` | Rounds down to the nearest integer. |
| `ceil` | `value` | Rounds up to the nearest integer. |
| `round` | `value` | Rounds to the nearest integer. |
| `sgn` | `value` | Returns the sign of a value (-1, 0, or 1). |

### Persistence

There are 64 integer slots (`index` `0`-`63`), persisted to disk on every `dset`. Out-of-range reads return `0` and out-of-range writes are ignored.

| Function | Parameters | Description |
|---|---|---|
| `dget` | `index` | Reads a persisted value at `index`. |
| `dset` | `index, value` | Writes a persisted value at `index` and saves to disk. |

## Autotile

An autotile is a **4×4 block of sprites** holding one terrain and every shape that terrain can take, so that painting a single cell in the Map Editor also refits the cells around it into the right edges, corners and diagonals.

Blocks are fixed to the grid: a block is 4×4 sprites aligned to a multiple of four on both axes, giving **8 blocks across by 7 down**. The sprite sheet is 30 rows tall, so its **last two rows form no block** — sprites there can never be part of an autotile. Of the block's sixteen cells, the top-left one is its **empty tile** — which for the very first block is sprite `0`, the sheet's reserved "no sprite" id — and the other fifteen hold one piece each, hence a *15-piece* autotile.

### The 16 cells

Terrain is tracked per **quadrant**, not per edge: each cell covers some subset of its tile's four 4×4 quadrants, and the sixteen cells cover the sixteen possible subsets exactly once — which is what lets painting pick a piece straight from the terrain a cell must show. The layout is fixed — draw your art to match it, reading the block left-to-right, top-to-bottom:

| | Column 1 | Column 2 | Column 3 | Column 4 |
|---|---|---|---|---|
| **Row 1** | *empty* | BR | TR + BL | TL |
| **Row 2** | BL | TR + BR | TL + BL + BR | BL + BR |
| **Row 3** | TL + BR | TR + BL + BR | **solid** (all four) | TL + TR + BL |
| **Row 4** | TR | TL + TR | TL + TR + BR | TL + BL |

`TL`/`TR`/`BL`/`BR` are the tile's top-left, top-right, bottom-left and bottom-right quadrants. Both editors can overlay exactly this table on your sprites — see the sprite editor's [Autotile Guide](#autotile-guide) and the map editor's [Autotile Blocks](#autotile-blocks).

### Marking a block

A block only behaves as an autotile once you mark it as such, which you do with the autotile button in the **Map Editor** (see below). The marks live in `data.atl`, one line per block row and one `0`/`1` per block, written with the rest of the project on `Ctrl+S`. A missing or short file reads as all-off, so an older project simply loads with no autotiles.

## Sprite Editor

Edits sprites in the sprite sheet, plus per-sprite flags and an 8-frame animation preview.

### Tools

Selected via the tool row below the palette:

| Tool | Description |
|---|---|
| Pixel | Left-click a pixel in the canvas to set it to the selected color. |
| Rect | Drag across the canvas to draw a rectangle outline. |
| RectFill | Drag across the canvas to draw a filled rectangle. |
| Oval | Drag across the canvas to draw an oval outline. |
| OvalFill | Drag across the canvas to draw a filled oval. |
| PaintBucket | Left-click to flood-fill the sprite region with the selected color. |

### Autotile Guide

The button at the end of the tool row is **not a tool** — it toggles on its own, so the guide can be shown while any paint tool is selected. It overlays, on the canvas, the terrain each cell of a 4×4 [autotile](#autotile) block is expected to hold: a half-transparent quarter-tile of color per quadrant the piece covers, and nothing for the quadrants it leaves empty. The fill is **green** once the block is marked as an autotile and **blue** while it is not.

The guide is always laid out from the canvas's top-left tile, so it only appears when the selected sprite is a **block's first cell** (its empty tile). Sprites elsewhere in a block, or in the sheet's leftover last two rows, start no block and draw no guide. Only the cells the current canvas zoom brings into view are overlaid: at `x1` the canvas holds a single tile and the guide covers just that one, so set the zoom (mouse wheel over the canvas) to `x4` or `x8` to see the whole block laid out while you draw it.

### Palette & Navigator

Click a color swatch in the palette (top-right) to select the draw color. Left-click a sprite in the bottom navigator to select it for editing, right-click a sprite to make it the reference sprite of the one being edited, or click a page button to switch sprite-sheet pages. Mouse wheel up/down over the canvas zooms it in/out (`x1`-`x8`).

### Sprite Flags

Eight flag toggle buttons below the tool row — click to toggle each of the 8 flag bits on the current sprite (equivalent to `fget`/`fset`).

### Animation Panel

An 8-slot animation frame strip (top-left) with playback controls and a live preview:

| Control | Description |
|---|---|
| Frame slots | Left-click a slot to assign the current sprite to it; right-click to clear it (mirrors the `1`-`8` digit-key toggle below). |
| Zoom button | Left-click cycles the preview zoom up (`x1`→`x2`→`x4`→`x8`); right-click cycles it down. |
| Speed button | Left-click cycles playback speed up; right-click cycles it down. |
| Loop-mode button | Left-click cycles Pause → Forward → Reverse → Ping-pong; right-click cycles the same list in reverse. |

### Reference Sprite (Onion Skinning)

A column of four buttons to the right of the sprite canvas lets you ghost-draw another sprite behind or in front of the one you're editing, useful for tracing over an adjacent animation frame. The reference sprite assignment is per-sprite (each sprite remembers its own reference) and is not saved to disk.

| Button | Description |
|---|---|
| Number | Left-click to start editing, then type digits (`0`-`9`) to set the reference sprite number (1 to the max sprite index). Click anywhere outside the button to stop editing; press `Delete` while editing to clear it. Shows `--` when unset. |
| Order | Left-click or right-click toggles whether the reference draws **behind** (`BEH`) or **in front** (`FRO`) of the current sprite. |
| Visualization | Left-click cycles forward, right-click cycles backward through `ORG` (original colors) → `RED` → `GRN` → `BLU`, recoloring the reference sprite via `pal`. |
| Opacity | Left-click cycles forward, right-click cycles backward through `20`/`40`/`60`/`80`/`100` (percent). |

Instead of typing a number, you can **right-click any sprite in the bottom navigator** to make it the reference of the sprite you are editing. This overwrites whatever reference was already set, or sets one if there was none. Right-clicking sprite `0` (the empty sprite) or the sprite currently being edited clears the reference back to `--`, since neither is a meaningful onion skin.

### Sprite Editor Hotkeys

| Key | Description |
|---|---|
| `Ctrl+S` | Saves the project. |
| `Ctrl+Z` | Undo (available when there is a change to undo). |
| `Ctrl+Shift+Z` | Redo (available when there is a change to redo). |
| `Ctrl+C` | Copies the current sprite region. |
| `Ctrl+V` | Pastes the copied region at the current sprite's position. |
| `Arrow Left/Right/Up/Down` | Shifts the pixels of the current sprite by one pixel in that direction. |
| `Delete` | Clears the current sprite region (or, while editing the reference-sprite number, clears the reference instead). |
| `F` | Flips the current sprite horizontally. |
| `V` | Flips the current sprite vertically. |
| `R` | Rotates the current sprite 90° clockwise. |
| `1`-`8` | Toggles the current sprite into/out of the corresponding animation frame slot (only when no modifier keys are held). |
| `9` | Clears all animation frame slots (only when no modifier keys are held). |

Mouse wheel up/down over the sprite canvas zooms the canvas in/out.

## Map Editor

The map editor paints tile indices onto the map grid using the sprite currently selected in the bottom sprite navigator (the same sprite sheet used by the Sprite Editor).

The map is edited as **four layers** (see below), so the viewport shows one quarter of the full map at a time with the layers stacked on top of each other.

### Layers

The `512×576` map is split into four equal `256×288` quarters, each treated as an independent layer:

| Layer | Quarter |
|---|---|
| 1 | Top-left |
| 2 | Top-right |
| 3 | Bottom-left |
| 4 | Bottom-right |

The viewport shows one quarter's worth of area and overlays all layers there, each drawn from its own quarter. They stack front-to-back in order 1, 2, 3, 4 — so layer 1 draws on top and layer 4 at the back. When the enabled layer is not the front-most one, the layers in front of it are drawn semi-transparent so the enabled layer still shows through.

Eight buttons sit on the tool row (just right of the tool buttons), in pairs per layer: **layer 1, view/hide 1, layer 2, view/hide 2, …** up to layer 4.

| Button | Description |
|---|---|
| Layer *X* | **Right-click** to make layer *X* the enabled layer — the one every edit (painting, filling, selecting, copying, pasting, deleting) acts on. Only one layer is enabled at a time; the swatch is white when enabled and dark grey otherwise. |
| View/hide *X* | **Right-click** to toggle whether layer *X* is drawn (open-eye icon when shown, closed-eye when hidden). The currently enabled layer is always drawn and cannot be hidden. |

Edits are confined to the enabled layer's quarter and never spill into a neighbour. Copy/paste share one clipboard across layers, so you can copy a region on one layer and paste it onto another.

### Autotile Blocks

One tile past the layer buttons sits the **autotile button**, which marks the 4×4 block the selected sprite belongs to as a 15-piece [autotile](#autotile) — or unmarks it. Left-click toggles it, and an `AUTOTILE ON` / `AUTOTILE OFF` notice confirms which way it went. The button lights up while the selected sprite's block is an autotile, and is inert for sprites in the sheet's last two rows, which are too short to form a block.

**Hover the button** (without clicking) to preview the block over the sprite navigator: which 4×4 block the button will affect, and the terrain each of its sixteen cells is expected to hold — a half-transparent quarter-tile of color per quadrant a piece covers, **green** if the block is already an autotile and **blue** if not. Cell 0 covers nothing, being the block's empty tile, so it draws nothing at all. A block spans four sheet rows and can straddle a page boundary, in which case only the rows the current navigator page shows are overlaid.

Both the button and its preview are part of the split view, so neither is shown in the full-screen map view.

### Painting with an Autotile

Once a block is marked, selecting **any** of its sprites and painting with the **Pixel** tool stamps that piece and refits the eight cells around it, so terrain grows with the correct edges, corners and diagonals as you drag. The piece you select names the terrain the stamped cell will show, and the neighbours are refitted against it: they grow edges up against terrain the stamp lays down and retract from terrain it clears. So pick the **solid** cell to paint terrain in, and the block's **empty** cell to erase it back out — the stamp always comes back out of the refit as the very piece you picked.

Nothing extra is stored to make this work: away from the stamp, the terrain is read back off the tiles already on the map, so a stroke composes with whatever it grows into. Tiles that are not pieces of the block being painted — another block's tiles, loose art, the far side of the layer's edge — carry no terrain, so the stroke fits its own edge up against them and leaves them untouched. Refitting is confined to the 3×3 around the stamped cell and to the enabled layer's quarter, and the whole drag stroke remains a single undo step.

The other tools are unaffected: **RectFill** fills a region with the raw selected sprite, with no refitting.

### Tools

Selected via the tool row (left of the sprite-number label and page buttons):

| Tool | Description |
|---|---|
| Pixel | Left-click (or drag) to set cells to the selected sprite. A whole drag stroke is a single undo step. |
| RectFill | Drag from one cell to another to fill the rectangular region with the selected sprite. |
| Select | Drag from one cell to another to mark an area. The area stays highlighted with an animated marching-ants border until you right-click to cancel, pick another tool, or leave the editor. With a selection active: `Del` clears it, `Ctrl+C` copies it, and `Ctrl+V` pastes the copied tiles at the selection's top-left. |
| Hand | Drag to pan the map viewport with the mouse. |

Map edits support undo/redo with `Ctrl+Z` / `Ctrl+Shift+Z` (up to 50 steps; history is cleared when a cart is loaded).

### Sprite Navigator

Click a sprite in the bottom navigator panel to select it for painting; click a page button to switch between sprite-sheet pages. The toggle in the top-left menu bar (only shown while the Map Editor is active) switches to a full-screen map view, hiding the tool row (including the layer buttons) and sprite navigator.

### Zoom

Mouse wheel up/down over the map viewport zooms in/out through three levels — `x1/2`, `x1` (default) and `x2` — in both split and full-screen map views. Zooming keeps the cell under the cursor anchored in place, and the wheel is ignored while a RectFill/Select drag or a Hand pan is in progress. Zooming out shows more of the map at once (at `x1/2` each cell is 4 pixels), which is useful for navigating large maps; painting, panning and the rectangle tools all operate on cells at whatever zoom is active.

The current zoom level is always shown on the bottom bar, immediately left of the `X:nnn Y:nnn` cell coordinates (which only appear while the cursor is over the map).

### Map Editor Hotkeys

| Key | Description |
|---|---|
| `Ctrl+S` | Saves the project. |
| `Arrow Left/Right/Up/Down` | Pans the map viewport by one tile in that direction, repeating while held. |
| `Ctrl` + `Arrow Left/Right/Up/Down` | Pans the map viewport by 8 tiles in that direction, repeating while held. |


## SFX Editor

Each SFX is 32 notes, and every note stores a pitch, a waveform, a volume and an effect. The editor has two views, toggled by the button in the top-left menu bar (only shown while the SFX editor is active):

- **Primary view** — draw pitch and volume bars directly with the mouse.
- **Alternate (tracker) view** — enter notes from the keyboard, PICO-8 tracker style.

### Updating a note in the alternate view

The alternate view lays the 32 notes out as an 8-row × 4-column grid, with palettes for the values applied to newly typed notes.

1. **Toggle the alternate view** using the menu-bar button in the top-left.
2. **Pick the value palettes** for the notes you are about to enter:
   - Click a **waveform** icon to choose the instrument.
   - Click an **OCT** box (1–4) to set the base octave.
   - Click a cell in the **VOL** fader (7 at the top, 0 at the bottom) to set the volume.
   - Click an **FX** icon (0–7) to set the effect.
3. **Position the cursor** on the target cell — click a grid cell, or move with the arrow keys (`Up`/`Down` move within a column, `Left`/`Right` jump between columns).
4. **Type a note** using the piano keys. This writes the note (pitch + selected waveform, volume and effect) into the cursor cell, previews it, then advances the cursor down one cell.
5. **Clear a note** by right-clicking its cell, or by pressing `Delete`/`Backspace` on the cursor cell (which also advances the cursor).

### SFX Editor Hotkeys

| Key | Description |
|---|---|
| `Ctrl+S` | Saves the project. |
| `Space` | Plays the current SFX. |
| `Left`/`Right` | Primary view: selects the previous/next SFX index. |
| `Arrow keys` | Alternate view: moves the note cursor (`Up`/`Down` within a column, `Left`/`Right` between columns). |
| `Delete`/`Backspace` | Alternate view: clears the note at the cursor and advances it. |
| `Z S X D C V G B H N J M , L .` | Alternate view: piano keys for the base octave (`Z` = root). |
| `Q 2 W 3 E R 5 T 6 Y 7 U I` | Alternate view: piano keys one octave above the base. |

The SFX index selector, speed (`SPD`), loop points (`LP`), and `PLAY`/`STOP` controls in the header are shared by both views. `PLAY` and loop/speed boxes also respond to the mouse wheel.

## Music Editor

A pattern bank where each pattern plays up to four SFX at once, one per channel. Each channel column shows a tracker-style note grid for the SFX assigned to it — editing a note here edits the underlying SFX directly.

- **Pattern strip** (top) — click a pattern box, or the `<`/`>` arrows, to select it. The selected pattern is highlighted white; the currently playing pattern lights up orange below its number, and its index is shown in green.
- **Loop/Stop controls** (top-right) — click to toggle loop-start, loop-end and stop flags on the selected pattern.
- **Channel header** (per column) — click the toggle box to enable/disable the channel for this pattern; while enabled, click the SFX number to scroll wheel through it (left-click +1, right-click -1), or click the pencil icon to jump to that SFX in the SFX Editor.
- **Note grid** (per enabled channel) — a scrollable 32-note tracker column (note, octave, waveform, volume, effect), identical semantics to the SFX editor's alternate view. Mouse wheel over a column scrolls it; while playing, each column auto-scrolls to keep the currently sounding note (highlighted yellow) centered.

### Music Editor Hotkeys

| Key | Description |
|---|---|
| `Ctrl+S` | Saves the project. |
| `Space` | Plays/stops the selected pattern. |
| `Up`/`Down` | Moves the note cursor within the selected channel's column. |
| `Left`/`Right` | Moves between note parts (note, octave, waveform, volume, effect). |
| `Delete`/`Backspace` | Clears the note at the cursor and advances it. |
| `Z S X D C V G B H N J M , L .` | Piano keys for the base octave (`Z` = root) — writes a note when the note part is selected. |
| `Q 2 W 3 E R 5 T 6 Y 7 U I` | Piano keys one octave above the base. |
| `0`-`9` | When an octave/waveform/volume/effect part is selected, sets that part's value. |

Left-click a note cell to select it (and the part clicked); right-click a note cell to clear it.