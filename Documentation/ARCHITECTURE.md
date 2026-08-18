# Torque Foundry Architecture v0.1

## Core rule

The numerical simulation must not depend on the Unity scene.

## Layers

1. **Definitions** — versioned, serializable descriptions of what the player built.
2. **Simulation** — pure numerical state and calculations using SI units internally.
3. **Presentation** — Unity 3D meshes, animation, UI, graphs and sound.
4. **Validation** — analytical tests, reference datasets and regression cases.
5. **Documentation** — equations, assumptions, validity ranges, fidelity and sources.

## Propulsion boundary

All propulsion families should implement a common power-unit contract so the vehicle layer can work with:

- reciprocating ICE
- hybrid systems
- hydrogen fuel-cell systems
- future gas turbines

## Fidelity policy

The core game targets realistic, physically grounded reduced-order models. Research-grade CFD, nonlinear FEA, detailed combustion chemistry, proprietary tyre models and exact OEM firmware emulation are intentionally parked outside the core scope until explicitly revisited.

## Engineering transparency

Every major model should expose:

- what it models
- what it omits
- equations / empirical relationships
- validity range
- calibration/reference source
- fidelity level
