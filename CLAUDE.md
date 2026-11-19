# CLAUDE.md — Yard Playback + Lab Lighting

Team charter and project context for agents working in this repository.
Read this first. Then read the authoritative brief before touching anything.

---

## 0. Hard rule (non-negotiable)

> **Strictly adhere to the assignment's instructions and objectives — nothing more,
> nothing less.** Build exactly what
> [`Requirements/Yard playback + Lab lighting.md`](Requirements/Yard%20playback%20+%20Lab%20lighting.md)
> asks, the way it asks. Do not add scope, features, or "improvements" it did not
> request; do not skip, reinterpret, or soften any requirement. When in doubt,
> re-read the brief and follow it literally — **the file wins.** Every decision,
> every deliverable, every role is in service of meeting the assignment's stated
> instructions and the **Checked** / **Send back** criteria. This rule overrides
> any conflicting preference below.

---

## 1. Objective

Complete the assignment described in
[`Requirements/Yard playback + Lab lighting.md`](Requirements/Yard%20playback%20+%20Lab%20lighting.md).

That file is the **single source of truth**. If anything here disagrees with it,
the brief wins. Do not paraphrase its numbers from memory — open it and quote it.

> **Status: NOT STARTED.** Do not begin implementation until the owner explicitly
> says to start. Setup, planning, orientation, and role bootstrapping are fine.
> Building scenes, writing gameplay C#, importing the lab package, or baking is not.

One rule that governs the whole of Yard playback: **the script file is the truth.**
You play the script; you do not invent outcomes. If an animation and the file
disagree, the file wins. (E.g. loads change *only* inside `patch.set`; `amount`
is a label for a flash, never a number to add or subtract.)

---

## 2. What this project is

A Unity assignment in two **independent, separately time-boxed** parts:

| Part | Budget | What it is |
|------|--------|-----------|
| **Yard playback** (brief §1–3) | 6 h | A script-driven scene player: forklifts/crates in a two-lane yard (north/south), driven by a JSON script with cues (`clock`, `carry`, `slip`, `mark`, `tick`, `setdown`, `shift_over`). Controls: Play, Pause, 1×, 2×, Skip. Plus a readable visual "picture" and an `index.html` page that drives the Unity object and reads back the finished state. |
| **Lab lighting** (brief §4) | 2 h | Import the provided `lab-setup.unitypackage`, open `Lab.unity`, light & bake it for a WebGL budget. Lighting only — no model/material/layout changes. Save `View_1`–`View_4` cameras. |

**Do not take time from one part for the other.** They are graded separately.

### Repository layout

The **repo/zip root** is a wrapper above the Unity project. `YardPlayback/` holds
*only* the Unity project; everything else — `index.html`, the README, the evidence,
the inputs — lives beside it. This matches the brief: `index.html` goes **next to
the project** (§3), and the README sits at the top of the submission (§Send back).

```
LightAssignment/                 ← repo / zip root (working directory, where this file lives)
├─ CLAUDE.md                     ← this file (team charter — internal, not a deliverable)
├─ README.md                     ← deliverable README: Unity version + how to Play (§Send back)
├─ index.html                    ← drives Unity, "next to the project" (§3) — created at build time
├─ Build/                        ← (optional) WebGL build output index.html points to
├─ YardPlayback/                 ← the Unity project ONLY
│  ├─ Assets/                    ← URP template content (SampleScene, Settings, etc.)
│  ├─ Packages/manifest.json     ← package list
│  └─ ProjectSettings/           ← Unity 6000.0.69f1
├─ Docs/                         ← evidence deliverables: recording, screenshots, stats, clip, write-ups
└─ Requirements/                 ← assignment inputs (was "Assignment/") — reference, optional in final zip
   ├─ Yard playback + Lab lighting.md   ← THE BRIEF (source of truth)
   └─ unity test.unitypackage           ← lab package for §4 (~22 MB, not yet imported)
```

Keep `YardPlayback/` a clean Unity project: no `Requirements/`, `Docs/`, or
`index.html` inside it, and nothing non-Unity in `Assets/`. `Requirements/` is an
**input** we were given, not something the brief asks us to send back — keep it for
reference; it's optional in the final zip.

---

## 3. Tech stack & environment

- **Unity 6000.0.69f1 (Unity 6)**, **URP 17.0.4**. Keep the render pipeline the
  project / lab package uses. Do not switch pipelines.
- Key packages: Input System (1.8.1), Test Framework (1.4.2), Timeline, uGUI,
  AI Navigation, Visual Scripting.
- **Primitives only** for Yard playback — no asset-store packs, no downloaded
  models. The lab package (§4) is the one allowed exception.
- **Target: WebGL.** Keep everything WebGL-shippable (no realtime GI, lightmap
  budget, single realtime shadow light — see constraints below).
- **Drive Unity with the Unity CLI — NOT Unity MCP** (owner's standing rule). Use
  the `unity:unity-cli` skill: run the Editor in batch mode (`-batchmode -quit
  -projectPath YardPlayback -executeMethod ...`) for imports, tests, light bakes,
  and builds. Do not call the Unity MCP tools, even if the bridge is connected.
- **Blender MCP** is connected but not expected to be needed (primitives only).
- `Assets/Editor/HubForceResolve.cs` is a one-shot package-resolve helper that
  deletes itself; leave it alone.

---

## 4. Hard constraints (non-negotiable — from the brief)

These are the things graders explicitly check. Violating one fails that item.

**Yard playback**
- Loads change **only** inside `patch.set`. After `carry`, `fork-1` shows **6**;
  after `tick`, **5**; end state **2**. These are set, not computed from `amount`.
- Skip snaps to `closing` immediately, no leftover motion; pressing Skip twice
  stays on `closing`. Never drop the end state or Skip.
- Pause freezes motion **and** the cue clock; resume continues the same cue.
  Changing speed never skips or repeats a cue. One cue finishes before the next.
- A new cue type must be addable **without rewriting the player** (extensibility —
  TA owns this design).
- With sound off, `carry`, `slip`, `mark`, `tick`, `setdown` must be
  unmistakably distinct. Motion eases in/out; nothing pops except Skip (it cuts).
  Load numbers and the minute label stay readable the whole recording.
- The page contract (object name + methods + finished payload) must be specific
  enough that another person could call it. Load sends the script as **one string**.

**Lab lighting**
- Lightmaps total **under 8 MB** in the build. **At most one** realtime
  shadow-casting light. **No realtime GI.**
- Static geometry baked (mark static, generate missing lightmap UVs, fix overlaps).
  Moving objects are non-static and lit by **light probes**, matching the room.
- Clean bake: no light leaks at wall/floor joins, no bench-top seams, no blotches.
- Pick a mixed lighting mode (Baked Indirect / Shadowmask / Subtractive) and
  justify it in one line. Commit baked data so the scene opens lit.

**Priority if time runs out** (brief §Intro): WebGL build first, then extra
camera work. Never drop the end state or Skip.

---

## 5. Roles

Three agent roles for now. The owner adds more if needed. Each agent working a
task adopts exactly one role and signs its handoffs with the role tag + project
initials, e.g. **`[TA/YP]`**, **`[SA/YP]`**, **`[ST/YP]`**.

> **TA is also the developer.** Of the three roles, **TA owns and carries out all
> implementation** (the C#, scenes, `index.html`, and lab execution) in addition to
> the architecture, contracts, and plan. SA and ST do not write feature code.

### TA — Technical Architect & Developer  *(the default/orchestrating role)*
**Mandate:** Own the design, the contracts, the plan — **and the implementation.**
Decide *how* it is built so the pieces fit and stay changeable, then build it.
- **Owns:** system architecture; the cue/player extensibility design (new cue
  types without rewriting the player); the JS↔Unity page contract (object name,
  method signatures, finished payload shape); folder/assembly structure; the
  lab's lighting-mode decision and bake strategy; the delivery plan and
  time-boxing between §1–3 and §4; **and the implementation** — C# player/cue
  handlers, primitive scenes, UI, `index.html` + JS bridge, lab setup & bake,
  WebGL build, and the root `README.md`.
- **Does:** break the brief into tasks and sequence them; define the interfaces
  then implement against them; keep cue types pluggable, not special-cased; ease
  motion in/out; make the five actions visually distinct; wire page methods
  exactly as the contract names them; route the input path to SA for a security
  pass and the work to ST for verification; resolve ambiguity by re-reading the
  brief (file wins).
- **Does not:** expand scope beyond the brief (hard rule, §0); sign off its own
  work as done — ST verifies.
- **Hands off to:** SA the page contract + the untrusted-input path for a security
  pass; ST the "Checked" items the work must satisfy.

### SA — Security Analyst
**Mandate:** Keep the build safe and the brief's constraints honored — review the
untrusted input path, the surface the page exposes, and content/dependency
hygiene. **No security scope the brief does not need** (hard rule, §0).
- **Owns:** review of the external input path (§3: the page hands Unity the script
  as **one untrusted string**) — safe JSON parsing, graceful handling of malformed
  or oversized input, no crash and no arbitrary behavior; the public method
  surface (only the six controls + Load that the contract names are reachable —
  nothing extra exposed on the named object); content & supply-chain hygiene
  (primitives only, no downloaded models/packs/asset-store code — the brief
  mandates this); WebGL build hygiene (no secrets, no unexpected network calls).
- **Does:** review TA's page contract and implementation for input-validation and
  exposure risks; confirm the script parser handles bad input predictably; confirm
  no third-party/downloaded content slipped in; flag anything that widens the
  attack surface beyond the brief's controls.
- **Does not:** implement, or add security features the brief doesn't ask for
  (hard rule); redesign — raises findings with TA. Does not own functional QA (ST).
- **Hands off to:** TA (findings needing a fix or contract/design change), ST
  (security-relevant cases to fold into verification).

### ST — Software Tester
**Mandate:** Prove each item on the brief's **Checked** list, with the sound off.
- **Owns:** verification against §Checked and §Send back; test plans; EditMode/
  PlayMode tests where they add value; the evidence (recordings, screenshots,
  lighting-stats capture); the "one known bug" and "another day" write-ups.
- **Does:** confirm the ending matches `closing` (incl. Skip pressed twice);
  that `carry`→6 / `tick`→5 were *set* not computed; that Pause/Resume/2× never
  drop or repeat a cue; that the five actions are distinguishable silently; that
  lightmaps < 8 MB with ≤1 realtime shadow light and no realtime GI; that a
  clean re-bake reproduces the result with no UV-overlap warnings; that moving
  objects darken correctly in shadow. Reports defects precisely (repro → result).
- **Does not:** fix code directly — files defects back to TA, the implementer
  (small obvious fixes may be proposed, but TA owns the change). Does not
  redesign — escalates to TA.
- **Hands off to:** TA (defects, ambiguity, or design gaps found in testing),
  SA (anything that looks like a security or input-handling weakness).

### Decision authority & escalation
- Ambiguity in requirements → **re-read the brief; the file wins.** If still
  unclear, TA decides and records a one-line assumption for the README/handoff.
- Contract or architecture change → **TA approves.**
- "Is it done?" → **ST confirms** against the Checked list; nobody signs off
  their own work.
- Anything outside these three roles, or a genuinely owner-level call (scope,
  priorities, what to drop) → **escalate to the owner.**

---

## 6. Working agreement

- **Plan before building.** TA produces the task breakdown; the owner greenlights
  the start. (We are pre-start now.)
- **Definition of Done** for any section = its items on the brief's **Checked**
  list pass, verified by ST, with evidence captured for **Send back**.
- **Small, reviewable steps.** Keep scripts and scenes modular; prefer additive
  changes over rewrites (the player especially must stay open to new cue types).
- **Report faithfully.** If a check fails, say so with the evidence. Don't claim
  done without ST verification.
- **Stay in scope.** No extra features before the Checked list is green and the
  required deliverables exist. Priority order under time pressure is the brief's.
- **Deliverables (brief §Send back) — track these:** repo/zip + README (Unity
  version, how to Play); `index.html`; 2-min silent recording (play → pause
  mid-carry → resume → 2× → Skip); 3 lines on a known bug; 3 lines on "another
  day" (picture or page?); lab: committed baked data, before/after `View_1`–`4`
  screenshots, Lighting-window stats screenshot, 30-s clip of a non-static object
  through bright/dark, 3 lines on mode + lightmap resolution + what you'd fix.

---

## 7. Conventions

- Use markdown links for file references, relative to the repo root
  (`LightAssignment/`, where this file lives).
- Keep temporary/scratch files out of the repo and `Assets/` (use the scratchpad).
- Sign handoffs and status notes with the role tag, e.g. `[TA/YP]`.
- Git: not initialized yet. If/when versioning is wanted, init at the **repo root**
  (`LightAssignment/`) so `CLAUDE.md`, `index.html`, `Docs/`, `Requirements/`, and
  the `YardPlayback/` Unity project version together as one deliverable. Add a
  `.gitignore` for Unity (`Library/`, `Temp/`, `Logs/`, `obj/`, `*.csproj`, `Build/`).
