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
**highly deterministic** — procedural maze, rule-based entity, timed sanity drain. Deterministic
systems are exactly what make for clean, non-flaky agent-run tests, which is the whole point.

### The pitch loop (what a player does)

You "noclip" into **Level 0** — endless yellow wallpaper, damp carpet, buzzing fluorescent
lights, mono-hum. You must **find the exit** before your **sanity** runs out, while **an
entity** roams the rooms. Almond water restores sanity. Reach the exit = escape (win). Sanity
hits zero, or the entity catches you = game over.

- **Session length:** 1–3 minutes. Short enough to replay, short enough for a fast E2E.
- **Controls (mobile, touch-first):** left virtual stick = move, right-side drag = look,
  on-screen buttons = sprint / interact. Desktop fallback: WASD + mouse-look for editor testing.

---

## 2. Non-negotiable constraints

| Constraint | Consequence for design |
|---|---|
| **Mobile WebGL** | No VR. Touch controls. Keep the `.data` file small (GitHub Pages has a 100 MB/file limit and phones have limited memory). Procedural geometry + tiny textures, not big art. |
| **GitHub Pages hosting** | Pages cannot send `Content-Encoding: br/gzip` headers. Unity WebGL build **must** use *Decompression Fallback* ON (or Compression = Disabled) or the loader fails. See `Documentation/DEPLOYMENT.md`. |
| **Deterministic tests** | Seeded maze, fixed-clock sanity, rule-based entity. No `Random` without a seed, no wall-clock timing in logic. |
| **MooseRunner doctrine** | Application→Module hierarchy, concrete classes (zero-interface), Facade/Router, module-owned TestFacade seams, ≤400 lines/file. See `ArchitectureGuidelines.md`. |

---

## 3. Architecture (Application → Modules)

Namespace root: **`Backrooms`** (mirrors Knuckle Drift's `KnuckleDrift.<Module>` convention).
One asmdef per module (`Backrooms.<Module>`, tests `Backrooms.<Module>.Tests`).

```
Assets/Backrooms/
  Application/
    GameManager/            # lifecycle state machine: Menu → Playing → Won/Lost; win/lose rules
    Gameplay/
      Scenes/Backrooms.unity  # the REAL shipped scene (E2E loads this)
      Tests/                   # E2E suites live here
  Modules/
    MazeManager/            # procedural Level-0 maze generation (seeded, deterministic)
    PlayerManager/          # first-person move + look; touch + desktop input; TestFacade simulation seam
    EntityManager/          # the roaming entity: patrol / detect / chase / catch (NavMesh or grid)
    SanityManager/          # sanity drain over game-time; almond-water pickups; zero = lose
    UIManager/              # main menu, HUD, on-screen touch controls, game-over / win screens
    AudioManager/           # fluorescent hum, footsteps, entity cue (royalty-free / generated)
```

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
- **EntityManager** — deterministic state machine: `Patrol → Suspicious → Chase → Catch`.
  Detection by distance + line-of-sight. `TestFacade` can place the entity, force a state, and
  read the current state for assertions.
- **SanityManager** — drains on a **fixed game clock** (not wall-clock), so a test can advance
  time deterministically. `TestFacade` sets/reads sanity and simulates a pickup.
- **GameManager** — owns win/lose and scene flow (Menu ↔ Gameplay). Mirrors Knuckle Drift's
  `GameManager` pattern (state machine + session stats + real pause).

---

## 4. MooseRunner showcase — the three disciplines

The demo is designed so each MooseRunner capability has an obvious, compelling home.

### 4a. Agent-driven PlayMode tests (white-box)
Isolated, deterministic, direct-state assertions. Examples:
- `MazeGenerator_SameSeed_ProducesIdenticalLayout`
- `MazeGenerator_ExitAlwaysReachableFromSpawn` (100 seeds)
- `Sanity_DrainsOverTime_ReachesZero_TriggersLose`
- `Sanity_AlmondWater_RestoresAndClamps`
- `Entity_WithinDetectionRadiusWithLoS_TransitionsToChase`
- `Entity_CatchesPlayer_TriggersGameOver`
- `Player_MoveInput_MovesForward_CollidesWithWalls`

### 4b. Black-box E2E flows
Load the **real** `Backrooms.unity` scene, drive **only** simulated *physical* input, assert by
**reading** production state. Ordered `[Test, Order(n)]` chains:
- `E2E_Playthrough`: LoadScene → tap Start → move to exit → **win screen shown**.
- `E2E_SanityLoss`: LoadScene → Start → wait/idle until sanity 0 → **game-over shown**.
- `E2E_EntityCatch`: LoadScene → Start → walk into the entity's path → **game-over shown**.

### 4c. Visual validation
- **SessionRecorder** during an E2E: record the run, then extract frames / Gemini-analyse to
  confirm "yellow rooms render, entity is visible, HUD present" — catches magenta-material /
  black-screen regressions a state assert can't.
- **`screenshot`** loop while building the menu/HUD: capture → look → adjust.

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

| # | Milestone | Done when |
|---|---|---|
| **M0** ✅ | **Project + repo skeleton** | Unity 6 URP project created; MooseRunner + UniTask in manifest; `mooserunnerCli ping` green; repo pushed to `Valectric/BackroomsDemo`. |
| **M1** ▶ | **Walk the rooms** | MazeManager generates Level 0 ✅; geometry (walls/floor/ceiling/lights) ✅; PlayerManager first-person move+look, desktop **and touch** ✅; gameplay scene authored ✅; 19 PlayMode tests + 4-step escape E2E green ✅. **Remaining: WebGL build on Pages** (blocked on the WebGL module). |
| **M2** | **HUD + menu** | UIManager start screen, run timer, escape screen; replay without reloading. `screenshot`-validated. |
| **M3** | **Sanity + almond water + win/exit** | SanityManager drains, pickups restore, exit ends the run with a win screen; GameManager state machine; PlayMode tests green. |
| **M4** | **The entity** | EntityManager patrol/chase/catch; game-over on catch; PlayMode tests green. |
| **M5** | **E2E + visual validation** | Full E2E suites (playthrough / sanity-loss / entity-catch) green; SessionRecorder visual check passes. |
| **M6** | **Polish + share** | Audio (hum/footsteps), fog/lighting atmosphere, README with the play link + "built & tested by an AI agent with MooseRunner" story. Share the link. |

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
