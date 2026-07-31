# Backrooms Demo

A small, single-player, first-person **Backrooms** game — built and tested end-to-end by an AI
agent using **[MooseRunner](https://mooserunner.com)**, and playable in your phone browser via
**WebGL on GitHub Pages**.

> ▶️ **Play it now:** **https://valectric.github.io/BackroomsDemo/** — works in a phone browser.

## What it demonstrates

This is a MooseRunner showcase. The same tiny game exercises all three MooseRunner disciplines:

- **Agent-driven PlayMode tests** — deterministic white-box tests (maze generation, sanity,
  entity AI) an AI agent writes and runs headlessly via the MooseRunner CLI.
- **Black-box E2E flows** — the real shipped scene, driven only by simulated physical input.
- **Visual validation** — SessionRecorder + screenshots so the agent can *see* the game renders
  correctly.

## The game

You noclip into Level 0 of the Backrooms. Find the exit before your sanity runs out, while an
entity roams the endless yellow rooms. Almond water restores sanity.

- **Controls (mobile):** left stick = move, right drag = look, buttons = sprint / interact.
- **Controls (desktop):** WASD + mouse.

## Tech

- Unity `6000.3.17f1` + URP · MooseRunner `2.2.5` · UniTask
- Architecture + testing doctrine: see `ArchitectureGuidelines.md`, `TestingGuidelines.md`, `CLAUDE.md`
- Plan & milestones: see `PLAN.md`
- Hosting runbook: see `Documentation/DEPLOYMENT.md`

## Credits

Sister project to *Knuckle Drift*. Built with Claude Code + MooseRunner.
