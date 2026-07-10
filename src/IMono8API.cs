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
        int color = 7);

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
    // SPRITE EDITOR
    // ============================================================

    void SetPixel(int x, int y, int colorIndex);

    void SetRectFill(int x, int y, int w, int h, int colorIndex);

    void SetRect(int x, int y, int w, int h, int colorIndex);

    void SetOval(int x0, int y0, int x1, int y1, int colorIndex);

    void SetOvalFill(int x0, int y0, int x1, int y1, int colorIndex);

    void SetPaintBucket(int x, int y, int regionX, int regionY, int regionW, int regionH, int colorIndex);

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
}