namespace mono8.core.common;

/// <summary>
/// One inclusive x span per scanline of a filled shape. Instances are meant to be kept
/// and reused: <see cref="Reset"/> only grows the arrays, so a shape of a size already
/// seen costs nothing to span again.
/// </summary>
internal sealed class RowSpans
{
    private static readonly int[] Empty = new int[0];

    public int Top;
    public int Count;
    public int[] Left = Empty;
    public int[] Right = Empty;

    public void Reset(int top, int count)
    {
        Top = top;
        Count = Math.Max(count, 0);

        if (Left.Length < Count)
        {
            Left = new int[Count];
            Right = new int[Count];
        }

        for (int i = 0; i < Count; i++)
        {
            Left[i] = int.MaxValue;
            Right[i] = int.MinValue;
        }
    }

    /// <summary>Widens a row to include <paramref name="x"/>. Rows outside the window are dropped.</summary>
    public void Add(int row, int x)
    {
        int i = row - Top;
        if (i < 0 || i >= Count) return;

        if (x < Left[i]) Left[i] = x;
        if (x > Right[i]) Right[i] = x;
    }

    public void Set(int row, int left, int right)
    {
        int i = row - Top;
        if (i < 0 || i >= Count) return;

        Left[i] = left;
        Right[i] = right;
    }

    /// <summary>Marks a row as covering nothing, in a form that compares equal between rows.</summary>
    public void Clear(int row) => Set(row, 1, 0);

    public bool TryGet(int row, out int left, out int right)
    {
        int i = row - Top;
        if (i < 0 || i >= Count)
        {
            left = 1;
            right = 0;
            return false;
        }

        left = Left[i];
        right = Right[i];
        return left <= right;
    }
}
