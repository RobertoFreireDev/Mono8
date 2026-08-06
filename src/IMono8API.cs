namespace mono8;

/// <summary>
/// PICO-8 style API.
/// All coordinates are pixel-based unless otherwise noted.
/// </summary>
public interface IMono8API
{
    // ============================================================
    // SYSTEM
    // ============================================================
    double time();

    int stat(int id);

    void menuitem(int index, string label, Action callback);

    void menuitem(int index);

    // ============================================================
    // GRAPHICS
    // ============================================================
    void cls(int colorIndex = 0);

    void pixel(int x, int y, int color, float colorOpaqueness = 1f);

    void line(int x0, int y0, int x1, int y1, int color);

    void rect(int x0, int y0, int x1, int y1, int color, float colorOpaqueness = 1f);

    void rectfill(int x0, int y0, int x1, int y1, int color, float colorOpaqueness = 1f);

    void circ(int x, int y, int radius, int color, float colorOpaqueness = 1f);

    void circfill(int x, int y, int radius, int color, float colorOpaqueness = 1f);

    void oval(int x0, int y0, int x1, int y1, int color, float colorOpaqueness = 1f);

    void ovalfill(int x0, int y0, int x1, int y1, int color, float colorOpaqueness = 1f);

    /// <summary>
    /// Covers the screen in <paramref name="color"/> except the rect at
    /// (<paramref name="x"/>, <paramref name="y"/>, <paramref name="w"/>, <paramref name="h"/>) — a
    /// clipping mask with a rectangular hole. The fill spans the viewport wherever the camera is;
    /// the hole is in world space, so it tracks whatever it is cut around.
    /// <paramref name="ditherSpriteId"/> tiles an 8x8 sprite over the one-tile ring just outside the
    /// hole, so the mask fades into the hole rather than cutting; 0 leaves a hard edge.
    /// </summary>
    void rectinv(int x, int y, int w, int h, int color, int ditherSpriteId = 0, float colorOpaqueness = 1f);

    /// <summary>
    /// <see cref="rectinv"/> with an oval hole. The hole is the oval <c>ovalfill</c> would draw for
    /// the same box, so the two line up pixel for pixel.
    /// </summary>
    void ovalinv(int x, int y, int w, int h, int color, int ditherSpriteId = 0, float colorOpaqueness = 1f);

    void sprr(
        int spriteId,
        int x,
        int y,
        int width = 1,
        int height = 1,
        float scale = 1f,
        bool flipX = false,
        bool flipY = false,
        float colorOpaqueness = 1f);

    void ssprr(
        int sx, int sy, int sw, int sh,
        int dx, int dy,
        int dw = -1, int dh = -1,
        bool flipX = false, bool flipY = false,
        float colorOpaqueness = 1f);

    void spr(
        int spriteId,
        int x,
        int y,
        int width = 1,
        int height = 1,
        float scale = 1f,
        bool flipX = false,
        bool flipY = false,
        float colorOpaqueness = 1f);

    void sspr(
        int sx, int sy, int sw, int sh,
        int dx, int dy,
        int dw = -1, int dh = -1,
        bool flipX = false, bool flipY = false,
        float colorOpaqueness = 1f);

    void print(
        string text,
        int x,
        int y,
        int color = 7,
        float colorOpaqueness = 1f);

    void icon(int n, int x, int y);

    void camera(float x = 0, float y = 0);

    void pal();

    void pal(int c0, int c1);

    void palt();

    void palt(int colorIndex);

    void palt(int colorIndex, bool transparent);

    // ============================================================
    // MAP
    // ============================================================

    int mget(
        int cellX,
        int cellY);

    void mset(
        int cellX,
        int cellY,
        int spriteId);

    /// <summary>
    /// Draws a region of the map, optionally scaled as a whole.
    /// <paramref name="scale"/> supports only 0.5, 1 and 2; other values snap to the nearest.
    /// </summary>
    void map(
        int cellX,
        int cellY,
        int screenX,
        int screenY,
        int cellWidth = 40, int cellHeight = 23,
        float scale = 1f,
        float colorOpaqueness = 1f,
        int layerMax = 0);

    // ============================================================
    // TILE COLLISION
    // ============================================================

    /// <summary>
    /// Whether the tile under the point (<paramref name="x"/>, <paramref name="y"/>) carries
    /// <paramref name="flag"/>. Coordinates are pixels over the whole map sheet — map cell
    /// coordinates times 8, the same space <see cref="mget"/> reads — so apply your own camera and
    /// room offsets first.
    /// <para>
    /// The flag is a bit index, <c>0</c>-<c>7</c>, read off the tile's sprite exactly as
    /// <see cref="fget(int, int)"/> does, which leaves what each one means entirely to your game:
    /// flag <c>0</c> for solid ground, another for ice, another for hazards. An index outside
    /// <c>0</c>-<c>7</c> collides with nothing. Empty cells hold sprite <c>0</c>, which
    /// <see cref="map"/> never draws, so they never collide however that sprite is flagged.
    /// </para>
    /// </summary>
    bool mcol(int x, int y, int flag = 0);

    /// <summary>
    /// Whether any tile under the rectangle running from (<paramref name="x"/>, <paramref name="y"/>)
    /// to (x + <paramref name="w"/> - 1, y + <paramref name="h"/> - 1) carries
    /// <paramref name="flag"/>. An empty rectangle — either side zero or negative — meets nothing.
    /// </summary>
    /// <inheritdoc cref="mcol(int, int, int)"/>
    bool mcol(int x, int y, int w, int h, int flag = 0);

    // ============================================================
    // SPRITE FLAGS
    // ============================================================

    int fget(
        int spriteId);

    bool fget(
        int spriteId,
        int flag);

    void fset(
        int spriteId,
        int flag,
        bool value);

    void fset(
        int spriteId,
        int value);

    // ============================================================
    // INPUT
    // ============================================================

    bool btn(int button);

    bool btn(int button, int player);

    bool btnp(int button);

    bool btnp(int button, int player);

    bool btnr(int button);

    bool mouseup();
    bool mousedown();
    bool mouselp();
    bool mouselr();
    bool mousel();
    bool mouserp();
    bool mouserr();
    bool mouser();
    (int x, int y) mousexy();

    // ============================================================
    // AUDIO
    // ============================================================

    void sfx(
    int sfxId,
    int channel = -1,
    int offset = 0,
    int length = -1);

    void music(
        int musicId,
        int fadeLength = 0,
        int channelMask = 0);

    // ============================================================
    // RANDOM
    // ============================================================

    float rnd(float max = 1f);

    double rnd(double max);

    int rnd(int max);

    void srand(int seed);

    // ============================================================
    // MATH
    // ============================================================

    double abs(double value);

    double atan2(double dy, double dx);

    double cos(double angle);

    double sin(double angle);

    double sqrt(double value);

    double min(double a, double b);

    double max(double a, double b);

    double mid(double a, double b, double c);

    double flr(double value);

    double ceil(double value);

    double round(double value);

    int sgn(double value);

    // ============================================================
    // PERSISTENCE
    // ============================================================

    int dget(int index);

    void dset(int index, int value);

    // ============================================================
    // JSON DATA
    // ============================================================

    /// <summary>
    /// The object authored at <paramref name="group"/> / <paramref name="obj"/> in
    /// <c>data.json</c>, or null when either name is unknown. Names are matched without regard to
    /// case. The lookup allocates nothing, so holding the result is an optimisation, not a
    /// requirement.
    /// </summary>
    Mono8JsonObject gjson(string group, string obj);

    /// <summary>
    /// Writes one value into an existing field. Returns false — and changes nothing — when the
    /// group, object or field is unknown, when <paramref name="index"/> is past the end of an
    /// array, or when the overload does not match the field's declared type. Never throws and
    /// never creates a field.
    /// <para>
    /// The write lands in memory only: <c>data.json</c> is authored in the editor, and a running
    /// game does not rewrite its own data.
    /// </para>
    /// </summary>
    bool sjson(string group, string obj, string field, int value, int index = 0);

    /// <inheritdoc cref="sjson(string, string, string, int, int)"/>
    bool sjson(string group, string obj, string field, double value, int index = 0);

    /// <inheritdoc cref="sjson(string, string, string, int, int)"/>
    bool sjson(string group, string obj, string field, decimal value, int index = 0);

    /// <inheritdoc cref="sjson(string, string, string, int, int)"/>
    bool sjson(string group, string obj, string field, bool value, int index = 0);

    /// <inheritdoc cref="sjson(string, string, string, int, int)"/>
    bool sjson(string group, string obj, string field, string value, int index = 0);

    /// <summary>
    /// Writes a PosXY field, as a tuple: <c>sjson("ENEMY", "SLIME", "SPAWN", (40, 88))</c>. The
    /// pair is one argument rather than two so it cannot be read as the int overload's
    /// <c>value, index</c>.
    /// </summary>
    /// <inheritdoc cref="sjson(string, string, string, int, int)"/>
    bool sjson(string group, string obj, string field, (int x, int y) value, int index = 0);
}

/// <summary>
/// Sprite-sheet mutation, available to the built-in editors only. Game code sees
/// <see cref="IMono8API"/>, which cannot paint over the sheet it is drawing from.
/// </summary>
internal interface IEditorAPI : IMono8API
{
    void SetPixel(int x, int y, int colorIndex);

    /// <summary>As <see cref="SetPixel"/>, but stencilled through another sprite's 8x8 tile.</summary>
    void SetPixelDithered(int x, int y, int colorIndex, int ditherSpriteId);

    void SetRectFill(int x, int y, int w, int h, int colorIndex);

    void SetRect(int x, int y, int w, int h, int colorIndex);

    void SetOval(int x0, int y0, int x1, int y1, int colorIndex);

    void SetOvalFill(int x0, int y0, int x1, int y1, int colorIndex);

    void SetPaintBucket(int x, int y, int regionX, int regionY, int regionW, int regionH, int colorIndex);
}