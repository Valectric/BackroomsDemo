# Decision log

Dated, reasoned decisions. **Read this before reversing anything** — most entries exist because the
obvious alternative was tried and failed. Add new entries at the bottom; do not rewrite history,
supersede it.

Format: what was decided, why, what it rules out, and how to tell if it was wrong.

---

## 2026-07-30 — D1. Build a separate demo, not a slice of Knuckle Drift

**Decided:** new sister project rather than extracting from Knuckle Drift.
**Why:** Knuckle Drift is VR; VR cannot run in mobile WebGL. A shareable browser demo has to be a
non-VR, touch-first game.
**Rules out:** reusing KD gameplay code directly.
**Wrong if:** the demo's value turns out to depend on KD-specific systems.

## 2026-07-30 — D2. Host on GitHub Pages, not cloud storage

**Decided:** GitHub Pages serving `main` `/docs`.
**Why:** OneDrive / Google Drive / Dropbox serve a preview wrapper with wrong MIME types at unstable
paths; the Unity WebGL loader cannot fetch `.wasm`/`.data` from them. Pages is real static hosting,
free, and git-driven so redeploy is a push.
**Rules out:** drag-and-drop hosting; also forces Decompression Fallback ON, since Pages cannot send
`Content-Encoding`.
**Wrong if:** build size outgrows Pages limits (100 MB/file) or we need server-side headers.

## 2026-07-30 — D3. Author scenes and builds from code, triggered by sentinel files

**Decided:** editor scripts (`BackroomsSceneBuilder`, `BackroomsWebGLBuilder`, …) invoked by
sentinel files that an `EditorApplication.update` poller consumes.
**Why:** `edit-asset` cannot create GameObjects, and unity-mcp was unreliable. This needs no extra
services, is reproducible, and is committed to the repo.
**Superseded detail:** the trigger was originally `[InitializeOnLoadMethod]` on domain reload — that
silently did nothing when `force-recompile` had no source changes to compile. Polling replaced it.
**Rules out:** depending on unity-mcp for headless editing.
**Wrong if:** Unity ever offers a supported headless scene-authoring API.

## 2026-07-31 — D4. The setting is the *Discount Dan* novel, not the meme Backrooms

**Decided:** follow *Discount Dan: A LitRPG Adventure* by James A. Hunter — a 999-floor dungeon
stitched from carnivals, malls, laundromats and asylums.
**Why:** user correction. The first build assumed internet/film canon (uniform yellow wallpaper,
"Level 0", almond water), which is a different fiction.
**Rules out:** meme-canon details; unofficial fan status must be stated publicly.
**Wrong if:** the user wants a generic Backrooms game after all.

## 2026-07-31 — D5. Descend themed floors; keep the demo minimal

**Decided:** reaching the exit drops you a floor; each floor has its own palette, name and prop
style. Explicitly **out of scope**: Dan's store hub, inventory, loot, stats, Croc.
**Why:** user asked for "descend the floors… but keep it minimal. This is a demo." The floors slice
reuses the existing exit mechanic and gives visual variety plus escalation for little work.
**Wrong if:** the demo needs the store to feel like *Discount Dan* specifically.

## 2026-07-31 — D6. Dwellers are the fail state

**Decided:** one Dweller per floor, deterministic BFS pathing, faster each floor, catch ends the run.
**Why:** without a fail state it was an atmosphere piece, not a game.
**Detail that matters:** Dwellers spawn in a far corner, never on the exit — camping the one cell the
player must reach is unwinnable-feeling.
**Wrong if:** playtesting shows the chase is frustrating rather than tense.

## 2026-07-31 — D7. Express Dweller speed in metres per second

**Decided:** `metresPerSecond`, comparable directly against player walk (3.2) and sprint (5.6).
**Why:** it was `cellsPerSecond` multiplied by a 4 m cell size, so a "1.3" Dweller actually moved at
5.2 m/s — faster than walking, and by floor 3 faster than sprinting. Unescapable, and invisible in
code review because the number looked small.
**Rules out:** grid-relative speed units anywhere in gameplay tuning.

## 2026-07-31 — D8. Braided maze with carved rooms, not a perfect maze

**Decided:** generate a perfect maze, then carve rooms and open ~80% of dead ends.
**Why:** user feedback — a perfect maze is "too much one way", tedious, all dead ends. Dead ends fell
from ~30% of cells to ~2%.
**Key property:** both passes only ever *open* walls, so full connectivity is preserved by
construction and the connectivity tests still hold.
**Open concern:** room carving is unbounded and can merge into one large hall (see H3 in HANDOVER).

## 2026-07-31 — D9. CC0 art (Kenney), not paid packs

**Decided:** Kenney Furniture Kit (CC0), committed into the repo. Attribution given though the
licence does not require it.
**Why:** the repo is a public showcase and must be clonable and buildable. Synty's licence forbids
sharing source assets outside seat-holders, which would force the assets out of the repo or the repo
private.
**Rules out:** Synty and other paid packs while the repo is public.

## 2026-07-31 — D10. Delete the procedural furniture generator

**Decided:** removed `PropMeshLibrary` / `MeshParts` and the fallback path; imported models only.
**Why:** compared side by side, generated furniture tinted from the floor palette produced yellow
furniture on yellow walls, simpler silhouettes, and a visible mesh artefact. Kenney models bring
their own colour, which is what makes a space read.
**Rules out:** a no-assets fallback. Acceptable because the CC0 pack is committed, so the catalogue
is always present.
**Wrong if:** we ever need to ship without the pack.

## 2026-08-01 — D11. Reviewer panels drive the visual work

**Decided:** run three exploratory reviewers (art direction, level design, technical defects) over
current screenshots + code, then implement the union.
**Why:** self-review had stalled — I was no longer seeing problems the user could see immediately.
Round 1 found 30+ issues including three provable geometry bugs; round 2 found the ambient/hue
coupling that no amount of taste-tuning would have located.
**Practice:** give reviewers an explicit exclusion list of already-fixed and already-planned items so
they hunt new ground, and ask each to state what got **worse**.

## 2026-08-01 — D12. Palette supplies hue; brightness is set explicitly

**Decided:** `FloorAtmosphere` normalises fog colour to unit luminance, then multiplies by an
explicit `AmbientLevel` / `FogLevel`.
**Why:** `ambient = fog * 0.30` coupled brightness to a hue choice. After gamma→linear, dark-fog
floors received **6.4× less light** than the entry floor. Nobody art-directed that; carnival and
asylum rendered 87–90% near-black.
**General rule:** never derive a lighting *level* from a colour chosen for its *hue*.

## 2026-08-01 — D13. One atmosphere entry point, shared with the tests

**Decided:** `FloorAtmosphere.Apply(theme)` is the only place ambient/fog are set; the game, the
scene builder and the screenshot tests all call it.
**Why:** atmosphere was set in three places with different values (scene asset 0.018 Exponential,
scene builder 0.018, runtime 0.045 ExpSq). Screenshot tooling had its own copy, so captures could
photograph settings that never ship — verification against a lie.
**Wrong if:** a floor ever needs atmosphere the shared path cannot express (extend the theme instead
of forking the call).

## 2026-08-01 — D14. Screenshots are part of the test suite

**Decided:** `FloorLookTests` captures an orthographic fog-off plan view plus an eye-level view of
every floor into `Screenshots/`, and a head-on furniture facing check.
**Why:** three classes of bug were invisible to every assertion — stripped shaders (magenta level),
per-renderer light limits (flat lighting), model facing (furniture backwards). Only a frame catches
them. The earlier perspective-through-fog "overhead" shots were useless for auditing layout.
**Rules out:** treating a green suite as sufficient verification for anything visual.

## 2026-08-01 — D15. Wall furniture is laid along wall *runs*, not per cell

**Decided:** collect collinear closed cell-sides into maximal `WallRun`s and lay furniture end-to-end
along them at free offsets — coverage-driven, gaps scattered from the leftover length, yaw ±6°, 1.2 m
clear at doorways. Per-cell placement is gone for wall pieces; islands survive but are jittered off
the cell centre.
**Why:** every prop sat dead-centre in a 4 m cell, so the floor read as a lattice — the same grid as
the walls, drawn a second time in furniture. Fixing the placement *errors* in the reviewer round only
made the grid clearer.
**Load-bearing detail:** a run stops where the two cells stop being mutually reachable, or it lays a
sideboard through a perpendicular wall into the next room. It also records *why* each end stopped —
doorway or corner — because only a doorway needs the 1.2 m inset.
**Rules out:** per-cell furniture density as a tuning knob. Density is now coverage per run plus a
length-weighted skip: 4 m stubs are mostly left bare (80%), long walls mostly dressed (70%), which is
what produces furnished stretches next to empty ones instead of one piece on every wall.
**Wrong if:** prop counts (494–762 per 24×24 floor, logged by `FloorLookTests`) turn out to cost too
much on a real phone. Lower `MinCoverage`/`MaxCoverage` in `WallRunDresser` first — that thins every
wall evenly; raise the skip chances to leave whole walls bare instead.

## 2026-08-01 — D16. Floors are 24×24 with three stairwells down

**Decided:** the shipping floor goes from 12×12 to 24×24 (four times the area, a 96 m square), and
the single exit becomes three stairwells scattered across it. `MazeLayout.Exit` is replaced by
`Stairs`; reaching *any* of them descends.
**Why:** user request. The two halves are one decision: a 4× floor with one exit is a long blind
search, and three ways down keep a floor to a few minutes. The E2E's route from spawn to the nearest
stairwell came out at 18 cells, versus a 24×24 floor's worst case of ~46.
**Detail that matters:** stairwells are placed with a spacing requirement (half the grid span from
spawn, 0.45 of it from each other) that is *relaxed in steps* until the count is met — a fixed
threshold cannot be satisfied on a small grid and the generator still has to return three.
**Also changed:** `MazeSettings.RoomCount` now derives from area (one room per 64 cells) so room
density holds as the grid grows; it still evaluates to 4 at 16×16, so nothing about the old floors
moved.
**Wrong if:** one Dweller wandering 4× the area never finds the player. Its sense range is 5 cells
and it starts in a far corner — that tuning is untested at this size.

## 2026-08-01 — D17. Stairwells are a real hole in the floor, over an intact collider

**Decided:** the floor mesh is built per cell and omits the stairwell cells, so the shaft, its lining
and a six-tread flight are genuinely visible. A *separate*, unbroken plane carries the mesh collider.
**Why:** "stairs" has to look like stairs, and a green pillar did not. But a player who could fall
into the hole would be stuck in a decorative pit — descending is triggered by proximity at 2 m, which
fires as they cross the cell edge, long before the invisible span matters.
**Second-order win:** the per-cell floor mesh is the same code H7 wants for chunking the floor.
**Trap found:** with no tonemapping in the pipeline, an unlit material brighter than 1.0 clips the
green channel first, so the stairs sign rendered as a blank white slab. Emissive markers that carry
meaning through *colour* must stay at strength 1 until H4 lands.
