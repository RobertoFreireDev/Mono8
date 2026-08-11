namespace mono8.core.sfx;

/// <summary>
/// The sequencer wired to live synthesis: every sample is computed as it is played, so an edit
/// pushed in with <see cref="AudioSequencer.SetSfx"/> is audible on the very next note. That is what
/// the editors audition with, and what a dev build plays throughout.
/// </summary>
internal sealed class SfxEngine : AudioSequencer
{
    public SfxEngine()
    {
        var channels = new IAudioChannel[AudioFormat.NumChannels];
        for (int i = 0; i < channels.Length; i++)
            channels[i] = new ChannelState(i, AudioFormat.BufferSamples, OnBufferNeeded, SfxBank);
        Channels = channels;
    }
}
