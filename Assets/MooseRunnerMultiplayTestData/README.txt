Multiplaytest session state
===========================

This folder holds the runtime config (multiplaytest.json) used to
coordinate ParrelSync clones during multiplayer tests.

It MUST live under Assets/ because ParrelSync clones share the master
project's Assets/ via symlinks. That symlink is what lets every clone
read the same config file (resolved via Application.dataPath).

Do not move this folder outside Assets/ (e.g. to ProjectSettings/ or
Library/) — clones would no longer see the master's config and the
Multiplaytest setup handshake would break.

The folder and its contents are gitignored; Unity regenerates them on
the first multiplayer test run.
