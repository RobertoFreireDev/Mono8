using System.Globalization;

namespace mono8.core.data;

/// <summary>
/// One authored object, compiled into typed storage the game reads without a cast, a parse or an
/// allocation.
/// <para>
/// <see cref="JsonSheet"/> keeps every value as a string because the editor has to hold half-typed
/// entries. That is the wrong shape for a game loop, so the sheet is compiled into this once at
/// load and again after each save. Values live in one array per runtime type — never
/// <c>object[]</c> — and a field's <see cref="Slot"/> says which array, where in it, and how many
/// items. <see cref="Slot"/> is a struct in the dictionary, so even a lookup boxes nothing.
/// </para>
/// <para>
/// Nothing here throws. A missing field, an index past the end or a getter that does not match the
/// field's declared type returns the fallback, because a data typo should not take the game down
/// with it; <see cref="Has"/>, <see cref="TypeOf"/> and <see cref="Count"/> are there to check
/// first when it matters.
/// </para>
/// </summary>
public sealed class Mono8JsonObject
{
    /// <summary>Which backing array a field's values live in.</summary>
    private enum Store { Int, Dec, Money, Bool, Str, Xy }

    /// <summary>Where one field's values are: the array is implied by <see cref="Type"/>.</summary>
    private readonly struct Slot
    {
        public readonly DataValueType Type;
        public readonly int Start;      // first item, counted in items within its own array
        public readonly int Count;      // 1 for a scalar
        public readonly bool IsArray;

        public Slot(DataValueType type, int start, int count, bool isArray)
        {
            Type = type;
            Start = start;
            Count = count;
            IsArray = isArray;
        }
    }

    /// <summary>Ints per PosXY item in <see cref="_xy"/>.</summary>
    private const int XyStride = 2;

    private readonly int[] _ints;
    private readonly double[] _decs;
    private readonly decimal[] _moneys;
    private readonly bool[] _bools;
    private readonly string[] _strs;
    private readonly int[] _xy;
    private readonly Dictionary<string, Slot> _index;

    private Mono8JsonObject(int[] ints, double[] decs, decimal[] moneys, bool[] bools,
        string[] strs, int[] xy, Dictionary<string, Slot> index)
    {
        _ints = ints;
        _decs = decs;
        _moneys = moneys;
        _bools = bools;
        _strs = strs;
        _xy = xy;
        _index = index;
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles one authored object. Counted first, then filled, so each array is exactly the size
    /// it needs and the whole object is six allocations plus its index.
    /// </summary>
    internal static Mono8JsonObject Build(JsonObject source)
    {
        int ints = 0, decs = 0, moneys = 0, bools = 0, strs = 0, xys = 0;

        foreach (var field in source.Fields)
        {
            int n = field.Values.Count;
            switch (StoreOf(field.Type))
            {
                case Store.Int: ints += n; break;
                case Store.Dec: decs += n; break;
                case Store.Money: moneys += n; break;
                case Store.Bool: bools += n; break;
                case Store.Xy: xys += n; break;
                default: strs += n; break;
            }
        }

        var intValues = Alloc<int>(ints);
        var decValues = Alloc<double>(decs);
        var moneyValues = Alloc<decimal>(moneys);
        var boolValues = Alloc<bool>(bools);
        var strValues = Alloc<string>(strs);
        var xyValues = Alloc<int>(xys * XyStride);

        var index = new Dictionary<string, Slot>(source.Fields.Count, StringComparer.OrdinalIgnoreCase);
        int ci = 0, cd = 0, cm = 0, cb = 0, cs = 0, cx = 0;

        foreach (var field in source.Fields)
        {
            int n = field.Values.Count;

            switch (StoreOf(field.Type))
            {
                case Store.Int:
                    index[field.Name] = new Slot(field.Type, ci, n, field.IsArray);
                    for (int i = 0; i < n; i++) intValues[ci++] = ParseInt(field.Values[i]);
                    break;

                case Store.Dec:
                    index[field.Name] = new Slot(field.Type, cd, n, field.IsArray);
                    for (int i = 0; i < n; i++) decValues[cd++] = ParseDec(field.Values[i]);
                    break;

                case Store.Money:
                    index[field.Name] = new Slot(field.Type, cm, n, field.IsArray);
                    for (int i = 0; i < n; i++) moneyValues[cm++] = ParseMoney(field.Values[i]);
                    break;

                case Store.Bool:
                    index[field.Name] = new Slot(field.Type, cb, n, field.IsArray);
                    for (int i = 0; i < n; i++) boolValues[cb++] = DataValue.IsTrue(field.Values[i]);
                    break;

                case Store.Xy:
                    index[field.Name] = new Slot(field.Type, cx, n, field.IsArray);
                    for (int i = 0; i < n; i++)
                    {
                        ParseXY(field.Values[i], out int x, out int y);
                        xyValues[cx * XyStride] = x;
                        xyValues[cx * XyStride + 1] = y;
                        cx++;
                    }
                    break;

                default:
                    index[field.Name] = new Slot(field.Type, cs, n, field.IsArray);
                    for (int i = 0; i < n; i++) strValues[cs++] = field.Values[i] ?? string.Empty;
                    break;
            }
        }

        return new Mono8JsonObject(intValues, decValues, moneyValues, boolValues, strValues, xyValues, index);
    }

    private static T[] Alloc<T>(int count) => count == 0 ? Array.Empty<T>() : new T[count];

    // A value the editor flagged as no longer matching its type still reaches here, so every parse
    // has to have an answer for text that will not read back. The type's zero is that answer.
    private static int ParseInt(string stored) =>
        int.TryParse(stored, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v) ? v : 0;

    private static double ParseDec(string stored) =>
        double.TryParse(stored, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0d;

    private static decimal ParseMoney(string stored) =>
        decimal.TryParse(stored, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal v) ? v : 0m;

    private static void ParseXY(string stored, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (string.IsNullOrEmpty(stored)) return;

        int comma = stored.IndexOf(',');
        if (comma < 0) return;

        int.TryParse(stored.AsSpan(0, comma), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out x);
        int.TryParse(stored.AsSpan(comma + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out y);
    }

    private static Store StoreOf(DataValueType type) => type switch
    {
        DataValueType.Int => Store.Int,
        DataValueType.Decimal => Store.Dec,
        DataValueType.Money => Store.Money,
        DataValueType.Bool => Store.Bool,
        DataValueType.PosXY => Store.Xy,
        _ => Store.Str          // Text is the only string-backed type.
    };

    // ── Shape ─────────────────────────────────────────────────────────────────

    /// <summary>True when this object declares <paramref name="field"/>. Case-insensitive.</summary>
    public bool Has(string field) => field != null && _index.ContainsKey(field);

    /// <summary>
    /// The field's declared type. A field that is not here reads as <see cref="DataValueType.Text"/>,
    /// so pair this with <see cref="Has"/> when the difference matters.
    /// </summary>
    public DataValueType TypeOf(string field) =>
        field != null && _index.TryGetValue(field, out Slot slot) ? slot.Type : DataValueType.Text;

    /// <summary>True when the field was authored as a list rather than a single value.</summary>
    public bool IsArray(string field) =>
        field != null && _index.TryGetValue(field, out Slot slot) && slot.IsArray;

    /// <summary>How many items the field holds: 1 for a scalar, 0 when the field is not here.</summary>
    public int Count(string field) =>
        field != null && _index.TryGetValue(field, out Slot slot) ? slot.Count : 0;

    // ── Read ──────────────────────────────────────────────────────────────────

    public int GetInt(string field, int i = 0, int fallback = 0) =>
        TryFind(field, Store.Int, i, out int at) ? _ints[at] : fallback;

    public double GetDec(string field, int i = 0, double fallback = 0) =>
        TryFind(field, Store.Dec, i, out int at) ? _decs[at] : fallback;

    public decimal GetMoney(string field, int i = 0, decimal fallback = 0) =>
        TryFind(field, Store.Money, i, out int at) ? _moneys[at] : fallback;

    public bool GetBool(string field, int i = 0, bool fallback = false) =>
        TryFind(field, Store.Bool, i, out int at) ? _bools[at] : fallback;

    /// <summary>Reads a Text field.</summary>
    public string GetStr(string field, int i = 0, string fallback = "") =>
        TryFind(field, Store.Str, i, out int at) ? _strs[at] : fallback;

    /// <summary>Reads a PosXY field, or <c>(0, 0)</c> when there is nothing to read.</summary>
    public (int x, int y) GetXY(string field, int i = 0) =>
        TryFind(field, Store.Xy, i, out int at)
            ? (_xy[at * XyStride], _xy[at * XyStride + 1])
            : (0, 0);

    /// <summary>
    /// A view straight onto the stored ints — no copy, no allocation. Empty when the field is
    /// missing or is not an Int field. Also covers a scalar, as a one-item span.
    /// </summary>
    public ReadOnlySpan<int> IntArray(string field) =>
        TrySlot(field, Store.Int, out Slot slot) ? new ReadOnlySpan<int>(_ints, slot.Start, slot.Count) : default;

    /// <inheritdoc cref="IntArray"/>
    public ReadOnlySpan<double> DecArray(string field) =>
        TrySlot(field, Store.Dec, out Slot slot) ? new ReadOnlySpan<double>(_decs, slot.Start, slot.Count) : default;

    /// <inheritdoc cref="IntArray"/>
    public ReadOnlySpan<bool> BoolArray(string field) =>
        TrySlot(field, Store.Bool, out Slot slot) ? new ReadOnlySpan<bool>(_bools, slot.Start, slot.Count) : default;

    // ── Write ─────────────────────────────────────────────────────────────────
    //
    // Writes land in this snapshot and stop there. data.json is authored in the editor; a running
    // game changes its own copy of the data, not the cartridge it came from.

    internal bool SetInt(string field, int value, int index)
    {
        if (!TryFind(field, Store.Int, index, out int at)) return false;
        _ints[at] = value;
        return true;
    }

    internal bool SetDec(string field, double value, int index)
    {
        if (!TryFind(field, Store.Dec, index, out int at)) return false;
        _decs[at] = value;
        return true;
    }

    internal bool SetMoney(string field, decimal value, int index)
    {
        if (!TryFind(field, Store.Money, index, out int at)) return false;
        _moneys[at] = value;
        return true;
    }

    internal bool SetBool(string field, bool value, int index)
    {
        if (!TryFind(field, Store.Bool, index, out int at)) return false;
        _bools[at] = value;
        return true;
    }

    internal bool SetStr(string field, string value, int index)
    {
        if (!TryFind(field, Store.Str, index, out int at)) return false;
        _strs[at] = value ?? string.Empty;
        return true;
    }

    internal bool SetXY(string field, int x, int y, int index)
    {
        if (!TryFind(field, Store.Xy, index, out int at)) return false;
        _xy[at * XyStride] = x;
        _xy[at * XyStride + 1] = y;
        return true;
    }

    // ── Lookup ────────────────────────────────────────────────────────────────

    private bool TrySlot(string field, Store store, out Slot slot)
    {
        slot = default;
        if (field == null || !_index.TryGetValue(field, out Slot found)) return false;
        if (StoreOf(found.Type) != store) return false;

        slot = found;
        return true;
    }

    /// <summary>Resolves one item to its position in the backing array for <paramref name="store"/>.</summary>
    private bool TryFind(string field, Store store, int index, out int at)
    {
        at = 0;
        if (!TrySlot(field, store, out Slot slot)) return false;
        if (index < 0 || index >= slot.Count) return false;

        at = slot.Start + index;
        return true;
    }
}
