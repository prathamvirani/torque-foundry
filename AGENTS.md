# Torque Foundry agent instructions

This file applies to the whole repository. Keep it short enough to load quickly; use the linked documents as the deeper source of truth.

## Read first

Before making non-trivial changes, read:

1. `Documentation/ARCHITECTURE.md`
2. `Documentation/VIBE_CODING_RULES.md`
3. `Documentation/CODEX_WORKFLOW.md`
4. `Documentation/ACTIVE_MILESTONE.md`

If those files disagree with an ad-hoc implementation idea, follow the documented architecture unless the user explicitly changes the design.

## Project map

- Unity project root: `Game/`
- Current editor target: Unity `6000.5.8f1` (Unity 6.5 Supported)
- Render pipeline: URP
- Core engineering code: `Game/Assets/VES/Core/`
- Unity-facing Engine Lab runtime: `Game/Assets/VES/EngineLab/Runtime/`
- Unity presentation/visualization: `Game/Assets/VES/EngineLab/Presentation/`
- Validation code: `Game/Assets/VES/Core/Validation/`
- Project documentation: `Documentation/`

The legacy namespace `VehicleEngineeringSandbox` is still present. Do not perform a mass namespace rename unless the task explicitly calls for it.

## Non-negotiable engineering rules

- Numerical simulation is authoritative; Unity scene objects and visuals are presentation.
- Core numerical code must not depend on `UnityEngine`.
- Use SI units internally. UI/display units are presentation only.
- Prefer physically grounded reduced-order models over magic performance multipliers.
- Difficulty changes guidance and assistance, not the laws of physics.
- Allow physically possible bad designs when reasonable; let consequences emerge from the model.
- Every major model needs assumptions, validity limits, fidelity notes, and at least one validation/reference case.
- Keep research-grade CFD, nonlinear FEA, detailed combustion chemistry, proprietary tyre models, exact OEM firmware/CAN emulation, and similar specialist work out of the core scope until the user explicitly reopens it.
- Do not introduce fake precision. If the model cannot support a number, expose the assumption or uncertainty rather than inventing detail.

## Current development priority

Work on the active Engine Lab milestone before expanding into whole-vehicle systems. The immediate goal is a configurable, validated reciprocating-engine lab with live 3D visualization.

Do not jump ahead into chassis, suspension, full drivetrain, hybrid, fuel-cell, economy, career, or market systems unless the active milestone document says to do so or the user explicitly redirects the work.

## Autonomous work loop

For routine implementation work, proceed without asking for permission for every file edit.

1. Inspect `git status` and the current branch.
2. Read the relevant code and documentation before editing.
3. Make the smallest coherent change that advances the active milestone.
4. Keep pure calculations in `Core`; keep Unity adapters thin.
5. Run compile/validation checks.
6. Inspect the diff for accidental generated files or unrelated edits.
7. Commit verified checkpoints to the current feature branch when operating autonomously.
8. Push the current feature branch after a verified checkpoint if credentials/permissions allow.

Never merge to `main`, force-push, rewrite published history, delete branches, or perform destructive Git operations unless the user explicitly asks.

## When to continue vs. when to ask

Continue autonomously for:

- normal C# edits and refactors within the documented architecture
- Unity scene/component setup needed by the active milestone
- generated `.meta` files that Unity legitimately creates for tracked assets
- compile-error fixes caused by the current task
- validation/test additions
- presentation polish that does not change an engineering assumption
- routine Git commits on the current feature branch

Stop and ask the user when:

- a product/design choice materially changes gameplay direction
- two plausible engineering abstractions have substantially different fidelity/performance tradeoffs
- a dependency/package install or Unity package upgrade is needed
- a Unity editor upgrade is proposed
- a save/schema-breaking change is needed
- the task would enter the parked research-grade/specialist scope
- credentials, paid services, licenses, or external accounts are required
- a destructive operation is needed
- a system-level Windows, driver, BIOS, registry, security, or administrator change would be required

## Unity and Windows guardrails

- Use the checked-in Unity version from `Game/ProjectSettings/ProjectVersion.txt`.
- Do not launch multiple Unity editor instances against the same project.
- Prefer normal D3D12 operation. Do not add `-force-d3d11`, `-force-vulkan`, or other graphics overrides unless diagnosing a specific issue.
- If the editor or display experiences a GPU reset/TDR, stop and report it. Do not change TDR registry values, NVIDIA drivers, BIOS settings, or Windows builds autonomously.
- Do not modify files outside this repository unless the user explicitly asks.
- Do not install software or run elevated/admin commands autonomously.

## Unity source-control hygiene

Track:

- `Game/Assets/**` including `.meta`
- `Game/Packages/manifest.json`
- `Game/Packages/packages-lock.json`
- `Game/ProjectSettings/**`
- useful project tooling/config such as `Game/.vsconfig`

Do not add generated local state such as:

- `Game/Library/`
- `Game/Temp/`
- `Game/Logs/`
- `Game/UserSettings/`
- IDE-generated `.csproj` / solution files

Respect `.gitignore`, `.gitattributes`, and Git LFS rules.

## Code conventions

- Prefer small deterministic classes/structs with explicit units in names where ambiguity is possible.
- Validate public engineering inputs at boundaries.
- Avoid hidden mutable state in calculation helpers.
- Avoid duplicated equations in UI or presentation code.
- Prefer descriptive names over abbreviations unless the abbreviation is a standard engineering quantity.
- Use comments to explain assumptions and model intent, not obvious syntax.
- Keep visualization geometry disposable/rebuildable from authoritative configuration.

## Validation expectations

A change that affects engineering behavior is not complete until there is evidence that the result is plausible and deterministic. Add or update analytical/reference cases in the validation layer when appropriate.

Known foundation references include:

- 86 mm bore × 86 mm stroke × 4 cylinders = approximately 1.9982288569 L
- 86 mm stroke at 7000 rpm = approximately 20.0666666667 m/s mean piston speed

Do not change reference expectations merely to make a failing implementation pass; first determine whether the implementation or the reference is wrong.

## Completion report

At the end of an autonomous task, report concisely:

- what changed
- what was tested/validated
- any warnings, limitations, or decisions still needed
- the commit/branch state

If something could not be verified, say so explicitly rather than claiming success.
