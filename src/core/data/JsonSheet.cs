namespace mono8.core.data;

/// <summary>One key of an object: a name, a declared type, and one or more values of that type.</summary>
internal sealed class JsonField
{
    public string Name;
    public DataValueType Type;
    public bool IsArray;

    /// <summary>Normalised stored forms. Exactly one entry when <see cref="IsArray"/> is false.</summary>
    public readonly List<string> Values = new List<string>();

    public JsonField(string name, DataValueType type, bool isArray)
    {
        Name = name;
        Type = type;
        IsArray = isArray;
    }
}

internal sealed class JsonObject
{
    public string Name;
    public readonly List<JsonField> Fields = new List<JsonField>();

    public JsonObject(string name) => Name = name;
}

internal sealed class JsonGroup
{
    public string Name;
    public readonly List<JsonObject> Objects = new List<JsonObject>();

    /// <summary>Fold state for the tree panel. UI state, kept here for convenience; never persisted.</summary>
    public bool Collapsed = false;

    public JsonGroup(string name) => Name = name;
}

/// <summary>
/// Editable, mutable bank of the authored <c>data.json</c> tree, and the sole parser and writer of
/// that format — the same shape <see cref="SfxSheet"/> has for <c>data.sfx</c>.
/// <para>
/// The tree is fixed at three levels (group → object → field); there is no recursion and no node
/// type. A field's type is encoded as a suffix on its key (<c>"HP:i"</c>) because the JSON value
/// alone cannot tell Decimal from Money, String from Text, or PosXY from a two-int array.
/// </para>
/// <para>
/// Loading is deliberately forgiving, like <see cref="SfxSheet.LoadSfxs"/>: an unknown type code, a
/// name that breaks <see cref="JsonNames"/>, a duplicate, a count over the limits or an unreadable
/// value drops that node and the load carries on. Nothing here throws.
/// </para>
/// </summary>
internal sealed class JsonSheet
{
    private readonly List<JsonGroup> _groups = new List<JsonGroup>();

    public IReadOnlyList<JsonGroup> Groups => _groups;

    /// <summary>True when the in-memory sheet differs from what is on disk.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>True when the last load dropped or repaired something — the editor's LOAD FIX toast.</summary>
    public bool HadLoadIssues { get; private set; }

    // ── Load / Save ───────────────────────────────────────────────────────────

    public void Load(string path)
    {
        _groups.Clear();
        IsDirty = false;
        HadLoadIssues = false;

        string text = FileIO.Read(Constants.File.Name, Constants.File.Extensions.Json, path);
        if (string.IsNullOrWhiteSpace(text)) return;   // a missing or empty file is an empty sheet

        RawValue root;
        try
        {
            root = new JsonReader(text).ReadDocument();
        }
        catch (FormatException)
        {
            // Structurally broken beyond recovery: start empty rather than guess at the user's
            // intent, and leave the file alone until something is actually edited.
            _groups.Clear();
            HadLoadIssues = true;
            return;
        }

        Build(root);

        // Whatever was repaired on the way in only reaches the file on the next save.
        if (HadLoadIssues) IsDirty = true;
    }

    public void Save(string path)
    {
        FileIO.Write(Constants.File.Name, Constants.File.Extensions.Json, ToJson(), path);
        IsDirty = false;
    }

    // ── Lookup ────────────────────────────────────────────────────────────────

    public JsonGroup FindGroup(string name)
    {
        foreach (var g in _groups)
        {
            if (g.Name == name) return g;
        }
        return null;
    }

    public static JsonObject FindObject(JsonGroup group, string name)
    {
        if (group == null) return null;
        foreach (var o in group.Objects)
        {
            if (o.Name == name) return o;
        }
        return null;
    }

    public static JsonField FindField(JsonObject obj, string name)
    {
        if (obj == null) return null;
        foreach (var f in obj.Fields)
        {
            if (f.Name == name) return f;
        }
        return null;
    }

    /// <summary>The group holding <paramref name="obj"/>, or null when it is not in this sheet.</summary>
    public JsonGroup OwnerOf(JsonObject obj)
    {
        if (obj == null) return null;
        foreach (var g in _groups)
        {
            if (g.Objects.Contains(obj)) return g;
        }
        return null;
    }

    /// <summary>The object holding <paramref name="field"/>, or null when it is not in this sheet.</summary>
    public JsonObject OwnerOf(JsonField field)
    {
        if (field == null) return null;
        foreach (var g in _groups)
        {
            foreach (var o in g.Objects)
            {
                if (o.Fields.Contains(field)) return o;
            }
        }
        return null;
    }

    // ── Structure editing ─────────────────────────────────────────────────────

    public bool TryAddGroup(string name, out JsonGroup group)
    {
        group = null;
        if (_groups.Count >= Constants.JsonData.MaxGroups) return false;
        if (!JsonNames.TryNormalize(name, out string n)) return false;
        if (FindGroup(n) != null) return false;

        group = new JsonGroup(n);
        _groups.Add(group);
        IsDirty = true;
        return true;
    }

    public bool TryAddObject(JsonGroup group, string name, out JsonObject obj)
    {
        obj = null;
        if (group == null || !_groups.Contains(group)) return false;
        if (group.Objects.Count >= Constants.JsonData.MaxObjectsPerGrp) return false;
        if (!JsonNames.TryNormalize(name, out string n)) return false;
        if (FindObject(group, n) != null) return false;

        obj = new JsonObject(n);
        group.Objects.Add(obj);
        IsDirty = true;
        return true;
    }

    public bool TryAddField(JsonObject obj, string name, DataValueType type, bool isArray, out JsonField field)
    {
        field = null;
        if (obj == null || OwnerOf(obj) == null) return false;
        if (obj.Fields.Count >= Constants.JsonData.MaxFieldsPerObj) return false;
        if (!JsonNames.TryNormalize(name, out string n)) return false;
        if (FindField(obj, n) != null) return false;

        field = new JsonField(n, type, isArray);
        field.Values.Add(DataValue.Default(type));   // item 0 always exists, so a new field is never blank
        obj.Fields.Add(field);
        IsDirty = true;
        return true;
    }

    /// <summary>Renames a group, object or field, rejecting a name that clashes with a sibling.</summary>
    public bool TryRename(object node, string name)
    {
        if (!JsonNames.TryNormalize(name, out string n)) return false;

        switch (node)
        {
            case JsonGroup g:
            {
                if (!_groups.Contains(g)) return false;
                var clash = FindGroup(n);
                if (clash != null && clash != g) return false;
                g.Name = n;
                break;
            }
            case JsonObject o:
            {
                var owner = OwnerOf(o);
                if (owner == null) return false;
                var clash = FindObject(owner, n);
                if (clash != null && clash != o) return false;
                o.Name = n;
                break;
            }
            case JsonField f:
            {
                var owner = OwnerOf(f);
                if (owner == null) return false;
                var clash = FindField(owner, n);
                if (clash != null && clash != f) return false;
                f.Name = n;
                break;
            }
            default:
                return false;
        }

        IsDirty = true;
        return true;
    }

    /// <summary>Removes a group, object or field along with everything under it.</summary>
    public void Remove(object node)
    {
        switch (node)
        {
            case JsonGroup g:
                if (_groups.Remove(g)) IsDirty = true;
                break;
            case JsonObject o:
                if (OwnerOf(o) is JsonGroup owner && owner.Objects.Remove(o)) IsDirty = true;
                break;
            case JsonField f:
                if (OwnerOf(f) is JsonObject obj && obj.Fields.Remove(f)) IsDirty = true;
                break;
        }
    }

    // ── Value editing ─────────────────────────────────────────────────────────

    /// <summary>Commits a raw entry to one slot, normalising it first; false leaves the old value in place.</summary>
    public bool TrySetValue(JsonField field, int index, string raw)
    {
        if (field == null || index < 0 || index >= field.Values.Count) return false;
        if (!DataValue.TryNormalize(field.Type, raw, out string normalized)) return false;

        if (field.Values[index] != normalized)
        {
            field.Values[index] = normalized;
            IsDirty = true;
        }
        return true;
    }

    /// <summary>
    /// Inserts a default item at <paramref name="index"/>, which is clamped into the list rather
    /// than rejected, so an out-of-range caller appends instead of silently doing nothing.
    /// </summary>
    public bool TryAddItem(JsonField field, int index)
    {
        if (field == null || !field.IsArray) return false;
        if (field.Values.Count >= Constants.JsonData.MaxArrayItems) return false;

        field.Values.Insert(Math.Clamp(index, 0, field.Values.Count), DataValue.Default(field.Type));
        IsDirty = true;
        return true;
    }

    public void RemoveItem(JsonField field, int index)
    {
        if (field == null || !field.IsArray) return;
        if (index < 0 || index >= field.Values.Count) return;

        field.Values.RemoveAt(index);
        IsDirty = true;
    }

    /// <summary>
    /// Re-declares a field's type, keeping every value exactly as it is. A value that does not read
    /// as the new type is deliberately left alone rather than dropped or defaulted: it is the user's
    /// data, the editor draws it in red, and <see cref="TryFindInvalid"/> holds the save back until
    /// it is dealt with.
    /// </summary>
    public void SetType(JsonField field, DataValueType type)
    {
        if (field == null || field.Type == type) return;

        field.Type = type;
        IsDirty = true;
    }

    /// <summary>
    /// Switches a field between one value and a list of them. Collapsing keeps item 0 and returns
    /// true when that dropped anything, so the editor can say so.
    /// </summary>
    public bool SetIsArray(JsonField field, bool isArray)
    {
        if (field == null || field.IsArray == isArray) return false;

        field.IsArray = isArray;
        IsDirty = true;

        if (isArray || field.Values.Count <= 1) return false;

        field.Values.RemoveRange(1, field.Values.Count - 1);
        return true;
    }

    /// <summary>True when the slot holds something that reads back as the field's declared type.</summary>
    public static bool IsValid(JsonField field, int index)
    {
        if (field == null || index < 0 || index >= field.Values.Count) return false;
        return DataValue.TryNormalize(field.Type, field.Values[index], out _);
    }

    /// <summary>
    /// The first value in the sheet that no longer reads as its field's type — the one thing that
    /// stops the editor saving, since writing it out would produce a file that will not load back.
    /// </summary>
    public bool TryFindInvalid(out JsonObject obj, out JsonField field, out int index)
    {
        foreach (var g in _groups)
        {
            foreach (var o in g.Objects)
            {
                foreach (var f in o.Fields)
                {
                    for (int i = 0; i < f.Values.Count; i++)
                    {
                        if (IsValid(f, i)) continue;

                        obj = o;
                        field = f;
                        index = i;
                        return true;
                    }
                }
            }
        }

        obj = null;
        field = null;
        index = -1;
        return false;
    }

    // ── Serializer ────────────────────────────────────────────────────────────

    /// <summary>
    /// Pretty-printed with a 2-space indent and in insertion order, so the file diffs stably and
    /// re-reading its own output reproduces this text byte for byte.
    /// </summary>
    public string ToJson()
    {
        if (_groups.Count == 0) return "{}\n";

        var sb = new System.Text.StringBuilder();
        sb.Append("{\n");

        for (int gi = 0; gi < _groups.Count; gi++)
        {
            var g = _groups[gi];
            sb.Append("  ").Append(Quote(g.Name)).Append(": {");

            for (int oi = 0; oi < g.Objects.Count; oi++)
            {
                var o = g.Objects[oi];
                sb.Append(oi == 0 ? "\n" : ",\n");
                sb.Append("    ").Append(Quote(o.Name)).Append(": {");

                for (int fi = 0; fi < o.Fields.Count; fi++)
                {
                    var f = o.Fields[fi];
                    sb.Append(fi == 0 ? "\n" : ",\n");
                    sb.Append("      ").Append(Quote(f.Name + ":" + DataValue.Code(f.Type))).Append(": ");
                    AppendValue(sb, f);
                }

                sb.Append(o.Fields.Count == 0 ? "}" : "\n    }");
            }

            sb.Append(g.Objects.Count == 0 ? "}" : "\n  }");
            sb.Append(gi == _groups.Count - 1 ? "\n" : ",\n");
        }

        sb.Append("}\n");
        return sb.ToString();
    }

    private static void AppendValue(System.Text.StringBuilder sb, JsonField field)
    {
        if (!field.IsArray)
        {
            AppendItem(sb, field.Type, field.Values.Count > 0 ? field.Values[0] : DataValue.Default(field.Type));
            return;
        }

        sb.Append('[');
        for (int i = 0; i < field.Values.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            AppendItem(sb, field.Type, field.Values[i]);
        }
        sb.Append(']');
    }

    private static void AppendItem(System.Text.StringBuilder sb, DataValueType type, string stored)
    {
        // Re-normalising is a no-op on a stored value; it is here so a value that somehow escaped
        // validation can never produce a file that will not parse.
        if (!DataValue.TryNormalize(type, stored, out string value)) value = DataValue.Default(type);

        if (type == DataValueType.PosXY)
        {
            int comma = value.IndexOf(',');
            sb.Append('[').Append(value, 0, comma).Append(", ").Append(value, comma + 1, value.Length - comma - 1).Append(']');
            return;
        }

        if (DataValue.IsQuoted(type)) sb.Append(Quote(value));
        else sb.Append(value);
    }

    /// <summary>
    /// Only <c>"</c> and <c>\</c> need escaping: every other typeable character is in the font atlas
    /// (<see cref="Text.Sanitize"/> guarantees it) and none of those are JSON control characters.
    /// </summary>
    private static string Quote(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (char c in s)
        {
            if (c == '"' || c == '\\') sb.Append('\\');
            sb.Append(c);
        }
        sb.Append('"');
        return sb.ToString();
    }

    // ── Parser ────────────────────────────────────────────────────────────────

    private void Build(RawValue root)
    {
        if (root.Kind != RawKind.Object)
        {
            HadLoadIssues = true;
            return;
        }

        foreach (var member in root.Members)
        {
            if (_groups.Count >= Constants.JsonData.MaxGroups) { HadLoadIssues = true; break; }

            if (!JsonNames.TryNormalize(member.Key, out string name)
                || member.Value.Kind != RawKind.Object
                || FindGroup(name) != null)
            {
                HadLoadIssues = true;
                continue;
            }

            if (name != member.Key) HadLoadIssues = true;   // repaired on the way in

            var group = new JsonGroup(name);
            _groups.Add(group);
            BuildObjects(group, member.Value);
        }
    }

    private void BuildObjects(JsonGroup group, RawValue raw)
    {
        foreach (var member in raw.Members)
        {
            if (group.Objects.Count >= Constants.JsonData.MaxObjectsPerGrp) { HadLoadIssues = true; break; }

            if (!JsonNames.TryNormalize(member.Key, out string name)
                || member.Value.Kind != RawKind.Object
                || FindObject(group, name) != null)
            {
                HadLoadIssues = true;
                continue;
            }

            if (name != member.Key) HadLoadIssues = true;

            var obj = new JsonObject(name);
            group.Objects.Add(obj);
            BuildFields(obj, member.Value);
        }
    }

    private void BuildFields(JsonObject obj, RawValue raw)
    {
        foreach (var member in raw.Members)
        {
            if (obj.Fields.Count >= Constants.JsonData.MaxFieldsPerObj) { HadLoadIssues = true; break; }
            BuildField(obj, member.Key, member.Value);
        }
    }

    private void BuildField(JsonObject obj, string key, RawValue raw)
    {
        int separator = key.LastIndexOf(':');
        if (separator <= 0 || separator == key.Length - 1) { HadLoadIssues = true; return; }

        if (!JsonNames.TryNormalize(key.Substring(0, separator), out string name)
            || !DataValue.TryParse(key.Substring(separator + 1), out DataValueType type)
            || FindField(obj, name) != null)
        {
            HadLoadIssues = true;
            return;
        }

        if (key != name + ":" + DataValue.Code(type)) HadLoadIssues = true;

        bool isArray;
        List<RawValue> items;

        if (raw.Kind == RawKind.Array)
        {
            // A PosXY is itself written [x, y], so a flat array is one position while an array of
            // positions nests: [[8, 8], [8, 40]]. Every other type writes a scalar bare.
            if (type == DataValueType.PosXY && raw.Items.Count > 0 && raw.Items[0].Kind != RawKind.Array)
            {
                isArray = false;
                items = new List<RawValue> { raw };
            }
            else
            {
                isArray = true;
                items = raw.Items;
            }
        }
        else
        {
            isArray = false;
            items = new List<RawValue> { raw };
        }

        var field = new JsonField(name, type, isArray);

        foreach (var item in items)
        {
            if (field.Values.Count >= Constants.JsonData.MaxArrayItems) { HadLoadIssues = true; break; }

            if (!TryItemText(type, item, out string text)
                || !DataValue.TryNormalize(type, text, out string normalized))
            {
                HadLoadIssues = true;
                continue;
            }

            if (normalized != text) HadLoadIssues = true;
            field.Values.Add(normalized);
        }

        if (!isArray && field.Values.Count == 0) { HadLoadIssues = true; return; }   // unreadable scalar

        obj.Fields.Add(field);
    }

    /// <summary>Flattens one parsed item into the raw text <see cref="DataValue.TryNormalize"/> expects.</summary>
    private static bool TryItemText(DataValueType type, RawValue item, out string text)
    {
        text = string.Empty;

        if (type == DataValueType.PosXY && item.Kind == RawKind.Array)
        {
            if (item.Items.Count != 2) return false;
            if (item.Items[0].Kind != RawKind.Number || item.Items[1].Kind != RawKind.Number) return false;
            text = item.Items[0].Literal + "," + item.Items[1].Literal;
            return true;
        }

        switch (item.Kind)
        {
            case RawKind.String:
            case RawKind.Number:
            case RawKind.Bool:
                text = item.Literal;
                return true;
            default:
                return false;   // an object, an array or null where a value belongs
        }
    }

    private enum RawKind { Object, Array, String, Number, Bool, Null }

    /// <summary>A node exactly as it appears in the file, before any of the rules are applied to it.</summary>
    private sealed class RawValue
    {
        public RawKind Kind;
        public string Literal = string.Empty;
        public List<RawValue> Items;
        public List<KeyValuePair<string, RawValue>> Members;
    }

    /// <summary>
    /// Recursive-descent reader for the subset of JSON this format uses. Hand-rolled rather than
    /// taken from System.Text.Json so the format stays exactly what the writer above produces.
    /// Throws <see cref="FormatException"/> on anything structurally broken; <see cref="Load"/> is
    /// the only caller and turns that into an empty sheet.
    /// </summary>
    private sealed class JsonReader
    {
        private const int MaxDepth = 8;   // root + group + object + PosXY array of arrays, with room to spare

        private readonly string _s;
        private int _i;

        public JsonReader(string s) => _s = s ?? string.Empty;

        public RawValue ReadDocument()
        {
            var value = ReadValue(0);
            SkipWhitespace();
            if (_i < _s.Length) throw new FormatException("content after the root object");
            return value;
        }

        private RawValue ReadValue(int depth)
        {
            if (depth > MaxDepth) throw new FormatException("nested too deep");

            SkipWhitespace();
            switch (Peek())
            {
                case '{': return ReadObject(depth);
                case '[': return ReadArray(depth);
                case '"': return new RawValue { Kind = RawKind.String, Literal = ReadString() };
                case 't': Expect("true"); return new RawValue { Kind = RawKind.Bool, Literal = "true" };
                case 'f': Expect("false"); return new RawValue { Kind = RawKind.Bool, Literal = "false" };
                case 'n': Expect("null"); return new RawValue { Kind = RawKind.Null };
                default: return ReadNumber();
            }
        }

        private RawValue ReadObject(int depth)
        {
            _i++;   // '{'
            var value = new RawValue { Kind = RawKind.Object, Members = new List<KeyValuePair<string, RawValue>>() };

            SkipWhitespace();
            if (Peek() == '}') { _i++; return value; }

            while (true)
            {
                SkipWhitespace();
                if (Peek() != '"') throw new FormatException("expected a key");
                string key = ReadString();

                SkipWhitespace();
                if (Peek() != ':') throw new FormatException("expected ':'");
                _i++;

                value.Members.Add(new KeyValuePair<string, RawValue>(key, ReadValue(depth + 1)));

                SkipWhitespace();
                char c = Peek();
                _i++;
                if (c == ',') continue;
                if (c == '}') return value;
                throw new FormatException("expected ',' or '}'");
            }
        }

        private RawValue ReadArray(int depth)
        {
            _i++;   // '['
            var value = new RawValue { Kind = RawKind.Array, Items = new List<RawValue>() };

            SkipWhitespace();
            if (Peek() == ']') { _i++; return value; }

            while (true)
            {
                value.Items.Add(ReadValue(depth + 1));

                SkipWhitespace();
                char c = Peek();
                _i++;
                if (c == ',') continue;
                if (c == ']') return value;
                throw new FormatException("expected ',' or ']'");
            }
        }

        private string ReadString()
        {
            _i++;   // '"'
            var sb = new System.Text.StringBuilder();

            while (true)
            {
                char c = Peek();
                _i++;

                if (c == '"') return sb.ToString();

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                char escape = Peek();
                _i++;
                switch (escape)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (_i + 4 > _s.Length) throw new FormatException("truncated \\u escape");
                        if (!int.TryParse(_s.Substring(_i, 4), System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture, out int code))
                            throw new FormatException("bad \\u escape");
                        sb.Append((char)code);
                        _i += 4;
                        break;
                    default: throw new FormatException("unknown escape");
                }
            }
        }

        private RawValue ReadNumber()
        {
            int start = _i;
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if (char.IsDigit(c) || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') _i++;
                else break;
            }

            string literal = _s.Substring(start, _i - start);
            if (literal.Length == 0
                || !double.TryParse(literal, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                throw new FormatException("expected a value");

            return new RawValue { Kind = RawKind.Number, Literal = literal };
        }

        private void Expect(string word)
        {
            if (_i + word.Length > _s.Length || string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0)
                throw new FormatException("expected " + word);
            _i += word.Length;
        }

        private char Peek()
        {
            if (_i >= _s.Length) throw new FormatException("unexpected end of file");
            return _s[_i];
        }

        private void SkipWhitespace()
        {
            while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
        }
    }
}
