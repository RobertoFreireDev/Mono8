namespace mono8.editor;

/// <summary>
/// A vertical scrollbar over a fixed track: a proportional thumb that can be dragged, and a track
/// that jumps the thumb to wherever it is clicked. Nothing in the project scrolls horizontally, so
/// there is no horizontal counterpart.
/// <para>
/// The widget owns no offset of its own — the panel keeps that, in whatever unit suits it (whole
/// rows for the tree, pixels for the inspector), and this only has to agree on the unit.
/// </para>
/// </summary>
internal sealed class ScrollBar
{
    private const int MinThumb = 6;

    private readonly IMono8API _api;
    private readonly Rectangle _track;

    private bool _dragging;
    private int _grab;   // distance from the thumb's top to where the cursor took hold of it

    public ScrollBar(IMono8API api, Rectangle track)
    {
        _api = api;
        _track = track;
    }

    /// <summary>Applies a drag to <paramref name="offset"/>. True when the bar consumed the mouse.</summary>
    public bool Update((int x, int y) mouse, int contentSize, int viewSize, ref int offset)
    {
        int max = Math.Max(0, contentSize - viewSize);
        if (max == 0)
        {
            _dragging = false;
            offset = 0;
            return false;
        }

        if (_dragging && !_api.mousel()) _dragging = false;

        int thumb = ThumbSize(contentSize, viewSize);
        int travel = _track.Height - thumb;

        if (!_dragging && _api.mouselp() && _track.Contains(mouse.x, mouse.y))
        {
            int top = _track.Y + (travel <= 0 ? 0 : offset * travel / max);

            // Taking hold of the thumb keeps it under the cursor; clicking the bare track centres it there.
            _grab = mouse.y >= top && mouse.y < top + thumb ? mouse.y - top : thumb / 2;
            _dragging = true;
        }

        if (!_dragging) return false;

        offset = travel <= 0 ? 0 : Math.Clamp((mouse.y - _grab - _track.Y) * max / travel, 0, max);
        return true;
    }

    public void Draw(int contentSize, int viewSize, int offset)
    {
        _api.rectfill(_track.X, _track.Y, _track.Right - 1, _track.Bottom - 1, Constants.Colors.DarkGray);

        // Nothing to scroll draws no thumb at all: a full-height one is indistinguishable from a
        // panel border and says nothing about the content.
        int max = Math.Max(0, contentSize - viewSize);
        if (max == 0) return;

        int thumb = ThumbSize(contentSize, viewSize);
        int travel = _track.Height - thumb;
        int top = _track.Y + (travel <= 0 ? 0 : Math.Clamp(offset, 0, max) * travel / max);

        _api.rectfill(_track.X, top, _track.Right - 1, top + thumb - 1, Constants.Colors.LightGray);
    }

    private int ThumbSize(int contentSize, int viewSize) =>
        Math.Clamp(viewSize * _track.Height / Math.Max(1, contentSize), MinThumb, _track.Height);
}
