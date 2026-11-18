# Yard playback + Lab lighting

**8 hours total: 6 for Yard playback (sections 1–3), 2 for Lab lighting (section 4).** Your own Unity project, 2022 LTS or Unity 6. Unity primitives only. No asset-store packs, no downloaded models. The one exception is the lab package we provide for section 4.

A web page will embed a scene like this and hand it a script. You play that script. You do not invent outcomes. If an animation and the file disagree, the file wins.

We are looking at three things in Yard playback:

1. **The player.** The script is obeyed, including pause, speed, and skip.
2. **The picture.** Someone can follow the yard with the sound off.
3. **The page.** An outside page can drive the scene and hear when it finishes.

Finish them in that order. If time runs out, drop a WebGL build first, then extra camera work. Do not drop the end state or Skip.

Section 4 is a separate test with its own time box. Do not take time from sections 1–3 for it, or the reverse.

## 1. The player

A yard with two lanes, north and south.

- Place one marker per entry in `opening`. Show each marker's `load` as a number.
- Play `cues` in order. One cue finishes before the next starts.
  - `clock` updates a minute label.
  - `carry` moves `who` and shows `what`. Then apply `patch.set`.
  - `slip` is a visible stumble. Numbers do not change.
  - `mark` pins a visible flag on `who`.
  - `tick` flashes that marker, then applies `patch.set`.
  - `setdown` plays a drop. Apply `patch.set`. Anything listed in `patch.gone` leaves the yard.
  - `shift_over` freezes the yard.
- When the script ends, lanes, loads, and who is present match `closing`. `crate-a` is gone. `fork-1` shows load **2**.
- Loads change only inside `patch.set`. `amount` is a label for the flash, not a number to add or subtract. After `carry`, `fork-1` shows **6**. After `tick`, `fork-1` shows **5**.
- Controls: Play, Pause, 1×, 2×, Skip.
  - Skip snaps to `closing` immediately. No leftover motion. Pressing Skip twice stays on `closing`.
  - Pause freezes motion and the cue clock. Resume continues the same cue.
  - Changing speed does not skip or repeat a cue.

A new cue type should be addable without rewriting the player.

## 2. The picture

Direct the scene. Primitives are enough if the shot is.

- Both lanes read in one glance. North and south are distinct.
- The marker involved in the current cue is the one the eye goes to.
- With the sound off, `carry`, `slip`, `mark`, `tick`, and `setdown` cannot be confused.
- Motion eases in and out. Nothing pops except Skip, which cuts.
- The load numbers and the minute stay readable for the whole recording.
- `setdown` is the moment of the clip. The crate leaving should be obvious.

## 3. The page

Add an `index.html` next to the project with six controls: Load, Start, Pause, Resume, Speed, Skip.

- Load sends the script as **one string**.
- The other buttons call one Unity object by name.
- When playback finishes, the page shows who is left and each load.
- A WebGL build wired to that page is welcome. It is not required.
- If you do not make a WebGL build, the HTML still names the object, the methods, and the finished payload, and your recording shows those same controls working inside Unity.

## 4. The lab

**2 hours.** Import `lab-setup.unitypackage` into the same project and open `Lab.unity`. The lab arrives unlit. Light it and bake it so it could ship in a WebGL build.

- Lighting only. Keep the render pipeline the package uses. Do not change models, materials, or layout. You may add lights, probes, and volumes, and change lighting and import settings.
- The room reads as a working lab. Bench tops are evenly lit. Nothing important sits in black shadow. The visible fixtures explain where the light comes from.
- Static geometry is baked. Mark it static, generate lightmap UVs where they are missing, and fix any overlaps the lightmapper reports.
- Pick a mixed lighting mode (Baked Indirect, Shadowmask, or Subtractive). Say why in one line.
- Objects that move are not static. They are lit by light probes and must match their surroundings. One that moves into a dark corner gets darker. It does not stay bright or go black.
- Add reflection probes where metal or glass would otherwise reflect the skybox.
- Web budget:
  - Lightmaps total **under 8 MB** in the build.
  - At most **one** realtime shadow-casting light.
  - No realtime GI. WebGL does not support it.
- Clean bake: no light leaking through wall and floor joins, no visible seams on bench tops, no blotchy or noisy patches on large flat surfaces.
- Save four fixed views as cameras in the scene (`View_1` to `View_4`) so we can check from the same spots.

## Script

```json
{
  "ticket": "shift-04",
  "opening": [
    { "id": "fork-1", "lane": "north", "load": 3 },
    { "id": "fork-2", "lane": "south", "load": 0 },
    { "id": "crate-a", "lane": "north", "load": 1 }
  ],
  "cues": [
    { "say": "clock", "minute": 1, "patch": {} },
    { "say": "carry", "who": "fork-1", "what": "crate-a", "patch": { "set": [{ "id": "fork-1", "load": 6 }] } },
    { "say": "slip", "who": "fork-2", "patch": {} },
    { "say": "mark", "who": "fork-1", "flag": "late", "patch": {} },
    { "say": "clock", "minute": 2, "patch": {} },
    { "say": "tick", "who": "fork-1", "flag": "late", "amount": 2, "patch": { "set": [{ "id": "fork-1", "load": 5 }] } },
    { "say": "setdown", "who": "fork-1", "what": "crate-a", "patch": { "set": [{ "id": "fork-1", "load": 2 }], "gone": ["crate-a"] } },
    { "say": "shift_over", "patch": {} }
  ],
  "closing": [
    { "id": "fork-1", "lane": "north", "load": 2 },
    { "id": "fork-2", "lane": "south", "load": 0 }
  ]
}
```

## Send back

- A repo or zip. Unity version in the README, and the steps to press Play.
- `index.html` as described above.
- A 2-minute recording, sound off: play from the start, pause mid-carry, resume, switch to 2×, then Skip.
- Three lines on a bug you know is still in it.
- Three lines on what you would do with another day. Say whether that day would go to the picture or the page.
- For the lab:
  - Baked lighting data committed, so the scene opens lit without re-baking.
  - Before and after screenshots from `View_1` to `View_4`.
  - A screenshot of the Lighting window stats (lightmap count and sizes).
  - A 30-second clip moving one non-static object through bright and dark parts of the room.
  - Three lines covering your mixed lighting mode, your lightmap resolution, and what you would fix with more time.

## Checked

- The ending matches `closing`, including after Skip is pressed twice.
- After `carry` the load is 6. After `tick` the load is 5. Those values were not calculated from `amount`.
- Pause, resume, and 2× do not drop or repeat a cue.
- The five actions are distinct with the sound off.
- The page contract is specific enough that another person could call it.
- The lab opens lit. Re-baking from a clean clone finishes without UV overlap warnings and gives the same result.
- Moving objects take their light from probes and match the room around them.
- Lightmaps are under 8 MB, with no more than one realtime shadow-casting light and no realtime GI.
- No leaks, seams, or blotches in the four views.