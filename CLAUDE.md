# Backrooms Demo — agent guide

A small, single-player, **first-person Backrooms** game that **showcases MooseRunner**
(agent-driven PlayMode tests, black-box E2E, visual validation) and ships as a **mobile WebGL
build on GitHub Pages**. Sister project to *Knuckle Drift*.

Read `PLAN.md` for the design, architecture, milestones, and hosting strategy.
Read `docs/DEPLOYMENT.md` before touching the WebGL build (the GitHub Pages compression gotcha).

## Hard constraints (do not break)

- **Mobile WebGL, non-VR.** Touch-first controls; keep the build small; no XR.
- **GitHub Pages deploy** requires **Decompression Fallback ON** (or Compression = Disabled) —
  see `docs/DEPLOYMENT.md`.
- **Deterministic everything** — seeded maze, fixed game-clock sanity, rule-based entity. No
  unseeded `Random`, no wall-clock timing in logic.

## Testing & architecture doctrine (always follow)

@TestingGuidelines.md
@ArchitectureGuidelines.md

Namespace root is `Backrooms`; one asmdef per module (`Backrooms.<Module>`, tests
`Backrooms.<Module>.Tests`). Concrete classes (zero-interface), Facade/Router, module-owned
TestFacade seams, ≤400 lines/file, XML docs on everything incl. tests.

## MooseRunner CLI quickstart

Invoke as `mooserunnerCli` (`Library/PackageCache/com.valectric.mooserunner@*/CLI~/mooserunnerCli.exe`,
or the stable `MooseRunner/mooserunnerCli.exe`).

- `mooserunnerCli ping` — verify daemon + Unity worker are reachable
- `mooserunnerCli status` — workflow state
- `mooserunnerCli test --class <Assembly> <Class>` — run a test class (E2E: use `--class`)
- `mooserunnerCli test --assembly <Assembly>` — run a suite
- `mooserunnerCli test-log` — your test code's `MooseRunnerFacade.Log(...)` output
- `mooserunnerCli console --types error,warning --count 50` — Unity console (check after every run)
- `mooserunnerCli screenshot` / `recording_extract_and_analyze ...` — visual validation
- `mooserunnerCli force-recompile` then retry `ping` every ~15s after adding tests

Exit codes: `0` pass · `1` test failed · `2` timeout · `3` CLI/daemon · `4` no worker.

## Unity

Unity `6000.3.17f1` + URP. MooseRunner (`com.valectric.mooserunner`) and UniTask
(`com.cysharp.unitask`) come from scoped registries — declared in `Packages/manifest.json`,
no local copy.
