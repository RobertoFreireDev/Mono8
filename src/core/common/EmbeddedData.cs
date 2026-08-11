namespace mono8.core.common;

/// <summary>
/// The authored data folder as it ships inside a published build. A <c>dotnet publish</c> of a build
/// with <see cref="Mono8API.PublishGame"/> set embeds every file of <c>publishdata</c> (see
/// mono8.csproj), so the game carries its own sprites, map, audio and json rather than reading them
/// from a folder next to the executable.
/// <para>
/// Nothing is embedded otherwise, so every lookup here misses in a dev build and <see cref="FileIO"/>
/// falls back to the data folder. <c>data.save</c> misses even in a published one — it is deliberately
/// left out of the embedding, since <c>dset</c> has to write it.
/// </para>
/// </summary>
internal static class EmbeddedData
{
    /// <summary>Matches the LogicalName the csproj gives each embedded file.</summary>
    private const string Prefix = "publishdata/";

    /// <summary>The embedded file's text, or null when this build does not carry it.</summary>
    internal static string Read(string fileName)
    {
        // The flag is read here as well as by the build: a dev build has no manifest to search, and
        // this keeps the disk the only source while the editors are the thing running.
        if (!Mono8API.PublishGame) return null;

        try
        {
            using var stream = typeof(EmbeddedData).Assembly.GetManifestResourceStream(Prefix + fileName);
            if (stream == null) return null;

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch
        {
            // Load runs outside the error handler, so a manifest that cannot be read is a fall back
            // to disk rather than a dead console.
            return null;
        }
    }
}
