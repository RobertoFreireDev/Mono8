namespace mono8.core.data;

/// <summary>
/// One sprite's onion-skin setup. Only sprites that differ from the editor's defaults are stored,
/// so a sheet with three referenced tiles writes three entries rather than 960.
/// </summary>
internal struct OnionEntry
{
    public int Sprite;
    public int Reference;
    public int Order;
    public int Tint;
    public int Opacity;
}

/// <summary>
/// Every editor's settings — the Sprite Editor's animation strip, onion-skin setup, dither slots and
/// canvas state, the Map Editor's tool and layers, the selected sfx and music indices, and the Json
/// Editor's selection — and the sole parser and writer of <c>config.json</c>.
/// <para>
/// It is deliberately not <c>data.json</c>: this file is written by the engine rather than authored
/// by hand, so it carries none of that format's type suffixes or name limits. It shares only the
/// plumbing — <see cref="JsonReader"/> on the way in, <see cref="FileIO"/> either way.
/// </para>
/// <para>
/// Loading is forgiving in the same spirit as <see cref="JsonSheet"/>: an unknown key, a value of
/// the wrong kind, a short array or an out-of-range sprite id drops that one value and leaves its
/// default in place. Nothing here throws — <c>Mono8API.Load</c> runs outside the error handler.
/// </para>
/// <para>
/// The enum-backed values are stored as indices, so reordering the editor's <c>Tool</c>,
/// <c>LoopMode</c>, <c>ReferenceOrder</c> or <c>ReferenceVisualization</c> reinterprets an existing
/// file. That is acceptable for settings and not for data, which is why they live apart.
/// </para>
/// </summary>
internal sealed class ConfigSheet
{
    public const int AnimFrameCount = 8;
    public const int DitherSlotCount = 8;
    public const int MapLayerCount = 4;

    private const int MaxSprite = Constants.GameDataSizes.MaxSpriteIndex;

    /// <summary>Sprite id per animation slot; -1 is an empty slot.</summary>
    public readonly int[] AnimFrames = new int[AnimFrameCount];
    public int AnimScaleIdx;
    public int AnimSpeedIdx;
    public int AnimLoopIdx;

    /// <summary>Sprite id whose pixels stencil each dither slot; -1 is an empty slot.</summary>
    public readonly int[] DitherSprites = new int[DitherSlotCount];
    public int ActiveDitherSlot;

    public int CanvasZoomIdx;
    public int ToolIdx;
    public int ColorIdx = Constants.Colors.White;
    public bool ShowAutotileGuide;

    public int MapToolIdx;
    public int MapEnabledLayer;
    public readonly bool[] MapLayerVisible = new bool[MapLayerCount];
    /// <summary>Viewport top-left, in the enabled layer's own quarter-local cells.</summary>
    public int MapCamX;
    public int MapCamY;
    public int MapZoomIdx = MapDefaultZoomIdx;

    public int SfxIndex;
    public int MusicPattern;

    /// <summary>
    /// The Json Editor's selection, by name — the tree is rebuilt from <c>data.json</c> on every
    /// start, so the objects a reference would point at no longer exist by then. Empty when nothing
    /// was selected, and a name that no longer resolves simply leaves the editor with no selection.
    /// </summary>
    public string JsonGroup = string.Empty;
    public string JsonObject = string.Empty;

    /// <summary>
    /// Names of the groups the Json Editor had folded shut, for the same reason as the selection:
    /// a group that has since been renamed or deleted simply comes back open.
    /// </summary>
    public readonly List<string> JsonCollapsed = new List<string>();

    /// <summary>Sparse, ascending by sprite id so the file diffs stably.</summary>
    public readonly List<OnionEntry> Onion = new List<OnionEntry>();

    // x1, the middle of the Map Editor's three zoom steps; the editor owns the table, this is only
    // the value a file without a MAP section comes back with.
    private const int MapDefaultZoomIdx = 1;

    public ConfigSheet() => Reset();

    private void Reset()
    {
        Array.Fill(AnimFrames, -1);
        AnimScaleIdx = 0;
        AnimSpeedIdx = 0;
        AnimLoopIdx = 0;

        Array.Fill(DitherSprites, -1);
        ActiveDitherSlot = 0;

        CanvasZoomIdx = 0;
        ToolIdx = 0;
        ColorIdx = Constants.Colors.White;
        ShowAutotileGuide = false;

        MapToolIdx = 0;
        MapEnabledLayer = 0;
        Array.Fill(MapLayerVisible, true);
        MapCamX = 0;
        MapCamY = 0;
        MapZoomIdx = MapDefaultZoomIdx;

        SfxIndex = 0;
        MusicPattern = 0;

        JsonGroup = string.Empty;
        JsonObject = string.Empty;
        JsonCollapsed.Clear();

        Onion.Clear();
    }

    // ── Load / Save ───────────────────────────────────────────────────────────

    public void Load(string path)
    {
        Reset();

        string text = FileIO.Read(Constants.File.ConfigName, Constants.File.Extensions.Json, path);
        if (string.IsNullOrWhiteSpace(text)) return;   // a missing or empty file is every default

        RawValue root;
        try
        {
            root = new JsonReader(text).ReadDocument();
        }
        catch (FormatException)
        {
            // Structurally broken beyond recovery: start on the defaults rather than guess, and
            // leave the file alone until the next save overwrites it.
            Reset();
            return;
        }

        Build(root);
    }

    public void Save(string path) =>
        FileIO.Write(Constants.File.ConfigName, Constants.File.Extensions.Json, ToJson(), path);

    // ── Serializer ────────────────────────────────────────────────────────────

    /// <summary>2-space indent, fixed key order, so re-reading this output reproduces it byte for byte.</summary>
    public string ToJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("{\n");

        sb.Append("  \"ANIM\": { \"FRAMES\": ");
        AppendArray(sb, AnimFrames);
        sb.Append(", \"SCALE\": ").Append(AnimScaleIdx)
          .Append(", \"SPEED\": ").Append(AnimSpeedIdx)
          .Append(", \"LOOP\": ").Append(AnimLoopIdx)
          .Append(" },\n");

        sb.Append("  \"DITHER\": { \"SLOTS\": ");
        AppendArray(sb, DitherSprites);
        sb.Append(", \"ACTIVE\": ").Append(ActiveDitherSlot).Append(" },\n");

        sb.Append("  \"CANVAS\": { \"ZOOM\": ").Append(CanvasZoomIdx)
          .Append(", \"TOOL\": ").Append(ToolIdx)
          .Append(", \"COLOR\": ").Append(ColorIdx)
          .Append(", \"GUIDE\": ").Append(ShowAutotileGuide ? "true" : "false")
          .Append(" },\n");

        sb.Append("  \"MAP\": { \"TOOL\": ").Append(MapToolIdx)
          .Append(", \"LAYER\": ").Append(MapEnabledLayer)
          .Append(", \"VISIBLE\": ");
        AppendBoolArray(sb, MapLayerVisible);
        sb.Append(", \"CAMX\": ").Append(MapCamX)
          .Append(", \"CAMY\": ").Append(MapCamY)
          .Append(", \"ZOOM\": ").Append(MapZoomIdx)
          .Append(" },\n");

        sb.Append("  \"SFX\": { \"INDEX\": ").Append(SfxIndex).Append(" },\n");
        sb.Append("  \"MUSIC\": { \"PATTERN\": ").Append(MusicPattern).Append(" },\n");

        sb.Append("  \"JSON\": { \"GROUP\": ").Append(Quote(JsonGroup))
          .Append(", \"OBJECT\": ").Append(Quote(JsonObject))
          .Append(", \"COLLAPSED\": ");
        AppendStrArray(sb, JsonCollapsed);
        sb.Append(" },\n");

        sb.Append("  \"ONION\": [");
        for (int i = 0; i < Onion.Count; i++)
        {
            var e = Onion[i];
            sb.Append(i == 0 ? "\n" : ",\n");
            sb.Append("    { \"SPR\": ").Append(e.Sprite)
              .Append(", \"REF\": ").Append(e.Reference)
              .Append(", \"ORDER\": ").Append(e.Order)
              .Append(", \"TINT\": ").Append(e.Tint)
              .Append(", \"OPACITY\": ").Append(e.Opacity)
              .Append(" }");
        }
        sb.Append(Onion.Count == 0 ? "]\n" : "\n  ]\n");

        sb.Append("}\n");
        return sb.ToString();
    }

    private static void AppendArray(System.Text.StringBuilder sb, int[] values)
    {
        sb.Append('[');
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(values[i]);
        }
        sb.Append(']');
    }

    private static void AppendStrArray(System.Text.StringBuilder sb, List<string> values)
    {
        sb.Append('[');
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(Quote(values[i]));
        }
        sb.Append(']');
    }

    private static void AppendBoolArray(System.Text.StringBuilder sb, bool[] values)
    {
        sb.Append('[');
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(values[i] ? "true" : "false");
        }
        sb.Append(']');
    }

    /// <summary>
    /// Only <c>"</c> and <c>\</c> need escaping: the one string here is a <see cref="JsonNames"/>
    /// name, which can hold neither, and every other character it can hold is a JSON literal.
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
        if (root.Kind != RawKind.Object) return;

        var anim = Member(root, "ANIM");
        ReadSpriteArray(anim, "FRAMES", AnimFrames);
        ReadInt(anim, "SCALE", ref AnimScaleIdx);
        ReadInt(anim, "SPEED", ref AnimSpeedIdx);
        ReadInt(anim, "LOOP", ref AnimLoopIdx);

        var dither = Member(root, "DITHER");
        ReadSpriteArray(dither, "SLOTS", DitherSprites);
        ReadInt(dither, "ACTIVE", ref ActiveDitherSlot);

        var canvas = Member(root, "CANVAS");
        ReadInt(canvas, "ZOOM", ref CanvasZoomIdx);
        ReadInt(canvas, "TOOL", ref ToolIdx);
        ReadInt(canvas, "COLOR", ref ColorIdx);
        ReadBool(canvas, "GUIDE", ref ShowAutotileGuide);

        var map = Member(root, "MAP");
        ReadInt(map, "TOOL", ref MapToolIdx);
        ReadInt(map, "LAYER", ref MapEnabledLayer);
        ReadBoolArray(map, "VISIBLE", MapLayerVisible);
        ReadInt(map, "CAMX", ref MapCamX);
        ReadInt(map, "CAMY", ref MapCamY);
        ReadInt(map, "ZOOM", ref MapZoomIdx);

        ReadInt(Member(root, "SFX"), "INDEX", ref SfxIndex);
        ReadInt(Member(root, "MUSIC"), "PATTERN", ref MusicPattern);

        var json = Member(root, "JSON");
        ReadStr(json, "GROUP", ref JsonGroup);
        ReadStr(json, "OBJECT", ref JsonObject);
        ReadStrList(json, "COLLAPSED", JsonCollapsed, Constants.JsonData.MaxGroups);

        BuildOnion(Member(root, "ONION"));
    }

    private void BuildOnion(RawValue list)
    {
        if (list == null || list.Kind != RawKind.Array) return;

        foreach (var item in list.Items)
        {
            if (item.Kind != RawKind.Object) continue;

            var entry = new OnionEntry { Sprite = -1, Reference = -1 };
            ReadInt(item, "SPR", ref entry.Sprite);
            // A sprite this entry cannot belong to is not repairable into one that it can.
            if (entry.Sprite < 0 || entry.Sprite > MaxSprite) continue;

            ReadInt(item, "REF", ref entry.Reference);
            entry.Reference = ClampSprite(entry.Reference);
            ReadInt(item, "ORDER", ref entry.Order);
            ReadInt(item, "TINT", ref entry.Tint);
            ReadInt(item, "OPACITY", ref entry.Opacity);

            Onion.Add(entry);
        }
    }

    private static RawValue Member(RawValue obj, string key)
    {
        if (obj == null || obj.Kind != RawKind.Object) return null;
        foreach (var m in obj.Members)
        {
            if (m.Key == key) return m.Value;
        }
        return null;
    }

    private static void ReadInt(RawValue obj, string key, ref int target)
    {
        if (TryInt(Member(obj, key), out int value)) target = value;
    }

    private static void ReadBool(RawValue obj, string key, ref bool target)
    {
        var value = Member(obj, key);
        if (value != null && value.Kind == RawKind.Bool) target = value.Literal == "true";
    }

    private static void ReadStr(RawValue obj, string key, ref string target)
    {
        var value = Member(obj, key);
        if (value != null && value.Kind == RawKind.String) target = value.Literal;
    }

    /// <summary>
    /// Fills <paramref name="target"/> from a string array, dropping non-strings and anything past
    /// <paramref name="limit"/> — a hand-mangled file cannot grow the list beyond what can exist.
    /// </summary>
    private static void ReadStrList(RawValue obj, string key, List<string> target, int limit)
    {
        var array = Member(obj, key);
        if (array == null || array.Kind != RawKind.Array) return;

        foreach (var item in array.Items)
        {
            if (target.Count >= limit) break;
            if (item.Kind == RawKind.String && !target.Contains(item.Literal)) target.Add(item.Literal);
        }
    }

    private static void ReadBoolArray(RawValue obj, string key, bool[] target)
    {
        var array = Member(obj, key);
        if (array == null || array.Kind != RawKind.Array) return;

        int count = Math.Min(array.Items.Count, target.Length);
        for (int i = 0; i < count; i++)
        {
            if (array.Items[i].Kind == RawKind.Bool) target[i] = array.Items[i].Literal == "true";
        }
    }

    private static void ReadSpriteArray(RawValue obj, string key, int[] target)
    {
        var array = Member(obj, key);
        if (array == null || array.Kind != RawKind.Array) return;

        // A file written by an older or newer build can be the wrong length; take what overlaps.
        int count = Math.Min(array.Items.Count, target.Length);
        for (int i = 0; i < count; i++)
        {
            if (TryInt(array.Items[i], out int value)) target[i] = ClampSprite(value);
        }
    }

    private static bool TryInt(RawValue value, out int result)
    {
        result = 0;
        return value != null
            && value.Kind == RawKind.Number
            && int.TryParse(value.Literal, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out result);
    }

    /// <summary>Anything outside the sheet reads as "no sprite" rather than as a neighbouring tile.</summary>
    private static int ClampSprite(int spriteId) => spriteId >= 0 && spriteId <= MaxSprite ? spriteId : -1;
}
