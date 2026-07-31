# Plan — CSV ("Excel") and JSON editors

Implementation plan for two new built-in editors, plus the shared file-IO and data-model
layer they sit on. Written against the code as it exists today; every reference below points
at the file and line that establishes the convention being followed.

---

## 0. What already exists (and what we reuse)

| Concern | Existing code | How the new editors use it |
|---|---|---|
| Editor contract | `src/editor/IEditor.cs` (`Init` / `Update` / `Draw` / `Exit`) | `CsvEditor` and `JsonEditor` implement it verbatim. |
| Editor registration | `src/Mono8API.cs:22-25` → `EditorRegistry.Register(editor, iconIndex, label)` (`src/editor/EditorRegistry.cs:13`) | Two more `Register` calls with the icon indexes supplied later. |
| Top menu bar | `src/editor/EditorMenuBar.cs:28-34` lays out one 8×8 icon button per registered editor, right-aligned | Grows from 4 to 6 buttons automatically (48 px of the 256 px bar). No change needed. |
| Icon buttons | `src/editor/Button.cs` (`IsClicked`, `Draw(selected)`, `api.pal` dimming when unselected) | Reused for every icon control in both editors. |
| Shared widgets | `src/editor/EditorUI.cs` — `BottomBarY`, `Box`, `TextButton`, `CycleOnClick` | `Box`/`TextButton` for cells and fields; `CycleOnClick` for the column-type picker. |
| Index strip / paging | `src/editor/SpriteNavigator.cs:117-128` (page buttons) and `src/editor/MusicEditor.cs:105` (`PatBox` pattern strip with `viewStart`) | The new `FileTabBar` is modelled directly on the Music pattern strip. |
| Toast messages | `src/editor/EventNotifier.cs` | `"SAVED"`, `"BAD VALUE"`, `"DUP KEY"` feedback on the bottom bar. |
| Data bank pattern | `src/core/sfx/SfxSheet.cs` — mutable bank + `LoadX(string[])` / `ToXLines()`, the *sole* parser of its format | `CsvSheet` and `JsonSheet` follow the exact same shape. |
| File IO | `src/core/common/FileIO.cs` (`Read`, `Write`, `BuildPath`, `SplitData`) | Extended with index-suffixed helpers (§2). |
| Load/save wiring | `src/Mono8API.cs:30-48` (`Load`) and `:53-62` (`Save`, bound to `Ctrl+S` in each editor) | Both new formats join both methods. |
| Mouse wheel | `src/core/input/InputBinding.cs:157-158` (`ScrollUp` / `ScrollDown`) — currently unused by any editor | First real use: vertical scrolling in both editors. |
| Font / valid chars | `src/core/graphics/Text.cs:12-25` | The single source of truth for input validation (§1). |

Screen is **256×144** (`Constants.Screen`), tile/icon size **8**, the global menu bar owns
`y 0..7`, and every editor reserves the bottom 8 px (`EditorUI.BottomBarY` = 136).
Font advance is **4 px per character** (`CharWidth 5` minus the 1 px overlap at
`Text.cs:100`) and a text line is **9 px** tall.

---

## 1. Valid characters — the constraint that drives everything

The font atlas in `src/core/graphics/Text.cs:12-25` defines exactly what can be drawn.
Anything else renders as `?` (`DefaultKey`, `Text.cs:5`). The full set is:

```
0-9  A-Z  a-z
, . : ; [ ] { } | # $ % ( ) ! ? " ' _ + - = * / \ < > ~ Ꮖ  and space
```

Notable **absences**: `@`, `&`, `^`, backtick, and any newline/tab inside a value
(`\t` and `\r` are consumed as layout control at `Text.cs:83-92`).

Three consequences that must be designed for, not discovered later:

1. **`,` and `"` are typeable** → the CSV writer must RFC-4180 quote (wrap in `"`, double
   inner `"`). A naive `string.Join(",")` corrupts data the user can legitimately enter.
2. **`"` and `\` are typeable** → the JSON writer must escape them. Restrict *keys* further
   (§4) so a key can never need escaping.
3. **`Text.DrawText` upper-cases everything it draws** (`Text.cs:58`). `abc` and `ABC` are
   indistinguishable on screen. Since JSON keys and CSV headers are identifiers, silent
   invisible duplicates are a real hazard.
   → **Decision: upper-case-normalise CSV column names and JSON keys on entry.** Cell values
   and JSON string values are stored exactly as typed (they are data, not identifiers).

### Step 1.1 — Expose the char set (`src/core/graphics/Text.cs`)

Add, without changing the existing list:

```csharp
public static bool IsValidChar(char c);              // membership test over _charIndexes
public static string Sanitize(string s);             // drops invalid chars
public static int Width(string s);                   // s.Length * 4, for layout maths
public const int CharAdvance = 4;
public const int LineHeight = 9;
```

`CharAdvance`/`LineHeight` also replace the local `const int charAdvance = 4` duplicated at
`MapEditor.cs:513` and `SpriteEditor.cs:717` — optional cleanup, same values.

### Step 1.2 — Typed-character capture (new `src/core/input/TextEntry.cs`)

Nothing in the project reads typed characters today; editors only test discrete `Keys`
(e.g. `SfxEditor.cs:269-282`). Hand-rolling shift/layout handling from `Keys` is a trap.

Use MonoGame's `GameWindow.TextInput` event, subscribed once from `Mono8Game.LoadContent`
(next to `Text.GetCharacterTextures`, `Mono8Game.cs:72`):

```csharp
public static class TextEntry
{
    public static void Attach(GameWindow window);   // window.TextInput += OnTextInput
    public static string Typed { get; }             // chars typed this frame, pre-filtered by Text.IsValidChar
    public static bool Backspace { get; }
    public static void Clear();                     // called once per frame from InputStateManager.Update
}
```

Filtering at capture means **no editor can ever store an unrenderable character** — the
"only valid chars" requirement is enforced in one place rather than at every call site.
Per-field rules (digits only, etc.) layer on top in `TextField`.

**Acceptance:** typing `@` anywhere produces nothing; typing `a` produces `a` in the buffer.

---

## 2. File IO layer — indexed data files

Today every data file is `data.<ext>` (`Constants.File.Name` = `"data"`, `Constants.cs:72`)
and is read via `FileIO.Read(name, ext, path)`. CSV and JSON differ: there are **N of each**,
named `data1.csv`, `data2.csv`, … and `data1.json`, `data2.json`, … (1-based, matching the
requirement). The tab bars address them by that index.

### Step 2.1 — Constants (`src/core/common/Constants.cs`)

```csharp
public static class Extensions { ...; public const string Csv = "csv"; public const string Json = "json"; }

public static class DataFiles          // inside GameDataSizes or its own nested class
{
    public const int MaxFiles   = 32;  // per format; caps the tab bar
    public const int CsvRows    = 256; // indexes 0..255
    public const int CsvColumns = 8;
    public const int MaxCellChars = 64;   // string / int / money / dates
    public const int MaxTextChars = 255;  // "text" type
    public const int MaxKeyChars  = 32;   // JSON keys
    public const int MaxJsonDepth = 16;   // guard against runaway nesting / cyclic writes
}
```

### Step 2.2 — Indexed helpers (`src/core/common/FileIO.cs`)

Add alongside the existing methods, keeping the same swallow-errors-return-empty style
(`FileIO.cs:16-19`):

```csharp
public static int[] ListIndexed(string extension, string path);        // scans data*.<ext>, returns sorted indexes
public static string ReadIndexed(int index, string extension, string path);
public static void   WriteIndexed(int index, string extension, string content, string path);
public static void   DeleteIndexed(int index, string extension, string path);
```

Naming is `Constants.File.Name + index` — i.e. `BuildPath($"data{index}", ext, path)`, so
`BuildPath` is reused unchanged. `ListIndexed` uses `Directory.GetFiles(path, "data*.<ext>")`
and parses the numeric suffix, ignoring anything that does not parse.

**Acceptance:** with `data1.csv` and `data3.csv` on disk, `ListIndexed` returns `[1, 3]`;
a hand-written `datafoo.csv` is ignored rather than throwing.

### Step 2.3 — Shared value model (new `src/core/data/DataValue.cs`)

The six cell types are shared by **both** editors — CSV columns and JSON leaf values use the
same enum and the same validators. This is the main piece of reuse between them.

```csharp
public enum DataValueType { String, Text, Int, Money, DateOnly, Date }

public static class DataValue
{
    public static bool   IsCharAllowed(DataValueType t, char c, string current);  // per-keystroke gate
    public static bool   TryNormalize(DataValueType t, string raw, out string normalized);
    public static string Format(DataValueType t, string stored);                  // display form
    public static int    MaxLength(DataValueType t);
    public static string Code(DataValueType t);                                   // "s","t","i","m","d","dt" for file headers
    public static DataValueType Parse(string code);
}
```

Rules:

| Type | Allowed chars | Normalised form |
|---|---|---|
| `String` | any valid char, ≤ 64 | as typed, trimmed |
| `Text` | any valid char, ≤ 255 | as typed |
| `Int` | `0-9` and a leading `-` | `int.TryParse`, re-emitted without leading zeros |
| `Money` | `0-9`, one `.`, leading `-`, ≤ 2 decimals | `decimal`, formatted `0.00` (invariant culture) |
| `DateOnly` | `0-9` and `-` | `yyyy-MM-dd`, `DateOnly.TryParseExact` |
| `Date` | `0-9`, `-`, `:`, space | `yyyy-MM-dd HH:mm:ss`, `DateTime.TryParseExact` |

All parsing/formatting uses `CultureInfo.InvariantCulture` so files stay diffable across
machines (consistent with the plain-text, commit-friendly goal stated in `README.md:56`).

**Acceptance:** typing `.` twice in a Money cell is rejected on the second press; committing
`5` in a Money cell stores `5.00`; committing `2026-13-01` in a DateOnly cell is rejected and
the cell keeps its previous value.

### Step 2.4 — Namespace

New folder `src/core/data/`, namespace `mono8.core.data`, added to
`src/GlobalUsings.cs` next to the other `mono8.core.*` usings.

---

## 3. CSV data model + editor

### Step 3.1 — `src/core/data/CsvSheet.cs` (logic only, no drawing)

Modelled on `SfxSheet` — mutable bank, sole parser of its format:

```csharp
internal sealed class CsvSheet
{
    public IReadOnlyList<int> Indexes { get; }          // e.g. [1,2,3]
    public int Count { get; }

    public void LoadAll(string path);                   // ListIndexed + parse each
    public void SaveAll(string path);                   // writes only dirty tables
    public int  AddTable();                             // next free index, returns it
    public void RemoveTable(int index);                 // + DeleteIndexed

    public string GetColumnName(int file, int col);
    public void   SetColumnName(int file, int col, string name);   // upper-cased, sanitized
    public DataValueType GetColumnType(int file, int col);
    public void   SetColumnType(int file, int col, DataValueType t);

    public string GetCell(int file, int row, int col);
    public bool   TrySetCell(int file, int row, int col, string raw);  // validates via DataValue
    public int    LongestCellLength(int file, int col);                // drives auto-width
    public bool   IsDirty(int file);
}
```

Backing store: `string[file][row, col]` with `CsvRows`×`CsvColumns` fixed at 256×8, so rows
0..255 always exist and the grid never needs insert/delete logic. An empty row is simply
8 empty strings and is written out as a bare `,,,,,,,` line — round-trips cleanly.

**File format** (`data1.csv`):

```
line 0 : header — 8 fields of "NAME:type" (e.g. NAME:s,QTY:i,PRICE:m,DUE:d)
line 1..256 : one line per row index 0..255, 8 fields
```

Fields are quoted per RFC 4180 when they contain `,`, `"` or a leading/trailing space.
The parser tolerates short lines (missing trailing fields → empty), extra fields (dropped),
and a missing/garbled header (falls back to `A..H` / `String`), mirroring the defensive
loading in `SfxSheet.LoadSfxs` (`SfxSheet.cs:74-79`).

**Acceptance:** a table containing the cell `a,b"c` saves and reloads byte-identical in
value; deleting `data2.csv` from disk and reloading leaves indexes `[1,3]` intact.

### Step 3.2 — `src/editor/CsvEditor.cs` (UI)

Layout inside the 256×144 screen (menu bar owns `y 0..7`, bottom bar starts at 136):

```
y   0..7    global EditorMenuBar (existing, unchanged)
y   8..15   FileTabBar  — [<] [1][2][3]… [>]  [+] [-]        (§5.1)
y  16..24   column header row — 8 boxes: NAME + type badge
y  25..123  grid viewport — 11 visible rows of 9 px
            x   0..13   row-index gutter (3 digits, right-aligned)
            x  14..251  scrollable column area
            x 252..255  vertical scrollbar
y 125..128  horizontal scrollbar
y 129..135  status line: "R012 C3 MONEY" + EventNotifier toasts
y 136..143  bottom bar
```

- **Auto width per column**: `max(len(name)+2, LongestCellLength) * Text.CharAdvance + 3`,
  clamped to `[24, 160]` px. Recomputed on commit, not per frame — cache an
  `int[8] _colWidth` and invalidate on edit/file switch.
- **Horizontal scroll**: `_scrollX` in pixels over the summed column widths; drag the
  scrollbar or press `Left`/`Right` past the viewport edge to follow the selection.
- **Vertical scroll**: `_scrollRow` in whole rows, mouse wheel via `InputBinding.ScrollUp/Down`,
  `EnsureCellVisible()` on selection change — same idea as `MusicEditor.EnsureCellVisible`.
- **Selection**: click a cell to select, arrows to move, `Enter`/typing starts editing,
  `Esc` cancels, `Tab` commits and moves right, `Delete` clears the cell.
- **Header editing**: click a header's name area → `TextField` for the name (upper-cased);
  right-click / click the type badge → `EditorUI.CycleOnClick` through the 6 types.
  Changing a type re-validates the column and marks offending cells (drawn in `Red`)
  rather than destroying them.
- **Colours**: reuse the existing editor palette — `LightGray` box on `Indigo` text
  (`EditorUI.TextButton`), `Orange` bars, `White` selection outline as in
  `SpriteNavigator.DrawSheet` (`SpriteNavigator.cs:113-114`).
- `Ctrl+S` → `Mono8Game.GameAPI.Save()` + `eventNotifier.AddEvent("SAVED")`, exactly as
  `MusicEditor.cs:112-116`.

**Acceptance:** 256 rows reachable by scrolling; a column whose longest cell is 20 chars
becomes ~83 px wide and pushes later columns off-screen behind the horizontal scrollbar;
typing `x` into an `Int` column produces nothing.

---

## 4. JSON data model + editor

### Step 4.1 — `src/core/data/JsonNode.cs`

Hand-rolled tree — no `System.Text.Json` dependency, so the writer stays inside the font's
character set and the format stays diffable.

```csharp
internal enum JsonKind { Value, Object, Array }

internal sealed class JsonNode
{
    public JsonKind Kind;
    public DataValueType ValueType;      // when Kind == Value — reuses §2.3
    public string Value;                 // stored, unescaped
    public List<string>   Keys;          // when Object (ordered, so files diff stably)
    public List<JsonNode> Children;      // Object values or Array items

    public JsonNode Get(string key); public JsonNode Get(int i);
    public bool TryAddKey(string key, JsonNode child);   // rejects duplicate/invalid keys
    public void RemoveAt(int i); public void Rename(int i, string key);
}
```

- **Serializer**: 2-space-indented pretty print (readable diffs), `"` and `\` escaped,
  numbers/dates emitted per `DataValue.Format` — `Int`/`Money` unquoted, everything else
  quoted. Depth-capped at `MaxJsonDepth`.
- **Parser**: small recursive-descent reader accepting standard JSON. Values are typed on
  load by sniffing: unquoted integer → `Int`, unquoted decimal → `Money`, quoted matching
  `yyyy-MM-dd` → `DateOnly`, `yyyy-MM-dd HH:mm:ss` → `Date`, else `String`
  (>64 chars → `Text`). Characters outside the font set are replaced via `Text.Sanitize`
  on load, and the file is marked dirty so the sanitised form is what gets saved back.
- **Key rules**: non-empty, ≤ `MaxKeyChars`, upper-cased, valid chars **minus** `"` `\` and
  leading/trailing space, unique within its object. A key can therefore never need escaping.

### Step 4.2 — `src/core/data/JsonSheet.cs`

Same bank shape as `CsvSheet`: `LoadAll` / `SaveAll` / `AddDocument` / `RemoveDocument` /
`Root(int file)` / `IsDirty(int file)`. A missing or unparseable file yields an empty
root object `{}` rather than throwing — the editor must always have something to draw.

**Acceptance:** `{"A":{"B":[1,"x"]}}` loads, and re-saving without edits produces the same
tree on the next load.

### Step 4.3 — `src/editor/JsonEditor.cs` (UI)

**Design decision — drill-in, not split-pane.** At 256×144 a tree pane plus a property pane
would leave ~110 px for each, i.e. ~27 characters. Instead the whole page shows **one node
at a time** as a list of its key/value pairs, with breadcrumbs and explicit navigation keys.
This matches the requirement ("click on an object and view this node to update each key
value pair", plus keys to go to root or back one level) and it reuses the CSV grid's row
selection, scrolling and inline-edit code almost unchanged.

```
y   0..7    global EditorMenuBar
y   8..15   FileTabBar (same widget as CSV)                       (§5.1)
y  16..24   path bar: [⌂ root] [↑ up] [← back]  $.A.B[2]  (right-truncated)
y  25..123  node rows — 11 visible rows of 9 px
            KEY .......... TYPE  VALUE            for leaf values
            KEY .......... {n}   →                for objects  (click to descend)
            KEY .......... [n]   →                for arrays   (click to descend)
            x 252..255  vertical scrollbar
y 125..135  row actions: [+val] [+obj] [+arr] [rename] [del] [type ◄►]
y 136..143  bottom bar + EventNotifier
```

- **Navigation**: click a row with `{}`/`[]` to descend; `Backspace` or `[↑]` goes up one
  level; `Home` or `[⌂]` jumps to root; `[←]` pops a small history stack (so "back" after
  jumping to root returns to where you were). Arrow keys move the row selection,
  wheel scrolls.
- **Editing a leaf**: `Enter` or click the value → `TextField` gated by
  `DataValue.IsCharAllowed` for that node's type; the `[type ◄►]` control cycles the six
  types via `EditorUI.CycleOnClick` and re-validates (a value that no longer fits is kept
  and flagged `Red`, never silently dropped).
- **Adding**: `[+val]`, `[+obj]`, `[+arr]` insert into the current node. Inside an object
  a key prompt appears first (validated and dup-checked → `"DUP KEY"` toast); inside an
  array the item is appended and rows are labelled by index instead of key.
- **Depth**: unlimited for the user in practice, hard-capped at `MaxJsonDepth` with a
  `"TOO DEEP"` toast — protects the serializer from stack overflow.
- `Ctrl+S` saves via the same global path as every other editor.

**Acceptance:** create root → object `A` → array `B` → 3 items of mixed types, save, restart,
and the tree is identical; renaming a key to an existing key is refused with a toast.

---

## 5. Shared editor widgets (built once, used by both)

### Step 5.1 — `src/editor/FileTabBar.cs`

The file-index strip used at `y 8..15` by both editors. Structurally the Music editor's
pattern strip (`MusicEditor.cs:22-24, 72-74, 105`) generalised:

```csharp
internal sealed class FileTabBar
{
    public FileTabBar(IMono8API api, int y, Func<IReadOnlyList<int>> indexes);
    public int  Selected { get; }
    public bool Update((int x, int y) mouse);   // true when the click was consumed
    public void Draw();
    public event Action AddRequested, RemoveRequested;
}
```

Prev/next arrows when the file count exceeds the visible slots, `[+]`/`[-]` buttons on the
right, selected tab drawn in the `White`-on-`Indigo` style used by `Button.Draw(selected)`.

### Step 5.2 — `src/editor/ScrollBar.cs`

No scrollbar exists in the project today. One widget, both axes:

```csharp
internal sealed class ScrollBar
{
    public ScrollBar(IMono8API api, Rectangle track, bool vertical);
    public bool Update((int x, int y) mouse, int contentSize, int viewSize, ref int offset);  // click + drag
    public void Draw(int contentSize, int viewSize, int offset);
}
```

4 px thick, `DarkGray` track / `LightGray` thumb, thumb length proportional to
`viewSize / contentSize`, minimum 6 px.

### Step 5.3 — `src/editor/TextField.cs`

Inline single-line editor: draws the text plus a blinking caret inside a `Rectangle`,
consumes `TextEntry.Typed`, gates each character through
`DataValue.IsCharAllowed(type, c, current)`, honours backspace, and commits on
`Enter` / cancels on `Esc`. Used for CSV cells, CSV column names, JSON keys and JSON values —
this is where "the user can only type valid chars" is actually enforced per field.

```csharp
internal sealed class TextField
{
    public void Begin(Rectangle bounds, string initial, DataValueType type, int maxLength);
    public bool Active { get; }
    public bool Update(out string committed, out bool cancelled);
    public void Draw();
}
```

---

## 6. Wiring up

### Step 6.1 — Register the editors (`src/Mono8API.cs`)

```csharp
Editors.Register(new CsvEditor(this),  CsvEditorIcon,  "Csv");
Editors.Register(new JsonEditor(this), JsonEditorIcon, "Json");
```

**Icon indexes are supplied later.** Declare them as two named constants in one place
(top of `Mono8API`, next to the existing literals `15, 16, 17, 18` at `Mono8API.cs:22-25`)
with a `// TODO: replace with the provided icon index` comment, so swapping them in is a
one-line change. No change to `data.icons` is part of this plan.

### Step 6.2 — Load and save (`src/Mono8API.cs:30-62`)

- `Load()`: `CsvSheet.LoadAll(path)` and `JsonSheet.LoadAll(path)` after the existing sheets.
- `Save()`: `CsvSheet.SaveAll(path)` and `JsonSheet.SaveAll(path)` — so the existing
  `Ctrl+S` in *any* editor persists them, matching the documented behaviour in `README.md:56`.
- Add `public static CsvSheet CsvSheet` / `public static JsonSheet JsonSheet` fields
  alongside `SpriteSheet`, `SfxSheet` etc. (`Mono8API.cs:7-11`).

### Step 6.3 — `Mono8Game.LoadContent` (`src/Mono8Game.cs:72`)

`TextEntry.Attach(Window);` next to `Text.GetCharacterTextures(GraphicsDevice)`, and
`TextEntry.Clear()` at the end of `InputStateManager.Update()` so `Typed` is per-frame.

### Step 6.4 — Docs (`README.md`)

- `## Editors` (line 43-45): "four editors" → "six", and mention the two new icons.
- `## Project Data` table (lines 58-67): add `data<N>.csv` and `data<N>.json` rows.
- New `## CSV Editor` and `## JSON Editor` sections with hotkey tables, following the shape
  of the existing `### SFX Editor Hotkeys` / `### Music Editor Hotkeys` sections.

---

## 7. Build order

Each phase compiles and is independently verifiable.

| # | Phase | Files | Verify |
|---|---|---|---|
| 1 | Char set + text input | `Text.cs`, new `TextEntry.cs`, `Mono8Game.cs`, `InputStateManager.cs` | Temporary debug print of `TextEntry.Typed` shows filtered characters. |
| 2 | Value model | new `core/data/DataValue.cs`, `Constants.cs`, `GlobalUsings.cs` | Round-trip each of the 6 types through `TryNormalize`/`Format`. |
| 3 | Indexed file IO | `FileIO.cs` | `ListIndexed` on a hand-made `data1.csv`/`data3.csv` pair. |
| 4 | CSV model | new `core/data/CsvSheet.cs` | Load → mutate → save → reload equality, including a cell containing `,` and `"`. |
| 5 | Shared widgets | new `FileTabBar.cs`, `ScrollBar.cs`, `TextField.cs` | Drop into the CSV editor shell as they land. |
| 6 | CSV editor UI | new `editor/CsvEditor.cs`, `Mono8API.cs` | Manual pass over §8. |
| 7 | JSON model | new `core/data/JsonNode.cs`, `JsonSheet.cs` | Parse → serialize → parse fixpoint on a nested sample. |
| 8 | JSON editor UI | new `editor/JsonEditor.cs`, `Mono8API.cs` | Manual pass over §8. |
| 9 | Docs + icons | `README.md`, icon constants | Both icons appear in the menu bar and switch correctly. |

---

## 8. Manual test checklist

**CSV**
- [ ] Tabs list every `data<N>.csv`; `[+]` creates the next free index; `[-]` deletes with the file.
- [ ] Rows 0..255 reachable; wheel, drag and keyboard scrolling all agree.
- [ ] A long cell widens its column and pushes the rest behind the horizontal scrollbar.
- [ ] `@` and `&` cannot be typed anywhere; `Int` columns reject letters; `Money` rejects a second `.`.
- [ ] A cell containing `a,b"c` survives save → reload.
- [ ] Switching a column's type flags (does not erase) values that no longer fit.
- [ ] `Ctrl+S` shows `SAVED` and the file on disk is diff-clean when nothing changed.

**JSON**
- [ ] Tabs list every `data<N>.json`; add/remove behave as for CSV.
- [ ] Descend into nested objects/arrays; `⌂`, `↑` and `←` all navigate correctly.
- [ ] A duplicate or empty key is refused with a toast; keys display upper-cased.
- [ ] Values of all six types edit, save and reload with their type preserved.
- [ ] A hand-edited malformed file loads as an empty object instead of crashing.
- [ ] Nesting past `MaxJsonDepth` is refused rather than throwing.

**Both**
- [ ] Switching editors and back preserves selection/scroll (or clears it in `Exit()` — pick one and be consistent).
- [ ] `Ctrl+R` (run game) and `Esc` still work while these editors are active.
- [ ] An exception inside either editor is caught by `ErrorHandler` (`Mono8API.cs:110`) rather than killing the process.

---

## 9. Decisions taken (flag if you disagree)

1. **Column-level types, not cell-level.** The requirement lists the six types for a "cell";
   a spreadsheet where each of 2048 cells carries its own type is unusable at this screen
   size and unrepresentable in flat CSV without a sidecar. The type lives on the column
   (stored in the header as `NAME:type`) and every cell in it is validated against it.
   *If per-cell typing is genuinely wanted, the header format and `CsvSheet` change; the
   editor UI does not.*
2. **Fixed 256×8 grid.** Rows always exist, so there is no insert/delete-row logic.
3. **Keys and column names upper-cased**, values stored as typed — because the font renders
   everything upper-case and invisible duplicates would be a data-corruption bug.
4. **JSON editor drills in** rather than showing a split tree/inspector, for the space
   reasons in §4.3.
5. **Pretty-printed JSON, RFC-4180 CSV** — both chosen for diffability, consistent with
   the project's plain-text data story.

## 10. Explicitly out of scope

- **Game-facing API.** Nothing in `IMono8API` exposes this data to `YourGame` yet
  (no `csvget` / `jsonget`). The editors author the files; wiring them into the runtime API
  is a separate change and should be planned once the shapes above are settled.
- Undo/redo (no editor in the project has it today).
- Copy/paste of cell ranges or subtrees.
- Supplying the actual icon artwork for `data.icons`.
