# Yard Playback + Lab Lighting

Submission for the *Yard playback + Lab lighting* assignment.
See the brief at [`Requirements/Yard playback + Lab lighting.md`](Requirements/Yard%20playback%20+%20Lab%20lighting.md).

## Unity version

| | |
|---|---|
| **Editor version** | **Unity 6000.0.69f1** (Unity 6, LTS line) |
| Revision | `5f8607f5118b` |
| Render pipeline | Universal RP (URP) 17.0.4 |
| Target platform | WebGL |

Open the project folder [`YardPlayback/`](YardPlayback/) with **exactly this
Editor version** (Unity 6000.0.69f1). Other 6000.0.x patch versions will usually
open it, but this is the version it was built and tested on.

## How to run it

> _To be completed when the player is built (assignment not yet started)._

1. Open [`YardPlayback/`](YardPlayback/) in Unity 6000.0.69f1.
2. Open the scene: `Assets/Scenes/…` _(TODO: name the playback scene)_.
3. Press **Play**.
4. Controls in the scene: **Play, Pause, 1×, 2×, Skip** _(TODO: where they are)_.

## Driving it from a web page

`index.html` sits at the repo root, **next to the project** (per brief §3), with
six controls: **Load, Start, Pause, Resume, Speed, Skip**.

> _To be completed:_ the Unity object name the page calls, the method names, and
> the finished payload (who is left + each load). _(TODO after the page contract
> is implemented.)_

A WebGL build wired to the page is optional; if present it lives in `Build/`.

## Lab lighting (section 4)

> _To be completed._ Mixed lighting mode + one-line justification, lightmap
> resolution, and `View_1`–`View_4` cameras. Baked data is committed so the scene
> opens lit without re-baking.

## Repository layout

```
LightAssignment/
├─ README.md          ← this file
├─ index.html         ← drives the Unity scene (built later)
├─ YardPlayback/      ← the Unity project
├─ Docs/              ← recording, screenshots, lighting stats, write-ups
└─ Requirements/      ← the assignment brief + lab package (inputs)
```

## Deliverables

See the **Send back** section of the brief. Tracked artifacts live in
[`Docs/`](Docs/) as they are produced (2-min recording, before/after `View_*`
screenshots, Lighting-window stats, 30-s moving-object clip, and the short
write-ups on the known bug, the mixed-lighting mode, and "another day").
