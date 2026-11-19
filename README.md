# Yard Playback + Lab Lighting

Submission for the *Yard playback + Lab lighting* assignment.
See the brief at [`Requirements/Yard playback + Lab lighting.md`](Requirements/Yard%20playback%20+%20Lab%20lighting.md).

## Unity version

| | |
|---|---|
| **Editor version** | **Unity 6000.0.69f1** (Unity 6, LTS line) |
| Revision | `5f8607f5118b` |
| Render pipeline | Universal RP (URP) 17.0.4 |
| Target platform | WebGL (active build target) |

Open the project folder [`YardPlayback/`](YardPlayback/) with **exactly this
Editor version** (Unity 6000.0.69f1). Other 6000.0.x patch versions will usually
open it, but this is the version it was built and tested on.

## How to run it

1. Open [`YardPlayback/`](YardPlayback/) in Unity 6000.0.69f1.
2. Open the scene `Assets/Scenes/Yard.unity`.
3. Press the Editor's **Play** button. The brief's `shift-04` script loads on its own.
4. Use the control bar at the bottom of the Game view: **Play, Pause, 1×, 2×, Skip**.

To drive the scene the way the page does, open **Yard › Page Controls** while in Play mode.
It has the page's six controls (Load, Start, Pause, Resume, Speed, Skip). Each one calls
the object named `YardPlayer` by name, and the window shows the finished payload.

## Driving it from a web page

[`index.html`](index.html) sits at the repo root, **next to the project** (brief §3), with
six controls: **Load, Start, Pause, Resume, Speed, Skip**. Every control calls one Unity
object by name: **`YardPlayer`**.

| Control | Call | Value |
|---|---|---|
| Load | `SendMessage("YardPlayer", "Load", script)` | the whole script as **one string** (JSON text) |
| Start | `SendMessage("YardPlayer", "Play")` | — |
| Pause | `SendMessage("YardPlayer", "Pause")` | — |
| Resume | `SendMessage("YardPlayer", "Resume")` | — |
| Speed | `SendMessage("YardPlayer", "SetSpeed", n)` | `1` or `2` |
| Skip | `SendMessage("YardPlayer", "Skip")` | — |

Speed is one control, a menu: pick 1× or 2× and that value is sent. It keeps no state, so
it stays correct when the in-scene 1×/2× buttons are also used.

**Finished:** Unity dispatches a `yard:finished` event on `window`. Its `event.detail` is a
JSON string giving who is left and each load, e.g.
`{"ticket":"shift-04","left":[{"id":"fork-1","lane":"north","load":2},{"id":"fork-2","lane":"south","load":0}]}`.

### WebGL build

The page drives a WebGL build of the Yard scene whose files sit in `Build/` next to it.

- **Online (GitHub Pages):** <https://kamyasuraj.github.io/YardPlaybackAssignment/>.
  The repo serves `index.html` and `Build/` as they are; the empty `.nojekyll` file stops
  GitHub from processing them. To enable it: Settings → Pages → Deploy from a branch →
  the default branch, folder `/ (root)`.
- **Locally:** a WebGL build must be served over http, because browsers block it when it is
  opened from `file://`. From the repo root, run `python -m http.server 8000`, then open
  <http://localhost:8000/index.html>.
- **Without the build:** opened directly, `index.html` shows a "No WebGL build" note. It
  still names the contract, and each control shows the exact call it makes.

To rebuild:
1. Set the target to **WebGL**.
2. In Player Settings, check **Gzip** compression with **Decompression Fallback** on, and
   **Run In Background** on, so the yard keeps playing while the page's buttons have focus.
3. Build only `Assets/Scenes/Yard.unity` to a staging folder named `YardPlayback`, so the
   files are called `YardPlayback.*`.
4. Copy just its `Build/` files into `Build/` here. Unity's own `index.html` is not used.

## Lab lighting (section 4)

> _To be completed._ Mixed lighting mode + one-line justification, lightmap
> resolution, and `View_1`–`View_4` cameras. Baked data is committed so the scene
> opens lit without re-baking.

## Repository layout

```
LightAssignment/
├─ README.md          ← this file
├─ index.html         ← the page: six controls + the page contract
├─ Build/             ← the WebGL build the page drives (4 files)
├─ YardPlayback/      ← the Unity project
├─ Docs/              ← recording, screenshots, lighting stats, write-ups
└─ Requirements/      ← the assignment brief + lab package (inputs)
```

## Deliverables

See the **Send back** section of the brief. Tracked artifacts live in
[`Docs/`](Docs/) as they are produced (2-min recording, before/after `View_*`
screenshots, Lighting-window stats, 30-s moving-object clip, and the short
write-ups on the known bug, the mixed-lighting mode, and "another day").
