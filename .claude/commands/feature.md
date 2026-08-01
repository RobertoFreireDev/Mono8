---
description: Implement a game feature in src/game/ using only the IMono8API surface
argument-hint: <feature description, with sprite ids / map layer / sfx / music / json objects>
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(dotnet build:*)
---

Implement this feature in the Mono8 game:

$ARGUMENTS

## Boundaries — non-negotiable

1. **Write only inside [src/game/](src/game/).** Read anything for context; create and edit nothing else. Not [src/IMono8API.cs](src/IMono8API.cs), not [src/core/](src/core/), not [src/editor/](src/editor/), not [src/mono8.csproj](src/mono8.csproj), and above all **not [src/data/](src/data/)** — sprites, map, flags, sfx, music and `data.json` are authored by the developer in the built-in editors.
2. **Every engine call goes through `API` (`IMono8API`).** No MonoGame types, no engine internals, no `System.IO`, no LINQ, no reflection, no threads or wall-clock timing. `Constants.Colors.*` is fine.
3. If the feature genuinely cannot be built on the existing API, **say so and stop** rather than reaching into the engine. Describe what is missing and the nearest thing that *is* achievable.

Full context is in [CLAUDE.md](CLAUDE.md) — read it if it is not already in context.

## Before writing code

Check the request for the asset details you need. Depending on the feature, that is some of:

- **sprite ids** — which id, and how many 8×8 tiles wide/tall (`spr(id, x, y, w, h)`)
- **map** — which of the four layers (cell origin `(0,0)` / `(256,0)` / `(0,288)` / `(256,288)`), which cell region, and what `layerMax` flag bits split background from foreground
- **collision** — flagged tiles (`mcol` + which flag `0`-`7`) or autotile terrain (`acol` + the block's sprite id)
- **audio** — sfx index `0`-`63` and channel, music pattern `0`-`63`
- **json** — the exact `GROUP` / `OBJECT` / `FIELD` names and their types, which you read with `gjson` and write with `sjson`
- **persistence** — which `dget`/`dset` slot `0`-`63`

**Never invent an id.** If something is missing, ask one short question listing exactly what you need, then wait. Only fall back to a marked `// TODO: developer to confirm` placeholder when the developer explicitly said to pick one.

If the request names json data, read [src/data/data.json](src/data/data.json) to confirm the group/object/field names and types before coding against them. Same for flags in [src/data/data.gff](src/data/data.gff) and autotile blocks in [src/data/data.atl](src/data/data.atl). Read only.

## Writing it

- Put every id the developer gave you in a named `const int` at the top of the class (`SprPlayer`, `SfxJump`, `FlagSolid`, `MapLayerX`) — a re-authored sheet should be a one-line change.
- Keep [src/game/YourGame.cs](src/game/YourGame.cs) as the coordinator: `Init` / `Update(elapsedSeconds)` / `Draw` and the objects they own. Put a substantial feature in its own file in [src/game/](src/game/) — namespace `mono8.game`, `internal` type, compiled automatically.
- Never change `YourGame`'s class name, constructor, `IEditor` implementation or the three method signatures.
- Reset all state in `Init()` — it runs on every Ctrl+R and on pause-menu Restart.
- Multiply movement by `elapsedSeconds`; don't hardcode the 30 FPS step.
- No allocation in `Update`/`Draw` for anything that could be a field.
- Match the existing code's style: file-scoped namespace, 4-space indent, comments that say *why* and only where a reader would otherwise wonder.

## Finishing

- Verify with `dotnet build src/mono8.csproj`. **Do not run the app** — it is a GUI that opens the editors; the developer runs it with Ctrl+R.
- Report: which files you added or changed, which asset ids the code now depends on, and anything you assumed. If part of the request was left out, say which part and why.
- Don't commit unless asked. `src/data/*` shows as modified whenever the developer saves in an editor — leave it alone.
