namespace mono8.core.sfx;

/// <summary>
/// The sequencer wired to the baked bank: what a published build plays. Audio in a shipped game
/// cannot change, so the samples are rendered once at save time and read back here instead of being
/// computed every frame.
/// <para>
/// Nothing about the console's behaviour moves with it. Channel allocation, the sfx() offset/length
/// semantics and the whole music sequencer are <see cref="AudioSequencer"/>'s, shared with
/// <see cref="SfxEngine"/>; a music pattern is still four SFX started together and timed off their
/// note clocks. Only where a sample comes from is different.
/// </para>
/// </summary>
internal sealed class AudioPlayer : AudioSequencer
{
    private readonly SfxBank _bank = new();

    public AudioPlayer()
    {
        var channels = new IAudioChannel[AudioFormat.NumChannels];
        for (int i = 0; i < channels.Length; i++)
            channels[i] = new BankChannel(i, AudioFormat.OutputBufferSamples, OnBufferNeeded, _bank);
        Channels = channels;
    }

    public override void LoadBank(string folderPath) => _bank.Load(folderPath, SfxBank);
}
