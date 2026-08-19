# Active milestone — Engine Lab foundation

## Status

**Phase:** P1 Engine Lab

**Active branch:** `feature/engine-lab-foundation`

**Verification note:** On 2026-08-20, the dedicated scene passed `Torque Foundry → Validate Engine Lab Scene` in a normal licensed Unity 6000.5.8f1 editor session using D3D12. The scene opened cleanly, the Engine Lab root remained reset, all bore/stroke/rod-length rebuild cases passed, the Console had zero red errors, and captured views confirmed that the Visual Fidelity Pass v1 assembly remains inspectable in Full Engine, Cutaway, Transparent Block / Head, Rotating Assembly Only, and Valvetrain Only modes.

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

The repeatable editor validation opens the dedicated scene, checks its root transform and script references, exercises bore/stroke/rod-length changes to confirm the assembly replaces its generated hierarchy without stale state, verifies the authoritative 86 × 86 × 143 mm reference state and slider-crank positions, and checks the limits and state isolation of the inspection controls.

The same verifier has also been run successfully from its normal editor menu command, with zero red Console errors and a captured inspection-camera view for visual review.

### Unity adapter

`Game/Assets/VES/EngineLab/Runtime/EngineLabController.cs`

This is a thin MonoBehaviour adapter. It may expose inputs/outputs to the Inspector and later UI, but engineering equations must remain in Core.

### Visual Fidelity Pass v1 presentation

`Game/Assets/VES/EngineLab/Presentation/InlineFourVisualFidelityAssembly.cs`

The active game-facing presentation is now a parameter-driven semi-realistic inline-four assembly. It provides:

- a recognizable cast-block silhouette with integrated barrel bays, deck/bore lands, deep crankcase skirt, five main-bearing bulkheads and caps, sump rail, oil pan, end faces, ribs, and bosses
- five main journals, four rod journals, broad webs, counterweights, front snout/damper, rear flange, and flywheel
- pistons with crown, ring lands, skirts, pin bosses, and wrist pins
- forged connecting rods with separate big-end caps, tapered I-beam forms, and small-end eyes
- a solid/cutaway head with chambers, short port regions, valves, springs, two camshafts, lobes, caps, and a valve/cam cover
- material classes for cast iron, cast aluminium, machined steel, piston aluminium, bearing surfaces, seals, and dark internal components

Authoritative bore, stroke, rod length, cylinder spacing, crank phasing, and slider-crank positions continue to originate in the controller/Core path. Additional casting, journal, piston, rod, valve, and cam proportions are presentation assumptions documented in `Documentation/ENGINE_LAB_VISUAL_FIDELITY_V1.md`; they never feed back into simulation state.

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
- a 0–360° crank-angle scrubber
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

2. **Improve the procedural mechanical model — Visual Fidelity Pass v1 completed**
   - completed: main/rod journals, broad crank webs, counterweights, snout/damper, flange, and flywheel
   - completed: forged-rod I-beam silhouettes with separate caps, and pistons with crown/ring-land/skirt/pin-boss forms
   - keep crank/piston kinematics authoritative to the core geometry
   - avoid turning presentation dimensions into hidden simulation inputs

3. **Add engine-block/cylinder-head context — Visual Fidelity Pass v1 completed**
   - wall-envelope prototype superseded by a stepped cast-block/crankcase silhouette and solid/cutaway head casting
   - four integrated bore openings and visible cutaway liners
   - five main-bearing bulkheads/saddles and caps, sump rail, oil pan, deck lands, end faces, ribs, and bosses
   - chambers, short intake/exhaust port regions, valves/springs, two cams/lobes/caps, and a valve/cam cover
   - separate generated groups retain crank/rod/piston and valvetrain inspection access

4. **Add useful inspection controls — completed**
   - bounded orbit/zoom/pan camera with focus and reset commands
   - crank-angle scrubber and play/pause teaching animation
   - adjustable teaching RPM kept separate from simulated operating RPM
   - five purpose-built inspection modes with mode-aware focus/framing
   - completed: normal-editor scene validation and targeted Play Mode check with zero Console errors

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
