# Mono8 — Agent Guide

Mono8 is a PICO-8 game engine built on MonoGame (.NET 8). The repo contains **two things**:

1. **The engine** — everything under [src/](src/) except [src/game/](src/game/). Editors, renderer, audio, file formats. **Off limits.**
2. **The game** — [src/game/](src/game/). This is the only place game code lives, and the only place you write.

---

## The two hard rules

### 1. Never edit anything outside `src/game/`

You may **read** any file in the repo for context. You may **write** only inside [src/game/](src/game/).

Never create, edit, delete or move:

- [src/IMono8API.cs](src/IMono8API.cs), [src/Mono8API.cs](src/Mono8API.cs), [src/Mono8Game.cs](src/Mono8Game.cs), [src/Program.cs](src/Program.cs)
- anything under [src/core/](src/core/) or [src/editor/](src/editor/)
- anything under [src/publishdata/](src/publishdata/) or [src/data/](src/data/) — sprites, map, sfx, music and json are **authored by the developer in the built-in editors**, never by you
- [src/mono8.csproj](src/mono8.csproj), [README.md](README.md), or any config

If a feature seems to need an engine change, **stop and say so**. Do not work around it by touching engine code. Propose the closest thing achievable through the existing API and let the developer decide.

### 2. Everything goes through `API` (`IMono8API`)

[src/IMono8API.cs](src/IMono8API.cs) is the complete surface available to game code. Game code must not:

- use MonoGame types (`Texture2D`, `SpriteBatch`, `Vector2`, `Color`, `Keyboard`, …)
- touch engine internals (`Mono8API.SpriteSheet`, `MapSheet`, `SfxEngine`, `Screen`, `ColorPalette`, …)
- read or write files, use `System.IO`, `Console`, threads, timers or `DateTime` for timing
- use `System.Linq`, reflection, or anything that allocates per frame in `Update`/`Draw`

Plain C# (`int`, `float`, `string`, arrays, `List<T>`, structs, classes, enums, `Math` for helpers the API lacks) is fine. `Constants.Colors.*` and `Constants.Screen.*` are fine — they are constants, not engine state.

`IEditorAPI` (the `SetPixel`/`SetRectFill`/… block at the bottom of `IMono8API.cs`) is **editor-only**. Game code never gets it and never calls it.

---

## Assets are the developer's, not yours

Sprite ids, map regions, sprite flags, sfx indices, music patterns and `data.json` groups/objects/fields are **authored by the developer**. The data files ship empty and the developer fills them in the editors.

**Never invent an asset id.** If the request says "draw the player" without saying which sprite, or "play a jump sound" without an sfx index, **ask** — one short question listing exactly what you need:

> Which sprite id is the player, and how many tiles wide/tall? Which sfx index for the jump?

The one exception: if the developer explicitly says "pick something" or "use a placeholder", use an obvious placeholder (`spr(1, …)`, `sfx(0)`) and mark it with a `// TODO: developer to confirm` comment.

`data.json` is the one asset you do not have to ask about — it is plain text you can read. Open it and use what is there; see [JSON data](#json-data-gjson--sjson).

When the developer *does* give you ids, put them in named `const int` fields at the top of the class, so a re-authored sprite sheet is a one-line change:

```csharp
private const int SprPlayer = 64;   // 2x2
private const int SfxJump = 3;
private const int FlagSolid = 0;
```

Read the current data files when you need to confirm what exists — they live in [src/publishdata/](src/publishdata/), which is where the editor mirrors every save and what git tracks:

| File | Holds |
|---|---|
| [src/publishdata/data.json](src/publishdata/data.json) | authored json — **read this before writing any `gjson` call** |
| [src/publishdata/data.gff](src/publishdata/data.gff) | per-sprite flag bits, one line per sheet row |
| [src/publishdata/data.atl](src/publishdata/data.atl) | which 4×4 blocks are autotiles |

Read only — never write them. (`src/data/` is the runtime copy the build consumes; it is not the authored source and is not where you look.)

---

## Console specs

| | |
|---|---|
| Screen | 256×144 pixels, palette indices `0`-`31` |
| Frame rate | Fixed 30 FPS (`elapsedSeconds` ≈ `0.0333`) |
| Sprite sheet | 256×240 px = 32×30 tiles of 8×8 → sprite ids `0`-`959` |
| Map | 512×576 cells, edited as four 256×288 layers |
| Audio | 64 sfx, 64 music patterns, 4 channels |
| Persistence | 64 int slots (`dget`/`dset`) |

Sprite `0` is the empty sprite — `map` never draws it and it never collides. Color `0` is transparent by default.

Colors have names in `Constants.Colors`: `Black`(0) `DarkBlue`(1) `DarkPurple`(2) `DarkGreen`(3) `Brown`(4) `DarkGray`(5) `LightGray`(6) `White`(7) `Red`(8) `Orange`(9) `Yellow`(10) `Green`(11) `Blue`(12) `Indigo`(13) `Pink`(14) `Peach`(15) `BrownBlack`(16) `DarkerBlue`(17) `DarkMaroon`(18) `DarkTeal`(19) `DarkBrick`(20) `DarkTaupe`(21) `Tan`(22) `LightYellow`(23) `DarkCrimson`(24) `BrightOrange`(25) `LimeGreen`(26) `MediumGreen`(27) `MediumBlue`(28) `Mauve`(29) `Salmon`(30) `LightPeach`(31). **Always use the names, never the raw index.**

---

## Game entry point

[src/game/YourGame.cs](src/game/YourGame.cs) is the only class the engine calls:

```csharp
internal class YourGame : IEditor
{
    public static IMono8API API;
    public YourGame(IMono8API api) { API = api; }

    public void Init() { }                        // once, when the developer hits Ctrl+R
    public void Update(float elapsedSeconds) { }  // once per frame, logic
    public void Draw() { }                        // once per frame, drawing
}
```

- `Init()` runs on **every** Ctrl+R and on pause-menu **Restart** — so it must fully reset state, not just set it up the first time.
- `Update` runs only while the game is playing and the pause menu is closed.
- Never change the class name, the constructor signature, the `IEditor` implementation or the three method signatures. `Exit()` may be added (it has a default implementation).
- An exception from any of the three does not crash the process — the engine draws the message and freezes. So a crash is silent-ish; prefer defensive reads (`gjson` returns `null`, getters return fallbacks).

### Adding files

New game files go in [src/game/](src/game/), namespace `mono8.game`, and are compiled automatically (SDK-style glob) — no csproj edit, which you could not make anyway.

Types should be `internal` (the engine's own types are internal; `IEditor` is internal, so a `public` type touching it will not compile). Reach the API from another class with `YourGame.API`, or take `IMono8API` in the constructor — prefer the constructor for anything testable, `YourGame.API` for small helpers.

Split by responsibility once `YourGame.cs` grows past a few hundred lines: `Player.cs`, `Level.cs`, `Hud.cs`, and keep `YourGame.cs` as the coordinator that owns them and forwards `Init`/`Update`/`Draw`.

### Style

Match the surrounding code: file-scoped namespaces, 4-space indent, `private` fields without a `_` prefix are *not* the house style — engine fields use `_camelCase` for private instance fields and `PascalCase` for public/static. Comments explain *why*, not *what*; the engine's comment density is low and deliberate. No regions, no XML docs on private members.

---

## API reference

Full prose reference is in [README.md](README.md#api-reference); the signatures are in [src/IMono8API.cs](src/IMono8API.cs). Condensed:

### Graphics

```csharp
cls(color = 0)
pixel(x, y, color, opacity = 1f)
line(x0, y0, x1, y1, color)
rect(x0, y0, x1, y1, color, opacity = 1f)      rectfill(...)
circ(x, y, radius, color, opacity = 1f)        circfill(...)
oval(x0, y0, x1, y1, color, opacity = 1f)      ovalfill(...)
spr(spriteId, x, y, width = 1, height = 1, scale = 1f, flipX = false, flipY = false, opacity = 1f)
sspr(sx, sy, sw, sh, dx, dy, dw = -1, dh = -1, flipX = false, flipY = false, opacity = 1f)
sprr(...)  ssprr(...)   // same params, single-pass fast path
print(text, x, y, color = 7, opacity = 1f)
icon(n, x, y)
camera(x = 0, y = 0)
pal()  pal(c0, c1)  palt()  palt(c)  palt(c, transparent)
```

- `spr` `width`/`height` are in **8×8 tiles** — `spr(64, x, y, 2, 2)` draws a 16×16 block whose top-left tile is sprite 64. The block reads across the sheet's 32-tile rows, so a 2×2 block at 64 uses sprites 64, 65, 96, 97.
- `scale` on `spr` is clamped to `0.125`-`8`. `sspr`'s destination size is arbitrary and can stretch non-uniformly.
- `pal`/`palt` apply to `spr`/`sspr` only. **`sprr`, `ssprr` and `map` ignore them** (single pre-baked pass) — color `0` is still transparent there, and `opacity` still works. Use `sprr`/`ssprr` for many sprites needing no palette tricks; use `spr`/`sspr` when you need recoloring or custom transparency.
- `print` draws **upper-case only**.
- `camera(x, y)` offsets every later draw call. Reset it with `camera()` before drawing the HUD.

### Map

```csharp
int  mget(cellX, cellY)                     // out of range reads 0
void mset(cellX, cellY, spriteId)           // out of range ignored
void map(cellX, cellY, screenX, screenY, cellWidth = 40, cellHeight = 23,
         scale = 1f, opacity = 1f, layerMax = 0)
```

- `scale` supports only `0.5`, `1`, `2` — anything else snaps to the nearest.
- Cells holding sprite `0` are skipped, so the background shows through.
- `layerMax` is a **bitmask over sprite flags**, not a layer index: `0` draws everything, otherwise a tile draws only when `fget(tile) & layerMax != 0`. Flag 0 on background tiles + flag 1 on foreground tiles → `map(..., layerMax: 1)` then your sprites then `map(..., layerMax: 2)`.

The map is one 512×576 sheet. The Map Editor presents it as four 256×288 layers, so a layer's cell origin is:

| Layer | Cell origin |
|---|---|
| 1 | `(0, 0)` |
| 2 | `(256, 0)` |
| 3 | `(0, 288)` |
| 4 | `(256, 288)` |

Ask the developer which layer holds what, then keep the offset in a const. Room/screen scrolling within a layer is your game's business — the engine has no room concept.

### Collision

```csharp
bool mcol(x, y, flag = 0)                   // point on a flagged tile
bool mcol(x, y, w, h, flag = 0)             // rect meets a flagged tile
bool acol(x, y, spriteId = -1)              // point on autotile terrain
bool acol(x, y, w, h, spriteId = -1)        // rect meets autotile terrain
```

Coordinates are **pixels over the whole map sheet** = cell coords × 8. Apply your camera/layer offsets *before* calling. The rect runs `x, y` → `x + w - 1, y + h - 1`; an empty rect (side ≤ 0) meets nothing, as does anything off the map.

- `mcol` reads a sprite flag (`0`-`7`), whose meaning is entirely your game's — flag 0 solid, another ice, another hazard, per the developer. One bit per whole 8×8 tile.
- `acol` reads **autotile terrain at quadrant precision** (quarter-tile), which is what edge and diagonal autotile pieces actually cover — a flag cannot express that. `spriteId` narrows the question to that sprite's 4×4 block, so walls answer without water answering; `-1` asks about every autotile.

Use `mcol` for hand-drawn flagged tiles, `acol` for terrain painted with an autotile brush. Ask the developer which one their level uses.

```csharp
int  fget(spriteId)              bool fget(spriteId, flag)
void fset(spriteId, flag, value) void fset(spriteId, value)
```

### Input

```csharp
bool btn(button)   bool btn(button, player)    // held
bool btnp(button)  bool btnp(button, player)   // pressed this frame, no repeat
bool btnr(button)                              // released this frame
bool mousel() mouselp() mouselr()  mouser() mouserp() mouserr()
bool mouseup() mousedown()                     // wheel
(int x, int y) mousexy()
```

Player 0 is buttons `0`-`7`, player 1 is `8`-`15`; `btn(b, p)` == `btn(p * 8 + b)`. `btnr` takes the raw index only.

| 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| Left | Right | Up | Down | A (Z) | B (X) | X (C) | Y (V) |

### Audio

```csharp
sfx(sfxId, channel = -1, offset = 0, length = -1)
music(musicId, fadeLength = 0, channelMask = 0)
```

4 channels (`0`-`3`). `channel = -1` restarts the sfx on the first free channel. `sfx(-1)` stops every channel, `sfx(-2, ch)` stops one, a negative `musicId` stops the music. `offset`/`length` select a note range within the 32-note sfx.

### JSON data (`gjson` / `sjson`)

Tuning values — speeds, spawn points, hit boxes, room layouts, animation frames — belong in `data.json` and not in code, so the developer can retune them in the editor without a rebuild. This is the one asset the developer authors that you can read directly, so **never ask which group or object to use — open the file and look.**

#### Step 1 — read the file

Always `Read` [src/publishdata/data.json](src/publishdata/data.json) before writing a `gjson` call. It is a fixed three levels, **group → object → field**, with the field's type as a one-char suffix on its key:

```json
{
  "ENEMY": {
    "SLIME": { "HP:i": 12, "SPD:d": 1.25, "SPAWN:p": [40, 88], "NAME:t": "Green slime" }
  }
}
```

Here `ENEMY` is the group, `SLIME` the object, `HP` the field. **The suffix is not part of the name** — it is `GetInt("HP")`, never `GetInt("HP:i")`.

#### Step 2 — suffix → getter

| Suffix | Type | JSON form | Getter | C# type |
|---|---|---|---|---|
| `:t` | Text | `"GREEN SLIME"` | `GetStr(f, i, fallback)` | `string` |
| `:i` | Int | `12` | `GetInt(f, i, fallback)` | `int` |
| `:d` | Decimal | `1.25` | `GetDec(f, i, fallback)` | `double` — cast to `float` |
| `:m` | Money | `"3.50"` (quoted, 2dp) | `GetMoney(f, i, fallback)` | `decimal` |
| `:p` | PosXY | `[40, 88]` | `GetXY(f, i)` | `(int x, int y)` |
| `:b` | Bool | `true` | `GetBool(f, i, fallback)` | `bool` |

A getter that does not match the field's declared type returns the fallback rather than converting — `GetInt` on a `:d` field gives you the fallback, not a truncated number. Match the suffix exactly.

**Any field can instead hold an array** of its type — a JSON `[…]` where a scalar would be, up to 16 items. `:p` is the one to watch: `"SPAWN:p": [40, 88]` is a *single* position, while `"SPAWN:p": [[40, 88], [72, 16]]` is two. Read arrays either by index or as a span:

```csharp
int n = data.Count("ID");                      // 1 for a scalar, 0 when the field is absent
for (int i = 0; i < n; i++) Use(data.GetStr("ID", i));

foreach (int hp in data.IntArray("HP")) { }    // no copy, no allocation; Dec/Bool too
```

#### Step 3 — the call

```csharp
Mono8JsonObject gjson(string group, string obj)   // null when unknown; case-insensitive; allocation-free
```

Names match without regard to case, so `gjson("PLAYER", "STATS")` and `gjson("player", "stats")` are the same lookup. Put the names in `const string` fields and the fallbacks in `const` next to them, then read defensively — an unauthored or half-authored object must still run:

```csharp
private const string JsonGroup = "PLAYER";
private const string JsonObject = "STATS";
private const float DefaultSpeed = 60f;   // px/s

// Re-read every Init: Ctrl+S in the JSON editor rebuilds the data without a restart.
var stats = API.gjson(JsonGroup, JsonObject);
if (stats != null)
{
    Speed = (float)stats.GetDec("SPEED", 0, DefaultSpeed);
    if (stats.Has("SPAWN")) (SpawnX, SpawnY) = stats.GetXY("SPAWN");
}
```

Three rules that make this hold up:

- **Never throw, never assume.** `gjson` returns `null` for an unknown group or object, and every getter returns its fallback for a missing field, a wrong-typed one or an index past the end. Null-check the object and pass a real fallback to each getter.
- **`GetXY` has no fallback parameter** — a missing PosXY reads `(0, 0)`, which is a legitimate coordinate. When `(0, 0)` would be wrong (a zero-sized hit box, a spawn at the origin), gate on `Has(field)` or sanity-check the values, as above.
- **Read in `Init()`, not in `Update`/`Draw`.** Ctrl+S rebuilds the runtime data without a restart, so an object cached from a previous run is stale; caching the object in a field is fine as long as `Init()` re-reads it.

Shape checks when you need them: `Has(field)`, `TypeOf(field)` (`DataValueType`), `IsArray(field)`, `Count(field)`.

#### Writing back

```csharp
bool sjson(group, obj, field, value, index = 0)   // one overload per type; in-memory only
```

The overload is picked from the value's C# type: `20` → Int, `1.5` → Decimal, `3.50m` → Money, `true` → Bool, `"t"` → Text, `(40, 88)` → PosXY (one tuple argument, so it cannot be read as the int overload's `value, index`). Returns `false` and changes nothing when the group, object or field is unknown, the index is past the end of an array, or the overload does not match the declared type.

`sjson` **never creates a field and never writes `data.json` back to disk** — the write lands in the running snapshot and is gone on restart. It is for live tweaks, not persistence; use `dset` for anything that must survive.

#### Authoring limits

Names are ≤ 8 chars, upper-cased, and cannot contain `:` `,` `"` `\` or spaces. At most 16 groups, 64 objects per group, 16 fields per object, 16 items per array. If a request needs data the file does not hold, say which group/object/field you need and let the developer author it — you cannot add it yourself.

### Math, random, persistence, system

Angles are in **turns** (`0`-`1`), not radians, and `sin` is **negated** to match the downward y-axis — PICO-8 convention. `atan2(dy, dx)` returns turns.

```csharp
abs atan2 cos sin sqrt min max mid flr ceil round sgn   // all double
float rnd(float max = 1f)   double rnd(double)   int rnd(int max)   // [0, max)
void srand(seed)
int dget(index)   void dset(index, value)      // 0-63, dset writes to disk immediately
double time()     int stat(id)                 // stat(7) = FPS, everything else 0
void menuitem(index, label, callback)          // index 0-2, label ≤ 16 chars
void menuitem(index)                           // remove
```

---

## Working conventions

- **Delta time**: multiply by `elapsedSeconds` for anything that moves. The engine is fixed at 30 FPS, but do not hardcode `1/30`.
- **Draw order**: `Draw()` paints back to front. Typical order is `cls` → `camera(worldX, worldY)` → background `map` → entities → foreground `map` → `camera()` → HUD `print`.
- **No per-frame allocation**: no `new` in `Update`/`Draw` for anything that could be a field, no LINQ, no string concatenation in a loop. `print($"SCORE {score}")` once per frame is acceptable; a hundred is not.
- **State resets in `Init()`**, since Restart calls it again.
- **Verify with a build**, never by running: `dotnet build src/mono8.csproj`. The app is a GUI that opens the editor — don't launch it. Report the build result honestly, including warnings you introduced.
- **Do not commit** unless asked. `src/publishdata/*` shows as modified in git whenever the developer saves in an editor — that is theirs, never stage or revert it.
