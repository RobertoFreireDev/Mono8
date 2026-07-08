# Mono8

- dotnet publish mono8.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

## API Reference

PICO-8 style API. All coordinates are pixel-based unless otherwise noted.

### System

| Function | Parameters | Description |
|---|---|---|
| `time` | — | Returns elapsed time in seconds since start. |
| `stat` | `id` | Returns a system statistic identified by `id`. |
| `menuitem` | `index, label, callback` | Adds/updates a custom menu item with a label and callback. |
| `menuitem` | `index` | Removes the custom menu item at `index`. |

### Graphics

| Function | Parameters | Description |
|---|---|---|
| `cls` | `colorIndex = 0` | Clears the screen with the given color. |
| `pixel` | `x, y, color` | Sets a single pixel's color. |
| `line` | `x0, y0, x1, y1, color` | Draws a line between two points. |
| `rect` | `x0, y0, x1, y1, color` | Draws a rectangle outline. |
| `rectfill` | `x0, y0, x1, y1, color` | Draws a filled rectangle. |
| `circ` | `x, y, radius, color` | Draws a circle outline. |
| `circfill` | `x, y, radius, color` | Draws a filled circle. |
| `oval` | `x0, y0, x1, y1, color` | Draws an oval outline within the given bounds. |
| `ovalfill` | `x0, y0, x1, y1, color` | Draws a filled oval within the given bounds. |
| `spr` | `spriteId, x, y, width = 1, height = 1, scale = 1, flipX = false, flipY = false` | Draws a sprite at the given position, with optional size, scale and flipping. |
| `sspr` | `sx, sy, sw, sh, dx, dy, dw = -1, dh = -1, flipX = false, flipY = false` | Draws a rectangular region of the sprite sheet, scaled and/or flipped, to the screen. |
| `print` | `text, x, y, color = 7` | Prints text at the given position with the given color. |
| `icon` | `n, x, y` | Draws icon `n` at the given position. |
| `camera` | `x = 0, y = 0` | Sets the camera offset applied to subsequent draw calls. |
| `pal` | — | Resets the palette to its default state. |
| `pal` | `c0, c1` | Remaps color `c0` to `c1` for subsequent draw calls. |
| `palt` | — | Resets transparency settings to default. |
| `palt` | `colorIndex` | Toggles transparency for a color index. |
| `palt` | `colorIndex, transparent` | Sets whether a color index is treated as transparent. |

### Map

| Function | Parameters | Description |
|---|---|---|
| `mget` | `cellX, cellY` | Gets the sprite id at a map cell. |
| `mset` | `cellX, cellY, spriteId` | Sets the sprite id at a map cell. |
| `map` | `cellX, cellY, screenX, screenY, cellWidth = 40, cellHeight = 23, layerMax = 0` | Draws a region of the map to the screen. |

### Sprite Flags

| Function | Parameters | Description |
|---|---|---|
| `fget` | `spriteId` | Gets all flag bits for a sprite. |
| `fget` | `spriteId, flag` | Gets whether a specific flag is set for a sprite. |
| `fset` | `spriteId, flag, value` | Sets a specific flag on a sprite. |
| `fset` | `spriteId, value` | Sets all flag bits for a sprite. |

### Input

| Function | Parameters | Description |
|---|---|---|
| `btn` | `button` | Returns whether a button is currently held (player 0). |
| `btn` | `button, player` | Returns whether a button is currently held for a given player. |
| `btnp` | `button` | Returns whether a button was just pressed (player 0), with repeat. |
| `btnp` | `button, player` | Returns whether a button was just pressed for a given player, with repeat. |
| `btnr` | `button` | Returns whether a button was just released. |
| `mouseup` | — | Returns whether the mouse scroll/direction is up. |
| `mousedown` | — | Returns whether the mouse scroll/direction is down. |
| `mouselp` | — | Returns whether the left mouse button was just pressed. |
| `mouselr` | — | Returns whether the left mouse button was just released. |
| `mousel` | — | Returns whether the left mouse button is held. |
| `mouserp` | — | Returns whether the right mouse button was just pressed. |
| `mouserr` | — | Returns whether the right mouse button was just released. |
| `mouser` | — | Returns whether the right mouse button is held. |
| `mousexy` | — | Returns the current mouse position as `(x, y)`. |

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
| `sfx` | `sfxId, channel = -1, offset = 0, length = -1` | Plays a sound effect on a channel. |
| `music` | `musicId, fadeLength = 0, channelMask = 0` | Plays a music track, with optional fade-in and channel mask. |

### Random

| Function | Parameters | Description |
|---|---|---|
| `rnd` | `max = 1f` (float) | Returns a random float in `[0, max)`. |
| `rnd` | `max` (double) | Returns a random double in `[0, max)`. |
| `rnd` | `max` (int) | Returns a random int in `[0, max)`. |
| `srand` | `seed` | Seeds the random number generator. |

### Math

| Function | Parameters | Description |
|---|---|---|
| `abs` | `value` | Returns the absolute value. |
| `atan2` | `dy, dx` | Returns the angle of the vector `(dx, dy)`. |
| `cos` | `angle` | Returns the cosine of an angle. |
| `sin` | `angle` | Returns the sine of an angle. |
| `sqrt` | `value` | Returns the square root. |
| `min` | `a, b` | Returns the smaller of two values. |
| `max` | `a, b` | Returns the larger of two values. |
| `mid` | `a, b, c` | Returns the middle value of three. |
| `flr` | `value` | Rounds down to the nearest integer. |
| `ceil` | `value` | Rounds up to the nearest integer. |
| `round` | `value` | Rounds to the nearest integer. |
| `sgn` | `value` | Returns the sign of a value (-1, 0, or 1). |

### Persistence

| Function | Parameters | Description |
|---|---|---|
| `dget` | `index` | Reads a persisted value at `index`. |
| `dset` | `index, value` | Writes a persisted value at `index`. |

## Sprite Editor Hotkeys

| Key | Description |
|---|---|
| `Ctrl+S` | Saves the project. |
| `Ctrl+Z` | Undo (available when there is a change to undo). |
| `Ctrl+Shift+Z` | Redo (available when there is a change to redo). |
| `Ctrl+C` | Copies the current sprite region. |
| `Ctrl+V` | Pastes the copied region at the current sprite's position. |
| `Arrow Left/Right/Up/Down` | Shifts the pixels of the current sprite by one pixel in that direction. |
| `Delete` | Clears the current sprite region. |
| `F` | Flips the current sprite horizontally. |
| `V` | Flips the current sprite vertically. |
| `R` | Rotates the current sprite 90° clockwise. |
| `1`-`8` | Toggles the current sprite into/out of the corresponding animation frame slot (only when no modifier keys are held). |
| `9` | Clears all animation frame slots (only when no modifier keys are held). |

Mouse wheel up/down over the sprite canvas zooms the canvas in/out.

## Map Editor Hotkeys

| Key | Description |
|---|---|
| `Ctrl+S` | Saves the project. |
| `Arrow Left/Right/Up/Down` | Pans the map viewport by one tile in that direction. |
| `Ctrl` + `Arrow Left/Right/Up/Down` | Pans the map viewport by 8 tiles in that direction. |

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