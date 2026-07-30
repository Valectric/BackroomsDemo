# M0 — create the Unity project in this folder

The repo scaffolding (plan, docs, doctrine, `.gitignore`, CI stub) is already here. The one
step that needs the Unity editor is **creating the actual Unity project**. Do this once, then
the agent takes over.

## 1. Create the project (Unity Hub)

- Unity Hub → **New project** → **Universal 3D** template → Editor version **6000.3.17f1**.
- Set the **location** to `C:\Users\JohanHoltby\Documents\GitHub` and the **name** to
  `BackroomsDemo` so it lands in this exact folder.
- If Hub warns the folder is not empty (because of the docs here), that's fine — accept; it
  only adds `Assets/`, `Packages/`, `ProjectSettings/` alongside the existing files.

> Using the **Universal 3D** template guarantees a valid URP setup for this Unity version —
> much safer than hand-authoring render-pipeline assets.

## 2. Add MooseRunner + UniTask to the manifest

Open `Packages/manifest.json` and merge in the entries from
`Documentation/manifest.additions.json` (scoped registries + the two dependencies). Unity will resolve
them on focus.

## 3. Activate MooseRunner once (UI step)

Unity → **Tools → MooseRunner → Open MooseRunner** to enter/confirm the license (trial
auto-activates). The CLI won't run tests until this one-time UI step is done.

## 4. Verify

```
mooserunnerCli ping      # -> PONG (sub-second)
mooserunnerCli status
```

## 5. Hand back to the agent

Tell the agent M0 is ready. It will scaffold the module folders, first scene, an empty WebGL
build to prove the Pages pipeline, and start M1.
