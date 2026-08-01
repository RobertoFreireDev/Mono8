namespace mono8.game;

/// <summary>
/// A sprite flipbook authored under the ANIM group in data.json:
/// <c>ID</c> the list of sprite ids, <c>SPEED</c> the playback rate in frames per second,
/// <c>MODE</c> how the list is walked — FW forward, BW backward, PP ping-pong.
/// </summary>
internal class Anim
{
    private const int MaxFrames = 16;    // data.json allows 16 items in an array
    private const int DefaultSpeed = 8;  // frames per second

    private const string JsonGroup = "ANIM";
    private const string FieldId = "ID";
    private const string FieldSpeed = "SPEED";
    private const string FieldMode = "MODE";
    private const string ModeBackward = "BW";
    private const string ModePingPong = "PP";

    private readonly int[] _frames = new int[MaxFrames];
    private int _count;
    private float _frameSeconds;
    private bool _pingPong;

    private int _index;
    private int _step;
    private float _timer;

    public int Sprite => _count > 0 ? _frames[_index] : 0;

    public void Load(string name)
    {
        _count = 0;
        _frameSeconds = 1f / DefaultSpeed;
        _pingPong = false;
        _index = 0;
        _step = 1;
        _timer = 0f;

        var data = YourGame.API.gjson(JsonGroup, name);
        if (data == null)
        {
            return;
        }

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

        int speed = data.GetInt(FieldSpeed, 0, DefaultSpeed);
        if (speed > 0)
        {
            _frameSeconds = 1f / speed;
        }

        string mode = data.GetStr(FieldMode, 0, string.Empty);
        _pingPong = string.Equals(mode, ModePingPong, StringComparison.OrdinalIgnoreCase);
        if (string.Equals(mode, ModeBackward, StringComparison.OrdinalIgnoreCase))
        {
            _step = -1;
            _index = _count > 0 ? _count - 1 : 0;
        }
    }

    public void Update(float elapsedSeconds)
    {
        if (_count < 2)
        {
            return;
        }

        _timer += elapsedSeconds;
        while (_timer >= _frameSeconds)
        {
            _timer -= _frameSeconds;
            Advance();
        }
    }

    private void Advance()
    {
        _index += _step;

        if (_pingPong)
        {
            if (_index >= _count)
            {
                _index = _count - 2;
                _step = -1;
            }
            else if (_index < 0)
            {
                _index = 1;
                _step = 1;
            }
        }
        else if (_index >= _count)
        {
            _index = 0;
        }
        else if (_index < 0)
        {
            _index = _count - 1;
        }
    }
}
