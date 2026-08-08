namespace mono8.editor;

/// <summary>
/// Editor for <c>data.json</c>: a tree of groups and objects on the left, the selected object's
/// keys and values on the right.
/// <para>
/// The tree is only ever two levels deep and names are at most 8 characters, so the whole navigator
/// fits in 40 px and the panels can sit side by side instead of drilling in. Groups and objects are
/// told apart by the fold marker and the text colour rather than by indentation.
/// </para>
/// <para>
/// The tree scrolls in whole rows; the inspector scrolls in pixels, because a Text value wraps and
/// its row block is as tall as it needs to be.
/// </para>
/// </summary>
internal sealed class JsonEditor : IEditor, IEditorConfig
{
    // ── Layout ────────────────────────────────────────────────────────────────
    private const int RowH = Text.LineHeight;                            // 9
    private const int ContentTop = Constants.GameDataSizes.TileSize;     // 8, just under the menu bar
    private const int VisibleRows = 13;
    private const int ContentH = VisibleRows * RowH;                     // 117
    private const int ContentBottom = ContentTop + ContentH;             // 125, exclusive

    private const int MarkerW = 4;    // the fold-marker column of a tree row
    private const int TreeW = 40;
    private const int ScrollW = 4;
    private const int TreeScrollX = TreeW;
    private const int DividerX = 44;

    private const int InspX = 46;
    private const int InspRight = 248;    // exclusive
    private const int InspScrollX = 252;

    // A name column is one character wider than the name it holds. A glyph is 5 px wide on a 4 px
    // advance, so 8 characters printed one pixel into the column reach 2 px past 8 × 4 — and the
    // inline editor keeps (width - 2) / 4 characters on screen, which at an exact 32 would scroll
    // the first character off the moment a name used all 8. The badge and the value follow from it.
    private const int NameW = 36;         // 8 characters plus the character of slack
    private const int KeyX = InspX;
    private const int BadgeX = KeyX + NameW + 1;
    private const int BadgeW = 7;
    private const int ValueX = BadgeX + BadgeW + 1;
    private const int ItemLabelW = 16;    // "63:" plus a gap
    private const int ItemValueX = ValueX + ItemLabelW;

    /// <summary>Characters that fit on one line of a scalar's value area, and of an array item's.</summary>
    private const int ScalarCols = (InspRight - ValueX) / Text.CharAdvance;       // 39
    private const int ItemCols = (InspRight - ItemValueX) / Text.CharAdvance;     // 35

    private const int ActionY = 126;
    private const int ActionH = 9;

    private const float DeleteArmSeconds = 2f;
    private const float DoubleClickSeconds = 0.4f;

    private static readonly int TypeCount = Enum.GetValues<DataValueType>().Length;

    private static readonly string[] TreeActions = { "+GRP", "+OBJ", "REN", "DEL" };
    private static readonly string[] TreeCopyActions = { "+GRP", "+OBJ", "REN", "DEL", "COPY" };
    private static readonly string[] TreePasteActions = { "+GRP", "+OBJ", "REN", "DEL", "DPCK", "DPCA" };
    private static readonly string[] TreeCopyPasteActions = { "+GRP", "+OBJ", "REN", "DEL", "COPY", "DPCK", "DPCA" };
    private static readonly string[] EmptyTreeActions = { "+GRP" };
    private static readonly string[] ScalarActions = { "+KEY", "REN", "DEL", "ARR" };
    private static readonly string[] ArrayActions = { "+KEY", "REN", "DEL", "ARR", "+ITM", "-ITM" };
    private static readonly string[] EmptyInspectorActions = { "+KEY" };

    // ── State ─────────────────────────────────────────────────────────────────
    private enum Panel { Tree, Inspector }

    /// <summary>What the open <see cref="TextField"/> is editing, so its commit knows where to go.</summary>
    private enum Editing { None, NodeName, KeyName, NewKey, Value }

    /// <summary>One drawn block of the inspector: a field's item, and the lines it occupies.</summary>
    private struct Block
    {
        public int Field;
        public int Item;
        public int Top;      // pixels from the top of the inspector's content
        public int Height;
        public bool FirstOfField;
    }

    private readonly IMono8API _api;
    private readonly EventNotifier _events;
    private readonly ScrollBar _treeBar;
    private readonly ScrollBar _inspectorBar;
    private readonly TextField _field;

    private readonly List<(JsonGroup Group, JsonObject Object)> _rows = new();
    private readonly List<Block> _blocks = new();

    private Panel _focus = Panel.Tree;
    private Editing _editing = Editing.None;

    private object _selected;          // the tree selection: a JsonGroup or a JsonObject
    private JsonObject _inspected;     // what the inspector shows; only an object row changes it
    private JsonObject _copied;        // [COPY]'s object: the template [DPCK] and [DPCA] lay down
    private int _selField;
    private int _selItem;

    private int _treeScroll;           // whole rows
    private int _inspectorScroll;      // pixels

    private object _deleteArmed;
    private float _deleteArmLeft;

    private object _clickTarget;       // the node or field the last name click landed on
    private float _clickLeft;

    /// <summary>Set once LOAD FIX has been said; it is about the file that was read, so it is said once.</summary>
    private bool _loadIssuesReported;

    public JsonEditor(IMono8API api)
    {
        _api = api;
        _events = new EventNotifier(api, 2f, 1, EditorUI.BottomBarY + 1);
        _treeBar = new ScrollBar(api, new Rectangle(TreeScrollX, ContentTop, ScrollW, ContentH));
        _inspectorBar = new ScrollBar(api, new Rectangle(InspScrollX, ContentTop, ScrollW, ContentH));
        _field = new TextField(api);

        ApplyConfig(Mono8API.ConfigSheet);
    }

    private static JsonSheet Sheet => Mono8API.JsonSheet;

    /// <summary>
    /// Re-finds the saved selection by name. A group or object that has since been renamed, deleted
    /// or never existed leaves nothing selected, and <see cref="Init"/>'s fallback then lands on the
    /// first group — a row that inspects nothing, so the panel reads NO OBJECT rather than opening
    /// on some other object the developer never chose.
    /// </summary>
    private void ApplyConfig(ConfigSheet config)
    {
        var group = Sheet.FindGroup(config.JsonGroup);
        if (group == null) return;

        if (config.JsonObject.Length == 0)
        {
            _selected = group;
            return;
        }

        var obj = JsonSheet.FindObject(group, config.JsonObject);
        if (obj == null) return;

        _selected = obj;
        Inspect(obj);
    }

    /// <summary>
    /// Saved by name rather than by reference: the tree is parsed afresh on every start, so the
    /// nodes this points at will not be the ones that come back.
    /// </summary>
    void IEditorConfig.CaptureConfig(ConfigSheet config)
    {
        var obj = _selected as JsonObject;
        var group = obj != null ? Sheet.OwnerOf(obj) : _selected as JsonGroup;

        config.JsonGroup = group?.Name ?? string.Empty;
        config.JsonObject = obj?.Name ?? string.Empty;
    }

    public void Init()
    {
        RebuildRows();
        if (_selected == null && _rows.Count > 0) SelectRow(0);
        else EnsureRowVisible();   // a restored selection can sit below the first screenful

        // data.json is parsed before any editor exists, so a load that dropped or repaired
        // something has nowhere to say so until the editor is first opened. It is worth saying
        // because the repair only reaches the file on the next Ctrl+S — until then, what is on
        // disk and what is in the tree are two different things.
        if (Sheet.HadLoadIssues && !_loadIssuesReported)
        {
            _loadIssuesReported = true;
            _events.AddEvent("LOAD FIX");
        }
    }

    /// <summary>Leaving mid-edit drops the edit; the selection and both scroll positions stay put.</summary>
    public void Exit()
    {
        _field.Cancel();
        _editing = Editing.None;
        _deleteArmed = null;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public void Update(float elapsedSeconds)
    {
        _events.Update(elapsedSeconds);
        if (Mono8API.MenuBar.HoverLabel != null) _events.SetHover(Mono8API.MenuBar.HoverLabel);

        if (_deleteArmLeft > 0f)
        {
            _deleteArmLeft -= elapsedSeconds;
            if (_deleteArmLeft <= 0f) _deleteArmed = null;
        }

        if (_clickLeft > 0f)
        {
            _clickLeft -= elapsedSeconds;
            if (_clickLeft <= 0f) _clickTarget = null;
        }

        RebuildRows();
        RebuildBlocks();
        ClampSelection();

        // An open field owns the keyboard: nothing else may read a keystroke meant for it.
        if (_field.Active)
        {
            UpdateField();
            return;
        }

        if (KeybrdInput.IsSaveShortcutPressed()) Save();

        var mouse = _api.mousexy();
        UpdateWheel(mouse);
        UpdateTreeMouse(mouse);
        UpdateInspectorHover(mouse);
        UpdateInspectorMouse(mouse);
        UpdateActions(mouse);
        UpdateKeys();

        RebuildRows();
        RebuildBlocks();
        ClampSelection();

        _treeBar.Update(mouse, _rows.Count, VisibleRows, ref _treeScroll);
        _inspectorBar.Update(mouse, InspectorHeight(), ContentH, ref _inspectorScroll);
        ClampScroll();
    }

    private void UpdateField()
    {
        // The field has to be sitting on the block before it reads the mouse, or a click would be
        // measured against where the value was a line ago.
        PositionValueField();

        // A paste while the value is open lands in the buffer and not in the sheet, since the buffer
        // is what the commit will write — anything else would be undone the moment the edit ended.
        if (_editing == Editing.Value && KeybrdInput.IsPasteShortcutPressed()) PasteIntoField();

        if (!_field.Update(out string committed, out bool cancelled)) return;

        var mode = _editing;
        bool advance = _field.Advance;
        _editing = Editing.None;

        if (!cancelled) Commit(mode, committed, advance);

        // A pending key's row was only ever room the open field was holding. It is gone now — added
        // as a real block, refused as a duplicate, or dropped with an Esc — so the scroll that
        // opened it up comes back off in the same frame rather than leaving a blank row behind.
        ClampScroll();
    }

    /// <summary>
    /// Sends what the field accepted wherever the edit that opened it was aimed.
    /// <para>
    /// A name field only ever accepts characters <see cref="JsonNames"/> allows and stops at its 8,
    /// so the one entry that can fail to be a name is an empty one — and clearing a name and pressing
    /// Enter reads as having changed nothing, not as an error. It is let through silently, leaving
    /// the old name in place; the only refusal left worth a toast is a sibling that has the name.
    /// </para>
    /// </summary>
    private void Commit(Editing mode, string committed, bool advance)
    {
        if (mode != Editing.Value && string.IsNullOrWhiteSpace(committed)) return;

        switch (mode)
        {
            case Editing.NodeName:
                if (!Sheet.TryRename(_selected, committed)) _events.AddEvent("DUP NAME");
                break;

            case Editing.KeyName:
                if (!Sheet.TryRename(SelectedField(), committed)) _events.AddEvent("DUP KEY");
                break;

            case Editing.NewKey:
                if (Sheet.TryAddField(_inspected, committed, DataValueType.Text, false, out JsonField added))
                {
                    _selField = _inspected.Fields.IndexOf(added);
                    _selItem = 0;
                    RebuildBlocks();
                    EnsureBlockVisible();
                }
                else _events.AddEvent("DUP KEY");
                break;

            case Editing.Value:
                var field = SelectedField();
                if (field == null) break;
                if (!Sheet.TrySetValue(field, _selItem, committed)) _events.AddEvent("BAD VAL");
                RebuildBlocks();
                if (advance)
                {
                    SelectBlock(SelectedBlock() + 1);
                    BeginValueEdit();
                }
                break;
        }
    }

    private void UpdateWheel((int x, int y) mouse)
    {
        if (mouse.y < ContentTop || mouse.y >= ContentBottom) return;

        bool overTree = mouse.x < DividerX;
        if (_api.mouseup())
        {
            if (overTree) _treeScroll--;
            else _inspectorScroll -= RowH;
        }
        else if (_api.mousedown())
        {
            if (overTree) _treeScroll++;
            else _inspectorScroll += RowH;
        }

        ClampScroll();
    }

    private void UpdateTreeMouse((int x, int y) mouse)
    {
        if (mouse.x >= TreeW || mouse.y < ContentTop || mouse.y >= ContentBottom) return;
        if (!_api.mouselp()) return;

        int row = _treeScroll + (mouse.y - ContentTop) / RowH;
        if (row < 0 || row >= _rows.Count) return;

        _focus = Panel.Tree;
        _deleteArmed = null;

        // The marker column folds a group; anywhere else on the row selects it.
        var (group, obj) = _rows[row];
        if (obj == null && mouse.x < MarkerW)
        {
            group.Collapsed = !group.Collapsed;
            _selected = group;
            _clickTarget = null;   // folding is not half of a rename
            return;
        }

        SelectRow(row);
        if (IsDoubleClick(obj ?? (object)group)) BeginNodeRename();
    }

    /// <summary>
    /// Spells the type badge out on the bottom bar while the cursor rests on it. The badge is one
    /// character wide, so <c>p</c> and <c>b</c> are only guessable until something names them.
    /// </summary>
    private void UpdateInspectorHover((int x, int y) mouse)
    {
        if (_inspected == null) return;
        if (mouse.x < BadgeX || mouse.x >= BadgeX + BadgeW) return;
        if (mouse.y < ContentTop || mouse.y >= ContentBottom) return;

        for (int i = 0; i < _blocks.Count; i++)
        {
            var block = _blocks[i];
            if (!block.FirstOfField) continue;   // only the first line of a field carries the badge

            int y = ContentTop + block.Top - _inspectorScroll;
            if (!IsLineVisible(y) || mouse.y < y || mouse.y >= y + RowH) continue;

            _events.SetHover(TypeHint(_inspected.Fields[block.Field], block.Item));

            // The badge is the one control in the panel; the name and the value open an edit on a
            // second click, and the rows around them are a list rather than buttons.
            EditorUI.HoverPointer(true);
            return;
        }
    }

    private void UpdateInspectorMouse((int x, int y) mouse)
    {
        if (_inspected == null) return;
        if (mouse.x < InspX || mouse.x >= InspRight) return;
        if (mouse.y < ContentTop || mouse.y >= ContentBottom) return;

        bool left = _api.mouselp();
        if (!left && !_api.mouserp()) return;

        // Anywhere in the panel takes focus, hit or miss — an object with no keys yet has no row to
        // click, and [+KEY] would otherwise be unreachable without the keyboard.
        _focus = Panel.Inspector;

        int y = mouse.y - ContentTop + _inspectorScroll;
        int hit = -1;
        for (int i = 0; i < _blocks.Count; i++)
        {
            if (y < _blocks[i].Top || y >= _blocks[i].Top + _blocks[i].Height) continue;
            hit = i;
            break;
        }
        if (hit < 0) return;

        var block = _blocks[hit];
        var field = _inspected.Fields[block.Field];

        _deleteArmed = null;
        _selField = block.Field;
        _selItem = block.Item;

        // The name and the type badge only exist on the field's first line.
        if (block.FirstOfField && y < block.Top + RowH)
        {
            var badge = new Rectangle(BadgeX, ContentTop + block.Top - _inspectorScroll, BadgeW, RowH);
            int type = (int)field.Type;
            if (EditorUI.CycleOnClick(_api, badge, mouse, ref type, TypeCount))
            {
                Sheet.SetType(field, (DataValueType)type);
                return;
            }

            // A single click on the name only selects the key, so the second one opens its rename.
            if (left && mouse.x < BadgeX && IsDoubleClick(field))
            {
                BeginKeyRename();
                return;
            }
        }

        if (!left || mouse.x < ValueColumnX(field)) return;

        if (field.Type == DataValueType.Bool)
        {
            ToggleBool(field, block.Item);
            return;
        }

        // The click that opened a Text value also says where in it the caret goes, so the edit
        // starts under the pointer rather than at the end of however many lines it runs to. It is
        // taken as an offset into the block, since opening the edit can scroll the block.
        int offsetX = mouse.x - ValueColumnX(field);
        int offsetY = mouse.y - (ContentTop + block.Top - _inspectorScroll);

        BeginValueEdit();
        _field.PlaceCaretIn(offsetX, offsetY);
    }

    private void UpdateActions((int x, int y) mouse)
    {
        var set = ActionSet();
        for (int i = 0; i < set.Length; i++)
        {
            if (!ActionRect(set, i).Contains(mouse.x, mouse.y)) continue;

            _events.SetHover(ActionHint(set[i]));
            EditorUI.HoverPointer(true);
            if (_api.mouselp()) RunAction(set[i]);
            return;
        }
    }

    private void UpdateKeys()
    {
        if (KeybrdInput.JustPressed(Keys.Tab))
        {
            _focus = _focus == Panel.Tree ? Panel.Inspector : Panel.Tree;
            return;
        }

        // [REN]'s shortcut. Ctrl+R runs the game, so only a bare R renames.
        if (KeybrdInput.NoModifiersPressed() && KeybrdInput.JustPressed(Keys.R))
        {
            if (_focus == Panel.Tree) BeginNodeRename();
            else BeginKeyRename();
            return;
        }

        if (_focus == Panel.Tree) UpdateTreeKeys();
        else UpdateInspectorKeys();
    }

    private void UpdateTreeKeys()
    {
        int row = SelectedRow();

        if (KeybrdInput.JustPressed(Keys.Up)) SelectRow(row < 0 ? 0 : row - 1);
        if (KeybrdInput.JustPressed(Keys.Down)) SelectRow(row < 0 ? 0 : row + 1);

        if (_selected is JsonGroup group)
        {
            if (KeybrdInput.JustPressed(Keys.Left)) group.Collapsed = true;
            if (KeybrdInput.JustPressed(Keys.Right)) group.Collapsed = false;
        }

        if (KeybrdInput.JustPressed(Keys.Enter)) BeginNodeRename();
        if (KeybrdInput.JustPressed(Keys.Delete)) DeleteNode();
    }

    private void UpdateInspectorKeys()
    {
        if (_inspected == null) return;

        if (KeybrdInput.IsPasteShortcutPressed()) PasteValue();

        if (KeybrdInput.JustPressed(Keys.Up)) SelectBlock(SelectedBlock() - 1);
        if (KeybrdInput.JustPressed(Keys.Down)) SelectBlock(SelectedBlock() + 1);

        if (KeybrdInput.JustPressed(Keys.Enter))
        {
            var field = SelectedField();
            if (field != null && field.Type == DataValueType.Bool) ToggleBool(field, _selItem);
            else BeginValueEdit();
        }

        if (KeybrdInput.JustPressed(Keys.Delete)) DeleteField();
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void RunAction(string label)
    {
        switch (label)
        {
            case "+GRP": AddGroup(); break;
            case "+OBJ": AddObject(); break;
            case "COPY": CopyObject(); break;
            case "DPCK": Duplicate(false); break;
            case "DPCA": Duplicate(true); break;
            case "+KEY": BeginNewKey(); break;
            case "REN": if (_focus == Panel.Tree) BeginNodeRename(); else BeginKeyRename(); break;
            case "DEL": if (_focus == Panel.Tree) DeleteNode(); else DeleteField(); break;
            case "ARR": ToggleArray(); break;
            case "+ITM": AddItem(); break;
            case "-ITM": RemoveItem(); break;
        }
    }

    private string ActionHint(string label) => label switch
    {
        "+GRP" => "NEW GROUP",
        "+OBJ" => "NEW OBJECT",
        "+KEY" => "NEW KEY",

        // These three name the object they act on. A duplicate's is the one [COPY] took, which is
        // not the selected one and may not even be on screen, so the name is the only thing saying
        // what is about to be laid down.
        "COPY" => "COPY " + NamePath(_selected as JsonObject),
        "DPCK" => "DUPLICATE ONLY KEYS FOR " + NamePath(_copied) + DestinationHint(),
        "DPCA" => "DUPLICATE KEYS/VALUES FOR " + NamePath(_copied) + DestinationHint(),

        "REN" => "RENAME [R]",
        "DEL" => DeleteTakesItem() ? "DELETE ITEM" : "DELETE",
        "ARR" => "SCALAR / ARRAY",
        "+ITM" => "ADD ITEM",
        _ => "REMOVE ITEM"
    };

    /// <summary>
    /// Where a duplicate would land, as the tail of the two hints. It is worth saying because the
    /// group is only the selected one when a group is what is selected: with an object selected the
    /// duplicate goes to the group holding it, which is a row the cursor is nowhere near.
    /// </summary>
    private string DestinationHint()
    {
        var group = DestinationGroup();
        return group == null ? string.Empty : " ON " + group.Name;
    }

    /// <summary>The group a new object goes into: the selected one, or the one holding the selection.</summary>
    private JsonGroup DestinationGroup() =>
        _selected as JsonGroup ?? Sheet.OwnerOf(_selected as JsonObject);

    /// <summary>
    /// What the one-character type badge stands for. PosXY says more than its name because its
    /// shape — two ints and a comma — is the one thing here that a name alone does not give away:
    /// the position it already holds if that reads back, and an example if it does not. Text
    /// carries its cap, since where it stops taking characters is not something the name says.
    /// </summary>
    private static string TypeHint(JsonField field, int item)
    {
        string label = TypeLabel(field.Type);
        switch (field.Type)
        {
            case DataValueType.Text:
                return label + " MAX " + DataValue.MaxLength(field.Type);

            case DataValueType.PosXY:
                return JsonSheet.IsValid(field, item)
                    ? label + " " + field.Values[item]
                    : label + " EG 40,88";

            default:
                return label;
        }
    }

    private static string TypeLabel(DataValueType type) => type switch
    {
        DataValueType.Text => "TEXT",
        DataValueType.Int => "INT",
        DataValueType.Decimal => "DECIMAL",
        DataValueType.Money => "MONEY",
        DataValueType.PosXY => "POSITION",
        _ => "BOOL"
    };

    private void AddGroup()
    {
        string name = FreeName("G", Constants.JsonData.MaxGroups, n => Sheet.FindGroup(n) != null);
        if (!Sheet.TryAddGroup(name, out JsonGroup group))
        {
            _events.AddEvent("MAX GRP");
            return;
        }

        _selected = group;
        _focus = Panel.Tree;
        RebuildRows();
        EnsureRowVisible();
    }

    private void AddObject()
    {
        var group = DestinationGroup();
        if (group == null)
        {
            _events.AddEvent("NO GROUP");
            return;
        }

        string name = FreeName("O", Constants.JsonData.MaxObjectsPerGrp,
            n => JsonSheet.FindObject(group, n) != null);

        if (!Sheet.TryAddObject(group, name, out JsonObject obj))
        {
            _events.AddEvent("MAX OBJ");
            return;
        }

        group.Collapsed = false;
        _selected = obj;
        Inspect(obj);
        _focus = Panel.Tree;
        RebuildRows();
        EnsureRowVisible();
    }

    /// <summary>
    /// Remembers the selected object as the template the two duplicate buttons work from. It is the
    /// object itself and not a snapshot of it, so a duplicate taken later is of what that object is
    /// then — there is nothing here that would say the copy had gone stale.
    /// </summary>
    private void CopyObject()
    {
        if (_selected is not JsonObject obj) return;

        _copied = obj;
        _events.AddEvent("COPIED " + NamePath(obj));
    }

    /// <summary>
    /// Lays the copied object down as a new one: every key with its type, its array flag and its
    /// item count, and with <paramref name="withValues"/> the values too. It lands in the selected
    /// group — or in the one holding the selected object, so a duplicate can be taken from the
    /// object it sits next to without selecting the group first — and the selection follows it.
    /// </summary>
    private void Duplicate(bool withValues)
    {
        if (_copied == null) return;

        var group = DestinationGroup();
        if (group == null)
        {
            _events.AddEvent("NO GROUP");
            return;
        }

        string name = FreeName("O", Constants.JsonData.MaxObjectsPerGrp,
            n => JsonSheet.FindObject(group, n) != null);

        if (!Sheet.TryCopyObject(group, name, _copied, withValues, out JsonObject obj))
        {
            _events.AddEvent("MAX OBJ");
            return;
        }

        group.Collapsed = false;
        _selected = obj;
        Inspect(obj);
        _focus = Panel.Tree;
        RebuildRows();
        EnsureRowVisible();
    }

    private void DeleteNode()
    {
        if (_selected == null || !ConfirmDelete(_selected)) return;

        int row = SelectedRow();

        // The inspector has to let go of whatever the delete takes with it.
        if (_selected is JsonObject obj && obj == _inspected) Inspect(null);
        else if (_selected is JsonGroup group && group.Objects.Contains(_inspected)) Inspect(null);

        Sheet.Remove(_selected);
        _selected = null;

        RebuildRows();
        if (_rows.Count > 0) SelectRow(Math.Min(row, _rows.Count - 1));
        ClampScroll();
    }

    /// <summary>
    /// True while [DEL] would take the selected array item rather than the whole key. The last item
    /// is the exception: an array never empties, so once one is left the only delete left to mean is
    /// the key itself — otherwise an array key could never be deleted without collapsing it first.
    /// </summary>
    private bool DeleteTakesItem()
    {
        if (_focus != Panel.Inspector) return false;   // the tree's [DEL] is the node's, not an item's

        var field = SelectedField();
        return field != null && field.IsArray && field.Values.Count > 1;
    }

    private void DeleteField()
    {
        var field = SelectedField();
        if (field == null) return;

        // The highlighted row is one item of the list, so that is what a delete aimed at it takes.
        if (DeleteTakesItem())
        {
            RemoveItem();
            return;
        }

        if (!ConfirmDelete(field)) return;

        Sheet.Remove(field);
        _selItem = 0;
        RebuildBlocks();
        ClampSelection();
        ClampScroll();
    }

    private void ToggleArray()
    {
        var field = SelectedField();
        if (field == null) return;

        if (Sheet.SetIsArray(field, !field.IsArray)) _events.AddEvent("KEEP 1");
        _selItem = 0;
        RebuildBlocks();
        EnsureBlockVisible();
    }

    /// <summary>
    /// Adds an item straight below the selected one and follows it, so a list is built in the order
    /// it reads rather than by adding at the end and moving what was added.
    /// </summary>
    private void AddItem()
    {
        var field = SelectedField();
        if (field == null || !field.IsArray) return;

        if (!Sheet.TryAddItem(field, _selItem + 1))
        {
            _events.AddEvent("MAX ITM");
            return;
        }

        _selItem = Math.Min(_selItem + 1, field.Values.Count - 1);
        RebuildBlocks();
        EnsureBlockVisible();
    }

    private void RemoveItem()
    {
        var field = SelectedField();
        if (field == null || !field.IsArray) return;

        // Item 0 is what a field collapses back to, so an array never empties itself.
        if (field.Values.Count <= 1)
        {
            _events.AddEvent("KEEP 1");
            return;
        }

        Sheet.RemoveItem(field, _selItem);
        _selItem = Math.Min(_selItem, field.Values.Count - 1);
        RebuildBlocks();
        EnsureBlockVisible();
    }

    private void ToggleBool(JsonField field, int item)
    {
        if (item < 0 || item >= field.Values.Count) return;
        Sheet.TrySetValue(field, item, DataValue.IsTrue(field.Values[item]) ? "false" : "true");
    }

    /// <summary>
    /// No modal dialog exists anywhere in the project, so a delete is confirmed by pressing it a
    /// second time while the <c>HOLD DEL</c> toast is still up.
    /// </summary>
    private bool ConfirmDelete(object node)
    {
        if (ReferenceEquals(_deleteArmed, node))
        {
            _deleteArmed = null;
            _deleteArmLeft = 0f;
            return true;
        }

        _deleteArmed = node;
        _deleteArmLeft = DeleteArmSeconds;
        _events.AddEvent("HOLD DEL");
        return false;
    }

    /// <summary>
    /// True when this click repeats the last one on the same target soon enough to read as a double
    /// click. Either way the target becomes what the next click is measured against, so a third
    /// click starts a fresh pair rather than renaming again.
    /// </summary>
    private bool IsDoubleClick(object target)
    {
        bool again = _clickLeft > 0f && ReferenceEquals(_clickTarget, target);

        _clickTarget = again ? null : target;
        _clickLeft = again ? 0f : DoubleClickSeconds;
        return again;
    }

    /// <summary>First unused <c>G1</c>..<c>G16</c> / <c>O1</c>..<c>O64</c>, so a new node is never invalid.</summary>
    private static string FreeName(string prefix, int max, Func<string, bool> taken)
    {
        for (int i = 1; i <= max; i++)
        {
            string name = prefix + i;
            if (!taken(name)) return name;
        }
        return prefix + max;
    }

    private void Save()
    {
        // Writing a value that no longer reads as its type would produce a file that cannot load
        // back, so the offender is put on screen instead and the save waits.
        if (Sheet.TryFindInvalid(out JsonObject obj, out JsonField field, out int index))
        {
            // Named rather than just flagged: the offender is often in a collapsed group nowhere
            // near what is on screen, so the toast says which one before the reveal jumps there.
            var group = Sheet.OwnerOf(obj);
            _events.AddEvent($"ERROR ON {group?.Name}/{obj.Name}/{field.Name}");
            Reveal(obj, field, index);
            return;
        }

        Mono8Game.GameAPI.Save();
        _events.AddEvent("SAVED");
    }

    private void Reveal(JsonObject obj, JsonField field, int index)
    {
        var group = Sheet.OwnerOf(obj);
        if (group != null) group.Collapsed = false;

        _selected = obj;
        Inspect(obj);
        _selField = obj.Fields.IndexOf(field);
        _selItem = index;
        _focus = Panel.Inspector;

        RebuildRows();
        RebuildBlocks();
        EnsureRowVisible();
        EnsureBlockVisible();
    }

    // ── Paste ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ctrl+V on the selected value with no edit open. It writes straight to the sheet, so what was
    /// copied has to read back as the field's own type — an Int has nowhere to put a position.
    /// </summary>
    private void PasteValue()
    {
        var field = SelectedField();
        if (!PasteAllowed(field)) return;

        if (!Sheet.TrySetValue(field, _selItem, ValueClipboard.Value))
        {
            _events.AddEvent("BAD VAL");
            return;
        }

        _events.AddEvent(ValueClipboard.PasteLabel);
        RebuildBlocks();
    }

    /// <summary>
    /// The same while the value is open for editing. The field gates it exactly as it gates typing,
    /// so a half-finished entry — a PosXY still missing its comma — is let in and refused on commit,
    /// just as a typed one would be.
    /// </summary>
    private void PasteIntoField()
    {
        if (!PasteAllowed(SelectedField())) return;

        if (_field.TrySetValue(ValueClipboard.Value)) _events.AddEvent(ValueClipboard.PasteLabel);
        else _events.AddEvent("BAD VAL");
    }

    /// <summary>
    /// Whether there is something to paste and a field that can take it, saying which is missing
    /// when there is not. Only three types can: a sprite or a colour index reads back as an Int, a
    /// map position as a PosXY, and a Text takes either as the string it is. A Decimal, a Money or a
    /// Bool has no copy anywhere that means anything to it, so the paste is refused rather than
    /// turning a sprite index into 137.00.
    /// </summary>
    private bool PasteAllowed(JsonField field)
    {
        if (field == null) return false;

        if (!ValueClipboard.HasValue)
        {
            _events.AddEvent("NOTHING COPIED");
            return false;
        }

        if (field.Type != DataValueType.Int
            && field.Type != DataValueType.PosXY
            && field.Type != DataValueType.Text)
        {
            _events.AddEvent("CANT PASTE");
            return false;
        }

        return true;
    }

    // ── Text entry ────────────────────────────────────────────────────────────

    private void BeginNodeRename()
    {
        int row = SelectedRow();
        if (row < 0) return;

        string name = _selected is JsonGroup group ? group.Name : ((JsonObject)_selected).Name;
        EnsureRowVisible();

        _field.BeginName(new Rectangle(MarkerW, ContentTop + (row - _treeScroll) * RowH, TreeW - MarkerW, RowH), name);
        _editing = Editing.NodeName;
    }

    private void BeginKeyRename()
    {
        var field = SelectedField();
        if (field == null) return;

        int block = FirstBlockOfField(_selField);
        if (block < 0) return;

        _selItem = 0;
        EnsureBlockVisible();

        int y = ContentTop + _blocks[block].Top - _inspectorScroll;
        _field.BeginName(new Rectangle(KeyX, y, BadgeX - KeyX - 1, RowH), field.Name);
        _editing = Editing.KeyName;
    }

    private void BeginNewKey()
    {
        if (_inspected == null) return;

        if (_inspected.Fields.Count >= Constants.JsonData.MaxFieldsPerObj)
        {
            _events.AddEvent("MAX KEY");
            return;
        }

        // The new key lands one row past the last block, so scroll far enough to show it. The row it
        // lands on is part of the panel's height for as long as the field is open, so this is the
        // bottom of the scroll rather than one row past it and it survives the next clamp.
        int top = BlocksHeight();
        _inspectorScroll = Math.Max(0, top + RowH - ContentH);

        _field.BeginName(new Rectangle(KeyX, ContentTop + top - _inspectorScroll, BadgeX - KeyX - 1, RowH), string.Empty);
        _editing = Editing.NewKey;
    }

    private void BeginValueEdit()
    {
        var field = SelectedField();
        if (field == null) return;

        if (field.Type == DataValueType.Bool)
        {
            ToggleBool(field, _selItem);
            return;
        }

        int block = SelectedBlock();
        if (block < 0) return;

        EnsureBlockVisible();

        int x = ValueColumnX(field);
        int y = ContentTop + _blocks[block].Top - _inspectorScroll;

        // A Text value is the one that wraps, so it is edited over the whole block it occupies and
        // with a caret in it. Everything else fits on its line and appends at the end.
        if (field.Type == DataValueType.Text)
        {
            _field.BeginText(new Rectangle(x, y, InspRight - x, _blocks[block].Height),
                field.Values[_selItem], Columns(field), DataValue.MaxLength(field.Type));
            _field.Clip(ContentTop, ContentBottom);
        }
        else
        {
            _field.Begin(new Rectangle(x, y, InspRight - x, RowH), field.Values[_selItem],
                field.Type, DataValue.MaxLength(field.Type));
        }

        _editing = Editing.Value;
    }

    /// <summary>
    /// Re-states where the open value field is. A Text value changes height as it is typed, so the
    /// block it sits in is re-measured every frame and the field is moved onto it — and the panel
    /// scrolls if the growth pushed its last line past the bottom.
    /// </summary>
    private void PositionValueField()
    {
        if (_editing != Editing.Value || !_field.Active) return;

        var field = SelectedField();
        if (field == null) return;

        EnsureBlockVisible();

        int block = SelectedBlock();
        if (block < 0) return;

        int x = ValueColumnX(field);
        int y = ContentTop + _blocks[block].Top - _inspectorScroll;
        _field.SetBounds(new Rectangle(x, y, InspRight - x, _blocks[block].Height));
    }

    // ── Model views ───────────────────────────────────────────────────────────

    /// <summary>The flattened tree: every group, and the objects of the groups that are open.</summary>
    private void RebuildRows()
    {
        _rows.Clear();
        foreach (var group in Sheet.Groups)
        {
            _rows.Add((group, null));
            if (group.Collapsed) continue;

            foreach (var obj in group.Objects) _rows.Add((group, obj));
        }
    }

    private void RebuildBlocks()
    {
        _blocks.Clear();
        if (_inspected == null) return;

        int top = 0;
        for (int fi = 0; fi < _inspected.Fields.Count; fi++)
        {
            var field = _inspected.Fields[fi];
            for (int item = 0; item < field.Values.Count; item++)
            {
                int height = LineCount(field, fi, item) * RowH;
                _blocks.Add(new Block
                {
                    Field = fi,
                    Item = item,
                    Top = top,
                    Height = height,
                    FirstOfField = item == 0
                });
                top += height;
            }
        }
    }

    /// <summary>The height of the blocks as they are drawn, with no room held for anything pending.</summary>
    private int BlocksHeight() =>
        _blocks.Count == 0 ? 0 : _blocks[_blocks.Count - 1].Top + _blocks[_blocks.Count - 1].Height;

    /// <summary>
    /// The height the inspector scrolls over. A key being named has no block until it is committed,
    /// so while its field is open the panel is one row taller than the blocks in it — otherwise the
    /// scroll that brought the empty name on screen is clamped straight back off it and the field
    /// ends up sitting over the last key instead of under it. The row goes back the moment the edit
    /// ends without adding anything.
    /// </summary>
    private int InspectorHeight() =>
        BlocksHeight() + (_field.Active && _editing == Editing.NewKey ? RowH : 0);

    private static int Columns(JsonField field) => field.IsArray ? ItemCols : ScalarCols;

    private static int ValueColumnX(JsonField field) => field.IsArray ? ItemValueX : ValueX;

    /// <summary>
    /// How many lines a value takes. An open Text edit is measured on what is in the field rather
    /// than on what is still stored, so the block grows and shrinks with the typing and the rows
    /// under it move out of its way instead of being drawn over.
    /// </summary>
    private int LineCount(JsonField field, int fieldIndex, int item)
    {
        if (field.Type != DataValueType.Text) return 1;

        string text = IsEditingValueOf(fieldIndex, item)
            ? _field.Value
            : DataValue.Format(field.Type, field.Values[item]);

        return EditorUI.WrapSpans(text, Columns(field)).Count;
    }

    /// <summary>True while the open field is editing exactly this item's value.</summary>
    private bool IsEditingValueOf(int fieldIndex, int item) =>
        _field.Active && _editing == Editing.Value && fieldIndex == _selField && item == _selItem;

    private JsonField SelectedField() =>
        _inspected != null && _selField >= 0 && _selField < _inspected.Fields.Count
            ? _inspected.Fields[_selField]
            : null;

    private int SelectedRow()
    {
        if (_selected == null) return -1;
        for (int i = 0; i < _rows.Count; i++)
        {
            if (ReferenceEquals(_rows[i].Object ?? (object)_rows[i].Group, _selected)) return i;
        }
        return -1;
    }

    private int SelectedBlock()
    {
        for (int i = 0; i < _blocks.Count; i++)
        {
            if (_blocks[i].Field == _selField && _blocks[i].Item == _selItem) return i;
        }
        return -1;
    }

    private int FirstBlockOfField(int field)
    {
        for (int i = 0; i < _blocks.Count; i++)
        {
            if (_blocks[i].Field == field) return i;
        }
        return -1;
    }

    private void SelectRow(int row)
    {
        if (_rows.Count == 0) return;

        row = Math.Clamp(row, 0, _rows.Count - 1);
        var (group, obj) = _rows[row];

        _selected = obj ?? (object)group;
        _deleteArmed = null;

        // Only an object row repaints the inspector; selecting a group leaves it alone.
        if (obj != null) Inspect(obj);

        EnsureRowVisible();
    }

    private void SelectBlock(int index)
    {
        if (_blocks.Count == 0) return;

        var block = _blocks[Math.Clamp(index, 0, _blocks.Count - 1)];
        _selField = block.Field;
        _selItem = block.Item;
        _deleteArmed = null;
        EnsureBlockVisible();
    }

    private void Inspect(JsonObject obj)
    {
        if (ReferenceEquals(_inspected, obj)) return;

        _inspected = obj;
        _selField = 0;
        _selItem = 0;
        _inspectorScroll = 0;
        RebuildBlocks();
    }

    private void ClampSelection()
    {
        // The object may have been deleted from under the inspector.
        if (_inspected != null && Sheet.OwnerOf(_inspected) == null) Inspect(null);
        if (_selected != null && SelectedRow() < 0) _selected = null;

        // A deleted object is no longer a template for anything, so the copy goes with it.
        if (_copied != null && Sheet.OwnerOf(_copied) == null) _copied = null;

        // Nothing to inspect is nothing to focus. The panel's keys and its [+KEY] both want an
        // object, so a Tab into it — or a focus left behind by the object being deleted — hands
        // straight back to the tree instead of sitting on an empty panel offering a button that
        // could only answer with a toast.
        if (_inspected == null) _focus = Panel.Tree;

        if (_inspected == null || _inspected.Fields.Count == 0)
        {
            _selField = 0;
            _selItem = 0;
            return;
        }

        _selField = Math.Clamp(_selField, 0, _inspected.Fields.Count - 1);
        _selItem = Math.Clamp(_selItem, 0, _inspected.Fields[_selField].Values.Count - 1);
    }

    private void ClampScroll()
    {
        _treeScroll = Math.Clamp(_treeScroll, 0, Math.Max(0, _rows.Count - VisibleRows));
        _inspectorScroll = Math.Clamp(_inspectorScroll, 0, Math.Max(0, InspectorHeight() - ContentH));
    }

    private void EnsureRowVisible()
    {
        int row = SelectedRow();
        if (row < 0) return;

        if (row < _treeScroll) _treeScroll = row;
        else if (row >= _treeScroll + VisibleRows) _treeScroll = row - VisibleRows + 1;

        ClampScroll();
    }

    private void EnsureBlockVisible()
    {
        int index = SelectedBlock();
        if (index < 0) return;

        var block = _blocks[index];
        if (block.Top < _inspectorScroll) _inspectorScroll = block.Top;
        else if (block.Top + block.Height > _inspectorScroll + ContentH)
            _inspectorScroll = block.Top + block.Height - ContentH;

        ClampScroll();
    }

    // ── Draw ──────────────────────────────────────────────────────────────────

    public void Draw()
    {
        RebuildRows();
        RebuildBlocks();
        PositionValueField();

        _api.rectfill(0, ContentTop, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY - 1,
            Constants.Colors.Black);

        DrawTree();
        _treeBar.Draw(_rows.Count, VisibleRows, _treeScroll);
        _api.line(DividerX, ContentTop, DividerX, ContentBottom - 1, Constants.Colors.DarkGray);

        DrawInspector();
        _inspectorBar.Draw(InspectorHeight(), ContentH, _inspectorScroll);

        DrawActions();

        _api.rectfill(0, EditorUI.BottomBarY, Constants.Screen.ResolutionX, Constants.Screen.ResolutionY - 1,
            Constants.Colors.Orange);
        _events.Draw();

        if (_field.Active) _field.Draw();
    }

    private void DrawTree()
    {
        bool renaming = _field.Active && _editing == Editing.NodeName;

        for (int i = 0; i < VisibleRows; i++)
        {
            int row = _treeScroll + i;
            if (row >= _rows.Count) break;

            var (group, obj) = _rows[row];
            object node = obj ?? (object)group;
            int y = ContentTop + i * RowH;
            bool selected = ReferenceEquals(node, _selected);

            if (selected)
            {
                _api.rectfill(0, y, TreeW - 1, y + RowH - 1,
                    _focus == Panel.Tree ? Constants.Colors.White : Constants.Colors.LightGray);
            }

            // The object the inspector is showing keeps a mark of its own, since selecting a group
            // does not change what is on the right.
            if (obj != null && ReferenceEquals(obj, _inspected))
                _api.rectfill(TreeW - 1, y, TreeW - 1, y + RowH - 1, Constants.Colors.Orange);

            int color = selected ? Constants.Colors.Indigo
                : obj == null ? Constants.Colors.White
                : Constants.Colors.LightGray;

            if (obj == null) _api.print(group.Collapsed ? "+" : "-", 0, y + 1, color);
            if (selected && renaming) continue;

            _api.print(obj?.Name ?? group.Name, MarkerW, y + 1, color);
        }
    }

    private void DrawInspector()
    {
        if (_inspected == null)
        {
            _api.print("NO OBJECT", InspX, ContentTop + 1, Constants.Colors.DarkGray);
            return;
        }

        if (_inspected.Fields.Count == 0 && !(_field.Active && _editing == Editing.NewKey))
        {
            _api.print("NO KEYS", InspX, ContentTop + 1, Constants.Colors.DarkGray);
            return;
        }

        for (int i = 0; i < _blocks.Count; i++) DrawBlock(_blocks[i]);
    }

    private void DrawBlock(Block block)
    {
        var field = _inspected.Fields[block.Field];
        int y = ContentTop + block.Top - _inspectorScroll;
        bool fieldSelected = block.Field == _selField;
        bool itemSelected = fieldSelected && block.Item == _selItem;

        if (block.FirstOfField && IsLineVisible(y))
        {
            bool renaming = _field.Active && _editing == Editing.KeyName && fieldSelected;
            if (fieldSelected)
            {
                _api.rectfill(KeyX, y, KeyX + NameW - 1, y + RowH - 1,
                    _focus == Panel.Inspector ? Constants.Colors.White : Constants.Colors.LightGray);
            }

            if (!renaming)
            {
                _api.print(field.Name, KeyX + 1, y + 1,
                    fieldSelected ? Constants.Colors.Indigo : Constants.Colors.White);
            }

            EditorUI.TextButton(_api, new Rectangle(BadgeX, y, BadgeW, RowH), DataValue.Code(field.Type));
        }

        // Every item of an array is numbered, so a value never has to be counted off by eye.
        if (field.IsArray && IsLineVisible(y))
            _api.print(block.Item + ":", ValueX, y + 1, Constants.Colors.DarkGray);

        if (IsEditingValueOf(block.Field, block.Item)) return;

        DrawValue(field, block, y, itemSelected);
    }

    private void DrawValue(JsonField field, Block block, int y, bool selected)
    {
        int x = ValueColumnX(field);
        string stored = field.Values[block.Item];
        bool valid = JsonSheet.IsValid(field, block.Item);

        if (field.Type == DataValueType.Bool && valid)
        {
            if (!IsLineVisible(y)) return;

            string label = DataValue.Format(field.Type, stored);
            EditorUI.TextButton(_api, new Rectangle(x, y, Text.Width(label) + 3, RowH), label);
            return;
        }

        // A value that no longer reads as its field's type is kept and shown as it is stored, never
        // erased and never dressed up — a 12 left over from an Int has to read as 12, not as FALSE.
        // What marks it is the row behind it rather than the text on it: red under a value the eye
        // finds at a glance, where red text has to be read before it says anything, and a long one
        // wraps its warning over several lines.
        int color = selected ? Constants.Colors.White : Constants.Colors.LightGray;
        bool filled = selected || !valid;
        int background = valid ? Constants.Colors.DarkBlue : Constants.Colors.Red;

        // A Text value is the only one whose case the developer chose and the file keeps, so it is
        // the only one drawn as it is stored. A number, a position and a bool have no case to lose.
        bool cased = field.Type == DataValueType.Text;

        var lines = EditorUI.Wrap(valid ? DataValue.Format(field.Type, stored) : stored, Columns(field));
        for (int i = 0; i < lines.Count; i++)
        {
            int lineY = y + i * RowH;
            if (!IsLineVisible(lineY)) continue;

            if (filled) _api.rectfill(x, lineY, InspRight - 1, lineY + RowH - 1, background);

            if (cased) EditorUI.PrintCased(lines[i], x, lineY + 1, color);
            else _api.print(lines[i], x, lineY + 1, color);
        }
    }

    /// <summary>
    /// Nothing clips, so a line only draws when all 9 of its pixels are inside the panel — the
    /// alternative is text bleeding over the menu bar and the action row.
    /// </summary>
    private static bool IsLineVisible(int y) => y >= ContentTop && y + RowH <= ContentBottom;

    private void DrawActions()
    {
        _api.rectfill(0, ActionY, Constants.Screen.ResolutionX, ActionY + ActionH - 1, Constants.Colors.DarkGray);

        var set = ActionSet();
        for (int i = 0; i < set.Length; i++) EditorUI.TextButton(_api, ActionRect(set, i), set[i]);

        string path = Path();
        if (path.Length == 0) return;

        _api.print(path, Constants.Screen.ResolutionX - Text.Width(path) - 2, ActionY + 1, Constants.Colors.LightGray);
    }

    private string Path()
    {
        if (_inspected == null) return string.Empty;

        string path = NamePath(_inspected);
        var field = SelectedField();
        return field == null ? path : path + "/" + field.Name;
    }

    /// <summary>
    /// An object written <c>GROUP/OBJECT</c>. Object names are only unique within their group, so
    /// this is what it takes to name one that is not the row under the cursor.
    /// </summary>
    private string NamePath(JsonObject obj)
    {
        if (obj == null) return string.Empty;

        var group = Sheet.OwnerOf(obj);
        return (group == null ? string.Empty : group.Name + "/") + obj.Name;
    }

    /// <summary>
    /// The buttons the current focus offers. A button with nothing under it to act on is left out
    /// rather than left sitting there to answer with a toast: [+ITM] and [-ITM] want an item of an
    /// array, [+OBJ] [REN] [DEL] want a group to have been made first, and the inspector's
    /// [REN] [DEL] [ARR] want a key. Leaving them out also keeps the row from claiming width the
    /// buttons that do mean something could use.
    /// </summary>
    private string[] ActionSet()
    {
        if (_focus == Panel.Tree) return TreeActionSet();

        var field = SelectedField();
        if (field == null) return EmptyInspectorActions;

        return field.IsArray ? ArrayActions : ScalarActions;
    }

    /// <summary>
    /// What the tree offers on top of its four: [COPY] takes the selected object as a template, and
    /// [DPCK] [DPCA] put one down once there is a template to put — under the selected group, or
    /// beside the selected object. Only an object is worth copying, so a group offers the paste
    /// pair and not [COPY].
    /// <para>
    /// A name being typed offers none of them. The row is about the name that is selected, and a
    /// name half-way through being edited is not yet the one it is going to be.
    /// </para>
    /// </summary>
    private string[] TreeActionSet()
    {
        if (_rows.Count == 0) return EmptyTreeActions;
        if (_selected == null || _field.Active) return TreeActions;

        bool copy = _selected is JsonObject;
        bool paste = _copied != null;

        if (copy) return paste ? TreeCopyPasteActions : TreeCopyActions;
        return paste ? TreePasteActions : TreeActions;
    }

    private static Rectangle ActionRect(string[] set, int index)
    {
        int x = 2;
        for (int i = 0; i < index; i++) x += ActionWidth(set[i]) + 2;
        return new Rectangle(x, ActionY, ActionWidth(set[index]), ActionH);
    }

    private static int ActionWidth(string label) => Text.Width(label) + 3;
}
