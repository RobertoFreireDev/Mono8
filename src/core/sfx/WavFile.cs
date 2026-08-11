using System.Buffers.Binary;

namespace mono8.core.sfx;

/// <summary>
/// The minimum of RIFF/WAVE needed to carry the baked SFX bank: 44.1kHz 16-bit mono PCM. A real wav
/// rather than a private blob so the bake can be opened in any audio player and listened to.
/// </summary>
internal static class WavFile
{
    private const int HeaderBytes = 44;
    private const int BitsPerSample = 16;
    private const int Channels = 1;
    private const int PcmFormat = 1;

    /// <summary>Wraps <paramref name="sampleCount"/> samples of <paramref name="samples"/> in a canonical 44-byte header.</summary>
    public static byte[] Write(short[] samples, int sampleCount)
    {
        int dataBytes = sampleCount * 2;
        var file = new byte[HeaderBytes + dataBytes];
        var span = file.AsSpan();

        WriteTag(span, 0, "RIFF");
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4), (uint)(36 + dataBytes));
        WriteTag(span, 8, "WAVE");

        WriteTag(span, 12, "fmt ");
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(20), PcmFormat);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(22), Channels);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(24), AudioFormat.SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(28), AudioFormat.SampleRate * Channels * (BitsPerSample / 8));
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(32), Channels * (BitsPerSample / 8));
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(34), BitsPerSample);

        WriteTag(span, 36, "data");
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(40), (uint)dataBytes);

        for (int i = 0; i < sampleCount; i++)
            BinaryPrimitives.WriteInt16LittleEndian(span.Slice(HeaderBytes + i * 2), samples[i]);

        return file;
    }

    /// <summary>
    /// Locates the PCM inside <paramref name="file"/>, rejecting anything that is not the format the
    /// bank reader assumes. Walks the chunk list rather than trusting a 44-byte header, so a file
    /// carrying an extra chunk decodes rather than being read at the wrong offset.
    /// </summary>
    public static bool TryRead(byte[] file, out int dataOffset, out int sampleCount)
    {
        dataOffset = 0;
        sampleCount = 0;

        if (file == null || file.Length < HeaderBytes) return false;
        var span = file.AsSpan();
        if (!TagIs(span, 0, "RIFF") || !TagIs(span, 8, "WAVE")) return false;

        bool formatOk = false;
        int pos = 12;
        while (pos + 8 <= file.Length)
        {
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(pos + 4));
            int body = pos + 8;

            if (TagIs(span, pos, "fmt "))
            {
                if (size < 16 || body + 16 > file.Length) return false;
                if (BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(body)) != PcmFormat) return false;
                if (BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(body + 2)) != Channels) return false;
                if (BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(body + 4)) != AudioFormat.SampleRate) return false;
                if (BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(body + 14)) != BitsPerSample) return false;
                formatOk = true;
            }
            else if (TagIs(span, pos, "data"))
            {
                if (!formatOk) return false;
                // A truncated file reports more than it carries; trust the shorter of the two.
                int available = file.Length - body;
                dataOffset = body;
                sampleCount = Math.Min((int)Math.Min(size, (uint)int.MaxValue), available) / 2;
                return true;
            }

            // RIFF chunks are word-aligned, so an odd size carries a pad byte.
            long next = (long)pos + 8 + size + (size & 1);
            if (next <= pos || next > file.Length) return false;
            pos = (int)next;
        }

        return false;
    }

    private static void WriteTag(Span<byte> span, int offset, string tag)
    {
        for (int i = 0; i < tag.Length; i++) span[offset + i] = (byte)tag[i];
    }

    private static bool TagIs(ReadOnlySpan<byte> span, int offset, string tag)
    {
        if (offset + tag.Length > span.Length) return false;
        for (int i = 0; i < tag.Length; i++)
            if (span[offset + i] != (byte)tag[i]) return false;
        return true;
    }
}
