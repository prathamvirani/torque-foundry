# Active milestone — Engine Lab foundation

## Status

**Phase:** P1 Engine Lab

**Active branch:** `feature/engine-lab-foundation`

**Verification note:** On 2026-08-20, the dedicated scene passed its Visual Fidelity Pass v2 validator in a normal licensed Unity 6000.5.8f1 editor session using D3D12. The scene opened cleanly, the Engine Lab root remained reset, all bore/stroke/rod-length rebuild cases passed without stale generated meshes, the Console had zero red errors, and five captured inspection views verified the lofted casting, traceable paired ports, functional valvetrain, timing chain, and unchanged rotating assembly.

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

### Valve timing and four-stroke teaching model

`Game/Assets/VES/Core/ICE/ValveTimingKinematics.cs`

Provides a deterministic Unity-independent 720-degree teaching cycle with:

- fixed `1-3-4-2` firing TDC references
- intake, compression, power, and exhaust phase identification
- exact half-speed camshaft rotation
- documented fixed intake/exhaust opening, peak, and closing angles
- a smooth normalized lift profile used by the presentation valves and springs

This is kinematics only; cam contact, lash, VTC/VTEC control, spring force, surge, deformation, and other valvetrain dynamics remain out of scope.

### Validation

`Game/Assets/VES/Core/Validation/ValidationRunner.cs`

Foundation reference cases now cover displacement, mean piston speed, compression-ratio round trips, configuration-to-state calculations, slider-crank behavior, the 2:1 crank/cam relationship, valve closed/opening/peak/closing lift, and 720-degree cylinder-phase sequencing.

`Game/Assets/VES/EngineLab/Editor/EngineLabSceneValidation.cs`

The repeatable editor validation opens the dedicated scene, checks its root transform and script references, exercises bore/stroke/rod-length changes to confirm the assembly replaces its generated hierarchy without stale state, verifies the authoritative 86 × 86 × 143 mm reference state and slider-crank positions, and checks the limits and state isolation of the inspection controls.

The same verifier has also been run successfully from its normal editor menu command, with zero red Console errors and a captured inspection-camera view for visual review.

### Unity adapter

`Game/Assets/VES/EngineLab/Runtime/EngineLabController.cs`

This is a thin MonoBehaviour adapter. It may expose inputs/outputs to the Inspector and later UI, but engineering equations must remain in Core.

### Visual Fidelity Pass v2 presentation

`Game/Assets/VES/EngineLab/Presentation/InlineFourVisualFidelityAssembly.cs`

The active game-facing presentation now supersedes the v1 compression-box aesthetic with:

- rounded arbitrary-profile lofts for the aluminium block, curved crankcase/lower ladder, sump, head, cam cover, timing case, and rear flange region
- four integrated cylinder-bank forms, five lower main-bay forms, main bulkheads/caps, cast ribs, core/accessory bosses, deck/bore lands, and port-side head bulges
- eight curved intake runners/openings and eight curved exhaust runners/openings with valve bowls and optional presentation-only airflow paths
- sixteen moving valves, visually compressing springs, phased cam lobes, and subtle four-stroke chamber highlights
- a 24/48-tooth chain-drive definition with crank/cam sprockets, phaser forms, moving chain markers, guides, tensioner, and timing cover
- refined high-clearance pistons, forged rods/caps, curved crank webs/counterweights, journal collars, snout/damper, flange, and flywheel

Authoritative bore, stroke, rod length, cylinder spacing, crank phasing, slider-crank positions, cycle phase, and valve-lift kinematics originate in the controller/Core path. Visual proportions and validity limits are documented in `Documentation/ENGINE_LAB_VISUAL_FIDELITY_V2.md`; they never feed back into simulation state. The v1 document remains as history for the superseded prototype.

### Prototype/reference presentation

`Game/Assets/VES/EngineLab/Presentation/InlineFourVisualizer.cs`

`Game/Assets/VES/EngineLab/Presentation/InlineFourEngineContextVisualizer.cs`

The original mechanism skeleton and wall-envelope context remain serialized but disabled as prototype/reference implementations. They established:

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

They are no longer the active game presentation and should not constrain later fidelity work.

### Dedicated Engine Lab scene

`Game/Assets/VES/EngineLab/Scenes/EngineLab.unity`

The dedicated scene contains the controller and Visual Fidelity Pass v1 assembly on a root transform at zero position/rotation and unit scale. The template `SampleScene` remains untouched and is not used for Engine Lab development.

### Inspection camera and teaching controls

- `Game/Assets/VES/EngineLab/Presentation/EngineLabInspectionCamera.cs`
- `Game/Assets/VES/EngineLab/Presentation/EngineLabInspectionPanel.cs`

The Play Mode inspection layer now provides:

- bounded left-mouse orbit, middle-mouse pan, and wheel zoom
- focus-engine and reset-view commands (`F` and `Home` shortcuts)
- play/pause teaching motion without changing the simulated operating point
- a 0–720° four-stroke crank-cycle scrubber
- a separate 0–300 rpm teaching-animation speed
- five coherent inspection presets: Full Engine, Cutaway, Transparent Block / Head, Rotating Assembly Only, and Valvetrain Only

These controls own presentation state only. The panel displays the authoritative simulated operating RPM read-only to make the separation explicit. Generated geometry is grouped by inspection category and remains disposable/rebuildable from the controller configuration. Cutaway retains sectioned casting, deck lands, end faces, and crankcase structure rather than representing inspection by deleting an entire camera-facing wall.

## Immediate next steps

Work in this order unless the user redirects:

1. **Repository/scene hygiene — completed**
   - required Unity `.meta` files for tracked scripts/folders are committed
   - the dedicated Engine Lab scene is stored under `Game/Assets/VES/EngineLab/Scenes/EngineLab.unity`
   - template `SampleScene` has been restored and is no longer the Engine Lab development scene
   - the Engine Lab root is serialized at `(0,0,0)` / zero rotation / unit scale

2. **Improve the procedural mechanical model — Visual Fidelity Pass v2 completed**
   - completed: main/rod journals, broad crank webs, counterweights, snout/damper, flange, and flywheel
   - completed: forged-rod I-beam silhouettes with separate caps, and pistons with crown/ring-land/skirt/pin-boss forms
   - keep crank/piston kinematics authoritative to the core geometry
   - avoid turning presentation dimensions into hidden simulation inputs

3. **Add automotive casting, functional ports, valvetrain, and timing context — Visual Fidelity Pass v2 completed**
   - continuous rounded/tapered lofts replace the primary cube-derived block/head silhouettes
   - curved lower ladder, sump, timing case, rear flange, port bulges, cam carrier/cover, ribs, and bosses
   - traceable paired intake/chamber/exhaust paths with optional non-simulated airflow overlays
   - deterministic moving valves/springs, half-speed cams, 720-degree firing phases, and chain timing drive
   - separate generated groups retain crank/rod/piston, casting, port, timing, and valvetrain inspection access

4. **Add useful inspection controls — completed**
   - bounded orbit/zoom/pan camera with focus and reset commands
   - crank-angle scrubber and play/pause teaching animation
   - adjustable teaching RPM kept separate from simulated operating RPM
   - five purpose-built inspection modes with mode-aware focus/framing
   - completed: normal-editor scene validation and targeted Play Mode check with zero Console errors

5. **Create the first proper Engine Lab UI — explicitly deferred during Visual Fidelity Pass v2**
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
   - completed: editor validation for scene integrity, geometry-input rebuilds, camera limits, teaching-state isolation, and group visibility without stale presentation state

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
