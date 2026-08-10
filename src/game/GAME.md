# Mono8 — Game

The state of the game in [src/game/](.). Right now there is no game: the folder is an empty template.

This file is the counterpart to [CLAUDE.md](../../CLAUDE.md). That one holds the rules — what game code
may and may not do. This one holds what reading a single file cannot tell you: what the game *is*, the
contracts that span files, which `data.json` group feeds which system, what the developer has authored,
and what is known missing. **Keep it current when you change how the game works.**

---

## What the game is

Not decided yet. Describe it here in a paragraph or two once it is: the genre, the goal, what a
session looks like, and the control map.

| Button | Key | Does |
|---|---|---|
| 0 / 1 | ← → | |
| 2 / 3 | ↑ ↓ | |
| 4 | Z | |
| 5 | X | |
| 6 | C | |
| 7 | V | |

### Pause menu

The engine allows five custom entries and lists them in index order, so **the index is the row**.
Record who owns each index here as they are registered, since two systems claiming one silently
overwrite each other.

| Index | Entry | Owner | Up on |
|---|---|---|---|
| — | — | — | — |

---

## Frame flow

[YourGame.cs](YourGame.cs) is the only class the engine calls, and today its three methods are empty
apart from a `cls`. As systems appear, sketch the order they run in here — `Init` first, then the
`Update` chain, then the back-to-front `Draw` order — and note the places the order is load-bearing.

```
Init()    —

Update()  —

Draw()    cls
```

---

## Files

| File | Owns |
|---|---|
| [YourGame.cs](YourGame.cs) | Entry point: `Init` / `Update` / `Draw`. Empty template |
| [API_REFERENCE.md](API_REFERENCE.md) | Full `IMono8API` reference. Documentation, not game code |

New game files go here, namespace `mono8.game`, `internal`, and are compiled automatically.

**Every type here is in scope engine-wide** — [src/GlobalUsings.cs](../GlobalUsings.cs) carries
`global using mono8.game`, so a game type whose name collides with a MonoGame one (`Color`, `Game`,
`Buttons`, `Keys`, …) breaks the *engine's* build. Name around the collision.

---

## Contracts across files

Nothing spans two files yet. This is where an invariant goes once one system depends on how another
one behaves — which sprite flag means what, which persistence slot holds what, which system is
allowed to write a value the rest read.

---

## data.json map

Nothing authored, nothing read. **Nothing should be tuned in code**: speeds, spawns, hit boxes, sprite
ids and HUD geometry belong in `data.json`, read through `gjson` in `Init()`. As groups are authored,
list them here so the next reader knows which system consumes which object.

| Group / object | Read by | Holds |
|---|---|---|
| — | — | — |

Read [src/publishdata/data.json](../publishdata/data.json) before writing any `gjson` call — it is the
authored source. When a feature needs a field the file does not hold, name the group/object/field for
the developer to author; game code cannot add one.

---

## Not done yet

Everything. There is no game code beyond the entry point.
