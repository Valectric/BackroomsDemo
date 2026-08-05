# Backrooms Demo — agent guide

A small, single-player, **first-person Backrooms** game that **showcases MooseRunner** (agent-driven
PlayMode tests, black-box E2E, visual validation) and ships as a **mobile WebGL build on GitHub
Pages**. Sister project to *Knuckle Drift*.

**Play:** https://valectric.github.io/BackroomsDemo/ · **Repo:** https://github.com/Valectric/BackroomsDemo (public)

Read `HANDOVER.md` first for current state and what to do next.
Read `DECISIONS.md` before reversing anything — decisions are dated and reasoned.
Read `Documentation/DEPLOYMENT.md` before touching the WebGL build.
Read `PLAN.md` for milestones.

## The source material — this is NOT the meme Backrooms

The setting is ***Discount Dan: A LitRPG Adventure* by James A. Hunter** (series: *Discount Dan's
Backroom Bargains*). Canon that matters:

- Dan **noclips** into the Backrooms: an ever-changing **dungeon of 999+ floors** "cobbled together
  from twisted carnivals, abandoned shopping malls, janky laundromats, condemned insane asylums".
  It is **not** uniform yellow wallpaper.
- Tone: Alice-in-Wonderland surrealism crossed with SCP horror.
- Monsters are **Dwellers**, stronger the deeper you go.
- LitRPG layer: stats, levels, XP, loot, **relics**, inventory limits.
- Dan's hook: he opens a **convenience store / safe haven** and sells to other trapped survivors.
- **Croc**: a mimic sidekick that appears as a dog with a New Zealand accent.

**Do not use meme-canon details** — "almond water" and "Level 0" belong to the internet Backrooms,
not this book. This is an unofficial fan demo; say so in anything public-facing.

## Hard constraints (do not break)

- **Mobile WebGL, non-VR.** Touch-first; keep the download small; no XR.
- **CC0 assets only.** The repo is public and must stay clonable and buildable by anyone. Paid packs
  (Synty etc.) would have to be gitignored or make the repo private — both defeat the showcase.
- **GitHub Pages** cannot send `Content-Encoding`, so the build needs Decompression Fallback ON.
- **Deterministic generation** — seeded maze, seeded prop placement, fixed game clock. No unseeded
  `Random`, no wall-clock timing in logic.

## Architecture

Namespace root `Backrooms`; one asmdef per module (`Backrooms.<Module>`, tests
`Backrooms.<Module>.Tests`). Concrete classes, Facade/Router, module-owned TestFacade seams,
≤400 lines/file, XML docs on everything including tests.

```
Assets/Backrooms/
  Application/Gameplay/     GameplayController (run/floor flow), Scenes/Backrooms.unity, Tests/ (E2E)
  Modules/
    MazeManager/            generation, geometry, textures, props, floor themes + atmosphere
    PlayerManager/          first-person move/look, touch + desktop, sim-input seam
    EntityManager/          Dwellers: patrol/chase/catch, BFS pathing
    UIManager/              HUD (IMGUI): floor, timer, banners
  Editor/                   scene builder, WebGL builder, Kenney import, prop catalogue
ThirdParty/Kenney/          CC0 furniture (committed on purpose)
ThirdParty/FreesoundCC0/    CC0 per-floor ambience; Resources/Ambience/<PropStyle>/
```

## Working loop

```
mooserunnerCli ping                                    # daemon + Unity worker
mooserunnerCli test --assembly Backrooms.<M>.Tests     # unit suites
mooserunnerCli test --class Backrooms.Gameplay.Tests EscapeLevel0E2E   # E2E: --class, never --method
mooserunnerCli console --types error,warning --count 50                # ALWAYS after a run
```

Headless editor actions are driven by **sentinel files** at the project root, picked up by an
`EditorApplication.update` poller within ~30s (`Assets/Backrooms/Editor/BackroomsBuildTriggers.cs`):

```
touch .backrooms-build-scene       # regenerate Assets/Backrooms/.../Backrooms.unity
touch .backrooms-build-webgl       # build WebGL into docs/
touch .backrooms-reimport-kenney   # reimport the furniture pack with URP materials
touch .backrooms-build-catalog     # regenerate the prop catalogue
```

Equivalent menu items live under **Backrooms/** in the Unity menu bar.

Deploy: build into `docs/`, then `bash Tools/publish-pages.sh` — which force-replaces the orphan
`gh-pages` branch so the 21MB payload never accumulates in history. **`docs/` is build output; do not
commit it to `main`.** Pushing `gh-pages` also triggers the itch.io publish workflow. Check with
`gh api repos/Valectric/BackroomsDemo/pages/builds/latest --jq '.status'`.

## Traps that have already cost time — read before debugging

**Tooling**
- **`force-recompile` prints `[PASS]` even when C# compilation FAILED.** `console --types error` also
  shows *stale* errors from earlier compiles, so it cannot disambiguate. **The only reliable proof
  that code compiled is running a test.**
- A **brand-new test `.asmdef` needs TWO `force-recompile` passes** before `test --assembly` finds
  it; the first attempt reports "Assembly not found in loaded assemblies".
- **Sentinel builds are a no-op while the editor is in Play Mode**, which is where every test run
  leaves it. `EditorSceneManager.NewScene` throws, and the trigger used to consume the sentinel
  anyway — so the scene silently stayed stale while the loop looked green. `BackroomsBuildTriggers`
  now defers instead of consuming, but the ordering still matters: **`force-recompile` first to exit
  Play Mode, then `touch` the sentinel.** Touching first means the currently-loaded (possibly stale)
  assembly serialises the scene.
- **Serialized scene values beat code defaults.** Changing a `[SerializeField]` default does nothing
  to the shipped scene until it is rebuilt — a renamed field drops its old value, but a changed
  default does not apply. Check the value landed: `grep -n '<field>' Assets/Backrooms/.../Backrooms.unity`.
- Long CLI commands report `[MODAL]` when the editor is merely busy building. **Never `unity_stop`
  during a build** — that kills it. Open Package Manager / Project Settings windows also register as
  false-positive modals.

**Build / deploy**
- **MooseRunner's own runtime assemblies break player builds.** `MooseRunner.Helpers.Runtime.dll`
  (note the casing — it differs from its `MooseRunner.helper` asmdef) references nunit, and
  `MooseRunner.Internal.dll` references it, so IL2CPP fails with
  `Failed to resolve assembly: 'nunit.framework'`. `Editor/BuildAssemblyFilter.cs` strips the whole
  family from player builds, matched **case-insensitively**.
- **Aggressive stripping breaks URP.** `ManagedStrippingLevel.High` + `stripEngineCode` removes
  reflection-reached code and shows up as "shader not supported" and a frozen player. Keep Minimal.
- **Never ship `WebGLExceptionSupport.None`** — it turns every crash into `The error was: undefined`.
- **`Shader.Find` shaders are stripped from builds** unless registered in Graphics Settings, and the
  level then renders **magenta**. `Editor/AlwaysIncludedShaders.cs` runs as part of the build.
- **`.gitignore`'s `[Bb]uild/` also matches `docs/Build/`** — the WebGL payload. The `!docs/Build/`
  exception must stay or Pages serves an `index.html` with no game.
- **Bump `PlayerSettings.bundleVersion` every build** (the builder does) or Unity's data caching
  serves returning players the old build forever.
- WebGL builds can exhaust Windows commit memory (`paging file is too small` / `LLVM ERROR: out of
  memory`). No-admin fix: set the Unity process CPU affinity to ~4 cores so child compilers inherit
  it — `(Get-Process -Id <pid>).ProcessorAffinity = [IntPtr]0xF` — then restore it.

**Art / rendering**
- **Kenney models are authored at 5 units per metre.** Import with `useFileScale = false;
  globalScale = 0.2f` or a chair is 4.7 m tall. `PropCatalogBuilder` logs probe sizes — check them
  after any pack change.
- Kenney materials are flat named colours with **no textures**; an `AssetPostprocessor` generates
  URP materials per name, else everything renders magenta.
- Kenney furniture faces **−Z**, so aiming a transform's *forward* into the room turns its back on
  you. Model facing conventions cannot be asserted from a transform — check a picture.
- **Never couple brightness to a palette hue.** `ambient = fogColour * k` silently made dark-fog
  floors 6× dimmer. Take hue from the palette, set level explicitly (`FloorAtmosphere`).
- Light **range must be ≥ ~0.75 × fixture pitch** or pools cannot meet and parts of the floor get
  zero direct light.
- URP's additional-light limit is **per renderer**, so one giant mesh can only ever be lit by a
  handful of lights. Walls are chunked for this reason; floor and ceiling still are not.

## Verification doctrine

Green tests are necessary and **not sufficient**. Three whole classes of bug here were invisible to
every assertion and only a rendered frame caught them: stripped shaders (magenta), per-renderer light
limits (flat lighting), and model facing (furniture backwards).

- `FloorLookTests` photographs every floor: an **orthographic, fog-off, ceiling-hidden plan view**
  plus an eye-level view, into `Screenshots/`. Read the PNGs. `DwellerLookTests` and `HudLookTests`
  do the same for the pursuit tell and the HUD warning.
- **Green unit tests can hide a broken *rate*.** Every Dweller test passed while the shipped game let
  you cross a whole floor without meeting one: pathing, states and catching were each correct, and
  the encounter rate — the thing that was actually broken — was measured by none of them.
  `DwellerEncounterTests` simulates whole crossings and asserts a percentage. Reach for that shape
  whenever behaviour is correct but the game still feels wrong.
- **The editor is not the shipping renderer.** `QualitySettings` sets WebGL to tier 0 (Mobile,
  0.8 render scale, no MSAA) while the editor runs tier 1 (PC).
- For quick iteration, serve the build locally (`python -m http.server 8765` in `docs/`) and drive a
  browser rather than round-tripping through Pages.
- **`FloorAtmosphere.Apply` is the single atmosphere entry point** — the game and the screenshot
  tests both call it, so captures cannot drift from what ships. Keep it that way.

## Testing & architecture doctrine

@TestingGuidelines.md
@ArchitectureGuidelines.md

Regenerate both after any MooseRunner upgrade:
`mooserunnerCli get-testing-guidelines-md > TestingGuidelines.md` (and the architecture equivalent).

## Unity

Unity `6000.3.17f1` + URP. MooseRunner `2.2.5` (Valectric npm registry) and UniTask (OpenUPM) come
from scoped registries declared in `Packages/manifest.json`. WebGL Build Support module required.
