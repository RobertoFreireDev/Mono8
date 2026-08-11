using Microsoft.JSInterop;
using mono8.core.common;

namespace mono8.web.Pages;

/// <summary>
/// The console's game loop, hung off the page. There is no blocking Run() in a browser: the host
/// page's requestAnimationFrame calls back into <see cref="TickDotNet"/>, and each call is one
/// frame of the same Update/Draw the desktop build runs.
/// </summary>
public partial class Index
{
    private Mono8Game _game;

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);

        // The canvas has to exist before KNI goes looking for it, so the loop starts from the first
        // render rather than from OnInitialized.
        if (firstRender)
            JsRuntime.InvokeAsync<object>("initRenderJS", DotNetObjectReference.Create(this));
    }

    [JSInvokable]
    public void TickDotNet()
    {
        try
        {
            Tick();
        }
        catch (Exception ex)
        {
            // Anything reaching here is fatal — the game's own exceptions are caught and drawn by
            // ErrorHandler, and the loop below is dead once this rethrows. The browser shows only
            // the outermost message, which for a failure in a static initialiser says nothing about
            // what actually threw, so the whole chain goes to the console first.
            for (Exception e = ex; e != null; e = e.InnerException)
                Console.WriteLine(e.GetType().FullName + ": " + e.Message + "\n" + e.StackTrace);
            throw;
        }
    }

    private void Tick()
    {
        if (_game == null)
        {
            // Before anything is constructed: Mono8API's load reads the save through this on the
            // very first frame, and dset writes back through it from then on.
            WebStorage.Attach(JsRuntime);
            // Also before: the audio is built from a static initialiser the moment Mono8API is
            // first touched, and it needs the rate this device will accept.
            AudioFormat.SetOutputSampleRate(WebAudio.SampleRate(JsRuntime));

            _game = new Mono8Game();
            // Returns as soon as the device and content are up — the browser owns the loop below.
            _game.Run();
        }

        _game.Tick();
    }
}
