namespace mono8.editor;

internal interface IEditor
{
    void Init();

    void Update(float elapsedSeconds);

    void Draw();
}
