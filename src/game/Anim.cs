namespace mono8.game;

/// <summary>
/// A sprite flipbook authored under the ANIM group in data.json:
/// <c>ID</c> the list of sprite ids, <c>SPEED</c> the playback rate in frames per second,
/// <c>MODE</c> how the list is walked — FW forward, BW or RV backward, PP ping-pong.
///
/// A clip loops by default. Loading one with <c>loop: false</c> makes it a one-shot: it stops on
/// the frame the mode ends on, holds it, and reports <see cref="Done"/>. <see cref="Play"/> rewinds
/// it to the start of the mode.
/// </summary>
internal class Anim
{
    private const int MaxFrames = 16;    // data.json allows 16 items in an array

    private const string JsonGroup = "ANIM";
    private const string FieldId = "ID";
    private const string FieldSpeed = "SPEED";
    private const string FieldMode = "MODE";
    private const string ModeBackward = "BW";
    private const string ModeReverse = "RV";
    private const string ModePingPong = "PP";

    private readonly int[] _frames = new int[MaxFrames];
    private int _count;
    private float _frameSeconds;
    private bool _pingPong;
    private bool _loop;
    private int _firstIndex;
    private int _firstStep;

    private int _index;
    private int _step;
    private float _timer;
    private bool _done;

    public int Sprite => _count > 0 ? _frames[_index] : 0;

    /// <summary>True once a one-shot has reached its final frame; a looping clip is never done.</summary>
    public bool Done => _done;

    public void Load(string name, bool loop = true)
    {
        _count = 0;
        _frameSeconds = 0f;
        _pingPong = false;
        _loop = loop;
        _firstIndex = 0;
        _firstStep = 1;

        var data = YourGame.API.gjson(JsonGroup, name);
        if (data != null)
        {
            // ID is authored as Text, so the ids arrive as strings and are parsed once, here.
            int n = data.Count(FieldId);
            if (n > MaxFrames)
            {
                n = MaxFrames;
            }
            for (int i = 0; i < n; i++)
            {
                if (int.TryParse(data.GetStr(FieldId, i), out int id))
                {
                    _frames[_count] = id;
                    _count++;
                }
            }

            int speed = data.GetInt(FieldSpeed);
            if (speed > 0)
            {
                _frameSeconds = 1f / speed;
            }

            string mode = data.GetStr(FieldMode, 0, string.Empty);
            _pingPong = string.Equals(mode, ModePingPong, StringComparison.OrdinalIgnoreCase);
            if (string.Equals(mode, ModeBackward, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, ModeReverse, StringComparison.OrdinalIgnoreCase))
            {
                _firstStep = -1;
                _firstIndex = _count > 0 ? _count - 1 : 0;
            }
        }

        Play();
    }

    /// <summary>
    /// Rewinds to the first frame of the authored mode and starts playing again.
    /// </summary>
    public void Play()
    {
        _index = _firstIndex;
        _step = _firstStep;
        _timer = 0f;

        // A clip with nothing to walk — one frame, or an unauthored SPEED — can never reach an end
        // on its own, so a one-shot is finished the moment it starts.
        _done = !_loop && (_count < 2 || _frameSeconds <= 0f);
    }

    public void Update(float elapsedSeconds)
    {
        // An unauthored SPEED leaves no frame duration to advance by, and the loop below would
        // never end on one.
        if (_done || _count < 2 || _frameSeconds <= 0f)
        {
            return;
        }

        _timer += elapsedSeconds;
        while (_timer >= _frameSeconds)
        {
            _timer -= _frameSeconds;
            Advance();

            if (_done)
            {
                _timer = 0f;
                return;
            }
        }
    }

    private void Advance()
    {
        int next = _index + _step;

        if (_pingPong)
        {
            if (next >= _count)
            {
                next = _count - 2;
                _step = -1;
            }
            else if (next < 0)
            {
                next = 1;
                _step = 1;
            }

            // Back on the frame it started from is a whole there-and-back cycle.
            _done = !_loop && next == _firstIndex;
        }
        else if (next >= _count || next < 0)
        {
            if (!_loop)
            {
                _done = true;
                return;    // hold the last frame rather than wrapping onto the first
            }

            next = _step > 0 ? 0 : _count - 1;
        }

        _index = next;
    }
}
