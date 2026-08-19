# Engine Lab Visual Fidelity Pass v2

## Purpose and originality

Visual Fidelity Pass v2 turns the inline-four teaching assembly into an original compact performance DOHC long block with continuous cast forms, traceable ports, a functional reduced-order valvetrain, and a coherent chain drive.

Public Honda K-series and modern L-series material was used only as an architectural reference archetype for compact aluminium inline-four proportions, DOHC packaging, paired valves and ports, timing-chain placement, and lower-block construction. Torque Foundry does not reproduce an OEM casting, part number, valvetrain switching mechanism, trademark, logo, or branded cover design.

The main public reference points were:

- Honda's 2000 introduction of its compact lightweight 2.0-litre DOHC i-VTEC engine: <https://global.honda/en/newsroom/worldnews/2000/4001026.html>
- Honda's current e:HEV engine overview, including chain-case treatment, cam/crank/chain-guide friction work, compact DOHC packaging, and high-flow/tumble intake ports: <https://global.honda/en/tech/two_motor_hybrid_system_honda_eHEV_lineup/>
- Honda's Earth Dreams overview for compact DOHC/VTC architecture and shortened bore-pitch/lightweight block development: <https://global.honda/en/newsroom/news/2011/4111130aeng.html>
- Honda's historical four-valve DOHC/VTEC explanation for paired intake/exhaust valve architecture: <https://global.honda/en/heritage/episodes/1989vtecengine.html>

## Authority boundary

Core remains authoritative for:

- bore, stroke, rod length, cylinder count, and calculated engine state
- the established `1.15 × bore` inline-four cylinder spacing
- crank-pin and piston-pin positions from `SliderCrankKinematics`
- the deterministic 720-degree cycle, valve lift profile, cam speed ratio, and cylinder phase from `ValveTimingKinematics`

Unity presentation reads these values. Casting, port, valve, spring, cam, sprocket, chain, piston, rod, material, and lighting dimensions never feed back into the controller or Core engineering state.

## Reusable procedural geometry

`ProceduralEngineMeshFactory` now provides:

- rounded-rectangle, ellipse, and smoothed arbitrary polygon profiles
- profile transformation and tapered multi-ring lofts along the engine axis
- shared-vertex smooth perimeter normals with separately faceted end caps
- elliptical swept tubes along curved paths
- the earlier rings, sectioned liners, extruded profiles, and limited beveled-box helper

The active block, lower ladder, oil pan, head, cam cover, timing case, and bellhousing region use lofted or swept profiles as their primary silhouettes. Small fasteners and limited local details may still use primitives where their real shape is itself cylindrical or where added tessellation would not improve the teaching view.

## Casting assumptions

| Presentation quantity | v2 assumption | Scope |
|---|---:|---|
| Cylinder spacing | `1.15 × bore` | Existing inline-four presentation layout |
| Block depth | minimum `1.50 × bore`, expanded by crank/big-end envelope | Retained-wall clearance around the rotating assembly |
| Head depth | `1.68 × bore` | Paired ports, 20° included valve angle, and DOHC carrier |
| Head height | `0.82 × bore` | Compact pent-roof DOHC package |
| Piston diameter | `0.97 × bore` | Readable running clearance, not a tolerance model |
| Maximum valve lift | `0.105 × bore` | Approximately 9.0 mm at the 86 mm baseline |
| Main / cam bearing count | five / five per cam | Conventional compact inline-four presentation |
| Lower structure | split lower ladder plus wet sump | Visual stiffness/oil-volume context only |

The block uses a continuous rounded loft with deeper cylinder-centre rings and narrowed inter-bore valleys. Separate curved external bay blends, five lower main-bay forms, core/accessory bosses, ribs, timing-case continuity, rear flange, sump rail, and a tapered oil pan break up the casting without claiming structural or manufacturing validation.

The head uses a tapered rounded casting loft, real visual deck thickness, paired port-side bulges and mouths, a rounded cam-carrier region, and a swept dark cam cover. No intake manifold, exhaust manifold, turbocharger, accessory drive, cooling plumbing, or production fastener inventory is included.

## Ports and airflow teaching path

Each cylinder has two intake valves and two exhaust valves. Each valve has a bowl and a curved runner linking its seat region to an external opening, producing eight intake and eight exhaust openings in total.

Cutaway, transparent, and valvetrain views can show translucent blue intake paths and orange exhaust paths. These tubes and markers are direction aids only. They do not calculate mass flow, tumble, pressure loss, valve curtain area, gas temperature, acoustics, or combustion.

## Valve timing and four-stroke cycle

`ValveTimingKinematics` is a Unity-independent reduced-order model using crank degrees over a 720-degree cycle.

Cylinder firing TDC references use the original teaching order `1-3-4-2`:

- cylinder 1: `0°`
- cylinder 3: `180°`
- cylinder 4: `360°`
- cylinder 2: `540°`

For each cylinder-local cycle:

- power: `0–180°`
- exhaust: `180–360°`
- intake: `360–540°`
- compression: `540–720°`

The idealized fixed timing events are:

| Event | Opening | Peak lift | Closing |
|---|---:|---:|---:|
| Exhaust | `140°` | `255°` | `370°` |
| Intake | `350°` | `465°` | `580°` |

Lift follows a smooth half-cosine opening and closing curve with zero velocity at the endpoints and peak. A direct bucket is now positioned geometrically between every lobe and valve, but this remains deterministic teaching kinematics rather than cam-design, lash/contact-force, spring-force, VTC, VTEC, deformation, surge, or dynamic simulation.

Both camshafts rotate at exactly half crankshaft angular speed. Presentation clocking offsets are `+105°` intake and `-15°` exhaust. Valve stems, heads, and retainers translate along their valve axes; spring stacks shorten visually with lift.

## Timing drive

The current serialized `TimingDrivePresentationDefinition` selects a chain drive with:

- 24-tooth crank sprocket
- 48-tooth intake and exhaust sprockets/phasers
- fixed and tensioning guides
- a hydraulic tensioner body
- a continuous reduced chain path and moving visual markers
- a rounded front timing-cover volume continuous with block and head

The tooth definition exposes the `0.5` cam/crank speed ratio. Belt and gear values exist as future definition types; v2 renders only the chain definition. It does not simulate individual link articulation, chain polygonal action, tension, wear, lubrication, backlash, phaser hydraulics, or torsional vibration.

## Inspection and validity

The five modes remain Full Engine, Cutaway, Transparent Block / Head, Rotating Assembly Only, and Valvetrain Only. The crank scrubber now covers the complete 720-degree cycle. Optional chamber highlights identify intake, compression, power, and exhaust without simulating combustion.

Validity limits:

- four-cylinder inline layout only
- plausible compact passenger-car bore/stroke/rod proportions
- fixed idealized valve timing and lift profile
- teaching/game geometry rather than OEM CAD, casting feasibility, stress, mass, flow, thermal, lubrication, general collision detection, or tolerance analysis; only the documented crankcase rotating-envelope clearance is asserted
- timing chain only in the current renderer

At 86 mm bore × 86 mm stroke × 143 mm rod length, authoritative displacement remains approximately `1.9982288569 L`, slider-crank locations remain unchanged, and all presentation geometry is disposable and rebuildable.
