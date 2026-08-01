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
| Tests | **50 green** (26 maze, 7 player, 7 HUD, 6 Dweller, 4 E2E), console clean |
| Gameplay | Descend themed floors; find the exit to go deeper; a Dweller hunts you; caught = run over |
| Floors | Yellow Rooms → Abandoned Mall → Janky Laundromat → Twisted Carnival → Condemned Asylum, then wraps |
| Art | Procedural wallpaper/carpet/ceiling textures, skirting, structural columns, Kenney CC0 furniture |
| Controls | Phone: left half = virtual stick, right half = look. Desktop: WASD + hold-LMB to look, Shift sprint |
| Confirmed | Runs on the user's phone; corridors navigable; touch controls work |

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

**H1 — Place wall props along continuous wall runs, not per cell.** *Biggest visual win.*
Every prop currently sits dead-centre in a 4 m cell, so the level reads as a lattice. Fixing the
placement *errors* in round 1 unmasked this. Collect collinear closed sides into runs, lay pieces
end-to-end at continuous offsets (gap 0.15–2.4 m), target ~35% wall coverage, yaw ±6°, and skip a
1.2 m band at every doorway. Clustering, surface clutter and rugs all want this model underneath
them.

**H2 — Fix the overlap-rejection rule before building clustering on it.**
`PropDecorator` scans row-major and destroys the *later* of any conflicting pair, so south/west
always wins and there is no retry. Harmless today (<3% fire rate) but it will deterministically gut
clusters from the north-east inward. Build all candidate placements, shuffle with the seeded RNG,
then place; retry with a smaller model on collision.

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

**H7 — Chunk floor and ceiling like the walls.** They are single level-spanning quads, so URP's
per-renderer additional-light limit means they receive only a handful of lights.

**H8 — Per-floor prop tinting.** The same orange Kenney chair appears on all five floors, which reads
as an asset flip. Lerp albedo toward the floor palette via `MaterialPropertyBlock`; reserve high
saturation for the exit marker so chroma means "go here".

**Smaller, known:** `WallGap = 0.14` clears the skirting but leaves a visible slot behind tall
pieces (seat solid-based pieces at 0.02 instead); colliders are stripped from all furniture so you
walk through it; light and column lattices are globally fixed rather than room-relative; non-tileable
Perlin puts a 2 m grid on wall/floor textures; walls are zero-thickness with no door jambs.

## Questions outstanding for the user

- Is it **too dark to navigate** on a phone in daylight? Last change dropped ambient substantially.
- Does the Dweller feel tense or annoying? Is ~5 cells of sense range right?
- Is a 12×12 floor the right size for a blind run?

---

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
