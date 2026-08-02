namespace mono8.editor;

/// <summary>
/// An editor whose settings outlive the session. <c>Mono8API.Save</c> asks every editor that
/// implements this for its current state just before <c>config.json</c> is written.
/// <para>
/// There is no matching Apply here: an editor restores its own settings in its constructor, which
/// runs after <c>Mono8API.Load</c> has filled the sheet and only once, unlike <see cref="IEditor.Init"/>.
/// </para>
/// </summary>
internal interface IEditorConfig
{
    void CaptureConfig(ConfigSheet config);
}
