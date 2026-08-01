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

    /// <summary>
    /// Recompiles the whole index. Worst case is 16 × 64 objects of 16 fields, which is a few
    /// milliseconds once per load — cheap enough to redo wholesale rather than track deltas.
    /// The new index replaces the old one only when it is complete.
    /// </summary>
    public void Build(JsonSheet sheet)
    {
        var groups = new Dictionary<string, Dictionary<string, Mono8JsonObject>>(
            sheet?.Groups.Count ?? 0, StringComparer.OrdinalIgnoreCase);

        if (sheet != null)
        {
            foreach (var group in sheet.Groups)
            {
                var objects = new Dictionary<string, Mono8JsonObject>(
                    group.Objects.Count, StringComparer.OrdinalIgnoreCase);

                foreach (var obj in group.Objects)
                {
                    objects[obj.Name] = Mono8JsonObject.Build(obj);
                }

                groups[group.Name] = objects;
            }
        }

        _groups = groups;
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
}
