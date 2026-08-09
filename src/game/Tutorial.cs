namespace mono8.game;

/// <summary>
/// The controls, spelled out on the first level and nowhere else — a player who has walked, swung
/// and changed club once does not need them again, and every level after the first is entered by
/// someone who has.
///
/// A row is up only while its button would actually do something, so the block reads as what can be
/// pressed right now rather than as a list of everything there is: the walk and the jump go while the
/// club is out, the swing button appears at the ball and then says what its next press will do,
/// backing out is only offered on a shot not yet taken, and the bag closes for as long as the swing
/// owns the club. The whole block is empty through the swing-through and the pause after it, which is
/// exactly the stretch nothing is pressable in.
///
/// Each control keeps its own slot rather than the list closing up around what is hidden: a caption
/// that jumps a row every time another one goes is harder to read than a gap.
///
/// Screen pixels, drawn with the rest of the HUD after the room's camera is back at the origin, so
/// the block stays in the corner whichever screenful of the sheet the room cuts out.
/// </summary>
internal static class Tutorial
{
    // The icons are the developer's, off the console's icon sheet.
    private const int IconLeft = 68;
    private const int IconRight = 71;
    private const int IconJump = 72;    // Z
    private const int IconSwing = 73;   // X
    private const int IconBack = 74;    // C
    private const int IconClub = 75;    // V

    private const int NoIcon = -1;

    // One press at a time, so the swing button carries two captions: what the club coming out is for,
    // and what the club already out is waiting to do.
    private const string CaptionWalk = "WALK";
    private const string CaptionJump = "JUMP";
    private const string CaptionAddress = "GET READY";
    private const string CaptionSwing = "SWING";
    private const string CaptionBack = "BACK OUT";
    private const string CaptionClub = "NEXT CLUB";

    // One icon is one tile, and the block is inset two of them from the top-left corner.
    private const int IconSize = Terrain.TileSize;
    private const int Margin = IconSize * 2;

    // Two icon slots on every row, so the captions line up in one column whether the row is one
    // button or the pair the walk row is.
    private const int IconSlots = 2;
    private const int IconGap = 2;

    // Icon and caption are centred on each other: the icon's middle is four rows down, the ink's is
    // Font.Middle down from where the line prints.
    private const int TextDrop = IconSize / 2 - Font.Middle;

    private const int RowHeight = IconSize + 2;

    // Which row of the block each control keeps, whether or not it is showing this frame.
    private const int SlotWalk = 0;
    private const int SlotJump = 1;
    private const int SlotSwing = 2;
    private const int SlotBack = 3;
    private const int SlotClub = 4;

    private static bool Shown;

    /// <param name="number">
    /// The room's NUMBER. Only the first level explains itself — a room that authors no number is
    /// not a level and gets nothing.
    /// </param>
    public static void Init(int number)
    {
        Shown = number == Levels.MinNumber;
    }

    public static void Draw()
    {
        if (!Shown)
        {
            return;
        }

        // The walk and the jump are off behind the same three conditions, so they go together.
        bool onFoot = Player.CanWalk;

        // Aiming is the club already out, which is the press that swings through; otherwise the
        // press is the one that brings the club out, and that is only offered standing at the ball —
        // the same reading Swing takes before it leaves Idle, so the row is up exactly when the
        // press would be accepted. Through the swing-through itself there is nothing to press.
        bool aiming = Swing.Aiming;
        bool swing = aiming || (!Swing.Active && Player.CanStartSwing());

        Row(SlotWalk, IconLeft, IconRight, CaptionWalk, onFoot);
        Row(SlotJump, IconJump, NoIcon, CaptionJump, onFoot);
        Row(SlotSwing, IconSwing, NoIcon, aiming ? CaptionSwing : CaptionAddress, swing);
        Row(SlotBack, IconBack, NoIcon, CaptionBack, aiming);
        Row(SlotClub, IconClub, NoIcon, CaptionClub, Club.CanSwap);
    }

    // One control: its icon — or its pair of them, for the two keys that are one control — and what
    // it does. Hidden rows still cost their slot, which is what keeps the block from shuffling.
    private static void Row(int slot, int first, int second, string caption, bool shown)
    {
        if (!shown)
        {
            return;
        }

        var api = YourGame.API;
        int y = Margin + slot * RowHeight;

        api.icon(first, Margin, y);

        if (second != NoIcon)
        {
            api.icon(second, Margin + IconSize, y);
        }

        // Outlined, like every other caption: the block sits over the room, which is a background it
        // has no say in.
        Font.PrintOutlined(caption, Margin + IconSlots * IconSize + IconGap, y + TextDrop,
            Constants.Colors.White);
    }
}
