# Setup — fresh machine

Everything needed to go from a clone to a running, testable, deployable project.

## 1. Prerequisites

| | |
|---|---|
| **Unity** | `6000.3.17f1` — the version is pinned in `ProjectSettings/ProjectVersion.txt` |
| **Unity modules** | **WebGL Build Support** (add via Unity Hub → Installs → gear → Add modules). Without it the WebGL build refuses with a clear error. |
| **MooseRunner licence** | Trial activates automatically, but the Inspector Panel must be opened **once** before the CLI will run tests |
| **git + gh** | `gh` authenticated with an account that can push to `Valectric/BackroomsDemo` |
| **Python 3** | Optional — only for serving the build locally |
| **ffmpeg** | Optional — only for SessionRecorder frame extraction |

## 2. Clone and open

```bash
git clone https://github.com/Valectric/BackroomsDemo.git
cd BackroomsDemo
```

Open the folder in Unity `6000.3.17f1`. Packages resolve automatically from the scoped registries
already declared in `Packages/manifest.json`:

- **Valectric** (`registry.npmjs.org`) → `com.valectric.mooserunner` 2.2.5
- **OpenUPM** → `com.cysharp.unitask`

First import takes a while (140 CC0 furniture models plus URP shader compilation).

## 3. Activate MooseRunner (one-time, UI)

Unity → **Tools → MooseRunner → Open MooseRunner**. The trial activates itself; the key persists.
CLI-only workflows still need this one step.

## 4. Verify

```bash
./MooseRunner/mooserunnerCli.exe ping        # expect PONG, sub-second
./MooseRunner/mooserunnerCli.exe test --assembly Backrooms.MazeManager.Tests
```

Expect **26/26**. Full suite is 50 tests across five assemblies — see `HANDOVER.md`.

> `force-recompile` prints `[PASS]` even when C# compilation **failed**. Running a test is the only
> reliable proof that code compiled. See `CLAUDE.md` → Traps.

## 5. Regenerate the scene and build

Headless, via sentinel files consumed by an editor poller within ~30s:

```bash
touch .backrooms-build-scene       # regenerate the gameplay scene from code
touch .backrooms-build-webgl       # build WebGL into docs/  (~5 min)
```

Or use the **Backrooms/** menu in the Unity menu bar.

If a WebGL build dies with `The paging file is too small` or `LLVM ERROR: out of memory`, the
compilers are exhausting Windows commit memory. Without admin rights, throttle Unity so its child
compilers inherit fewer cores, then restore afterwards:

```powershell
(Get-Process -Id <unityPid>).ProcessorAffinity = [IntPtr]0xF   # 4 cores
# ... build ...
(Get-Process -Id <unityPid>).ProcessorAffinity = [IntPtr]0xFFFFFF
```

## 6. Test the build locally before deploying

```bash
cd docs && python -m http.server 8765
# open http://localhost:8765/
```

Seconds per iteration instead of a full push-and-wait cycle.

## 7. Deploy

```bash
git add -A && git commit -m "..." && git push
gh api repos/Valectric/BackroomsDemo/pages/builds/latest --jq '.status'   # 'built' when live
```

GitHub Pages serves `main` `/docs`. It is already configured; nothing to enable.

## 8. Optional — SessionRecorder video

```bash
./MooseRunner/mooserunnerCli.exe recording_set_ffmpeg_path "<abs path to ffmpeg.exe>"
./MooseRunner/mooserunnerCli.exe recording_extract_frame "<sessionPath>" <seconds> --out out.png
```

Recording is currently **disabled in the E2E** — Unity Recorder logs `AudioRender ... called while
system was not recording` errors on stop, and a suite that leaves console errors is not a clean
pass. Re-enable deliberately when reviewing footage; see the comment in `EscapeLevel0E2E`.

## Unity AI MCP — not required

`unity-mcp` is **not** used by this project and is not needed. Headless GameObject creation and
builds are handled by committed editor scripts driven by sentinel files (`DECISIONS.md` D3), which
need no extra services. If you want MCP anyway: Unity → **Edit → Project Settings → AI → Unity MCP
Server**, start it, accept the pending connection, then restart the agent session so the tools load.
