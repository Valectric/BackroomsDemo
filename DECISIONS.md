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

## 2026-08-01 — D18. Encounter rate is a measured number, not a vibe

**Decided:** `DwellerEncounterTests` simulates a whole crossing of a shipping-size floor on the grid —
player walking the route to the nearest stairwell, Dwellers roaming at their real speed ratio — over
25 seeds, and asserts how often a Dweller starts hunting.
**Why:** the user reported "I don't see any dwellers" while every Dweller unit test was green. Pathing,
the state machine and catching were all individually correct. What was broken was the *rate*, which no
test that places a Dweller next to the player can see. Measured: the shipped configuration hunted the
player on **12%** of crossings; the fixed one on **84%**.
**Rules out:** tuning Dweller difficulty by feel. Change the numbers, re-run, read the percentage.
**Sweep result worth keeping:** sense range dominates (8→12 cells took 56%→84%); Dweller count barely
matters above three; patrol span is nearly flat. Tune sense range first.

## 2026-08-01 — D19. Dwellers patrol to a destination; a chase is unmistakable

**Decided:** patrolling Dwellers plan a route to a random far cell and walk it, instead of picking a
random neighbour each step. Three per floor, sense range 12 cells. A chasing Dweller opens glowing red
eyes, casts a red light, and raises a pulsing HUD border plus "A DWELLER HAS SEEN YOU".
**Why:** a random walk spreads as the square root of the steps taken, so on 576 cells one Dweller
effectively never arrives anywhere. And a Dweller that has noticed you looked identical to one that
has not, so even a successful encounter read as confusing rather than tense.
**Detail that matters:** sense range 12 cells is 48 m, slightly beyond the 45 m camera clip. A Dweller
notices you just before you could see it — the HUD warning fires first, then it comes out of the fog.
That ordering is deliberate.
**Wrong if:** 84% makes floors feel relentless rather than tense. Lower sense range, not count.

## 2026-08-01 — D20. Being caught is a losing condition you can leave

**Decided:** catching freezes the player, shows the run's floor and time, and restarts on a tap or
click. Confirm-press is read by PlayerManager (which owns input devices) and exposed as
`PlayerFacade.ConfirmPressed`, rather than the application layer touching the Input System.
**Why:** the caught banner had no way out — the run just stopped forever, which is a dead end rather
than a fail state.
**Trap found:** a Dweller left parked in `Caught` still reports having caught the player, so the next
run ended the instant it began. `Hide()` now clears router state, not just the body.

## 2026-08-01 — D21. Three opposed Dweller kinds, not one creature repeated

**Decided:** a floor carries one LURKER, one WATCHER and one SKITTER, dealt out in turn.
Differences live in a data table (`DwellerArchetypes`) rather than in subclasses, so the whole roster
is readable at a glance.

| | height | speed | sense | eyes |
|---|---|---|---|---|
| Lurker | 2.2 m | 2.2 m/s | 12 cells | 2, red |
| Watcher | 2.85 m | 1.58 m/s | 18 cells | 2, cold blue |
| Skitter | 1.05 m | 3.3 m/s | ~7 cells | 4, amber |

**Why:** three of the same creature teaches the player nothing. These are deliberately *opposed* — the
Watcher trades speed for sight, the Skitter sight for speed — so a Watcher is something you outrun but
cannot lose, and a Skitter something you can hide from but not outpace. `DwellerArchetypeTests`
asserts the trade-off holds: no kind may be both fastest and furthest-sighted, none may outrun a
sprint, and no two may be within 0.4 m in height (height is the cue that survives fog).
**Measured:** the mixed roster hunts the player on **88%** of crossings, against 84% for three
identical Dwellers — the Watcher's 18-cell sense more than covers the Skitter's blindness.
**Trap found twice, same root:** eyes are placed on the body's *surface*, computed per eye from the
capsule's silhouette radius at that height. A flat forward offset buries them inside a wide body or
leaves them hanging beside it, and both look exactly like the feature being broken. Nothing but a
screenshot catches it.

## 2026-08-01 — D22. Playthrough footage is recorded, not described

**Decided:** `PlaythroughRecording` plays the shipped scene through simulated input and records it
with MooseRunner's SessionRecorder to `.mooserunner/Recordings/playthrough`. It is `[Explicit]`
because Unity Recorder logs benign errors when it stops, and a suite that leaves console errors is
not a clean pass.
**Why:** reviewers judging still frames judge composition; reviewers judging a playthrough judge the
*game*. The first recorded run immediately produced a finding no still had: 112 seconds across three
floors with **no Dweller visible at all**, on a build measured to hunt the player 88% of the time.
**Note:** NUnit skips `[Explicit]` tests even under `--class` selection, so recording footage means
removing the attribute for the run and restoring it after.

## 2026-08-01 — D23. A chase must be able to end

**Decided:** hunting speed is a separate number from patrol speed — Lurker 3.89, Watcher 3.41,
Skitter 4.62 m/s, hard-capped at 5.1 so a sprint always escapes however deep the floor.
**Why:** every Dweller was slower than the player's 3.2 m/s walk. Only the Skitter beat it, by
0.1 m/s; the Lurker did not until floor 10 and the Watcher not until floor 20. The game reported the
player hunted on 88% of crossings and killed nobody, because a hunt was a flag rather than a pursuit.
Found by a reviewer watching recorded footage, not by any test.
**The test that should have caught it** asserted only that speed stayed *below* a sprint. An upper
bound says a chase is survivable; it takes both bounds to say a chase is a chase. Replaced with
`HuntingSpeed_CatchesAWalker_ButNotASprinter`, plus `AWalkingPlayer_IsSometimesCaught`, which
measures **catches** rather than hunts — the quantity that was never measured.

## 2026-08-01 — D24. Stairwell placement optimises coverage, not separation

**Decided:** stairwells are placed by furthest-point seeding plus a local search that minimises the
**longest walk any cell faces to the nearest way down**, bounded by a minimum distance from spawn.
**Why:** the old rule enforced a minimum *separation* between stairwells, which is a different
property from covering the floor and does not imply it. Measured over 30 seeds: worst cell 47 cells
from any way down, mean 33.4 — 188 m of walking on a 96 m floor. Now worst 29, mean 24.9. A reviewer
predicted this from the plan views before it was measured.
**Three strategies that measured worse — do not re-attempt:**
- Pure furthest-point selection drives stairwells to the extremes, close to the worst shape for
  coverage (mean 37.2, worse than the rule it replaced).
- Repeatedly relocating a stairwell onto the single worst-served cell stalls on its first pass: with
  only three, vacating any one opens a hole at least as big as the one it fills.
- Sampling candidates biased towards badly-served cells is backwards — the best site for a stairwell
  is a well-connected middle cell, so the bias rejects the cells worth trying.
**Coupling worth knowing:** shorter routes mean less exposure, so this change alone dropped the
Dweller hunt rate from 96% to 28%. A minimum spawn distance of 16 cells recovers it to 44% while
keeping the coverage win. Stairwell placement, floor size and Dweller sensing are one system; tuning
any of them alone moves the others.

## 2026-08-01 — D25. Stairwell coverage needs a doorstep guard on both passes

**Decided:** cells within 16 maze-cells of the spawn are ineligible to hold a stairwell, enforced in
**both** the greedy seeding and the local search.
**Why:** minimising the worst walk, on its own, puts a way down next to where the player arrives —
the mirror image of the long-walk bug. The chain is specific: the first stairwell is the cell
furthest from spawn, the distance field is then reseeded from that stairwell alone, and the cell
furthest from *it* is the spawn's own corner. Measured: the shortest spawn-to-stairs walk was **2
cells**. Guarding only the local search was not enough, because the greedy pass had already placed it
and no single move improved coverage by removing it.
**Now:** shortest 16 cells, mean 19.9; worst walk 29, mean 24.6.
**How it was found:** a reviewer read the new code and predicted the regression from the reseeding
chain before it was measured. The cheap check they proposed — log spawn-to-nearest-stairs across the
existing seeds — took one test and confirmed it immediately. `TheNearestWayDown_IsNeverOnTheDoorstep`
now guards it.

## 2026-08-01 — D26. Relics make descending a decision

**Decided:** a new `RelicManager` module places one relic per floor at the cell with the **longest
walk to the nearest stairwell or the spawn** — the cell the stairwell placement works hardest to
avoid creating. Violet, on a plinth, throwing light far enough to be noticed from somewhere the
player was not going.
**Why:** the game had one verb — walk to the green thing. Every floor asked the same question and the
answer was always the same. A relic at the far end of the floor makes each floor ask instead: leave
now, or go and get it. It is also on-canon; relics are core to *Discount Dan*.
**Rules out:** placing relics for looks. `ARelic_IsAGenuineDetour` measures the detour across 20
seeds and fails if a relic is ever collectable in passing.
**Colour is load-bearing:** green already means "way down" and the player has learned it; the
creatures own red, cold blue and amber. Violet is the only saturated hue left that says "worth going
to" without saying "exit" or "danger".
**Also added:** a run summary and a persisted best, because a relic the player cannot compare against
anything is a noise and a number.

## 2026-08-01 — D27. Audio is synthesised, never imported

**Decided:** a new `AudioManager` module generates every sound at runtime from float arrays — room
hum, footsteps, a pursuit drone, a relic chime, a descent tone.
**Why:** the repo may only carry CC0 assets, which makes a sound library a licensing audit and had
kept the game silent. A waveform computed from a formula has no licence to audit. It is also a few
kilobytes of code against megabytes of clips on a build that loads over a phone connection.
**The trap, and the test that caught it:** a looping clip must contain a whole number of cycles of
**every** partial. The drone's detuned partial was expressed as a frequency ratio (1.0136×), which
left it part-way through a cycle at the buffer's end — a click on every repeat, forever.
`LoopingWaveforms_MeetAtTheSeam` measured the seam discontinuity at 0.184 against an interior step of
0.006 and failed before anyone heard it. Detune is now expressed as **one extra whole cycle across
the loop**, which cannot desynchronise.
**How to test audio without listening:** clipping, loop-seam discontinuity, DC offset, silence and
determinism are all measurable from the samples. Those five cover the faults that make generated
audio unusable.

## 2026-08-01 — D28. Relics carry powers, bound to gestures

**Decided:** six relic kinds, dealt out one per floor so descending offers something new each time —
three compasses (nearest Dweller, nearest stairs, nearest relic), a Ward that absorbs one catch, a
Blink Shard, and a Banisher with five shots. Powers fire on **double taps**: look side blinks, move
side banishes.
**Why:** a relic that is only a counter is a collectible, not a decision. With powers, the detour buys
something the player can feel, and each floor asks a different question because each floor offers a
different relic.
**Gestures, not buttons:** the touch scheme deliberately has no on-screen widgets — the left half is
already a stick and the right half is already the camera — so a double tap is the only input left
that steals from neither. Desktop gets the same gesture on the same halves rather than a separate key,
so there is one thing to learn.
**Detail that matters:** a recognised double tap is *consumed*, so drumming a finger fires once rather
than emptying a relic. And a Banisher shot only spends a charge if something actually dies; a shot
into an empty corridor that still costs a charge reads as the relic being broken.
**The Ward has to remove the Dweller too.** Cancelling only the catch leaves the creature standing on
the player, and it catches them again the next frame — the ward would appear to have done nothing.

## 2026-08-01 — D29. Control hints are idle-triggered, not timed

**Decided:** the touch-zone hints appear only after the player has given **no input for ten seconds**,
and vanish the instant they do anything.
**Why:** user correction, and the right call. A hint shown on arrival covers the middle of the screen
for someone who is already walking and already knows how. Someone who has stood still for ten seconds
is either lost or has not realised the screen halves do different things — which is exactly the person
the hint is for.
**Also:** the game now detects a portrait screen and asks for the phone to be turned. The level is
built around a wide field of view and a HUD anchored to the corners; in portrait it is not merely
uglier, it is harder to play.

## 2026-08-01 — D30. Footsteps are body, not surface

**Decided:** the footstep is two cascaded low-passes over noise plus a 62 Hz sine thump, played
quieter and pitched down.
**Why:** user feedback — the single gentle filter left enough upper content that every step read as a
slap on a hard floor. On a phone speaker the sub-bass thump is what actually carries a footfall; the
filtered noise alone is all texture and no body.

