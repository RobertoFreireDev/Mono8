# Mono8 on the web

`mono8.web.csproj` is the browser build of the console: the same engine and the same game as
`mono8.csproj`, compiled against [KNI](https://github.com/kniEngine/kni) — a MonoGame fork with a
WebGL/WebAssembly backend — and hosted in a Blazor WebAssembly page instead of an SDL window.

There is no second copy of the sources. The project compiles `../*.cs`, `../core`, `../editor` and
`../game` directly, with `BLAZORGL` defined; the handful of places the browser has no equivalent of
a desktop branch on that symbol and nothing else.

## It only builds when the game is published

`Mono8API.PublishGame` gates it. The csproj reads the flag out of `../Mono8API.cs` exactly the way
`mono8.csproj` does, and fails the build with a message when it is `false`:

> Mono8API.PublishGame is false, so there is no web build to make.

That is not a formality. A web build has no folder beside the executable, so the only data it can
read is the copy embedded in the assembly — and the console only looks for that copy when
`PublishGame` is set. The editors would also have nowhere to save to.

## Publishing

```sh
# 1. set PublishGame = true in src/Mono8API.cs
# 2. run the console once and press Ctrl+S, so src/publishdata holds the current sprites,
#    map, sfx, music, json and the baked audio bank
dotnet publish src/web/mono8.web.csproj -c Release
```

The site is `src/web/bin/Release/net8.0/publish/wwwroot` — a static folder. `<base href="./">`
makes it work from a subdirectory, so it can be zipped and uploaded to itch.io as-is, or dropped on
any static host. `dotnet run --project src/web/mono8.web.csproj` serves it locally.

Installing the `wasm-tools` workload (`dotnet workload install wasm-tools`) is optional; it shrinks
the download and enables AOT.

### What is embedded

Everything in `../publishdata` except `data.save`, under the same `publishdata/<name>` resource
names the desktop build uses. Anything the mirror does not carry yet — the font and the icon sheet,
before the first save — is taken from `../data` instead, since the browser has no disk to fall back
to. `publishdata` wins wherever both hold the same file.

## What differs from the desktop build

| | Desktop | Web |
|---|---|---|
| Game loop | `Game.Run()` blocks | `requestAnimationFrame` calls `Tick()` per frame (`Pages/Index.razor.cs`) |
| Window | Sized and centred on the desktop; fullscreen when published | Fills the canvas the page gives it, and follows it on resize (`Mono8Game.SyncCanvasSize`) |
| Focus | Clicking away dims and freezes the frame | Always focused — the browser already stops the loop for a hidden tab |
| `dset` | `data.save` next to the executable | `localStorage`, key `data.save` (`core/common/WebStorage.cs`) |
| Pause menu | Continue / Restart Game / Exit | No **Exit** — a tab has nowhere to exit to |
| Alt+F4 | Quits | Nothing |
| F2 | Toggles fullscreen | Toggles fullscreen, if the browser grants it |

Audio is unchanged: a published build plays the bank baked at save time, which is the same
`DynamicSoundEffectInstance` path on both, over WebAudio here.

## Keeping the KNI version in step

`wwwroot/index.html` loads KNI's JavaScript half by explicit, version-stamped paths
(`_content/nkast.Wasm.Dom/js/Window.8.0.11.js` and friends). `8.0.11` is the `nkast.Wasm.*` version
that `nkast.Kni.Platform.Blazor.GL` depends on. If you bump the KNI packages, check that dependency
and update these script tags with it — a stale path 404s silently and takes input, audio or the
canvas with it.
