#if BLAZORGL
using Microsoft.JSInterop;

namespace mono8.core.common;

/// <summary>
/// What rate the browser will play at. WebAudio does not resample for us: an AudioContext runs at
/// whatever the machine's output device is set to — 48000 on most of them, 44100 on some — and KNI
/// refuses to build a voice at any other rate, which took the console down in its static
/// initialiser before a frame was drawn.
/// <para>
/// So the console asks first, and <see cref="AudioFormat"/> carries the answer into every channel;
/// the baked bank is still 44100 and is read into this rate instead. Same synchronous
/// <see cref="IJSInProcessRuntime"/> path as <see cref="WebStorage"/>, and for the same reason.
/// </para>
/// </summary>
internal static class WebAudio
{
    /// <summary>The device's rate, or 0 when the page cannot tell us — audio disabled, or an old host page.</summary>
    internal static int SampleRate(IJSRuntime runtime)
    {
        try
        {
            var js = runtime as IJSInProcessRuntime;
            return js == null ? 0 : (int)Math.Round(js.Invoke<double>("mono8AudioSampleRate"));
        }
        catch
        {
            return 0;
        }
    }
}
#endif
