namespace mono8.editor;

internal class Button
{
    public Rectangle Bounds;
    public int IconIndex;

    public Button(int x, int y, int size, int iconIndex)
    {
        Bounds = new Rectangle(x, y, size, size);
        IconIndex = iconIndex;
    }

    public bool IsClicked(IMono8API api, (int x, int y) mouse) =>
        Bounds.Contains(mouse.x, mouse.y) && api.mouselp();

    public void Draw(IMono8API api, bool selected)
    {
        if (!selected)
        {
            api.pal(Constants.Colors.White, Constants.Colors.Indigo);
        }
        
        api.icon(IconIndex, Bounds.X, Bounds.Y);

        api.pal();
    }
}
