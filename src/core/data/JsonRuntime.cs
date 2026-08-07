namespace mono8.core.data;

/// <summary>
/// The game-facing view of <c>data.json</c>: every authored object compiled into a
/// <see cref="Mono8JsonObject"/> and indexed by group and object name.
/// <para>
/// Rebuilt from <see cref="JsonSheet"/> at load and after each save, so a value edited in the
/// editor is visible to <c>gjson</c> on the next <c>Ctrl+S</c> without a restart. Lookup is two
/// dictionary hits and allocates nothing, which is what makes it safe to call from
/// <c>Update</c> every frame.
/// </para>
/// </summary>
internal sealed class JsonRuntime
{
    private Dictionary<string, Dictionary<string, Mono8JsonObject>> _groups =
        new Dictionary<string, Dictionary<string, Mono8JsonObject>>(StringComparer.OrdinalIgnoreCase);

    // The same objects in the order they were authored, which the dictionary throws away. A game that
    // keys its data on a field rather than on the object name has no other way to find the objects.
    private Dictionary<string, string[]> _order =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Recompiles the whole index. Worst case is 16 × 64 objects of 16 fields, which is a few
    /// milliseconds once per load — cheap enough to redo wholesale rather than track deltas.
    /// The new index replaces the old one only when it is complete.
    /// </summary>
    public void Build(JsonSheet sheet)
    {
        var groups = new Dictionary<string, Dictionary<string, Mono8JsonObject>>(
            sheet?.Groups.Count ?? 0, StringComparer.OrdinalIgnoreCase);

        var order = new Dictionary<string, string[]>(
            sheet?.Groups.Count ?? 0, StringComparer.OrdinalIgnoreCase);

        if (sheet != null)
        {
            foreach (var group in sheet.Groups)
            {
                var objects = new Dictionary<string, Mono8JsonObject>(
                    group.Objects.Count, StringComparer.OrdinalIgnoreCase);

                var names = new string[group.Objects.Count];

                for (int i = 0; i < group.Objects.Count; i++)
                {
                    var obj = group.Objects[i];
                    objects[obj.Name] = Mono8JsonObject.Build(obj);
                    names[i] = obj.Name;
                }

                groups[group.Name] = objects;
                order[group.Name] = names;
            }
        }

        _groups = groups;
        _order = order;
    }

    /// <summary>
    /// The object authored at <paramref name="group"/> / <paramref name="obj"/>, or null when
    /// either name is unknown. Names match without regard to case, since the editor upper-cases
    /// every name it stores.
    /// </summary>
    public Mono8JsonObject Find(string group, string obj)
    {
        if (group == null || obj == null) return null;
        if (!_groups.TryGetValue(group, out var objects)) return null;

        return objects.TryGetValue(obj, out var found) ? found : null;
    }

    /// <summary>How many objects <paramref name="group"/> holds, or 0 when the group is unknown.</summary>
    public int CountOf(string group)
    {
        if (group == null) return 0;

        return _order.TryGetValue(group, out var names) ? names.Length : 0;
    }

    /// <summary>
    /// The name of the object at <paramref name="index"/>, in the order the group authors them, or
    /// null when the group is unknown or the index is past the end.
    /// </summary>
    public string NameAt(string group, int index)
    {
        if (group == null) return null;
        if (!_order.TryGetValue(group, out var names)) return null;

        return index >= 0 && index < names.Length ? names[index] : null;
    }
}
