namespace mono8;

internal class Mono8API : IMono8API
{
    public static IEditor editor;
    private static SfxEngine _sfxEngine = new SfxEngine();
    public static SpriteSheet SpriteSheet = new SpriteSheet();
    public static MapSheet MapSheet = new MapSheet();
    private static string _folder = string.Empty;

    public Mono8API()
    {
        editor = new SpriteEditor();

        try
        {
            editor.Init();
        }
        catch (Exception ex) { ErrorHandler.SetError(ex); }
    }

    public void load(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        _folder = folder;
        Reload();
    }

    internal void Reload()
    {
        if (string.IsNullOrWhiteSpace(_folder))
        {
            return;
        }

        _sfxEngine.Sfx(-1);
        var path = Path.Combine(Directory.GetCurrentDirectory(), _folder);
        _sfxEngine.LoadSfxs(FileIO.SplitData(FileIO.Read(Constants.File.Name, Constants.File.Extensions.Sfx, path)));
        _sfxEngine.LoadMusicPatterns(FileIO.SplitData(FileIO.Read(Constants.File.Name, Constants.File.Extensions.Music, path)));
        SpriteSheet.LoadSprites(
            FileIO.SplitData(FileIO.Read(Constants.File.Name, Constants.File.Extensions.SpriteSheet, path)),
            FileIO.SplitData(FileIO.Read(Constants.File.Name, Constants.File.Extensions.Flags, path)));
        MapSheet.LoadMaps(FileIO.SplitData(FileIO.Read(Constants.File.Name, Constants.File.Extensions.MapSheet, path)));
        SaveData.Load(path);
    }

    public void Update(GameTime gameTime)
    {
        if (ErrorHandler.HasError()) return;

        try
        {
            _sfxEngine.UpdateMusic();
            if (!Menu.IsPaused())
            {
                editor.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
            }
        }
        catch (Exception ex) { ErrorHandler.SetError(ex); }
    }

    public void Draw()
    {
        if (ErrorHandler.HasError())
        {
            ErrorHandler.Draw();
            return;
        }

        try
        {
            editor.Draw();
            camera(0, 0);
            Menu.Draw();
        }
        catch (Exception ex) { ErrorHandler.SetError(ex); }
    }

    public void StopSounds() => _sfxEngine.Sfx(-1);

    public void Unload()
    {
        StopSounds();
        _sfxEngine.Dispose();
    }

    public bool btn(int button) => ButtonInput.Pressed(button);
    public bool btn(int button, int player) => ButtonInput.Pressed(player * 8 + button);
    public bool btnp(int button) => ButtonInput.JustPressed(button);
    public bool btnp(int button, int player) => ButtonInput.JustPressed(player * 8 + button);
    public bool btnr(int button) => ButtonInput.Released(button);
    public bool mouseup() => MouseInputBinding.ScrollUp();
    public bool mousedown() => MouseInputBinding.ScrollDown();
    public bool mouselp() => MouseInputBinding.LeftJustPressed();
    public bool mouselr() => MouseInputBinding.LeftReleased();
    public bool mousel() => MouseInputBinding.LeftPressed();
    public bool mouserp() => MouseInputBinding.RightJustPressed();
    public bool mouserr() => MouseInputBinding.RightReleased();
    public bool mouser() => MouseInputBinding.RightPressed();
    public (int x, int y) mousexy() => MouseInputBinding.PosXY();

    public void camera(float x = 0, float y = 0)
    {
        mono8.SpriteBatch.End();
        Camera2D.Camera((int)x, (int)y);
        mono8.SpriteBatch.Begin();
    }

    public void print(string text, int x, int y, int color = 7)
    {
        Text.DrawText(text, new Vector2(x,y), color);
    }

    public void sfx(int sfxId, int channel = -1, int offset = 0, int length = -1) 
        => _sfxEngine.Sfx(sfxId, channel, offset, length);

    public void spr(int spriteId, int x, int y, int width = 1, int height = 1,
        int scale = 1, bool flipX = false, bool flipY = false)
    {
        SpriteSheet.Draw(spriteId, x, y, width, height, scale, flipX, flipY);
    }

    public void sspr(int sx, int sy, int sw, int sh, int dx, int dy,
        int dw = -1, int dh = -1, bool flipX = false, bool flipY = false)
    {
        SpriteSheet.DrawSub(sx, sy, sw, sh, dx, dy, dw < 0 ? sw : dw, dh < 0 ? sh : dh, flipX, flipY);
    }

    public void cls(int colorIndex = 0)
    {
        mono8.SpriteBatch.DrawBaseBox(colorIndex);
    }

    public int stat(int id)
    {
        switch (id)
        {
            case 7:
                return mono8.DisplayFps;
        }

        return 0;
    }

    public void pixel(int x, int y, int color)
    {
        mono8.SpriteBatch.DrawPixel(x, y, color);
    }

    public void line(int x0, int y0, int x1, int y1, int color)
    {
        mono8.SpriteBatch.DrawLine(x0, y0, x1, y1, color);
    }

    public void rect(int x0, int y0, int x1, int y1, int color)
    {
        (int x, int y, int w, int h) = ToRect(x0, y0, x1, y1);
        mono8.SpriteBatch.DrawRect(x0, y0, w, h, color);
    }

    public void rectfill(int x0, int y0, int x1, int y1, int color)
    {
        (int x, int y, int w, int h) = ToRect(x0, y0, x1, y1);
        mono8.SpriteBatch.DrawRectFill(x0, y0, w, h, color);
    }

    public (int x, int y, int w, int h) ToRect(int x0, int y0,int x1, int y1)
    {
        return (Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0) + 1, Math.Abs(y1 - y0) + 1);
    }

    public void circ(int x, int y, int radius, int color)
    {
        mono8.SpriteBatch.DrawCirc(x, y, radius, color);
    }

    public void circfill(int x, int y, int radius, int color)
    {
        mono8.SpriteBatch.DrawCircFill(x, y, radius, color);
    }

    public void palt()
    {
        ColorPalette.PaltReset();
    }

    public void palt(int colorIndex)
    {
        ColorPalette.Palt(colorIndex, true);
    }

    public void palt(int colorIndex, bool transparent)
    {
        ColorPalette.Palt(colorIndex, transparent);
    }

    public void pal()
    {
        ColorPalette.Pal();
    }

    public void pal(int c0, int c1)
    {
        ColorPalette.Pal(c0, c1);
    }

    public int mget(int cellX, int cellY)
    {
        return MapSheet.GetTile(cellX, cellY);
    }

    public void mset(int cellX, int cellY, int spriteId)
    {
        MapSheet.SetTile(cellX, cellY, spriteId);
    }

    public void map(int cellX, int cellY, int screenX, int screenY, int cellWidth = 40, int cellHeight = 23, int layerMax = 0, int color = - 1)
    {
        MapSheet.DrawMap(cellX, cellY, screenX, screenY, cellWidth, cellHeight, layerMax, color);
    }

    public int fget(int spriteId) => SpriteSheet.GetFlags(spriteId);

    public bool fget(int spriteId, int flag) => SpriteSheet.GetFlag(spriteId, flag);

    public void fset(int spriteId, int flag, bool value) => SpriteSheet.SetFlag(spriteId, flag, value);

    public void fset(int spriteId, int value) => SpriteSheet.SetFlags(spriteId, value);

    public void music(int musicId, int fadeLength = 0, int channelMask = 0)
        => _sfxEngine.Music(musicId, fadeLength, channelMask);

    private static Random _rng = new Random();

    public float rnd(float max = 1f) => (float)_rng.NextDouble() * max;

    public double rnd(double max) => _rng.NextDouble() * max;

    public int rnd(int max) => max <= 0 ? 0 : _rng.Next(0, max);

    public void srand(int seed) => _rng = new Random(seed);

    public void run()
    {
        editor = new SpriteEditor();
        editor.Init();
    }

    public double time() => (double)DateTime.Now.TimeOfDay.TotalSeconds;

    public double abs(double value) => Math.Abs(value);

    public double atan2(double dy, double dx) => Math.Atan2(dy, dx) / (2 * Math.PI);

    public double cos(double angle) => Math.Cos(angle * 2 * Math.PI);

    // PICO-8 sin is negated (y-axis flipped)
    public double sin(double angle) => -Math.Sin(angle * 2 * Math.PI);

    public double sqrt(double value) => Math.Sqrt(value);

    public double min(double a, double b) => Math.Min(a, b);

    public double max(double a, double b) => Math.Max(a, b);

    public double mid(double a, double b, double c) => Math.Max(Math.Min(Math.Max(a, b), c), Math.Min(a, b));

    public double flr(double value) => Math.Floor(value);

    public double ceil(double value) => Math.Ceiling(value);

    public double round(double value) => Math.Round(value, MidpointRounding.AwayFromZero);

    public int sgn(double value) => value > 0 ? 1 : value < 0 ? -1 : 0;

    public int dget(int index) => SaveData.Get(index);

    public void dset(int index, int value) => SaveData.Set(index, value);

    public void menuitem(int index, string label, Action callback)
        => Menu.SetItem(index, label, callback);

    public void menuitem(int index)
        => Menu.ClearItem(index);
}
