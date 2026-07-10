namespace mono8;

/// <summary>Splash screen shown once at application start, before the editors take over.</summary>
internal class Intro
{
    // Timeline (seconds): blank | fade in | full opacity | fade out.
    private const double BlankUntil = 0.5;
    private const double FadeInEnd = 1.0;
    private const double FullUntil = 2.0;
    private const double DurationSeconds = 2.5;
    private const string Title = "MONO-8";
    private const int CellSize = 8;
    private const int GridColumns = 4;
    private const int GridRows = 4;

    private double _elapsed;

    public bool IsFinished => _elapsed >= DurationSeconds;

    /// <summary>Fades the splash in over its first half-second of visibility and back out at the end.</summary>
    private float Opacity
    {
        get
        {
            if (_elapsed < BlankUntil) return 0f;
            if (_elapsed < FadeInEnd) return (float)((_elapsed - BlankUntil) / (FadeInEnd - BlankUntil));
            if (_elapsed < FullUntil) return 1f;
            if (_elapsed < DurationSeconds) return (float)(1.0 - (_elapsed - FullUntil) / (DurationSeconds - FullUntil));
            return 0f;
        }
    }

    /// <summary>Steps through all 16 palette entries exactly once across the intro's duration.</summary>
    private int ColorOffset => (int)(_elapsed / DurationSeconds * Constants.GameDataSizes.ColorPalette)
        % Constants.GameDataSizes.ColorPalette;

    public void Update(GameTime gameTime)
    {
        if (IsFinished) return;
        _elapsed += gameTime.ElapsedGameTime.TotalSeconds;
    }

    public void Draw(Mono8API api)
    {
        api.cls();

        float opacity = Opacity;
        if (opacity <= 0f) return;

        // Each glyph advances 4px, the last one still draws its full 5px width.
        int titleWidth = (Title.Length - 1) * 4 + 5;
        int titleX = (Constants.Screen.ResolutionX - titleWidth) / 2;
        int gridWidth = GridColumns * CellSize;
        int gridHeight = GridRows * CellSize;
        int titleY = (Constants.Screen.ResolutionY - gridHeight) / 2 - 12;
        int gridX = (Constants.Screen.ResolutionX - gridWidth) / 2;
        int gridY = titleY + 14;

        api.print(Title, titleX, titleY, Constants.Colors.Orange, opacity);

        int offset = ColorOffset;

        for (int row = 0; row < GridRows; row++)
        {
            for (int column = 0; column < GridColumns; column++)
            {
                int x = gridX + column * CellSize;
                int y = gridY + row * CellSize;
                int cell = row * GridColumns + column;
                int colorIndex = (cell - offset + Constants.GameDataSizes.ColorPalette)
                    % Constants.GameDataSizes.ColorPalette;
                api.rectfill(x, y, x + CellSize - 1, y + CellSize - 1, colorIndex, opacity);
            }
        }
    }
}
