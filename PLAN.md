# Backrooms Demo — plan

A small, single-player, **first-person Backrooms** game built to **showcase MooseRunner**:
agent-driven PlayMode tests, black-box E2E flows, and visual validation. It ships as a
**mobile-friendly WebGL build hosted on GitHub Pages**, so anyone can play it from a phone
by opening a link.

> Sister project to `Knuckle Drift`. Same Unity (`6000.3.17f1`) + URP, same
> MooseRunner + architecture doctrine (see `TestingGuidelines.md`,
> `ArchitectureGuidelines.md`, and `CLAUDE.md`).

---

## 1. Why this game

The demo has two audiences at once:

1. **Players** — a short, atmospheric, works-on-your-phone Backrooms experience.
2. **MooseRunner evaluators** — people deciding whether to adopt MooseRunner. The value
   they need to *see* is: an AI agent can drive a real Unity game headlessly, write and run
   PlayMode + E2E tests, and visually verify the result.

Backrooms fits both: it is atmospheric and recognisable, yet mechanically simple and
**highly deterministic** — seeded maze, seeded prop placement, rule-based Dwellers. Deterministic
systems are exactly what make for clean, non-flaky agent-run tests, which is the whole point.

### The pitch loop (what a player does)

Setting: ***Discount Dan* by James A. Hunter** — see `DECISIONS.md` D4. You **noclip** into an
ever-changing dungeon of floors stitched from carnivals, malls, laundromats and asylums. Find the
exit on each floor to **descend**; each floor down is a different kind of space and the **Dweller**
hunting you is faster. It catches you, the run ends.

- **Session length:** a minute or two per floor.
- **Controls (mobile, touch-first):** left half of the screen = virtual stick from wherever you
  press, right half = look. Desktop: WASD, hold left mouse to look, Shift to sprint.

---

## 2. Non-negotiable constraints

| Constraint | Consequence for design |
|---|---|
| **Mobile WebGL** | No VR. Touch controls. Keep the `.data` file small (GitHub Pages has a 100 MB/file limit and phones have limited memory). Procedural geometry + tiny textures, not big art. |
| **GitHub Pages hosting** | Pages cannot send `Content-Encoding: br/gzip` headers. Unity WebGL build **must** use *Decompression Fallback* ON (or Compression = Disabled) or the loader fails. See `Documentation/DEPLOYMENT.md`. |
| **Deterministic tests** | Seeded maze, seeded prop placement, rule-based Dwellers. No `Random` without a seed, no wall-clock timing in logic. |
| **MooseRunner doctrine** | Application→Module hierarchy, concrete classes (zero-interface), Facade/Router, module-owned TestFacade seams, ≤400 lines/file. See `ArchitectureGuidelines.md`. |

---

## 3. Architecture (Application → Modules)

Namespace root: **`Backrooms`** (mirrors Knuckle Drift's `KnuckleDrift.<Module>` convention).
One asmdef per module (`Backrooms.<Module>`, tests `Backrooms.<Module>.Tests`).

```
Assets/Backrooms/
  Application/
    Gameplay/               # GameplayController: run + floor flow, win/lose
      Scenes/Backrooms.unity  # the REAL shipped scene (E2E loads this)
      Tests/                   # E2E suites live here
  Modules/
    MazeManager/            # seeded braided maze, geometry, procedural textures, props, floor themes
    PlayerManager/          # first-person move + look; touch + desktop input; TestFacade simulation seam
    EntityManager/          # Dwellers: patrol / chase / catch, BFS pathing over the grid
    UIManager/              # HUD: floor counter, run timer, arrival and end-of-run banners
  Editor/                   # scene builder, WebGL builder, Kenney import, prop catalogue
```

Not built, deliberately (`DECISIONS.md` D5): store hub, inventory, loot, stats, Croc, audio.

**Per-module shape (doctrine):** `«Module»Facade` (the one public door, MonoBehaviour that
self-bootstraps), `Internal/«Module»Router` (single-line wiring only), submodules under
`Internal/`, `Tests/`, and `«Module»TestFacade` for state inspection + simulation/mock seams.
Scenes are the glue — module Facades placed as GameObjects, wired via inspector. Graph stays
acyclic; cross-module calls go through public Facades only.

### Key module notes

- **MazeManager** — grid of rooms/corridors from a seed. Guarantees: exit is always
  reachable from spawn; no isolated pockets; bounded size. This is the richest source of clean
  PlayMode tests (determinism, connectivity, invariants).
- **PlayerManager** — `TestFacade` exposes a **simulation-input seam** (inbound): tests and
  E2E drive movement/look by intent ("move forward until at exit", "look at entity") without
  synthesizing raw Input System device events (forbidden by doctrine). Real touch + real
  desktop input both flow through the same handler.
- **EntityManager** — deterministic `Patrol → Chase → Caught` state machine. Senses the player
  **along open corridors**, never through walls, and paths by BFS. `TestFacade` places a Dweller and
  steps it a cell at a time so pathing is exactly testable without a scene.
- **UIManager** — IMGUI on purpose: no font assets or canvas prefabs, so the scene stays fully
  reproducible from code.

---

## 4. MooseRunner showcase — the three disciplines

The demo is designed so each MooseRunner capability has an obvious, compelling home.

### 4a. Agent-driven PlayMode tests (white-box)
Isolated, deterministic, direct-state assertions. Examples:
- `SameSeed_ProducesIdenticalLayout`, `ExitReachableFromSpawn_ForManySeeds` (BFS-verified)
- `Floors_HaveFewDeadEnds`, `Floors_ContainLoops`, `Floors_ContainOpenRooms`
- `NeverMovesThroughAWall` (Dweller, 40 steps, each verified against an open passage)
- `EntersChase_WhenPlayerIsWithinSenseRange`, `Chasing_ClosesDistanceEveryStep`
- `WallAhead_BlocksMovement`, `LookUp_PitchIsClamped`, `SimulationDisabled_IgnoresInjectedInput`

### 4b. Black-box E2E flows
Load the **real** `Backrooms.unity` scene, drive **only** simulated *physical* input, assert by
**reading** production state. Ordered `[Test, Order(n)]` chains:
- `EscapeLevel0E2E`: loads the shipped scene, confirms the player spawned in the maze's own spawn
  cell, then **BFS-solves the maze and physically walks it** with simulated input until the game
  itself reports the descent to floor 2.

### 4c. Visual validation
- `FloorLookTests` photographs every floor — an orthographic fog-off **plan view** plus an
  **eye-level** view — into `Screenshots/`, and checks furniture facing head-on.
- This has caught three bugs no assertion could: stripped shaders (magenta level), URP's
  per-renderer light limit (flat lighting), and model facing (furniture backwards).
- SessionRecorder video capture works but is disabled in the E2E — Unity Recorder leaves console
  errors on stop. See `Documentation/SETUP.md`.

This directly demonstrates the "an agent can *see* whether the game looks right" story.

---

## 5. Hosting & the "incremental build" flow (GitHub Pages)

Goal: a URL you open on your phone that updates as the demo grows. Full runbook in
`Documentation/DEPLOYMENT.md`. Summary:

**Phase A — manual (start here, zero secrets):**
1. Build WebGL from the editor with **Decompression Fallback ON** into `docs/` (repo root).
2. Commit + push. Enable **GitHub Pages → Deploy from branch → `main` / `docs`**.
3. Play at `https://<user>.github.io/BackroomsDemo/` on your phone.

**Phase B — automated CI (optional, true incremental):**
- `game-ci/unity-builder` GitHub Action builds WebGL on every push and publishes to Pages.
- Requires a Unity license secret (`UNITY_LICENSE` for Personal). Workflow stub in
  `.github/workflows/`. Adopt once the manual loop is proven.

**Why not OneDrive/Drive/Dropbox:** they serve a preview wrapper with wrong MIME types, not raw
files at stable paths — the Unity WebGL loader can't fetch `.wasm`/`.data`. Real static hosting
is required; GitHub Pages is it.

---

## 6. Milestones (incremental — each ends in a playable/testable build)

| # | Milestone | State |
|---|---|---|
| **M0** | Project + repo skeleton | ✅ Unity 6 URP, MooseRunner + UniTask, repo public at `Valectric/BackroomsDemo` |
| **M1** | Walk the rooms | ✅ Maze, geometry, first-person player (touch + desktop), gameplay scene, WebGL on Pages |
| **M2** | HUD | ✅ Floor counter, run timer, arrival + end-of-run banners (IMGUI, no font assets) |
| **M3** | Descend themed floors | ✅ Five palettes + prop styles, exit descends a floor, per-floor atmosphere |
| **M4** | Dwellers | ✅ BFS pathing hunters, faster each floor, catch ends the run |
| **M5** | Art pass | ▶ Procedural textures, trim, columns, Kenney CC0 furniture, two reviewer rounds applied. Remaining: wall-run placement, tall props, post-processing — see `HANDOVER.md` |
| **M6** | Polish + share | Audio, README with play link, final pass |

**Deliberately out of scope** for this demo (see `DECISIONS.md` D5): Dan's store hub, inventory,
loot, stats, and Croc.

Each milestone → a fresh WebGL build pushed to Pages, so there's always something to play.

---

## 7. Open items to confirm as we go

- **Entity pathing:** NavMesh (needs `com.unity.ai.navigation`, already familiar from KD) vs
  a lightweight grid-follow. Grid-follow is more deterministic for tests; NavMesh looks better.
  Lean grid-follow first, NavMesh if time.
- **Audio source:** reuse White Bat Audio royalty-free ambience (credit required, as in KD) or
  generate a simple procedural hum. TBD at M6.
- **CI license:** whether to set up `game-ci` (Phase B) or stay manual. Decide after M1.
- **Repo:** new GitHub repo `BackroomsDemo` under your account/org — confirm name + visibility.
```
