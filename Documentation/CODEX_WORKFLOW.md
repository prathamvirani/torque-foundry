# Codex autonomous workflow

This document defines the preferred working contract for an autonomous coding agent operating on Torque Foundry.

## Goal

Reduce routine human intervention while preserving engineering quality, repository safety, and clear decision boundaries.

The agent should be able to perform ordinary implementation, Unity setup, compile/test cycles, validation work, and feature-branch commits without asking the user to approve every small step. It should stop for decisions that materially change scope, fidelity, architecture, dependencies, or system configuration.

## Environment

Repository root:

`F:\Github\torque-foundry`

Unity project root:

`F:\Github\torque-foundry\Game`

Target editor:

Unity 6.5 Supported, exact project version recorded in `Game/ProjectSettings/ProjectVersion.txt`.

The repository uses Git LFS and an LF-normalized text policy through `.gitattributes`.

## Session startup checklist

At the start of a work session:

1. Read root `AGENTS.md`.
2. Read `Documentation/ARCHITECTURE.md`, `Documentation/VIBE_CODING_RULES.md`, and `Documentation/ACTIVE_MILESTONE.md`.
3. Run `git status` and identify the current branch.
4. Do not discard or overwrite pre-existing local changes.
5. If remote changes are expected, fetch/pull only when doing so is safe for the current working tree.
6. Read the relevant implementation before writing new code.

If the current branch is `main`, do not begin a non-trivial feature directly on it unless the user explicitly asked for that. Prefer an existing feature branch or create a clearly named feature branch when authorized by the task.

## Implementation loop

Use this loop repeatedly:

### 1. Define the next smallest verifiable increment

Examples:

- add one engine geometry quantity
- add one validation case
- add one procedural visual subsystem
- expose one configuration value through a thin Unity adapter
- make one scene setup change

Avoid broad speculative rewrites.

### 2. Preserve layer boundaries

Core engineering equations and deterministic simulation belong under `Game/Assets/VES/Core/` and must remain free of Unity dependencies.

Unity adapters, scene behavior, inspector-facing components, camera controls, meshes, animation, UI, graphs, and sound belong in presentation/runtime layers under `Game/Assets/VES/EngineLab/` or later feature-specific presentation directories.

Do not duplicate an engineering formula inside MonoBehaviours merely for convenience. Call the core model.

### 3. Build for inspectability

Engineering state should be understandable from code and, where useful, from the Unity inspector/debug UI.

Prefer:

- explicit units
- named intermediate quantities
- deterministic outputs
- visible assumptions
- simple validation cases

Avoid opaque tuning constants with no physical interpretation.

### 4. Compile and validate

When Unity is available interactively, prefer using the real project in the editor and confirm the Console is free of red compile errors before declaring a Unity-facing change complete.

If computer-use capabilities are available, the agent may operate Unity for routine work such as:

- opening the project
- waiting for import/compile
- creating or renaming scenes
- adding documented components
- saving scenes/assets
- entering Play Mode for a targeted check
- inspecting the Console and Inspector
- taking screenshots for evidence

Do not use UI automation to make undocumented architectural decisions.

For pure C# engineering changes, run available automated validation/tests in addition to Unity compilation. If a suitable automated test harness does not yet exist, improve the harness as part of the milestone rather than relying indefinitely on manual inspection.

### 5. Inspect repository state

Before committing:

- run `git status`
- inspect the staged/unstaged diff
- ensure no `Library`, `Temp`, `Logs`, `UserSettings`, IDE-generated project files, crash dumps, tokens, or unrelated files are included
- include Unity `.meta` files for tracked assets
- retain Git LFS treatment for large binary assets

### 6. Commit verified checkpoints

Use short, descriptive imperative commit messages, for example:

- `Add slider-crank validation cases`
- `Add procedural inline-four crank geometry`
- `Create Engine Lab scene foundation`

A checkpoint should be coherent and preferably compile cleanly. Do not bundle unrelated cleanup with feature work unless needed for the feature.

When operating autonomously on an approved feature branch, the agent may push verified commits to that same branch. Never merge to `main` or rewrite remote history without explicit user direction.

## Unity operating rules

### Editor launch

Use the version checked into `ProjectVersion.txt`. On the known Windows development machine, the Hub-installed editor is normally under:

`C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe`

Detect the checked-in version rather than assuming a different editor.

Do not open the same project in two Unity instances simultaneously.

### Graphics

The expected normal path is D3D12. Do not proactively force D3D11 or Vulkan.

If Unity reports an unrecoverable graphics-device error, Windows resets the display driver, or a TDR occurs:

1. stop the automated Unity loop
2. preserve logs
3. report the exact error and what action preceded it
4. wait for user direction

Do not autonomously change NVIDIA drivers, Windows builds, BIOS graphics modes, registry TDR settings, overclock/undervolt settings, or other system-level graphics configuration.

### Scene and asset editing

Prefer meaningful project scenes over permanently developing inside template `SampleScene`.

Unity-generated `.meta` files are first-class source-control files and should be committed with their assets.

Procedural visual children should be safe to destroy/rebuild from authoritative configuration. Presentation transforms should not become hidden simulation state.

## Engineering validation policy

For each major model, capture:

- equation or relationship used
- input/output units
- assumptions
- intended validity range
- fidelity level
- at least one analytical/reference case

Validation tolerances should reflect numerical behavior and model accuracy. Do not use excessively loose tolerances simply to avoid investigating a mismatch.

When introducing an empirical model later, preserve the source/calibration reference in documentation or data metadata.

## Human-attention triggers

The agent should stop and request user input for any of the following:

- choosing between materially different gameplay directions
- deciding a fidelity/performance tradeoff that changes the product experience
- adding/upgrading third-party Unity packages or external dependencies
- changing Unity editor version
- moving into research-grade/specialist features parked by project policy
- breaking save/data schema compatibility
- deleting significant assets or history
- merging to `main`
- force-pushing or rebasing published shared history
- installing software or using administrator privileges
- modifying Windows, drivers, BIOS, registry, firewall, security tools, or credentials
- spending money or accepting a license/service agreement

Routine compile fixes, normal file edits, Unity component setup, validation additions, and presentation refinements do not require a human interruption when they fit the active milestone.

## Failure handling

If an implementation fails:

1. gather the concrete error, stack trace, log, or failing validation
2. identify the smallest plausible cause
3. fix and re-run the check
4. avoid random package/system changes
5. if two focused attempts do not materially improve the situation, report the evidence and ask for help rather than thrashing

Never hide a failing validation, disable a check, or change a reference value without understanding why.

## End-of-task report

A good autonomous completion note includes:

- summary of implemented behavior
- files/subsystems changed
- compile/test/validation evidence
- screenshots or observable behavior when useful
- commit SHA(s) and branch
- any remaining risks, TODOs, or user decisions

If Unity could not be launched or a check could not be run, state that explicitly.
