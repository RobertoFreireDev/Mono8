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
internal sealed class JsonEditor : IEditor
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

    private const int NameW = 32;         // 8 characters
    private const int KeyX = InspX;
    private const int BadgeX = 79;
    private const int BadgeW = 7;
    private const int ValueX = 87;
    private const int ItemLabelW = 16;    // "63:" plus a gap
    private const int ItemValueX = ValueX + ItemLabelW;

    /// <summary>Characters that fit on one line of a scalar's value area, and of an array item's.</summary>
    private const int ScalarCols = (InspRight - ValueX) / Text.CharAdvance;       // 40
    private const int ItemCols = (InspRight - ItemValueX) / Text.CharAdvance;     // 36

    private const int ActionY = 126;
    private const int ActionH = 9;

    private const float DeleteArmSeconds = 2f;
    private const float DoubleClickSeconds = 0.4f;

    private static readonly int TypeCount = Enum.GetValues<DataValueType>().Length;

    private static readonly string[] TreeActions = { "+GRP", "+OBJ", "REN", "DEL" };
    private static readonly string[] InspectorActions = { "+KEY", "REN", "DEL", "ARR", "+ITM", "-ITM" };

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
    private int _selField;
    private int _selItem;

    private int _treeScroll;           // whole rows
    private int _inspectorScroll;      // pixels

    private object _deleteArmed;
    private float _deleteArmLeft;

    private object _clickTarget;       // the node or field the last name click landed on
    private float _clickLeft;

    public JsonEditor(IMono8API api)
    {
        _api = api;
        _events = new EventNotifier(api, 2f, 1, EditorUI.BottomBarY + 1);
        _treeBar = new ScrollBar(api, new Rectangle(TreeScrollX, ContentTop, ScrollW, ContentH));
        _inspectorBar = new ScrollBar(api, new Rectangle(InspScrollX, ContentTop, ScrollW, ContentH));
        _field = new TextField(api);
    }

    private static JsonSheet Sheet => Mono8API.JsonSheet;

    public void Init()
    {
        RebuildRows();
        if (_selected == null && _rows.Count > 0) SelectRow(0);
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
        if (!_field.Update(out string committed, out bool cancelled)) return;

        var mode = _editing;
        bool advance = _field.Advance;
        _editing = Editing.None;
        if (cancelled) return;

        switch (mode)
        {
            case Editing.NodeName:
                if (!Sheet.TryRename(_selected, committed)) _events.AddEvent("DUP NAME");
                break;

            case Editing.KeyName:
                if (!Sheet.TryRename(SelectedField(), committed)) _events.AddEvent("DUP KEY");
                break;

            case Editing.NewKey:
                if (Sheet.TryAddField(_inspected, committed, DataValueType.String, false, out JsonField added))
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

        if (field.Type == DataValueType.Bool) ToggleBool(field, block.Item);
        else BeginValueEdit();
    }

    private void UpdateActions((int x, int y) mouse)
    {
        var set = _focus == Panel.Tree ? TreeActions : InspectorActions;
        for (int i = 0; i < set.Length; i++)
        {
            if (!ActionRect(set, i).Contains(mouse.x, mouse.y)) continue;

            _events.SetHover(ActionHint(set[i]));
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
            case "+KEY": BeginNewKey(); break;
            case "REN": if (_focus == Panel.Tree) BeginNodeRename(); else BeginKeyRename(); break;
            case "DEL": if (_focus == Panel.Tree) DeleteNode(); else DeleteField(); break;
            case "ARR": ToggleArray(); break;
            case "+ITM": AddItem(); break;
            case "-ITM": RemoveItem(); break;
        }
    }

    private static string ActionHint(string label) => label switch
    {
        "+GRP" => "NEW GROUP",
        "+OBJ" => "NEW OBJECT",
        "+KEY" => "NEW KEY",
        "REN" => "RENAME [R]",
        "DEL" => "DELETE",
        "ARR" => "SCALAR / ARRAY",
        "+ITM" => "ADD ITEM",
        _ => "REMOVE ITEM"
    };

    /// <summary>
    /// What the one-character type badge stands for. PosXY says more than its name because its
    /// shape — two ints and a comma — is the one thing here that a name alone does not give away:
    /// the position it already holds if that reads back, and an example if it does not. String and
    /// Text differ only in how much they hold, so each carries its cap — otherwise the pair reads
    /// as two names for the same thing until an entry stops taking characters.
    /// </summary>
    private static string TypeHint(JsonField field, int item)
    {
        string label = TypeLabel(field.Type);
        switch (field.Type)
        {
            case DataValueType.String:
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
        DataValueType.String => "STRING",
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
        var group = _selected as JsonGroup ?? Sheet.OwnerOf(_selected as JsonObject);
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

    private void DeleteField()
    {
        var field = SelectedField();
        if (field == null || !ConfirmDelete(field)) return;

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

    private void AddItem()
    {
        var field = SelectedField();
        if (field == null) return;

        if (!field.IsArray)
        {
            _events.AddEvent("NOT ARRAY");
            return;
        }

        if (!Sheet.TryAddItem(field))
        {
            _events.AddEvent("MAX ITM");
            return;
        }

        _selItem = field.Values.Count - 1;
        RebuildBlocks();
        EnsureBlockVisible();
    }

    private void RemoveItem()
    {
        var field = SelectedField();
        if (field == null) return;

        if (!field.IsArray)
        {
            _events.AddEvent("NOT ARRAY");
            return;
        }

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
        if (_inspected == null)
        {
            _events.AddEvent("NO OBJECT");
            return;
        }

        if (_inspected.Fields.Count >= Constants.JsonData.MaxFieldsPerObj)
        {
            _events.AddEvent("MAX KEY");
            return;
        }

        // The new key lands one row past the last block, so scroll far enough to show it.
        int top = InspectorHeight();
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
        _field.Begin(new Rectangle(x, y, InspRight - x, RowH), field.Values[_selItem],
            field.Type, DataValue.MaxLength(field.Type));
        _editing = Editing.Value;
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
                int height = LineCount(field, item) * RowH;
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

    private int InspectorHeight() =>
        _blocks.Count == 0 ? 0 : _blocks[_blocks.Count - 1].Top + _blocks[_blocks.Count - 1].Height;

    private static int Columns(JsonField field) => field.IsArray ? ItemCols : ScalarCols;

    private static int ValueColumnX(JsonField field) => field.IsArray ? ItemValueX : ValueX;

    private static int LineCount(JsonField field, int item) =>
        field.Type == DataValueType.Text
            ? Wrap(DataValue.Format(field.Type, field.Values[item]), Columns(field)).Count
            : 1;

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

    /// <summary>
    /// Word-wraps to <paramref name="columns"/> characters, breaking mid-word only for a run that
    /// cannot fit a line on its own. Always returns at least one line.
    /// </summary>
    private static List<string> Wrap(string s, int columns)
    {
        var lines = new List<string>();
        if (columns <= 0 || string.IsNullOrEmpty(s))
        {
            lines.Add(s ?? string.Empty);
            return lines;
        }

        int i = 0;
        while (i < s.Length)
        {
            if (s.Length - i <= columns)
            {
                lines.Add(s.Substring(i));
                break;
            }

            int space = s.LastIndexOf(' ', i + columns, columns + 1);
            int take = space > i ? space - i : columns;
            lines.Add(s.Substring(i, take));

            i += take;
            while (i < s.Length && s[i] == ' ') i++;
        }

        return lines;
    }

    // ── Draw ──────────────────────────────────────────────────────────────────

    public void Draw()
    {
        RebuildRows();
        RebuildBlocks();

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

        if (_field.Active && _editing == Editing.Value && itemSelected) return;

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
        int color = !valid ? Constants.Colors.Red
            : selected ? Constants.Colors.White
            : Constants.Colors.LightGray;

        var lines = Wrap(valid ? DataValue.Format(field.Type, stored) : stored, Columns(field));
        for (int i = 0; i < lines.Count; i++)
        {
            int lineY = y + i * RowH;
            if (!IsLineVisible(lineY)) continue;

            if (selected) _api.rectfill(x, lineY, InspRight - 1, lineY + RowH - 1, Constants.Colors.DarkBlue);
            _api.print(lines[i], x, lineY + 1, color);
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

        var set = _focus == Panel.Tree ? TreeActions : InspectorActions;
        for (int i = 0; i < set.Length; i++) EditorUI.TextButton(_api, ActionRect(set, i), set[i]);

        string path = Path();
        if (path.Length == 0) return;

        _api.print(path, Constants.Screen.ResolutionX - Text.Width(path) - 2, ActionY + 1, Constants.Colors.LightGray);
    }

    private string Path()
    {
        if (_inspected == null) return string.Empty;

        var group = Sheet.OwnerOf(_inspected);
        string path = (group == null ? string.Empty : group.Name + "/") + _inspected.Name;

        var field = SelectedField();
        return field == null ? path : path + "/" + field.Name;
    }

    private static Rectangle ActionRect(string[] set, int index)
    {
        int x = 2;
        for (int i = 0; i < index; i++) x += ActionWidth(set[i]) + 2;
        return new Rectangle(x, ActionY, ActionWidth(set[index]), ActionH);
    }

    private static int ActionWidth(string label) => Text.Width(label) + 3;
}
