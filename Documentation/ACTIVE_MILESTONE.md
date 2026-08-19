# Active milestone — Engine Lab foundation

## Status

**Phase:** P1 Engine Lab

**Active branch:** `feature/engine-lab-foundation`

**Verification note:** The dedicated scene has a repeatable editor verifier, but the latest unattended Unity batch attempt was blocked before project execution by the local Unity Licensing Client repeatedly losing its channel. Source compilation and pure-C# validation can still be run independently; the scene verifier must be rerun once the editor has a working licensed session.

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

`Game/Assets/VES/EngineLab/Editor/EngineLabSceneValidation.cs`

The repeatable editor validation opens the dedicated scene, checks its root transform and script references, and exercises bore/stroke/rod-length changes to confirm the mechanism and context replace their generated hierarchies without stale state.

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
- five main journals
- four crank pins with paired webs and counterweights
- bore/cylinder guides
- conventional I4 phasing: cylinders 1/4 together and 2/3 180° opposite
- editor preview crank angle
- separate slow teaching-animation RPM

The visualizer is intentionally an engineering skeleton, not final art.

### Dedicated Engine Lab scene

`Game/Assets/VES/EngineLab/Scenes/EngineLab.unity`

The dedicated scene contains the controller, I4 mechanism, and Engine Lab presentation context on a root transform at zero position/rotation and unit scale. The template `SampleScene` is no longer used for Engine Lab development.

### Engine block and head context

`Game/Assets/VES/EngineLab/Presentation/InlineFourEngineContextVisualizer.cs`

The presentation now provides independently inspectable generated groups for:

- a camera-facing cutaway block envelope
- four parameter-driven cutaway cylinder liners
- a deck-plane frame around the bores
- a camera-facing cutaway cylinder-head envelope

This context is a teaching and spatial-reference aid, not a structural, thermal, casting, combustion-chamber, or mass model. Bore, stroke, rod length, and shared cylinder spacing drive its rebuild; block depth, head height, liner wall, and cutaway proportions remain explicitly presentation-only. The current validity limit is the inline-four prototype.

## Immediate next steps

Work in this order unless the user redirects:

1. **Repository/scene hygiene — completed**
   - required Unity `.meta` files for tracked scripts/folders are committed
   - the dedicated Engine Lab scene is stored under `Game/Assets/VES/EngineLab/Scenes/EngineLab.unity`
   - template `SampleScene` has been restored and is no longer the Engine Lab development scene
   - the Engine Lab root is serialized at `(0,0,0)` / zero rotation / unit scale

2. **Improve the procedural mechanical model — in progress**
   - completed: mechanically legible main journals, crank pins, paired webs, and counterweights
   - remaining: improve connecting-rod and piston proportions while keeping geometry parameter-driven
   - keep crank/piston kinematics authoritative to the core geometry
   - avoid turning presentation dimensions into hidden simulation inputs

3. **Add engine-block/cylinder-head context — completed**
   - configurable presentation-only block envelope
   - four visible cutaway cylinder liners
   - deck-plane frame and cutaway head envelope
   - separate generated groups retain crank/rod/piston inspection access

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

6. **Strengthen automated validation — in progress**
   - add a Unity Test Framework or similarly repeatable test path for core calculations
   - cover slider-crank endpoints and symmetry
   - completed: editor validation for scene integrity and geometry-input rebuilds without stale presentation state

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
