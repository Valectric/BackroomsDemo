# Deploying the WebGL build to GitHub Pages

The goal: a URL you open on your phone that shows the latest build. This is the
**"incremental build"** loop — rebuild, push, refresh on the phone.

---

## The one gotcha that breaks everything: compression

GitHub Pages serves static files and **cannot** add a `Content-Encoding: br` (Brotli) or
`gzip` header. Unity's default WebGL build ships Brotli-compressed `.wasm`/`.data`/`.framework.js`
and expects the server to advertise that encoding. On Pages you get:

```
Unable to parse Build/BackroomsDemo.framework.js.br! ...
```

**Fix — pick one (both work on Pages):**

- **Compression Format = Disabled** (Project Settings → Player → WebGL → Publishing Settings).
  Simplest; larger files but fine for a small demo.
- **Compression Format = Gzip + Decompression Fallback = ON.** Smaller download; the
  JS-side fallback decompresses in-browser without needing the server header.

Either avoids the `.br` header dependency. **Decompression Fallback ON is the safe default.**

---

## Player Settings for a small, mobile-friendly WebGL build

Project Settings → Player → WebGL:

- **Publishing:** Compression = Gzip, **Decompression Fallback = ON**, Data Caching = ON.
- **Other Settings:** Color Space = Gamma (smaller/faster on mobile GPUs) or Linear if the
  look needs it; Managed Stripping Level = High; Strip Engine Code = ON.
- **Exception support:** None (or Explicitly Thrown Only) — smaller build.
- **Resolution:** WebGL template = Minimal; enable a responsive canvas so it fills the phone.
- Keep textures tiny and lean on procedural geometry — watch the `.data` size (Pages 100 MB/file
  cap; phones have limited memory).

Set the build **output folder to `docs/`** at the repo root so Pages can serve it from `main`.

---

## Gotchas already hit and fixed (do not re-learn these)

- **`.gitignore` swallows the build.** The standard Unity `.gitignore` has `[Bb]uild/`, which also
  matches `docs/Build/` — the WebGL payload. Without the `!docs/Build/` exception you push an
  `index.html` with no game behind it and Pages serves a broken page.
- **MooseRunner assemblies break player builds.** `MooseRunner.Helpers.Runtime.dll` references
  nunit and ships as a *Runtime* assembly, and `MooseRunner.Internal.dll` references it in turn, so
  the IL2CPP linker dies with `Failed to resolve assembly: 'nunit.framework'`.
  `Assets/Backrooms/Editor/BuildAssemblyFilter.cs` strips the whole MooseRunner family from player
  builds. Delete that filter if the package ever gates those behind `UNITY_INCLUDE_TESTS`.
- **WebGL builds can exhaust Windows commit memory.** Bee spawns roughly one `clang++` per logical
  core (24 on this machine), each committing GBs, and the build dies with
  `The paging file is too small` / `LLVM ERROR: out of memory`. Fix without admin rights: set the
  Unity process's CPU affinity to ~4 logical cores
  (`(Get-Process -Id <pid>).ProcessorAffinity = [IntPtr]0xF`) so the child compilers inherit it;
  restore it afterwards. A bigger page file is the permanent fix.
- **Build the scene and the player headlessly** with the sentinel files
  (`touch .backrooms-build-scene` / `.backrooms-build-webgl`); an editor-update poller picks them
  up within ~30s. Menu equivalents live under **Backrooms/** in the menu bar.

---

## Phase A — manual deploy (start here, no secrets)

1. In Unity: **File → Build Settings → WebGL → Build**, output to `docs/` in the repo root.
2. Commit and push `docs/` to `main`.
3. GitHub repo → **Settings → Pages → Build and deployment → Deploy from a branch →
   `main` / `/docs`**. Save.
4. Wait ~1 min, then open `https://<user>.github.io/BackroomsDemo/` on your phone.

> Pages serves `docs/index.html`. Unity's WebGL build already produces `index.html` +
> `Build/` + `TemplateData/` at the output root, so pointing Pages at `/docs` just works.

Repeat 1–2 to ship a new build. That's the incremental loop.

---

## Phase B — automated CI build on push (optional)

For a true "push and it rebuilds itself" flow, use
[`game-ci/unity-builder`](https://game.ci/docs/github/getting-started):

- Add repo secrets: `UNITY_LICENSE` (from `game-ci/unity-request-activation-file` for a
  Personal license), and `UNITY_EMAIL` / `UNITY_PASSWORD` if using Pro/Plus.
- A workflow builds the WebGL target and deploys the artifact to Pages via
  `actions/deploy-pages`.
- A ready-to-fill stub lives at `.github/workflows/webgl-pages.yml.disabled` — rename to
  `.yml` and add the secrets to enable.

Adopt this **after** the manual loop (Phase A) is proven working end to end.

---

## Mobile sanity checks

- Test on the actual phone browser you'll share (Chrome/Safari differ on WebGL/audio).
- Audio on mobile requires a user gesture to start — begin ambience on the first tap
  (the Start button gesture covers this).
- Add a portrait/landscape hint if controls assume an orientation.
```

## Publishing (from 2026-08-05)

The WebGL payload no longer lives on `main`. It is published to an **orphan `gh-pages` branch that is
force-replaced on every deploy**, so the artifact is always exactly one commit deep and never
accumulates. Committing it to `main` had put 34 builds x ~21MB into history — 634MB of a 650MB
repository.

```
touch .backrooms-build-webgl      # build into docs/ as before
bash Tools/publish-pages.sh       # replace gh-pages with docs/ and force-push
```

`Tools/publish-pages.sh` builds the tree with `hash-object`/`mktree`/`commit-tree`, so it never
touches the working tree or the index and cannot leave the repository on the wrong branch.

**itch.io** is published by `.github/workflows/publish-itch.yml`, which fires on any push to
`gh-pages` — so Pages and itch always serve the same build from the same commit. It needs two
repository settings, both added through the GitHub UI:

- Secret `BUTLER_API_KEY` — from <https://itch.io/user/settings/api-keys>
- Variable `ITCH_TARGET` — e.g. `valectric/backrooms-demo:html5`

**Pages source must be set to `gh-pages` / `/`** for this to serve. Until it is, Pages still reads
`main` `/docs`.

