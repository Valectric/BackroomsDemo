# Handover — state as of 2026-08-01

Start here. Then `CLAUDE.md` (constraints + traps), then `DECISIONS.md` (why things are the way they
are). `PLAN.md` holds the milestone view.

---

## What this is

A first-person Backrooms game — setting from the novel ***Discount Dan* by James A. Hunter**, not the
internet meme (see `DECISIONS.md` D4) — built to **showcase MooseRunner** and playable in a phone
browser.

- **Live:** https://valectric.github.io/BackroomsDemo/
- **Repo:** https://github.com/Valectric/BackroomsDemo (public, `main`)
- **Build:** 20 MB WebGL in `docs/`, served by Pages

## Current state — working and deployed

| | |
|---|---|
| Tests | **110 green** (40 maze, 11 player, 13 HUD, 21 Dweller, 15 relic, 6 audio, 4 E2E), console clean |
| Gameplay | Descend themed floors; find any of **three stairwells** to go deeper; detour for a **relic** at the far end of each floor; a **Lurker, a Watcher and a Skitter** hunt you; caught = run over, tap to retry |
| Floors | **24×24 cells (96 m square)**. Yellow Rooms → Abandoned Mall → Janky Laundromat → Twisted Carnival → Condemned Asylum, then wraps |
| Art | Procedural wallpaper/carpet/ceiling textures, skirting, structural columns, Kenney CC0 furniture laid along wall runs |
| Controls | Phone: left half = stick (double-tap = Banisher), right half = look (double-tap = Blink). Desktop: WASD + hold-LMB, Shift sprint, same double-taps by screen half |
| Confirmed | Runs on the user's phone; corridors navigable; touch controls work — **all at the old 12×12 size** |

## Verified environment

- Unity **6000.3.17f1** + URP, WebGL Build Support installed
- MooseRunner **2.2.5**; `TestingGuidelines.md` / `ArchitectureGuidelines.md` regenerated 2026-08-01
  from the CLI and match the installed package (2.2.5.0 — no drift)
- `mooserunnerCli ping` → PONG. Run it as `./MooseRunner/mooserunnerCli.exe` from the project root
- Licence already activated; no one-time UI step outstanding
- `gh` authenticated as **JohanHoltby**, admin of the **Valectric** org

## Fresh-machine setup

See `Documentation/SETUP.md`. Short version: clone, open in Unity 6000.3.17f1 (packages resolve from
the Valectric + OpenUPM scoped registries), open MooseRunner once to activate, `ping`.

---

## Open work, highest value first

These come from three exploratory reviewers (art direction, level design, technical defects) run
twice over screenshots + code. Their findings are summarised here; the reasoning is in `DECISIONS.md`
D11.

**~~H1~~ — DONE 2026-08-01.** Wall props are laid along continuous `WallRun`s (see `DECISIONS.md`
D15). `WallRunPlanner` collects the runs, `WallRunDresser` fills them, `WallRunTests` covers the
planner. Prop counts are logged by `FloorLookTests` — read them before changing coverage.

**H2 — Fix the overlap-rejection rule.** *Partly done.* Runs are now shuffled with the seeded RNG
before dressing, so the row-major "south and west always win" bias is gone, and a run retries up to
three model draws when one is too wide. Still outstanding: on an actual *collision* the piece is
destroyed and the slot left empty rather than retried with a smaller model. Matters most where two
runs meet at an inside corner.

**H3 — Tall props on every floor.**
Four of five floors have nothing above **1.1 m** against a 3 m wall, so nothing ever breaks a
sightline and fog does all the occlusion. Add bookcases/coat racks/cabinets to all styles and enforce
that ~40% of wall picks are ≥1.6 m.

**H4 — Post-processing volume.** None exists. Order matters: **film grain first** (banding is
measurable — 7 distinct luminance levels across a 700 px ceiling; grain is the cheapest dither), then
tonemapping (fixtures currently hard-clip), then bloom, vignette, slight contrast.

**H5 — Material and texture leaks.** Every floor rebuild orphans ~32 materials and three 256²
textures; `Destroy` on a GameObject does not free runtime `Material`/`Texture2D`. On WebGL the heap
is fixed at build time. Track created objects and destroy them in `RebuildGeometry`; use
`tex.Apply(true, true)`.

**H6 — Bound the room carving.** Rooms are placed at uniform random positions with no overlap or
area budget and may touch the border, so ~45% of a floor can merge into one shapeless hall. Cap the
room-cell union at ~25%, inset from the border, and add partition stubs inside large rooms.

**H7 — Chunk floor and ceiling like the walls.** The **ceiling** is still one level-spanning quad, so
URP's per-renderer additional-light limit means it receives only a handful of lights — and it is four
times the area it used to be. The floor is now built per cell (to cut stairwell holes) but still
emitted as one mesh; grouping those cells into chunks is a small change in
`MazeMeshBuilder.BuildFloorWithHoles`.

**H8 — Per-floor prop tinting.** The same orange Kenney chair appears on all five floors, which reads
as an asset flip. Lerp albedo toward the floor palette via `MaterialPropertyBlock`; reserve high
saturation for the exit marker so chroma means "go here".

**Smaller, known:** `WallGap = 0.14` clears the skirting but leaves a visible slot behind tall
pieces (seat solid-based pieces at 0.02 instead); colliders are stripped from all furniture so you
walk through it; light and column lattices are globally fixed rather than room-relative; non-tileable
Perlin puts a 2 m grid on wall/floor textures; walls are zero-thickness with no door jambs.

## Questions outstanding for the user

- **The threat numbers do not meet, and this is the biggest open design question.** A Watcher
  notices you at 72 m but closes on a walking player at 0.21 m/s, so it needs ~343 s to reach you;
  the Lurker needs ~69 s. A floor takes 20-60 s to cross and fog hides everything past ~25 m. So a
  chase starts outside the visible world and usually cannot resolve. Current measured state: hunted
  on 44% of crossings, caught on 12%. The reviewers' fix is to cut sense range towards fog distance
  (~6 cells) so chases begin where they can be seen and end before the floor does — at the cost of a
  lower hunt rate. Not done: it is a difficulty decision for the user.
- **Do 500–760 props per floor cost too much on a phone?** Fog and a 45 m far clip bound what is
  drawn per frame, but not instantiation or the fixed WebGL heap. `FloorLookTests` logs the count.
- Is it **too dark to navigate** on a phone in daylight? Last change dropped ambient substantially.
- Are three stairwells the right number, and is the green ceiling sign findable enough across 96 m?

---

## Recording footage

`PlaythroughRecording` plays the shipped scene and records it with SessionRecorder to
`.mooserunner/Recordings/playthrough` (video.mp4 + per-object motion). It is `[Explicit]` — **NUnit
skips Explicit tests even under `--class` selection**, so remove the attribute for the run and put it
back afterwards. `MooseRunner/.env` carries `FFMPEG_PATH` and a `GEMINI_API_KEY` (copied from CADcog,
2026-08-01), so `recording_extract_and_analyze` works for whole-segment video critique. For a frame
contact sheet, call ffmpeg directly — one CLI round-trip per frame is far slower.

## How to work on this

Read the **Traps** section of `CLAUDE.md` before debugging anything. The three that have cost the
most time:

1. **`force-recompile` reports `[PASS]` even when compilation failed.** Only running a test proves
   code compiled. Console errors are retained from previous compiles and cannot disambiguate.
2. **Green tests are not sufficient for anything visual.** Read the PNGs in `Screenshots/`.
3. **The editor is not the shipping renderer** (WebGL runs the Mobile quality tier).

Typical loop:

```bash
# edit code
./MooseRunner/mooserunnerCli.exe force-recompile --timeout 300
./MooseRunner/mooserunnerCli.exe test --assembly Backrooms.MazeManager.Tests
touch .backrooms-build-scene     # if scene composition changed
./MooseRunner/mooserunnerCli.exe test --class Backrooms.MazeManager.Tests FloorLookTests
# read Screenshots/*.png and judge with your eyes
touch .backrooms-build-webgl     # ~5 min
git add -A && git commit && git push          # Pages redeploys from main /docs
```

Verify locally before deploying: `cd docs && python -m http.server 8765`, then load
`http://localhost:8765/` in a browser — seconds per iteration instead of minutes.
