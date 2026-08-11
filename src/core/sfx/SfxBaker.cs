using System.Text;

namespace mono8.core.sfx;

/// <summary>
/// Renders the authored SFX sheet to <c>data.wav</c> + <c>data.snx</c>, which a published build
/// plays instead of synthesising. Runs on every save, so the bank can never fall behind the sheet.
/// <para>
/// The renderer is not a second implementation of the synthesiser — it drives the very same
/// <see cref="SfxVoice"/> the live channels do, and simply pulls samples out of it. That is what
/// makes a baked SFX identical to the first samples <c>sfx(n)</c> would have synthesised, rather
/// than merely similar to them.
/// </para>
/// </summary>
internal static class SfxBaker
{
    /// <summary>
    /// A fixed seed, not an unseeded Random: without it the noise waveform differs every run, so
    /// two saves of unchanged data would write two different multi-megabyte wavs. The seed is
    /// re-applied per SFX, so editing one never shifts the noise of the ones after it.
    /// </summary>
    private const int NoiseSeed = 20250811;

    /// <summary>Refuses to write a bank beyond this, rather than hanging a save on a stray speed-255 slot.</summary>
    private const long MaxBankSamples = 200_000_000;

    public static void Bake(SfxSheet sheet, string folderPath)
    {
        var sfxData = new Dictionary<int, SfxData>(SfxSheet.Count);
        for (int i = 0; i < SfxSheet.Count; i++) sfxData[i] = sheet.ToSfxData(i);

        long projected = 0;
        for (int i = 0; i < SfxSheet.Count; i++)
        {
            if (IsSilent(sfxData[i])) continue;
            projected += (long)PassNotes(sfxData[i]) * AudioFormat.SamplesPerNote(sfxData[i]);
        }
        if (projected > MaxBankSamples) return;

        var samples = new short[projected];
        var index = new string[SfxSheet.Count];
        int total = 0;

        for (int i = 0; i < SfxSheet.Count; i++)
        {
            index[i] = string.Empty;

            var data = sfxData[i];
            if (IsSilent(data)) continue;

            int spn = AudioFormat.SamplesPerNote(data);
            int notes = PassNotes(data);
            if (spn <= 0 || notes <= 0) continue;

            int start = total;
            int written = Render(data, sfxData, notes, spn, samples, start);

            // Trailing silence is trimmed off the end, snapped down to a whole note so the
            // note-to-sample mapping the reader derives stays exact. Measured on the rendered
            // samples rather than on note volumes, because a zero-volume note carrying a slide
            // after an audible one is not silent.
            int lastAudible = -1;
            for (int s = written - 1; s >= 0; s--)
            {
                if (samples[start + s] != 0) { lastAudible = s; break; }
            }
            if (lastAudible < 0) continue;

            int keptNotes = Math.Min(notes, lastAudible / spn + 1);
            total = start + keptNotes * spn;
            index[i] = $"{start} {keptNotes} {spn}";
        }

        var lines = new StringBuilder();
        lines.Append("mono8snx 1 ").Append(AudioFormat.SampleRate).Append(' ').Append(total);
        for (int i = 0; i < index.Length; i++) lines.Append('\n').Append(index[i]);

        FileIO.WriteBytes(Constants.File.Name, Constants.File.Extensions.SfxBank,
            WavFile.Write(samples, total), folderPath);
        FileIO.Write(Constants.File.Name, Constants.File.Extensions.SfxBankIndex,
            lines.ToString(), folderPath);
    }

    /// <summary>
    /// Pulls one pass of <paramref name="data"/> out of a voice. A looping SFX stops at its loop
    /// bound — the playing channel wraps back to the loop start itself — while everything else runs
    /// to the end of its notes.
    /// </summary>
    private static int Render(SfxData data, Dictionary<int, SfxData> bank, int notes, int spn,
                              short[] dest, int destOffset)
    {
        var voice = new SfxVoice(bank, NoiseSeed);
        voice.Start(data, 0, data.Notes.Count);

        int count = notes * spn;
        for (int s = 0; s < count; s++)
        {
            if (!voice.IsPlaying)
            {
                for (int r = s; r < count; r++) dest[destOffset + r] = 0;
                break;
            }
            dest[destOffset + s] = SfxVoice.Quantise(voice.Next());
        }

        return count;
    }

    private static int PassNotes(SfxData data) => data.HasLoop ? data.LoopEnd : data.Notes.Count;

    /// <summary>
    /// Whether the SFX is silent throughout, without rendering it. Safe as a whole-SFX test: with
    /// every volume at zero the previous-note volume is never above zero either, so the slide that
    /// could otherwise carry an audible level into a zero-volume note never engages, and every
    /// effect ends up scaling a zero.
    /// <para>
    /// This is the common case, not an edge one — a freshly initialised sheet is 64 silent slots.
    /// </para>
    /// </summary>
    private static bool IsSilent(SfxData data)
    {
        int notes = Math.Min(PassNotes(data), data.Notes.Count);
        for (int n = 0; n < notes; n++)
            if (data.Notes[n].Volume > 0) return false;
        return true;
    }
}
