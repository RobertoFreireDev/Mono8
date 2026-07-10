namespace mono8.editor;

internal interface IEditor
{
    void Init();

    void Update(float elapsedSeconds);

    void Draw();

    // Called when the editor is switched away from, so transient state (e.g. an
    // active selection) can be dropped. Init() only runs on first activation.
    void Exit() { }
}
