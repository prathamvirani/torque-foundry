# Active milestone — Engine Lab foundation

## Status

**Phase:** P1 Engine Lab

**Active branch:** `feature/engine-lab-foundation`

The current milestone is to turn the initial validated engine geometry into a usable Engine Lab: a configurable reciprocating engine with authoritative pure-C# calculations and a live Unity 3D teaching/inspection presentation.

Do not expand into whole-vehicle systems yet.

## Working foundation

The branch currently contains:

### Core ICE geometry

`Game/Assets/VES/Core/ICE/EngineGeometry.cs`

Provides validated calculations for:

- total displacement
- mean piston speed
- compression ratio
- clearance volume from compression ratio

### Engine configuration/state pipeline

- `EngineConfiguration.cs`
- `EngineCalculator.cs`
- `EngineCalculatedState.cs`

This pipeline converts user-selected geometry/operating inputs into deterministic derived engineering state without Unity dependencies.

Current baseline inputs:

- bore: 86 mm
- stroke: 86 mm
- connecting rod length: 143 mm
- cylinders: 4
- compression ratio: 10:1
- engine speed: 7000 rpm

Expected baseline outputs include approximately:

- displacement: 1.9982288569 L
- mean piston speed: 20.0666666667 m/s
- bore/stroke ratio: 1.0
- rod/stroke ratio: 1.6627906977
- clearance volume per cylinder: 55.506 cm³

### Slider-crank model

`Game/Assets/VES/Core/ICE/SliderCrankKinematics.cs`

The presentation should derive piston and rod motion from the actual slider-crank geometry rather than using arbitrary sinusoidal animation.

### Validation

`Game/Assets/VES/Core/Validation/ValidationRunner.cs`

Foundation reference cases cover displacement, mean piston speed, compression-ratio round trips, configuration-to-state calculations, and slider-crank behavior.

### Unity adapter

`Game/Assets/VES/EngineLab/Runtime/EngineLabController.cs`

This is a thin MonoBehaviour adapter. It may expose inputs/outputs to the Inspector and later UI, but engineering equations must remain in Core.

### Procedural I4 presentation

`Game/Assets/VES/EngineLab/Presentation/InlineFourVisualizer.cs`

The first procedural inline-four mechanism is working in Unity with:

- four pistons
- wrist pins
- connecting rods
- crank pins/throws
- crankshaft presentation
- bore/cylinder guides
- conventional I4 phasing: cylinders 1/4 together and 2/3 180° opposite
- editor preview crank angle
- separate slow teaching-animation RPM

The visualizer is intentionally an engineering skeleton, not final art.

## Immediate next steps

Work in this order unless the user redirects:

1. **Repository/scene hygiene**
   - ensure Unity-generated `.meta` files for all new tracked scripts/folders are committed
   - create/save a dedicated Engine Lab scene under `Game/Assets/VES/EngineLab/Scenes/EngineLab.unity`
   - stop using template `SampleScene` as the long-term development scene
   - ensure the Engine Lab root transform is `(0,0,0)` / zero rotation / unit scale

2. **Improve the procedural mechanical model**
   - make crankshaft geometry mechanically legible: main journals, crank pins, webs/counterweights
   - improve connecting-rod and piston proportions while keeping geometry parameter-driven
   - keep crank/piston kinematics authoritative to the core geometry
   - avoid turning presentation dimensions into hidden simulation inputs

3. **Add engine-block/cylinder-head context**
   - simple configurable block envelope
   - visible cylinders/liners or cutaway bores
   - deck plane and head envelope
   - keep cutaway/isolation-friendly hierarchy

4. **Add useful inspection controls**
   - orbit/zoom/focus camera suitable for an engineering lab
   - crank-angle scrubber
   - play/pause teaching animation
   - isolate/fade mechanical groups where practical

5. **Create the first proper Engine Lab UI**
   - bore
   - stroke
   - rod length
   - cylinder count/layout restrictions appropriate to this I4 prototype
   - compression ratio
   - RPM operating point
   - live calculated displacement, piston speed, ratios, and warnings

6. **Strengthen automated validation**
   - add a Unity Test Framework or similarly repeatable test path for core calculations
   - cover slider-crank endpoints and symmetry
   - verify changes to geometry inputs rebuild presentation without stale state

7. **Prepare basic dyno milestone**
   - only after geometry/scene/UI foundation is stable
   - do not jump to fabricated horsepower curves; define a documented reduced-order torque/airflow/combustion approach first

## Definition of done for Engine Lab foundation

This milestone is ready to merge when:

- the dedicated Engine Lab scene opens cleanly
- Unity Console has no red compile errors
- geometry inputs update authoritative calculated state
- the I4 presentation rebuilds reliably from configuration
- slider-crank motion is mechanically consistent
- reference/automated validation passes
- generated Unity assets have correct `.meta` files in Git
- no `Library`, `Temp`, `Logs`, `UserSettings`, crash dumps, or IDE-generated project files are tracked
- documentation reflects any new assumptions or model limits
- the feature branch is clean and reviewable

## Explicitly parked

Do not work on these during this milestone unless the user explicitly changes scope:

- research-grade CFD
- nonlinear FEA
- detailed 3D combustion chemistry
- proprietary/black-box tyre emulation
- exact OEM ECU/TCU firmware or CAN reproduction
- homologation-grade certification simulation
- detailed PEM fuel-cell multiphysics
- full NVH/multibody specialist simulation
- full vehicle chassis/suspension/drivetrain implementation
- hybrid/fuel-cell vehicle implementation
- career/economy/market systems

The project will eventually expand into many of these adjacent systems at reduced-order fidelity, but they are not the current task.
