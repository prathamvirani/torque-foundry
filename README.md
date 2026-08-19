# Torque Foundry

**A realistic 3D vehicle engineering sandbox and learning platform.**

Torque Foundry is a long-term project to let players design, simulate, tune, test, diagnose, and understand complete vehicles from first principles. The main game targets realistic, physically grounded reduced-order simulation rather than research/OEM-grade multiphysics.

## Project status

**Phase:** P1 Engine Lab foundation

The current development target is the Engine Lab: a configurable inline-four with live procedural 3D geometry, validated analytical calculations, slider-crank kinematics, and a clean separation between simulation and presentation.

## Core principles

- Physics is authoritative; the 3D scene is presentation.
- Simulation uses SI units internally.
- Beginner assistance changes guidance, not physics.
- Bad engineering ideas are usually allowed and should fail for understandable physical reasons.
- Major models include assumptions, validity ranges, fidelity levels, and validation cases.
- Research-grade CFD/FEA/combustion/OEM emulation is intentionally outside the core scope for now.

## Current toolchain

- Unity 6.5 Supported release
- C#
- Git + Git LFS
- GitHub

## Unity version policy

Torque Foundry intentionally follows Unity's Supported/Update releases when newer features are useful rather than remaining permanently on an LTS release. Editor upgrades should be deliberate: commit/backup first, verify package compatibility, upgrade the project, then run validation/regression tests before continuing development.

## Agent-assisted development

The repository is prepared for autonomous/agentic development.

Start with:

- `AGENTS.md` — repository-wide operating rules and guardrails
- `Documentation/CODEX_WORKFLOW.md` — autonomous implementation, Unity, validation, and Git workflow
- `Documentation/ACTIVE_MILESTONE.md` — current Engine Lab scope and next steps
- `Documentation/ARCHITECTURE.md` — simulation/presentation architecture
- `Documentation/VIBE_CODING_RULES.md` — engineering-model principles

Agents should work on the current feature branch, validate changes before checkpoint commits, and stop for material design/fidelity decisions or system-level changes. Merging to `main` remains an explicit user decision.

## Tracker

The detailed backlog and engineering roadmap are maintained separately in the project tracker.
