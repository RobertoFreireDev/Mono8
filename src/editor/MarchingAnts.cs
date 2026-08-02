namespace mono8.editor;

// The animated dashed border an editor draws around a committed selection. Kept here rather than in
// each editor because the phase has to advance on its own clock, independent of what is selected.
internal sealed class MarchingAnts
{
    private const float FrameSeconds = 0.12f;
    private static readonly int[] Palette =
        { Constants.Colors.White, Constants.Colors.LightGray, Constants.Colors.DarkGray };

    private float elapsed;
    private int phase;

    public void Update(float elapsedSeconds)
    {
        elapsed += elapsedSeconds;
        while (elapsed >= FrameSeconds)
        {
            elapsed -= FrameSeconds;
            phase = (phase + 1) % Palette.Length;
        }
    }

    /// <summary>
    /// Outlines a screen-space rectangle, dropping any pixel outside <paramref name="clip"/> so a
    /// selection scrolled past the edge of its viewport doesn't paint over the panels around it.
    /// </summary>
    public void Draw(IMono8API api, int x0, int y0, int x1, int y1, Rectangle clip)
    {
        void Ant(int x, int y, int t)
        {
            if (!clip.Contains(x, y)) return;
            api.pixel(x, y, Palette[(t + phase) % Palette.Length]);
        }

        int step = 0;
        for (int x = x0; x <= x1; x++) Ant(x, y0, step++);       // top, L->R
        for (int y = y0 + 1; y <= y1; y++) Ant(x1, y, step++);   // right, T->B
        for (int x = x1 - 1; x >= x0; x--) Ant(x, y1, step++);   // bottom, R->L
        for (int y = y1 - 1; y > y0; y--) Ant(x0, y, step++);    // left, B->T
    }
}
