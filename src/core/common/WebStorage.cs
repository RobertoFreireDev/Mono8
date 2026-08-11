#if BLAZORGL
using Microsoft.JSInterop;

namespace mono8.core.common;

/// <summary>
/// The browser's localStorage, reached synchronously — the console's stand-in for the one file it
/// writes at runtime, <c>data.save</c>.
/// <para>
/// <c>dset</c> is called from game code in the middle of a frame and returns having persisted, so
/// the awaitable <see cref="IJSRuntime"/> is no use here. On WebAssembly the same runtime is also an
/// <see cref="IJSInProcessRuntime"/>, whose calls cross into JavaScript and back before returning;
/// the host hands it over in <see cref="Attach"/> before the game is built.
/// </para>
/// </summary>
internal static class WebStorage
{
    private static IJSInProcessRuntime _js;

    /// <summary>Called once by the Blazor host, before <c>Mono8Game</c> loads anything.</summary>
    internal static void Attach(IJSRuntime runtime) => _js = runtime as IJSInProcessRuntime;

    /// <summary>The stored value, or null when nothing is stored under <paramref name="key"/>.</summary>
    internal static string Read(string key)
    {
        try
        {
            return _js?.Invoke<string>("localStorage.getItem", key);
        }
        catch
        {
            // Private-browsing modes and cross-origin frames can refuse storage outright. A game
            // that cannot save is still a game that runs.
            return null;
        }
    }

    internal static void Write(string key, string value)
    {
        try
        {
            _js?.InvokeVoid("localStorage.setItem", key, value);
        }
        catch
        {
        }
    }
}
#endif
