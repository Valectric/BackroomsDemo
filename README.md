# Backrooms Demo

A small, single-player, first-person **Backrooms** game — built and tested end-to-end by an AI
agent using **[MooseRunner](https://mooserunner.com)**, and playable in your phone browser via
**WebGL on GitHub Pages**.

> ▶️ **Play it now:** **https://valectric.github.io/BackroomsDemo/** — works in a phone browser.

## What it demonstrates

This is a MooseRunner showcase. The same tiny game exercises all three MooseRunner disciplines:

- **Agent-driven PlayMode tests** — deterministic white-box tests (maze generation and topology,
  Dweller pathing, player movement, HUD state) an AI agent writes and runs headlessly via the CLI.
- **Black-box E2E flows** — the real shipped scene, driven only by simulated physical input: the
  agent BFS-solves the maze and *walks* it until the game reports the descent.
- **Visual validation** — SessionRecorder + screenshots so the agent can *see* the game renders
  correctly.

## The game

You **noclip** into the Backrooms — an ever-changing dungeon stitched together from twisted
carnivals, abandoned malls, janky laundromats and condemned asylums. Find the exit on each floor to
descend. Every floor down is a different kind of space, and the **Dweller** hunting you is faster
than the last. Let it reach you and the run is over.

- **Controls (mobile):** left half of the screen = virtual stick from wherever you press;
  right half = look. Push the stick far to sprint.
- **Controls (desktop):** WASD to move, hold left mouse to look, Shift to sprint.

## Tech

- Unity `6000.3.17f1` + URP · MooseRunner `2.2.5` · UniTask
- Architecture + testing doctrine: see `ArchitectureGuidelines.md`, `TestingGuidelines.md`, `CLAUDE.md`
- Current state + next steps: see `HANDOVER.md`
- Dated decision log: see `DECISIONS.md`
- Setup from scratch: see `Documentation/SETUP.md`
- Plan & milestones: see `PLAN.md`
- Hosting runbook: see `Documentation/DEPLOYMENT.md`

## Credits

**3D furniture models:** [Furniture Kit](https://kenney.nl/assets/furniture-kit) by
[Kenney](https://kenney.nl), licensed **CC0 1.0 (public domain)**. The licence requires no
attribution; it is given because Kenney's free assets are worth supporting. The pack lives in
`Assets/ThirdParty/Kenney/FurnitureKit/` with its original `License.txt` intact, so this repository
stays clonable and buildable by anyone.

**Ambience:** seven recordings from [Freesound](https://freesound.org) by TRP, Helmer88,
KaleidacousticsAudio, Breviceps, tec_studio and qubodup, each verified **CC0 1.0** before download.
One or two per floor theme — a dishwasher finishing, a street organ, a heavy institutional door —
played rarely and at random, and processed to sound about a hundred metres off.

Everything else is generated procedurally in code with no imported art: the level geometry,
wallpaper / carpet / ceiling-tile textures, skirting, columns, stairwells, relics and the Dwellers.
The rest of the audio is synthesised too — the room hum, the pursuit drone, footsteps, the relic
chime and the descent tone. A waveform computed from a formula has no licence, which is how a repo
restricted to CC0 gets a soundtrack at all.

Full list with links: **[CREDITS.md](CREDITS.md)**.

## Licence

The code is **[MIT](LICENSE)**. Every bundled asset is **CC0 1.0** and stays that way however you
use this — nothing here is licensed on terms a fork would have to renegotiate.

[MooseRunner](https://mooserunner.com) itself is a separate commercial product and is **not**
redistributed here; it resolves as a Unity package from the public npm registry.

Setting inspired by *Discount Dan: A LitRPG Adventure* by James A. Hunter. This is an unofficial
fan demo, not affiliated with or endorsed by the author.

Sister project to *Knuckle Drift*. Built with Claude Code + MooseRunner.
