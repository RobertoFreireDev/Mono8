# Plan — JSON editor (`data.json`)

Implementation plan for one new built-in editor plus the data layer and runtime API it sits
on. Written against the code as it exists today; every reference points at the file and line
that establishes the convention being followed.
Update Readme file after changes

Delivered as **three independent tasks**:

| Task | Scope | Deliverable |
|---|---|---|
| **T1** | Data model + load/save of `data.json` | Round-trippable file, no UI |
| **T2** | JSON UI editor | Tree navigator + key/value inspector |
| **T3** | Runtime access | `gjson` / `sjson` on `IMono8API` |

---

## 0. What already exists (and what we reuse)

| Concern | Existing code | How the editor uses it |
|---|---|---|
| Editor contract | `src/editor/IEditor.cs` (`Init` / `Update` / `Draw` / `Exit`) | `JsonEditor` implements it verbatim. |
| Editor registration | `src/Mono8API.cs:22-25` → `EditorRegistry.Register(editor, iconIndex, label)` (`src/editor/EditorRegistry.cs:13`) | One more `Register` call, icon index supplied later. |
| Global menu bar | `src/editor/EditorMenuBar.cs:28-34` — one 8×8 icon button per registered editor | Grows from 4 to 5 buttons automatically. No change needed. |
| Icon buttons | `src/editor/Button.cs` (`IsClicked`, `Draw(selected)`, `api.pal` dimming) | Reused for every icon control. |
| Shared widgets | `src/editor/EditorUI.cs` — `BottomBarY`, `Box`, `TextButton`, `CycleOnClick` | `Box`/`TextButton` for rows and actions; `CycleOnClick` for the value-type picker. |
| Toast messages | `src/editor/EventNotifier.cs` | `SAVED`, `BAD VAL`, `ERROR ON G/O/K`, `DUP KEY`, `MAX GRP`… on the bottom bar. |
| Data bank pattern | `src/core/sfx/SfxSheet.cs` — mutable bank + `LoadX(string[])` / `ToXLines()`, sole parser of its format | `JsonSheet` follows the exact same shape. |
| File IO | `src/core/common/FileIO.cs` (`Read`, `Write`, `BuildPath`) | Used **unchanged** — `data.json` is one more `data.<ext>` file. |
| Load/save wiring | `src/Mono8API.cs:30-48` (`Load`) and `:53-62` (`Save`, `Ctrl+S`) | `JsonSheet` joins both methods. |
| Mouse wheel | `src/core/input/InputBinding.cs:157-158` (`ScrollUp` / `ScrollDown`) — unused today | First real use: vertical scrolling in both panels. |
| Font / valid chars | `src/core/graphics/Text.cs:12-25` | Single source of truth for input validation (§1). |

Screen is **256×144** (`Constants.Screen`), tile/icon size **8**, the global menu bar owns
`y 0..7`, and every editor reserves the bottom 8 px (`EditorUI.BottomBarY` = 136).
Font advance is **4 px per character** (`CharWidth 5` minus the 1 px overlap at `Text.cs:100`)
and a text line is **9 px** tall.

### Removed from the previous plan

CSV editor, `CsvSheet`, `DataValueType.DateOnly`/`Date`, `FileTabBar`, indexed file IO
(`ListIndexed` / `ReadIndexed` / …), the recursive `JsonNode` tree, `MaxJsonDepth`, and the
breadcrumb/path bar. One file, fixed depth, one editor.

---

## 1. Valid characters — the constraint that drives everything

The font atlas in `src/core/graphics/Text.cs:12-25` defines exactly what can be drawn.
Anything else renders as `?` (`DefaultKey`, `Text.cs:5`). The full set is:

```
0-9  A-Z  a-z
, . : ; [ ] { } | # $ % ( ) ! ? " ' _ + - = * / \ < > ~ Ꮖ  and space
```

Two consequences that must be designed for, not discovered later:

1. **`"` and `\` are typeable** → the writer must escape them in *values*. Names (§1.3) are
   restricted so they can never need escaping.
2. **`Text.DrawText` upper-cases everything it draws** (`Text.cs:58`) → `abc` and `ABC` are
   indistinguishable on screen. Group, object and field names are identifiers, so silent
   invisible duplicates would be a data-corruption bug.
   → **Decision: upper-case-normalise all names on entry.** String/text *values* are stored
   exactly as typed (they are data, not identifiers).

### Step 1.1 — Expose the char set (`src/core/graphics/Text.cs`)

Add, without changing the existing list:

```csharp
public static bool   IsValidChar(char c);   // membership test over _charIndexes
public static string Sanitize(string s);    // drops invalid chars
public static int    Width(string s);       // s.Length * CharAdvance
public const  int    CharAdvance = 4;
public const  int    LineHeight  = 9;
```

`CharAdvance`/`LineHeight` also replace the local `const int charAdvance = 4` duplicated at
`MapEditor.cs:513` and `SpriteEditor.cs:717` — optional cleanup, same values.

### Step 1.2 — Typed-character capture (new `src/core/input/TextEntry.cs`)

Nothing in the project reads typed characters today; editors only test discrete `Keys`
(e.g. `SfxEditor.cs:269-282`). Hand-rolling shift/layout handling from `Keys` is a trap.

Use MonoGame's `GameWindow.TextInput`, subscribed once from `Mono8Game.LoadContent`
(next to `Text.GetCharacterTextures`, `Mono8Game.cs:72`):

```csharp
public static class TextEntry
{
    public static void   Attach(GameWindow window);  // window.TextInput += OnTextInput
    public static string Typed { get; }              // chars typed this frame, pre-filtered by Text.IsValidChar
    public static bool   Backspace { get; }
    public static void   Clear();                    // once per frame from InputStateManager.Update
}
```

Filtering at capture means **no editor can ever store an unrenderable character**. Per-field
rules (name charset, digits only, …) layer on top in `TextField`.

**Acceptance:** typing `@` anywhere produces nothing; typing `a` produces `a` in the buffer.

### Step 1.3 — Name rules (one rule, three uses)

Group names, object names and field (key) names all obey the **same** rule:

| Rule | Value |
|---|---|
| Max length | **7 chars** |
| Allowed | valid font chars **minus** `"` `\` `:` `,` and space |
| Normalisation | upper-cased, trimmed |
| Uniqueness | unique among its siblings (groups globally; objects within a group; fields within an object) |
| Empty | rejected |

`:` is excluded because it is the type separator in the file format (§2.2). `"` and `\` are
excluded so a name never needs JSON escaping. Implemented once as
`JsonNames.IsValidNameChar(char)` / `JsonNames.TryNormalize(string, out string)` in
`src/core/data/`.

---

# Task 1 — `data.json` load/save

## 2.1 Constants (`src/core/common/Constants.cs`)

```csharp
public static class Extensions { ...; public const string Json = "json"; }

public static class JsonData
{
    public const int MaxGroups        = 16;
    public const int MaxObjectsPerGrp = 64;
    public const int MaxFieldsPerObj  = 16;
    public const int MaxNameChars     = 7;
    public const int MaxStringChars   = 16;
    public const int MaxTextChars     = 256;
    public const int MaxArrayItems    = 64;   // see §9 — needs your call
}
```

File path is `Constants.File.Name + ".json"` → `data.json`, read/written with the **existing**
`FileIO.Read` / `FileIO.Write` / `FileIO.BuildPath`. No new IO helpers.

## 2.2 Value model (new `src/core/data/DataValue.cs`)

Seven types, no dates:

```csharp
public enum DataValueType { String, Text, Int, Decimal, Money, PosXY, Bool }
```

| Type | Code | Allowed chars | Stored / normalised form | C# runtime type (T3) |
|---|---|---|---|---|
| `String` | `s` | any valid char, ≤ 16 | as typed | `string` |
| `Text` | `t` | any valid char, ≤ 256 | as typed | `string` |
| `Int` | `i` | `0-9`, leading `-` | `int.TryParse`, no leading zeros | `int` |
| `Decimal` | `d` | `0-9`, one `.`, leading `-` | `double`, round-trip `R` format | `double` |
| `Money` | `m` | `0-9`, one `.`, leading `-`, ≤ 2 decimals | `decimal`, formatted `0.00` | `decimal` |
| `PosXY` | `p` | `0-9`, `-`, `,` (two ints) | `[x,y]` in the file, `x,y` in the UI | `(int x, int y)` |
| `Bool` | `b` | not typed — toggled | `true` / `false` | `bool` |

```csharp
public static class DataValue
{
    public static bool   IsCharAllowed(DataValueType t, char c, string current);
    public static bool   TryNormalize(DataValueType t, string raw, out string normalized);
    public static string Format(DataValueType t, string stored);   // display form
    public static int    MaxLength(DataValueType t);
    public static bool   IsQuoted(DataValueType t);                // false for i/d/m/p/b
    public static string Code(DataValueType t);                    // "s","t","i","d","m","p","b"
    public static DataValueType Parse(string code);
    public static string Default(DataValueType t);                 // "", "0", "0.00", "0,0", "false"
}
```

All parsing/formatting uses `CultureInfo.InvariantCulture` so the file stays diffable across
machines, consistent with the plain-text goal in `README.md:56`.

**Acceptance:** typing `.` twice in a Money field is rejected on the second press; committing
`5` in a Money field stores `5.00`; committing `x` in an Int field is rejected and the field
keeps its previous value.

## 2.3 Data model (new `src/core/data/JsonSheet.cs`)

Fixed three levels — root → group → object → field. No recursion, no `JsonNode`.

```csharp
internal sealed class JsonField
{
    public string        Name;      // ≤ 8, normalised
    public DataValueType Type;
    public bool          IsArray;
    public List<string>  Values;    // 1 entry when !IsArray; normalised strings
}

internal sealed class JsonObject { public string Name; public List<JsonField> Fields; }
internal sealed class JsonGroup  { public string Name; public List<JsonObject> Objects; public bool Collapsed; }

internal sealed class JsonSheet
{
    public IReadOnlyList<JsonGroup> Groups { get; }
    public bool IsDirty { get; }

    public void Load(string path);          // FileIO.Read + parse; missing/garbled → empty
    public void Save(string path);          // FileIO.Write(ToJson())
    public string ToJson();                 // pretty-printed, 2-space indent

    public bool TryAddGroup(string name, out JsonGroup g);        // MaxGroups + dup check
    public bool TryAddObject(JsonGroup g, string name, out JsonObject o);
    public bool TryAddField(JsonObject o, string name, DataValueType t, bool isArray, out JsonField f);
    public bool TryRename(object node, string name);              // group/object/field
    public void Remove(object node);

    public bool TrySetValue(JsonField f, int index, string raw);  // validates via DataValue
    public bool TryAddItem(JsonField f);                          // arrays only, MaxArrayItems
    public void RemoveItem(JsonField f, int index);
}
```

`Collapsed` is UI state living on the model for convenience; it is **not** persisted.

Ordering is insertion order (`List`, never `Dictionary`) so the file diffs stably.

### File format

Field type is encoded as a **key suffix** — `"NAME:code"`. This is deterministic, whereas
sniffing the JSON value cannot distinguish `Decimal` from `Money`, `String` from `Text`, or
`PosXY` from a 2-item int array.

```json
{
  "ENEMY": {
    "SLIME": {
      "HP:i": 12,
      "SPD:d": 1.25,
      "COST:m": "3.50",
      "SPAWN:p": [40, 88],
      "BOSS:b": false,
      "NAME:s": "Green slime",
      "DESC:t": "Splits in two when hit by fire.",
      "DROPS:i": [1, 4, 7],
      "WAYPTS:p": [[8, 8], [8, 40]]
    },
    "BAT": { "HP:i": 6 }
  },
  "LEVEL": {
    "L1": { "TIME:i": 90 }
  }
}
```

- Arrays are homogeneous and hold the field's declared type. A single value is written bare;
  an array is written as a JSON array. `PosXY` is `[x,y]`, so an array of `PosXY` is
  `[[x,y],[x,y]]` — the only nested array in the format.
- `Money` is quoted to protect its trailing zeros (`"3.50"`, not `3.5`).
- Depth is fixed and validated: anything deeper or shallower than group→object→field is
  dropped on load.

### Parser / serializer

Hand-rolled recursive-descent reader in `JsonSheet`, no `System.Text.Json` dependency — the
writer stays inside the font's character set and the format stays diffable.

Defensive loading, mirroring `SfxSheet.LoadSfxs` (`SfxSheet.cs:74-79`): a missing file, a
malformed file, an unknown type code, a name that fails §1.3, a duplicate name, or a count
over the limits **drops that node and keeps going** — never throws. Characters outside the
font set are replaced via `Text.Sanitize` on load and the sheet is marked dirty so the
sanitised form is what gets saved back. Dropped nodes raise one `LOAD FIX` toast.

## 2.4 Wiring (`src/Mono8API.cs`)

- `public static JsonSheet JsonSheet` alongside `SpriteSheet`, `SfxSheet` etc. (`:7-11`).
- `Load()` → `JsonSheet.Load(path)` after the existing sheets (`:30-48`).
- `Save()` → `JsonSheet.Save(path)` (`:53-62`), so `Ctrl+S` in *any* editor persists it,
  matching `README.md:56`.
- New folder `src/core/data/`, namespace `mono8.core.data`, added to `src/GlobalUsings.cs`.

**T1 acceptance:**
- The sample file above loads, and `ToJson()` on an untouched sheet is byte-identical.
- Deleting `data.json` and loading yields an empty sheet, not an exception.
- A hand-written `"HP:z": 1`, a 9-char name, a duplicate key and a 17th group are each
  dropped without killing the load.
- A string value containing `a"b\c` survives save → reload.

---

# Task 2 — JSON UI editor

## 3.1 Layout (`src/editor/JsonEditor.cs`)

No file tab bar (single file) and no breadcrumb bar (fixed depth). The global
`EditorMenuBar` at `y 0..7` stays — it is how you leave this editor (§9, Q1).

```
y   0..7    global EditorMenuBar (existing, unchanged)
y   8..125  x   0..39   TREE PANEL   — groups + objects, 13 rows of 9 px
            x  40..43   tree vertical scrollbar
            x  44       1 px divider (DarkGray)
            x  46..247  INSPECTOR    — key/value rows of the selected object
            x 252..255  inspector vertical scrollbar
y 126..135  action row — context-sensitive, follows the focused panel
y 136..143  bottom bar + EventNotifier toasts
```

118 px of content ÷ 9 px = **13 visible rows** per panel.

## 3.2 Tree panel (left)

Flat two-level list, **no indentation** — a group row and an object row start their text at
the same `x`. They are told apart by the fold marker and the colour:

```
x  0..3    fold marker: '+' / '-' on group rows, blank on object rows
x  4..31   name, 7 chars × 4 px = 28 px
```

| Row | Marker | Text colour |
|---|---|---|
| Group | `+` collapsed / `-` expanded | `White` |
| Object | none | `LightGray` |
| Selected (either) | — | `Indigo` on a `White` `EditorUI.Box` |

- **Collapse/expand**: click the marker column, or press `Left`/`Right` on a selected group.
  A collapsed group hides its object rows; `Collapsed` is session-only state.
- **Selection**: click a row, or `Up`/`Down`. Clicking an **object** row is what repaints the
  inspector. Clicking a group row selects it for rename/delete and does not clear the
  inspector.
- **Scrolling**: `_scrollRow` in whole rows over the flattened visible-row list
  (16 groups + up to 1024 objects = 1040 rows worst case), mouse wheel via
  `InputBinding.ScrollUp/ScrollDown`, plus the `ScrollBar` widget (§5.2), plus
  `EnsureVisible()` on selection change — same idea as `MusicEditor.EnsureCellVisible`.
- **Rename**: `Enter`, `R`, `[REN]` or a double click on the row's name opens a `TextField`
  (§5.3) in place, gated by §1.3.
- **Create/delete**: `[+GRP]` appends a group; `[+OBJ]` appends an object to the selected
  group (or to the parent group of the selected object); `[DEL]` removes the selected node
  after a `HOLD DEL` confirm-by-second-press (no modal dialogs exist in the project).
  Default names are the first free `G1..G16`, `O1..O64`, so a new node is never invalid.
- Limits produce `MAX GRP` / `MAX OBJ` toasts; a duplicate rename produces `DUP NAME`.

## 3.3 Inspector (right)

The selected object's fields, one field per **row block**. Single-level by construction: a
value is either one scalar or an array of scalars, never another object.

```
x  46..73   key name (7 chars, 28 px)
x  79..83   type badge — 1 char (s t i d m p b), click to cycle via EditorUI.CycleOnClick
x  86..247  value area — 162 px = 40 characters per line
```

**Row heights** — this is the "fits on the same line" rule made concrete:

| Type | Rendering |
|---|---|
| `Int`, `Decimal`, `Money`, `Bool` | one 9 px line, value on the key's line |
| `PosXY` | one line, two small fields `x , y` |
| `String` (≤16) | one line, always fits in 40 chars |
| `Text` (≤256) | **starts on the key's line**, wraps at 40 chars into as many extra 9 px lines as needed (≤ 7) |

Wrapping is word-wrap with hard-break fallback for a 40+ char run. The block height is
`1 + ceil((len - 40) / 40)` lines for `Text`, 1 otherwise.

- `Bool` draws as a `[TRUE]`/`[FALSE]` `EditorUI.TextButton` — click toggles, no text entry.
- **Arrays**: the badge shows `[n]` after the type code. The scalar row shows item `0`;
  items `1..n-1` follow on their own rows labelled `1:`, `2:` … at `x 86`, each sized by the
  same table. `[+ITM]` appends, `[-ITM]` removes the selected item.
- **Editing**: click a value or press `Enter` → `TextField` gated by
  `DataValue.IsCharAllowed(type, …)`; `Esc` cancels, `Enter` commits, `Tab` commits and moves
  to the next row.
- **Changing a type** re-validates every item. A value that no longer fits is **kept and drawn
  in `Red`**, never silently dropped, and blocks the save with an `ERROR ON GROUP/OBJECT/KEY` toast.
- **Renaming a key**: `R`, `[REN]` or a double click on the key name — a single click there
  only selects, so the second one opens the `TextField`.
- **Create/delete**: `[+KEY]` prompts for a name (validated + dup-checked → `DUP KEY`) and
  creates it as `String` with `DataValue.Default`; `[DEL]` removes the selected field;
  `[ARR]` toggles the selected field between scalar and array (collapsing to array keeps
  item 0 only, with a `KEEP 1` toast).
- **Scrolling**: `_scrollPx` in **pixels**, not rows, because row blocks have varying height.

## 3.4 Action row (`y 126..135`)

Context-sensitive on the focused panel, built from `EditorUI.TextButton`:

```
tree focused       [+GRP] [+OBJ] [REN] [DEL]
inspector focused  [+KEY] [REN] [DEL] [ARR] [+ITM] [-ITM]
```

`Ctrl+S` → `Mono8Game.GameAPI.Save()` + `eventNotifier.AddEvent("SAVED")`, exactly as
`MusicEditor.cs:112-116`. `Tab` moves focus between panels. `R` is `[REN]`'s shortcut in both
panels — bare only, since `Ctrl+R` runs the game.

## 3.5 Shared widgets

### `src/editor/ScrollBar.cs` (new)

No scrollbar exists in the project today. One widget, used by both panels:

```csharp
internal sealed class ScrollBar
{
    public ScrollBar(IMono8API api, Rectangle track);
    public bool Update((int x, int y) mouse, int contentSize, int viewSize, ref int offset);
    public void Draw(int contentSize, int viewSize, int offset);
}
```

4 px wide, `DarkGray` track / `LightGray` thumb, thumb length proportional to
`viewSize / contentSize`, minimum 6 px. Vertical only — nothing scrolls horizontally.

### `src/editor/TextField.cs` (new)

Inline single-line editor: draws text plus a blinking caret inside a `Rectangle`, consumes
`TextEntry.Typed`, gates each character through `DataValue.IsCharAllowed` (values) or
`JsonNames.IsValidNameChar` (names), honours backspace, commits on `Enter`, cancels on `Esc`.

```csharp
internal sealed class TextField
{
    public void Begin(Rectangle bounds, string initial, DataValueType type, int maxLength);
    public void BeginName(Rectangle bounds, string initial);   // §1.3 rules, 7 chars
    public bool Active { get; }
    public bool Update(out string committed, out bool cancelled);
    public void Draw();
}
```

This is where "the user can only type valid chars" is actually enforced per field.

## 3.6 Registration (`src/Mono8API.cs`)

```csharp
Editors.Register(new JsonEditor(this), JsonEditorIcon, "Json");
```

**Icon index supplied later** — declare `JsonEditorIcon` as a named constant next to the
existing literals `15, 16, 17, 18` (`Mono8API.cs:22-25`) with a
`// TODO: replace with the provided icon index` comment, so swapping it in is a one-line
change. No change to `data.icons` is part of this plan.

`TextEntry.Attach(Window);` goes next to `Text.GetCharacterTextures` (`Mono8Game.cs:72`), and
`TextEntry.Clear()` at the end of `InputStateManager.Update()`.

**T2 acceptance:**
- 16 groups × 64 objects scroll smoothly; wheel, drag and arrow keys agree on position.
- Collapsing a group hides exactly its objects and nothing else.
- A 256-char `Text` value wraps to 7 lines and the rows below it move down accordingly.
- `@`, `:` and a 9th character are all impossible to type into a name.
- Switching a field from `Int` to `Bool` flags the bad value in `Red` instead of erasing it.
- `Ctrl+S` shows `SAVED` and the file on disk is diff-clean when nothing changed.

---

# Task 3 — runtime access (`gjson` / `sjson`)

Depends on T1 only. `IMono8API` (`src/IMono8API.cs`) exposes the authored data to `YourGame`.

## 4.1 No boxing — how

The requirement is that nothing crosses the API as `object`. `JsonSheet` stores strings for
the editor's benefit, so T3 compiles a **typed snapshot** once, at the end of `Load()` and
after every `Save()` (worst case 16 × 64 × 16 = 16 384 fields — microseconds):

```csharp
public sealed class Mono8JsonObject
{
    // parallel typed storage, no object[] anywhere
    private readonly int[]     _ints;
    private readonly double[]  _decs;
    private readonly decimal[] _moneys;
    private readonly bool[]    _bools;
    private readonly string[]  _strs;
    private readonly int[]     _xy;      // 2 slots per PosXY item
    private readonly Dictionary<string, Slot> _index;   // name → (type, start, count)

    public bool          Has(string field);
    public DataValueType TypeOf(string field);
    public bool          IsArray(string field);
    public int           Count(string field);           // 1 for scalars, 0 when absent

    public int        GetInt  (string field, int i = 0, int      fallback = 0);
    public double     GetDec  (string field, int i = 0, double   fallback = 0);
    public decimal    GetMoney(string field, int i = 0, decimal  fallback = 0);
    public bool       GetBool (string field, int i = 0, bool     fallback = false);
    public string     GetStr  (string field, int i = 0, string   fallback = "");
    public (int x, int y) GetXY(string field, int i = 0);

    public ReadOnlySpan<int>     IntArray(string field);    // zero-alloc views
    public ReadOnlySpan<double>  DecArray(string field);
    public ReadOnlySpan<bool>    BoolArray(string field);
}
```

`Slot` is a `readonly struct (DataValueType Type, int Start, int Count)` — a struct value in
a `Dictionary`, so no boxing on lookup either. A type mismatch (`GetInt` on a `Bool` field)
returns the fallback and raises one `ErrorHandler` warning rather than throwing, consistent
with the engine's forgiving-runtime posture.

## 4.2 API surface (`src/IMono8API.cs`)

```csharp
// read a whole object
Mono8JsonObject gjson(string group, string obj);       // null when not found

// write one field
bool sjson(string group, string obj, string field, int     value, int index = 0);
bool sjson(string group, string obj, string field, double  value, int index = 0);
bool sjson(string group, string obj, string field, decimal value, int index = 0);
bool sjson(string group, string obj, string field, bool    value, int index = 0);
bool sjson(string group, string obj, string field, string  value, int index = 0);
bool sjson(string group, string obj, string field, int x, int y,  int index = 0);
```

- One overload per valid value type — the compiler picks the right one and nothing boxes.
- Returns `false` when the group/object/field is missing, the index is out of range, or the
  overload does not match the field's declared type. Never throws, never creates fields.
- Writes hit the typed snapshot **in memory only**. `data.json` is authored by the editor;
  a running game does not rewrite its own cartridge data (§9, Q2).

Lookup is `Dictionary<string, Dictionary<string, Mono8JsonObject>>` built at load — O(1),
allocation-free at call time. Names are upper-cased on lookup so `gjson("enemy","slime")`
and `gjson("ENEMY","SLIME")` both resolve.

## 4.3 Lua binding

NLua marshals `int`, `double`, `bool` and `string` natively. Two notes:

- `decimal` becomes a Lua number (double) — precision beyond 15 digits is lost, which no
  money value in an 8-char-key fantasy console will ever reach.
- `(int x, int y)` cannot cross to Lua as a tuple. Bind `GetXY` as a two-return-value Lua
  function (`local x, y = o:GetXY("SPAWN")`), the same shape the mouse API already uses.

Overload resolution from Lua is fragile (every Lua number arrives as a double), so the Lua
side gets explicit names rather than relying on NLua to pick: `sjson_i`, `sjson_d`,
`sjson_m`, `sjson_b`, `sjson_s`, `sjson_p`. `IMono8API` keeps the clean C# overloads.

**T3 acceptance:**
- `gjson("ENEMY","SLIME").GetInt("HP")` returns `12` on the sample file.
- `gjson("ENEMY","NOPE")` returns `null`; `GetInt` on a missing field returns the fallback.
- `sjson("ENEMY","SLIME","HP", 20)` returns `true`; `sjson(..., "HP", true)` returns `false`.
- `IntArray("DROPS")` yields `[1,4,7]` with no allocation (verify with an allocation profiler
  or a simple GC-count assertion).

---

## 5. Build order

Each phase compiles and is independently verifiable.

| # | Task | Phase | Files |
|---|---|---|---|
| 1 | T1 | Char set helpers + name rules | `Text.cs`, new `core/data/JsonNames.cs` |
| 2 | T1 | Value model | new `core/data/DataValue.cs`, `Constants.cs`, `GlobalUsings.cs` |
| 3 | T1 | Sheet + parser + serializer | new `core/data/JsonSheet.cs` |
| 4 | T1 | Load/save wiring | `Mono8API.cs` |
| 5 | T2 | Text input | new `core/input/TextEntry.cs`, `Mono8Game.cs`, `InputStateManager.cs` |
| 6 | T2 | Widgets | new `editor/ScrollBar.cs`, `editor/TextField.cs` |
| 7 | T2 | Editor UI | new `editor/JsonEditor.cs`, `Mono8API.cs` |
| 8 | T3 | Typed snapshot | new `core/data/Mono8JsonObject.cs`, `JsonRuntime.cs` |
| 9 | T3 | API + Lua binding | `IMono8API.cs`, `Mono8API.cs`, NLua registration |
| 10 | — | Docs | `README.md` |

Docs: `## Editors` (line 43-45) "four editors" → "five"; `## Project Data` table (lines 58-67)
gains a `data.json` row; a new `## JSON Editor` section with a hotkey table following the
shape of the existing `### SFX Editor Hotkeys` section; and a `## JSON API` section for
`gjson`/`sjson`.

---

## 6. Manual test checklist

**Model / file**
- [ ] Missing, empty and malformed `data.json` all load as an empty sheet.
- [ ] A value containing `"` and `\` survives save → reload.
- [ ] `Money` keeps `3.50` (not `3.5`) across a round trip.
- [ ] `PosXY` and an array of `PosXY` both round-trip.
- [ ] The 17th group, the 65th object and the 17th key are refused with a toast.

**Editor**
- [ ] Tree scrolls past 13 rows; collapse/expand is correct at the top and bottom of the list.
- [ ] Rename, create and delete work for groups, objects and keys.
- [ ] Clicking an object repaints the inspector; clicking a group does not.
- [ ] A `Text` value of 256 chars wraps and stays fully editable by scrolling.
- [ ] Every scalar type edits inline on the key's line.
- [ ] Array add/remove keeps the remaining items and their order.
- [ ] `Esc` cancels an edit and restores the previous value.

**Both / integration**
- [ ] Switching editors and back preserves selection and scroll.
- [ ] `Ctrl+R` (run game) and `Esc` still work while the editor is active.
- [ ] An exception inside the editor is caught by `ErrorHandler` (`Mono8API.cs:110`).
- [ ] `gjson` sees a value edited in the editor after `Ctrl+S` without restarting.

---

## 7. Decisions taken

1. **Type in the key suffix** (`"HP:i"`) rather than sniffing values — sniffing cannot tell
   `Decimal` from `Money`, `String` from `Text`, or `PosXY` from a 2-int array.
2. **Names upper-cased**, values stored as typed — the font renders everything upper-case, so
   invisible duplicate identifiers would be a data-corruption bug.
3. **No indentation in the tree**, per the requirement; groups and objects are distinguished
   by the fold marker and text colour instead.
4. **Split panels, not drill-in.** The previous plan drilled in because the tree was
   arbitrarily deep; at a fixed 2 levels with 8-char names, a 40 px tree panel leaves 202 px
   for the inspector — enough for a 40-char value column.
5. **Inspector scrolls in pixels**, not rows, because `Text` blocks have variable height.
6. **Arrays are homogeneous** in the field's declared type — required for the zero-boxing
   typed storage in T3.
7. **Pretty-printed, insertion-ordered JSON** for readable diffs, consistent with the
   project's plain-text data story.

## 8. Explicitly out of scope

- Undo/redo (no editor in the project has it today).
- Copy/paste of objects or key blocks.
- Runtime creation/deletion of groups, objects or fields — `sjson` only writes existing ones.
- Persisting runtime `sjson` writes back to `data.json`.
- Supplying the actual icon artwork for `data.icons`.

## 9. Open questions — please confirm

1. **Global menu bar.** "No more top bar to navigate" is read as *the per-file tab bar is
   gone* (there is one file now). The 8 px `EditorMenuBar` at `y 0..7` is kept, since it is
   the only way to switch to the sprite/map/sfx/music editors. Say the word and the editor
   goes full-height `y 0..135`, but then it needs its own exit key.
2. **`sjson` signature.** Yours lists three arguments and no value, so it is read as a
   **setter** with the value implied: `sjson(group, obj, field, value)`, one overload per
   type (§4.2). If you actually meant a *typed single-field getter*, C# cannot overload on
   return type alone — it would become `gjson_i(g,o,f)`, `gjson_s(g,o,f)`, … instead. One
   line to change either way.
3. **`Decimal` → `double` or `float`?** `double` is planned. The engine is mostly `float`;
   if you want `float` for consistency with the graphics API, say so before T3.
4. **`Money` → `decimal` or `long` cents?** `decimal` is planned (exact, formats as `0.00`).
   `long` cents is faster and crosses to Lua losslessly if that matters more.
5. **Array cap.** `MaxArrayItems = 64` is a guess — nothing in your constraints bounds array
   length, but an unbounded array makes the inspector's row budget unbounded too.
