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

Open `Assets/Scenes/Lab_Starter.unity` (the lab package's scene). The baked data is
committed: the lightmap, light probes and reflection probes are in `Assets/Scenes/Lab_Starter/`.
The scene opens lit without re-baking.

- **Views:** `Views/View_1` to `View_4`, shown one at a time in the Game view.
  - Play mode: press **1–4**. A short hint explains this when Play starts; **Close** dismisses it.
  - Edit or Play mode: the menu **Lab › View 1–4** (Alt+1 to Alt+4).
  - Scene view: select a view and press **Ctrl+Shift+F** (Align View to Selected).
- **Moving object:** `Lighting/Probe test (moving, not static)` is lit by the light probes. In
  Play mode it moves from under the key light into the dark west corners and back.
- **Evidence** (in [`Docs/lab/`](Docs/lab/)):
  - before and after screenshots of the four views (`before/`, `after/`);
  - the Lighting window stats (`lighting-window-stats.png`);
  - the 30-second probe clip (`probe-test-30s.mp4`).

**Mode, resolution, more time:**
1. **Baked Indirect.** One mixed key spot gives moving objects real-time direct light and the
   scene's only real-time shadow. Everything else (16 fixture lights, bounce, AO) is baked, and
   unlike Shadowmask it needs no shadowmask textures.
2. **14 texels per unit** (about 7 cm), non-directional, max 1024. That gives one 1024² lightmap,
   **1.3 MB** in the WebGL build against the 8 MB budget. The FBX lightmap-UV padding is set for
   the same 14 texels per unit, and a clean re-bake reports no UV overlaps.
3. **With more time:**
   - Fit the area lights to the exact fixture shapes.
   - Add smaller reflection probes near the open end, because the mirror-finish metal floor
     shows its reflections more than its baked light.
   - Confirm whether the reactor and pack holders, which arrive at 1/100 scale in the delivered
     scene, are meant to be that size.

**What was changed (lighting only):**
- **Lights:** 8 baked ceiling area lights and 8 baked strip lights over the bench bays, at the
  visible fixtures, plus 1 mixed key spot. That is one real-time shadow-casting light, and
  real-time GI is off.
- **Probes:** a 294-point light-probe grid, and 4 box-projected reflection probes. Every surface
  is metallic and would otherwise reflect the sky.
- **Static flags:** the two west end walls, which weren't static, are now marked static.
- **Tiny objects:** the reactor, pack holders and two tiny wall copies take their light from
  probes. They are a few centimetres across, so they can't hold lightmap texels.
- **FBX import:** lightmap-UV padding set to match the lightmap resolution.
- **Ambient:** flat and neutral.
- **WebGL:** the Mobile URP asset (the WebGL quality level) now allows the key light's real-time
  shadow.
- **Not changed:** models, materials and layout.
- **One compatibility fix on import** (not a material change): the package's `PackedMaskLit.shader`
  includes a fog file that only exists in URP 17.1+. On this project's URP 17.0.4 that one line is
  replaced by the equivalent `#pragma multi_compile_fog`, so the materials compile instead of
  rendering pink. Material settings and textures are untouched.
- **Added for checking:** a test sphere and its mover script for the probe clip, and a view
  switcher (keys 1–4, the Lab menu, a hint when Play starts). Both are in `Assets/_Lab`; neither
  affects the lighting.

## Repository layout

```
LightAssignment/
├─ README.md          ← this file
├─ index.html         ← the page: six controls + the page contract
├─ Build/             ← the WebGL build the page drives (4 files)
├─ YardPlayback/      ← the Unity project
├─ Docs/              ← evidence: yard/ (the recording), lab/ (screenshots, stats, probe clip)
└─ Requirements/      ← the assignment brief + lab package (inputs)
```

## Deliverables

The brief's **Send back** list, item by item.

### Yard playback (sections 1–3)

| Item | Where | Status |
|---|---|---|
| Repo, with the Unity version and the steps to press Play | this README: [Unity version](#unity-version), [How to run it](#how-to-run-it) | done |
| `index.html` as described | [`index.html`](index.html), contract in [Driving it from a web page](#driving-it-from-a-web-page) | done |
| WebGL build wired to the page (optional) | [`Build/`](Build/), live on [GitHub Pages](https://kamyasuraj.github.io/YardPlaybackAssignment/) | done |
| Recording (2 minutes at most), sound off: play from the start, pause mid-carry, resume, 2×, then Skip | [`Docs/yard/recording.mp4`](Docs/yard/recording.mp4): 24 s, 1080p, no audio track. Recorded with Unity Recorder (an Editor-only package) clicking the in-scene buttons: Play 2.5 s, Pause mid-carry 5.4 s, Resume 9.4 s, 2× 15.4 s, Skip during setdown 19.1 s, then the end on `closing` | done |
| Three lines on a bug still in it | [Known bug](#known-bug) below | done |
| Three lines on another day, and whether it goes to the picture or the page | [Another day](#another-day) below | done |

### Lab lighting (section 4)

| Item | Where | Status |
|---|---|---|
| Baked lighting data committed, so the scene opens lit | `YardPlayback/Assets/Scenes/Lab_Starter/` | done |
| Before and after screenshots, `View_1` to `View_4` | [`Docs/lab/before/`](Docs/lab/before/), [`Docs/lab/after/`](Docs/lab/after/) | done |
| Lighting window stats (lightmap count and sizes) | [`Docs/lab/lighting-window-stats.png`](Docs/lab/lighting-window-stats.png) | done |
| 30-second clip of a non-static object through bright and dark | [`Docs/lab/probe-test-30s.mp4`](Docs/lab/probe-test-30s.mp4) | done |
| Three lines: mixed lighting mode, lightmap resolution, more time | [Mode, resolution, more time](#lab-lighting-section-4) above | done |

### Known bug

1. If a script's `closing` disagrees with what its cues produce, the yard plays `shift_over` (freeze
   and "SHIFT OVER") and only then animates to `closing`, so it moves after it says it has frozen.
2. The brief's shift-04 never hits this, because its cues already end on `closing`.
   `Tests/Scenarios/09_closing_disagrees.json` reproduces it.
3. Fix: settle the closing correction before a final `shift_over`, so the freeze is the last thing seen.

### Another day

1. Another day would go to the **picture**.
2. A director camera that frames each cue's marker, not just the setdown push-in, and labels that
   stay readable at small window sizes.
3. Richer primitive set-pieces: a visible mast lift on carry, the crate tipping off the forks on
   setdown, and a minute clock that ticks rather than rolls.
