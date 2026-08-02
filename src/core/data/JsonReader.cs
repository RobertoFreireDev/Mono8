namespace mono8.core.data;

internal enum RawKind { Object, Array, String, Number, Bool, Null }

/// <summary>A node exactly as it appears in the file, before any of the rules are applied to it.</summary>
internal sealed class RawValue
{
    public RawKind Kind;
    public string Literal = string.Empty;
    public List<RawValue> Items;
    public List<KeyValuePair<string, RawValue>> Members;
}

/// <summary>
/// Recursive-descent reader for the subset of JSON the data files use. Hand-rolled rather than taken
/// from System.Text.Json so the format stays exactly what the writers produce. Throws
/// <see cref="FormatException"/> on anything structurally broken; <see cref="JsonSheet.Load"/> and
/// <see cref="ConfigSheet.Load"/> are the only callers and each turns that into an empty sheet.
/// </summary>
internal sealed class JsonReader
{
    private const int MaxDepth = 8;   // root + group + object + PosXY array of arrays, with room to spare

    private readonly string _s;
    private int _i;

    public JsonReader(string s) => _s = s ?? string.Empty;

    public RawValue ReadDocument()
    {
        var value = ReadValue(0);
        SkipWhitespace();
        if (_i < _s.Length) throw new FormatException("content after the root object");
        return value;
    }

    private RawValue ReadValue(int depth)
    {
        if (depth > MaxDepth) throw new FormatException("nested too deep");

        SkipWhitespace();
        switch (Peek())
        {
            case '{': return ReadObject(depth);
            case '[': return ReadArray(depth);
            case '"': return new RawValue { Kind = RawKind.String, Literal = ReadString() };
            case 't': Expect("true"); return new RawValue { Kind = RawKind.Bool, Literal = "true" };
            case 'f': Expect("false"); return new RawValue { Kind = RawKind.Bool, Literal = "false" };
            case 'n': Expect("null"); return new RawValue { Kind = RawKind.Null };
            default: return ReadNumber();
        }
    }

    private RawValue ReadObject(int depth)
    {
        _i++;   // '{'
        var value = new RawValue { Kind = RawKind.Object, Members = new List<KeyValuePair<string, RawValue>>() };

        SkipWhitespace();
        if (Peek() == '}') { _i++; return value; }

        while (true)
        {
            SkipWhitespace();
            if (Peek() != '"') throw new FormatException("expected a key");
            string key = ReadString();

            SkipWhitespace();
            if (Peek() != ':') throw new FormatException("expected ':'");
            _i++;

            value.Members.Add(new KeyValuePair<string, RawValue>(key, ReadValue(depth + 1)));

            SkipWhitespace();
            char c = Peek();
            _i++;
            if (c == ',') continue;
            if (c == '}') return value;
            throw new FormatException("expected ',' or '}'");
        }
    }

    private RawValue ReadArray(int depth)
    {
        _i++;   // '['
        var value = new RawValue { Kind = RawKind.Array, Items = new List<RawValue>() };

        SkipWhitespace();
        if (Peek() == ']') { _i++; return value; }

        while (true)
        {
            value.Items.Add(ReadValue(depth + 1));

            SkipWhitespace();
            char c = Peek();
            _i++;
            if (c == ',') continue;
            if (c == ']') return value;
            throw new FormatException("expected ',' or ']'");
        }
    }

    private string ReadString()
    {
        _i++;   // '"'
        var sb = new System.Text.StringBuilder();

        while (true)
        {
            char c = Peek();
            _i++;

            if (c == '"') return sb.ToString();

            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }

            char escape = Peek();
            _i++;
            switch (escape)
            {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    if (_i + 4 > _s.Length) throw new FormatException("truncated \\u escape");
                    if (!int.TryParse(_s.Substring(_i, 4), System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture, out int code))
                        throw new FormatException("bad \\u escape");
                    sb.Append((char)code);
                    _i += 4;
                    break;
                default: throw new FormatException("unknown escape");
            }
        }
    }

    private RawValue ReadNumber()
    {
        int start = _i;
        while (_i < _s.Length)
        {
            char c = _s[_i];
            if (char.IsDigit(c) || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') _i++;
            else break;
        }

        string literal = _s.Substring(start, _i - start);
        if (literal.Length == 0
            || !double.TryParse(literal, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            throw new FormatException("expected a value");

        return new RawValue { Kind = RawKind.Number, Literal = literal };
    }

    private void Expect(string word)
    {
        if (_i + word.Length > _s.Length || string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0)
            throw new FormatException("expected " + word);
        _i += word.Length;
    }

    private char Peek()
    {
        if (_i >= _s.Length) throw new FormatException("unexpected end of file");
        return _s[_i];
    }

    private void SkipWhitespace()
    {
        while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
    }
}
